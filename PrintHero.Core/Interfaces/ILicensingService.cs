using PrintHero.Core.Models;

namespace PrintHero.Core.Interfaces;

public interface ILicensingService
{
    Task<LicenseValidationResult> ValidateLicenseAsync();
    Task<bool> InstallLicenseAsync(string licenseKey);
    Task<bool> DeactivateLicenseAsync();
    Task<LicenseInfo?> GetCurrentLicenseAsync();
    Task<string> GenerateMachineFingerprintAsync();
    bool IsLicenseValid { get; }
    LicenseType CurrentLicenseType { get; }
}

public class LicenseValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public LicenseInfo? License { get; set; }
    public DateTime ValidationDate { get; set; } = DateTime.Now;
    public TimeSpan? TimeRemaining { get; set; }
}