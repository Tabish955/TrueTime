@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo   Publishing TrueTime Suite (Self-Contained Single-File)
echo ========================================================

cd /d "%~dp0"

:: Create output directories if they do not exist
if not exist "publish\Service" mkdir "publish\Service"
if not exist "publish\GUI" mkdir "publish\GUI"

:: 1. Publish TrueTimeService
echo.
echo [1/2] Publishing TrueTimeService...
dotnet publish NetTimeService/NetTimeService.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/Service
if %errorlevel% neq 0 (
    echo [ERROR] Failed to publish TrueTimeService.
    exit /b %errorlevel%
)

:: 2. Publish TrueTime.Tray
echo.
echo [2/2] Publishing TrueTime.Tray...
dotnet publish NetTime.Tray/NetTime.Tray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/GUI
if %errorlevel% neq 0 (
    echo [ERROR] Failed to publish TrueTime.Tray.
    exit /b %errorlevel%
)

:: 3. Ensure all required assets are copied to the publish folders
echo.
echo [3/3] Verifying and copying required assets...

if exist "NetTimeService\appsettings.json" (
    copy /Y "NetTimeService\appsettings.json" "publish\Service\appsettings.json" >nul
    echo   [OK] Copied appsettings.json to publish\Service\
)

if exist "NetTime.Tray\app.ico" (
    copy /Y "NetTime.Tray\app.ico" "publish\GUI\app.ico" >nul
    copy /Y "NetTime.Tray\app.ico" "publish\GUI\tray.ico" >nul
    echo   [OK] Copied tray icon to publish\GUI\
)

echo.
echo ========================================================
echo [SUCCESS] TrueTime successfully published!
echo   Service: %~dp0publish\Service\TrueTimeService.exe
echo   GUI:     %~dp0publish\GUI\TrueTime.Tray.exe
echo ========================================================
exit /b 0
