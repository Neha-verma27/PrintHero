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
                    _logger?.LogInformation("🔄 Syncing toggle state - ViewModel: {ViewModelState}, Toggle: {ToggleState}", 
                        _viewModel.IsServiceRunning, PowerToggle.IsChecked);
                    PowerToggle.IsChecked = _viewModel.IsServiceRunning;
                }
                
                // For the desired behavior, ensure service is always ON
                if (_viewModel.IsServiceRunning)
                {
                    _logger?.LogInformation("✅ PrintHero is running and monitoring - Toggle is ON");
                }
                else
                {
                    _logger?.LogWarning("⚠️ PrintHero service is not running - this may not be the desired state");
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
                
                // Update UI with ViewModel data
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
            // Only start service if it's not already running (prevent circular calls)
            if (_viewModel != null && !_viewModel.IsServiceRunning)
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
            else
            {
                _logger?.LogInformation("Service already running - toggle checked event ignored");
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
            // Only stop service if it's currently running (prevent circular calls)
            if (_viewModel != null && _viewModel.IsServiceRunning)
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
            else
            {
                _logger?.LogInformation("Service already stopped - toggle unchecked event ignored");
            }
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
                // Update ViewModel with new settings - UI will auto-update via binding
                if (!string.IsNullOrEmpty(printerSettingsWindow.SelectedPrinter) && _viewModel != null)
                {
                    _viewModel.DefaultPrinter = printerSettingsWindow.SelectedPrinter;
                }

                // Update paper size
                if (!string.IsNullOrEmpty(printerSettingsWindow.PaperSize) && _viewModel != null)
                {
                    string paperSize = printerSettingsWindow.PaperSize;
                    if (paperSize.Contains("("))
                    {
                        paperSize = paperSize.Substring(0, paperSize.IndexOf("(")).Trim();
                    }
                    _viewModel.PaperSize = paperSize;
                }
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
                // Update ViewModel with new folder - UI will auto-update via binding
                if (!string.IsNullOrEmpty(folderSettingsWindow.FolderPathTextBox.Text) && _viewModel != null)
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
                    
                    // Notify that the folder list changed
                    _viewModel.NotifyPropertyChanged(nameof(_viewModel.FirstMonitoredFolder));
                }
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
}