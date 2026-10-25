[CmdletBinding()]
param([Alias('GameRoot')][string]$GameFolder)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameFolder)) { $GameFolder = Read-Host 'Enter the full FINAL FANTASY IX installation folder' }
. (Join-Path $PSScriptRoot 'tools/Assert-EchosOfGaia.ps1')
$null = Assert-EchosOfGaiaRelease -GameRoot $GameFolder -PackageRoot $PSScriptRoot
$backupRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'EchosOfGaia/Backups'
& (Join-Path $PSScriptRoot 'tools/Deploy-FF9Access.ps1') -GameRoot $GameFolder -PackageRoot $PSScriptRoot -BackupRoot $backupRoot
if (!$?) { throw 'Echos of Gaia installation did not complete. Preserve the printed backup for recovery.' }
Write-Host 'Echos of Gaia is installed. Start your screen reader, then launch FINAL FANTASY IX normally.'
