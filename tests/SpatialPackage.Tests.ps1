param([string]$EvidenceRoot='D:\FF9Access\research\2026-10-21\spatial-proximity-cues')
$ErrorActionPreference='Stop'
$source=Split-Path $PSScriptRoot -Parent
. (Join-Path $source 'tools/FF9Access.Deployment.ps1')
foreach($arch in @('x86','x64')){if(!(Test-FF9AllowedPayload "$arch/FF9_Data/Managed/Memoria.SpatialAudio.dll")){throw "Spatial dependency not allowed for $arch"}}
foreach($path in @('x64/FF9_Data/Plugins/SdLib.dll','x86/FF9_Data/Plugins/steam_api.dll','x64/FF9_Data/Plugins/unreviewed.dll')){if(Test-FF9AllowedPayload $path){throw 'Payload policy permits replacing an original or unreviewed game plugin'}}
Write-Host 'PASS Spatial native dependencies allowed while original and unreviewed game plugins remain protected'
$root=Join-Path $source 'assets/navigation'
[void](Assert-FF9SpatialRecordings $root)
$fixture=Join-Path $EvidenceRoot ('asset-fixture-'+[guid]::NewGuid().ToString('N'));[void](New-Item -ItemType Directory -Path $fixture)
foreach($file in @('proximity-manifest.json','field-npc.wav','field-npc-original.wav','field-interactable.wav','field-interactable-original.wav')){Copy-Item -LiteralPath (Join-Path $root $file) -Destination $fixture}
$cue=Join-Path $fixture 'field-npc.wav';$data=[IO.File]::ReadAllBytes($cue);$data[100]=$data[100] -bxor 1;[IO.File]::WriteAllBytes($cue,$data)
$rejected=$false;try{[void](Assert-FF9SpatialRecordings $fixture)}catch{$rejected=$_.Exception.Message.Contains('recording differs')}
if(!$rejected){throw 'Changed supplied recording was accepted'}
Write-Host 'PASS Tampered supplied recording rejected'
Copy-Item -LiteralPath (Join-Path $root 'field-npc.wav') -Destination $cue -Force
$catalog=Get-Content (Join-Path $fixture 'proximity-manifest.json') -Raw | ConvertFrom-Json
$catalog.cues[1].kind='npc';Write-FF9Json (Join-Path $fixture 'proximity-manifest.json') $catalog
$rejected=$false;try{[void](Assert-FF9SpatialRecordings $fixture)}catch{$rejected=$_.Exception.Message.Contains('category')}
if(!$rejected){throw 'Ambiguous sound categories were accepted'}
Write-Host 'PASS Duplicate category assignment rejected'
exit 0
