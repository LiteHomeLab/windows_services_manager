@echo off
REM =============================================================================
REM WinServiceManager Wrapper Script for Exit Code-Based Restart
REM =============================================================================
REM Usage: wrapper.bat "executable_path" [args...] restart_exit_code
REM Example: wrapper.bat "C:\MyApp\app.exe" --port 8080 99
REM =============================================================================

setlocal enabledelayedexpansion

REM Initialize variables
set "TARGET_EXE=%~1"
set "LOG_FILE=%~dp0logs\wrapper.log"

REM Create logs directory if not exists
if not exist "%~dp0logs" mkdir "%~dp0logs"

REM ==============================================================================
REM Parse command line arguments
REM ==============================================================================

REM Check minimum arguments (need at least executable and restart_code)
if "%~2"=="" (
    echo [%DATE% %TIME%] ERROR: Insufficient arguments >> "%LOG_FILE%"
    echo ERROR: Insufficient arguments. Usage: wrapper.bat "executable" [args...] restart_exit_code
    endlocal
    exit /b 1
)

REM Get all arguments count and extract restart exit code (last argument)
set "RESTART_CODE="
set /a ARG_COUNT=0
for %%i in (%*) do set /a ARG_COUNT+=1
set /a CURRENT=0
for %%i in (%*) do (
    set /a CURRENT+=1
    if !CURRENT!==!ARG_COUNT! set "RESTART_CODE=%%i"
)

REM Validate restart code
if not defined RESTART_CODE (
    echo [%DATE% %TIME%] ERROR: No restart exit code specified >> "%LOG_FILE%"
    echo ERROR: No restart exit code specified
    endlocal
    exit /b 1
)

REM Verify restart code is numeric
echo !RESTART_CODE!| findstr /r "^[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo [%DATE% %TIME%] ERROR: Restart exit code must be numeric: !RESTART_CODE! >> "%LOG_FILE%"
    echo ERROR: Restart exit code must be numeric
    endlocal
    exit /b 1
)

REM Rebuild arguments (skip first executable and last restart_code)
set "TARGET_ARGS="
set /a CURRENT=0
for %%i in (%*) do (
    set /a CURRENT+=1
    if !CURRENT! gtr 1 if !CURRENT! lss !ARG_COUNT! (
        if defined TARGET_ARGS (
            set "TARGET_ARGS=!TARGET_ARGS! %%i"
        ) else (
            set "TARGET_ARGS=%%i"
        )
    )
)

REM ==============================================================================
REM Validate inputs
REM ==============================================================================

if not exist "%TARGET_EXE%" (
    echo [%DATE% %TIME%] ERROR: Target executable not found: %TARGET_EXE% >> "%LOG_FILE%"
    echo ERROR: Target executable not found: %TARGET_EXE%
    endlocal
    exit /b 1
)

REM ==============================================================================
REM Log startup information
REM =============================================================================
echo [%DATE% %TIME%] Starting wrapper for: %TARGET_EXE% >> "%LOG_FILE%"
if defined TARGET_ARGS (
    echo [%DATE% %TIME%] Arguments: %TARGET_ARGS% >> "%LOG_FILE%"
)
echo [%DATE% %TIME%] Restart exit code: %RESTART_CODE% >> "%LOG_FILE%"

REM ==============================================================================
REM Execute target program and capture exit code
REM =============================================================================
echo [%DATE% %TIME%] Executing: "%TARGET_EXE%" %TARGET_ARGS% >> "%LOG_FILE%"
"%TARGET_EXE%" %TARGET_ARGS%
set "EXIT_CODE=%ERRORLEVEL%"
echo [%DATE% %TIME%] Target exited with code: %EXIT_CODE% >> "%LOG_FILE%"

REM ==============================================================================
REM Determine wrapper exit code based on target exit code
REM =============================================================================
if "%EXIT_CODE%"=="%RESTART_CODE%" (
    echo [%DATE% %TIME%] Exit code matches restart code, triggering restart... >> "%LOG_FILE%"
    endlocal
    exit /b 1
) else (
    echo [%DATE% %TIME%] Exit code does not match restart code, exiting with %EXIT_CODE% >> "%LOG_FILE%"
    endlocal
    exit /b %EXIT_CODE%
)
