using PrintHero.Core.Models;

namespace PrintHero.Core.Interfaces;

public interface IConfigurationService
{
    Task<AppSettings> LoadConfigurationAsync();
    Task SaveConfigurationAsync(AppSettings configuration);
}
