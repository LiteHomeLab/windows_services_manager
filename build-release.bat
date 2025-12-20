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
dotnet clean src/WinServiceManager.sln -c %CONFIGURATION% -p:Platform=%PLATFORM%

REM Restore packages
echo Restoring NuGet packages...
dotnet restore src/WinServiceManager.sln

REM Build the project
echo Building solution...
dotnet build src/WinServiceManager.sln -c %CONFIGURATION% -p:Platform=%PLATFORM% --no-restore

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    exit /b 1
)

REM Publish if requested
if /i "%PUBLISH%"=="true" (
    echo Publishing application...

    set RID=win-%PLATFORM%
    set PUBLISH_PATH=%OUTPUT_PATH%\%RID%

    dotnet publish src/WinServiceManager/WinServiceManager.csproj ^
        -c %CONFIGURATION% ^
        -r %RID% ^
        --self-contained false ^
        -p:PublishSingleFile=true ^
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
        copy "templates\WinSW-x64.exe" "%PUBLISH_PATH%\templates\WinSW-x64.exe"
        echo Copied WinSW executable
    ) else (
        echo Warning: WinSW executable not found at templates\WinSW-x64.exe
    )

    REM Create distribution package
    for /f "tokens=3 delims=<>" %%i in ('findstr "AssemblyVersion" src\WinServiceManager\WinServiceManager.csproj') do set VERSION=%%i
    set PACKAGE_NAME=WinServiceManager-v!VERSION!-%RID%
    set PACKAGE_PATH=%OUTPUT_PATH%\%PACKAGE_NAME%

    if exist "%PACKAGE_PATH%" rmdir /s /q "%PACKAGE_PATH%"
    mkdir "%PACKAGE_PATH%"

    REM Copy published files
    xcopy "%PUBLISH_PATH%\*" "%PACKAGE_PATH%\" /E /I /Y

    REM Create README for distribution
    (
        echo # WinServiceManager v!VERSION!
        echo.
        echo ## 系统要求
        echo - Windows 10/11 或 Windows Server 2019/2022
        echo - .NET 8 Runtime
        echo - 管理员权限
        echo.
        echo ## 安装说明
        echo 1. 以管理员身份运行 WinServiceManager.exe
        echo 2. 首次运行会自动创建必要的目录结构
        echo 3. 确保 templates\WinSW-x64.exe 文件存在
        echo.
        echo ## 使用说明
        echo 详见项目文档：https://github.com/LiteHomeLab/windows_services_manager
        echo.
        echo ## 发布日期
        echo %date%
    ) > "%PACKAGE_PATH%\README.txt"

    echo.
    echo Distribution package created: %OUTPUT_PATH%\%PACKAGE_NAME%.zip
    echo Package contents:
    dir "%PACKAGE_PATH%" /B

    REM Create ZIP package using PowerShell
    powershell -Command "Compress-Archive -Path '%PACKAGE_PATH%\*' -DestinationPath '%OUTPUT_PATH%\%PACKAGE_NAME%.zip' -Force"
)

echo.
echo Build process completed successfully!
pause