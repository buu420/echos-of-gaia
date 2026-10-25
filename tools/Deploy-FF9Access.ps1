param([Parameter(Mandatory=$true)][string]$PackageRoot,[Parameter(Mandatory=$true)][string]$GameRoot,[Parameter(Mandatory=$true)][string]$BackupRoot,[int]$FailureAfterFiles=0,[int]$InterruptionAfterFiles=0,[int]$InterruptionBeforeFile=0)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FF9Access.Deployment.ps1')
$PackageRoot=Resolve-FF9Path $PackageRoot;$GameRoot=Resolve-FF9Path $GameRoot;$BackupRoot=Resolve-FF9Path $BackupRoot
Assert-FF9NotRunning $GameRoot
Assert-FF9SeparatePaths $GameRoot $BackupRoot
Assert-FF9SeparatePaths $PackageRoot $BackupRoot
$package=Assert-FF9Package $PackageRoot $GameRoot
if (!(Test-Path -LiteralPath (Join-Path $GameRoot 'x86/FF9.exe')) -or !(Test-Path -LiteralPath (Join-Path $GameRoot 'x64/FF9.exe'))) { throw 'Both original game architectures must be present' }
$backupDirectory=Join-Path $BackupRoot ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[void](New-Item -ItemType Directory -Path $backupDirectory -Force)
$entries=New-Object 'System.Collections.Generic.List[object]'
$ownedPaths=@($package.Files | ForEach-Object {$_.Path})+@('Memoria.ini','accessibility/installed-build.json')
# Complete the recoverable original set before modifying the installation.
foreach ($relative in $ownedPaths) {
    $path=Get-FF9OwnedPath $GameRoot $relative
    $exists=Test-Path -LiteralPath $path -PathType Leaf
    $originalHash=$null
    if ($exists) {
        $originalHash=Get-FF9Hash $path
        $backup=Get-FF9OwnedPath $backupDirectory ('Originals/'+$relative)
        [void](New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force)
        Copy-Item -LiteralPath $path -Destination $backup
        if ((Get-FF9Hash $backup) -ne $originalHash) { throw "Original backup hash mismatch: $relative" }
    }
    $entries.Add([pscustomobject]@{Path=$relative;OriginalExists=$exists;OriginalSHA256=$originalHash;NewSHA256=$null;Applied=$false;AddedSettings=@();TemporaryPath=$null})
}
$manifestPath=Join-Path $backupDirectory 'backup-manifest.json'
$backupManifest=[pscustomobject]@{Schema=1;ForkId=$package.ForkId;SourceCommit=$package.SourceCommit;GameRoot=$GameRoot;PackageRoot=$PackageRoot;PackageSHA256=(Get-FF9Hash (Join-Path $PackageRoot 'package-manifest.json'));Status='Prepared';CreatedUtc=[DateTime]::UtcNow.ToString('o');RestoredUtc=$null;Files=$entries.ToArray();Protected=(Get-FF9ProtectedSnapshot $GameRoot $package)}
Write-FF9Json $manifestPath $backupManifest
$copied=0
try {
    $backupManifest.Status='Installing'
    foreach ($file in $package.Files) {
        $entry=$entries | Where-Object {$_.Path -eq $file.Path} | Select-Object -First 1
        $path=Get-FF9OwnedPath $GameRoot $file.Path
        if ($entry.OriginalExists -and (Get-FF9Hash $path) -ne $entry.OriginalSHA256) { throw "Installation changed during backup: $($file.Path)" }
        [void](New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force)
        $entry.Applied=$true;$entry.NewSHA256=$file.SHA256;$entry.TemporaryPath=$file.Path+'.ff9access-'+[guid]::NewGuid().ToString('N')+'.tmp'
        Write-FF9Json $manifestPath $backupManifest
        if($InterruptionBeforeFile -gt 0 -and $InterruptionBeforeFile -eq ($copied+1)){[Environment]::Exit(73)}
        Copy-FF9Atomic (Get-FF9OwnedPath $PackageRoot ('Payload/'+$file.Path)) $path (Get-FF9OwnedPath $GameRoot $entry.TemporaryPath)
        $entry.NewSHA256=Get-FF9Hash $path
        if ($entry.NewSHA256 -ne $file.SHA256) { throw "Installed hash mismatch: $($file.Path)" }
        $copied++
        if ($InterruptionAfterFiles -gt 0 -and $copied -ge $InterruptionAfterFiles) { [Environment]::Exit(73) }
        if ($FailureAfterFiles -gt 0 -and $copied -ge $FailureAfterFiles) { throw 'Injected temporary-fixture copy failure' }
    }
    $entry=$entries | Where-Object {$_.Path -eq 'Memoria.ini'} | Select-Object -First 1
    $path=Get-FF9OwnedPath $GameRoot $entry.Path
    $pending=Join-Path $backupDirectory 'Pending';[void](New-Item -ItemType Directory -Path $pending -Force)
    $plannedIni=Join-Path $pending 'Memoria.ini'
    if ($entry.OriginalExists) {Copy-Item -LiteralPath $path -Destination $plannedIni} else {[IO.File]::WriteAllText($plannedIni,'')}
    $entry.AddedSettings=@(Merge-FF9NavigationSettings $plannedIni);$entry.NewSHA256=Get-FF9Hash $plannedIni
    $entry.Applied=$true;$entry.TemporaryPath=$entry.Path+'.ff9access-'+[guid]::NewGuid().ToString('N')+'.tmp'
    Write-FF9Json $manifestPath $backupManifest
    if($InterruptionBeforeFile -gt 0 -and $InterruptionBeforeFile -eq ($copied+1)){[Environment]::Exit(73)}
    Copy-FF9Atomic $plannedIni $path (Get-FF9OwnedPath $GameRoot $entry.TemporaryPath)
    $copied++;if ($InterruptionAfterFiles -gt 0 -and $copied -ge $InterruptionAfterFiles) {[Environment]::Exit(73)}
    $entry=$entries | Where-Object {$_.Path -eq 'accessibility/installed-build.json'} | Select-Object -First 1
    $path=Get-FF9OwnedPath $GameRoot $entry.Path
    $planned=Join-Path $pending 'installed-build.json'
    Write-FF9Json $planned ([pscustomobject]@{ForkId=$package.ForkId;AccessibilityApi=$package.AccessibilityApi;SourceCommit=$package.SourceCommit;PackageManifest=(Join-Path $PackageRoot 'package-manifest.json');BackupManifest=$manifestPath})
    $entry.NewSHA256=Get-FF9Hash $planned;$entry.Applied=$true;$entry.TemporaryPath=$entry.Path+'.ff9access-'+[guid]::NewGuid().ToString('N')+'.tmp'
    Write-FF9Json $manifestPath $backupManifest
    Copy-FF9Atomic $planned $path (Get-FF9OwnedPath $GameRoot $entry.TemporaryPath)
    $backupManifest.Status='Deployed';Write-FF9Json $manifestPath $backupManifest
    & (Join-Path $PSScriptRoot 'Test-FF9AccessInstall.ps1') -GameRoot $GameRoot -PackageManifest (Join-Path $PackageRoot 'package-manifest.json') -BackupManifest $manifestPath
    if (!$?) { throw 'Install verification failed' }
    Write-Host "PASS Deploy: backup and rollback manifest $manifestPath"
} catch {
    $failure=$_
    try { Restore-FF9Transaction $backupManifest $backupDirectory $GameRoot $true; $backupManifest.Status='FailedAndRestored';Write-FF9Json $manifestPath $backupManifest }
    catch { $backupManifest.Status='RecoveryRequired';Write-FF9Json $manifestPath $backupManifest;throw "Deployment failed and rollback needs attention. Backup: $manifestPath. $($_.Exception.Message)" }
    throw $failure
}
