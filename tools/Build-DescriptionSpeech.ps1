param([Parameter(Mandatory=$true)][string]$OutRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FF9Access.Paths.ps1')
$sourceRoot=Resolve-FF9Path (Split-Path $PSScriptRoot -Parent)
$OutRoot=Resolve-FF9Path $OutRoot
if($OutRoot.Equals($sourceRoot,[StringComparison]::OrdinalIgnoreCase) -or
    $OutRoot.StartsWith($sourceRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or
    $sourceRoot.StartsWith($OutRoot+'\',[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Description speech output must be separate from the source checkout'
}
function Assert-DescriptionStage([string]$Path) {
    for($parent=$Path;$parent;$parent=Split-Path $parent -Parent) {
        if(Test-Path -LiteralPath $parent) {
            if((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Reparse point in output path: $parent"}
            foreach($gameFile in @('FF9.exe','x86/FF9.exe','x64/FF9.exe')) {
                if(Test-Path -LiteralPath (Join-Path $parent $gameFile)){throw "Output is inside a game installation: $parent"}
            }
        }
    }
}
Assert-DescriptionStage $OutRoot
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if(!(Test-Path -LiteralPath $vswhere)){throw 'Visual Studio C++ Build Tools are required'}
$installation=(& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1)
if(!$installation){throw 'Visual Studio C++ Build Tools are required'}
$vcvars=Join-Path $installation 'VC/Auxiliary/Build/vcvarsall.bat'
$source=Join-Path $sourceRoot 'native/descriptions/description-speech.cpp'
[void](New-Item -ItemType Directory -Path $OutRoot -Force)
$definition=Join-Path $OutRoot 'Memoria.DescriptionSpeech.def'
@'
LIBRARY Memoria.DescriptionSpeech.dll
EXPORTS
    FF9AD_Create
    FF9AD_Speak
    FF9AD_Pause
    FF9AD_Resume
    FF9AD_Stop
    FF9AD_Playing
    FF9AD_Free
'@ | Set-Content -LiteralPath $definition -Encoding ASCII
$files=@()
foreach($architecture in @('x86','x64')) {
    $output=Join-Path $OutRoot $architecture
    Assert-DescriptionStage $output
    [void](New-Item -ItemType Directory -Path $output -Force)
    $script=Join-Path $output 'build.cmd'
    $library=Join-Path $output 'Memoria.DescriptionSpeech.dll'
    @"
@echo off
call "$vcvars" $architecture >nul
if errorlevel 1 exit /b 1
cd /d "$output"
cl /nologo /W4 /WX /O2 /EHsc /MT /LD /std:c++14 /Fe:"$library" "$source" /link /DEF:"$definition" Ole32.lib Uuid.lib
exit /b %errorlevel%
"@ | Set-Content -LiteralPath $script -Encoding ASCII
    & $env:ComSpec /d /v:off /c ('"'+$script+'"') 2>&1 | Tee-Object -FilePath (Join-Path $output 'build.log')
    if($LASTEXITCODE -ne 0){throw "Description speech build failed: $architecture"}
    $bytes=[IO.File]::ReadAllBytes($library)
    $offset=[BitConverter]::ToInt32($bytes,60)
    $machine=[BitConverter]::ToUInt16($bytes,$offset+4)
    $expected=if($architecture -eq 'x86'){0x14c}else{0x8664}
    if($machine -ne $expected){throw "Description speech architecture mismatch: $architecture"}
    $files+=[pscustomobject]@{path=($architecture+'/Memoria.DescriptionSpeech.dll');architecture=$architecture;machine=$machine;bytes=$bytes.Length;sha256=(Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$sources=@('native/descriptions/description-speech.cpp','native/descriptions/README.md','tools/Build-DescriptionSpeech.ps1') | ForEach-Object {
    [pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $sourceRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$manifest=[pscustomobject]@{schema=1;abi='cdecl; UTF-16; signed HRESULT operations; Playing 1 busy / 0 done / negative HRESULT';runtime='Windows SAPI 5; static MSVC runtime';createdUtc=[DateTime]::UtcNow.ToString('o');sources=@($sources);files=@($files)}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutRoot 'manifest.json') -Encoding UTF8
Write-Output "PASS owned description speech x86+x64: $OutRoot"
