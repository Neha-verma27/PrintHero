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
using PrintHero.Core.Data;

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

                base.OnStartup(e);
                Log.Information("PrintHero application started successfully");
            }
            catch (Exception hostEx)
            {
                Log.Error(hostEx, "Failed to create host, running with basic functionality");

                // Fallback: create basic main window without DI
                var fallbackWindow = new MainWindow();
                fallbackWindow.Show();

                MessageBox.Show($"Application started with limited functionality due to initialization error:\n{hostEx.Message}\n\nSome features may not work properly.",
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
        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
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
            services.AddSingleton<DatabaseService>();
            Log.Information("DatabaseService registered");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to register DatabaseService");
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
            //services.AddSingleton<ILicensingService, LicensingService>();
            Log.Information("LicensingService registered");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to register LicensingService");
        }
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

                Log.Information("Creating MainViewModel with services: FileMonitoring={FileMonitoring}, Printing={Printing}, Settings={Settings}",
                    fileMonitoring != null, printing != null, settings != null);

                if (fileMonitoring != null && printing != null && settings != null)
                {
                    return new MainViewModel(fileMonitoring, printing, settings, logger);
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
}