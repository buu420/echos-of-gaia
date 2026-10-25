param([Parameter(Mandatory=$true)][string]$GameRoot,[Parameter(Mandatory=$true)][string]$StageRoot,[switch]$Lifecycle,[switch]$ChildDomain,[switch]$Stress)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FF9Access.Paths.ps1')
$GameRoot=Resolve-FF9Path $GameRoot;$StageRoot=Resolve-FF9Path $StageRoot
Assert-FF9SeparatePaths -GameRoot $GameRoot -OtherRoot $StageRoot
$sourceRoot=Split-Path $PSScriptRoot -Parent
$probeSource=Join-Path $sourceRoot 'tests/FF9Access.MonoProbe'
$probeRoot=Join-Path $StageRoot ('FootstepAudioProbe-'+[guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $probeRoot)
$vcvars='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvarsall.bat'
$csc='C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe'
& $csc /nologo /target:exe ("/out:"+(Join-Path $probeRoot 'WorldFootstepAudioProbe.exe')) (Join-Path $probeSource 'WorldFootstepAudioProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Managed footstep audio probe did not compile'}
foreach($arch in @('x86','x64')) {
    $fixture=Join-Path $probeRoot $arch;[void](New-Item -ItemType Directory -Path $fixture)
    foreach($name in @('Assembly-CSharp.dll','Memoria.Prime.dll','Memoria.ScreenReader.dll')) {Copy-Item -LiteralPath (Join-Path $StageRoot "Game/$arch/$name") -Destination $fixture}
    $script=Join-Path $probeRoot "build-$arch.cmd"
    @"
@echo off
call "$vcvars" $arch >nul
if errorlevel 1 exit /b 1
cd /d "$probeRoot"
cl /nologo /O2 /EHsc /MT /Fe:MonoHost-$arch.exe "$probeSource\MonoHost.cpp"
exit /b %errorlevel%
"@ | Set-Content -LiteralPath $script -Encoding ASCII
    & $env:ComSpec /d /c ('"'+$script+'"')
    if($LASTEXITCODE -ne 0){throw "Native Mono audio host did not compile: $arch"}
    $arguments=@((Join-Path $GameRoot "$arch/FF9_Data/Mono/mono.dll"),(Join-Path $GameRoot "$arch/FF9_Data/Managed"),(Join-Path $GameRoot "$arch/FF9_Data/Mono/etc"),(Join-Path $probeRoot 'WorldFootstepAudioProbe.exe'),(Join-Path $fixture 'Assembly-CSharp.dll'),(Join-Path $GameRoot "$arch/FF9_Data/Plugins/SaXAudio.dll"),(Join-Path $sourceRoot 'assets/footsteps'),$arch)
    if($Stress){$arguments+='--stress'}elseif($Lifecycle){$arguments+='--lifecycle'}
    $output=Join-Path $probeRoot "$arch-output.log";$errors=Join-Path $probeRoot "$arch-errors.log"
    $previousChildProbeEnvironment=$env:FF9ACCESS_PROBE_CHILD_DOMAIN
    try {
        $env:FF9ACCESS_PROBE_CHILD_DOMAIN=if($ChildDomain){'1'}else{$null}
        $probe=Start-Process -FilePath (Join-Path $probeRoot "MonoHost-$arch.exe") -ArgumentList @($arguments | ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru -RedirectStandardOutput $output -RedirectStandardError $errors
    } finally {$env:FF9ACCESS_PROBE_CHILD_DOMAIN=$previousChildProbeEnvironment}
    if(!$probe.WaitForExit(45000)){if(!$probe.HasExited){$probe.Kill()};$probe.WaitForExit();Get-Content -LiteralPath $output;Get-Content -LiteralPath $errors;throw "Original Mono/native audio lifecycle hung: $arch; retained $probeRoot"}
    $probe.WaitForExit();Get-Content -LiteralPath $output;Get-Content -LiteralPath $errors
    if($probe.ExitCode -ne 0){throw "Original Mono/native footstep audio probe failed: $arch; retained $probeRoot"}
}
"PASS ExactMonoFootstepAudio x86+x64; evidence fixture $probeRoot"
exit 0
