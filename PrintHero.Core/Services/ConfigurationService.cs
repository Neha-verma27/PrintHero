using System.Text.Json;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Models;

namespace PrintHero.Core.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configPath;

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var printHeroPath = Path.Combine(appDataPath, "PrintHero");
        Directory.CreateDirectory(printHeroPath);
        _configPath = Path.Combine(printHeroPath, "config.json");
    }

    public async Task<AppSettings> LoadConfigurationAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                var config = JsonSerializer.Deserialize<AppSettings>(json);
                _logger.LogInformation("Configuration loaded successfully");
                return config ?? new AppSettings();
            }

            _logger.LogInformation("No configuration file found, returning default configuration");
            return new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration, returning default");
            return new AppSettings();
        }
    }

    public async Task SaveConfigurationAsync(AppSettings configuration)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(configuration, options);
            await File.WriteAllTextAsync(_configPath, json);

            _logger.LogInformation("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
            throw;
        }
    }
}