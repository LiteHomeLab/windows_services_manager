@echo off
:: Run Tests Script for WinServiceManager (Batch Version)

setlocal enabledelayedexpansion

echo =====================================
echo WinServiceManager Test Runner
echo =====================================
echo.

:: Get script directory
set SCRIPT_DIR=%~dp0
set SOLUTION_DIR=%SCRIPT_DIR%..
set TEST_PROJECT_DIR=%SOLUTION_DIR%\src\WinServiceManager.Tests

:: Check if test project exists
if not exist "%TEST_PROJECT_DIR%" (
    echo Error: Test project not found at %TEST_PROJECT_DIR%
    exit /b 1
)

:: Build the solution first
echo Building solution...
dotnet build "%SOLUTION_DIR%"

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    exit /b 1
)

echo.
echo Build successful!

:: Run tests
echo.
echo Running tests...
dotnet test "%TEST_PROJECT_DIR%" --logger "console;verbosity=normal"

if %ERRORLEVEL% equ 0 (
    echo.
    echo All tests passed!
) else (
    echo.
    echo Some tests failed!
)

exit /b %ERRORLEVEL%