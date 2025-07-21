@echo off
echo Building PrintHero Installer...
echo.

REM Build applications
echo Step 1: Building applications...
dotnet build PrintHero.UI\PrintHero.UI.csproj --configuration Release --nologo
if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed
    echo Press any key to continue...
    pause >nul
    exit /b 1
)
echo ✓ Applications built successfully

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

if exist "PrintHero.Installer\bin\Release\PrintHeroSetup.msi" (
    echo Installer: PrintHero.Installer\bin\Release\PrintHeroSetup.msi
) else if exist "PrintHero.Installer\bin\PrintHero-Setup.msi" (
    echo Installer: PrintHero.Installer\bin\PrintHero-Setup.msi
) else (
    echo WARNING: Installer file not found in expected location
)

echo Press any key to continue...
pause >nul