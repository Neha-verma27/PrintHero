using System.Diagnostics;
using System.ServiceProcess;

namespace PrintHero.Core.Services;

public class WindowsServiceController
{
    private const string ServiceName = "PrintHeroBackgroundService";
    private const string ServiceExecutablePath = @"PrintHero.Service.exe";

    public WindowsServiceController()
    {
    }

    public async Task<bool> IsServiceInstalledAsync()
    {
        try
        {
            // Use safer service detection
            using var serviceManager = new System.Management.ManagementObjectSearcher(
                $"SELECT * FROM Win32_Service WHERE Name='{ServiceName}'");
            using var services = serviceManager.Get();
            return services.Count > 0;
        }
        catch (Exception ex)
        {
            // Fallback to ServiceController with safer handling
            try
            {
                using var service = new ServiceController(ServiceName);
                // Just accessing the service name will throw if service doesn't exist
                _ = service.ServiceName;
                return true;
            }
            catch (InvalidOperationException)
            {
                // Service doesn't exist
                return false;
            }
            catch (Exception serviceEx)
            {
                // Final fallback using sc.exe with better safety
                try
                {
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "sc.exe",
                            Arguments = $"query \"{ServiceName}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
                catch (Exception altEx)
                {
                    return false;
                }
            }
        }
    }

    public async Task<bool> IsServiceRunningAsync()
    {
        try
        {
            // First check if service exists
            if (!await IsServiceInstalledAsync())
            {
                return false;
            }

            using var service = new ServiceController(ServiceName);
            service.Refresh(); // Ensure we get current status
            return service.Status == ServiceControllerStatus.Running;
        }
        catch (Exception ex)
        {
            // Fallback using sc.exe with safer handling
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = $"query \"{ServiceName}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    return output?.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) == true;
                }
            }
            catch (Exception altEx)
            {
                // Ignore exception, return false below
            }
            
            return false;
        }
    }

    public async Task<bool> InstallServiceAsync()
    {
        try
        {
            if (await IsServiceInstalledAsync())
            {
                return true;
            }

            var serviceExePath = GetServiceExecutablePath();
            if (!File.Exists(serviceExePath))
            {
                // Service executable not found - this will prevent proper service installation
                return false;
            }
            
            // Verify the service executable is valid
            if (!IsValidServiceExecutable(serviceExePath))
            {
                return false;
            }

            // Try installation without elevation first (might work if already admin)
            if (await TryInstallService(serviceExePath, false))
            {
                return true;
            }

            // If that fails, try with elevation
            return await TryInstallService(serviceExePath, true);
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private async Task<bool> TryInstallService(string serviceExePath, bool withElevation)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"create \"{ServiceName}\" binPath=\"\"{serviceExePath}\"\" start=demand DisplayName=\"PrintHero Background Service\"",
                UseShellExecute = withElevation,
                CreateNoWindow = !withElevation,
                RedirectStandardOutput = !withElevation,
                RedirectStandardError = !withElevation
            };

            if (withElevation)
            {
                processInfo.Verb = "runas";
            }

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    if (!withElevation && process.ExitCode == 5) // Access denied
                    {
                        return false;
                    }
                }
            }

            return false;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled UAC prompt
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<bool> StartServiceAsync()
    {
        try
        {
            // First check if service is installed
            if (!await IsServiceInstalledAsync())
            {
                // For installed apps, service should already be installed by MSI
                // Only try to install if this appears to be a development scenario
                if (IsRunningFromDevelopment())
                {
                    if (!await InstallServiceAsync())
                    {
                        return false;
                    }
                    
                    // Wait a moment after installation
                    await Task.Delay(2000);
                }
                else
                {
                    return false;
                }
            }

            // Try to start the service
            try
            {
                using var service = new ServiceController(ServiceName);
                
                if (service.Status == ServiceControllerStatus.Running)
                {
                    return true;
                }

                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    try
                    {
                        service.Start();
                        
                        // Wait up to 30 seconds with shorter intervals
                        for (int i = 0; i < 30; i++)
                        {
                            await Task.Delay(1000);
                            service.Refresh();
                            
                            if (service.Status == ServiceControllerStatus.Running)
                            {
                                return true;
                            }
                            
                            // If service went back to stopped, it failed to start
                            if (service.Status == ServiceControllerStatus.Stopped)
                            {
                                return false;
                            }
                        }
                        
                        // Timeout - service didn't start in time
                        return false;
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Service failed to start - try reinstalling
                        var reinstalled = await ReinstallServiceAsync();
                        if (reinstalled)
                        {
                            // Try starting again after reinstall
                            try
                            {
                                using var newService = new ServiceController(ServiceName);
                                if (newService.Status == ServiceControllerStatus.Stopped)
                                {
                                    newService.Start();
                                    newService.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                                    return true;
                                }
                            }
                            catch
                            {
                                // Still failed after reinstall
                            }
                        }
                        return false;
                    }
                    catch (InvalidOperationException)
                    {
                        // Service in invalid state - try reinstalling
                        var reinstalled = await ReinstallServiceAsync();
                        if (reinstalled)
                        {
                            // Try starting again after reinstall
                            try
                            {
                                using var newService = new ServiceController(ServiceName);
                                if (newService.Status == ServiceControllerStatus.Stopped)
                                {
                                    newService.Start();
                                    newService.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                                    return true;
                                }
                            }
                            catch
                            {
                                // Still failed after reinstall
                            }
                        }
                        return false;
                    }
                }

                return false;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot open"))
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<bool> StopServiceAsync()
    {
        try
        {
            // First check if service exists
            if (!await IsServiceInstalledAsync())
            {
                return true;
            }

            // Method 1: Try ServiceController (most reliable)
            var serviceControllerResult = await TryStopViaServiceController();
            if (serviceControllerResult)
            {
                return true;
            }

            // Method 2: Try sc.exe without elevation first
            var scResult = await TryStopViaScCommand(false);
            if (scResult)
            {
                return true;
            }

            // Method 3: Try sc.exe with elevation
            var scElevatedResult = await TryStopViaScCommand(true);
            if (scElevatedResult)
            {
                return true;
            }

            // Method 4: Force stop using taskkill (last resort)
            return await TryForceStopService();
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private async Task<bool> TryStopViaServiceController()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            service.Refresh();
            
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                return true;
            }

            if (service.Status == ServiceControllerStatus.Running || 
                service.Status == ServiceControllerStatus.Paused)
            {
                service.Stop();
                
                // Wait with shorter timeout and periodic checks
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(1000);
                    service.Refresh();
                    if (service.Status == ServiceControllerStatus.Stopped)
                    {
                        return true;
                    }
                }
                
                return false;
            }

            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private async Task<bool> TryStopViaScCommand(bool withElevation)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"stop \"{ServiceName}\"",
                    UseShellExecute = withElevation,
                    RedirectStandardOutput = !withElevation,
                    RedirectStandardError = !withElevation,
                    CreateNoWindow = !withElevation,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            if (withElevation)
            {
                process.StartInfo.Verb = "runas";
            }

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                // Wait and verify service actually stopped
                await Task.Delay(2000);
                return !(await IsServiceRunningAsync());
            }
            else
            {
                if (!withElevation)
                {
                    // Could read process output here if needed for debugging
                }
                return false;
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private async Task<bool> TryForceStopService()
    {
        try
        {
            // Find processes named after the service executable
            var processes = System.Diagnostics.Process.GetProcessesByName("PrintHero.Service");
            if (processes.Length == 0)
            {
                return true; // Nothing to stop
            }

            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    await process.WaitForExitAsync();
                }
                catch (Exception ex)
                {
                    // Ignore individual process kill failures
                }
                finally
                {
                    process.Dispose();
                }
            }

            await Task.Delay(2000);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<bool> UninstallServiceAsync()
    {
        try
        {
            if (!await IsServiceInstalledAsync())
            {
                return true;
            }

            // Stop the service first
            await StopServiceAsync();

            var processInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"delete \"{ServiceName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Verb = "runas" // Run as administrator
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private string GetServiceExecutablePath()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        
        // First try: Same directory as the UI app (for installed apps)
        var servicePath = Path.Combine(appDirectory, ServiceExecutablePath);
        if (File.Exists(servicePath))
        {
            return Path.GetFullPath(servicePath);
        }
        
        // Second try: Development paths relative to current executing assembly
        var executingAssemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var executingDirectory = Path.GetDirectoryName(executingAssemblyPath) ?? appDirectory;
        
        var possiblePaths = new[]
        {
            // Try same folder as current assembly (most common case)
            Path.Combine(executingDirectory, ServiceExecutablePath),
            
            // Try service bin output (development scenarios)
            Path.Combine(executingDirectory, "..", "..", "..", "PrintHero.Service", "bin", "Release", "net8.0-windows", ServiceExecutablePath),
            Path.Combine(executingDirectory, "..", "..", "..", "PrintHero.Service", "bin", "Debug", "net8.0-windows", ServiceExecutablePath),
            
            // Try current working directory
            Path.Combine(Directory.GetCurrentDirectory(), ServiceExecutablePath),
            
            // Try application directory variations
            Path.Combine(appDirectory, "PrintHero.Service.exe"),
            Path.Combine(appDirectory, "bin", ServiceExecutablePath),
            Path.Combine(appDirectory, "service", ServiceExecutablePath)
        };
        
        foreach (var path in possiblePaths)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Invalid path, skip
                continue;
            }
        }
        
        // If nothing found, return the expected path in same directory as UI
        return Path.GetFullPath(servicePath);
    }

    private bool IsValidServiceExecutable(string exePath)
    {
        try
        {
            if (!File.Exists(exePath))
                return false;
                
            // Check if the file is a valid .NET executable
            var fileInfo = new FileInfo(exePath);
            if (fileInfo.Length == 0)
                return false;
                
            // Try to get the file version (this will fail if file is corrupted)
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            return !string.IsNullOrEmpty(versionInfo.FileVersion);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ReinstallServiceAsync()
    {
        try
        {
            // First uninstall the existing service
            if (await IsServiceInstalledAsync())
            {
                await UninstallServiceAsync();
                await Task.Delay(2000); // Wait for uninstall to complete
            }
            
            // Then install fresh
            return await InstallServiceAsync();
        }
        catch
        {
            return false;
        }
    }

    private bool IsRunningFromDevelopment()
    {
        try
        {
            var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var appDirectory = Path.GetDirectoryName(appPath) ?? string.Empty;
            
            // Development indicators
            return appDirectory.Contains("bin\\Debug") || 
                   appDirectory.Contains("bin\\Release") || 
                   appDirectory.Contains("\\obj\\") ||
                   appDirectory.Contains("Projects") ||
                   appDirectory.Contains("Source") ||
                   appDirectory.Contains("src");
        }
        catch
        {
            return false; // If we can't determine, assume installed
        }
    }
}