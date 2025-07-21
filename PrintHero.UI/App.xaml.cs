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

            try
            {
                // Create host with services
                _host = CreateHost();
                await _host.StartAsync();

                _logger = _host.Services.GetService<ILogger<App>>();
                _logger?.LogInformation("Host started successfully");

                // Create main window
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

                // Set up auto-start functionality
                SetupAutoStart();

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
                    services.AddTransient<MainWindow>();

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
            services.AddSingleton<PrintHero.Core.Data.SqliteDatabaseService>();
            Log.Information("SqliteDatabaseService registered");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to register SqliteDatabaseService");
        }

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
                var databaseService = provider.GetService<PrintHero.Core.Data.SqliteDatabaseService>();

                Log.Information("Creating MainViewModel with services: FileMonitoring={FileMonitoring}, Printing={Printing}, Settings={Settings}, JsonConfig={JsonConfig}, Database={Database}",
                    fileMonitoring != null, printing != null, settings != null, jsonConfig != null, databaseService != null);

                if (fileMonitoring != null && printing != null && settings != null)
                {
                    return new MainViewModel(fileMonitoring, printing, settings, logger, jsonConfig, databaseService);
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

}
