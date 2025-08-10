using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PrintHero.Core.Interfaces;
using PrintHero.Core.Services;
using PrintHero.Service;
using System.Diagnostics;
using System.Security.Principal;

// Handle command line arguments for service installation
if (args.Length > 0)
{
    await HandleServiceCommands(args);
    return;
}

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "PrintHeroBackgroundService";
    })
    .ConfigureServices((context, services) =>
    {
        // Register PrintHero services
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IFileMonitoringService, FileMonitoringService>();
        services.AddSingleton<IPrintingService, PrintingService>();
        services.AddSingleton<JsonConfigService>();
        
        // Register the background service
        services.AddHostedService<PrintHeroBackgroundService>();
    })
    .Build();

await host.RunAsync();

static async Task HandleServiceCommands(string[] args)
{
    const string ServiceName = "PrintHeroBackgroundService";
    var command = args[0].ToLower();
    var executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

    switch (command)
    {
        case "install":
            await InstallService(ServiceName, executablePath);
            break;
        case "uninstall":
            await UninstallService(ServiceName);
            break;
        case "start":
            await StartService(ServiceName);
            break;
        case "stop":
            await StopService(ServiceName);
            break;
        default:
            Console.WriteLine("Usage: PrintHero.Service.exe [install|uninstall|start|stop]");
            break;
    }
}

static async Task<bool> InstallService(string serviceName, string executablePath)
{
    try
    {
        if (!IsAdministrator())
        {
            Console.WriteLine("Administrator privileges required to install service.");
            return false;
        }

        var processInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"create \"{serviceName}\" binPath=\"{executablePath}\" start=auto DisplayName=\"{serviceName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                Console.WriteLine("Service installed successfully.");
                return true;
            }
            else
            {
                Console.WriteLine($"Failed to install service. Output: {output}, Error: {error}");
                return false;
            }
        }
        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error installing service: {ex.Message}");
        return false;
    }
}

static async Task<bool> UninstallService(string serviceName)
{
    try
    {
        if (!IsAdministrator())
        {
            Console.WriteLine("Administrator privileges required to uninstall service.");
            return false;
        }

        // Stop service first
        await StopService(serviceName);

        var processInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"delete \"{serviceName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                Console.WriteLine("Service uninstalled successfully.");
                return true;
            }
            else
            {
                Console.WriteLine($"Failed to uninstall service. Output: {output}, Error: {error}");
                return false;
            }
        }
        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error uninstalling service: {ex.Message}");
        return false;
    }
}

static async Task<bool> StartService(string serviceName)
{
    try
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"start \"{serviceName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
            {
                Console.WriteLine("Service started successfully.");
                return true;
            }
            else
            {
                Console.WriteLine("Failed to start service.");
                return false;
            }
        }
        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error starting service: {ex.Message}");
        return false;
    }
}

static async Task<bool> StopService(string serviceName)
{
    try
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"stop \"{serviceName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
            {
                Console.WriteLine("Service stopped successfully.");
                return true;
            }
            else
            {
                Console.WriteLine("Service may not be running or failed to stop.");
                return true; // Don't treat as error if service wasn't running
            }
        }
        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error stopping service: {ex.Message}");
        return false;
    }
}

static bool IsAdministrator()
{
    var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}