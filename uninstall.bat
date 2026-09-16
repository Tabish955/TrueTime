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

echo =======================================================
echo          TrueTime Service Uninstallation
echo =======================================================

echo [1/3] Closing running TrueTime.Tray instances...
taskkill /F /IM TrueTime.Tray.exe >nul 2>&1
taskkill /F /IM NetTime.Tray.exe >nul 2>&1

echo [2/3] Stopping and deleting service %SERVICE_NAME%...
sc.exe stop %SERVICE_NAME% >nul 2>&1
sc.exe delete %SERVICE_NAME% >nul 2>&1

echo [3/3] Restoring default w32time service configuration...
sc.exe config w32time start= demand >nul 2>&1

echo.
echo =======================================================
echo [SUCCESS] TrueTime uninstallation completed.
echo =======================================================
exit /b 0
