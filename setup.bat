@echo off
REM ============================================================
REM  Sentry Capture - one-click setup
REM  Installs the .NET 8 SDK (if missing) and builds the app
REM  into a single self-contained .exe. No manual steps needed.
REM ============================================================
title Sentry Capture - Setup
echo.
echo  Sentry Capture - Setup
echo  ----------------------
echo  This will install the .NET 8 SDK if needed and build the app.
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup.ps1"
set EXITCODE=%ERRORLEVEL%

echo.
if "%EXITCODE%"=="0" (
    echo  Setup finished successfully.
    echo  You can now launch the app with: launch.bat
) else (
    echo  Setup failed ^(exit code %EXITCODE%^). See the messages above.
)
echo.
pause
exit /b %EXITCODE%
