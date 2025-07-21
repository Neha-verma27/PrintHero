using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Management;

namespace PrintHero.Uninstaller
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check if running as administrator
            if (!IsRunningAsAdministrator())
            {
                var result = MessageBox.Show(
                    "PrintHero Uninstaller requires administrator privileges to remove all components.\n\n" +
                    "Would you like to restart as administrator?",
                    "Administrator Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    RestartAsAdministrator();
                }
                return;
            }

            // Show uninstall confirmation
            var confirmResult = MessageBox.Show(
                "This will completely remove PrintHero and all its components from your system.\n\n" +
                "• Application files\n" +
                "• Registry entries\n" +
                "• Auto-start settings\n" +
                "• Desktop and Start Menu shortcuts\n\n" +
                "User documents in the Printing folder will be preserved.\n\n" +
                "Do you want to continue?",
                "Uninstall PrintHero",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }

            // Perform uninstallation
            var uninstaller = new PrintHeroUninstaller();
            bool success = uninstaller.Uninstall();

            if (success)
            {
                MessageBox.Show(
                    "PrintHero has been successfully uninstalled from your system.\n\n" +
                    "Thank you for using PrintHero!",
                    "Uninstall Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    "Some components could not be removed. Please check the log for details.\n\n" +
                    "You may need to manually remove remaining files.",
                    "Uninstall Incomplete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static bool IsRunningAsAdministrator()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private static void RestartAsAdministrator()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart as administrator: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class PrintHeroUninstaller
    {
        private readonly List<string> _logMessages = new List<string>();

        public bool Uninstall()
        {
            bool success = true;
            Log("Starting PrintHero uninstallation...");

            try
            {
                // Stop PrintHero processes
                success &= StopPrintHeroProcesses();

                // Remove auto-start entries
                success &= RemoveAutoStartEntries();

                // Remove registry entries
                success &= RemoveRegistryEntries();

                // Remove shortcuts
                success &= RemoveShortcuts();

                // Remove application files
                success &= RemoveApplicationFiles();

                // Try to uninstall via MSI if available
                success &= UninstallViaMSI();

                Log("PrintHero uninstallation completed.");
            }
            catch (Exception ex)
            {
                Log($"Error during uninstallation: {ex.Message}");
                success = false;
            }

            // Save log file
            SaveLogFile();

            return success;
        }

        private bool StopPrintHeroProcesses()
        {
            Log("Stopping PrintHero processes...");
            bool success = true;

            string[] processNames = { "PrintHero.UI", "PrintHero.Service", "PrintHero" };

            foreach (string processName in processNames)
            {
                try
                {
                    var processes = Process.GetProcessesByName(processName);
                    foreach (var process in processes)
                    {
                        Log($"Stopping process: {process.ProcessName} (PID: {process.Id})");
                        process.Kill();
                        process.WaitForExit(5000); // Wait up to 5 seconds
                        Log($"Process {process.ProcessName} stopped successfully");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error stopping {processName}: {ex.Message}");
                    success = false;
                }
            }

            return success;
        }

        private bool RemoveAutoStartEntries()
        {
            Log("Removing auto-start entries...");
            bool success = true;

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("PrintHero");
                        if (value != null)
                        {
                            key.DeleteValue("PrintHero");
                            Log("Removed PrintHero from Windows startup");
                        }
                        else
                        {
                            Log("PrintHero startup entry not found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error removing auto-start entries: {ex.Message}");
                success = false;
            }

            return success;
        }

        private bool RemoveRegistryEntries()
        {
            Log("Removing registry entries...");
            bool success = true;

            // Registry paths to clean up
            var registryCleanup = new Dictionary<RegistryKey, string[]>
            {
                {
                    Registry.CurrentUser,
                    new[] {
                        @"SOFTWARE\PrintHero",
                        @"SOFTWARE\Classes\PrintHero.PDF",
                        @"SOFTWARE\Classes\.pdf\OpenWithProgids\PrintHero.PDF",
                        @"SOFTWARE\RegisteredApplications\PrintHero"
                    }
                },
                {
                    Registry.LocalMachine,
                    new[] {
                        @"SOFTWARE\PrintHero",
                        @"SOFTWARE\Classes\PrintHero.PDF",
                        @"SOFTWARE\Classes\.pdf\OpenWithProgids\PrintHero.PDF",
                        @"SOFTWARE\WOW6432Node\PrintHero",
                        @"SOFTWARE\RegisteredApplications\PrintHero"
                    }
                }
            };

            foreach (var rootKey in registryCleanup)
            {
                foreach (string path in rootKey.Value)
                {
                    try
                    {
                        rootKey.Key.DeleteSubKeyTree(path, false);
                        Log($"Removed registry key: {rootKey.Key.Name}\\{path}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Registry key not found or error removing {rootKey.Key.Name}\\{path}: {ex.Message}");
                        // Don't mark as failure if key doesn't exist
                    }
                }
            }

            // Clean up Windows Installer cache entries
            success &= CleanWindowsInstallerCache();

            // Clean up program files associations
            success &= CleanFileAssociations();

            return success;
        }

        private bool CleanWindowsInstallerCache()
        {
            Log("Cleaning Windows Installer cache...");
            bool success = true;

            try
            {
                // Clean MSI cache folder
                string msiCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Installer");
                if (Directory.Exists(msiCachePath))
                {
                    var msiFiles = Directory.GetFiles(msiCachePath, "*.msi", SearchOption.TopDirectoryOnly);
                    foreach (string msiFile in msiFiles)
                    {
                        try
                        {
                            // Check if this MSI is related to PrintHero
                            var fileInfo = new FileInfo(msiFile);
                            if (IsPrintHeroMSI(msiFile))
                            {
                                File.Delete(msiFile);
                                Log($"Removed MSI cache file: {msiFile}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"Could not remove MSI cache file {msiFile}: {ex.Message}");
                        }
                    }
                }

                // Clean installer registry entries
                string[] installerKeys = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData",
                    @"SOFTWARE\Classes\Installer\Products"
                };

                foreach (string keyPath in installerKeys)
                {
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(keyPath, true))
                        {
                            if (key != null)
                            {
                                foreach (string subKeyName in key.GetSubKeyNames())
                                {
                                    try
                                    {
                                        using (var subKey = key.OpenSubKey(subKeyName))
                                        {
                                            if (subKey != null && IsPrintHeroInstallerKey(subKey))
                                            {
                                                key.DeleteSubKeyTree(subKeyName);
                                                Log($"Removed installer registry key: {keyPath}\\{subKeyName}");
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log($"Error cleaning installer key {subKeyName}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error accessing installer registry {keyPath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error cleaning Windows Installer cache: {ex.Message}");
                success = false;
            }

            return success;
        }

        private bool IsPrintHeroMSI(string msiPath)
        {
            try
            {
                // Basic check by filename or could use Windows Installer API
                var fileName = Path.GetFileName(msiPath).ToLowerInvariant();
                return fileName.Contains("printhero");
            }
            catch
            {
                return false;
            }
        }

        private bool IsPrintHeroInstallerKey(RegistryKey key)
        {
            try
            {
                var productName = key.GetValue("ProductName")?.ToString();
                var manufacturer = key.GetValue("Manufacturer")?.ToString();
                
                return (productName != null && productName.Contains("PrintHero", StringComparison.OrdinalIgnoreCase)) ||
                       (manufacturer != null && manufacturer.Contains("PrintHero", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private bool CleanFileAssociations()
        {
            Log("Cleaning file associations...");
            bool success = true;

            try
            {
                // Remove PDF file association
                string[] associationKeys = {
                    @"SOFTWARE\Classes\.pdf\OpenWithProgids",
                    @"SOFTWARE\Classes\Applications\PrintHero.UI.exe"
                };

                foreach (string keyPath in associationKeys)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                        {
                            if (key != null)
                            {
                                var valueNames = key.GetValueNames();
                                foreach (string valueName in valueNames)
                                {
                                    if (valueName.Contains("PrintHero", StringComparison.OrdinalIgnoreCase))
                                    {
                                        key.DeleteValue(valueName);
                                        Log($"Removed file association value: {keyPath}\\{valueName}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error cleaning file association {keyPath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error cleaning file associations: {ex.Message}");
                success = false;
            }

            return success;
        }

        private bool RemoveShortcuts()
        {
            Log("Removing shortcuts...");
            bool success = true;

            // Desktop shortcuts - check both current user and public desktop
            string[] desktopPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            string[] shortcutNames = { "PrintHero.lnk", "PrintHero.url" };

            foreach (string desktopPath in desktopPaths)
            {
                foreach (string shortcutName in shortcutNames)
                {
                    string shortcutPath = Path.Combine(desktopPath, shortcutName);
                    if (File.Exists(shortcutPath))
                    {
                        try
                        {
                            File.Delete(shortcutPath);
                            Log($"Removed desktop shortcut: {shortcutPath}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error removing desktop shortcut {shortcutPath}: {ex.Message}");
                            success = false;
                        }
                    }
                }
            }

            // Start Menu shortcuts - check both user and common start menu
            string[] startMenuBasePaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            };

            foreach (string basePath in startMenuBasePaths)
            {
                string startMenuPath = Path.Combine(basePath, "Programs", "PrintHero");
                
                if (Directory.Exists(startMenuPath))
                {
                    try
                    {
                        Directory.Delete(startMenuPath, true);
                        Log($"Removed Start Menu folder: {startMenuPath}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Error removing Start Menu folder {startMenuPath}: {ex.Message}");
                        success = false;
                    }
                }

                // Also check for individual shortcuts in Programs folder
                string programsPath = Path.Combine(basePath, "Programs");
                foreach (string shortcutName in new[] { "PrintHero.lnk", "PrintHero.url" })
                {
                    string shortcutPath = Path.Combine(programsPath, shortcutName);
                    if (File.Exists(shortcutPath))
                    {
                        try
                        {
                            File.Delete(shortcutPath);
                            Log($"Removed Start Menu shortcut: {shortcutPath}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error removing Start Menu shortcut {shortcutPath}: {ex.Message}");
                            success = false;
                        }
                    }
                }
            }

            // Search for any remaining PrintHero shortcuts
            success &= SearchAndRemoveShortcuts();

            return success;
        }

        private bool SearchAndRemoveShortcuts()
        {
            Log("Searching for any remaining PrintHero shortcuts...");
            bool success = true;

            string[] searchPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                Environment.GetFolderPath(Environment.SpecialFolder.Recent)
            };

            foreach (string searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath)) continue;

                try
                {
                    // Search for .lnk files that contain "PrintHero"
                    var shortcuts = Directory.GetFiles(searchPath, "*.lnk", SearchOption.AllDirectories)
                        .Where(file => Path.GetFileNameWithoutExtension(file).Contains("PrintHero", StringComparison.OrdinalIgnoreCase));

                    foreach (string shortcut in shortcuts)
                    {
                        try
                        {
                            File.Delete(shortcut);
                            Log($"Removed found shortcut: {shortcut}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error removing found shortcut {shortcut}: {ex.Message}");
                            success = false;
                        }
                    }

                    // Search for .url files that contain "PrintHero"
                    var urlShortcuts = Directory.GetFiles(searchPath, "*.url", SearchOption.AllDirectories)
                        .Where(file => Path.GetFileNameWithoutExtension(file).Contains("PrintHero", StringComparison.OrdinalIgnoreCase));

                    foreach (string urlShortcut in urlShortcuts)
                    {
                        try
                        {
                            File.Delete(urlShortcut);
                            Log($"Removed found URL shortcut: {urlShortcut}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error removing found URL shortcut {urlShortcut}: {ex.Message}");
                            success = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error searching for shortcuts in {searchPath}: {ex.Message}");
                    success = false;
                }
            }

            return success;
        }

        private bool RemoveApplicationFiles()
        {
            Log("Removing application files...");
            bool success = true;

            // Common installation paths
            string[] installPaths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PrintHero"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PrintHero"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrintHero")
            };

            foreach (string installPath in installPaths)
            {
                if (Directory.Exists(installPath))
                {
                    try
                    {
                        Directory.Delete(installPath, true);
                        Log($"Removed installation folder: {installPath}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Error removing installation folder {installPath}: {ex.Message}");
                        success = false;
                    }
                }
            }

            return success;
        }

        private bool UninstallViaMSI()
        {
            Log("Attempting to uninstall via Windows Installer...");
            bool success = true;

            try
            {
                // Search for PrintHero in both HKLM and HKCU uninstall keys
                string[] uninstallKeys = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (string uninstallKeyPath in uninstallKeys)
                {
                    // Check HKEY_LOCAL_MACHINE
                    success &= ProcessUninstallRegistry(Registry.LocalMachine, uninstallKeyPath);
                    
                    // Check HKEY_CURRENT_USER  
                    success &= ProcessUninstallRegistry(Registry.CurrentUser, uninstallKeyPath);
                }
            }
            catch (Exception ex)
            {
                Log($"Error during MSI uninstall: {ex.Message}");
                success = false;
            }

            return success;
        }

        private bool ProcessUninstallRegistry(RegistryKey rootKey, string uninstallKeyPath)
        {
            bool success = true;
            
            try
            {
                using (var key = rootKey.OpenSubKey(uninstallKeyPath, true))
                {
                    if (key != null)
                    {
                        var subKeyNames = key.GetSubKeyNames().ToList();
                        
                        foreach (string subKeyName in subKeyNames)
                        {
                            try
                            {
                                using (var subKey = key.OpenSubKey(subKeyName, true))
                                {
                                    if (subKey != null)
                                    {
                                        var displayName = subKey.GetValue("DisplayName")?.ToString();
                                        var publisher = subKey.GetValue("Publisher")?.ToString();
                                        
                                        if ((displayName != null && displayName.Contains("PrintHero", StringComparison.OrdinalIgnoreCase)) ||
                                            (publisher != null && publisher.Contains("PrintHero", StringComparison.OrdinalIgnoreCase)))
                                        {
                                            Log($"Found PrintHero entry: {displayName} (Key: {subKeyName})");
                                            
                                            var uninstallString = subKey.GetValue("UninstallString")?.ToString();
                                            if (!string.IsNullOrEmpty(uninstallString))
                                            {
                                                Log($"Found MSI uninstaller: {uninstallString}");
                                                
                                                // Try to run MSI uninstaller first
                                                if (uninstallString.Contains("msiexec"))
                                                {
                                                    var process = Process.Start(new ProcessStartInfo
                                                    {
                                                        FileName = "msiexec.exe",
                                                        Arguments = $"/x {subKeyName} /quiet /norestart",
                                                        UseShellExecute = false,
                                                        CreateNoWindow = true
                                                    });

                                                    if (process != null)
                                                    {
                                                        process.WaitForExit(30000);
                                                        Log($"MSI uninstaller completed with exit code: {process.ExitCode}");
                                                    }
                                                }
                                            }
                                            
                                            // Force remove the registry entry
                                            try
                                            {
                                                key.DeleteSubKeyTree(subKeyName);
                                                Log($"Forcibly removed registry entry: {rootKey.Name}\\{uninstallKeyPath}\\{subKeyName}");
                                            }
                                            catch (Exception ex)
                                            {
                                                Log($"Could not remove registry entry {subKeyName}: {ex.Message}");
                                                success = false;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"Error processing registry key {subKeyName}: {ex.Message}");
                                success = false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error accessing uninstall registry {rootKey.Name}\\{uninstallKeyPath}: {ex.Message}");
                success = false;
            }
            
            return success;
        }

        private void Log(string message)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            _logMessages.Add(logEntry);
            Console.WriteLine(logEntry);
        }

        private void SaveLogFile()
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "PrintHero_Uninstall_Log.txt");

                File.WriteAllLines(logPath, _logMessages);
                Log($"Uninstall log saved to: {logPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save log file: {ex.Message}");
            }
        }
    }
}