using Microsoft.Extensions.Logging;
using PrintHero.Core.Data;
using PrintHero.Core.Models;
using PrintHero.Core.Services;
using Serilog;
using System.Data;

namespace PrintHero.Console;

class Program
{
    private static DatabaseService? _databaseService;
    private static MonitoredFolderService? _folderService;
    private static SettingsService? _settingsService;
    private static PrintingService? _printingService;
    private static FileMonitoringService? _fileMonitoringService;
    private static ILogger<Program>? _logger;

    static async Task Main(string[] args)
    {
        // Setup logging
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PrintHero", "logs", "console-.log"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
        _logger = loggerFactory.CreateLogger<Program>();

        try
        {
            _logger.LogInformation("PrintHero Console Manager starting...");

            // Initialize services
            await InitializeServicesAsync();

            // Show menu
            await ShowMainMenuAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in PrintHero Console Manager");
            System.Console.WriteLine($"Fatal error: {ex.Message}");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task InitializeServicesAsync()
    {
        _databaseService = new DatabaseService(_logger as ILogger<DatabaseService>);
        _folderService = new MonitoredFolderService(_databaseService, _logger as ILogger<MonitoredFolderService>);
        _settingsService = new SettingsService(_databaseService, _logger as ILogger<SettingsService>);
        _printingService = new PrintingService(_logger as ILogger<PrintingService>);
        _fileMonitoringService = new FileMonitoringService(_databaseService, _printingService, _logger as ILogger<FileMonitoringService>);

        _logger?.LogInformation("Services initialized successfully");
    }

    private static async Task ShowMainMenuAsync()
    {
        while (true)
        {
            System.Console.Clear();
            System.Console.WriteLine("=== PrintHero Console Manager ===");
            System.Console.WriteLine();
            System.Console.WriteLine("1. View Monitored Folders");
            System.Console.WriteLine("2. Add Monitored Folder");
            System.Console.WriteLine("3. Remove Monitored folder");
            System.Console.WriteLine("4. View Print Jobs");
            System.Console.WriteLine("5. View Settings");
            System.Console.WriteLine("6. Update Printer Settings");
            System.Console.WriteLine("7. Start Monitoring (Test Mode)");
            System.Console.WriteLine("8. Stop Monitoring");
            System.Console.WriteLine("9. View Statistics");
            System.Console.WriteLine("0. Exit");
            System.Console.WriteLine();
            System.Console.Write("Select an option: ");

            var input = System.Console.ReadLine();

            try
            {
                switch (input)
                {
                    case "1":
                        await ViewMonitoredFoldersAsync();
                        break;
                    case "2":
                        await AddMonitoredFolderAsync();
                        break;
                    case "3":
                        await RemoveMonitoredFolderAsync();
                        break;
                    case "4":
                        await ViewPrintJobsAsync();
                        break;
                    case "5":
                        await ViewSettingsAsync();
                        break;
                    case "6":
                        await UpdatePrinterSettingsAsync();
                        break;
                    case "7":
                        await StartMonitoringAsync();
                        break;
                    case "8":
                        await StopMonitoringAsync();
                        break;
                    case "9":
                        await ViewStatisticsAsync();
                        break;
                    case "0":
                        System.Console.WriteLine("Exiting...");
                        return;
                    default:
                        System.Console.WriteLine("Invalid option. Press any key to continue...");
                        System.Console.ReadKey();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing menu option: {Option}", input);
                System.Console.WriteLine($"Error: {ex.Message}");
                System.Console.WriteLine("Press any key to continue...");
                System.Console.ReadKey();
            }
        }
    }

    private static async Task ViewMonitoredFoldersAsync()
    {
        System.Console.Clear();
        System.Console.WriteLine("=== Monitored Folders ===");
        System.Console.WriteLine();

        var folders = await _folderService!.GetAllFoldersAsync();

        if (!folders.Any())
        {
            System.Console.WriteLine("No monitored folders configured.");
        }
        else
        {
            System.Console.WriteLine($"{"ID",-5} {"Active",-8} {"Path",-50} {"Pattern",-10} {"Action",-15}");
            System.Console.WriteLine(new string('-', 88));

            foreach (var folder in folders)
            {
                var status = folder.IsActive ? "Yes" : "No";
                var action = folder.PostPrintAction.ToString();

                System.Console.WriteLine($"{folder.Id,-5} {status,-8} {folder.FolderPath,-50} {folder.FilePattern,-10} {action,-15}");
            }
        }

        System.Console.WriteLine();
        System.Console.WriteLine("Press any key to continue...");
        System.Console.ReadKey();
    }

    private static async Task AddMonitoredFolderAsync()
    {
        System.Console.Clear();
        System.Console.WriteLine("=== Add Monitored Folder ===");
        System.Console.WriteLine();

        System.Console.Write("Enter folder path: ");
        var folderPath = System.Console.ReadLine();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            System.Console.WriteLine("Invalid folder path.");
            System.Console.ReadKey();
            return;
        }

        if (!Directory.Exists(folderPath))
        {
            System.Console.Write("Folder doesn't exist. Create it? (y/n): ");
            var create = System.Console.ReadLine();
            if (create?.ToLowerInvariant() == "y")
            {
                try
                {
                    Directory.CreateDirectory(folderPath);
                    System.Console.WriteLine("Folder created successfully.");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Failed to create folder: {ex.Message}");
                    System.Console.ReadKey();
                    return;
                }
            }
            else
            {
                return;
            }
        }

        System.Console.Write("File pattern (default: *.pdf): ");
        var pattern = System.Console.ReadLine();
        if (string.IsNullOrWhiteSpace(pattern))
            pattern = "*.pdf";

        System.Console.Write("Include subfolders? (y/n): ");
        var includeSubfolders = System.Console.ReadLine()?.ToLowerInvariant() == "y";

        System.Console.WriteLine();
        System.Console.WriteLine("Post-print action:");
        System.Console.WriteLine("1. Move to 'Processed' subfolder");
        System.Console.WriteLine("2. Delete file");
        System.Console.WriteLine("3. Keep file");
        System.Console.Write("Select action (1-3): ");

        var actionInput = System.Console.ReadLine();
        var postPrintAction = actionInput switch
        {
            "2" => PostPrintAction.DeleteFile,
            "3" => PostPrintAction.KeepFile,
            _ => PostPrintAction.MoveToSubfolder
        };

        var folder = new MonitoredFolder
        {
            FolderPath = folderPath,
            FilePattern = pattern,
            IncludeSubfolders = includeSubfolders,
            PostPrintAction = postPrintAction,
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        var folderId = await _folderService!.AddFolderAsync(folder);

        if (folderId > 0)
        {
            System.Console.WriteLine($"Monitored folder added successfully (ID: {folderId})");
        }
        else
        {
            System.Console.WriteLine("Failed to add monitored folder.");
        }

        System.Console.WriteLine("Press any key to continue...");
        System.Console.ReadKey();
    }

    private static async Task RemoveMonitoredFolderAsync()
    {
        System.Console.Clear();
        System.Console.WriteLine("=== Remove Monitored Folder ===");
        System.Console.WriteLine();

        await ViewMonitoredFoldersAsync();

        System.Console.Write("Enter folder ID to remove: ");
        var idInput = System.Console.ReadLine();

        if (int.TryParse(idInput, out int folderId))
        {
            var success = await _folderService!.DeleteFolderAsync(folderId);

            if (success)
            {
                System.Console.WriteLine("Monitored folder removed successfully.");
            }
            else
            {
                System.Console.WriteLine("Failed to remove monitored folder or folder not found.");
            }
        }
        else
        {
            System.Console.WriteLine("Invalid folder ID.");
        }

        System.Console.WriteLine("Press any key to continue...");
        System.Console.ReadKey();
    }

    private static async Task ViewPrintJobsAsync()
    {
        System.Console.Clear();
        System.Console.WriteLine("=== Recent Print Jobs ===");
        System.Console.WriteLine();

        try
        {
            using var connection = _databaseService!.GetConnection();
            await connection.OpenAsync();

            var sql = @"SELECT Id, FileName, PrinterName, CreatedAt, PrintedAt, Status, ErrorMessage 
                       FROM PrintJobs 
                       ORDER BY CreatedAt DESC 
                       LIMIT 20";

            using var command = new System.Data.SQLite.SQLiteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            System.Console.WriteLine($"{"ID",-5} {"File Name",-30} {"Printer",-20} {"Created",-20} {"Status",-12}");
            System.Console.WriteLine(new string('-', 87));

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32("Id");
                var fileName = reader.GetString("FileName");
                var printer = reader.GetString("PrinterName");
                var created = DateTime.Parse(reader.GetString("CreatedAt"));
                var status = ((PrintJobStatus)reader.GetInt32("Status")).ToString();

                if (fileName.Length > 30) fileName = fileName.Substring(0, 27) + "...";
                if (printer.Length > 20) printer = printer.Substring(0, 17) + "...";

                System.Console.WriteLine($"{id,-5} {fileName,-30} {printer,-20} {created:MM/dd HH:mm}         {status,-12}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading print jobs: {ex.Message}");
        }

        System.Console.WriteLine();
        System.Console.WriteLine("Press any key to continue...");
        System.Console.ReadKey();
    }

    private static async Task ViewSettingsAsync()
    {
        System.Console.Clear();
        System.Console.WriteLine("=== Current Settings ===");
        System.Console.WriteLine();

        try
        {
            var defaultPrinter = await _settingsService!.GetDefaultPrinterAsync();
            var paperSize = await _settingsService.GetPaperSizeAsync();

            System.Console.WriteLine($"Default Printer: {defaultPrinter}");
            System.Console.WriteLine($"Paper Size: {paperSize}");

            // Show available printers
            System.Console.WriteLine();
            System.Console.WriteLine("Available Printers:");
            var printers = System.Drawing.Printing.PrinterSettings.InstalledPrinters;
            for (int i = 0; i < printers.Count; i++)
            {
                var marker = printers[i] == defaultPrinter ? " [DEFAULT]" : "";
                System.Console.WriteLine($"  {i + 1}. {printers[i]}{marker}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading settings: {ex.Message}");
        }

        System.Console.WriteLine();
        System.Console.WriteLine("Press any key to continue...");
        System.Console.ReadKey();
    }

    private static async Task UpdatePrinterSettingsAsync()
    {
        System.Console.Clear();
        System.Console.WriteLine("=== Update Printer Settings ===");
        System.Console.WriteLine();

        try
        {
            var printers = System.Drawing.Printing.PrinterSettings.InstalledPrinters;

            System.Console.WriteLine("Available Printers:");
            for (int i = 0; i < printers.Count; i++)
            {
                System.Console.WriteLine($"{i + 1}. {printers[i]}");
            }

            System.Console.Write($"Select printer (1-{printers.Count}): ");
            var input = System.Console.ReadLine();

            if (int.TryParse(input, out int selection) && selection >= 1 && selection <= printers.Count)
            {
                var selectedPrinter = printers[selection - 1];
                await _settingsService!.SetDefaultPrinterAsync(selectedPrinter);
                System.Console.WriteLine($"Default printer updated to: {selectedPrinter}");
            }
            else
            {
                System.Console.WriteLine("Invalid selection.");
            }

            System.Console.WriteLine();
            System.Console.Write("Update paper size? (current: ");
            System.Console.Write(await _settingsService!.GetPaperSizeAsync());
            System.Console.Write(") (y/n): ");

            if (System.Console.ReadLine()?.ToLowerInvariant() == "y")
            {
                System.Console.WriteLine("Paper sizes: 1=A4, 2=Letter, 3=A3, 4=Legal");
                System.Console.Write("Select (1-4): ");
                var sizeInput = System.Console.ReadLine();

                var paperSize = sizeInput switch
                {
                    "2" => "Letter",
                    "3" => "A3",
                    "4" => "Legal",
                    _ => "A4"
                };

                await _settingsService.SetPaperSizeAsync(paperSize);
                System.Console.WriteLine($"Paper size updated to: {paperSize}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error updating settings: {ex.Message}");
        }

        System.Console.WriteLine("Press any key to continue...");
        System.Console.ReadKey();
    }

    private static async Task StartMonitoringAsync()
    {
        System.Console.Clear();
        System.Console.WriteLine("=== Start Monitoring (Test Mode) ===");
        System.Console.WriteLine();

        try
        {
            await _fileMonitoringService!.StartMonitoringAsync(null);
            System.Console.WriteLine("File monitoring started successfully!");
            System.Console.WriteLine("Monitoring will continue until you stop it or exit the application.");
            System.Console.WriteLine();
            System.Console.WriteLine("Press any key to return to menu (monitoring will continue in background)...");
            System.Console.ReadKey();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Failed to start monitoring: {ex.Message}");
            System.Console.WriteLine("Press any key to continue...");
            System.Console.ReadKey();
        }
    }

    private static async Task StopMonitoringAsync()
    {
        System.Console.Clear();
        System.Console.WriteLine("=== Stop Monitoring ===");
        System.Console.WriteLine();

        try
        {
            await _fileMonitoringService!.StopMonitoringAsync();
            System.Console.WriteLine("File monitoring stopped successfully!");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error stopping monitoring: {ex.Message}");
        }

        System.Console.WriteLine("Press any key to continue...");
        System.Console.ReadKey();
    }

    private static async Task ViewStatisticsAsync()
    {
        System.Console.Clear();
        System.Console.WriteLine("=== Statistics ===");
        System.Console.WriteLine();

        try
        {
            using var connection = _databaseService!.GetConnection();
            await connection.OpenAsync();

            // Today's stats
            var today = DateTime.Today.ToString("yyyy-MM-dd");

            var todayProcessed = await GetCountAsync(connection,
                $"SELECT COUNT(*) FROM PrintJobs WHERE Status = 2 AND DATE(CreatedAt) = '{today}'");
            var todayErrors = await GetCountAsync(connection,
                $"SELECT COUNT(*) FROM PrintJobs WHERE Status = 3 AND DATE(CreatedAt) = '{today}'");
            var todayPending = await GetCountAsync(connection,
                $"SELECT COUNT(*) FROM PrintJobs WHERE Status = 0 AND DATE(CreatedAt) = '{today}'");

            // Total stats
            var totalProcessed = await GetCountAsync(connection, "SELECT COUNT(*) FROM PrintJobs WHERE Status = 2");
            var totalErrors = await GetCountAsync(connection, "SELECT COUNT(*) FROM PrintJobs WHERE Status = 3");
            var totalPending = await GetCountAsync(connection, "SELECT COUNT(*) FROM PrintJobs WHERE Status = 0");

            // Active folders
            var activeFolders = await GetCountAsync(connection, "SELECT COUNT(*) FROM MonitoredFolders WHERE IsActive = 1");

            System.Console.WriteLine("=== Today's Statistics ===");
            System.Console.WriteLine($"Files Processed: {todayProcessed}");
            System.Console.WriteLine($"Print Errors: {todayErrors}");
            System.Console.WriteLine($"Pending Jobs: {todayPending}");
            System.Console.WriteLine();

            System.Console.WriteLine("=== Total Statistics ===");
            System.Console.WriteLine($"Files Processed: {totalProcessed}");
            System.Console.WriteLine($"Print Errors: {totalErrors}");
            System.Console.WriteLine($"Pending Jobs: {totalPending}");
            System.Console.WriteLine();

            System.Console.WriteLine("=== Configuration ===");
            System.Console.WriteLine($"Active Monitored Folders: {activeFolders}");
            System.Console.WriteLine($"Default Printer: {await _settingsService!.GetDefaultPrinterAsync()}");
            System.Console.WriteLine($"Paper Size: {await _settingsService.GetPaperSizeAsync()}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading statistics: {ex.Message}");
        }

        System.Console.WriteLine();
        System.Console.WriteLine("Press any key to continue...");
        System.Console.ReadKey();
    }

    private static async Task<int> GetCountAsync(System.Data.SQLite.SQLiteConnection connection, string sql)
    {
        using var command = new System.Data.SQLite.SQLiteCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}