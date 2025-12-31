@REM CHANGE LOG
@REM - 2025-12-31 | Request: MVVM split | Run the PromptLoom.View project explicitly.
@echo off
setlocal
cd /d %~dp0

echo PromptLoom: restore + run

dotnet restore
if errorlevel 1 (
  echo dotnet restore failed
  exit /b 1
)

dotnet run --project "%~dp0PromptLoom.View\\PromptLoom.View.csproj"


echo.
echo PromptLoom exited. Press any key to close.
pause
