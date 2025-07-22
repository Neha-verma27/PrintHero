using System.Diagnostics;
using System.Drawing.Printing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using PdfiumViewer;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Models;
using PrintHero.Core.Data;
using System.Data;

namespace PrintHero.Core.Services;

public class PrintingService : IPrintingService
{
    private readonly ILogger<PrintingService> _logger;
    private readonly SqliteDatabaseService? _database;
    private string? _defaultPrinter;

    public bool IsEnabled { get; set; } = true;

    public PrintingService(ILogger<PrintingService> logger, SqliteDatabaseService? database = null)
    {
        _logger = logger;
        _database = database;
        _defaultPrinter = new PrinterSettings().PrinterName;
    }

    public void SetPrinterSettings(string printerName, string paperSize, string orientation)
    {
        _defaultPrinter = printerName;
        _logger.LogInformation($"Printer settings updated: {printerName}, {paperSize}, {orientation}");
    }

    public async Task<bool> PrintFileAsync(string filePath)
    {
        PrintJob? printJob = null;
        
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

            printJob = await CreatePrintJobAsync(filePath);
            
            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            bool success = false;

            switch (fileExtension)
            {
                case ".pdf":
                    success = await PrintPdfAsync(filePath, printJob);
                    break;
                case ".txt":
                case ".doc":
                case ".docx":
                    success = await PrintDocumentAsync(filePath, printJob);
                    break;
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                    success = await PrintImageAsync(filePath, printJob);
                    break;
                default:
                    await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, $"Unsupported file type: {fileExtension}");
                    _logger.LogWarning($"Unsupported file type: {fileExtension}");
                    return false;
            }

