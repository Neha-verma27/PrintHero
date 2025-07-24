# PrintHero Installer

This directory contains the WiX-based installer for the PrintHero application suite.

## Prerequisites

To build the installer, you need:

1. **Windows SDK/Build Tools** - For MSBuild
2. **WiX Toolset v3.11 or later** - Download from: https://wixtoolset.org/releases/
3. **.NET 8.0 SDK** - For building the application projects

## Building the Installer

### Method 1: Using the Build Script (Recommended)

Run the provided batch file from the root directory:

```cmd
build-installer.bat
```

This script will:
1. Build PrintHero.Core in Release configuration
2. Build PrintHero.UI in Release configuration  
3. Build PrintHero.Service in Release configuration
4. Build the WiX installer project
5. Generate the MSI installer file

### Method 2: Manual Build

1. First, build all projects in Release configuration:
   ```cmd
   dotnet build PrintHero.Core\PrintHero.Core.csproj --configuration Release
   dotnet build PrintHero.UI\PrintHero.UI.csproj --configuration Release
   dotnet build PrintHero.Service\PrintHero.Service.csproj --configuration Release
   ```

2. Then build the installer:
   ```cmd
   cd PrintHero.Installer
   msbuild PrintHero.Installer.wixproj /p:Configuration=Release
   ```

## Installer Output

The completed installer will be created at:
```
PrintHero.Installer\bin\Release\PrintHeroInstaller.msi
```

## What Gets Installed

The installer includes:

### Core Components
- **PrintHero.Core.dll** - Core business logic library
- **PrintHero.UI.exe** - Main WPF application
- **PrintHero.Service.exe** - Background service for file monitoring
- All required dependencies and runtime libraries

### Features
- **Start Menu Shortcuts** - Access PrintHero from Start Menu
- **Desktop Shortcut** (optional) - Quick access from desktop
- **PDF File Association** - Right-click context menu for PDF files
- **Registry Settings** - Application configuration storage
- **Uninstaller** - Clean removal via Windows Programs & Features

### Installation Location
- Default: `C:\Program Files\PrintHero\`
- User can choose custom location during installation

## Installer Features

- **Professional UI** - Uses WiX UI extension for modern installer experience
- **Upgrade Support** - Automatically handles upgrades from previous versions
- **Component-based Installation** - Efficient file management and updates
- **x86 Support** - Compatible with both 32-bit and 64-bit Windows systems
- **Clean Uninstall** - Removes all files, shortcuts, and registry entries

## Troubleshooting

### Build Errors

**"WiX Toolset not found"**
- Download and install WiX Toolset v3.11 or later
- Restart Visual Studio/Command Prompt after installation

**"Source file not found"**
- Ensure all projects are built in Release configuration first
- Check that all referenced DLL files exist in the bin\Release folders

**"Platform mismatch"**
- This installer is configured for x86. Ensure you're building with the correct platform
- If you need x64 support, modify the Platform attribute in Product.wxs and change ProgramFilesFolder to ProgramFiles64Folder

### Installation Issues

**"Installation failed"**
- Run installer as Administrator
- Check Windows Event Log for detailed error information
- Ensure target directory has write permissions

**"Application won't start after installation"**
- Verify .NET 8.0 Runtime is installed on target system
- Check that all dependency DLLs were properly installed

## Customization

To modify the installer:

1. **Change Installation Directory**: Edit the Directory structure in Product.wxs
2. **Add/Remove Files**: Modify the ComponentGroup sections
3. **Change Product Info**: Update Product attributes (Name, Version, Manufacturer)
4. **Modify UI**: Add custom dialogs or modify existing WiX UI

## Version Management

The installer version is set in Product.wxs:
```xml
<Product Id="*" Version="1.0.0.0" ...>
```

Remember to increment the version number for each release to ensure proper upgrade handling.