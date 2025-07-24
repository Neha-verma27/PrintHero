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

public class PrintJobStats
{
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int PendingJobs { get; set; }
    public double SuccessRate => TotalJobs > 0 ? (double)CompletedJobs / TotalJobs * 100 : 0;
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

public class PrintJobAnalytics
{
    public Dictionary<string, int> JobsByFileType { get; set; } = new();
    public Dictionary<string, int> JobsByPrinter { get; set; } = new();
    public Dictionary<string, int> ErrorsByType { get; set; } = new();
    public List<PrintJob> RecentJobs { get; set; } = new();
    public PrintJobStats DailyStats { get; set; } = new();
    public PrintJobStats WeeklyStats { get; set; } = new();
    public PrintJobStats MonthlyStats { get; set; } = new();
}