using System.Drawing.Printing;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;

namespace PrintHero.UI.Views
{
    public partial class PrinterSettingsWindow : Window
    {
        private readonly ILogger? _logger;
        private List<string> _availablePrinters = new();

        public string? SelectedPrinter { get; private set; }
        public string PaperSize { get; private set; } = "A4";
        public string Orientation { get; private set; } = "Portrait";

        public PrinterSettingsWindow()
        {
            InitializeComponent();
            LoadPrinters();
            this.Loaded += PrinterSettingsWindow_Loaded;
        }
        public PrinterSettingsWindow(ILogger logger) : this()
        {
            _logger = logger;
        }

        public PrinterSettingsWindow(ILogger<PrinterSettingsWindow> logger) : this()
        {
            _logger = logger;
        }

        private void PrinterSettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadPrinters();
                LoadCurrentSettings();
                _logger?.LogInformation("Printer Settings window loaded successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load printer settings window");
                MessageBox.Show($"Failed to load printer settings: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPrinters()
        {
            try
            {
                _availablePrinters.Clear();
                PrinterComboBox.Items.Clear();

                foreach (string printerName in PrinterSettings.InstalledPrinters)
                {
                    _availablePrinters.Add(printerName);
                    PrinterComboBox.Items.Add(printerName);
                }

                if (_availablePrinters.Any())
                {
                    // Apply pending settings if they exist, otherwise use system default
                    if (!string.IsNullOrEmpty(_pendingPrinter))
                    {
                        ApplyPendingSettings();
                    }
                    else
                    {
                        var defaultPrinter = new PrinterSettings().PrinterName;
                        var defaultIndex = _availablePrinters.IndexOf(defaultPrinter);

                        if (defaultIndex >= 0)
                        {
                            PrinterComboBox.SelectedIndex = defaultIndex;
                        }
                        else
                        {
                            PrinterComboBox.SelectedIndex = 0;
                        }
                    }

                    UpdatePrinterStatus("Ready");
                }
                else
                {
                    UpdatePrinterStatus("No printers found");
                    MessageBox.Show("No printers are installed on this system.",
                        "No Printers", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                _logger?.LogInformation($"Loaded {_availablePrinters.Count} printers");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load printers");
                UpdatePrinterStatus("Error loading printers");
                MessageBox.Show($"Failed to load printers: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string? _pendingPrinter;
        private string? _pendingPaperSize;
        private string? _pendingOrientation;

        public void LoadExistingSettings(string? defaultPrinter, string paperSize, string orientation)
        {
            try
            {
                // Store the settings to be applied after printers are loaded
                _pendingPrinter = defaultPrinter;
                _pendingPaperSize = paperSize;
                _pendingOrientation = orientation;

                _logger?.LogInformation($"Pending settings stored - Printer: {defaultPrinter}, Paper: {paperSize}, Orientation: {orientation}");

                // Apply settings if printers are already loaded
                if (PrinterComboBox.Items.Count > 0)
                {
                    ApplyPendingSettings();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to store existing settings");
            }
        }

        private void ApplyPendingSettings()
        {
            try
            {
                // Load existing printer selection if available
                if (!string.IsNullOrEmpty(_pendingPrinter) && PrinterComboBox.Items.Count > 0)
                {
                    for (int i = 0; i < PrinterComboBox.Items.Count; i++)
                    {
                        if (PrinterComboBox.Items[i].ToString() == _pendingPrinter)
                        {
                            PrinterComboBox.SelectedIndex = i;
                            _logger?.LogInformation($"Selected printer: {_pendingPrinter} at index {i}");
                            break;
                        }
                    }
                }

                // Load existing paper size
                if (!string.IsNullOrEmpty(_pendingPaperSize))
                {
                    for (int i = 0; i < PaperSizeComboBox.Items.Count; i++)
                    {
                        if (PaperSizeComboBox.Items[i] is ComboBoxItem item && 
                            item.Content.ToString()!.StartsWith(_pendingPaperSize, StringComparison.OrdinalIgnoreCase))
                        {
                            PaperSizeComboBox.SelectedIndex = i;
                            _logger?.LogInformation($"Selected paper size: {_pendingPaperSize} at index {i}");
                            break;
                        }
                    }
                }
                else
                {
                    PaperSizeComboBox.SelectedIndex = 0; // Default to A4
                }

                // Load existing orientation
                _logger?.LogInformation($"Attempting to set orientation: '{_pendingOrientation}' (Total items: {OrientationComboBox.Items.Count})");
                if (!string.IsNullOrEmpty(_pendingOrientation))
                {
                    if (_pendingOrientation.Equals("Portrait", StringComparison.OrdinalIgnoreCase))
                    {
                        OrientationComboBox.SelectedIndex = 0;
                        _logger?.LogInformation($"Selected orientation: Portrait (index 0), Current selection: {OrientationComboBox.SelectedIndex}");
                    }
                    else if (_pendingOrientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase))
                    {
                        OrientationComboBox.SelectedIndex = 1;
                        _logger?.LogInformation($"Selected orientation: Landscape (index 1), Current selection: {OrientationComboBox.SelectedIndex}");
                    }
                    else
                    {
                        _logger?.LogWarning($"Unknown orientation value: '{_pendingOrientation}', defaulting to Portrait");
                        OrientationComboBox.SelectedIndex = 0;
                    }
                }
                else
                {
                    OrientationComboBox.SelectedIndex = 0; // Default to Portrait
                    _logger?.LogInformation("No pending orientation, defaulting to Portrait");
                }

                _logger?.LogInformation("Applied pending settings successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply pending settings");
            }
        }

        private void LoadCurrentSettings()
        {
            try
            {
                // Set default values if no existing settings are loaded
                if (PaperSizeComboBox.SelectedIndex == -1)
                    PaperSizeComboBox.SelectedIndex = 0; // A4
                if (OrientationComboBox.SelectedIndex == -1)
                    OrientationComboBox.SelectedIndex = 0; // Portrait

                _logger?.LogInformation("Current settings loaded with defaults");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load current settings");
            }
        }

        private void UpdatePrinterStatus(string status)
        {
            if (status.ToLower().Contains("ready"))
            {
                PrinterStatusText.Text = $"Status: {status}";
                PrinterStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            else if (status.ToLower().Contains("error") || status.ToLower().Contains("not found"))
            {
                PrinterStatusText.Text = $"Status: {status}";
                PrinterStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                PrinterStatusText.Text = $"Status: {status}";
                PrinterStatusText.Foreground = System.Windows.Media.Brushes.Orange;
            }
        }

        private void RefreshPrinters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogInformation("Refreshing printer list");
                LoadPrinters();
                MessageBox.Show("Printer list refreshed successfully.",
                    "Refresh Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to refresh printers");
                MessageBox.Show($"Failed to refresh printers: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PrinterComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Please select a printer first.", "No Printer Selected",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string selectedPrinter = PrinterComboBox.SelectedItem.ToString()!;
                _logger?.LogInformation($"Starting test print to: {selectedPrinter}");

                var printDoc = new PrintDocument();
                printDoc.PrinterSettings.PrinterName = selectedPrinter;

                printDoc.PrintPage += (s, ev) =>
                {
                    var font = new Font("Arial", 12);
                    var brush = Brushes.Black;

                    ev.Graphics!.DrawString("PrintHero Test Page",
                        new Font("Arial", 16, System.Drawing.FontStyle.Bold),
                        brush, 100, 100);

                    ev.Graphics.DrawString($"Printer: {selectedPrinter}", font, brush, 100, 150);
                    ev.Graphics.DrawString($"Paper Size: {PaperSizeComboBox.Text}", font, brush, 100, 180);
                    ev.Graphics.DrawString($"Orientation: {OrientationComboBox.Text}", font, brush, 100, 210);
                    ev.Graphics.DrawString($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", font, brush, 100, 240);
                    ev.Graphics.DrawString("This is a test print from PrintHero application.", font, brush, 100, 300);
                };

                printDoc.Print();

                MessageBox.Show("Test print sent successfully!", "Test Print",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                _logger?.LogInformation("Test print completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to perform test print");
                MessageBox.Show($"Test print failed: {ex.Message}",
                    "Test Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (PrinterComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Please select a printer.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedPrinter = PrinterComboBox.SelectedItem.ToString();

                if (PaperSizeComboBox.SelectedItem is ComboBoxItem paperItem)
                {
                    PaperSize = paperItem.Content.ToString()!;
                }

                if (OrientationComboBox.SelectedItem is ComboBoxItem orientationItem)
                {
                    Orientation = orientationItem.Content.ToString()!;

                    if (Orientation.Contains("Portrait"))
                        Orientation = "Portrait";
                    else if (Orientation.Contains("Landscape"))
                        Orientation = "Landscape";
                }

                _logger?.LogInformation($"Saving printer settings: {SelectedPrinter}, {PaperSize}, {Orientation}");

                MessageBox.Show("Printer settings saved successfully!", "Settings Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save printer settings");
                MessageBox.Show($"Failed to save settings: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInformation("Printer settings cancelled");
            DialogResult = false;
            Close();
        }
    }
}