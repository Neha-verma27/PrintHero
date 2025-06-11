@echo off
REM PrintHero MSI-Only Build Script
REM This script builds just the MSI installer (easier to build)

echo Building PrintHero MSI Installer...
echo.

REM Build all projects in Release configuration
echo Step 1: Building PrintHero Core...
dotnet build PrintHero.Core\PrintHero.Core.csproj --configuration Release --nologo
if %ERRORLEVEL% neq 0 (
    echo Failed to build PrintHero.Core
    pause
    exit /b 1
)

echo Step 2: Building PrintHero UI...
dotnet build PrintHero.UI\PrintHero.UI.csproj --configuration Release --nologo
if %ERRORLEVEL% neq 0 (
    echo Failed to build PrintHero.UI
    pause
    exit /b 1
)

echo Step 3: Building PrintHero Service...
dotnet build PrintHero.Service\PrintHero.Service.csproj --configuration Release --nologo
if %ERRORLEVEL% neq 0 (
    echo Failed to build PrintHero.Service
    pause
    exit /b 1
)

echo Step 4: Building MSI Installer...
cd PrintHero.MSI

REM Try to find MSBuild
set MSBUILD_PATH=""
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
) else (
    echo MSBuild not found in standard Visual Studio 2022 locations.
    echo Please ensure Visual Studio 2022 with WiX Toolset is installed.
    echo.
    echo Alternative: Install WiX Toolset v3.11+ and add MSBuild to PATH
    pause
    exit /b 1
)

echo Using MSBuild from: %MSBUILD_PATH%
%MSBUILD_PATH% PrintHero.MSI.wixproj /p:Configuration=Release /p:Platform=x86
if %ERRORLEVEL% neq 0 (
    echo Failed to build MSI installer
    pause
    exit /b 1
)
cd ..

echo.
echo Build completed successfully!
echo MSI Installer created at: PrintHero.MSI\bin\Release\PrintHero.msi
echo.
echo To install PrintHero:
echo   Double-click: PrintHero.MSI\bin\Release\PrintHero.msi
echo   OR use command: msiexec /i "PrintHero.MSI\bin\Release\PrintHero.msi"
echo.
pause