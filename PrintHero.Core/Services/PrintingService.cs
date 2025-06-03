using System.Diagnostics;
using System.Drawing.Printing;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Models;

namespace PrintHero.Core.Services;

public class PrintingService : IPrintingService
{
    private readonly ILogger<PrintingService> _logger;
    private string? _defaultPrinter;
    private string _paperSize = "A4";
    private string _orientation = "Portrait";

    public PrintingService(ILogger<PrintingService> logger)
    {
        _logger = logger;
        _defaultPrinter = new PrinterSettings().PrinterName;
    }

    public void SetPrinterSettings(string printerName, string paperSize, string orientation)
    {
        _defaultPrinter = printerName;
        _paperSize = paperSize;
        _orientation = orientation;
        _logger.LogInformation($"Printer settings updated: {printerName}, {paperSize}, {orientation}");
    }

    public async Task<bool> PrintFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(_defaultPrinter))
            {
                _logger.LogError("No default printer set");
                return false;
            }

            if (!File.Exists(filePath))
            {
                _logger.LogError($"File not found: {filePath}");
                return false;
            }

            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

            switch (fileExtension)
            {
                case ".pdf":
                    return await PrintPdfAsync(filePath);
                case ".txt":
                case ".doc":
                case ".docx":
                    return await PrintDocumentAsync(filePath);
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                    return await PrintImageAsync(filePath);
                default:
                    _logger.LogWarning($"Unsupported file type: {fileExtension}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to print file: {filePath}");
            return false;
        }
    }

    private async Task<bool> PrintPdfAsync(string filePath)
    {
        try
        {
            if (await TryPrintWithAdobeReader(filePath))
            {
                return true;
            }

            return await PrintPdfWithBuiltInMethod(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to print PDF: {filePath}");
            return false;
        }
    }

    private async Task<bool> TryPrintWithAdobeReader(string filePath)
    {
        try
        {
            // Try to find Adobe Reader
            var adobePaths = new[]
            {
                @"C:\Program Files\Adobe\Acrobat DC\Acrobat\Acrobat.exe",
                @"C:\Program Files (x86)\Adobe\Acrobat Reader DC\Reader\AcroRd32.exe",
                @"C:\Program Files\Adobe\Acrobat Reader DC\Reader\AcroRd32.exe"
            };

            string? adobePath = adobePaths.FirstOrDefault(File.Exists);

            if (adobePath != null)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adobePath,
                        Arguments = $"/t \"{filePath}\" \"{_defaultPrinter}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                _logger.LogInformation($"PDF printed using Adobe Reader: {filePath}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to print with Adobe Reader, trying alternative method");
            return false;
        }
    }

    private async Task<bool> PrintPdfWithBuiltInMethod(string filePath)
    {
        try
        {
            using var document = PdfReader.Open(filePath, PdfDocumentOpenMode.ReadOnly);

            var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = _defaultPrinter;

            // Set paper size and orientation
            foreach (PaperSize size in printDoc.PrinterSettings.PaperSizes)
            {
                if (size.PaperName.Contains(_paperSize, StringComparison.OrdinalIgnoreCase))
                {
                    printDoc.DefaultPageSettings.PaperSize = size;
                    break;
                }
            }

            printDoc.DefaultPageSettings.Landscape = _orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase);

            int currentPageIndex = 0;
            printDoc.PrintPage += (sender, e) =>
            {
                if (currentPageIndex < document.PageCount)
                {
                    // This is a simplified implementation
                    // For proper PDF rendering, you'd need a more sophisticated approach
                    e.Graphics!.DrawString($"PDF Page {currentPageIndex + 1}",
                        new Font("Arial", 12), Brushes.Black, 100, 100);

                    currentPageIndex++;
                    e.HasMorePages = currentPageIndex < document.PageCount;
                }
            };

            printDoc.Print();

            _logger.LogInformation($"PDF printed using built-in method: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to print PDF with built-in method: {filePath}");
            return false;
        }
    }

    private async Task<bool> PrintDocumentAsync(string filePath)
    {
        try
        {
            // Use default application to print
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = filePath,
                    Verb = "print",
                    UseShellExecute = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            _logger.LogInformation($"Document printed: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to print document: {filePath}");
            return false;
        }
    }

    private async Task<bool> PrintImageAsync(string filePath)
    {
        try
        {
            var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = _defaultPrinter;

            using var image = Image.FromFile(filePath);

            printDoc.PrintPage += (sender, e) =>
            {
                var bounds = e.MarginBounds;
                var imageSize = image.Size;

                // Scale image to fit page
                float scale = Math.Min((float)bounds.Width / imageSize.Width,
                                     (float)bounds.Height / imageSize.Height);

                int scaledWidth = (int)(imageSize.Width * scale);
                int scaledHeight = (int)(imageSize.Height * scale);

                int x = bounds.Left + (bounds.Width - scaledWidth) / 2;
                int y = bounds.Top + (bounds.Height - scaledHeight) / 2;

                e.Graphics!.DrawImage(image, x, y, scaledWidth, scaledHeight);
            };

            printDoc.Print();

            _logger.LogInformation($"Image printed: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to print image: {filePath}");
            return false;
        }
    }

    public Task<bool> TestPrintAsync()
    {
        try
        {
            var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = _defaultPrinter;

            printDoc.PrintPage += (sender, e) =>
            {
                var font = new Font("Arial", 12);
                e.Graphics!.DrawString("PrintHero Test Page",
                    new Font("Arial", 16, FontStyle.Bold), Brushes.Black, 100, 100);
                e.Graphics.DrawString($"Printer: {_defaultPrinter}", font, Brushes.Black, 100, 150);
                e.Graphics.DrawString($"Date: {DateTime.Now}", font, Brushes.Black, 100, 180);
            };

            printDoc.Print();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test print failed");
            return Task.FromResult(false);
        }
    }
}