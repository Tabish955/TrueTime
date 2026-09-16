@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo   Publishing TrueTime Suite (Clean Native Assemblies)
echo ========================================================

cd /d "%~dp0"

:: Clean previous publish output
if exist "publish" rmdir /s /q "publish"
mkdir "publish\Service"
mkdir "publish\GUI"

:: 1. Publish TrueTimeService (Clean, Unpacked Framework-Dependent)
echo.
echo [1/2] Publishing TrueTimeService...
dotnet publish NetTimeService/NetTimeService.csproj -c Release -r win-x64 --self-contained false -o ./publish/Service
if %errorlevel% neq 0 (
    echo [ERROR] Failed to publish TrueTimeService.
    exit /b %errorlevel%
)

:: 2. Publish TrueTime.Tray (Clean, Unpacked Framework-Dependent)
echo.
echo [2/2] Publishing TrueTime.Tray...
dotnet publish NetTime.Tray/NetTime.Tray.csproj -c Release -r win-x64 --self-contained false -o ./publish/GUI
if %errorlevel% neq 0 (
    echo [ERROR] Failed to publish TrueTime.Tray.
    exit /b %errorlevel%
)

:: 3. Remove debug symbol files (.pdb) from release folders
echo.
echo [3/4] Cleaning debug symbols...
del /q "publish\Service\*.pdb" >nul 2>&1
del /q "publish\GUI\*.pdb" >nul 2>&1

:: 4. Verify and copy required assets
echo.
echo [4/4] Verifying and copying required assets...

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
