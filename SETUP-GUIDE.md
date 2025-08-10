# PrintHero Setup Guide

## 📋 Table of Contents
1. [Prerequisites](#prerequisites)
2. [Development Environment Setup](#development-environment-setup)
3. [WiX Toolset Installation](#wix-toolset-installation)
4. [Building the Application](#building-the-application)
5. [Creating the Installer](#creating-the-installer)
6. [Deployment](#deployment)
7. [Troubleshooting](#troubleshooting)

---

## 🔧 Prerequisites

### System Requirements
- **Operating System**: Windows 10/11 (64-bit)
- **RAM**: Minimum 4GB, Recommended 8GB+
- **Storage**: 2GB free space for development tools
- **Network**: Internet connection for package downloads

### Required Software
1. **Visual Studio 2022** (Community/Professional/Enterprise)
   - Workloads: `.NET Desktop Development`
   - Individual Components: `.NET 8.0 Runtime`

2. **WiX Toolset v4.x** (for creating installers)
3. **Git** (for source control)

---

## 🚀 Development Environment Setup

### Step 1: Install Visual Studio 2022
1. Download from: https://visualstudio.microsoft.com/downloads/
2. During installation, select:
   - ✅ **.NET desktop development** workload
   - ✅ **Windows 11 SDK** (latest version)
   - ✅ **.NET 8.0 Runtime**

### Step 2: Clone the Repository
```bash
git clone [repository-url]
cd PrintHero
```

### Step 3: Restore NuGet Packages
```bash
dotnet restore PrintHero.sln
```

---

## 🛠️ WiX Toolset Installation

### Option 1: Install WiX Extension for Visual Studio (Recommended)
1. Open Visual Studio 2022
2. Go to **Extensions** → **Manage Extensions**
3. Search for "**WiX Toolset Visual Studio 2022 Extension**"
4. Install and restart Visual Studio

### Option 2: Install WiX Toolset Manually
1. Download WiX v4.x from: https://wixtoolset.org/
2. Install the MSI package
3. Add WiX to your PATH environment variable:
   ```
   C:\Program Files (x86)\WiX Toolset v4.0\bin
   ```

### Verify WiX Installation
Open Command Prompt and run:
```bash
candle.exe -?
```
If successful, you'll see WiX Compiler help text.

---

## 🔨 Building the Application

### Debug Build
```bash
# Build entire solution
dotnet build PrintHero.sln --configuration Debug

# Or build specific project
dotnet build PrintHero.UI/PrintHero.UI.csproj --configuration Debug
```

### Release Build
```bash
# Build for production
dotnet build PrintHero.sln --configuration Release

# Publish self-contained
dotnet publish PrintHero.UI/PrintHero.UI.csproj --configuration Release --output ./publish
```

### Project Structure
```
PrintHero/
├── PrintHero.Core/          # Core business logic
├── PrintHero.UI/            # WPF User Interface
├── PrintHero.Service/       # Background service (if needed)
├── PrintHero.Installer/     # WiX installer project
├── PrintHero.Uninstaller/   # Custom uninstaller
└── PrintHero-Client-Package/ # Distribution files
```

---

## 📦 Creating the Installer

### Prerequisites for Installer Build
1. **Release Build**: Ensure Release build is completed first
2. **WiX Toolset**: Must be installed and accessible

### Build Installer using Visual Studio
1. Open `PrintHero.sln` in Visual Studio
2. Set configuration to **Release**
3. Right-click `PrintHero.Installer` project
4. Select **Build**

### Build Installer using Command Line
```bash
# Navigate to installer directory
cd PrintHero.Installer

# Build using MSBuild
msbuild PrintHero.Installer.wixproj /p:Configuration=Release /p:Platform=x64
```

### Build Installer using Batch File
```bash
# Use the provided batch file
build-installer.bat
```

### Expected Output
- **Location**: `PrintHero.Installer/bin/Release/`
- **File**: `PrintHero.msi` (Windows Installer package)
- **Size**: ~50-100MB (includes all dependencies)

---

## 🚀 Deployment

### Installer Features
The PrintHero installer includes:
- ✅ **Main Application**: PrintHero.UI.exe
- ✅ **All Dependencies**: .NET 8.0 runtime libraries
- ✅ **PDF Processing**: PdfSharp and PdfiumViewer libraries
- ✅ **Database**: SQLite support
- ✅ **Logging**: Serilog integration
- ✅ **Start Menu**: Shortcuts and uninstaller
- ✅ **Desktop Shortcut**: Optional desktop icon
- ✅ **Auto-Start**: Windows startup integration

### Installation Options
1. **Silent Install**: `msiexec /i PrintHero.msi /quiet`
2. **Interactive Install**: Double-click `PrintHero.msi`
3. **Custom Install**: `msiexec /i PrintHero.msi INSTALLLEVEL=3`

### Default Installation Paths
- **Program Files**: `C:\Program Files\PrintHero\`
- **User Data**: `%APPDATA%\PrintHero_Release\`
- **Logs**: `%APPDATA%\PrintHero_Release\logs\`

---

## 🔍 Troubleshooting

### Common Build Issues

#### Issue: "WiX toolset not found"
**Solution**:
```bash
# Add WiX to PATH
set PATH=%PATH%;C:\Program Files (x86)\WiX Toolset v4.0\bin
```

#### Issue: "ICE80 warnings in installer"
**Solution**: These are architecture warnings and can be ignored. The installer will work correctly.

#### Issue: "Missing dependencies in Release build"
**Solution**:
```bash
# Ensure all packages are restored
dotnet restore
dotnet clean
dotnet build --configuration Release
```

#### Issue: "PDF processing not working"
**Solution**: Verify these files are included in the installer:
- `pdfium.dll` (in x64 folder)
- `PdfSharp.dll`
- `PdfiumViewer.dll`

### Runtime Issues

#### Issue: "Application won't start"
**Solution**:
1. Check Windows Event Viewer for errors
2. Verify .NET 8.0 Desktop Runtime is installed
3. Run application from command line to see errors

#### Issue: "Printers not detected"
**Solution**:
1. Ensure application runs with appropriate permissions
2. Check that printer spooler service is running
3. Verify network printers are properly installed

#### Issue: "File monitoring not working"
**Solution**:
1. Check folder permissions (read/write access)
2. Verify folder paths exist
3. Ensure no antivirus blocking file operations

---

## 📝 Development Notes

### Key Technologies Used
- **.NET 8.0**: Application framework
- **WPF**: User interface
- **WiX Toolset v4**: Installer creation
- **SQLite**: Local database storage
- **Serilog**: Logging framework
- **PdfSharp**: PDF processing
- **System.Drawing**: Printing operations

### Architecture Overview
```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   PrintHero.UI  │───▶│  PrintHero.Core  │───▶│ File Monitoring │
│   (WPF App)     │    │ (Business Logic) │    │   & Printing    │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                        │                       │
         ▼                        ▼                       ▼
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│  Settings UI    │    │   JSON Config    │    │  Windows APIs   │
│   Management    │    │    Storage       │    │   (Printing)    │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### Build Configuration
- **Debug**: Full logging, development features enabled
- **Release**: Optimized performance, minimal logging

---

## 📞 Support

### Getting Help
1. **Documentation**: Check this guide first
2. **Logs**: Review application logs in `%APPDATA%\PrintHero_Release\logs\`
3. **Issues**: Create GitHub issues for bugs/feature requests

### Log Locations
- **Application Logs**: `%APPDATA%\PrintHero_Release\logs\`
- **Windows Event Log**: Windows Logs → Application
- **Installer Logs**: `%TEMP%\PrintHero_Install.log`

---

## ✅ Quick Start Checklist

- [ ] Visual Studio 2022 installed with .NET 8.0
- [ ] WiX Toolset v4.x installed
- [ ] Repository cloned and packages restored
- [ ] Debug build successful
- [ ] Release build successful
- [ ] Installer build successful
- [ ] Application tested on clean system
- [ ] Installer tested on target machines

---

*This guide covers PrintHero v1.0 setup and deployment. For updates and additional information, refer to the project documentation.*