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
        private readonly WindowsServiceController _serviceController;

        public event PropertyChangedEventHandler? PropertyChanged;

        private int _filesProcessedToday;
        private string? _defaultPrinter;
        private bool _isServiceRunning;
        private string _paperSize = "A4";
        private string _orientation = "Portrait";
        private bool _autoStartService = true; // Always default to auto-start
        private bool _isServiceEnabledByUser = true; // Remember user's toggle preference

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

            MonitoredFolders = new ObservableCollection<MonitoredFolder>();
            PrintJobConfigurations = new ObservableCollection<PrintJobConfiguration>();
            FilesProcessedToday = 0;
            PrintingErrorsToday = 0;
            DefaultPrinter = "No printer selected";
            IsServiceRunning = false;

            SetupDefaultPrinter();

            StartServiceCommand = new RelayCommand(async () => await StartService());
            StopServiceCommand = new RelayCommand(async () => await StopService());
        }

        public MainViewModel(IFileMonitoringService fileMonitoringService,
                           IPrintingService printingService,
                           IAppSettingsService appSettingsService,
                           ILogger<MainViewModel>? logger = null,
                           JsonConfigService? jsonConfigService = null,
                           SqliteDatabaseService? databaseService = null,
                           WindowsServiceController? serviceController = null) : this()
        {
            _fileMonitoringService = fileMonitoringService;
            _printingService = printingService;
            _appSettingsService = appSettingsService;
            _logger = logger;
            _jsonConfigService = jsonConfigService;
            _databaseService = databaseService;
            _serviceController = serviceController ?? new WindowsServiceController();

            // Subscribe to file processing events
            _fileMonitoringService.FileProcessed += OnFileProcessed;

            // Default printer will be set up after loading settings

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

        public string Orientation
        {
            get => _orientation;
            set => SetProperty(ref _orientation, value);
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
                SetProperty(ref _isServiceRunning, value);
            }
        }

        public ObservableCollection<MonitoredFolder> MonitoredFolders { get; }
        public ObservableCollection<PrintJobConfiguration> PrintJobConfigurations { get; set; }

        public ICommand StartServiceCommand { get; }
        public ICommand StopServiceCommand { get; }
        
        public Task InitializationTask { get; private set; } = Task.CompletedTask;

        private async Task InitializeAsync()
        {
            try
            {
                // Load settings first
                await LoadSettingsAsync();
                
                // Force refresh all UI properties
                RefreshAllUIProperties();
            }
            catch (Exception ex)
            {
                // Error during ViewModel initialization
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
                Orientation = settings.Orientation;
                _autoStartService = settings.AutoStartService;
                _isServiceEnabledByUser = settings.IsServiceEnabledByUser;

                if (string.IsNullOrEmpty(DefaultPrinter) || DefaultPrinter == "No printer selected")
                {
                    SetupDefaultPrinter();
                    
                    // Save the auto-detected printer setting
                    if (!string.IsNullOrEmpty(DefaultPrinter) && DefaultPrinter != "No printer selected")
                    {
                        _ = Task.Run(async () => await SaveSettingsAsync());
                    }
                }
                // Always load from settings first (for persistent in-memory counters)
                FilesProcessedToday = ShouldResetDailyStats(settings) ? 0 : settings.FilesProcessedToday;
                PrintingErrorsToday = ShouldResetDailyStats(settings) ? 0 : settings.PrintingErrors;
                

                // Load MonitoredFolders from JsonConfigService
                MonitoredFolders.Clear();
                if (_jsonConfigService != null)
                {
                    var monitoredFolders = await _jsonConfigService.GetMonitoredFoldersAsync();
                    foreach (var folder in monitoredFolders)
                    {
                        MonitoredFolders.Add(folder);
                    }
                    OnPropertyChanged(nameof(FirstMonitoredFolder)); // Notify UI of folder change
                }
                else
                {
                    // Fallback to settings.MonitoredFolders if JsonConfigService not available
                    foreach (var folder in settings.MonitoredFolders)
                    {
                        MonitoredFolders.Add(folder);
                    }
                    OnPropertyChanged(nameof(FirstMonitoredFolder)); // Notify UI of folder change
                }

                // Load PrintJobConfigurations from settings
                PrintJobConfigurations.Clear();
                foreach (var printJobConfig in settings.PrintJobConfigurations)
                {
                    PrintJobConfigurations.Add(printJobConfig);
                }

                    
                
                // Auto-start the service only if we have print jobs or folders configured
                if (PrintJobConfigurations.Any() || MonitoredFolders.Any())
                {
                    await AutoStartServiceAsync();
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                // Failed to load settings
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
                    return;
                }

                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                // Query completed print jobs for today - cast to int to avoid type issues
                var todayCompletedJobs = await _databaseService.ExecuteScalarAsync<long>(
                    @"SELECT COUNT(*) FROM PrintJobs 
                      WHERE Status = 2 
                      AND DATE(CreatedAt) = DATE(@Today)",
                    new Microsoft.Data.Sqlite.SqliteParameter("@Today", today.ToString("yyyy-MM-dd")));

                FilesProcessedToday = (int)todayCompletedJobs;

                // Query failed print jobs for today - cast to int to avoid type issues
                var todayFailedJobs = await _databaseService.ExecuteScalarAsync<long>(
                    @"SELECT COUNT(*) FROM PrintJobs 
                      WHERE Status = 3 
                      AND DATE(CreatedAt) = DATE(@Today)",
                    new Microsoft.Data.Sqlite.SqliteParameter("@Today", today.ToString("yyyy-MM-dd")));

                PrintingErrorsToday = (int)todayFailedJobs;

            }
            catch (Exception ex)
            {
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
                    Orientation = Orientation,
                    FilesProcessedToday = FilesProcessedToday,
                    PrintingErrors = PrintingErrorsToday,
                    MonitoredFolders = new List<MonitoredFolder>(), // Empty since we store these separately
                    PrintJobConfigurations = PrintJobConfigurations.ToList(),
                    AutoStartService = _autoStartService, // Use the stored auto-start preference, not current running state
                    IsServiceEnabledByUser = _isServiceEnabledByUser, // Save user's toggle preference
                    LastResetDate = DateTime.Today
                };

                await _appSettingsService.SaveSettingsAsync(settings);

                // Save MonitoredFolders to JsonConfigService
                if (_jsonConfigService != null)
                {
                    await _jsonConfigService.SaveMonitoredFoldersAsync(MonitoredFolders.ToList());
                }

            }
            catch (Exception ex)
            {
                // Failed to save settings
            }
        }

        private async Task StartService()
        {
            try
            {
                _isServiceEnabledByUser = true;
                
                if (_serviceController == null)
                {
                    IsServiceRunning = false;
                    return;
                }
                
                // Check if service is installed, install if needed
                if (!await _serviceController.IsServiceInstalledAsync())
                {
                    var installed = await _serviceController.InstallServiceAsync();
                    if (!installed)
                    {
                        // Installation failed - likely missing exe or permissions
                        IsServiceRunning = false;
                        throw new InvalidOperationException("Failed to install Windows service. Service executable may be missing or insufficient permissions.");
                    }
                    
                    // Wait a moment after installation
                    await Task.Delay(2000);
                }
                
                // Start the service
                var serviceStarted = await _serviceController.StartServiceAsync();
                if (serviceStarted)
                {
                    IsServiceRunning = true;
                    await SaveSettingsAsync();
                }
                else
                {
                    IsServiceRunning = false;
                    throw new InvalidOperationException("Windows service failed to start. Check if PrintHero.Service.exe exists and has proper permissions.");
                }
            }
            catch
            {
                IsServiceRunning = false;
            }
        }


        private async Task StopService()
        {
            try
            {
                _isServiceEnabledByUser = false;
                
                if (_serviceController == null)
                {
                    IsServiceRunning = false;
                    return;
                }
                
                await _serviceController.StopServiceAsync();
                IsServiceRunning = false;
                await SaveSettingsAsync();
            }
            catch
            {
                IsServiceRunning = false;
            }
        }

        private void OnFileProcessed(object? sender, FileProcessedEventArgs e)
        {
            
            // Marshal to UI thread for proper UI updates
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    if (e.Success)
                    {
                        // Increment processed count and force UI update
                        _filesProcessedToday++;
                        OnPropertyChanged(nameof(FilesProcessedToday));
                    }
                    else
                    {
                        // Increment error count and force UI update
                        _printingErrorsToday++;
                        OnPropertyChanged(nameof(PrintingErrorsToday));
                    }

                    // Queue settings save (debounced to avoid excessive disk I/O)
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(2000); // Wait 2 seconds before saving
                        await SaveSettingsAsync();
                    });

                    // Don't call LoadDailyStatisticsAsync() here as it overwrites our counters
                    // The counters are more reliable for real-time updates
                }
                catch (Exception ex)
                {
                    // Error handling file processed event
                }
            }));
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
        }

        private void RefreshAllUIProperties()
        {
            OnPropertyChanged(nameof(IsServiceRunning));
            OnPropertyChanged(nameof(DefaultPrinter));
            OnPropertyChanged(nameof(PaperSize));
            OnPropertyChanged(nameof(Orientation));
            OnPropertyChanged(nameof(FirstMonitoredFolder));
            OnPropertyChanged(nameof(FilesProcessedToday));
            OnPropertyChanged(nameof(PrintingErrorsToday));
        }


        private void SetupDefaultPrinter()
        {
            try
            {

                if (!string.IsNullOrEmpty(DefaultPrinter) && DefaultPrinter != "No printer selected")
                {
                    return;
                }

                var systemDefaultPrinter = GetSystemDefaultPrinter();
                if (!string.IsNullOrEmpty(systemDefaultPrinter))
                {
                    DefaultPrinter = systemDefaultPrinter;
                    return;
                }

                // If no system default, get the first available printer
                var availablePrinters = GetAvailablePrinters();
                if (availablePrinters.Any())
                {
                    var firstPrinter = availablePrinters.First();
                    DefaultPrinter = firstPrinter;
                    return;
                }

                // No printers found
                DefaultPrinter = "No printer found";
            }
            catch (Exception ex)
            {
                DefaultPrinter = "Error detecting printer";
            }
        }

        private async Task SetupDefaultPrintingFolder()
        {
            try
            {
                // Check if we already have monitored folders
                if (MonitoredFolders?.Any() == true)
                {
                    return;
                }

                // Use the same default path pattern as FolderSettingsWindow
                var defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    "PrintHero", 
                    "Input"
                );

                // Create the directory if it doesn't exist
                if (!Directory.Exists(defaultPath))
                {
                    Directory.CreateDirectory(defaultPath);
                }

                // Create default MonitoredFolder
                var defaultFolder = new MonitoredFolder
                {
                    FolderPath = defaultPath,
                    FilePattern = "*.pdf",
                    IsActive = true,
                    IncludeSubfolders = false,
                    CreatedAt = DateTime.Now
                };

                // Add via JsonConfigService if available
                if (_jsonConfigService != null)
                {
                    var addedFolder = await _jsonConfigService.AddMonitoredFolderAsync(defaultFolder);
                    if (addedFolder != null)
                    {
                        MonitoredFolders.Add(addedFolder);
                    }
                }
                else
                {
                    // Fallback: Add directly to collection with manual ID assignment
                    defaultFolder.Id = 1;
                    MonitoredFolders.Add(defaultFolder);
                }

                // Notify UI of changes
                OnPropertyChanged(nameof(FirstMonitoredFolder));
                
                // Save settings
                await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                // Failed to setup default printing folder
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
                    return defaultPrinter;
                }
            }
            catch (Exception ex)
            {
                // Error getting system default printer
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
                    }
                }
            }
            catch (Exception ex)
            {
                // Error getting available printers
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
                
                // Check if user wants the service enabled based on their last toggle preference
                if (!_isServiceEnabledByUser)
                {
                    IsServiceRunning = false;
                    return;
                }
                
                // Brief delay to ensure all services are ready
                await Task.Delay(100);
                
                // Ensure we have folders to monitor (Windows service will read these from settings)
                if (MonitoredFolders == null || (!MonitoredFolders.Any() && !PrintJobConfigurations.Any()))
                {
                    await SetupDefaultPrintingFolder();
                }
                
                // Start the Windows service (it will handle monitoring)
                await StartService();
                
                // Force UI update
                RefreshAllUIProperties();
                
            }
            catch (Exception ex)
            {
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