using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace PrintHero.UI.Views;

public partial class FolderSettingsWindow : Window
{
    private readonly ILogger<FolderSettingsWindow>? _logger;

    public FolderSettingsWindow()
    {
        InitializeComponent();
        _logger = null;
        LoadCurrentSettings();
    }

    public FolderSettingsWindow(ILogger<FolderSettingsWindow>? logger)
    {
        InitializeComponent();
        _logger = logger;
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        try
        {
            FolderPathTextBox.Text = @"C:\PrintHero\Input";
            FilePatternTextBox.Text = "*.pdf";
            IncludeSubfoldersCheckBox.IsChecked = false;
            //DeleteAfterPrintCheckBox.IsChecked = false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load current settings");
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = "Select folder to monitor for PDF files",
                UseDescriptionForTitle = true,
                SelectedPath = FolderPathTextBox.Text
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FolderPathTextBox.Text = dialog.SelectedPath;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to browse for folder");
            System.Windows.MessageBox.Show($"Failed to browse for folder: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(FolderPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please select a folder to monitor.", "Validation Error",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(FolderPathTextBox.Text))
            {
                var result = System.Windows.MessageBox.Show("The selected folder does not exist. Would you like to create it?",
                                           "Folder Not Found",
                                           MessageBoxButton.YesNo,
                                           MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        Directory.CreateDirectory(FolderPathTextBox.Text);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to create folder: {ex.Message}", "Error",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(FilePatternTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please enter a file pattern.", "Validation Error",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save folder settings");
            System.Windows.MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}