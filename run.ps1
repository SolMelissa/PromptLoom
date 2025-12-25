$ErrorActionPreference = 'Stop'

Write-Host "PromptLoom: restore + run" -ForegroundColor Cyan

# Ensure we're running from the repo root
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

dotnet restore
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

dotnet run
