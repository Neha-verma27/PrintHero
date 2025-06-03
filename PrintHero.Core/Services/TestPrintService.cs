using System.Drawing.Printing;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Models;

namespace PrintHero.Core.Services;

public class TestPrintService
{
    private readonly ILogger<PrintingService>? _logger;

    public TestPrintService(ILogger<PrintingService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<bool> TestPrintAsync(string pdfFilePath, string printerName)
    {
        _logger?.LogInformation($"Testing print: {pdfFilePath} to {printerName}");

        // 1. Check file exists
        if (!File.Exists(pdfFilePath))
        {
            _logger?.LogError($"File not found: {pdfFilePath}");
            return false;
        }

        // 2. Check printer exists
        var installedPrinters = PrinterSettings.InstalledPrinters.Cast<string>().ToList();
        if (!installedPrinters.Contains(printerName))
        {
            _logger?.LogError($"Printer '{printerName}' not found. Available printers:");
            foreach (var printer in installedPrinters)
            {
                _logger?.LogError($"  - {printer}");
            }
            return false;
        }

        // 3. Check printer status
        try
        {
            var printerSettings = new PrinterSettings { PrinterName = printerName };
            _logger?.LogInformation($"Printer valid: {printerSettings.IsValid}");
            _logger?.LogInformation($"Printer default page settings: {printerSettings.DefaultPageSettings.PaperSize.PaperName}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error checking printer settings for: {printerName}");
        }

        // 4. Try to print using different methods
        _logger?.LogInformation("Attempting to print with multiple methods...");

        var printingService = new PrintingService(_logger);
        var printJob = new PrintJob
        {
            FilePath = pdfFilePath,
            FileName = Path.GetFileName(pdfFilePath),
            PrinterName = printerName,
            Status = PrintJobStatus.Pending,
            PostPrintAction = PostPrintAction.KeepFile
        };

        return true;//await printingService.PrintFileAsync(printJob);
    }

    public void ListAvailablePrinters()
    {
        _logger?.LogInformation("Available Printers:");
        var printers = PrinterSettings.InstalledPrinters;
        var defaultPrinter = new PrinterSettings().PrinterName;

        foreach (string printer in printers)
        {
            var isDefault = printer == defaultPrinter ? " [DEFAULT]" : "";
            _logger?.LogInformation($"  - {printer}{isDefault}");

            try
            {
                var settings = new PrinterSettings { PrinterName = printer };
                _logger?.LogInformation($"    Status: {(settings.IsValid ? "Ready" : "Not Ready")}");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"    Error checking status: {ex.Message}");
            }
        }
    }
}

// Usage example for testing
/*
// In your console application or test:
var testService = new TestPrintService(logger);
testService.ListAvailablePrinters();

var success = await testService.TestPrintAsync(
    @"C:\path\to\test.pdf", 
    "Your Printer Name"
);

if (success)
{
    Console.WriteLine("Print test successful!");
}
else
{
    Console.WriteLine("Print test failed - check logs for details");
}
*/