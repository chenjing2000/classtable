@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] dotnet not found. Install .NET 10 SDK first.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish.ps1" %*
if errorlevel 1 goto :fail
echo.
echo Publish succeeded.
echo Output: dist\ClassSchedule.exe
pause
exit /b 0
:fail
echo.
echo Publish failed. Save changes and exit ClassSchedule from its tray menu before retrying.
pause
exit /b 1
