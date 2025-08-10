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
            _printingService.IsEnabled = false;
            await _fileMonitoringService.StopMonitoringAsync();
            
            _isCurrentlyRunning = false;
        }
        catch (Exception ex)
        {
            // Failed to stop PrintHero monitoring service
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
}