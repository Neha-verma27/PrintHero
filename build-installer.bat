@echo off
echo Building PrintHero Installer...
echo.

REM Build applications
echo Step 1: Building applications...
dotnet build PrintHero.UI\PrintHero.UI.csproj --configuration Release --nologo
if %ERRORLEVEL% neq 0 (
    echo ERROR: Main application build failed
    echo Press any key to continue...
    pause >nul
    exit /b 1
)
echo ✓ Main application built successfully

echo Building uninstaller...
dotnet build PrintHero.Uninstaller\PrintHero.Uninstaller.csproj --configuration Release --nologo
if %ERRORLEVEL% neq 0 (
    echo ERROR: Uninstaller build failed
    echo Press any key to continue...
    pause >nul
    exit /b 1
)
echo ✓ Uninstaller built successfully

REM Check if WiX is installed
echo Step 2: Checking for WiX toolset...
where candle >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo ✓ WiX toolset found, using direct WiX build
    goto :wix_build
) else (
    echo WiX CLI tools not found, trying MSBuild...
    goto :msbuild_search
)

:msbuild_search
REM Find MSBuild
echo Step 3: Looking for MSBuild...
set MSBUILD_FOUND=0

if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    set MSBUILD_FOUND=1
    echo ✓ Found VS 2022 Community MSBuild
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    set MSBUILD_FOUND=1
    echo ✓ Found VS 2022 Professional MSBuild
)

if %MSBUILD_FOUND% equ 0 (
    echo ERROR: Neither WiX nor MSBuild found
    echo Please install either:
    echo 1. WiX Toolset v3.11+ OR
    echo 2. Visual Studio 2022
    echo Press any key to continue...
    pause >nul
    exit /b 1
)

echo Step 4: Building installer with MSBuild...
echo Using: %MSBUILD%
"%MSBUILD%" PrintHero.Installer\PrintHero.Installer.wixproj /p:Configuration=Release /p:Platform=x64 /verbosity:normal
if %ERRORLEVEL% neq 0 (
    echo ERROR: MSBuild failed
    echo Press any key to continue...
    pause >nul
    exit /b 1
)
goto :success

:wix_build
echo Step 4: Building installer with WiX tools...
cd PrintHero.Installer
candle Product.wxs -out obj\Product.wixobj
if %ERRORLEVEL% neq 0 (
    echo ERROR: candle.exe failed
    cd ..
    echo Press any key to continue...
    pause >nul
    exit /b 1
)

light obj\Product.wixobj -out bin\PrintHero-Setup.msi -ext WixUIExtension
if %ERRORLEVEL% neq 0 (
    echo ERROR: light.exe failed
    cd ..
    echo Press any key to continue...
    pause >nul
    exit /b 1
)
cd ..

:success
echo.
echo =====================================================
echo ✓ Build completed successfully!
echo =====================================================

REM Determine which installer file was created
set "INSTALLER_SOURCE="
set "INSTALLER_NAME=PrintHeroSetup.msi"
if exist "PrintHero.Installer\bin\Release\PrintHeroSetup_Fixed_Release_x64.msi" (
    set "INSTALLER_SOURCE=PrintHero.Installer\bin\Release\PrintHeroSetup_Fixed_Release_x64.msi"
    echo Installer found: PrintHero.Installer\bin\Release\PrintHeroSetup_Fixed_Release_x64.msi
) else if exist "PrintHero.Installer\bin\Release\PrintHeroSetup_Release_x64.msi" (
    set "INSTALLER_SOURCE=PrintHero.Installer\bin\Release\PrintHeroSetup_Release_x64.msi"
    echo Installer found: PrintHero.Installer\bin\Release\PrintHeroSetup_Release_x64.msi
) else if exist "PrintHero.Installer\bin\Release\PrintHeroSetup.msi" (
    set "INSTALLER_SOURCE=PrintHero.Installer\bin\Release\PrintHeroSetup.msi"
    echo Installer found: PrintHero.Installer\bin\Release\PrintHeroSetup.msi
) else if exist "PrintHero.Installer\bin\PrintHero-Setup.msi" (
    set "INSTALLER_SOURCE=PrintHero.Installer\bin\PrintHero-Setup.msi"
    set "INSTALLER_NAME=PrintHero-Setup.msi"
    echo Installer found: PrintHero.Installer\bin\PrintHero-Setup.msi
) else (
    echo WARNING: Installer file not found in expected location
    echo Checking for any MSI files...
    dir PrintHero.Installer\bin\Release\*.msi
    goto :end
)

REM Update Client Package
echo.
echo Step 5: Updating Client Package...
if not exist "PrintHero-Client-Package" (
    echo Creating PrintHero-Client-Package directory...
    mkdir "PrintHero-Client-Package"
)

