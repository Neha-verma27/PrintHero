using PrintHero.Core.Models;

namespace PrintHero.Core.Interfaces;

public interface IAppSettingsService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}