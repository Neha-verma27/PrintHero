namespace PrintHero.Core.Models;

public class FileProcessedEventArgs : EventArgs
{
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}