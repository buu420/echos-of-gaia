param([Parameter(Mandatory=$true)][string]$GameRoot,[Parameter(Mandatory=$true)][string]$PackageManifest,[Parameter(Mandatory=$true)][string]$BackupManifest)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FF9Access.Deployment.ps1')
$GameRoot=Resolve-FF9Path $GameRoot
$package=Assert-FF9Package (Split-Path $PackageManifest -Parent) $GameRoot
$backup=Get-Content -LiteralPath $BackupManifest -Raw | ConvertFrom-Json
if (!(Resolve-FF9Path $backup.GameRoot).Equals($GameRoot,[StringComparison]::OrdinalIgnoreCase) -or $backup.Status -ne 'Deployed') { throw 'Install verification requires its deployed backup manifest' }
foreach ($file in $package.Files) {
    $path=Get-FF9OwnedPath $GameRoot $file.Path
    if (!(Test-Path -LiteralPath $path) -or (Get-FF9Hash $path) -ne $file.SHA256) { throw "Installed payload differs: $($file.Path)" }
}
foreach ($file in $backup.Protected) {
    $path=Get-FF9OwnedPath $GameRoot $file.Path
    if (!(Test-Path -LiteralPath $path) -or (Get-FF9Hash $path) -ne $file.SHA256) { throw "Protected installation file changed: $($file.Path)" }
}
foreach ($entry in $backup.Files) {
    if ($entry.OriginalExists -and (Get-FF9Hash (Get-FF9OwnedPath (Split-Path $BackupManifest -Parent) ('Originals/'+$entry.Path))) -ne $entry.OriginalSHA256) { throw "Rollback file missing or changed: $($entry.Path)" }
}
foreach ($arch in @('x86','x64')) {
    $path=Join-Path $GameRoot "$arch/FF9_Data/Managed/Assembly-CSharp.dll"
    $bytes=[IO.File]::ReadAllBytes($path)
    $text=[Text.Encoding]::UTF8.GetString($bytes)
    # CLR string/blob heaps need not begin at an even file offset.
    $identity=[Text.Encoding]::Unicode.GetString($bytes).Contains('ff9-access-local') -or [Text.Encoding]::Unicode.GetString($bytes,1,$bytes.Length-1).Contains('ff9-access-local')
    if (!$text.Contains('NavigationBuildIdentity') -or !$identity) { throw "Custom build identity missing in $arch" }
    foreach ($type in @('NavigationRuntime','OwnedFieldFollower','OwnedWorldTravel','NavigationCoordinator')) { if (!$text.Contains($type)) { throw "Accessibility type missing in ${arch}: $type" } }
}
Write-Host "PASS Install: $($package.Files.Count) payload files; $($backup.Protected.Count) protected files unchanged; complete rollback; both active navigation cores"
