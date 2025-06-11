@echo off
REM PrintHero Installer Build Script
REM This script builds the PrintHero application and creates the installer

echo Building PrintHero Installer...
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

REM Try different MSBuild paths
set MSBUILD_PATH=""
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
) else (
    echo MSBuild not found. Please ensure Visual Studio or Build Tools are installed.
    pause
    exit /b 1
)

echo Using MSBuild from: %MSBUILD_PATH%
%MSBUILD_PATH% PrintHero.Installer.wixproj /p:Configuration=Release /p:Platform=x86
if %ERRORLEVEL% neq 0 (
    echo Failed to build installer
    pause
    exit /b 1
)
cd ..

echo.
echo Build completed successfully!
echo MSI Installer created at: PrintHero.Installer\bin\Release\PrintHeroSetup.msi
echo.
pause