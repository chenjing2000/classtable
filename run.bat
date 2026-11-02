@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0dist\ClassSchedule.exe" (
  echo [ERROR] Published application not found. Run build-release.bat first.
  pause
  exit /b 1
)
start "" "%~dp0dist\ClassSchedule.exe"
