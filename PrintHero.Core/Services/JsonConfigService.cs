using System.Text.Json;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Models;

namespace PrintHero.Core.Services;

public class JsonConfigService
{
    private readonly string _configFolder;
    private readonly ILogger<JsonConfigService>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonConfigService(ILogger<JsonConfigService>? logger = null)
    {
        _configFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PrintHero");
        
        Directory.CreateDirectory(_configFolder);
        
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    // MonitoredFolders methods
    public async Task<List<MonitoredFolder>> GetMonitoredFoldersAsync()
    {
        try
        {
            var filePath = Path.Combine(_configFolder, "monitored-folders.json");
            
            if (!File.Exists(filePath))
            {
                var defaultFolders = new List<MonitoredFolder>();
                await SaveMonitoredFoldersAsync(defaultFolders);
                return defaultFolders;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var folders = JsonSerializer.Deserialize<List<MonitoredFolder>>(json, _jsonOptions) ?? new List<MonitoredFolder>();
            
            _logger?.LogInformation("Loaded {Count} monitored folders from JSON", folders.Count);
            return folders;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading monitored folders from JSON");
            return new List<MonitoredFolder>();
        }
    }

    public async Task SaveMonitoredFoldersAsync(List<MonitoredFolder> folders)
    {
        try
        {
            var filePath = Path.Combine(_configFolder, "monitored-folders.json");
            var json = JsonSerializer.Serialize(folders, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            
            _logger?.LogInformation("Saved {Count} monitored folders to JSON", folders.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving monitored folders to JSON");
            throw;
        }
    }

    public async Task<MonitoredFolder?> AddMonitoredFolderAsync(MonitoredFolder folder)
    {
        try
        {
            var folders = await GetMonitoredFoldersAsync();
            
            // Generate new ID
            folder.Id = folders.Any() ? folders.Max(f => f.Id) + 1 : 1;
            folder.CreatedAt = DateTime.Now;
            
            folders.Add(folder);
            await SaveMonitoredFoldersAsync(folders);
            
            _logger?.LogInformation("Added monitored folder: {FolderPath}", folder.FolderPath);
            return folder;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding monitored folder");
            return null;
        }
    }

    public async Task<bool> UpdateMonitoredFolderAsync(MonitoredFolder folder)
    {
        try
        {
            var folders = await GetMonitoredFoldersAsync();
            var existingIndex = folders.FindIndex(f => f.Id == folder.Id);
            
            if (existingIndex >= 0)
            {
                folders[existingIndex] = folder;
                await SaveMonitoredFoldersAsync(folders);
                
                _logger?.LogInformation("Updated monitored folder: {FolderPath}", folder.FolderPath);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating monitored folder");
            return false;
        }
    }

    public async Task<bool> DeleteMonitoredFolderAsync(int id)
    {
        try
        {
            var folders = await GetMonitoredFoldersAsync();
            var folderToRemove = folders.FirstOrDefault(f => f.Id == id);
            
            if (folderToRemove != null)
            {
                folders.Remove(folderToRemove);
                await SaveMonitoredFoldersAsync(folders);
                
                _logger?.LogInformation("Deleted monitored folder: {FolderPath}", folderToRemove.FolderPath);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting monitored folder");
            return false;
        }
    }

    public async Task<Dictionary<string, string>> GetSettingsAsync()
    {
        try
        {
            var filePath = Path.Combine(_configFolder, "settings.json");
            
            if (!File.Exists(filePath))
            {
                var defaultSettings = new Dictionary<string, string>();
                await SaveSettingsAsync(defaultSettings);
                return defaultSettings;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions) ?? new Dictionary<string, string>();
            
            _logger?.LogInformation("Loaded {Count} settings from JSON", settings.Count);
            return settings;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading settings from JSON");
            return new Dictionary<string, string>();
        }
    }

    public async Task SaveSettingsAsync(Dictionary<string, string> settings)
    {
        try
        {
            var filePath = Path.Combine(_configFolder, "settings.json");
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            
            _logger?.LogInformation("Saved {Count} settings to JSON", settings.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving settings to JSON");
            throw;
        }
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        try
        {
            var settings = await GetSettingsAsync();
            return settings.TryGetValue(key, out var value) ? value : null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting setting: {Key}", key);
            return null;
        }
    }

    public async Task<bool> SetSettingAsync(string key, string value)
    {
        try
        {
            var settings = await GetSettingsAsync();
            settings[key] = value;
            await SaveSettingsAsync(settings);
            
            _logger?.LogInformation("Set setting: {Key} = {Value}", key, value);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting: {Key}", key);
            return false;
        }
    }

    public async Task<bool> DeleteSettingAsync(string key)
    {
        try
        {
            var settings = await GetSettingsAsync();
            if (settings.Remove(key))
            {
                await SaveSettingsAsync(settings);
                _logger?.LogInformation("Deleted setting: {Key}", key);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting setting: {Key}", key);
            return false;
        }
    }
}