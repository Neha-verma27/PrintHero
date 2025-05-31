using System.Windows;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Models;
using PrintHero.UI.ViewModels;
using PrintHero.UI.Views;
using MessageBox = System.Windows.MessageBox;

namespace PrintHero.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(MainViewModel viewModel, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _viewModel = viewModel ?? new MainViewModel(); 
        _logger = logger;
        DataContext = _viewModel;

        this.Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadInitialData();
    }

    private void LoadInitialData()
    {
        try
        {
            FilesPrintedText.Text = _viewModel?.FilesProcessedToday.ToString() ?? "0";
            PrintingErrorsText.Text = "0";
            PrinterNameText.Text = _viewModel?.DefaultPrinter ?? "No printer selected";
            PaperSizeText.Text = "A4";

            var firstFolder = _viewModel?.MonitoredFolders?.FirstOrDefault();
            FolderPathText.Text = firstFolder?.FolderPath ?? "No folder selected";

            LicenseNumberText.Text = _viewModel?.LicenseKey ?? "**************";
            PowerToggle.IsChecked = _viewModel?.IsServiceRunning ?? false;

        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load initial data");
        }
    }

    private void PowerToggle_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel?.StartServiceCommand != null)
            {
                _viewModel.StartServiceCommand.Execute(null);
            }
            else
            {
                _logger?.LogWarning("StartServiceCommand is null - ViewModel not properly initialized");
            }

            _logger?.LogInformation("Service started via UI toggle");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start service");
        }
    }

    private void PowerToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel?.StopServiceCommand != null)
            {
                _viewModel.StopServiceCommand.Execute(null);
            }
            else
            {
                _logger?.LogWarning("StopServiceCommand is null - ViewModel not properly initialized");
            }

            _logger?.LogInformation("Service stopped via UI toggle");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop service");
        }
    }

    private void PrinterSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printerSettingsWindow = new PrinterSettingsWindow();
            printerSettingsWindow.Owner = this;
            var result = printerSettingsWindow.ShowDialog();

            if (result == true)
            {
                // Update main window with new settings
                if (!string.IsNullOrEmpty(printerSettingsWindow.SelectedPrinter))
                {
                    PrinterNameText.Text = printerSettingsWindow.SelectedPrinter;

                    // Update ViewModel if available
                    if (_viewModel != null)
                    {
                        _viewModel.DefaultPrinter = printerSettingsWindow.SelectedPrinter;
                    }
                }

                // Update paper size
                if (!string.IsNullOrEmpty(printerSettingsWindow.PaperSize))
                {
                    string paperSize = printerSettingsWindow.PaperSize;
                    if (paperSize.Contains("("))
                    {
                        paperSize = paperSize.Substring(0, paperSize.IndexOf("(")).Trim();
                    }
                    PaperSizeText.Text = paperSize;

                    if (_viewModel != null)
                    {
                        _viewModel.PaperSize = paperSize;
                    }
                }
                LoadPrinterSettings();
                _logger.LogInformation($"Printer settings updated Printer: {printerSettingsWindow.SelectedPrinter}, Paper: {printerSettingsWindow.PaperSize}, Orientation: {printerSettingsWindow.Orientation}");

                MessageBox.Show("Printer settings have been updated successfully!",
                    "Settings Updated", MessageBoxButton.OK, MessageBoxImage.Information);

            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open printer settings window");

            MessageBox.Show($"Failed to open printer settings: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void HotFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderSettingsWindow = new FolderSettingsWindow();
            folderSettingsWindow.Owner = this;
            var result = folderSettingsWindow.ShowDialog();

            if (result == true)
            {
                // Update main window with new settings
                if (!string.IsNullOrEmpty(folderSettingsWindow.FolderPathTextBox.Text))
                {
                    FolderPathText.Text = folderSettingsWindow.FolderPathTextBox.Text;

                    // Update ViewModel if available
                    if (_viewModel != null)
                    {
                        _viewModel.MonitoredFolders.Add(new MonitoredFolder
                        {
                            FolderPath = folderSettingsWindow.FolderPathTextBox.Text,
                            FilePattern = folderSettingsWindow.FilePatternTextBox.Text,
                            IncludeSubfolders = folderSettingsWindow.IncludeSubfoldersCheckBox.IsChecked == true,
                            PostPrintAction = folderSettingsWindow.DeleteAfterPrintingCheckBox.IsChecked == true
                                                ? PostPrintAction.DeleteFile
                                                : PostPrintAction.KeepFile,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                LoadFolderSettings();
                _logger.LogInformation("Folder settings updated successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open folder settings window");

            MessageBox.Show($"Failed to open folder settings: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LicenseActivation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open license activation window");

            MessageBox.Show($"Failed to open license activation: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadPrinterSettings()
    {
        try
        {
            PrinterNameText.Text = _viewModel.DefaultPrinter ?? "No printer selected";
            PaperSizeText.Text = _viewModel?.PaperSize ?? "A4";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load printer settings");
        }
    }

    private void LoadFolderSettings()
    {
        try
        {
            var firstFolder = _viewModel.MonitoredFolders.FirstOrDefault();
            FolderPathText.Text = firstFolder?.FolderPath ?? "No folder selected";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load folder settings");
        }
    }
}