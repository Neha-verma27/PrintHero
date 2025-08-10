using System.Drawing.Printing;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace PrintHero.UI.Views
{
    public partial class PrinterSettingsWindow : Window
    {
        private readonly ILogger? _logger;
        private List<string> _availablePrinters = new();
        private List<string> _existingJobNames = new();

        public string? SelectedPrinter { get; private set; }
        public string PaperSize { get; private set; } = "A4";
        public string Orientation { get; private set; } = "Portrait";
        public string JobName { get; private set; } = string.Empty;
        public string HotFolderPath { get; private set; } = string.Empty;
        public string FileType { get; private set; } = "*.PDF";

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

        public void SetExistingJobNames(List<string> existingJobNames)
        {
            _existingJobNames = existingJobNames ?? new List<string>();
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

                foreach (string printerName in PrinterSettings.InstalledPrinters)
                {
                    _availablePrinters.Add(printerName);
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
                        PrinterSelectionTextBox.Text = defaultPrinter;
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

                // Apply settings if available printers are loaded
                if (_availablePrinters.Any())
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
                if (!string.IsNullOrEmpty(_pendingPrinter))
                {
                    PrinterSelectionTextBox.Text = _pendingPrinter;
                    _logger?.LogInformation($"Set printer: {_pendingPrinter}");
                }

                // Load existing paper size
                if (!string.IsNullOrEmpty(_pendingPaperSize))
                {
                    SelectComboBoxItem(PaperTypeComboBox, _pendingPaperSize);
                    _logger?.LogInformation($"Set paper size: {_pendingPaperSize}");
                }
                else
                {
                    SelectComboBoxItem(PaperTypeComboBox, "A4"); // Default to A4
                }

                // Load existing orientation
                if (!string.IsNullOrEmpty(_pendingOrientation))
                {
                    SelectComboBoxItem(OrientationComboBox, _pendingOrientation);
                    _logger?.LogInformation($"Set orientation: {_pendingOrientation}");
                }
                else
                {
                    SelectComboBoxItem(OrientationComboBox, "Portrait"); // Default to Portrait
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
                if (PaperTypeComboBox.SelectedItem == null)
                    SelectComboBoxItem(PaperTypeComboBox, "A4");
                if (OrientationComboBox.SelectedItem == null)
                    SelectComboBoxItem(OrientationComboBox, "Portrait");

                _logger?.LogInformation("Current settings loaded with defaults");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load current settings");
            }
        }

        private void UpdatePrinterStatus(string status)
        {
            // Status is now static text in XAML, no need to update dynamically
            _logger?.LogInformation($"Printer status: {status}");
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
                if (string.IsNullOrEmpty(PrinterSelectionTextBox.Text))
                {
                    MessageBox.Show("Please enter a printer name first.", "No Printer Selected",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string selectedPrinter = PrinterSelectionTextBox.Text;
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
                    ev.Graphics.DrawString($"Paper Size: {GetComboBoxText(PaperTypeComboBox)}", font, brush, 100, 180);
                    ev.Graphics.DrawString($"Orientation: {GetComboBoxText(OrientationComboBox)}", font, brush, 100, 210);
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
                if (string.IsNullOrEmpty(PrinterSelectionTextBox.Text))
                {
                    MessageBox.Show("Please enter a printer name.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate required field
                if (string.IsNullOrEmpty(JobNameTextBox.Text))
                {
                    MessageBox.Show("Please enter a job name.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate job name uniqueness
                var jobName = JobNameTextBox.Text.Trim();
                if (_existingJobNames.Any(name => string.Equals(name, jobName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"A print job with the name '{jobName}' already exists. Please choose a different name.", "Duplicate Job Name",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    JobNameTextBox.Focus();
                    JobNameTextBox.SelectAll();
                    return;
                }

                if (string.IsNullOrEmpty(HotFolderTextBox.Text))
                {
                    MessageBox.Show("Please enter a hot folder path.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                JobName = JobNameTextBox.Text.Trim();
                HotFolderPath = HotFolderTextBox.Text.Trim();
                FileType = string.IsNullOrEmpty(FileTypeTextBox.Text) ? "*.PDF" : FileTypeTextBox.Text.Trim();
                SelectedPrinter = PrinterSelectionTextBox.Text;
                PaperSize = GetComboBoxText(PaperTypeComboBox);
                Orientation = GetComboBoxText(OrientationComboBox);

                _logger?.LogInformation($"Saving printer settings: {SelectedPrinter}, {PaperSize}, {Orientation}");

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

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folderDialog = new OpenFolderDialog()
                {
                    Title = "Select Hot Folder",
                    Multiselect = false
                };

                if (!string.IsNullOrEmpty(HotFolderTextBox.Text) && Directory.Exists(HotFolderTextBox.Text))
                {
                    folderDialog.InitialDirectory = HotFolderTextBox.Text;
                }

                if (folderDialog.ShowDialog() == true)
                {
                    HotFolderTextBox.Text = folderDialog.FolderName;
                    _logger?.LogInformation($"Hot folder selected: {folderDialog.FolderName}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open folder browser");
                MessageBox.Show($"Failed to open folder browser: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowsePrinter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_availablePrinters.Any())
                {
                    LoadPrinters();
                    if (!_availablePrinters.Any())
                    {
                        MessageBox.Show("No printers are available on this system.", "No Printers",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Create a simple selection dialog
                var printerSelectionWindow = new PrinterSelectionWindow(_availablePrinters, _logger);
                printerSelectionWindow.Owner = this;
                
                if (printerSelectionWindow.ShowDialog() == true)
                {
                    PrinterSelectionTextBox.Text = printerSelectionWindow.SelectedPrinter;
                    _logger?.LogInformation($"Printer selected: {printerSelectionWindow.SelectedPrinter}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open printer browser");
                MessageBox.Show($"Failed to open printer browser: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectComboBoxItem(ComboBox comboBox, string value)
        {
            try
            {
                foreach (ComboBoxItem item in comboBox.Items)
                {
                    if (item.Content?.ToString() == value)
                    {
                        comboBox.SelectedItem = item;
                        return;
                    }
                }
                // If not found, select first item as fallback
                if (comboBox.Items.Count > 0)
                {
                    comboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to select ComboBox item: {value}");
            }
        }

        private string GetComboBoxText(ComboBox comboBox)
        {
            try
            {
                return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get ComboBox text");
                return string.Empty;
            }
        }
    }
}