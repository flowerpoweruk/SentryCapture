@echo off
REM ============================================================
REM  Sentry Capture - one-click launch
REM ============================================================
title Sentry Capture

set "EXE=%~dp0publish\SentryCapture.exe"

if not exist "%EXE%" (
    echo Sentry Capture has not been built yet.
    echo Please run setup.bat first.
    echo.
    pause
    exit /b 1
)

start "" "%EXE%"
exit /b 0
