using System.Data;
using System.IO;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Data;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Models;

namespace PrintHero.Core.Services;

public class FileMonitoringService : IFileMonitoringService, IDisposable
{
    private readonly IPrintingService _printingService;
    private readonly DatabaseService _databaseService;
    private readonly ILogger<FileMonitoringService> _logger;
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    private readonly object _lockObject = new();
    private bool _isRunning;

    public event EventHandler<FileProcessedEventArgs>? FileProcessed;

    public FileMonitoringService(DatabaseService databaseService, IPrintingService printingService, ILogger<FileMonitoringService> logger)
    {
        _printingService = printingService;
        _databaseService = databaseService;
        _logger = logger;
    }

    public async Task StartMonitoringAsync(IEnumerable<MonitoredFolder>? folders)
    {
        try
        {
            if (folders == null)
            {
                folders = await GetActiveMonitoredFoldersAsync();
            }

            foreach (var folder in folders)
            {
                await StartMonitoringFolderAsync(folder);
            }

            _logger?.LogInformation($"Started monitoring {folders.Count()} folders");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start monitoring");
            throw;
        }
    }

    private async Task<List<MonitoredFolder>> GetActiveMonitoredFoldersAsync()
    {
        var folders = new List<MonitoredFolder>();

        try
        {
            using var connection = _databaseService.GetConnection();
            await connection.OpenAsync();

            var sql = "SELECT * FROM MonitoredFolders WHERE IsActive = 1";
            using var command = new System.Data.SQLite.SQLiteCommand(sql, connection);
            using var reader = (System.Data.SQLite.SQLiteDataReader)await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                folders.Add(new MonitoredFolder
                {
                    Id = reader.GetInt32("Id"),
                    FolderPath = reader.GetString("FolderPath"),
                    IsActive = reader.GetBoolean("IsActive"),
                    FilePattern = reader.GetString("FilePattern"),
                    IncludeSubfolders = reader.GetBoolean("IncludeSubfolders"),
                    CreatedAt = DateTime.Parse(reader.GetString("CreatedAt")),
                    LastActivity = reader.IsDBNull("LastActivity") ? null : DateTime.Parse(reader.GetString("LastActivity")),
                    PostPrintAction = (PostPrintAction)reader.GetInt32("PostPrintAction"),
                    CustomMoveFolder = reader.IsDBNull("CustomMoveFolder") ? null : reader.GetString("CustomMoveFolder")
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get monitored folders");
        }

        return folders;
    }

    public Task StopMonitoringAsync()
    {
        lock (_lockObject)
        {
            if (!_isRunning)
            {
                _logger.LogWarning("File monitoring is not running");
                return Task.CompletedTask;
            }

            _isRunning = false;
        }

        try
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
            _logger.LogInformation("Stopped file monitoring");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping file monitoring");
        }

        return Task.CompletedTask;
    }

    private async Task StartMonitoringFolderAsync(MonitoredFolder folder)
    {
        try
        {
            if (!Directory.Exists(folder.FolderPath))
            {
                _logger.LogWarning($"Monitored folder does not exist: {folder.FolderPath}");
                return;
            }

            var watcher = new FileSystemWatcher(folder.FolderPath)
            {
                Filter = folder.FilePattern,
                IncludeSubdirectories = folder.IncludeSubfolders,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName
            };

            watcher.Created += async (sender, e) => await OnFileCreated(e, folder);
            watcher.EnableRaisingEvents = true;

            _watchers[folder.FolderPath] = watcher;

            // Process existing files in the folder
            await ProcessExistingFiles(folder);

            _logger.LogInformation($"Started monitoring folder: {folder.FolderPath} with pattern: {folder.FilePattern}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to start monitoring folder: {folder.FolderPath}");
        }
    }

    private async Task ProcessExistingFiles(MonitoredFolder folder)
    {
        try
        {
            var searchOption = folder.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(folder.FolderPath, folder.FilePattern, searchOption);

            foreach (var file in files)
            {
                await ProcessFile(file, folder);
            }

            if (files.Length > 0)
            {
                _logger.LogInformation($"Processed {files.Length} existing files in {folder.FolderPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to process existing files in folder: {folder.FolderPath}");
        }
    }

    private async Task OnFileCreated(FileSystemEventArgs e, MonitoredFolder folder)
    {
        // Add a small delay to ensure file is completely written
        await Task.Delay(1000);
        await ProcessFile(e.FullPath, folder);
    }

    private async Task ProcessFile(string filePath, MonitoredFolder folder)
    {
        try
        {
            if (!await WaitForFileAvailable(filePath))
            {
                _logger.LogWarning($"File is not available for processing: {filePath}");
                return;
            }

            _logger.LogInformation($"Processing file: {filePath}");

            // Print the file
            var success = await _printingService.PrintFileAsync(filePath);

            if (success)
            {
                bool moveSuccess = MoveFileToFolder(filePath, null);
                if (moveSuccess)
                {
                    _logger.LogInformation($"PDF printed and moved successfully: {filePath}");
                }
                else
                {
                    _logger.LogWarning($"PDF printed successfully but failed to move file: {filePath}");
                }

                //await HandlePostPrintAction(filePath, folder);

                FileProcessed?.Invoke(this, new FileProcessedEventArgs
                {
                    FilePath = filePath,
                    Success = true,
                    ProcessedAt = DateTime.Now
                });
            }
            else
            {
                _logger.LogError($"Failed to print file: {filePath}");

                FileProcessed?.Invoke(this, new FileProcessedEventArgs
                {
                    FilePath = filePath,
                    Success = false,
                    ProcessedAt = DateTime.Now,
                    ErrorMessage = "Printing failed"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing file: {filePath}");

            FileProcessed?.Invoke(this, new FileProcessedEventArgs
            {
                FilePath = filePath,
                Success = false,
                ProcessedAt = DateTime.Now,
                ErrorMessage = ex.Message
            });
        }
    }


    private bool MoveFileToFolder(string sourceFilePath, string destinationFolder)
    {
        try
        {
            // Set default destination folder if not provided
            if (string.IsNullOrEmpty(destinationFolder))
            {
                destinationFolder = Path.Combine(Path.GetDirectoryName(sourceFilePath), "Printed");
            }

            // Create destination directory if it doesn't exist
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
                _logger.LogInformation($"Created directory: {destinationFolder}");
            }

            // Get the file name
            string fileName = Path.GetFileName(sourceFilePath);
            string destinationPath = Path.Combine(destinationFolder, fileName);

            // Handle file name conflicts
            destinationPath = GetUniqueFileName(destinationPath);

            // Move the file
            File.Move(sourceFilePath, destinationPath);

            _logger.LogInformation($"File moved from {sourceFilePath} to {destinationPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error moving file: {ex.Message}");
            return false;
        }
    }

    private string GetUniqueFileName(string filePath)
    {
        if (!File.Exists(filePath))
            return filePath;

        string directory = Path.GetDirectoryName(filePath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);

        int counter = 1;
        string newFilePath;

        do
        {
            string newFileName = $"{fileNameWithoutExtension}_{counter}{extension}";
            newFilePath = Path.Combine(directory, newFileName);
            counter++;
        }
        while (File.Exists(newFilePath));

        return newFilePath;
    }

    private async Task<bool> WaitForFileAvailable(string filePath, int maxWaitTimeMs = 10000)
    {
        var startTime = DateTime.Now;
        while (DateTime.Now.Subtract(startTime).TotalMilliseconds < maxWaitTimeMs)
        {
            try
            {
                using (var stream = File.OpenRead(filePath))
                {
                    return true;
                }
            }
            catch (IOException)
            {
                await Task.Delay(500);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(500);
            }
        }
        return false;
    }

    private async Task HandlePostPrintAction(string filePath, MonitoredFolder folder)
    {
        try
        {
            switch (folder.PostPrintAction)
            {
                case PostPrintAction.DeleteFile:
                    File.Delete(filePath);
                    _logger.LogInformation($"Deleted file after printing: {filePath}");
                    break;

                case PostPrintAction.MoveToSubfolder:
                    var printedFolder = Path.Combine(Path.GetDirectoryName(filePath)!, "Printed");
                    Directory.CreateDirectory(printedFolder);
                    var newPath = Path.Combine(printedFolder, Path.GetFileName(filePath));
                    File.Move(filePath, newPath);
                    _logger.LogInformation($"Moved file to printed folder: {newPath}");
                    break;

                case PostPrintAction.MoveToCustomFolder:
                    if (!string.IsNullOrEmpty(folder.CustomMoveFolder))
                    {
                        Directory.CreateDirectory(folder.CustomMoveFolder);
                        var customPath = Path.Combine(folder.CustomMoveFolder, Path.GetFileName(filePath));
                        File.Move(filePath, customPath);
                        _logger.LogInformation($"Moved file to custom folder: {customPath}");
                    }
                    break;

                case PostPrintAction.KeepFile:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to handle post-print action for file: {filePath}");
        }
    }

    public void Dispose()
    {
        StopMonitoringAsync().Wait();
    }
}