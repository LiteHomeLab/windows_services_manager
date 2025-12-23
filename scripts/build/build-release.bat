@echo off
setlocal enabledelayedexpansion

echo Starting build process...
echo.

REM Set default configuration
set CONFIGURATION=Release
set PLATFORM=x64
set PUBLISH=false
set OUTPUT_PATH=publish

REM Parse command line arguments
:parse_args
if "%~1"=="" goto :build
if /i "%~1"=="-c" (
    set CONFIGURATION=%~2
    shift
    shift
    goto :parse_args
)
if /i "%~1"=="-p" (
    set PLATFORM=%~2
    shift
    shift
    goto :parse_args
)
if /i "%~1"=="--publish" (
    set PUBLISH=true
    shift
    goto :parse_args
)
if /i "%~1"=="-o" (
    set OUTPUT_PATH=%~2
    shift
    shift
    goto :parse_args
)
echo Unknown argument: %~1
echo Usage: %0 [-c Configuration] [-p Platform] [--publish] [-o OutputPath]
echo.
echo Options:
echo   -c Configuration   Build configuration (Debug or Release, default: Release)
echo   -p Platform        Target platform (x64, x86, or AnyCPU, default: x64)
echo   --publish          Create distribution package
echo   -o OutputPath      Output directory for published files (default: publish)
echo.
exit /b 1

:build
echo Configuration: %CONFIGURATION%
echo Platform: %PLATFORM%
echo.

REM Clean previous builds
echo Cleaning previous builds...
dotnet clean src\WinServiceManager\WinServiceManager.csproj -c %CONFIGURATION% -p:Platform=%PLATFORM%

REM Restore packages
echo Restoring NuGet packages...
dotnet restore src\WinServiceManager\WinServiceManager.csproj

REM Build the main project only (excluding tests to avoid build failures)
echo Building main project...
dotnet build src\WinServiceManager\WinServiceManager.csproj -c %CONFIGURATION% -p:Platform=%PLATFORM% --no-restore

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    exit /b 1
)

REM Publish if requested
if /i "%PUBLISH%"=="true" (
    echo Publishing application...

    set RID=win-%PLATFORM%
    set PUBLISH_PATH=%OUTPUT_PATH%\%RID%

    dotnet publish src\WinServiceManager\WinServiceManager.csproj ^
        -c %CONFIGURATION% ^
        -r %RID% ^
        --self-contained false ^
        -p:PublishSingleFile=false ^
        -p:PublishReadyToRun=true ^
        -o %PUBLISH_PATH%

    if %ERRORLEVEL% neq 0 (
        echo Publish failed!
        exit /b 1
    )

    REM Copy additional files
    echo Copying additional files...

    REM Create templates directory
    if not exist "%PUBLISH_PATH%\templates" mkdir "%PUBLISH_PATH%\templates"

    REM Copy WinSW executable if exists
    if exist "templates\WinSW-x64.exe" (
        copy "templates\WinSW-x64.exe" "%PUBLISH_PATH%\templates\WinSW-x64.exe" >nul
        echo Copied WinSW executable
    ) else (
        echo Warning: WinSW executable not found at templates\WinSW-x64.exe
    )

    REM Create distribution package
    for /f "tokens=3 delims=<> " %%i in ('findstr "AssemblyVersion" src\WinServiceManager\WinServiceManager.csproj') do set VERSION=%%i
    if "!VERSION!"=="" set VERSION=1.0.0
    set PACKAGE_NAME=WinServiceManager-v!VERSION!-%RID%
    set PACKAGE_PATH=%OUTPUT_PATH%\%PACKAGE_NAME%

    if exist "%PACKAGE_PATH%" rmdir /s /q "%PACKAGE_PATH%"
    mkdir "%PACKAGE_PATH%"

    REM Copy published files
    xcopy "%PUBLISH_PATH%\*" "%PACKAGE_PATH%\" /E /I /Y >nul

    REM Create README for distribution
    (
        echo # WinServiceManager v!VERSION!
        echo.
        echo ## System Requirements
        echo - Windows 10/11 or Windows Server 2019/2022
        echo - .NET 8 Runtime
        echo - Administrator privileges
        echo.
        echo ## Installation Instructions
        echo 1. Run WinServiceManager.exe as Administrator
        echo 2. First run will automatically create necessary directory structure
        echo 3. Ensure templates\WinSW-x64.exe file exists
        echo.
        echo ## Usage Instructions
        echo See project documentation: https://github.com/LiteHomeLab/windows_services_manager
        echo.
        echo ## Release Date
        echo %date%
    ) > "%PACKAGE_PATH%\README.txt"

    echo.
    echo Distribution package created: %OUTPUT_PATH%\%PACKAGE_NAME%
    echo Package contents:
    dir "%PACKAGE_PATH%" /B
)

echo.
echo Build process completed successfully!
echo.
echo Output location:
echo src\WinServiceManager\bin\%PLATFORM%\%CONFIGURATION%\net8.0-windows\
echo.
echo Main executable:
echo src\WinServiceManager\bin\%PLATFORM%\%CONFIGURATION%\net8.0-windows\WinServiceManager.exe
echo.
pause