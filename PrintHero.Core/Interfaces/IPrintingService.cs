using PrintHero.Core.Models;

namespace PrintHero.Core.Interfaces;

public interface IPrintingService
{   
//    Task QueuePrintJobAsync(PrintJob printJob);
//    void Dispose();
    void SetPrinterSettings(string printerName, string paperSize, string orientation);
    Task<bool> PrintFileAsync(string filePath);

}

public class PrintResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int PagesPrinted { get; set; }
    public TimeSpan PrintDuration { get; set; }
}

public class PrintConfiguration
{
    public string PrinterName { get; set; } = string.Empty;
    public int Copies { get; set; } = 1;
    public bool IsColor { get; set; } = false;
    public bool IsDuplex { get; set; } = false;
    public bool IsLandscape { get; set; } = false;
    public string PaperSize { get; set; } = "A4";
}

public class PrinterCapabilities
{
    public bool SupportsColor { get; set; }
    public bool SupportsDuplex { get; set; }
    public List<string> SupportedPaperSizes { get; set; } = new();
    public bool IsOnline { get; set; }
    public string Status { get; set; } = string.Empty;
}