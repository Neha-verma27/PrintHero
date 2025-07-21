using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Models;
using PrintHero.Core.Services;
using PrintHero.Core.Data;
using System.IO;
using System.Drawing.Printing;

namespace PrintHero.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IFileMonitoringService _fileMonitoringService;
        private readonly IPrintingService _printingService;
        private readonly IAppSettingsService _appSettingsService;
        private readonly ILogger<MainViewModel>? _logger;
        private readonly JsonConfigService? _jsonConfigService;
        private readonly SqliteDatabaseService? _databaseService;

        public event PropertyChangedEventHandler? PropertyChanged;

        private int _filesProcessedToday;
        private string? _defaultPrinter;
        private bool _isServiceRunning;
        private string _paperSize = "A4";
        private bool _autoStartService = true; // Always default to auto-start

        private int _printingErrorsToday;


        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void NotifyPropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        public MainViewModel()
        {
            // Default constructor for design-time and fallback
            MonitoredFolders = new ObservableCollection<MonitoredFolder>();
            FilesProcessedToday = 0;
            PrintingErrorsToday = 0;
            DefaultPrinter = "No printer selected";
            IsServiceRunning = false;
            
            // Set up basic folder even in fallback mode
            SetupDefaultPrintingFolder();
            SetupDefaultPrinter();

            StartServiceCommand = new RelayCommand(async () => await StartService());
            StopServiceCommand = new RelayCommand(async () => await StopService());
        }

        public MainViewModel(IFileMonitoringService fileMonitoringService,
                           IPrintingService printingService,
                           IAppSettingsService appSettingsService,
                           ILogger<MainViewModel>? logger = null,
                           JsonConfigService? jsonConfigService = null,
                           SqliteDatabaseService? databaseService = null) : this()
        {
            _fileMonitoringService = fileMonitoringService;
            _printingService = printingService;
            _appSettingsService = appSettingsService;
            _logger = logger;
            _jsonConfigService = jsonConfigService;
            _databaseService = databaseService;

            // Subscribe to file processing events
            _fileMonitoringService.FileProcessed += OnFileProcessed;

            // Default printer will be set up after loading settings

            // Initialize async components - will be awaited by UI
            InitializationTask = InitializeAsync();
        }


        public int FilesProcessedToday
        {
            get => _filesProcessedToday;
            set
            {
                if (_filesProcessedToday != value)
                {
                    _filesProcessedToday = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PrintingErrorsToday
        {
            get => _printingErrorsToday;
            set
            {
                if (_printingErrorsToday != value)
                {
                    _printingErrorsToday = value;
                    OnPropertyChanged();
                }
            }
        }
        public string? DefaultPrinter
        {
            get => _defaultPrinter;
            set => SetProperty(ref _defaultPrinter, value);
        }

        public string PaperSize
        {
            get => _paperSize;
            set => SetProperty(ref _paperSize, value);
        }

        public string FirstMonitoredFolder
        {
            get => MonitoredFolders?.FirstOrDefault()?.FolderPath ?? "No folder selected";
        }

        public bool IsServiceRunning
        {
            get => _isServiceRunning;
            set
            {
                if (SetProperty(ref _isServiceRunning, value))
                {
                    _logger?.LogInformation("IsServiceRunning changed to: {IsRunning}", value);
                }
            }
        }



        public ObservableCollection<MonitoredFolder> MonitoredFolders { get; }

        public ICommand StartServiceCommand { get; }
        public ICommand StopServiceCommand { get; }
        
        public Task InitializationTask { get; private set; } = Task.CompletedTask;

        private async Task InitializeAsync()
        {
            try
            {
                _logger?.LogInformation("Starting ViewModel initialization...");
                
                // Load settings first
                await LoadSettingsAsync();
                
                // Force refresh all UI properties
                RefreshAllUIProperties();
                
                _logger?.LogInformation("ViewModel initialization completed successfully");
                _logger?.LogInformation("Final state - Printer: {Printer}, IsServiceRunning: {IsRunning}, Folders: {FolderCount}", 
                    DefaultPrinter, IsServiceRunning, MonitoredFolders.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during ViewModel initialization");
            }
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                if (_appSettingsService == null) return;

                var settings = await _appSettingsService.LoadSettingsAsync();

                DefaultPrinter = settings.DefaultPrinter;
                PaperSize = settings.PaperSize;
                _autoStartService = settings.AutoStartService;
                
                // Set up default printer if none is configured
                if (string.IsNullOrEmpty(DefaultPrinter) || DefaultPrinter == "No printer selected")
                {
                    SetupDefaultPrinter();
                    
                    // Save the auto-detected printer setting
                    if (!string.IsNullOrEmpty(DefaultPrinter) && DefaultPrinter != "No printer selected")
                    {
                        _ = Task.Run(async () => await SaveSettingsAsync());
                    }
                }
                // Load daily statistics from database
                await LoadDailyStatisticsAsync();
                
                // Fall back to settings if database is not available
                if (_databaseService == null)
                {
                    FilesProcessedToday = ShouldResetDailyStats(settings) ? 0 : settings.FilesProcessedToday;
                    PrintingErrorsToday = ShouldResetDailyStats(settings) ? 0 : settings.PrintingErrors;
                }

                // Load MonitoredFolders from JsonConfigService
                MonitoredFolders.Clear();
                if (_jsonConfigService != null)
                {
                    var monitoredFolders = await _jsonConfigService.GetMonitoredFoldersAsync();
                    foreach (var folder in monitoredFolders)
                    {
                        MonitoredFolders.Add(folder);
                    }
                    _logger?.LogInformation("Loaded {Count} monitored folders from JsonConfigService", monitoredFolders.Count);
                    OnPropertyChanged(nameof(FirstMonitoredFolder)); // Notify UI of folder change
                }
                else
                {
                    // Fallback to settings.MonitoredFolders if JsonConfigService not available
                    foreach (var folder in settings.MonitoredFolders)
                    {
                        MonitoredFolders.Add(folder);
                    }
                    _logger?.LogInformation("Loaded {Count} monitored folders from AppSettingsService", settings.MonitoredFolders.Count);
                    OnPropertyChanged(nameof(FirstMonitoredFolder)); // Notify UI of folder change
                }

                // Set up default printing folder after loading existing folders
                SetupDefaultPrintingFolder();
                
                _logger?.LogInformation("Settings loaded successfully - Printer: {Printer}, AutoStart: {AutoStart}, MonitoredFolders: {FolderCount}", 
                    DefaultPrinter, _autoStartService, MonitoredFolders.Count);
                
                // Always auto-start the service (this is the desired behavior)
                _logger?.LogInformation("Starting PrintHero monitoring service automatically...");
                await AutoStartServiceAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load settings");
            }
        }

        private bool ShouldResetDailyStats(AppSettings settings)
        {
            return settings.LastResetDate.Date < DateTime.Today;
        }

        private async Task LoadDailyStatisticsAsync()
        {
            try
            {
                if (_databaseService == null)
                {
                    _logger?.LogWarning("Database service not available for loading daily statistics");
                    return;
                }

                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                // Query completed print jobs for today
                var todayCompletedJobs = await _databaseService.ExecuteScalarAsync<int?>(
                    @"SELECT COUNT(*) FROM PrintJobs 
                      WHERE Status = 2 
                      AND DATE(CreatedAt) = DATE(@Today)",
                    new Microsoft.Data.Sqlite.SqliteParameter("@Today", today.ToString("yyyy-MM-dd")));

                FilesProcessedToday = todayCompletedJobs ?? 0;

                // Query failed print jobs for today
                var todayFailedJobs = await _databaseService.ExecuteScalarAsync<int?>(
                    @"SELECT COUNT(*) FROM PrintJobs 
                      WHERE Status = 3 
                      AND DATE(CreatedAt) = DATE(@Today)",
                    new Microsoft.Data.Sqlite.SqliteParameter("@Today", today.ToString("yyyy-MM-dd")));

                PrintingErrorsToday = todayFailedJobs ?? 0;

                _logger?.LogInformation("Loaded daily statistics: {ProcessedCount} processed, {ErrorCount} errors", 
                    FilesProcessedToday, PrintingErrorsToday);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load daily statistics from database");
                FilesProcessedToday = 0;
                PrintingErrorsToday = 0;
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                if (_appSettingsService == null) return;

                var settings = new AppSettings
                {
                    DefaultPrinter = DefaultPrinter ?? string.Empty,
                    PaperSize = PaperSize,
                    FilesProcessedToday = FilesProcessedToday,
                    PrintingErrors = PrintingErrorsToday,
                    MonitoredFolders = new List<MonitoredFolder>(), // Empty since we store these separately
                    AutoStartService = IsServiceRunning,
                    LastResetDate = DateTime.Today
                };

                await _appSettingsService.SaveSettingsAsync(settings);

                // Save MonitoredFolders to JsonConfigService
                if (_jsonConfigService != null)
                {
                    await _jsonConfigService.SaveMonitoredFoldersAsync(MonitoredFolders.ToList());
                    _logger?.LogInformation("Saved {Count} monitored folders to JsonConfigService", MonitoredFolders.Count);
                }

                _logger?.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save settings");
            }
        }

        private async Task StartService()
        {
            try
            {
                if (_fileMonitoringService == null)
                {
                    _logger?.LogWarning("File monitoring service not available - running in limited mode");
                    IsServiceRunning = false;
                    return;
                }

                // Update printer settings and enable printing
                if (!string.IsNullOrEmpty(DefaultPrinter))
                {
                    _printingService?.SetPrinterSettings(DefaultPrinter, PaperSize, "Portrait");
                    _logger?.LogInformation("Configured printer: {Printer}", DefaultPrinter);
                }
                else
                {
                    _logger?.LogWarning("No printer configured");
                }
                
                if (_printingService != null)
                {
                    _printingService.IsEnabled = true;
                    _logger?.LogInformation("Printing service enabled");
                }

                // Ensure we have folders to monitor
                if (!MonitoredFolders.Any())
                {
                    _logger?.LogWarning("No folders to monitor - service cannot start");
                    IsServiceRunning = false;
                    return;
                }

                // Start monitoring
                _logger?.LogInformation("Starting file monitoring with {Count} folders, DefaultPrinter: {Printer}", 
                    MonitoredFolders.Count, DefaultPrinter);
                await _fileMonitoringService.StartMonitoringAsync(MonitoredFolders);
                IsServiceRunning = true;

                await SaveSettingsAsync();
                _logger?.LogInformation("✅ File monitoring service started successfully - IsServiceRunning: {IsRunning}", IsServiceRunning);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Failed to start service");
                IsServiceRunning = false;
            }
        }

        private async Task StopService()
        {
            try
            {
                if (_fileMonitoringService == null)
                {
                    _logger?.LogWarning("File monitoring service not available");
                    return;
                }

                // Disable printing
                if (_printingService != null)
                {
                    _printingService.IsEnabled = false;
                }

                await _fileMonitoringService.StopMonitoringAsync();
                IsServiceRunning = false;

                await SaveSettingsAsync();
                _logger?.LogInformation("File monitoring service stopped");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to stop service");
            }
        }

        private async void OnFileProcessed(object? sender, FileProcessedEventArgs e)
        {
            try
            {
                if (e.Success)
                {
                    _logger?.LogInformation($"File processed successfully: {e.FilePath}");
                }
                else
                {
                    _logger?.LogError($"File processing failed: {e.FilePath} - {e.ErrorMessage}");
                }

                // Refresh daily statistics from database to get accurate counts
                await LoadDailyStatisticsAsync();
                
                // Save updated stats
                await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling file processed event");
            }
        }


        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public async Task RefreshStatisticsAsync()
        {
            await LoadDailyStatisticsAsync();
            _logger?.LogInformation($"Statistics refreshed: {FilesProcessedToday} processed, {PrintingErrorsToday} errors");
        }

        private void RefreshAllUIProperties()
        {
            OnPropertyChanged(nameof(IsServiceRunning));
            OnPropertyChanged(nameof(DefaultPrinter));
            OnPropertyChanged(nameof(PaperSize));
            OnPropertyChanged(nameof(FirstMonitoredFolder));
            OnPropertyChanged(nameof(FilesProcessedToday));
            OnPropertyChanged(nameof(PrintingErrorsToday));
        }

        private void SetupDefaultPrintingFolder()
        {
            try
            {
                // Define the default printing folder path
                string defaultPrintingPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    "Printing");

                // Always ensure the directory exists
                if (!Directory.Exists(defaultPrintingPath))
                {
                    Directory.CreateDirectory(defaultPrintingPath);
                    _logger?.LogInformation("📁 Created default printing folder: {Path}", defaultPrintingPath);
                }
                else
                {
                    _logger?.LogInformation("📁 Default printing folder already exists: {Path}", defaultPrintingPath);
                }

                // Check if we already have this folder in our monitored folders
                bool folderExists = MonitoredFolders.Any(f => 
                    string.Equals(f.FolderPath, defaultPrintingPath, StringComparison.OrdinalIgnoreCase));

                if (!folderExists)
                {
                    // Add the default folder to monitored folders
                    var defaultFolder = new MonitoredFolder
                    {
                        FolderPath = defaultPrintingPath,
                        FilePattern = "*.pdf", // Default to PDF files
                        IncludeSubfolders = false,
                        PostPrintAction = PostPrintAction.MoveToSubfolder,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };

                    MonitoredFolders.Add(defaultFolder);
                    OnPropertyChanged(nameof(FirstMonitoredFolder)); // Notify UI of folder change
                    
                    // Save the new folder to JsonConfigService
                    Task.Run(async () =>
                    {
                        try
                        {
                            if (_jsonConfigService != null)
                            {
                                await _jsonConfigService.AddMonitoredFolderAsync(defaultFolder);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to save default folder to JsonConfigService");
                        }
                    });
                    
                    _logger?.LogInformation($"Added default printing folder to monitoring: {defaultPrintingPath}");
                }
                else
                {
                    _logger?.LogInformation($"Default printing folder already exists in monitored folders: {defaultPrintingPath}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to set up default printing folder");
            }
        }

        private void SetupDefaultPrinter()
        {
            try
            {
                // Check if we already have a printer set
                if (!string.IsNullOrEmpty(DefaultPrinter) && DefaultPrinter != "No printer selected")
                {
                    _logger?.LogInformation($"Printer already configured: {DefaultPrinter}");
                    return;
                }

                // Get the system default printer first
                var systemDefaultPrinter = GetSystemDefaultPrinter();
                if (!string.IsNullOrEmpty(systemDefaultPrinter))
                {
                    DefaultPrinter = systemDefaultPrinter;
                    _logger?.LogInformation($"Set system default printer: {systemDefaultPrinter}");
                    return;
                }

                // If no system default, get the first available printer
                var availablePrinters = GetAvailablePrinters();
                if (availablePrinters.Any())
                {
                    var firstPrinter = availablePrinters.First();
                    DefaultPrinter = firstPrinter;
                    _logger?.LogInformation($"Set first available printer: {firstPrinter}");
                    return;
                }

                // No printers found
                DefaultPrinter = "No printer found";
                _logger?.LogWarning("No printers found on the system");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to setup default printer");
                DefaultPrinter = "Error detecting printer";
            }
        }

        private string? GetSystemDefaultPrinter()
        {
            try
            {
                var printerSettings = new System.Drawing.Printing.PrinterSettings();
                var defaultPrinter = printerSettings.PrinterName;
                
                if (!string.IsNullOrEmpty(defaultPrinter))
                {
                    _logger?.LogDebug($"System default printer found: {defaultPrinter}");
                    return defaultPrinter;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting system default printer");
            }
            return null;
        }

        private List<string> GetAvailablePrinters()
        {
            var printers = new List<string>();
            try
            {
                foreach (string printerName in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    // Verify the printer is actually available
                    if (IsPrinterValid(printerName))
                    {
                        printers.Add(printerName);
                        _logger?.LogDebug($"Available printer found: {printerName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting available printers");
            }
            return printers;
        }

        private bool IsPrinterValid(string printerName)
        {
            try
            {
                var printerSettings = new System.Drawing.Printing.PrinterSettings();
                printerSettings.PrinterName = printerName;
                return printerSettings.IsValid;
            }
            catch
            {
                return false;
            }
        }

        private async Task AutoStartServiceAsync()
        {
            try
            {
                _logger?.LogInformation("PrintHero auto-start initiated...");
                
                // Brief delay to ensure all services are ready
                await Task.Delay(300);
                
                // Ensure we have the required services
                if (_fileMonitoringService == null)
                {
                    _logger?.LogError("Cannot auto-start: FileMonitoringService is null - this is a critical error");
                    return;
                }
                
                if (_printingService == null)
                {
                    _logger?.LogError("Cannot auto-start: PrintingService is null - this is a critical error");
                    return;
                }
                
                // Ensure we have at least the default folder
                if (MonitoredFolders == null || !MonitoredFolders.Any())
                {
                    _logger?.LogWarning("No monitored folders - creating default folder now");
                    SetupDefaultPrintingFolder();
                }
                
                // Now start the service
                _logger?.LogInformation("Starting PrintHero monitoring with {FolderCount} folders...", MonitoredFolders?.Count ?? 0);
                await StartService();
                
                // Force UI update
                RefreshAllUIProperties();
                
                _logger?.LogInformation("✅ PrintHero auto-start completed - IsServiceRunning: {IsRunning}", IsServiceRunning);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Failed to auto-start PrintHero service - this should not happen");
                
                // Try to recover by ensuring service state is correct
                IsServiceRunning = false;
            }
        }




    }

    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Action? _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;

            _isExecuting = true;
            try
            {
                if (_executeAsync != null)
                {
                    await _executeAsync();
                }
                else
                {
                    _execute?.Invoke();
                }
            }
            finally
            {
                _isExecuting = false;
            }
        }
    }
}