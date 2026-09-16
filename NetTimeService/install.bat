@echo off
setlocal

:: Check for Administrator privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Administrator privileges are required.
    echo Please right-click this script and select "Run as administrator".
    exit /b 1
)

set "SERVICE_NAME=TrueTimeService"
set "DISPLAY_NAME=TrueTime Synchronization Service"

echo =======================================================
echo          TrueTime Installation & Setup
echo =======================================================

:: 1. Stop and disable w32time service to avoid port 123 conflict
echo [1/4] Stopping and disabling conflicting w32time service...
sc.exe stop w32time >nul 2>&1
sc.exe config w32time start= disabled >nul 2>&1

:: 2. Dynamically resolve binaries
echo [2/4] Resolving binaries...
set "SERVICE_BIN=%~dp0TrueTimeService.exe"
if not exist "%SERVICE_BIN%" (
    if exist "%~dp0publish\Service\TrueTimeService.exe" (
        set "SERVICE_BIN=%~dp0publish\Service\TrueTimeService.exe"
    ) else if exist "%~dp0publish\Service\NetTimeService.exe" (
        set "SERVICE_BIN=%~dp0publish\Service\NetTimeService.exe"
    ) else if exist "%~dp0bin\Debug\net8.0-windows\TrueTimeService.exe" (
        set "SERVICE_BIN=%~dp0bin\Debug\net8.0-windows\TrueTimeService.exe"
    ) else if exist "%~dp0NetTimeService\bin\Debug\net8.0-windows\TrueTimeService.exe" (
        set "SERVICE_BIN=%~dp0NetTimeService\bin\Debug\net8.0-windows\TrueTimeService.exe"
    )
)

set "TRAY_BIN=%~dp0TrueTime.Tray.exe"
if not exist "%TRAY_BIN%" (
    if exist "%~dp0publish\GUI\TrueTime.Tray.exe" (
        set "TRAY_BIN=%~dp0publish\GUI\TrueTime.Tray.exe"
    ) else if exist "%~dp0publish\GUI\NetTime.Tray.exe" (
        set "TRAY_BIN=%~dp0publish\GUI\NetTime.Tray.exe"
    ) else if exist "%~dp0NetTime.Tray\bin\Debug\net8.0-windows\TrueTime.Tray.exe" (
        set "TRAY_BIN=%~dp0NetTime.Tray\bin\Debug\net8.0-windows\TrueTime.Tray.exe"
    )
)

echo   Service binary: "%SERVICE_BIN%"
echo   Tray binary:    "%TRAY_BIN%"

:: 3. Register and start Windows Service
echo [3/4] Registering persistent Windows service %SERVICE_NAME%...
sc.exe stop %SERVICE_NAME% >nul 2>&1
sc.exe delete %SERVICE_NAME% >nul 2>&1

sc.exe create %SERVICE_NAME% binPath= "\"%SERVICE_BIN%\"" start= auto DisplayName= "%DISPLAY_NAME%"
if %errorlevel% neq 0 (
    echo [ERROR] Failed to register %SERVICE_NAME% with sc.exe.
    exit /b %errorlevel%
)

sc.exe description %SERVICE_NAME% "Authoritative multi-server internet time synchronization service with automated failover."

echo   Starting %SERVICE_NAME%...
sc.exe start %SERVICE_NAME%

:: 4. Launch System Tray application
echo [4/4] Launching TrueTime.Tray.exe...
if exist "%TRAY_BIN%" (
    start "" "%TRAY_BIN%"
) else (
    echo   [WARNING] TrueTime.Tray.exe was not found. Please run publish.bat first.
)

echo.
echo =======================================================
echo [SUCCESS] TrueTime installed and running!
echo =======================================================
exit /b 0