            return success;
        }
        catch (Exception ex)
        {
            await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, ex.Message);
            _logger.LogError(ex, $"Failed to print file: {filePath}");
            return false;
        }
    }

    private async Task<bool> PrintPdfAsync(string filePath, PrintJob? printJob = null)
    {
        try
        {
            await UpdatePrintJobAsync(printJob, PrintJobStatus.Printing);
            _logger.LogInformation($"Attempting to print PDF: {filePath}");

            if (await TryPrintPDFFile(filePath))
            {
                await UpdatePrintJobAsync(printJob, PrintJobStatus.Completed);
                _logger.LogInformation("PDF printed successfully");
                await MoveFileAfterPrint(filePath);
                return true;
            }

            await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, "PDF printing failed");
            _logger.LogError("PDF printing failed");
            return false;
        }
        catch (Exception ex)
        {
            await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, ex.Message);
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

            if (string.IsNullOrEmpty(_defaultPrinter))
            {
                _logger.LogError("No printer configured");
                return false;
            }

            if (!IsPrinterAvailable(_defaultPrinter))
            {
                _logger.LogError($"Printer not available: {_defaultPrinter}");
                return false;
            }

            if (!IsPrinterOnline(_defaultPrinter))
            {
                _logger.LogError($"Printer is offline: {_defaultPrinter}");
                return false;
            }

            using var document = PdfDocument.Load(pdfPath);
            if (document.PageCount == 0)
            {
                _logger.LogError($"PDF has no pages: {pdfPath}");
                return false;
            }

            _logger.LogInformation($"PDF loaded successfully. Pages: {document.PageCount}");

            using var printDoc = document.CreatePrintDocument();

            printDoc.PrinterSettings.PrinterName = _defaultPrinter;
            printDoc.PrintController = new StandardPrintController(); // No print dialog
            printDoc.DocumentName = Path.GetFileName(pdfPath);

            var printCompleted = false;
            var printError = false;
            string? errorMessage = null;

            // Subscribe to print events to verify actual printing
            printDoc.EndPrint += (sender, e) =>
            {
                printCompleted = true;
                if (e.Cancel)
                {
                    printError = true;
                    errorMessage = "Print job was cancelled";
                }
                else if (e.PrintAction == PrintAction.PrintToFile)
                {
                    printError = true;
                    errorMessage = "Print job was redirected to file";
                }
            };

            printDoc.PrintPage += (sender, e) =>
            {
                // This event fires for each page being printed
                // If this doesn't fire, the print job didn't start
                _logger.LogDebug($"Printing page for document: {Path.GetFileName(pdfPath)}");
            };

            _logger.LogInformation($"Sending PDF to printer: {_defaultPrinter} (Pages: {document.PageCount})");
            
            // Send to printer
            printDoc.Print();

            // Wait for print completion with timeout
            var timeout = DateTime.Now.AddSeconds(30);
            while (!printCompleted && DateTime.Now < timeout)
            {
                await Task.Delay(100);
            }

            if (!printCompleted)
            {
                _logger.LogError($"Print job timed out for: {pdfPath}");
                return false;
            }

            if (printError)
            {
                _logger.LogError($"Print job failed: {errorMessage} for file: {pdfPath}");
                return false;
            }

            if (!await VerifyPrintJobProcessed(_defaultPrinter))
            {
                _logger.LogWarning($"Could not verify print job was processed by printer: {_defaultPrinter}");
                // Don't return false here as the job might still be successful
            }

            _logger.LogInformation($"PDF printed successfully: {pdfPath}");
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

            string sourceDirectory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
            string printedFolder = Path.Combine(sourceDirectory, "Printed");

            if (!Directory.Exists(printedFolder))
            {
                Directory.CreateDirectory(printedFolder);
                _logger.LogInformation($"Created directory: {printedFolder}");
            }

            string fileName = Path.GetFileName(sourceFilePath);
            string destinationPath = Path.Combine(printedFolder, fileName);

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

    private async Task<bool> PrintDocumentAsync(string filePath, PrintJob? printJob = null)
    {
        try
        {
            await UpdatePrintJobAsync(printJob, PrintJobStatus.Printing);

            if (!IsPrinterOnline(_defaultPrinter))
            {
                var errorMsg = $"Printer is offline, cannot print document: {_defaultPrinter}";
                await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, errorMsg);
                _logger.LogError(errorMsg);
                return false;
            }

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

            // Verify the process completed successfully
            if (process.ExitCode != 0)
            {
                var errorMsg = $"Print process exited with error code {process.ExitCode}";
                await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, errorMsg);
                _logger.LogError($"{errorMsg} for file: {filePath}");
                return false;
            }

            // Wait and verify print job
            if (!await VerifyPrintJobProcessed(_defaultPrinter))
            {
                _logger.LogWarning($"Could not verify document print job was processed: {filePath}");
            }

            await UpdatePrintJobAsync(printJob, PrintJobStatus.Completed);
            _logger.LogInformation($"Document printed: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, ex.Message);
            _logger.LogError(ex, $"Failed to print document: {filePath}");
            return false;
        }
    }

    private async Task<bool> PrintImageAsync(string filePath, PrintJob? printJob = null)
    {
        try
        {
            await UpdatePrintJobAsync(printJob, PrintJobStatus.Printing);

            if (!IsPrinterOnline(_defaultPrinter))
            {
                var errorMsg = $"Printer is offline, cannot print image: {_defaultPrinter}";
                await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, errorMsg);
                _logger.LogError(errorMsg);
                return false;
            }

            var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = _defaultPrinter;
            printDoc.PrintController = new StandardPrintController();

            using var image = Image.FromFile(filePath);

            var printCompleted = false;
            var printError = false;
            string? errorMessage = null;

            printDoc.EndPrint += (sender, e) =>
            {
                printCompleted = true;
                if (e.Cancel)
                {
                    printError = true;
                    errorMessage = "Image print job was cancelled";
                }
            };

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

            // Wait for print completion
            var timeout = DateTime.Now.AddSeconds(30);
            while (!printCompleted && DateTime.Now < timeout)
            {
                await Task.Delay(100);
            }

            if (!printCompleted)
            {
                var errorMsg = "Image print job timed out";
                await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, errorMsg);
                _logger.LogError($"{errorMsg}: {filePath}");
                return false;
            }

            if (printError)
            {
                await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, errorMessage);
                _logger.LogError($"Image print job failed: {errorMessage} for file: {filePath}");
                return false;
            }

            // Verify print job
            if (!await VerifyPrintJobProcessed(_defaultPrinter))
            {
                _logger.LogWarning($"Could not verify image print job was processed: {filePath}");
            }

            await UpdatePrintJobAsync(printJob, PrintJobStatus.Completed);
            _logger.LogInformation($"Image printed: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            await UpdatePrintJobAsync(printJob, PrintJobStatus.Failed, ex.Message);
            _logger.LogError(ex, $"Failed to print image: {filePath}");
            return false;
        }
    }

    private bool IsPrinterAvailable(string printerName)
    {
        try
        {
            foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
            {
                if (string.Equals(printer, printerName, StringComparison.OrdinalIgnoreCase))
                {

                    var printerSettings = new PrinterSettings();
                    printerSettings.PrinterName = printerName;
                    return printerSettings.IsValid;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking printer availability: {printerName}");
            return false;
        }
    }

    private bool IsPrinterOnline(string printerName)
    {
        try
        {
            var printerSettings = new PrinterSettings();
            printerSettings.PrinterName = printerName;
            
            if (!printerSettings.IsValid)
            {
                _logger.LogError($"Printer settings are invalid: {printerName}");
                return false;
            }

            if (!printerSettings.CanDuplex && !printerSettings.IsPlotter)
            {
                // Basic printer status check via WMI
                return CheckPrinterStatusViaWMI(printerName);
            }

            return true; // Assume online if we can't determine status
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking printer online status: {printerName}");
            return false;
        }
    }

    private bool CheckPrinterStatusViaWMI(string printerName)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT * FROM Win32_Printer WHERE Name = '{printerName.Replace("\\", "\\\\")}'");
            
            using var collection = searcher.Get();
            
            foreach (System.Management.ManagementObject printer in collection)
            {
                var printerState = printer["PrinterState"];
                var printerStatus = printer["PrinterStatus"];
                var workOffline = printer["WorkOffline"];

                if (workOffline != null && (bool)workOffline)
                {
                    _logger.LogWarning($"Printer is set to work offline: {printerName}");
                    return false;
                }

                if (printerState != null)
                {
                    var state = Convert.ToUInt32(printerState);
                    if (state == 2) // Error state
                    {
                        _logger.LogWarning($"Printer is in error state: {printerName}");
                        return false;
                    }
                    if (state == 1) // Paused state
                    {
                        _logger.LogWarning($"Printer is paused: {printerName}");
                        return false;
                    }
                }

                if (printerStatus != null)
                {
                    var status = Convert.ToUInt32(printerStatus);
                    if (status == 2) // Unknown status might indicate offline
                    {
                        _logger.LogWarning($"Printer status unknown, might be offline: {printerName}");
                        return false;
                    }
                }

                return true; // Printer appears to be online
            }

            _logger.LogWarning($"Printer not found in WMI: {printerName}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking printer status via WMI: {printerName}");
            return true; // Assume online if we can't check
        }
    }

    private async Task<bool> VerifyPrintJobProcessed(string printerName)
    {
        try
        {
            // Wait a moment for the job to appear in queue
            await Task.Delay(1000);

            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT * FROM Win32_PrintJob WHERE Name LIKE '%{printerName.Replace("\\", "\\\\")}%'");
            
            using var collection = searcher.Get();
            
            var jobCount = collection.Count;
            _logger.LogDebug($"Found {jobCount} print jobs in queue for printer: {printerName}");

            // If there are jobs in queue, check their status
            foreach (System.Management.ManagementObject job in collection)
            {
                var status = job["Status"];
                var document = job["Document"];
                
                _logger.LogDebug($"Print job status: {status}, Document: {document}");
                
                // Job statuses: "Printing", "Spooling", "Printed", "Error", etc.
                if (status != null && status.ToString().Contains("Error"))
                {
                    _logger.LogError($"Print job has error status: {status}");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error verifying print job: {printerName}");
            return true; // Don't fail the print if we can't verify
        }
    }

    private async Task<PrintJob?> CreatePrintJobAsync(string filePath)
    {
        if (_database == null) return null;

        try
        {
            var fileInfo = new FileInfo(filePath);
            var printJob = new PrintJob
            {
                FileName = fileInfo.Name,
                FilePath = filePath,
                FileSize = fileInfo.Length,
                FileType = fileInfo.Extension.ToLowerInvariant(),
                PrinterName = _defaultPrinter ?? string.Empty,
                Status = PrintJobStatus.Pending,
                ProcessingStarted = DateTime.Now
            };

            var sql = @"INSERT INTO PrintJobs (FileName, FilePath, FileSize, FileType, PrinterName, Status, ProcessingStarted, CreatedAt)
                       VALUES (@FileName, @FilePath, @FileSize, @FileType, @PrinterName, @Status, @ProcessingStarted, @CreatedAt)";

            await _database.ExecuteNonQueryAsync(sql,
                new SqliteParameter("@FileName", printJob.FileName),
                new SqliteParameter("@FilePath", printJob.FilePath),
                new SqliteParameter("@FileSize", printJob.FileSize),
                new SqliteParameter("@FileType", printJob.FileType),
                new SqliteParameter("@PrinterName", printJob.PrinterName),
                new SqliteParameter("@Status", (int)printJob.Status),
                new SqliteParameter("@ProcessingStarted", printJob.ProcessingStarted?.ToString("yyyy-MM-dd HH:mm:ss")),
                new SqliteParameter("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

            try
            {
                var getId = await _database.ExecuteScalarAsync<long?>("SELECT last_insert_rowid()");
                printJob.Id = (int)(getId ?? 0L);
            }
            catch
            {
                printJob.Id = 0;
            }
            return printJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create print job record for: {FilePath}", filePath);
            return null;
        }
    }

    private async Task UpdatePrintJobAsync(PrintJob? printJob, PrintJobStatus status, string? errorMessage = null)
    {
        if (_database == null || printJob == null) return;

        try
        {
            var sql = @"UPDATE PrintJobs 
                       SET Status = @Status, ErrorMessage = @ErrorMessage, ProcessingCompleted = @ProcessingCompleted, UpdatedAt = @UpdatedAt
                       WHERE Id = @Id";

            var processingCompleted = status == PrintJobStatus.Completed || status == PrintJobStatus.Failed 
                ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") 
                : null;

            await _database.ExecuteNonQueryAsync(sql,
                new SqliteParameter("@Id", printJob.Id),
                new SqliteParameter("@Status", (int)status),
                new SqliteParameter("@ErrorMessage", errorMessage),
                new SqliteParameter("@ProcessingCompleted", processingCompleted),
                new SqliteParameter("@UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

            printJob.Status = status;
            printJob.ErrorMessage = errorMessage;
            if (processingCompleted != null)
            {
                printJob.ProcessingCompleted = DateTime.Parse(processingCompleted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update print job record: {PrintJobId}", printJob.Id);
        }
    }

    public async Task<PrintJobStats> GetPrintJobStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        if (_database == null) return new PrintJobStats();

        try
        {
            fromDate ??= DateTime.Today;
            toDate ??= DateTime.Today.AddDays(1);

            var sql = @"SELECT 
                           COUNT(*) as TotalJobs,
                           SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) as CompletedJobs,
                           SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) as FailedJobs,
                           SUM(CASE WHEN Status < 2 THEN 1 ELSE 0 END) as PendingJobs
                       FROM PrintJobs 
                       WHERE CreatedAt >= @FromDate AND CreatedAt < @ToDate";

            var stats = await _database.ExecuteQueryAsync(sql, reader => new PrintJobStats
            {
                TotalJobs = reader.GetInt32("TotalJobs"),
                CompletedJobs = reader.GetInt32("CompletedJobs"),
                FailedJobs = reader.GetInt32("FailedJobs"),
                PendingJobs = reader.GetInt32("PendingJobs"),
                LastUpdated = DateTime.Now
            }, 
                new SqliteParameter("@FromDate", fromDate.Value.ToString("yyyy-MM-dd HH:mm:ss")),
                new SqliteParameter("@ToDate", toDate.Value.ToString("yyyy-MM-dd HH:mm:ss")));

            return stats.FirstOrDefault() ?? new PrintJobStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get print job statistics");
            return new PrintJobStats();
        }
    }

    public async Task<List<PrintJob>> GetRecentPrintJobsAsync(int limit = 50)
    {
        if (_database == null) return new List<PrintJob>();

        try
        {
            var sql = @"SELECT * FROM PrintJobs 
                       ORDER BY CreatedAt DESC 
                       LIMIT @Limit";

            return await _database.ExecuteQueryAsync(sql, reader => new PrintJob
            {
                Id = reader.GetInt32("Id"),
                FileName = reader.GetString("FileName"),
                FilePath = reader.GetString("FilePath"),
                FileSize = reader.GetInt64("FileSize"),
                FileType = reader.GetString("FileType"),
                PrinterName = reader.GetString("PrinterName"),
                Status = (PrintJobStatus)reader.GetInt32("Status"),
                ErrorMessage = reader.IsDBNull("ErrorMessage") ? null : reader.GetString("ErrorMessage"),
                ProcessingStarted = reader.IsDBNull("ProcessingStarted") ? null : DateTime.Parse(reader.GetString("ProcessingStarted")),
                ProcessingCompleted = reader.IsDBNull("ProcessingCompleted") ? null : DateTime.Parse(reader.GetString("ProcessingCompleted")),
                MonitoredFolderId = reader.IsDBNull("MonitoredFolderId") ? null : reader.GetInt32("MonitoredFolderId"),
                CreatedAt = DateTime.Parse(reader.GetString("CreatedAt")),
                UpdatedAt = DateTime.Parse(reader.GetString("UpdatedAt"))
            }, new SqliteParameter("@Limit", limit));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent print jobs");
            return new List<PrintJob>();
        }
    }
}