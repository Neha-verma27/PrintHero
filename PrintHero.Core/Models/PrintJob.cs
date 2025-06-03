namespace PrintHero.Core.Models;

public class PrintJob
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? PrintedAt { get; set; }
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int MonitoredFolderId { get; set; }
    public long FileSizeBytes { get; set; }
    public PostPrintAction PostPrintAction { get; set; }
    public string? MovedToPath { get; set; }
}

public enum PrintJobStatus
{
    Pending = 0,
    Printing = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}