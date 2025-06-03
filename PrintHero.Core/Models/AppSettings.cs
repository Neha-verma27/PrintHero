namespace PrintHero.Core.Models;

public class AppSettings
{
    public string DefaultPrinter { get; set; } = string.Empty;
    public string PaperSize { get; set; } = "A4";
    public string Orientation { get; set; } = "Portrait";
    public List<MonitoredFolder> MonitoredFolders { get; set; } = new();
    public bool AutoStartService { get; set; } = true;
    public int FilesProcessedToday { get; set; } = 0;
    public int PrintingErrors { get; set; } = 0;
    public DateTime LastResetDate { get; set; } = DateTime.Today;
}

public enum PostPrintAction
{
    MoveToSubfolder,
    MoveToCustomFolder,
    DeleteFile,
    KeepFile
}