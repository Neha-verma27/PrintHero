// PrintHero.Service/Worker.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Data;
using PrintHero.Core.Services;

namespace PrintHero.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly DatabaseService _databaseService;
    private readonly FileMonitoringService _fileMonitoringService;
    private readonly PrintingService _printService;
    //private readonly LicensingService _licensingService;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;

        // Initialize services
        _databaseService = new DatabaseService(_logger as ILogger<DatabaseService>);
        _printService = new PrintingService(_logger as ILogger<PrintingService>);
        _fileMonitoringService = new FileMonitoringService(_databaseService, _printService, _logger as ILogger<FileMonitoringService>);
       // _licensingService = new LicensingService(_databaseService, _logger as ILogger<LicensingService>);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("PrintHero Service starting...");

            // Validate license
            //var licenseValidation = await _licensingService.ValidateLicenseAsync();
            //if (!licenseValidation.IsValid)
            //{
            //    _logger.LogWarning($"License validation failed: {licenseValidation.ErrorMessage}");
            //    // Continue in trial mode
            //}
            //else
            //{
            //    _logger.LogInformation($"License valid: {licenseValidation.License?.LicenseType}");
            //}

            // Start monitoring folders
            await _fileMonitoringService.StartMonitoringAsync(null);
            _logger.LogInformation("PrintHero Service started successfully");

            // Keep service running
            while (!stoppingToken.IsCancellationRequested)
            {
                // Log status every 30 minutes
                _logger.LogInformation($"PrintHero Service running at: {DateTimeOffset.Now}");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PrintHero Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in PrintHero Service");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PrintHero Service is stopping...");

        try
        {
            await _fileMonitoringService.StopMonitoringAsync();
            _fileMonitoringService.Dispose();
            //_printService.Dispose();

            _logger.LogInformation("PrintHero Service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}