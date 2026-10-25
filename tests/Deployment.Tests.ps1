param([string]$StageRoot='D:\FF9Access\staging\development', [string]$InstalledGameRoot='D:\SteamLibrary\steamapps\common\FINAL FANTASY IX')
$ErrorActionPreference = 'Stop'
$source = Split-Path $PSScriptRoot -Parent
. (Join-Path $source 'tools/FF9Access.Deployment.ps1')
$owned = Join-Path $source ('.superpowers/sdd/2026-10-10-ffix-accessible-navigation/DeploymentFixture-' + [guid]::NewGuid().ToString('N'))
$game = Join-Path $owned 'game'
$package = Join-Path $owned 'package'
$backup = Join-Path $owned 'backups'
function Require([bool]$condition,[string]$message) { if (!$condition) { throw $message } }
function RunScript([string]$name,[hashtable]$arguments) {
    & (Join-Path $source "tools/$name") @arguments
    if (!$?) { throw "Script failed: $name" }
}
[void](New-Item -ItemType Directory -Path $game -Force)
foreach ($arch in @('x86','x64')) {
    $managed = Join-Path $game "$arch/FF9_Data/Managed"
    [void](New-Item -ItemType Directory -Path $managed -Force)
    Copy-Item -Path (Join-Path $InstalledGameRoot "$arch/FF9_Data/Managed/*.dll") -Destination $managed
    [IO.File]::WriteAllText((Join-Path $game "$arch/FF9.exe"), 'protected native executable')
    [IO.File]::WriteAllText((Join-Path $game "$arch/FF9_Data/asset.bin"), 'protected game asset')
}
[IO.File]::WriteAllText((Join-Path $game 'saved-game.dat'), 'protected save')
[IO.File]::WriteAllText((Join-Path $game 'Memoria.ini'), "[Graphics]`r`nAntiAliasing = 8`r`n[Accessibility]`r`nAnnounceIcons = False`r`nCageMinigameSkipEnabled = False`r`nNavigationUpdateInterval = 3`r`n")
[IO.File]::WriteAllText((Join-Path $game 'FF9_Launcher.exe'), 'original launcher')
function Snapshot { $result=@{}; Get-ChildItem -LiteralPath $game -Recurse -File | ForEach-Object { $result[$_.FullName.Substring($game.Length+1)] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }; return $result }
$before = Snapshot
# Reject a partial build that restamps HEAD while retaining old game binaries.
$wrongStage=Join-Path $owned 'incomplete-stage';[void](New-Item -ItemType Directory -Path $wrongStage -Force)
$buildManifest=Get-Content -LiteralPath (Join-Path $StageRoot 'build-manifest.json') -Raw | ConvertFrom-Json
$buildManifest.Mode='Tests';Write-FF9Json (Join-Path $wrongStage 'build-manifest.json') $buildManifest
$rejected=$false
try {RunScript 'Package-FF9Access.ps1' @{StageRoot=$wrongStage;PackageRoot=(Join-Path $owned 'partial-package');GameRoot=$game}}catch{$rejected=$_.Exception.Message.Contains('complete All/Both')}
Require $rejected 'Partial build provenance was accepted';Write-Host 'PASS PartialBuildProvenanceRejected'
# A complete build stamp must also match the actual staged payload bytes.
$buildManifest.Mode='All';Write-FF9Json (Join-Path $wrongStage 'build-manifest.json') $buildManifest
$wrongManaged=Join-Path $wrongStage 'Game/x86';[void](New-Item -ItemType Directory -Path $wrongManaged -Force)
$coreBytes=[IO.File]::ReadAllBytes((Join-Path $StageRoot 'Game/x86/Assembly-CSharp.dll'))
$coreBytes[$coreBytes.Length-1]=$coreBytes[$coreBytes.Length-1] -bxor 1
[IO.File]::WriteAllBytes((Join-Path $wrongManaged 'Assembly-CSharp.dll'),$coreBytes)
$rejected=$false
try {RunScript 'Package-FF9Access.ps1' @{StageRoot=$wrongStage;PackageRoot=(Join-Path $owned 'changed-package');GameRoot=$game}}catch{$rejected=$_.Exception.Message.Contains('differs from the complete build manifest')}
Require $rejected 'Changed staged bytes were accepted';Write-Host 'PASS StagedPayloadHashMismatchRejected'
RunScript 'Package-FF9Access.ps1' @{StageRoot=$StageRoot;PackageRoot=$package;GameRoot=$game}
$manifestPath = Join-Path $package 'package-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Require ($manifest.ForkId -eq 'ff9-access-local') 'Missing custom build identity'
foreach ($arch in @('x86','x64')) { Require ($null -ne ($manifest.Files | Where-Object {$_.Path -eq "$arch/prism.dll"})) "Missing $arch speech" }
# PE32 alone also describes AnyCPU; a native x86 speech package needs an x86 process.
$launcherPath=Join-Path $package 'Payload/FF9_Launcher.exe'
$launcherBytes=[IO.File]::ReadAllBytes($launcherPath)
$anyCpuPath=Join-Path $StageRoot 'LauncherSpeech/Memoria.ScreenReader.dll'
Require (((Get-FF9PE $anyCpuPath).CorFlags -band 2) -eq 0) 'Architecture regression fixture is not AnyCPU'
Copy-Item -LiteralPath $anyCpuPath -Destination $launcherPath -Force
($manifest.Files | Where-Object {$_.Path -eq 'FF9_Launcher.exe'}).SHA256=Get-FF9Hash $launcherPath
Write-FF9Json $manifestPath $manifest
$rejected=$false
try {[void](Assert-FF9Package $package $game)}catch{$rejected=$_.Exception.Message.Contains('x86 managed process architecture mismatch')}
Require $rejected 'AnyCPU launcher was accepted with native x86 speech dependencies'
[IO.File]::WriteAllBytes($launcherPath,$launcherBytes)
($manifest.Files | Where-Object {$_.Path -eq 'FF9_Launcher.exe'}).SHA256=Get-FF9Hash $launcherPath
Write-FF9Json $manifestPath $manifest
Write-Host 'PASS AnyCpuLauncherRejected'
$bad = Join-Path $package 'Payload/x86/prism.dll'
$originalPrism = [IO.File]::ReadAllBytes($bad)
$bytes = [byte[]]$originalPrism.Clone()
$at = [Text.Encoding]::ASCII.GetString($bytes).IndexOf('KERNEL32.dll',[StringComparison]::OrdinalIgnoreCase)
Require ($at -ge 0) 'Native import fixture has no kernel import'
[Text.Encoding]::ASCII.GetBytes('missing9.dll').CopyTo($bytes,$at)
[IO.File]::WriteAllBytes($bad,$bytes)
($manifest.Files | Where-Object {$_.Path -eq 'x86/prism.dll'}).SHA256=(Get-FileHash -LiteralPath $bad -Algorithm SHA256).Hash
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$rejected=$false
try { RunScript 'Deploy-FF9Access.ps1' @{PackageRoot=$package;GameRoot=$game;BackupRoot=$backup} } catch { $rejected=$true }
Require $rejected 'Missing native import was allowed'
[IO.File]::WriteAllBytes($bad,$originalPrism)
($manifest.Files | Where-Object {$_.Path -eq 'x86/prism.dll'}).SHA256=(Get-FileHash -LiteralPath $bad -Algorithm SHA256).Hash
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host 'PASS MissingImportRejected'
$rejected=$false
try { RunScript 'Deploy-FF9Access.ps1' @{PackageRoot=$package;GameRoot=$game;BackupRoot=$backup;FailureAfterFiles=3} } catch { $rejected=$true }
Require $rejected 'Injected copy failure did not fail'
$failedManifest=Get-ChildItem -LiteralPath $backup -Filter 'backup-manifest.json' -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$failedData=Get-Content -LiteralPath $failedManifest.FullName -Raw | ConvertFrom-Json
Require ($failedData.Status -eq 'FailedAndRestored' -and @($failedData.Files | Where-Object {$_.Applied}).Count -ge 3) 'Copy-failure fixture did not reach three actual transaction writes'
$after=Snapshot
Require ($before.Count -eq $after.Count) 'Failure recovery left package files'
foreach ($key in $before.Keys) { Require ($after[$key] -eq $before[$key]) "Failure recovery changed $key" }
Write-Host 'PASS PartialCopyFailureRecovered'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $source 'tools/Deploy-FF9Access.ps1') -PackageRoot $package -GameRoot $game -BackupRoot $backup -InterruptionAfterFiles 3
Require ($LASTEXITCODE -eq 73) 'Abrupt interruption fixture did not terminate the child process'
$interrupted=Get-ChildItem -LiteralPath $backup -Filter 'backup-manifest.json' -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
RunScript 'Restore-FF9Access.ps1' @{GameRoot=$game;BackupManifest=$interrupted.FullName}
$after=Snapshot
Require ($before.Count -eq $after.Count) 'Abrupt-process recovery left package files'
foreach ($key in $before.Keys) { Require ($after[$key] -eq $before[$key]) "Abrupt-process recovery changed $key" }
Write-Host 'PASS AbruptProcessInterruptionRecovered'
foreach($checkpoint in @('IntentBeforeFirstFile','AfterSettings')) {
    if($checkpoint -eq 'IntentBeforeFirstFile') {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $source 'tools/Deploy-FF9Access.ps1') -PackageRoot $package -GameRoot $game -BackupRoot $backup -InterruptionBeforeFile 1
    } else {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $source 'tools/Deploy-FF9Access.ps1') -PackageRoot $package -GameRoot $game -BackupRoot $backup -InterruptionAfterFiles ($manifest.Files.Count+1)
    }
    Require ($LASTEXITCODE -eq 73) "Abrupt $checkpoint fixture did not terminate the child process"
    $interrupted=Get-ChildItem -LiteralPath $backup -Filter 'backup-manifest.json' -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    RunScript 'Restore-FF9Access.ps1' @{GameRoot=$game;BackupManifest=$interrupted.FullName}
    $after=Snapshot;Require ($before.Count -eq $after.Count) "Abrupt $checkpoint recovery left package files"
    foreach($key in $before.Keys){Require ($after[$key] -eq $before[$key]) "Abrupt $checkpoint recovery changed $key"}
    Write-Host "PASS Abrupt${checkpoint}Recovered"
}
RunScript 'Deploy-FF9Access.ps1' @{PackageRoot=$package;GameRoot=$game;BackupRoot=$backup}
$backupManifest = Get-ChildItem -LiteralPath $backup -Filter 'backup-manifest.json' -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
RunScript 'Test-FF9AccessInstall.ps1' @{GameRoot=$game;PackageManifest=$manifestPath;BackupManifest=$backupManifest.FullName}
$ini=[IO.File]::ReadAllText((Join-Path $game 'Memoria.ini'))
Require ($ini.Contains('AnnounceIcons = False') -and $ini.Contains('CageMinigameSkipEnabled = False') -and $ini.Contains('AntiAliasing = 8')) 'Existing preferences changed'
Require ($ini.Contains('NavigationIntervalMilliseconds = 3000')) 'Legacy guidance interval was lost'
Require ($ini.Contains('PreviousCategory=U|NextCategory=O')) 'Binding assignments were not stored in native INI-safe syntax'
Require ($ini.Contains('PreviousTarget=K|NextTarget=Semicolon|Status=L')) 'Shifted bottom-row defaults were not provisioned safely'
Write-Host 'PASS PreferencesAndLegacyIntervalPreserved'
RunScript 'Restore-FF9Access.ps1' @{GameRoot=$game;BackupManifest=$backupManifest.FullName}
$after=Snapshot
Require ($before.Count -eq $after.Count) 'Rollback left package files'
foreach ($key in $before.Keys) { Require ($after[$key] -eq $before[$key]) "Rollback changed $key" }
Write-Host 'PASS ByteForByteRollback'
RunScript 'Deploy-FF9Access.ps1' @{PackageRoot=$package;GameRoot=$game;BackupRoot=$backup}
$backupManifest = Get-ChildItem -LiteralPath $backup -Filter 'backup-manifest.json' -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Add-Content -LiteralPath (Join-Path $game 'Memoria.ini') '[UserOptions]'
Add-Content -LiteralPath (Join-Path $game 'Memoria.ini') 'PreserveMe = True'
[IO.File]::WriteAllText((Join-Path $game 'unrelated.txt'), 'preserve unrelated work')
RunScript 'Restore-FF9Access.ps1' @{GameRoot=$game;BackupManifest=$backupManifest.FullName}
Require ([IO.File]::ReadAllText((Join-Path $game 'Memoria.ini')).Contains('PreserveMe = True')) 'Rollback erased unrelated settings'
Require (Test-Path -LiteralPath (Join-Path $game 'unrelated.txt')) 'Rollback erased unrelated files'
Write-Host 'PASS UnrelatedChangesPreserved'
Write-Host 'PASS Deployment: native imports, both architectures, backup, failure recovery, settings, protected files, rollback'
Write-Host "Evidence retained: $owned"
exit 0
