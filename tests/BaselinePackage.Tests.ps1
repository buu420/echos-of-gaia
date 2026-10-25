param(
    [string]$LegacyPackageRoot='D:\FF9Access\packages\disc1-narration-20261024-a7182ba',
    [string]$PackageRoot,
    [string]$GameRoot='D:\SteamLibrary\steamapps\common\FINAL FANTASY IX'
)
$ErrorActionPreference='Stop'
$source=Split-Path $PSScriptRoot -Parent
. (Join-Path $source 'tools/FF9Access.Deployment.ps1')
function Require([bool]$condition,[string]$message){if(!$condition){throw $message}}
# A package that validates only on an already-modded PC must no longer pass.
$rejected=$false
try{[void](Assert-FF9Package $LegacyPackageRoot $GameRoot)}catch{$rejected=$_.Exception.Message -match 'baseline|support|Memoria.Scripts'}
Require $rejected 'Legacy package without source-matched Memoria baseline was accepted'
Write-Host 'PASS LegacyPackageWithoutBaselineRejected'
foreach($path in @('StreamingAssets/Scripts/Memoria.Scripts.dll','StreamingAssets/Data/Items/Items.csv','StreamingAssets/Shaders/BattleCommon.txt','FF9_Data/EmbeddedAsset/UI/Sprites/changetodisc2.png','x86/FF9_Data/Plugins/SaXAudio.dll','x64/FF9_Data/Plugins/soloud.dll','x64/FF9_Data/Plugins/JoyShockLibrary.dll')){Require (Test-FF9AllowedPayload $path) "Required community support was excluded: $path"}
foreach($path in @('x86/FF9.exe','x64/FF9_Data/Managed/UnityEngine.dll','x86/FF9_Data/Managed/Assembly-CSharp-firstpass.dll','x64/FF9_Data/Plugins/SdLib.dll','x86/FF9_Data/Plugins/unknown.dll','StreamingAssets/p0data2.bin','x86/XAudio2_9.dll')){Require (!(Test-FF9AllowedPayload $path)) "Original game or unknown native payload was accepted: $path"}
Write-Host 'PASS BaselineAllowlistPreservesOriginalGameFiles'
if(!$PackageRoot){Write-Host 'Baseline fixture checks require -PackageRoot with a complete release package';exit 0}
[void](Assert-FF9Package $PackageRoot $GameRoot)
$fixture=Join-Path (Split-Path $PackageRoot -Parent) ('baseline-fixture-'+[guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $fixture -Force)
# Hard links retain immutable large narration assets without duplicating their bytes.
foreach($file in Get-ChildItem -LiteralPath $PackageRoot -Recurse -File){
    $relative=$file.FullName.Substring($PackageRoot.Length+1);$target=Join-Path $fixture $relative
    [void](New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force)
    [void](New-Item -ItemType HardLink -Path $target -Target $file.FullName)
}
$manifestPath=Join-Path $fixture 'package-manifest.json'
# Replace links before changing fixture bytes. Never mutate a linked source file.
function WriteFixture([string]$Path,[byte[]]$Bytes){
    $temporary=$Path+'.fixture-'+[guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllBytes($temporary,$Bytes)
    [IO.File]::Replace($temporary,$Path,($Path+'.previous-'+[guid]::NewGuid().ToString('N')))
}
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
function SaveManifest{WriteFixture $manifestPath ([Text.Encoding]::UTF8.GetBytes(($manifest|ConvertTo-Json -Depth 12)))}
$missing='StreamingAssets/Data/Items/Items.csv'
$record=@($manifest.Files|Where-Object {$_.Path -ceq $missing})
Require ($record.Count -eq 1) 'Complete package omitted the required item database'
$manifest.Files=@($manifest.Files|Where-Object {$_.Path -cne $missing});SaveManifest
$rejected=$false;try{[void](Assert-FF9Package $fixture $GameRoot)}catch{$rejected=$_.Exception.Message -match 'baseline|support'}
Require $rejected 'Required source support omitted from package manifest was accepted'
$manifest.Files=@($manifest.Files)+$record;SaveManifest
Write-Host 'PASS OmittedBaselineSupportRejected'
$native='x86/FF9_Data/Plugins/SaXAudio.dll';$file=Join-Path $fixture ('Payload/'+$native)
$bytes=[IO.File]::ReadAllBytes($file);$changed=[byte[]]$bytes.Clone();$changed[$changed.Length-1]=$changed[$changed.Length-1] -bxor 1
WriteFixture $file $changed
($manifest.Files|Where-Object {$_.Path -ceq $native}).SHA256=Get-FF9Hash $file;SaveManifest
$rejected=$false;try{[void](Assert-FF9Package $fixture $GameRoot)}catch{$rejected=$_.Exception.Message -match 'pinned|support|baseline'}
Require $rejected 'Restamped native support differing from the pinned source was accepted'
WriteFixture $file $bytes
($manifest.Files|Where-Object {$_.Path -ceq $native}).SHA256=Get-FF9Hash $file;SaveManifest
Write-Host 'PASS RestampedNativeSupportTamperingRejected'
$scriptFile=Join-Path $fixture 'Payload/StreamingAssets/Scripts/Memoria.Scripts.dll'
$scriptBytes=[IO.File]::ReadAllBytes($scriptFile)
WriteFixture $scriptFile ([IO.File]::ReadAllBytes((Join-Path $fixture 'Payload/x86/FF9_Data/Managed/Assembly-CSharp.dll')))
($manifest.Files|Where-Object {$_.Path -ceq 'StreamingAssets/Scripts/Memoria.Scripts.dll'}).SHA256=Get-FF9Hash $scriptFile;SaveManifest
$rejected=$false;try{[void](Assert-FF9Package $fixture $GameRoot)}catch{$rejected=$_.Exception.Message -match 'Memoria.Scripts|shared scripts'}
Require $rejected 'Architecture-specific game assembly masquerading as shared scripts was accepted'
WriteFixture $scriptFile $scriptBytes
($manifest.Files|Where-Object {$_.Path -ceq 'StreamingAssets/Scripts/Memoria.Scripts.dll'}).SHA256=Get-FF9Hash $scriptFile;SaveManifest
Write-Host 'PASS ArchitectureSpecificScriptsRejected'
[void](Assert-FF9Package $fixture $GameRoot)
Write-Host "PASS BaselinePackage; immutable fixture retained: $fixture"
exit 0
