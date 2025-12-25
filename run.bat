@echo off
setlocal
cd /d %~dp0

echo PromptLoom: restore + run

dotnet restore
if errorlevel 1 (
  echo dotnet restore failed
  exit /b 1
)

dotnet run


echo.
echo PromptLoom exited. Press any key to close.
pause
