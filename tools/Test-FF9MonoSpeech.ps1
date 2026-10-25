param([Parameter(Mandatory=$true)][string]$GameRoot,[Parameter(Mandatory=$true)][string]$StageRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FF9Access.Paths.ps1')
Assert-FF9SeparatePaths -GameRoot $GameRoot -OtherRoot $StageRoot
$GameRoot=Resolve-FF9Path $GameRoot;$StageRoot=Resolve-FF9Path $StageRoot
$sourceRoot=Split-Path $PSScriptRoot -Parent
$probeSource=Join-Path $sourceRoot 'tests/FF9Access.MonoProbe'
$probeRoot=Join-Path $StageRoot ('MonoProbe-'+[guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $probeRoot)
$vcvars='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvarsall.bat'
$csc='C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe'
if(!(Test-Path -LiteralPath $vcvars) -or !(Test-Path -LiteralPath $csc)){throw 'MSVC and .NET 3.5 compiler required for the exact-runtime probe'}
& $csc /nologo /target:exe ("/out:"+(Join-Path $probeRoot 'SpeechProbe.exe')) (Join-Path $probeSource 'SpeechProbe.cs') (Join-Path $probeSource 'WorldPlanningProbe.cs') (Join-Path $probeSource 'AudioDescriptionProbe.cs') (Join-Path $probeSource 'SpatialAudioProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Managed Mono probe did not compile'}
foreach($arch in @('x86','x64')) {
    $fixture=Join-Path $probeRoot $arch;[void](New-Item -ItemType Directory -Path $fixture)
    Copy-Item -LiteralPath (Join-Path $StageRoot "Game/$arch/Memoria.ScreenReader.dll") -Destination $fixture
    Copy-Item -LiteralPath (Join-Path $StageRoot "Game/$arch/Assembly-CSharp.dll") -Destination $fixture
    Copy-Item -LiteralPath (Join-Path $StageRoot "DescriptionSpeech/$arch/Memoria.DescriptionSpeech.dll") -Destination $fixture
    Copy-Item -LiteralPath (Join-Path $sourceRoot "native/spatial/$arch/Memoria.SpatialAudio.dll") -Destination $fixture
    $spatialAssets=Join-Path $fixture 'navigation';[void](New-Item -ItemType Directory -Path $spatialAssets)
    foreach($name in @('field-exit.wav','field-npc.wav','field-interactable.wav')){Copy-Item -LiteralPath (Join-Path $sourceRoot "assets/navigation/$name") -Destination $spatialAssets}
    foreach($name in @('prism.dll','Tolk.dll',"nvdaControllerClient$(if($arch -eq 'x86'){'32'}else{'64'}).dll")) {Copy-Item -LiteralPath (Join-Path $sourceRoot "native/speech/$arch/$name") -Destination $fixture}
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
    if($LASTEXITCODE -ne 0){throw "Native Mono host did not compile: $arch"}
    & (Join-Path $probeRoot "MonoHost-$arch.exe") (Join-Path $GameRoot "$arch/FF9_Data/Mono/mono.dll") (Join-Path $GameRoot "$arch/FF9_Data/Managed") (Join-Path $GameRoot "$arch/FF9_Data/Mono/etc") (Join-Path $probeRoot 'SpeechProbe.exe') (Join-Path $fixture 'Memoria.ScreenReader.dll') $arch
    if($LASTEXITCODE -ne 0){throw "Exact installed Mono speech probe failed: $arch"}
}
"PASS ExactMonoSpeech x86+x64; evidence fixture $probeRoot"
exit 0
