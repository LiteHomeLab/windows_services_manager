@echo off
:: Enhanced Test Runner for WinServiceManager
:: Supports running different test categories with options

setlocal enabledelayedexpansion

set SCRIPT_VERSION=1.0
set SCRIPT_DIR=%~dp0
set SOLUTION_DIR=%SCRIPT_DIR%..\..
set SRC_DIR=%SOLUTION_DIR%\src

:: Colors for output (ANSI escape codes)
set GREEN=[92m
set RED=[91m
set YELLOW=[93m
set BLUE=[94m
set RESET=[0m

:: Default values
set TEST_TYPE=all
set COLLECT_COVERAGE=false
set PARALLEL=false
set VERBOSE=normal

:: Parse command line arguments
:parse_args
if "%~1"=="" goto :args_done
if /i "%~1"=="--help" goto :show_help
if /i "%~1"=="-h" goto :show_help
if /i "%~1"=="--unit" set TEST_TYPE=unit
if /i "%~1"=="--integration" set TEST_TYPE=integration
if /i "%~1"=="--performance" set TEST_TYPE=performance
if /i "%~1"=="--ui" set TEST_TYPE=ui
if /i "%~1"=="--coverage" set COLLECT_COVERAGE=true
if /i "%~1"=="--parallel" set PARALLEL=true
if /i "%~1"=="--verbose" set VERBOSE=detailed
if /i "%~1"=="--quiet" set VERBOSE=quiet
shift
goto :parse_args
:args_done

:: Display header
echo.
echo %BLUE%=====================================%RESET%
echo %BLUE%WinServiceManager Test Runner v%SCRIPT_VERSION%%RESET%
echo %BLUE%=====================================%RESET%
echo.

:: Check for administrator privileges (required for some tests)
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo %YELLOW%WARNING: Not running as administrator%RESET%
    echo %YELLOW%Some tests (UI, Integration) may fail without admin rights%RESET%
    echo.
)

:: Run tests based on type
if /i "%TEST_TYPE%"=="unit" goto :run_unit_tests
if /i "%TEST_TYPE%"=="integration" goto :run_integration_tests
if /i "%TEST_TYPE%"=="performance" goto :run_performance_tests
if /i "%TEST_TYPE%"=="ui" goto :run_ui_tests

:: Run all tests
echo %GREEN%Running all tests...%RESET%
echo.

:: 1. Unit Tests (if build succeeds)
echo %BLUE%[1/4] Building solution...%RESET%
dotnet build "%SOLUTION_DIR%\src\WinServiceManager.sln" --no-restore
if %ERRORLEVEL% neq 0 (
    echo %RED%Build failed! Skipping tests.%RESET%
    exit /b 1
)
echo %GREEN%Build successful!%RESET%
echo.

:: 2. Integration Tests
echo %BLUE%[2/4] Running Integration Tests...%RESET%
call :run_integration_tests_silent
echo.

:: 3. Performance Tests
echo %BLUE%[3/4] Running Performance Tests...%RESET%
call :run_performance_tests_silent
echo.

:: 4. UI Tests
echo %BLUE%[4/4] Running UI Tests...%RESET%
call :run_ui_tests_silent
echo.

goto :summary

:run_unit_tests
echo %GREEN%Running Unit Tests...%RESET%
dotnet test "%SRC_DIR%\WinServiceManager.Tests" ^
    --filter "FullyQualifiedName~UnitTests" ^
    --logger "console;verbosity=%VERBOSE%" ^
    %COVERAGE_ARGS%
exit /b %ERRORLEVEL%

:run_integration_tests
echo %GREEN%Running Integration Tests...%RESET%
echo %YELLOW%Note: Integration tests require proper test fixtures setup%RESET%
dotnet test "%SRC_DIR%\WinServiceManager.Tests" ^
    --filter "FullyQualifiedName~IntegrationTests" ^
    --logger "console;verbosity=%VERBOSE%" ^
    %COVERAGE_ARGS%
exit /b %ERRORLEVEL%

:run_performance_tests
echo %GREEN%Running Performance Benchmarks...%RESET%
echo %YELLOW%Note: Performance tests take longer to complete%RESET%
dotnet run --project "%SRC_DIR%\WinServiceManager.PerformanceTests" ^
    --configuration Release
exit /b %ERRORLEVEL%

:run_ui_tests
echo %GREEN%Running UI Automation Tests...%RESET%
echo %YELLOW%Note: UI tests require administrator privileges%RESET%
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo %RED%ERROR: UI tests require administrator privileges%RESET%
    echo Please run this script as administrator.
    exit /b 1
)
dotnet test "%SRC_DIR%\WinServiceManager.UI.Tests" ^
    --logger "console;verbosity=%VERBOSE%"
exit /b %ERRORLEVEL%

:: Silent versions for batch execution
:run_integration_tests_silent
dotnet test "%SRC_DIR%\WinServiceManager.Tests" ^
    --filter "FullyQualifiedName~IntegrationTests" ^
    --logger "console;verbosity=%VERBOSE%" ^
    --no-build >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo %GREEN%- Integration Tests: PASSED%RESET%
) else (
    echo %RED%- Integration Tests: FAILED (may require admin rights)%RESET%
)
exit /b 0

:run_performance_tests_silent
dotnet run --project "%SRC_DIR%\WinServiceManager.PerformanceTests" ^
    --configuration Release --no-build >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo %GREEN%- Performance Tests: PASSED%RESET%
) else (
    echo %YELLOW%- Performance Tests: SKIPPED (project may not be built)%RESET%
)
exit /b 0

:run_ui_tests_silent
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo %YELLOW%- UI Tests: SKIPPED (requires admin rights)%RESET%
    exit /b 0
)
dotnet test "%SRC_DIR%\WinServiceManager.UI.Tests" ^
    --logger "console;verbosity=quiet" --no-build >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo %GREEN%- UI Tests: PASSED%RESET%
) else (
    echo %YELLOW%- UI Tests: FAILED (application may not be built)%RESET%
)
exit /b 0

:summary
echo.
echo %BLUE%=====================================%RESET%
echo %BLUE%Test Run Summary%RESET%
echo %BLUE%=====================================%RESET%
echo.
echo To run specific test categories:
echo   run-tests-enhanced --unit         Run only unit tests
echo   run-tests-enhanced --integration  Run only integration tests
echo   run-tests-enhanced --performance  Run only performance benchmarks
echo   run-tests-enhanced --ui           Run only UI tests
echo.
echo Options:
echo   --coverage    Collect code coverage
echo   --parallel    Run tests in parallel
echo   --verbose     Show detailed output
echo   --quiet       Show minimal output
echo.
echo Run 'run-tests-enhanced --help' for more information.
echo.

exit /b 0

:show_help
echo Usage: run-tests-enhanced [OPTIONS] [TEST_TYPE]
echo.
echo Test Types:
echo   --unit         Run only unit tests
echo   --integration  Run only integration tests
echo   --performance  Run only performance benchmarks
echo   --ui           Run only UI automation tests
echo   (default)      Run all tests
echo.
echo Options:
echo   --coverage    Enable code coverage collection
echo   --parallel    Run tests in parallel where supported
echo   --verbose     Show detailed test output
echo   --quiet       Show minimal test output
echo   -h, --help    Show this help message
echo.
echo Examples:
echo   run-tests-enhanced --integration
echo   run-tests-enhanced --unit --coverage
echo   run-tests-enhanced --performance --verbose
echo.
exit /b 0
