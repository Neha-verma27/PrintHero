using System;

namespace PrintHero.Core.Models;

public class LicenseInfo
{
    public int Id { get; set; }
    public string LicenseKey { get; set; } = string.Empty;
    public string MachineFingerprint { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public DateTime ActivationDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public LicenseType LicenseType { get; set; }
    public bool IsActive { get; set; }
    public int MaxDevices { get; set; }
    public string? Features { get; set; }
}

public enum LicenseType
{
    Trial = 0,
    Standard = 1,
    Professional = 2,
    Enterprise = 3
}