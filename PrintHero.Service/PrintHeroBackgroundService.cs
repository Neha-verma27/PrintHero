using Microsoft.Extensions.Hosting;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Models;
using PrintHero.Core.Services;

namespace PrintHero.Service;

public class PrintHeroBackgroundService : BackgroundService
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IFileMonitoringService _fileMonitoringService;
    private readonly IPrintingService _printingService;
    private readonly JsonConfigService _jsonConfigService;
    private bool _isCurrentlyRunning = false;
    private bool _lastToggleState = false;
    private FileSystemWatcher? _configWatcher;
    private DateTime _lastConfigReload = DateTime.MinValue;

    public PrintHeroBackgroundService(
        IAppSettingsService appSettingsService,
        IFileMonitoringService fileMonitoringService,
        IPrintingService printingService,
        JsonConfigService jsonConfigService)
    {
        _appSettingsService = appSettingsService;
        _fileMonitoringService = fileMonitoringService;
        _printingService = printingService;
        _jsonConfigService = jsonConfigService;
        
        // Subscribe to file processing events to log print jobs
        _fileMonitoringService.FileProcessed += OnFileProcessed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Register for shutdown events to allow clean file releases
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckToggleStateAndManageService();
                    await Task.Delay(5000, stoppingToken); // Check every 5 seconds
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await Task.Delay(10000, stoppingToken); // Wait longer on error
                }
            }
        }
        finally
        {
            if (_isCurrentlyRunning)
            {
                await StopPrintHeroMonitoring();
            }
            
            // Cleanup event handlers
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (_isCurrentlyRunning)
        {
            StopPrintHeroMonitoring().Wait(TimeSpan.FromSeconds(5));
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        if (_isCurrentlyRunning)
        {
            StopPrintHeroMonitoring().Wait(TimeSpan.FromSeconds(5));
        }
    }

    private async Task CheckToggleStateAndManageService()
    {
        try
        {
            var settings = await _appSettingsService.LoadSettingsAsync();
            bool toggleState = settings.IsServiceEnabledByUser;

            if (toggleState != _lastToggleState)
            {
                _lastToggleState = toggleState;

                if (toggleState && !_isCurrentlyRunning)
                {
                    await StartPrintHeroMonitoring(settings);
                }
                else if (!toggleState && _isCurrentlyRunning)
                {
                    await StopPrintHeroMonitoring();
                }
            }
        }
        catch (Exception ex)
        {
            // Failed to check toggle state
        }
    }

    private async Task StartPrintHeroMonitoring(AppSettings settings)
    {
        try
        {
            // Configure printer settings
            if (!string.IsNullOrEmpty(settings.DefaultPrinter))
            {
                _printingService.SetPrinterSettings(settings.DefaultPrinter, settings.PaperSize, settings.Orientation);
            }

            _printingService.IsEnabled = true;

            // Load monitored folders and print jobs
            var monitoredFolders = await _jsonConfigService.GetMonitoredFoldersAsync();
            var printJobConfigurations = settings.PrintJobConfigurations;

            if (printJobConfigurations?.Any() == true)
            {
                await _fileMonitoringService.StartMonitoringPrintJobsAsync(printJobConfigurations);
            }
            else if (monitoredFolders?.Any() == true)
            {
                await _fileMonitoringService.StartMonitoringAsync(monitoredFolders);
            }
            else
            {
                await CreateDefaultFolder();
                
                // Try again with default folder
                var folders = await _jsonConfigService.GetMonitoredFoldersAsync();
                if (folders?.Any() == true)
                {
                    await _fileMonitoringService.StartMonitoringAsync(folders);
                }
            }

            // Start monitoring configuration changes
            StartConfigurationMonitoring();

            _isCurrentlyRunning = true;
        }
        catch (Exception ex)
        {
            _isCurrentlyRunning = false;
        }
    }

    private async Task StopPrintHeroMonitoring()
    {
        try
        {
            _isCurrentlyRunning = false;
            _printingService.IsEnabled = false;
            await _fileMonitoringService.StopMonitoringAsync();
            
            // Stop configuration monitoring
            StopConfigurationMonitoring();
            
            // Give a small delay to ensure cleanup completes
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            _isCurrentlyRunning = false;
            // Log error but don't throw - we want stop to complete
        }
    }

    private async Task CreateDefaultFolder()
    {
        try
        {
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PrintHero",
                "Input"
            );

            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }

            var defaultFolder = new MonitoredFolder
            {
                FolderPath = defaultPath,
                FilePattern = "*.pdf",
                IsActive = true,
                IncludeSubfolders = false,
                CreatedAt = DateTime.Now
            };

            await _jsonConfigService.AddMonitoredFolderAsync(defaultFolder);
        }
        catch (Exception ex)
        {
            // Failed to create default folder
        }
    }

    private void StartConfigurationMonitoring()
    {
        try
        {
            var configFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "PrintHero");

            _configWatcher = new FileSystemWatcher(configFolder)
            {
                Filter = "monitored-folders.json",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _configWatcher.Changed += OnConfigurationChanged;
        }
        catch (Exception ex)
        {
            // Failed to setup configuration monitoring
        }
    }

    private void StopConfigurationMonitoring()
    {
        try
        {
            if (_configWatcher != null)
            {
                _configWatcher.EnableRaisingEvents = false;
                _configWatcher.Changed -= OnConfigurationChanged;
                _configWatcher.Dispose();
                _configWatcher = null;
            }
        }
        catch (Exception ex)
        {
            // Error stopping configuration monitoring
        }
    }

    private async void OnConfigurationChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Debounce rapid file changes (common with file writes)
            var now = DateTime.Now;
            if (now - _lastConfigReload < TimeSpan.FromSeconds(2))
            {
                return;
            }
            _lastConfigReload = now;

            // Wait a moment for file write to complete
            await Task.Delay(500);

            if (_isCurrentlyRunning)
            {
                // Restart the entire service when folders change
                var settings = await _appSettingsService.LoadSettingsAsync();
                await StopPrintHeroMonitoring();
                await StartPrintHeroMonitoring(settings);
            }
        }
        catch (Exception ex)
        {
            // Error handling configuration change
        }
    }


    private async void OnFileProcessed(object? sender, FileProcessedEventArgs e)
    {
        try
        {
            // Update the count in AppSettings so UI can read it
            var settings = await _appSettingsService.LoadSettingsAsync();
            
            if (e.Success)
            {
                settings.FilesProcessedToday++;
            }
            else
            {
                settings.PrintingErrors++;
            }
            
            await _appSettingsService.SaveSettingsAsync(settings);
        }
        catch (Exception ex)
        {
            // Error updating settings
        }
    }
}