[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$gameFolder, [string]$modFolder)
$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot 'tools/Assert-EchosOfGaia.ps1')
    $null = Assert-EchosOfGaiaRelease -GameRoot $gameFolder -PackageRoot $PSScriptRoot
    Write-Output 'Echos of Gaia: supported game, complete package, native dependencies and closed-game checks passed. Amethyst Mod Manager will manage file backups, updates and uninstall.'
    exit 0
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
