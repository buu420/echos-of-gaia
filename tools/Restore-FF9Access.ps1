param([Parameter(Mandatory=$true)][string]$BackupManifest,[Parameter(Mandatory=$true)][string]$GameRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FF9Access.Deployment.ps1')
$GameRoot=Resolve-FF9Path $GameRoot
$BackupManifest=Resolve-FF9Path $BackupManifest
Assert-FF9NotRunning $GameRoot
$backupDirectory=Split-Path $BackupManifest -Parent
Assert-FF9SeparatePaths $GameRoot $backupDirectory
$manifest=Get-Content -LiteralPath $BackupManifest -Raw | ConvertFrom-Json
if ($manifest.Schema -ne 1 -or $manifest.ForkId -ne 'ff9-access-local' -or !(Resolve-FF9Path $manifest.GameRoot).Equals($GameRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Backup does not belong to this explicit installation' }
Restore-FF9Transaction $manifest $backupDirectory $GameRoot $false
$manifest.Status='Restored';$manifest.RestoredUtc=[DateTime]::UtcNow.ToString('o')
Write-FF9Json $BackupManifest $manifest
Write-Host "PASS Restore: $BackupManifest"