REM Copy the installer to client package
echo Copying %INSTALLER_NAME% to client package...
copy "%INSTALLER_SOURCE%" "PrintHero-Client-Package\PrintHeroSetup.msi" >nul
if %ERRORLEVEL% equ 0 (
    echo ✓ Installer copied to client package
) else (
    echo ERROR: Failed to copy installer to client package
)

REM Update README.txt with current date and build info
echo Updating README.txt...
(
echo PrintHero - Automatic PDF Printer
echo ==================================
echo.
echo This package contains:
echo - PrintHeroSetup.msi ^(Installer^)
echo - INSTALLATION.txt ^(Setup instructions^)
echo.
echo Package built on: %DATE% at %TIME%
echo.
echo To install PrintHero, please read INSTALLATION.txt first.
echo.
) > "PrintHero-Client-Package\README.txt"
echo ✓ README.txt updated

REM Get file size for installer
for %%A in ("%INSTALLER_SOURCE%") do set "INSTALLER_SIZE=%%~zA"
set /a "INSTALLER_SIZE_MB=%INSTALLER_SIZE% / 1048576"

REM Update INSTALLATION.txt with current info
echo Updating INSTALLATION.txt...
(
echo PrintHero v1.0 - Installation Guide
echo ======================================
echo.
echo Package Information:
echo - Build Date: %DATE% %TIME%
echo - Installer Size: %INSTALLER_SIZE_MB% MB
echo - File: PrintHeroSetup.msi
echo.
echo System Requirements:
echo - Windows 10/11 ^(64-bit^)
echo - .NET 8.0 Runtime ^(included in installer^)
echo - Administrator privileges for installation
echo.
echo Installation Steps:
echo 1. Right-click PrintHeroSetup.msi and select "Run as Administrator"
echo 2. Follow the installation wizard
echo 3. Launch PrintHero from Desktop shortcut or Start Menu
echo 4. Configure your PDF monitoring folders in Settings
echo.
echo Features:
echo - Automatic PDF printing from monitored folders
echo - Desktop and Start Menu shortcuts
echo - Auto-start with Windows
echo - Complete dependency package ^(no additional downloads needed^)
echo.
echo Troubleshooting:
echo - If Windows shows "Unrecognized app" warning, click "More info" then "Run anyway"
echo - Ensure antivirus software is not blocking the installation
echo - Contact support if you encounter issues
echo.
) > "PrintHero-Client-Package\INSTALLATION.txt"
echo ✓ INSTALLATION.txt updated

echo.
echo Step 6: Creating Uninstaller Package...
if not exist "PrintHero-Client-Package\Uninstaller" (
    echo Creating Uninstaller directory...
    mkdir "PrintHero-Client-Package\Uninstaller"
)

REM Copy uninstaller executable and dependencies
echo Copying uninstaller files...
copy "PrintHero.Uninstaller\bin\Release\net8.0-windows\PrintHero.Uninstaller.exe" "PrintHero-Client-Package\Uninstaller\" >nul
if %ERRORLEVEL% neq 0 (
    echo ERROR: Failed to copy uninstaller executable
) else (
    echo ✓ Uninstaller executable copied
)

REM Copy uninstaller dependencies
for %%f in (PrintHero.Uninstaller\bin\Release\net8.0-windows\*.dll) do (
    copy "%%f" "PrintHero-Client-Package\Uninstaller\" >nul
)
echo ✓ Uninstaller dependencies copied

REM Create uninstaller README
echo Creating uninstaller documentation...
(
echo PrintHero Uninstaller
echo ====================
echo.
echo This folder contains the PrintHero uninstaller utility.
echo.
echo To completely remove PrintHero from your system:
echo 1. Close PrintHero if it's running
echo 2. Right-click PrintHero.Uninstaller.exe and select "Run as Administrator"
echo 3. Follow the uninstaller prompts
echo.
echo The uninstaller will:
echo - Stop and remove the PrintHero service
echo - Remove all installed files
echo - Remove registry entries
echo - Remove desktop and start menu shortcuts
echo - Remove auto-startup entries
echo.
echo Note: User configuration files may be preserved for future installations.
echo.
echo Built on: %DATE% at %TIME%
echo.
) > "PrintHero-Client-Package\Uninstaller\README.txt"
echo ✓ Uninstaller README created

echo.
echo =====================================================
echo ✓ Complete Package Created Successfully!
echo =====================================================
echo Package Location: PrintHero-Client-Package\
echo Main Installer: PrintHero-Client-Package\PrintHeroSetup.msi
echo Uninstaller: PrintHero-Client-Package\Uninstaller\PrintHero.Uninstaller.exe
echo.

:end
echo Press any key to continue...
pause >nul