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

    public MainWindow() : this(null, null)
    {
    }

    public MainWindow(MainViewModel viewModel, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _viewModel = viewModel ?? new MainViewModel(); 
        _logger = logger;
        DataContext = _viewModel;

        this.Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadInitialDataAsync();
        
        // Debug: Check DataContext and binding state
        _logger?.LogInformation("MainWindow loaded - DataContext is ViewModel: {IsViewModel}, ViewModel IsServiceRunning: {IsRunning}", 
            DataContext == _viewModel, _viewModel?.IsServiceRunning ?? false);
            
        // Force a final UI refresh after everything is loaded
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (_viewModel != null)
            {
                // Wait a moment for all async operations to complete
                await Task.Delay(1000);
                
                _logger?.LogInformation("Final UI refresh - IsServiceRunning: {IsRunning}, Toggle IsChecked: {IsChecked}", 
                    _viewModel.IsServiceRunning, PowerToggle.IsChecked);
                    
                // Ensure toggle always matches ViewModel state after initialization
                if (PowerToggle.IsChecked != _viewModel.IsServiceRunning)
                {
                    _logger?.LogInformation("?? Syncing toggle state - ViewModel: {ViewModelState}, Toggle: {ToggleState}", 
                        _viewModel.IsServiceRunning, PowerToggle.IsChecked);
                    PowerToggle.IsChecked = _viewModel.IsServiceRunning;
                }
                
                // For the desired behavior, ensure service is always ON
                if (_viewModel.IsServiceRunning)
                {
                    _logger?.LogInformation("? PrintHero is running and monitoring - Toggle is ON");
                }
                else
                {
                    _logger?.LogWarning("?? PrintHero service is not running - this may not be the desired state");
                }
            }
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            if (_viewModel != null)
            {
                // Wait for ViewModel to fully initialize
                _logger?.LogInformation("Waiting for ViewModel initialization...");
                await _viewModel.InitializationTask;
                _logger?.LogInformation("ViewModel initialization completed, refreshing UI...");

                await RefreshUIFromViewModel();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load initial data");
        }
    }

    private async Task RefreshUIFromViewModel()
    {
        try
        {
            if (_viewModel != null)
            {
                // Force refresh of key properties
                _viewModel.NotifyPropertyChanged(nameof(_viewModel.IsServiceRunning));
                _viewModel.NotifyPropertyChanged(nameof(_viewModel.DefaultPrinter));
                _viewModel.NotifyPropertyChanged(nameof(_viewModel.PaperSize));
                _viewModel.NotifyPropertyChanged(nameof(_viewModel.FirstMonitoredFolder));
                _viewModel.NotifyPropertyChanged(nameof(_viewModel.FilesProcessedToday));
                _viewModel.NotifyPropertyChanged(nameof(_viewModel.PrintingErrorsToday));

                // Log current state for debugging
                _logger?.LogInformation("UI refreshed - Printer: {Printer}, Folder: {Folder}, IsServiceRunning: {IsRunning}", 
                    _viewModel.DefaultPrinter, _viewModel.FirstMonitoredFolder, _viewModel.IsServiceRunning);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh UI from ViewModel");
        }
    }

    private void PowerToggle_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            // Always execute start command to save user preference, regardless of current service state
            if (_viewModel != null)
            {
                if (_viewModel.StartServiceCommand != null)
                {
                    _viewModel.StartServiceCommand.Execute(null);
                    _logger?.LogInformation("Service started via UI toggle");
                }
                else
                {
                    _logger?.LogWarning("StartServiceCommand is null - ViewModel not properly initialized");
                }
            }
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
            // Always execute stop command to save user preference, regardless of current service state
            if (_viewModel != null)
            {
                if (_viewModel.StopServiceCommand != null)
                {
                    _viewModel.StopServiceCommand.Execute(null);
                    _logger?.LogInformation("Service stopped via UI toggle");
                }
                else
                {
                    _logger?.LogWarning("StopServiceCommand is null - ViewModel not properly initialized");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop service");
        }
    }

    private async void PrinterSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printerSettingsWindow = new PrinterSettingsWindow(_logger);
            
            // Pass current settings to the window
            if (_viewModel != null)
            {
                printerSettingsWindow.LoadExistingSettings(_viewModel.DefaultPrinter, _viewModel.PaperSize, _viewModel.Orientation);
            }
            
            printerSettingsWindow.Owner = this;
            var result = printerSettingsWindow.ShowDialog();

            if (result == true)
            {
                if (!string.IsNullOrEmpty(printerSettingsWindow.SelectedPrinter) && _viewModel != null)
                {
                    _viewModel.DefaultPrinter = printerSettingsWindow.SelectedPrinter;
                }

                if (!string.IsNullOrEmpty(printerSettingsWindow.PaperSize) && _viewModel != null)
                {
                    string paperSize = printerSettingsWindow.PaperSize;
                    if (paperSize.Contains("("))
                    {
                        paperSize = paperSize.Substring(0, paperSize.IndexOf("(")).Trim();
                    }
                    _viewModel.PaperSize = paperSize;
                }
                
                if (!string.IsNullOrEmpty(printerSettingsWindow.Orientation) && _viewModel != null)
                {
                    _viewModel.Orientation = printerSettingsWindow.Orientation;
                }
                
                // Save the settings to persistent storage
                await _viewModel.SaveSettingsAsync();
                
                // Update the PrintingService with the new settings immediately
                await UpdatePrintingServiceSettingsAsync();
                
                _logger?.LogInformation($"Printer settings updated and saved - Printer: {printerSettingsWindow.SelectedPrinter}, Paper: {printerSettingsWindow.PaperSize}, Orientation: {printerSettingsWindow.Orientation}");
                _logger?.LogInformation($"ViewModel now has - Printer: {_viewModel.DefaultPrinter}, Paper: {_viewModel.PaperSize}, Orientation: {_viewModel.Orientation}");

                MessageBox.Show("Printer settings have been updated and saved successfully!",
                    "Settings Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open printer settings window");
            MessageBox.Show($"Failed to open printer settings: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void HotFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderSettingsWindow = new FolderSettingsWindow(_logger);
            
            // Load current hot folder settings if available
            if (_viewModel != null && _viewModel.MonitoredFolders.Any())
            {
                var currentFolder = _viewModel.MonitoredFolders.First();
                folderSettingsWindow.LoadExistingSettings(
                    currentFolder.FolderPath,
                    currentFolder.FilePattern,
                    currentFolder.IncludeSubfolders
                );
                _logger?.LogInformation("Loaded current folder settings: {FolderPath}", currentFolder.FolderPath);
            }
            
            folderSettingsWindow.Owner = this;
            var result = folderSettingsWindow.ShowDialog();

            if (result == true && _viewModel != null)
            {
                if (!string.IsNullOrEmpty(folderSettingsWindow.FolderPathTextBox.Text))
                {
                    var updatedFolder = new MonitoredFolder
                    {
                        FolderPath = folderSettingsWindow.FolderPathTextBox.Text,
                        FilePattern = folderSettingsWindow.FilePatternTextBox.Text,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };

                    // Update existing folder or add new one
                    if (_viewModel.MonitoredFolders.Any())
                    {
                        // Update the first (current) folder
                        var existingFolder = _viewModel.MonitoredFolders.First();
                        updatedFolder.Id = existingFolder.Id; // Preserve ID
                        updatedFolder.CreatedAt = existingFolder.CreatedAt; // Preserve creation date
                        
                        // Remove old and add updated
                        _viewModel.MonitoredFolders.Clear();
                        _viewModel.MonitoredFolders.Add(updatedFolder);
                        
                        _logger?.LogInformation("Updated existing folder: {OldPath} -> {NewPath}", 
                            existingFolder.FolderPath, updatedFolder.FolderPath);
                    }
                    else
                    {
                        // Add as new folder
                        _viewModel.MonitoredFolders.Add(updatedFolder);
                        _logger?.LogInformation("Added new folder: {FolderPath}", updatedFolder.FolderPath);
                    }
                    
                    // Force save the complete settings
                    await _viewModel.SaveSettingsAsync();
                    
                    // Force refresh the UI to show the updated folder
                    _viewModel.NotifyPropertyChanged(nameof(_viewModel.FirstMonitoredFolder));
                    
                    // Restart the monitoring service to pick up the new folder settings
                    await RestartMonitoringServiceAsync();
                    
                    // Log the current state for debugging
                    _logger?.LogInformation("Hot folder settings updated. Now monitoring: {FirstFolder}", 
                        _viewModel.FirstMonitoredFolder);
                    
                    MessageBox.Show($"Hot folder settings have been updated successfully!\n\nNow monitoring: {_viewModel.FirstMonitoredFolder}\nFile pattern: {updatedFolder.FilePattern}", 
                        "Settings Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Please specify a valid folder path.", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save hot folder settings");
            MessageBox.Show($"Failed to save folder settings: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RestartMonitoringServiceAsync()
    {
        try
        {
            if (_viewModel?.IsServiceRunning == true)
            {
                _logger?.LogInformation("Restarting monitoring service to update folder settings");
                
                // Get access to the file monitoring service through the ViewModel
                var fileMonitoringService = GetFileMonitoringService();
                if (fileMonitoringService != null)
                {
                    // Restart monitoring with the updated folder settings
                    await fileMonitoringService.RestartMonitoringAsync(_viewModel.MonitoredFolders);
                    _logger?.LogInformation("Monitoring service restarted successfully with new folder settings");
                }
                else
                {
                    _logger?.LogWarning("Could not access file monitoring service - settings may not take effect until service restart");
                }
            }
            else
            {
                _logger?.LogInformation("Service not running - folder settings will be applied when service starts");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to restart monitoring service");
        }
    }

    private PrintHero.Core.Interfaces.IFileMonitoringService? GetFileMonitoringService()
    {
        try
        {
            // Use reflection to access the private _fileMonitoringService field in MainViewModel
            var viewModelType = _viewModel.GetType();
            var field = viewModelType.GetField("_fileMonitoringService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(_viewModel) as PrintHero.Core.Interfaces.IFileMonitoringService;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to access file monitoring service via reflection");
            return null;
        }
    }

    private async Task UpdatePrintingServiceSettingsAsync()
    {
        try
        {
            if (_viewModel == null) return;

            // Get access to the printing service through reflection
            var printingService = GetPrintingService();
            if (printingService != null)
            {
                // Update the printing service with current ViewModel settings
                printingService.SetPrinterSettings(
                    _viewModel.DefaultPrinter ?? string.Empty,
                    _viewModel.PaperSize,
                    _viewModel.Orientation
                );
                
                _logger?.LogInformation("PrintingService updated with new settings - Printer: {Printer}, Paper: {Paper}, Orientation: {Orientation}", 
                    _viewModel.DefaultPrinter, _viewModel.PaperSize, _viewModel.Orientation);
            }
            else
            {
                _logger?.LogWarning("Could not access printing service - new printer settings may not take effect until service restart");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update printing service settings");
        }
    }

    private PrintHero.Core.Interfaces.IPrintingService? GetPrintingService()
    {
        try
        {
            // Use reflection to access the private _printingService field in MainViewModel
            var viewModelType = _viewModel.GetType();
            var field = viewModelType.GetField("_printingService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(_viewModel) as PrintHero.Core.Interfaces.IPrintingService;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to access printing service via reflection");
            return null;
        }
    }
}