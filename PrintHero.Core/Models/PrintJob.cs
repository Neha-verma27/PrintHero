namespace PrintHero.Core.Models;

public class PrintJob
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileType { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty;
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessingStarted { get; set; }
    public DateTime? ProcessingCompleted { get; set; }
    public int? MonitoredFolderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public enum PrintJobStatus
{
    Pending = 0,
    Printing = 1,
    Completed = 2,
    Failed = 3
}

// New model for print job configurations/monitoring rules
public class PrintJobConfiguration
{
    public int Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string HotFolderPath { get; set; } = string.Empty;
    public string FilePattern { get; set; } = "*.PDF";
    public string PrinterName { get; set; } = string.Empty;
    public string PaperType { get; set; } = "A4";
    public string Orientation { get; set; } = "Portrait";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    
    // Derived properties for display
    public string Status => IsActive ? "Active" : "Inactive";
    public string DisplayName => string.IsNullOrEmpty(JobName) ? "Unnamed Job" : JobName;
}

