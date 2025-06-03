using System.Text.Json;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Models;

namespace PrintHero.Core.Services;

public class AppSettingsService : IAppSettingsService
{
    private readonly ILogger<AppSettingsService> _logger;
    private readonly string _settingsPath;

    public AppSettingsService(ILogger<AppSettingsService> logger)
    {
        _logger = logger;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var printHeroPath = Path.Combine(appDataPath, "PrintHero");
        Directory.CreateDirectory(printHeroPath);
        _settingsPath = Path.Combine(printHeroPath, "appsettings.json");
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                _logger.LogInformation("App settings loaded successfully");
                return settings ?? new AppSettings();
            }

            _logger.LogInformation("No app settings file found, returning default settings");
            return new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load app settings, returning default");
            return new AppSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_settingsPath, json);

            _logger.LogInformation("App settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save app settings");
            throw;
        }
    }
}