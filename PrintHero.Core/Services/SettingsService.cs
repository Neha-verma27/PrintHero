using System.Data.SQLite;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Data;

namespace PrintHero.Core.Services;

public class SettingsService
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger<SettingsService>? _logger;

    public SettingsService(DatabaseService databaseService, ILogger<SettingsService>? logger = null)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        try
        {
            var sql = "SELECT Value FROM Settings WHERE Key = @Key";
            var result = await _databaseService.ExecuteScalarAsync(sql,
                new SQLiteParameter("@Key", key));

            return result?.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to get setting: {key}");
            return null;
        }
    }

    public async Task<bool> SetSettingAsync(string key, string value)
    {
        try
        {
            var sql = @"INSERT OR REPLACE INTO Settings (Key, Value, UpdatedAt) 
                       VALUES (@Key, @Value, @UpdatedAt)";

            await _databaseService.ExecuteNonQueryAsync(sql,
                new SQLiteParameter("@Key", key),
                new SQLiteParameter("@Value", value),
                new SQLiteParameter("@UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to set setting: {key}");
            return false;
        }
    }

    // Specific setting methods
    public async Task<string> GetDefaultPrinterAsync()
    {
        var printer = await GetSettingAsync("DefaultPrinter");
        return printer ?? new System.Drawing.Printing.PrinterSettings().PrinterName;
    }

    public async Task<bool> SetDefaultPrinterAsync(string printerName)
    {
        return await SetSettingAsync("DefaultPrinter", printerName);
    }

    public async Task<string> GetPaperSizeAsync()
    {
        return await GetSettingAsync("PaperSize") ?? "A4";
    }

    public async Task<bool> SetPaperSizeAsync(string paperSize)
    {
        return await SetSettingAsync("PaperSize", paperSize);
    }
}