@echo off
REM CHANGE LOG
REM - 2025-12-30 | Request: Add git ACL preflight | Run git ACL preflight before restore/run.

setlocal

cd /d %~dp0

echo PromptLoom: preflight git ACL
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\preflight-git-acl.ps1"
if errorlevel 1 goto :err

echo PromptLoom: dotnet restore
dotnet restore
if errorlevel 1 goto :err

echo PromptLoom: dotnet run
dotnet run
if errorlevel 1 goto :err

echo.
echo PromptLoom exited. Press any key to close.
pause >nul

exit /b 0

:err
echo.
echo Build or run failed.
echo.
echo Press any key to close.
pause >nul
exit /b 1
