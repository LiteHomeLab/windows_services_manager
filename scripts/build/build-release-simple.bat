@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Building WinServiceManager Release
echo ========================================

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script requires administrator privileges.
    echo Please run as administrator.
    pause
    exit /b 1
)

echo.
echo Building release version...

REM Build the solution
dotnet build src\WinServiceManager.sln -c Release

if %errorlevel% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo Build completed successfully!

REM Check if WinSW exists
if not exist "src\WinServiceManager\templates\WinSW-x64.exe" (
    echo.
    echo WARNING: WinSW executable not found!
    echo Please download WinSW-x64.exe from:
    echo https://github.com/winsw/winsw/releases/download/v3.0.0/WinSW-x64.exe
    echo And copy it to: src\WinServiceManager\templates\WinSW-x64.exe
    echo.
)

echo.
echo To run the application:
echo   1. Ensure WinSW-x64.exe is in src\WinServiceManager\templates\
echo   2. Run as administrator: src\WinServiceManager\bin\Release\net8.0-windows\WinServiceManager.exe
echo.

pause