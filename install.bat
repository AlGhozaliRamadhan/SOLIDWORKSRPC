@echo off
:: Right-click this file -> Run as Administrator
:: Auto-discovers SolidWorks, builds, and registers the add-in
echo [SOLIDWORKS Design] Building and registering (Admin required)...
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\install.ps1" -Build
if %ERRORLEVEL% NEQ 0 (
  echo.
  echo Failed. Make sure you ran this as Administrator.
  pause
  exit /b 1
)
echo.
echo SUCCESS - now open SolidWorks -> Tools -> Add-Ins -> check SOLIDWORKS Design
pause
