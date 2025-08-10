using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Services;
using PrintHero.UI.ViewModels;
using Serilog;
using System.IO;
using MessageBox = System.Windows.MessageBox;
using Microsoft.Win32;
using System.Reflection;
using PrintHero.Core.Services;

namespace PrintHero.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ILogger<App>? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Configure Serilog first
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PrintHero", "logs", "printhero-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30)
                .CreateLogger();

            Log.Information("PrintHero application starting...");

            // Register exit event handlers to stop services with safe handling
            try
            {
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                this.Exit += OnApplicationExit;
            }
            catch (Exception eventEx)
            {
                Log.Warning(eventEx, "Failed to register exit event handlers - app will continue but cleanup may be limited");
            }

            try
            {

                _host = CreateHost();
                await _host.StartAsync();

                _logger = _host.Services.GetService<ILogger<App>>();
                _logger?.LogInformation("Host started successfully");

                var mainWindow = CreateMainWindow();

                var args = Environment.GetCommandLineArgs();
                if (args.Contains("-minimized"))
                {
                    mainWindow.WindowState = WindowState.Minimized;
                    mainWindow.Show();
                    mainWindow.Hide();
                }
                else
                {
                    mainWindow.Show();
                }

                SetupAutoStart();
                
                // For installed apps, the MSI already installs the service
                // For development, skip service installation to avoid file locking
                // The app will use the service if it exists, or fall back to local monitoring

                base.OnStartup(e);
                Log.Information("PrintHero application started successfully");
            }
            catch (Exception hostEx)
            {
                Log.Error(hostEx, "Failed to create host, running with basic functionality");
                
                var detailedError = $"Host Initialization Error:\n{hostEx.Message}\n\nStack Trace:\n{hostEx.StackTrace}";
                
                // Write error to a simple text file for debugging
                try
                {
                    var errorLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrintHero", "startup-error.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(errorLogPath));
                    File.WriteAllText(errorLogPath, $"{DateTime.Now}: {detailedError}");
                }
                catch { }

                // Fallback: create basic main window without DI
                var fallbackWindow = new MainWindow();
                fallbackWindow.Show();

                MessageBox.Show($"Application started with limited functionality due to initialization error:\n{hostEx.Message}\n\nDetails logged to: %APPDATA%\\PrintHero\\startup-error.txt\n\nSome features may not work properly.",
                    "Startup Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                base.OnStartup(e);
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Critical error during application startup:\n{ex.Message}\n\nDetails:\n{ex.StackTrace}";

            Log.Fatal(ex, "Critical startup error");

            MessageBox.Show(errorMessage, "Critical Startup Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);

            Shutdown();
        }
    }

    private IHost CreateHost()
    {
        return new HostBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                try
                {
                    // Register core services with error handling
                    RegisterCoreServices(services);

                    // Register ViewModels
                    RegisterViewModels(services);

                    // Register Views
                    services.AddTransient<MainWindow>(provider =>
                    {
                        var viewModel = provider.GetRequiredService<MainViewModel>();
                        var logger = provider.GetService<ILogger<MainWindow>>();
                        return new MainWindow(viewModel, logger);
                    });

                    Log.Information("Services registered successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error registering services");
                    throw;
                }
            })
            .Build();
    }

    private void RegisterCoreServices(IServiceCollection services)
    {
        try
        {
            services.AddSingleton<JsonConfigService>();
            Log.Information("JsonConfigService registered");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to register JsonConfigService");
        }

        try
        {
            services.AddSingleton<IPrintingService, PrintingService>();
            Log.Information("PrintingService registered");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to register PrintingService");
        }

        try
        {
            services.AddSingleton<IFileMonitoringService, FileMonitoringService>();
            Log.Information("FileMonitoringService registered");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to register FileMonitoringService");
        }

        try
        {
            services.AddSingleton<IAppSettingsService, AppSettingsService>();
            Log.Information("AppSettingsService registered");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to register AppSettingsService");
        }

        try
        {
            services.AddSingleton<WindowsServiceController>();
            Log.Information("WindowsServiceController registered");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to register WindowsServiceController");
        }

        // LicensingService removed - no licensing required
    }

    private void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<MainViewModel>(provider =>
        {
            try
            {
                var fileMonitoring = provider.GetService<IFileMonitoringService>();
                var printing = provider.GetService<IPrintingService>();
                var settings = provider.GetService<IAppSettingsService>();
                var logger = provider.GetService<ILogger<MainViewModel>>();
                var jsonConfig = provider.GetService<JsonConfigService>();
                var databaseService = (PrintHero.Core.Data.SqliteDatabaseService?)null;
                var serviceController = provider.GetService<WindowsServiceController>();

                Log.Information("Creating MainViewModel with services: FileMonitoring={FileMonitoring}, Printing={Printing}, Settings={Settings}, JsonConfig={JsonConfig}, Database={Database}, ServiceController={ServiceController}",
                    fileMonitoring != null, printing != null, settings != null, jsonConfig != null, databaseService != null, serviceController != null);

                if (fileMonitoring != null && printing != null && settings != null)
                {
                    return new MainViewModel(fileMonitoring, printing, settings, logger, jsonConfig, databaseService, serviceController);
                }
                else
                {
                    Log.Warning("Some services are null, creating basic MainViewModel");
                    return new MainViewModel();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating MainViewModel, returning basic instance");
                return new MainViewModel();
            }
        });
    }

    private MainWindow CreateMainWindow()
    {
        try
        {
            if (_host != null)
            {
                var mainWindow = _host.Services.GetService<MainWindow>();
                if (mainWindow != null)
                {
                    Log.Information("MainWindow created via DI");
                    return mainWindow;
                }
            }

            Log.Warning("Creating MainWindow without DI");
            return new MainWindow();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating MainWindow via DI, creating basic instance");
            return new MainWindow();
        }
    }

    private void SetupAutoStart()
    {
        try
        {
            const string appName = "PrintHero";
            string exePath = Assembly.GetExecutingAssembly().Location;
            string appPath = Path.ChangeExtension(exePath, ".exe");

            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null)
                {
                    object? existingValue = key.GetValue(appName);
                    
                    if (existingValue == null || existingValue.ToString() != $"\"{appPath}\" -minimized")
                    {
                        key.SetValue(appName, $"\"{appPath}\" -minimized");
                        Log.Information("Auto-start configured for PrintHero at: {AppPath}", appPath);
                    }
                    else
                    {
                        Log.Information("Auto-start already configured for PrintHero");
                    }
                }
                else
                {
                    Log.Warning("Could not access Windows Run registry key");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to configure auto-start functionality");
        }
    }

    public static void RemoveAutoStart()
    {
        try
        {
            const string appName = "PrintHero";
            
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null)
                {
                    object? existingValue = key.GetValue(appName);
                    if (existingValue != null)
                    {
                        key.DeleteValue(appName);
                        Log.Information("Auto-start removed for PrintHero");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to remove auto-start functionality");
        }
    }

    public static bool IsAutoStartEnabled()
    {
        try
        {
            const string appName = "PrintHero";
            
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
            {
                if (key != null)
                {
                    object? existingValue = key.GetValue(appName);
                    return existingValue != null;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check auto-start status");
        }
        
        return false;
    }

    private async Task TryInstallBackgroundService()
    {
        try
        {
            // Check if we've already attempted installation
            const string regKey = @"SOFTWARE\PrintHero";
            const string regValue = "ServiceInstallAttempted";
            
            using (var key = Registry.CurrentUser.OpenSubKey(regKey))
            {
                if (key?.GetValue(regValue) != null)
                {
                    // Already attempted installation
                    return;
                }
            }

            // Try to install the service
            if (_host?.Services != null)
            {
                var serviceController = _host.Services.GetService<PrintHero.Core.Services.WindowsServiceController>();
                if (serviceController != null)
                {
                    Log.Information("Attempting to install background service...");
                    var installed = await serviceController.InstallServiceAsync();
                    
                    if (installed)
                    {
                        Log.Information("Background service installed successfully");
                    }
                    else
                    {
                        Log.Warning("Background service installation failed - app will use local monitoring");
                    }
                }
            }

            // Mark that we've attempted installation (whether successful or not)
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(regKey);
                key?.SetValue(regValue, "1");
            }
            catch (Exception regEx)
            {
                Log.Warning(regEx, "Failed to update registry after service installation attempt");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to install background service during startup");
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        try
        {
            Log.Information("Process exit detected, stopping services...");
            StopAllServicesSync();
        }
        catch (Exception ex)
        {
            try
            {
                Log.Error(ex, "Error stopping services on process exit");
            }
            catch
            {
                // Even logging failed - just exit cleanly
            }
        }
    }

    private void OnApplicationExit(object? sender, ExitEventArgs e)
    {
        try
        {
            Log.Information("Application exit detected, stopping services...");
            StopAllServicesSync();
        }
        catch (Exception ex)
        {
            try
            {
                Log.Error(ex, "Error stopping services on application exit");
            }
            catch
            {
                // Even logging failed - just exit cleanly
            }
        }
    }

    private void StopAllServicesSync()
    {
        try
        {
            // Only force kill processes in development mode
            // In installed apps, let Windows service continue running
            if (!IsInstalledApp())
            {
                // Development mode: force kill processes to prevent file locking
                var processes = System.Diagnostics.Process.GetProcessesByName("PrintHero.Service");
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000); // Wait up to 5 seconds
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

                // Try to stop Windows service if it exists (development only)
                if (_host?.Services != null)
                {
                    var serviceController = _host.Services.GetService<PrintHero.Core.Services.WindowsServiceController>();
                    if (serviceController != null)
                    {
                        try
                        {
                            serviceController.StopServiceAsync().Wait(TimeSpan.FromSeconds(5));
                        }
                        catch (Exception)
                        {
                            // Silent fail
                        }
                    }
                }
            }
            // In installed mode: Windows service should continue running in background
        }
        catch (Exception)
        {
            // Silent fail
        }
    }

    private bool IsInstalledApp()
    {
        try
        {
            // Check if we're running from Program Files or a typical installation directory
            var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var appDirectory = Path.GetDirectoryName(appPath) ?? string.Empty;
            
            // Installed apps typically run from Program Files, not from bin/Debug or bin/Release
            return !appDirectory.Contains("bin\\Debug") && 
                   !appDirectory.Contains("bin\\Release") && 
                   !appDirectory.Contains("\\obj\\") &&
                   (appDirectory.Contains("Program Files") || 
                    appDirectory.Contains("ProgramFiles") ||
                    !appDirectory.Contains("Projects") &&
                    !appDirectory.Contains("Source") &&
                    !appDirectory.Contains("src"));
        }
        catch
        {
            // If we can't determine, assume it's installed to be safe
            return true;
        }
    }

}
