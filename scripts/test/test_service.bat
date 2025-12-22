@echo off
echo Starting test batch service...
echo Current directory: %CD%
echo Time: %TIME% %DATE%

:loop
echo [%DATE% %TIME%] Batch service is running...
timeout /t 5 /nobreak >nul

REM Simulate different log levels
set /a counter+=1
if %counter% equ 10 (
    echo [%DATE% %TIME%] WARNING: This is a warning message
)
if %counter% equ 20 (
    echo [%DATE% %TIME%] ERROR: This is an error message (for testing)
)

goto loop