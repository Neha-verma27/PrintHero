using PrintHero.Core.Models;

namespace PrintHero.Core.Interfaces;

public interface IFileMonitoringService
{
    event EventHandler<FileProcessedEventArgs>? FileProcessed;
    Task StartMonitoringAsync(IEnumerable<MonitoredFolder>? folders);
    Task StopMonitoringAsync();
    
}

public class FileDetectedEventArgs : EventArgs
{
    public string FilePath { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }

    public FileDetectedEventArgs(string filePath, string folderPath)
    {
        FilePath = filePath;
        FolderPath = folderPath;
        DetectedAt = DateTime.Now;
    }
}