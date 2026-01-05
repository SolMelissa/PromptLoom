# CHANGE LOG
# - 2026-01-05 | Fix: Solution restore failures | Disable static graph restore and force single-node restore.
# - 2025-12-31 | Request: MVVM split | Run the PromptLoom.View project explicitly.
# - 2025-12-30 | Request: Add git ACL preflight | Run git ACL preflight before restore/run.

$ErrorActionPreference = 'Stop'

Write-Host "PromptLoom: restore + run" -ForegroundColor Cyan

# Ensure we're running from the repo root
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

Write-Host "PromptLoom: preflight git ACL" -ForegroundColor Cyan
powershell -ExecutionPolicy Bypass -File (Join-Path $root 'scripts\preflight-git-acl.ps1')
if ($LASTEXITCODE -ne 0) { throw "git ACL preflight failed" }

dotnet restore (Join-Path $root 'PromptLoom.sln') -p:RestoreUseStaticGraphEvaluation=false -m:1
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

dotnet run --project (Join-Path $root 'PromptLoom.View\PromptLoom.View.csproj')
