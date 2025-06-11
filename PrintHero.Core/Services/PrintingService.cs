using System.Diagnostics;
using System.Drawing.Printing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using PdfiumViewer;
using PrintHero.Core.Interfaces;

namespace PrintHero.Core.Services;

public class PrintingService : IPrintingService
{
    private readonly ILogger<PrintingService> _logger;
    private string? _defaultPrinter;

    public bool IsEnabled { get; set; } = false;

    public PrintingService(ILogger<PrintingService> logger)
    {
        _logger = logger;
        _defaultPrinter = new PrinterSettings().PrinterName;
    }

    public void SetPrinterSettings(string printerName, string paperSize, string orientation)
    {
        _defaultPrinter = printerName;
        _logger.LogInformation($"Printer settings updated: {printerName}, {paperSize}, {orientation}");
    }

    public async Task<bool> PrintFileAsync(string filePath)
    {
        try
        {
            if (!IsEnabled)
            {
                _logger.LogInformation("Printing is disabled - skipping file: {FilePath}", filePath);
                return false;
            }

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
            _logger.LogInformation($"Attempting to print PDF: {filePath}");

            if (await TryPrintPDFFile(filePath))
            {
                _logger.LogInformation("PDF printed successfully with PowerShell");
                await MoveFileAfterPrint(filePath);
                return true;
            }

            _logger.LogError("PDF printing failed");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to print PDF: {filePath}");
            return false;
        }
    }

    private async Task<bool> TryPrintPDFFile(string pdfPath)
    {
        try
        {
            if (!File.Exists(pdfPath))
            {
                _logger.LogError($"PDF not found: {pdfPath}");
                return false;
            }

            using var document = PdfDocument.Load(pdfPath);
            using var printDoc = document.CreatePrintDocument();

            printDoc.PrinterSettings.PrinterName = _defaultPrinter ?? new PrinterSettings().PrinterName;
            printDoc.PrintController = new StandardPrintController(); // No print dialog
            printDoc.DocumentName = Path.GetFileName(pdfPath);

            _logger.LogInformation($"Sending PDF to printer: {_defaultPrinter}");
            printDoc.Print();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error printing PDF: {pdfPath}");
            return false;
        }
    }

    private async Task MoveFileAfterPrint(string sourceFilePath)
    {
        try
        {
            // Get the directory of the source file
            string sourceDirectory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
            string printedFolder = Path.Combine(sourceDirectory, "Printed");

            // Create date subfolder
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            string destinationFolder = Path.Combine(printedFolder, dateFolder);

            // Create destination directory if it doesn't exist
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
                _logger.LogInformation($"Created directory: {destinationFolder}");
            }

            string fileName = Path.GetFileName(sourceFilePath);
            string destinationPath = Path.Combine(destinationFolder, fileName);

            // Handle filename conflicts
            destinationPath = GetUniqueFileName(destinationPath);

            // Move the file
            File.Move(sourceFilePath, destinationPath);
            _logger.LogInformation($"File moved from {sourceFilePath} to {destinationPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error moving file after print: {sourceFilePath}");
        }
    }

    private string GetUniqueFileName(string filePath)
    {
        if (!File.Exists(filePath))
            return filePath;

        string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);

        int counter = 1;
        string newFilePath;

        do
        {
            string newFileName = $"{fileNameWithoutExtension}_{counter:D3}{extension}";
            newFilePath = Path.Combine(directory, newFileName);
            counter++;
        }
        while (File.Exists(newFilePath));

        return newFilePath;
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
}