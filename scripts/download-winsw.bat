@echo off
REM WinSW Download Script for WinServiceManager (Batch version)
REM Automatically downloads the correct WinSW version for the project

setlocal enabledelayedexpansion

echo.
echo ===========================================
echo   WinSW Download Script for WinServiceManager
echo ===========================================
echo.

REM Configuration
set WINSW_VERSION=3.0.0
set BASE_URL=https://github.com/winsw/winsw/releases/download/v%WINSW_VERSION%
set FILE_NAME=WinSW-x64.exe
set DOWNLOAD_URL=%BASE_URL%/%FILE_NAME%
set TARGET_DIR=src\WinServiceManager\templates
set TARGET_PATH=%TARGET_DIR%\%FILE_NAME%

echo Version: %WINSW_VERSION%
echo Target: %TARGET_PATH%
echo URL: %DOWNLOAD_URL%
echo.

REM Create target directory if it doesn't exist
if not exist "%TARGET_DIR%" (
    echo Creating target directory...
    mkdir "%TARGET_DIR%"
)

REM Check if WinSW already exists
if exist "%TARGET_PATH%" (
    if "%1"=="--force" (
        echo Overriding existing WinSW file...
    ) else (
        echo WinSW already exists: %FILE_NAME%
        echo Use --force to override
        goto :success
    )
)

REM Download WinSW using PowerShell (more reliable than bitsadmin)
echo Downloading WinSW...
powershell -Command "& {$ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest -Uri '%DOWNLOAD_URL%' -OutFile '%TARGET_PATH'}"

if %errorlevel% equ 0 (
    echo.
    echo Download completed successfully!

    REM Verify download
    if exist "%TARGET_PATH%" (
        for %%F in ("%TARGET_PATH%") do (
            echo   File: %%~fF
            echo   Size: %%~zF bytes
        )

        REM Quick validation
        echo Verifying WinSW executable...
        "%TARGET_PATH%" --version >nul 2>&1
        if %errorlevel% le 1 (
            echo WinSW executable is valid and working
        ) else (
            echo WinSW downloaded but may need verification
        )
    ) else (
        echo Error: Downloaded file not found
        goto :error
    )
) else (
    echo.
    echo Download failed!
    echo Please check your internet connection and try again.
    echo You can also download manually from: %DOWNLOAD_URL%
    goto :error
)

:success
echo.
echo ===========================================
echo   WinSW setup completed!
echo ===========================================
echo The WinServiceManager is now ready to use.
echo.
goto :end

:error
echo.
echo Setup failed. Please check the error messages above.
echo.
exit /b 1

:end
endlocal