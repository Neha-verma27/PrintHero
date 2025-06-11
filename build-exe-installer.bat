@echo off
REM PrintHero EXE Installer Build Script
REM This script builds the PrintHero application and creates both MSI and EXE installers

echo Building PrintHero EXE Installer...
echo.

REM Build all projects in Release configuration
echo Step 1: Building PrintHero Core...
dotnet build PrintHero.Core\PrintHero.Core.csproj --configuration Release
if %ERRORLEVEL% neq 0 (
    echo Failed to build PrintHero.Core
    pause
    exit /b 1
)

echo Step 2: Building PrintHero UI...
dotnet build PrintHero.UI\PrintHero.UI.csproj --configuration Release
if %ERRORLEVEL% neq 0 (
    echo Failed to build PrintHero.UI
    pause
    exit /b 1
)

echo Step 3: Building PrintHero Service...
dotnet build PrintHero.Service\PrintHero.Service.csproj --configuration Release
if %ERRORLEVEL% neq 0 (
    echo Failed to build PrintHero.Service
    pause
    exit /b 1
)

echo Step 4: Building MSI Installer...
cd PrintHero.Installer
msbuild PrintHero.Installer.wixproj /p:Configuration=Release /p:Platform=x86
if %ERRORLEVEL% neq 0 (
    echo Failed to build MSI installer
    pause
    exit /b 1
)
cd ..

echo Step 5: Building EXE Bootstrapper...
cd PrintHero.Bootstrapper
msbuild PrintHero.Bootstrapper.wixproj /p:Configuration=Release /p:Platform=x86
if %ERRORLEVEL% neq 0 (
    echo Failed to build EXE installer
    pause
    exit /b 1
)
cd ..

echo.
echo Build completed successfully!
echo MSI Installer: PrintHero.Installer\bin\Release\PrintHeroInstaller.msi
echo EXE Installer: PrintHero.Bootstrapper\bin\Release\PrintHeroSetup.exe
echo.
echo To install PrintHero, run either:
echo   PrintHero.Bootstrapper\bin\Release\PrintHeroSetup.exe
echo   OR
echo   PrintHero.Installer\bin\Release\PrintHeroInstaller.msi
echo.
pause