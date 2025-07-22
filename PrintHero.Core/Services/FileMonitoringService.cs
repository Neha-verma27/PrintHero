using System.IO;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Models;

namespace PrintHero.Core.Services;

public class FileMonitoringService : IFileMonitoringService, IDisposable
{
    private readonly IPrintingService _printingService;
    private readonly JsonConfigService _jsonConfigService;
    private readonly ILogger<FileMonitoringService> _logger;
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    private readonly object _lockObject = new();
    private bool _isRunning;

    public event EventHandler<FileProcessedEventArgs>? FileProcessed;

    public FileMonitoringService(JsonConfigService jsonConfigService, IPrintingService printingService, ILogger<FileMonitoringService> logger)
    {
        _printingService = printingService;
        _jsonConfigService = jsonConfigService;
        _logger = logger;
    }

    public async Task StartMonitoringAsync(IEnumerable<MonitoredFolder>? folders)
    {
        try
        {
            lock (_lockObject)
            {
                if (_isRunning)
                {
                    _logger?.LogWarning("File monitoring is already running");
                    return;
                }
                _isRunning = true;
            }

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
            lock (_lockObject)
            {
                _isRunning = false;
            }
            throw;
        }
    }

    private async Task<List<MonitoredFolder>> GetActiveMonitoredFoldersAsync()
    {
        try
        {
            var allFolders = await _jsonConfigService.GetMonitoredFoldersAsync();
            return allFolders.Where(f => f.IsActive).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get monitored folders");
            return new List<MonitoredFolder>();
        }
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

        await Task.Delay(1000);
        await ProcessFile(e.FullPath, folder);
    }

    private async Task ProcessFile(string filePath, MonitoredFolder folder)
    {
        try
        {

            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"File no longer exists: {filePath}");
                return;
            }

            if (!IsFileMatchingPattern(filePath, folder.FilePattern))
            {
                _logger.LogDebug($"File does not match pattern {folder.FilePattern}: {filePath}");
                return;
            }

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
                // Only handle post-print action, remove duplicate file movement
                await HandlePostPrintActionAsync(filePath, folder);
                
                _logger.LogInformation($"File processed successfully: {filePath}");

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

            if (string.IsNullOrEmpty(destinationFolder))
            {
                destinationFolder = Path.Combine(Path.GetDirectoryName(sourceFilePath), "Printed");
            }

            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
                _logger.LogInformation($"Created directory: {destinationFolder}");
            }

            string fileName = Path.GetFileName(sourceFilePath);
            string destinationPath = Path.Combine(destinationFolder, fileName);

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

    private async Task HandlePostPrintActionAsync(string filePath, MonitoredFolder folder)
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

    private bool IsFileMatchingPattern(string filePath, string pattern)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);

            if (pattern == "*.*")
                return true;
                
            if (pattern.StartsWith("*."))
            {
                var extension = pattern.Substring(1); // Remove the *
                return filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
            }
            
            // For more complex patterns, use basic wildcard matching
            var regexPattern = pattern.Replace("*", ".*").Replace("?", ".");
            return System.Text.RegularExpressions.Regex.IsMatch(fileName, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error matching file pattern {pattern} for file {filePath}");
            return true; // Default to processing the file if pattern matching fails
        }
    }

    public void Dispose()
    {
        StopMonitoringAsync().Wait();
    }
}