namespace PrintHero.Core.Models;

public class FileProcessedEventArgs : EventArgs
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool Success { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string PrinterUsed { get; set; } = string.Empty;
    public string PrintJobName { get; set; } = string.Empty;
}