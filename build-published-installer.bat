@echo off
REM PrintHero Published Installer Build Script
REM This script publishes the PrintHero application and creates the installer

echo Building PrintHero Installer from Published Files...
echo.

REM Step 1: Clean previous publish output
echo Step 1: Cleaning previous publish output...
if exist "C:\Projects\PrintHero Publish" (
    rmdir /s /q "C:\Projects\PrintHero Publish"
)

REM Step 2: Publish PrintHero UI (this includes all dependencies)
echo Step 2: Publishing PrintHero UI Application...
dotnet publish PrintHero.UI\PrintHero.UI.csproj --configuration Release --output "C:\Projects\PrintHero Publish" --framework net8.0-windows --self-contained false
if %ERRORLEVEL% neq 0 (
    echo Failed to publish PrintHero.UI
    pause
    exit /b 1
)

REM Step 3: Copy missing DLLs from Debug build to publish directory
echo Step 3: Copying missing DLLs to publish directory...

REM Copy System.Drawing.Common.dll if missing
if not exist "C:\Projects\PrintHero Publish\System.Drawing.Common.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\System.Drawing.Common.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\System.Drawing.Common.dll" "C:\Projects\PrintHero Publish\"
        echo Copied System.Drawing.Common.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\System.Drawing.Common.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\System.Drawing.Common.dll" "C:\Projects\PrintHero Publish\"
        echo Copied System.Drawing.Common.dll from Debug
    ) else (
        echo WARNING: System.Drawing.Common.dll not found in Release or Debug folders
    )
)

REM Copy all other DLLs referenced in Product-Published.wxs
if not exist "C:\Projects\PrintHero Publish\Microsoft.Extensions.Configuration.Abstractions.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Configuration.Abstractions.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Configuration.Abstractions.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Configuration.Abstractions.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Configuration.Abstractions.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Configuration.Abstractions.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Configuration.Abstractions.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\Microsoft.Extensions.Configuration.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Configuration.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Configuration.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Configuration.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Configuration.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Configuration.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Configuration.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\Microsoft.Extensions.Hosting.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Hosting.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Hosting.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Hosting.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Hosting.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Hosting.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Hosting.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\Microsoft.Extensions.Hosting.Abstractions.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Hosting.Abstractions.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Hosting.Abstractions.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Hosting.Abstractions.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Hosting.Abstractions.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Hosting.Abstractions.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Hosting.Abstractions.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\Microsoft.Extensions.Options.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Options.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Options.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Options.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Options.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Options.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Options.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\Microsoft.Extensions.Primitives.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Primitives.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\Microsoft.Extensions.Primitives.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Primitives.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Primitives.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\Microsoft.Extensions.Primitives.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Microsoft.Extensions.Primitives.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\System.Text.Encodings.Web.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\System.Text.Encodings.Web.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\System.Text.Encodings.Web.dll" "C:\Projects\PrintHero Publish\"
        echo Copied System.Text.Encodings.Web.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\System.Text.Encodings.Web.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\System.Text.Encodings.Web.dll" "C:\Projects\PrintHero Publish\"
        echo Copied System.Text.Encodings.Web.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\System.IO.Pipelines.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\System.IO.Pipelines.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\System.IO.Pipelines.dll" "C:\Projects\PrintHero Publish\"
        echo Copied System.IO.Pipelines.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\System.IO.Pipelines.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\System.IO.Pipelines.dll" "C:\Projects\PrintHero Publish\"
        echo Copied System.IO.Pipelines.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\System.Diagnostics.EventLog.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\System.Diagnostics.EventLog.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\System.Diagnostics.EventLog.dll" "C:\Projects\PrintHero Publish\"
        echo Copied System.Diagnostics.EventLog.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\System.Diagnostics.EventLog.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\System.Diagnostics.EventLog.dll" "C:\Projects\PrintHero Publish\"
        echo Copied System.Diagnostics.EventLog.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\Serilog.Extensions.Hosting.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\Serilog.Extensions.Hosting.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\Serilog.Extensions.Hosting.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Serilog.Extensions.Hosting.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\Serilog.Extensions.Hosting.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\Serilog.Extensions.Hosting.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Serilog.Extensions.Hosting.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\Serilog.Sinks.File.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\Serilog.Sinks.File.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\Serilog.Sinks.File.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Serilog.Sinks.File.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\Serilog.Sinks.File.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\Serilog.Sinks.File.dll" "C:\Projects\PrintHero Publish\"
        echo Copied Serilog.Sinks.File.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\PdfiumViewer.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\PdfiumViewer.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\PdfiumViewer.dll" "C:\Projects\PrintHero Publish\"
        echo Copied PdfiumViewer.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\PdfiumViewer.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\PdfiumViewer.dll" "C:\Projects\PrintHero Publish\"
        echo Copied PdfiumViewer.dll from Debug
    )
)

if not exist "C:\Projects\PrintHero Publish\PdfSharp.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\PdfSharp.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\PdfSharp.dll" "C:\Projects\PrintHero Publish\"
        echo Copied PdfSharp.dll from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\PdfSharp.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\PdfSharp.dll" "C:\Projects\PrintHero Publish\"
        echo Copied PdfSharp.dll from Debug
    )
)

REM Copy System.Drawing.Common.dll to refs folder if needed (as per WiX file)
if not exist "C:\Projects\PrintHero Publish\refs" (
    mkdir "C:\Projects\PrintHero Publish\refs"
)
if not exist "C:\Projects\PrintHero Publish\refs\System.Drawing.Common.dll" (
    if exist "PrintHero.UI\bin\Release\net8.0-windows\System.Drawing.Common.dll" (
        copy "PrintHero.UI\bin\Release\net8.0-windows\System.Drawing.Common.dll" "C:\Projects\PrintHero Publish\refs\"
        echo Copied System.Drawing.Common.dll to refs folder from Release
    ) else if exist "PrintHero.UI\bin\Debug\net8.0-windows\System.Drawing.Common.dll" (
        copy "PrintHero.UI\bin\Debug\net8.0-windows\System.Drawing.Common.dll" "C:\Projects\PrintHero Publish\refs\"
        echo Copied System.Drawing.Common.dll to refs folder from Debug
    )
)

REM Step 4: Verify published files exist
echo Step 4: Verifying published files...
if not exist "C:\Projects\PrintHero Publish\PrintHero.UI.exe" (
    echo ERROR: PrintHero.UI.exe not found in publish directory
    pause
    exit /b 1
)

if not exist "C:\Projects\PrintHero Publish\PrintHero.Core.dll" (
    echo ERROR: PrintHero.Core.dll not found in publish directory
    pause
    exit /b 1
)


echo Published files verified successfully!

REM Step 5: Build MSI Installer using published files
echo Step 5: Building MSI Installer from published files...
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
echo.
echo Published files location: C:\Projects\PrintHero Publish\
echo MSI Installer created at: PrintHero.Installer\bin\Release\PrintHeroSetup.msi
echo.
echo The installer now uses the published files with all dependencies properly resolved.
echo PrintHero has been built without SQL Server dependencies.
echo.
pause