using System.Windows;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Models;
using PrintHero.UI.ViewModels;
using PrintHero.UI.Views;
using MessageBox = System.Windows.MessageBox;
using System.Linq;
using System.IO;

namespace PrintHero.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            _viewModel = new MainViewModel(); 
            _logger = null;
            DataContext = _viewModel;
            
            // Subscribe to PropertyChanged to sync toggle state
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
            
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }
        catch (Exception ex)
        {
            // Log to Windows Event Log if regular logging fails
            try
            {
                System.Diagnostics.EventLog.WriteEntry("PrintHero", 
                    $"Critical error in MainWindow constructor: {ex.Message}", 
                    System.Diagnostics.EventLogEntryType.Error);
            }
            catch { }
            throw; // Re-throw to prevent app from continuing in bad state
        }
    }

    public MainWindow(MainViewModel viewModel, ILogger<MainWindow> logger)
    {
        try
        {
            InitializeComponent();
            _viewModel = viewModel ?? new MainViewModel(); 
            _logger = logger;
            DataContext = _viewModel;

            // Subscribe to PropertyChanged to sync toggle state
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }
        catch (Exception ex)
        {
            // Log to Windows Event Log if regular logging fails
            try
            {
                System.Diagnostics.EventLog.WriteEntry("PrintHero", 
                    $"Critical error in MainWindow constructor (with DI): {ex.Message}", 
                    System.Diagnostics.EventLogEntryType.Error);
            }
            catch { }
            throw; // Re-throw to prevent app from continuing in bad state
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsServiceRunning))
        {
            // Sync toggle with ViewModel state on UI thread
            Dispatcher.BeginInvoke(() =>
            {
                PowerToggle.IsChecked = _viewModel?.IsServiceRunning ?? false;
            });
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadInitialDataAsync();
        
        // Force a final UI refresh after everything is loaded
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (_viewModel != null)
            {
                await Task.Delay(1000);
                
                // Always sync toggle with ViewModel state
                PowerToggle.IsChecked = _viewModel.IsServiceRunning;
            }
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            if (_viewModel != null)
            {
                await _viewModel.InitializationTask;

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

            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh UI from ViewModel");
        }
    }

    private async void PowerToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        
        // Disable toggle during operation
        PowerToggle.IsEnabled = false;
        
        try
        {
            // Start service operations in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await CallStartServiceAsync();
                    
                    // Update UI on main thread based on actual service state
                    Dispatcher.BeginInvoke(() =>
                    {
                        // Sync toggle with ViewModel state
                        PowerToggle.IsChecked = _viewModel.IsServiceRunning;
                        PowerToggle.IsEnabled = true;
                    });
                    
                    // Save settings asynchronously without blocking
                    _ = Task.Run(async () => await _viewModel.SaveSettingsAsync());
                }
                catch (Exception ex)
                {
                    // Update UI on main thread with error state - toggle should be OFF
                    // Don't show error message here - let ViewModel handle it to avoid duplicates
                    Dispatcher.BeginInvoke(() =>
                    {
                        PowerToggle.IsChecked = false; // Force toggle OFF on error
                        PowerToggle.IsEnabled = true;
                    });
                }
            });
            
            // Re-enable toggle quickly for responsive feel
            await Task.Delay(200);
            PowerToggle.IsEnabled = true;
        }
        catch (Exception ex)
        {
            PowerToggle.IsChecked = false;
            PowerToggle.IsEnabled = true;
            // Don't show error message here - let ViewModel handle it to avoid duplicates
        }
    }
    
    private async Task CallStartServiceAsync()
    {
        if (_viewModel?.StartServiceCommand.CanExecute(null) == true)
        {
            _viewModel.StartServiceCommand.Execute(null);
            // Reduced delay for faster perceived response
            await Task.Delay(500);
        }
    }

    private async void PowerToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        
        // Immediately provide visual feedback - don't wait for service operations
        PowerToggle.IsEnabled = false;
        
        try
        {
            // Stop service operations in background without blocking UI
            _ = Task.Run(async () =>
            {
                try
                {
                    await CallStopServiceAsync();
                    
                    // Update UI on main thread
                    Dispatcher.BeginInvoke(() =>
                    {
                        PowerToggle.IsChecked = _viewModel.IsServiceRunning;
                        PowerToggle.IsEnabled = true;
                    });
                    
                    // Save settings asynchronously without blocking
                    _ = Task.Run(async () => await _viewModel.SaveSettingsAsync());
                }
                catch (Exception ex)
                {
                    // Update UI on main thread with error state
                    // Don't show error message here - let ViewModel handle it to avoid duplicates
                    Dispatcher.BeginInvoke(() =>
                    {
                        PowerToggle.IsEnabled = true;
                    });
                }
            });
            
            // Re-enable toggle immediately for better UX (operations continue in background)
            await Task.Delay(100); // Brief delay to show the toggle is processing
            PowerToggle.IsEnabled = true;
        }
        catch (Exception ex)
        {
            PowerToggle.IsEnabled = true;
            // Don't show error message here - let ViewModel handle it to avoid duplicates
        }
    }
    
    private async Task CallStopServiceAsync()
    {
        if (_viewModel?.StopServiceCommand.CanExecute(null) == true)
        {
            _viewModel.StopServiceCommand.Execute(null);
            // Reduced delay for faster perceived response
            await Task.Delay(500);
        }
    }

    private async Task ForceKillBackgroundService()
    {
        try
        {
            // Find and kill PrintHero.Service.exe processes
            var processes = System.Diagnostics.Process.GetProcessesByName("PrintHero.Service");
            
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    await process.WaitForExitAsync();
                    await Task.Delay(1000); // Wait for cleanup
                }
                catch (Exception)
                {
                    // Process might already be gone
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception)
        {
            // Silent fail
        }
    }

    private async Task CreateDefaultFolder()
    {
        try
        {
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                "PrintHero", "Input");

            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }

            var defaultFolder = new PrintHero.Core.Models.MonitoredFolder
            {
                FolderPath = defaultPath,
                FilePattern = "*.pdf",
                IsActive = true,
                IncludeSubfolders = false,
                CreatedAt = DateTime.Now
            };

            _viewModel.MonitoredFolders?.Add(defaultFolder);
            await _viewModel.SaveSettingsAsync();
        }
        catch (Exception)
        {
            // Silent fail
        }
    }

    private async void AddPrintJob_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printJobSettingsWindow = new PrinterSettingsWindow(_logger);
            
            // Pass existing job names for uniqueness validation
            if (_viewModel?.PrintJobConfigurations != null)
            {
                var existingJobNames = _viewModel.PrintJobConfigurations
                    .Select(job => job.JobName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();
                printJobSettingsWindow.SetExistingJobNames(existingJobNames);
            }
            
            printJobSettingsWindow.Owner = this;
            var result = printJobSettingsWindow.ShowDialog();

            if (result == true && _viewModel != null)
            {
                // Create new print job configuration from the settings window
                var newPrintJobConfig = new PrintHero.Core.Models.PrintJobConfiguration
                {
                    Id = (_viewModel.PrintJobConfigurations?.Any() == true) 
                        ? _viewModel.PrintJobConfigurations.Max(j => j.Id) + 1 
                        : 1,
                    JobName = printJobSettingsWindow.JobName,
                    HotFolderPath = printJobSettingsWindow.HotFolderPath,
                    FilePattern = printJobSettingsWindow.FileType,
                    PrinterName = printJobSettingsWindow.SelectedPrinter,
                    PaperType = printJobSettingsWindow.PaperSize,
                    Orientation = printJobSettingsWindow.Orientation,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                // Add to the print job configurations collection
                if (_viewModel.PrintJobConfigurations == null)
                {
                    _viewModel.PrintJobConfigurations = new System.Collections.ObjectModel.ObservableCollection<PrintHero.Core.Models.PrintJobConfiguration>();
                }
                
                _viewModel.PrintJobConfigurations.Add(newPrintJobConfig);
                
                // Save the settings to persistent storage
                await _viewModel.SaveSettingsAsync();
                
                // Restart the monitoring service to include the new print job
                if (_viewModel.IsServiceRunning)
                {
                    await RestartMonitoringServiceAsync();
                }

                MessageBox.Show($"Print job '{newPrintJobConfig.DisplayName}' has been created successfully!",
                    "Print Job Created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create print job");
            MessageBox.Show($"Failed to create print job: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RemovePrintJob_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel?.PrintJobConfigurations == null || !_viewModel.PrintJobConfigurations.Any())
            {
                MessageBox.Show("No print jobs available to remove.", "No Print Jobs",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Get the selected print job from the DataGrid
            var selectedPrintJob = PrintJobsGrid.SelectedItem as PrintHero.Core.Models.PrintJobConfiguration;
            if (selectedPrintJob == null)
            {
                MessageBox.Show("Please select a print job to remove.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirm deletion
            var result = MessageBox.Show($"Are you sure you want to remove the print job '{selectedPrintJob.DisplayName}'?\n\nThis action cannot be undone.",
                "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Remove from the collection
                _viewModel.PrintJobConfigurations.Remove(selectedPrintJob);
                
                // Save the updated settings
                await _viewModel.SaveSettingsAsync();
                
                // Restart the monitoring service to remove the print job from monitoring
                if (_viewModel.IsServiceRunning)
                {
                    await RestartMonitoringServiceAsync();
                }

                MessageBox.Show($"Print job '{selectedPrintJob.DisplayName}' has been removed successfully!",
                    "Print Job Removed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to remove print job");
            MessageBox.Show($"Failed to remove print job: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    private async Task RestartMonitoringServiceAsync()
    {
        try
        {
            if (_viewModel?.IsServiceRunning == true)
            {
                var fileMonitoringService = GetFileMonitoringService();
                if (fileMonitoringService != null)
                {
                    await fileMonitoringService.StopMonitoringAsync();
                    
                    if (_viewModel.PrintJobConfigurations.Any())
                    {
                        await fileMonitoringService.StartMonitoringPrintJobsAsync(_viewModel.PrintJobConfigurations);
                    }
                    else if (_viewModel.MonitoredFolders.Any())
                    {
                        await fileMonitoringService.StartMonitoringAsync(_viewModel.MonitoredFolders);
                    }
                }
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
                printingService.SetPrinterSettings(
                    _viewModel.DefaultPrinter ?? string.Empty,
                    _viewModel.PaperSize,
                    _viewModel.Orientation
                );
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

    public async void TestToggleManually(bool turnOn)
    {
        try
        {
            if (turnOn)
            {
                PowerToggle_Checked(PowerToggle, new RoutedEventArgs());
            }
            else
            {
                PowerToggle_Unchecked(PowerToggle, new RoutedEventArgs());
            }
        }
        catch (Exception ex)
        {
        }
    }


    private PrintHero.Core.Services.WindowsServiceController? GetServiceController()
    {
        try
        {
            var viewModelType = _viewModel?.GetType();
            if (viewModelType == null) return null;
            
            var field = viewModelType.GetField("_serviceController", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(_viewModel) as PrintHero.Core.Services.WindowsServiceController;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // In installed apps, only stop local monitoring but keep Windows service running
            // In development, stop everything to prevent file locking
            if (IsInstalledApp())
            {
                // For installed apps: only stop local monitoring, let Windows service continue
                if (_viewModel?.StopServiceCommand.CanExecute(null) == true)
                {
                    // This will stop gracefully but Windows service will continue running in background
                    _viewModel.StopServiceCommand.Execute(null);
                }
            }
            else
            {
                // For development: stop everything including killing processes
                if (_viewModel?.StopServiceCommand.CanExecute(null) == true)
                {
                    _viewModel.StopServiceCommand.Execute(null);
                }
            }
        }
        catch (Exception)
        {
            // Silent fail - don't prevent window from closing
        }
    }

    private bool IsInstalledApp()
    {
        try
        {
            var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var appDirectory = Path.GetDirectoryName(appPath) ?? string.Empty;
            
            return !appDirectory.Contains("bin\\Debug") && 
                   !appDirectory.Contains("bin\\Release") && 
                   !appDirectory.Contains("\\obj\\") &&
                   (appDirectory.Contains("Program Files") || 
                    appDirectory.Contains("ProgramFiles") ||
                    (!appDirectory.Contains("Projects") &&
                     !appDirectory.Contains("Source") &&
                     !appDirectory.Contains("src")));
        }
        catch
        {
            return true;
        }
    }

    private bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    // Method to completely remove the background service (for troubleshooting)
    public async Task DeleteBackgroundServiceAsync()
    {
        try
        {
            var serviceController = GetServiceController();
            if (serviceController != null)
            {
                await serviceController.UninstallServiceAsync();
            }
            
            // Also force kill any running processes
            await ForceKillBackgroundService();
        }
        catch (Exception)
        {
            // Silent fail
        }
    }

    // Method to check if background service is installed
    public async Task<bool> IsBackgroundServiceInstalledAsync()
    {
        try
        {
            var serviceController = GetServiceController();
            if (serviceController != null)
            {
                return await serviceController.IsServiceInstalledAsync();
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Debug method to test service functionality
    public async Task<string> TestServiceStatusAsync()
    {
        try
        {
            var result = new System.Text.StringBuilder();
            
            result.AppendLine($"Is Installed App: {IsInstalledApp()}");
            result.AppendLine($"Running as Administrator: {IsRunningAsAdministrator()}");
            result.AppendLine($"ViewModel IsServiceRunning: {_viewModel?.IsServiceRunning}");
            
            var serviceController = GetServiceController();
            if (serviceController == null)
            {
                result.AppendLine("ServiceController is null");
                return result.ToString();
            }

            var isInstalled = await serviceController.IsServiceInstalledAsync();
            result.AppendLine($"Service installed: {isInstalled}");
            
            if (isInstalled)
            {
                var isRunning = await serviceController.IsServiceRunningAsync();
                result.AppendLine($"Service running: {isRunning}");
            }
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // Simple test method to manually trigger toggle
    public async Task TestToggleOnAsync()
    {
        try
        {
            if (_viewModel?.StartServiceCommand.CanExecute(null) == true)
            {
                _viewModel.StartServiceCommand.Execute(null);
                await Task.Delay(2000); // Wait for service to start
            }
            
            System.Windows.MessageBox.Show($"Service started\n\n{await TestServiceStatusAsync()}", 
                "Toggle Test", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error: {ex.Message}", "Toggle Test Error", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

}