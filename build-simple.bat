@echo off
REM Simple PrintHero Build Script
REM This script builds just the .NET applications

echo Building PrintHero Applications...
echo.

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

echo.
echo Build completed successfully!
echo.
echo To manually install PrintHero:
echo 1. Copy files from PrintHero.UI\bin\Release\net8.0-windows\ to your installation directory
echo 2. Copy files from PrintHero.Service\bin\Release\net8.0-windows\ to the same directory
echo 3. Run PrintHero.UI.exe
echo.
echo For installer creation, you need:
echo - Visual Studio with WiX Toolset extension, OR
echo - WiX Toolset v3.11+ with MSBuild
echo.
pause