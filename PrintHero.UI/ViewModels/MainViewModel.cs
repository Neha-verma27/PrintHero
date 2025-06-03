// PrintHero.UI/ViewModels/MainViewModel.cs (Updated)
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Models;
using PrintHero.Core.Services;
using Serilog;

namespace PrintHero.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IFileMonitoringService _fileMonitoringService;
        private readonly IPrintingService _printingService;
        private readonly IAppSettingsService _appSettingsService;
        private readonly ILogger<MainViewModel>? _logger;

        private int _filesProcessedToday;
        private int _printingErrors;
        private string? _defaultPrinter;
        private bool _isServiceRunning;
        private string? _licenseKey;
        private string _paperSize = "A4";

        public MainViewModel()
        {
            // Default constructor for design-time
            MonitoredFolders = new ObservableCollection<MonitoredFolder>();
            FilesProcessedToday = 0;
            PrintingErrors = 0;
            DefaultPrinter = "No printer selected";
            IsServiceRunning = false;
            LicenseKey = "**************";

            StartServiceCommand = new RelayCommand(async () => await StartService());
            StopServiceCommand = new RelayCommand(async () => await StopService());
        }

        public MainViewModel(IFileMonitoringService fileMonitoringService,
                           IPrintingService printingService,
                           IAppSettingsService appSettingsService,
                           ILogger<MainViewModel>? logger = null) : this()
        {
            _fileMonitoringService = fileMonitoringService;
            _printingService = printingService;
            _appSettingsService = appSettingsService;
            _logger = logger;

            // Subscribe to file processing events
            _fileMonitoringService.FileProcessed += OnFileProcessed;

            // Load settings on initialization
            _ = LoadSettingsAsync();
        }

        public int FilesProcessedToday
        {
            get => _filesProcessedToday;
            set => SetProperty(ref _filesProcessedToday, value);
        }

        public int PrintingErrors
        {
            get => _printingErrors;
            set => SetProperty(ref _printingErrors, value);
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

        public bool IsServiceRunning
        {
            get => _isServiceRunning;
            set => SetProperty(ref _isServiceRunning, value);
        }

        public string? LicenseKey
        {
            get => _licenseKey;
            set => SetProperty(ref _licenseKey, value);
        }

        public ObservableCollection<MonitoredFolder> MonitoredFolders { get; }

        public ICommand StartServiceCommand { get; }
        public ICommand StopServiceCommand { get; }

        private async Task LoadSettingsAsync()
        {
            try
            {
                if (_appSettingsService == null) return;

                var settings = await _appSettingsService.LoadSettingsAsync();

                DefaultPrinter = settings.DefaultPrinter;
                PaperSize = settings.PaperSize;
                FilesProcessedToday = ShouldResetDailyStats(settings) ? 0 : settings.FilesProcessedToday;
                PrintingErrors = ShouldResetDailyStats(settings) ? 0 : settings.PrintingErrors;

                MonitoredFolders.Clear();
                foreach (var folder in settings.MonitoredFolders)
                {
                    MonitoredFolders.Add(folder);
                }

                _logger?.LogInformation("Settings loaded successfully");
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
                    PrintingErrors = PrintingErrors,
                    MonitoredFolders = MonitoredFolders.ToList(),
                    AutoStartService = IsServiceRunning,
                    LastResetDate = DateTime.Today
                };

                await _appSettingsService.SaveSettingsAsync(settings);
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
                    _logger?.LogWarning("File monitoring service not available");
                    return;
                }

                // Update printer settings
                if (!string.IsNullOrEmpty(DefaultPrinter))
                {
                    _printingService?.SetPrinterSettings(DefaultPrinter, PaperSize, "Portrait");
                }

                // Start monitoring
                await _fileMonitoringService.StartMonitoringAsync(MonitoredFolders);
                IsServiceRunning = true;

                await SaveSettingsAsync();
                _logger?.LogInformation("File monitoring service started");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start service");
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
                    FilesProcessedToday++;
                    _logger?.LogInformation($"File processed successfully: {e.FilePath}");
                }
                else
                {
                    PrintingErrors++;
                    _logger?.LogError($"File processing failed: {e.FilePath} - {e.ErrorMessage}");
                }

                // Save updated stats
                await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling file processed event");
            }
        }

        public async Task UpdatePrinterSettingsAsync(string printerName, string paperSize, string orientation)
        {
            DefaultPrinter = printerName;
            PaperSize = paperSize;

            _printingService?.SetPrinterSettings(printerName, paperSize, orientation);
            await SaveSettingsAsync();

            _logger?.LogInformation($"Printer settings updated: {printerName}, {paperSize}, {orientation}");
        }

        public async Task AddMonitoredFolderAsync(MonitoredFolder folder)
        {
            MonitoredFolders.Add(folder);
            await SaveSettingsAsync();
            _logger?.LogInformation($"Added monitored folder: {folder.FolderPath}");
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public void Dispose()
        {
            if (_fileMonitoringService != null)
            {
                _fileMonitoringService.FileProcessed -= OnFileProcessed;
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