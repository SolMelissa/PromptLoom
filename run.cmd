@echo off
setlocal

cd /d %~dp0

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
