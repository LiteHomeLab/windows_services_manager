@echo off
setlocal enabledelayedexpansion

REM Get the project root directory (two levels up from script location)
set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..\..\

REM Convert to absolute path
for %%i in ("%PROJECT_ROOT%") do set PROJECT_ROOT=%%~fi

echo Starting build process...
echo Project root: %PROJECT_ROOT%
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

REM Set output directory to project root bin folder
set BIN_OUTPUT=%PROJECT_ROOT%bin\%PLATFORM%\%CONFIGURATION%

REM Clean previous builds
echo Cleaning previous builds...
if exist "%BIN_OUTPUT%" rmdir /s /q "%BIN_OUTPUT%"
dotnet clean "%PROJECT_ROOT%src\WinServiceManager\WinServiceManager.csproj" -c %CONFIGURATION% -p:Platform=%PLATFORM%

REM Restore packages
echo Restoring NuGet packages...
dotnet restore "%PROJECT_ROOT%src\WinServiceManager\WinServiceManager.csproj"

REM Build the main project only (excluding tests to avoid build failures)
echo Building main project...
dotnet build "%PROJECT_ROOT%src\WinServiceManager\WinServiceManager.csproj" -c %CONFIGURATION% -p:Platform=%PLATFORM% --no-restore -o "%BIN_OUTPUT%"

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    exit /b 1
)

REM Copy template files to output directory (for non-publish builds)
echo Copying template files...

REM Create templates directory in output
if not exist "%BIN_OUTPUT%\templates" mkdir "%BIN_OUTPUT%\templates"

REM Copy WinSW executable if exists and is valid (size > 100KB)
set "WINSW_SOURCE=%PROJECT_ROOT%src\WinServiceManager\templates\WinSW-x64.exe"
if exist "%WINSW_SOURCE%" (
    for %%A in ("%WINSW_SOURCE%") do set WINSW_SIZE=%%~zA
    if !WINSW_SIZE! GTR 102400 (
        copy "%WINSW_SOURCE%" "%BIN_OUTPUT%\templates\WinSW-x64.exe" >nul
        echo Copied WinSW executable (!WINSW_SIZE! bytes)
    ) else (
        echo Warning: WinSW file exists but is too small (!WINSW_SIZE! bytes), possibly a placeholder
        echo Please download the real WinSW-x64.exe from https://github.com/winsw/winsw/releases
    )
) else (
    echo Warning: WinSW executable not found at src\WinServiceManager\templates\WinSW-x64.exe
)

REM Copy wrapper.bat if exists
if exist "%PROJECT_ROOT%src\WinServiceManager\templates\wrapper.bat" (
    copy "%PROJECT_ROOT%src\WinServiceManager\templates\wrapper.bat" "%BIN_OUTPUT%\templates\wrapper.bat" >nul
    echo Copied wrapper.bat template
) else (
    echo Warning: wrapper.bat not found at src\WinServiceManager\templates\wrapper.bat
)

REM Publish if requested
if /i "%PUBLISH%"=="true" (
    echo Publishing application...

    set RID=win-%PLATFORM%
    set PUBLISH_PATH=%PROJECT_ROOT%%OUTPUT_PATH%\%RID%

    dotnet publish "%PROJECT_ROOT%src\WinServiceManager\WinServiceManager.csproj" ^
        -c %CONFIGURATION% ^
        -r %RID% ^
        --self-contained false ^
        -p:PublishSingleFile=false ^
        -p:PublishReadyToRun=true ^
        -o "%PUBLISH_PATH%"

    if %ERRORLEVEL% neq 0 (
        echo Publish failed!
        exit /b 1
    )

    REM Copy additional files
    echo Copying additional files...

    REM Create templates directory
    if not exist "%PUBLISH_PATH%\templates" mkdir "%PUBLISH_PATH%\templates"

    REM Copy WinSW executable if exists and is valid (size > 100KB)
    set "WINSW_SOURCE=%PROJECT_ROOT%src\WinServiceManager\templates\WinSW-x64.exe"
    if exist "%WINSW_SOURCE%" (
        for %%A in ("%WINSW_SOURCE%") do set WINSW_SIZE=%%~zA
        if !WINSW_SIZE! GTR 102400 (
            copy "%WINSW_SOURCE%" "%PUBLISH_PATH%\templates\WinSW-x64.exe" >nul
            echo Copied WinSW executable (!WINSW_SIZE! bytes)
        ) else (
            echo Warning: WinSW file exists but is too small (!WINSW_SIZE! bytes), possibly a placeholder
            echo Please download the real WinSW-x64.exe from https://github.com/winsw/winsw/releases
        )
    ) else (
        echo Warning: WinSW executable not found at src\WinServiceManager\templates\WinSW-x64.exe
    )

    REM Copy wrapper.bat if exists
    if exist "%PROJECT_ROOT%src\WinServiceManager\templates\wrapper.bat" (
        copy "%PROJECT_ROOT%src\WinServiceManager\templates\wrapper.bat" "%PUBLISH_PATH%\templates\wrapper.bat" >nul
        echo Copied wrapper.bat template
    ) else (
        echo Warning: wrapper.bat not found at src\WinServiceManager\templates\wrapper.bat
    )

    REM Create distribution package
    for /f "delims=" %%i in ('type "%PROJECT_ROOT%src\WinServiceManager\WinServiceManager.csproj" ^| findstr /r /c:"<AssemblyVersion>"') do (
        for /f "tokens=3 delims=><" %%v in ("%%i") do set VERSION=%%v
    )
    if "!VERSION!"=="" set VERSION=1.0.0
    set PACKAGE_NAME=WinServiceManager-v!VERSION!-%RID%
    set PACKAGE_PATH=%PROJECT_ROOT%%OUTPUT_PATH%\%PACKAGE_NAME%

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
echo %BIN_OUTPUT%
echo.
echo Main executable:
echo %BIN_OUTPUT%\WinServiceManager.exe
echo.
pause