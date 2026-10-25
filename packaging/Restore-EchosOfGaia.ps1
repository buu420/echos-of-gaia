[CmdletBinding()]
param([Parameter(Mandatory=$true)][Alias('GameRoot')][string]$GameFolder, [Parameter(Mandatory=$true)][string]$BackupManifest)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'tools/Restore-FF9Access.ps1') -GameRoot $GameFolder -BackupManifest $BackupManifest
if (!$?) { throw 'Rollback did not complete. The backup is preserved; inspect the reported changed file before retrying.' }
