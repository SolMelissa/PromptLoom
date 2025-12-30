# CHANGE LOG
# - 2025-12-30 | Request: Add git ACL preflight | Added .git write check with optional fix guidance.

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
  [switch]$Fix
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$gitDir = Join-Path $repoRoot '.git'

if (-not (Test-Path -LiteralPath $gitDir)) {
  Write-Error "No .git folder found at $gitDir. Run this from a git working tree."
  exit 2
}

$testFile = Join-Path $gitDir 'acl_test.tmp'
$lastError = $null

try {
  New-Item -Path $testFile -ItemType File -Force -ErrorAction Stop | Out-Null
  Remove-Item -Path $testFile -Force -ErrorAction Stop
  Write-Host "OK: .git is writable."
  exit 0
} catch {
  $lastError = $_
}

Write-Error "ERROR: .git is not writable. Git uses .git\index.lock, so commands like git add/checkout will fail."
if ($lastError) {
  Write-Host "Details: $($lastError.Exception.Message)"
}

$acl = Get-Acl -LiteralPath $gitDir
$denyRules = $acl.Access | Where-Object { $_.AccessControlType -eq 'Deny' }
if ($denyRules) {
  Write-Host "Deny ACL entries on .git:"
  foreach ($rule in $denyRules) {
    $identity = $rule.IdentityReference.Value
    $rights = $rule.FileSystemRights
    $inherit = $rule.InheritanceFlags
    $prop = $rule.PropagationFlags
    Write-Host " - $identity | $rights | $inherit | $prop"
  }
}

Write-Host ""
Write-Host "Fix option (requires elevated PowerShell):"
Write-Host "  Run: .\scripts\preflight-git-acl.ps1 -Fix"
Write-Host ""
Write-Host "Manual fix steps (elevated PowerShell):"
Write-Host "  `$path = '$gitDir'"
Write-Host "  `$acl = New-Object System.Security.AccessControl.DirectorySecurity"
Write-Host "  `$acl.SetAccessRuleProtection(`$true, `$false)"
Write-Host "  `$acl.SetOwner([System.Security.Principal.NTAccount]'$env:USERDOMAIN\$env:USERNAME')"
Write-Host "  `$rules = @("
Write-Host "    (New-Object System.Security.AccessControl.FileSystemAccessRule('$env:USERDOMAIN\$env:USERNAME','FullControl','ContainerInherit,ObjectInherit','None','Allow'))"
Write-Host "    (New-Object System.Security.AccessControl.FileSystemAccessRule('SYSTEM','FullControl','ContainerInherit,ObjectInherit','None','Allow'))"
Write-Host "    (New-Object System.Security.AccessControl.FileSystemAccessRule('Administrators','FullControl','ContainerInherit,ObjectInherit','None','Allow'))"
Write-Host "    (New-Object System.Security.AccessControl.FileSystemAccessRule('Authenticated Users','Modify','ContainerInherit,ObjectInherit','None','Allow'))"
Write-Host "  )"
Write-Host "  `$rules | ForEach-Object { `$acl.AddAccessRule(`$_) }"
Write-Host "  Set-Acl -Path `$path -AclObject `$acl"
Write-Host "  Get-ChildItem -Path `$path -Force -Recurse | ForEach-Object { Set-Acl -Path `$_.FullName -AclObject `$acl }"

if (-not $Fix) {
  exit 1
}

Write-Host ""
Write-Host "Attempting fix (this will replace ACLs on .git and its children)."

if ($PSCmdlet.ShouldProcess($gitDir, 'Reset .git ACLs')) {
  $ownerAccount = "$env:USERDOMAIN\$env:USERNAME"
  $newAcl = New-Object System.Security.AccessControl.DirectorySecurity
  $newAcl.SetAccessRuleProtection($true, $false)
  $newAcl.SetOwner([System.Security.Principal.NTAccount]$ownerAccount)

  $rules = @(
    (New-Object System.Security.AccessControl.FileSystemAccessRule($ownerAccount, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    (New-Object System.Security.AccessControl.FileSystemAccessRule('SYSTEM', 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    (New-Object System.Security.AccessControl.FileSystemAccessRule('Administrators', 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    (New-Object System.Security.AccessControl.FileSystemAccessRule('Authenticated Users', 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
  )

  $rules | ForEach-Object { $newAcl.AddAccessRule($_) }
  Set-Acl -Path $gitDir -AclObject $newAcl
  Get-ChildItem -Path $gitDir -Force -Recurse | ForEach-Object { Set-Acl -Path $_.FullName -AclObject $newAcl }

  Write-Host "Fix applied. Re-run this script to confirm."
}
