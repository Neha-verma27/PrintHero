namespace PrintHero.Core.Models;

public class MonitoredFolder
{
    public int Id { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string FilePattern { get; set; } = "*.pdf";
    public bool IncludeSubfolders { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastActivity { get; set; }
    public PostPrintAction PostPrintAction { get; set; } = PostPrintAction.MoveToSubfolder;
    public string? CustomMoveFolder { get; set; }
}

public enum PostPrintAction
{
    MoveToSubfolder,
    MoveToCustomFolder,
    DeleteFile,
    KeepFile
}