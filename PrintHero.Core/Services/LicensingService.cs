using System.Data;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Data;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Models;

namespace PrintHero.Core.Services;

public class LicensingService : ILicensingService
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger<LicensingService>? _logger;
    private readonly string _encryptionKey = "PrintHero2025SecretKey123456789!"; // In production, use secure key management
    private LicenseInfo? _cachedLicense;

    public bool IsLicenseValid { get; private set; }
    public LicenseType CurrentLicenseType { get; private set; } = LicenseType.Trial;

    public LicensingService(DatabaseService databaseService, ILogger<LicensingService>? logger = null)
    {
        _databaseService = databaseService;
        _logger = logger;

        // Initialize license status
        Task.Run(async () => await ValidateLicenseAsync());
    }

    public async Task<LicenseValidationResult> ValidateLicenseAsync()
    {
        try
        {
            var license = await GetCurrentLicenseAsync();

            if (license == null)
            {
                IsLicenseValid = false;
                CurrentLicenseType = LicenseType.Trial;
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "No license found. Running in trial mode."
                };
            }

            // Check if license is active
            if (!license.IsActive)
            {
                IsLicenseValid = false;
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "License is deactivated."
                };
            }

            // Validate machine fingerprint
            var currentFingerprint = await GenerateMachineFingerprintAsync();
            if (license.MachineFingerprint != currentFingerprint)
            {
                IsLicenseValid = false;
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "License is not valid for this machine."
                };
            }

            // Check expiry date
            if (license.ExpiryDate.HasValue && license.ExpiryDate < DateTime.Now)
            {
                IsLicenseValid = false;
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "License has expired.",
                    License = license
                };
            }

            // Validate license key format and checksum
            if (!IsValidLicenseKeyFormat(license.LicenseKey))
            {
                IsLicenseValid = false;
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid license key format."
                };
            }

            // License is valid
            IsLicenseValid = true;
            CurrentLicenseType = license.LicenseType;
            _cachedLicense = license;

            var timeRemaining = license.ExpiryDate?.Subtract(DateTime.Now);

            _logger?.LogInformation("License validation successful. Type: {LicenseType}, Expires: {ExpiryDate}",
                license.LicenseType, license.ExpiryDate?.ToString("yyyy-MM-dd") ?? "Never");

            return new LicenseValidationResult
            {
                IsValid = true,
                License = license,
                TimeRemaining = timeRemaining
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "License validation failed");
            IsLicenseValid = false;
            return new LicenseValidationResult
            {
                IsValid = false,
                ErrorMessage = $"License validation error: {ex.Message}"
            };
        }
    }

    public async Task<bool> InstallLicenseAsync(string licenseKey)
    {
        try
        {
            // Validate license key format
            if (!IsValidLicenseKeyFormat(licenseKey))
            {
                _logger?.LogWarning("Invalid license key format provided");
                return false;
            }

            // Parse license key to extract information
            var licenseInfo = ParseLicenseKey(licenseKey);
            if (licenseInfo == null)
            {
                _logger?.LogWarning("Failed to parse license key");
                return false;
            }

            // Generate machine fingerprint
            var machineFingerprint = await GenerateMachineFingerprintAsync();

            // Create license record
            var license = new LicenseInfo
            {
                LicenseKey = licenseKey,
                MachineFingerprint = machineFingerprint,
                ActivationDate = DateTime.Now,
                ExpiryDate = licenseInfo.ExpiryDate,
                LicenseType = licenseInfo.LicenseType,
                IsActive = true,
                MaxDevices = licenseInfo.MaxDevices,
                Features = licenseInfo.Features
            };

            // Save to database
            var sql = @"INSERT OR REPLACE INTO License 
                       (LicenseKey, MachineFingerprint, ActivationDate, ExpiryDate, LicenseType, IsActive, MaxDevices, Features) 
                       VALUES (@LicenseKey, @MachineFingerprint, @ActivationDate, @ExpiryDate, @LicenseType, @IsActive, @MaxDevices, @Features)";

            await _databaseService.ExecuteNonQueryAsync(sql,
                new System.Data.SQLite.SQLiteParameter("@LicenseKey", license.LicenseKey),
                new System.Data.SQLite.SQLiteParameter("@MachineFingerprint", license.MachineFingerprint),
                new System.Data.SQLite.SQLiteParameter("@ActivationDate", license.ActivationDate),
                new System.Data.SQLite.SQLiteParameter("@ExpiryDate", license.ExpiryDate ?? (object)DBNull.Value),
                new System.Data.SQLite.SQLiteParameter("@LicenseType", (int)license.LicenseType),
                new System.Data.SQLite.SQLiteParameter("@IsActive", license.IsActive),
                new System.Data.SQLite.SQLiteParameter("@MaxDevices", license.MaxDevices),
                new System.Data.SQLite.SQLiteParameter("@Features", license.Features ?? (object)DBNull.Value));

            // Update cache and status
            _cachedLicense = license;
            await ValidateLicenseAsync();

            _logger?.LogInformation("License installed successfully. Type: {LicenseType}", license.LicenseType);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to install license");
            return false;
        }
    }

    public async Task<bool> DeactivateLicenseAsync()
    {
        try
        {
            var sql = "UPDATE License SET IsActive = 0 WHERE IsActive = 1";
            await _databaseService.ExecuteNonQueryAsync(sql);

            IsLicenseValid = false;
            CurrentLicenseType = LicenseType.Trial;
            _cachedLicense = null;

            _logger?.LogInformation("License deactivated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to deactivate license");
            return false;
        }
    }

    public async Task<LicenseInfo?> GetCurrentLicenseAsync()
    {
        if (_cachedLicense != null)
            return _cachedLicense;

        try
        {
            using var connection = _databaseService.GetConnection();
            await connection.OpenAsync();

            var sql = "SELECT * FROM License WHERE IsActive = 1 ORDER BY ActivationDate DESC LIMIT 1";
            using var command = new System.Data.SQLite.SQLiteCommand(sql, connection);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var license = new LicenseInfo
                {
                    Id = reader.GetInt32("Id"),
                    LicenseKey = reader.GetString("LicenseKey"),
                    MachineFingerprint = reader.GetString("MachineFingerprint"),
                    CustomerName = reader.IsDBNull("CustomerName") ? null : reader.GetString("CustomerName"),
                    ActivationDate = reader.GetDateTime("ActivationDate"),
                    ExpiryDate = reader.IsDBNull("ExpiryDate") ? null : reader.GetDateTime("ExpiryDate"),
                    LicenseType = (LicenseType)reader.GetInt32("LicenseType"),
                    IsActive = reader.GetBoolean("IsActive"),
                    MaxDevices = reader.GetInt32("MaxDevices"),
                    Features = reader.IsDBNull("Features") ? null : reader.GetString("Features")
                };

                _cachedLicense = license;
                return license;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get current license");
            return null;
        }
    }

    public async Task<string> GenerateMachineFingerprintAsync()
    {
        try
        {
            var identifiers = new List<string>();

            // CPU ID
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var processorId = obj["ProcessorId"]?.ToString();
                    if (!string.IsNullOrEmpty(processorId))
                        identifiers.Add(processorId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get CPU ID");
            }

            // Motherboard serial
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var serialNumber = obj["SerialNumber"]?.ToString();
                    if (!string.IsNullOrEmpty(serialNumber) && serialNumber != "None")
                        identifiers.Add(serialNumber);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get motherboard serial");
            }

            // MAC Address (first physical adapter)
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                                  nic.OperationalStatus == OperationalStatus.Up)
                    .Select(nic => nic.GetPhysicalAddress().ToString())
                    .Where(addr => !string.IsNullOrEmpty(addr) && addr != "000000000000")
                    .Take(2); // Take first 2 MAC addresses for stability

                identifiers.AddRange(networkInterfaces);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get MAC addresses");
            }

            // Hard Drive Serial (System Drive)
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_PhysicalMedia");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var serialNumber = obj["SerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(serialNumber))
                    {
                        identifiers.Add(serialNumber);
                        break; // Take only first one for consistency
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get hard drive serial");
            }

            // Fallback: Machine name and user name
            if (identifiers.Count < 2)
            {
                identifiers.Add(Environment.MachineName);
                identifiers.Add(Environment.UserName);
            }

            // Create hash
            var combined = string.Join("|", identifiers.Take(4)); // Use only first 4 for consistency
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            var fingerprint = Convert.ToBase64String(hash);

            _logger?.LogDebug("Generated machine fingerprint from {Count} identifiers", identifiers.Count);
            return fingerprint;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate machine fingerprint");
            // Return a fallback fingerprint based on machine name
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(Environment.MachineName + Environment.UserName));
            return Convert.ToBase64String(hash);
        }
    }

    private bool IsValidLicenseKeyFormat(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return false;

        // Expected format: XXXX-XXXX-XXXX-XXXX
        var pattern = @"^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$";
        return System.Text.RegularExpressions.Regex.IsMatch(licenseKey, pattern);
    }

    private LicenseKeyInfo? ParseLicenseKey(string licenseKey)
    {
        try
        {
            // Remove dashes
            var cleanKey = licenseKey.Replace("-", "");

            if (cleanKey.Length != 16)
                return null;

            // Extract components (this is a simplified parser - implement your own logic)
            var typeCode = cleanKey.Substring(0, 2);
            var expiryCode = cleanKey.Substring(2, 4);
            var featuresCode = cleanKey.Substring(6, 4);
            var checksumCode = cleanKey.Substring(10, 6);

            // Parse license type
            var licenseType = typeCode switch
            {
                "01" => LicenseType.Trial,
                "02" => LicenseType.Standard,
                "03" => LicenseType.Professional,
                "04" => LicenseType.Enterprise,
                _ => LicenseType.Trial
            };

            // Parse expiry (days from activation)
            if (int.TryParse(expiryCode, System.Globalization.NumberStyles.HexNumber, null, out int expiryDays))
            {
                var expiryDate = expiryDays == 0 ? (DateTime?)null : DateTime.Now.AddDays(expiryDays);

                return new LicenseKeyInfo
                {
                    LicenseType = licenseType,
                    ExpiryDate = expiryDate,
                    MaxDevices = licenseType == LicenseType.Enterprise ? 10 : 1,
                    Features = GetFeaturesForLicenseType(licenseType)
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse license key");
            return null;
        }
    }

    private string GetFeaturesForLicenseType(LicenseType licenseType)
    {
        return licenseType switch
        {
            LicenseType.Trial => "BasicPrinting,SingleFolder",
            LicenseType.Standard => "BasicPrinting,MultipleFolders,PrintHistory",
            LicenseType.Professional => "AdvancedPrinting,MultipleFolders,PrintHistory,Scheduling,EmailNotifications",
            LicenseType.Enterprise => "AdvancedPrinting,MultipleFolders,PrintHistory,Scheduling,EmailNotifications,MultiUser,CentralManagement",
            _ => "BasicPrinting"
        };
    }

    private class LicenseKeyInfo
    {
        public LicenseType LicenseType { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int MaxDevices { get; set; }
        public string Features { get; set; } = string.Empty;
    }
}