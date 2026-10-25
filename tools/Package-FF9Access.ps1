param([Parameter(Mandatory=$true)][string]$StageRoot, [Parameter(Mandatory=$true)][string]$PackageRoot, [Parameter(Mandatory=$true)][string]$GameRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FF9Access.Deployment.ps1')
$StageRoot=Resolve-FF9Path $StageRoot;$PackageRoot=Resolve-FF9Path $PackageRoot;$GameRoot=Resolve-FF9Path $GameRoot
Assert-FF9SeparatePaths $GameRoot $StageRoot
Assert-FF9SeparatePaths $GameRoot $PackageRoot
Assert-FF9SeparatePaths $StageRoot $PackageRoot
if ((Test-Path -LiteralPath $PackageRoot) -and @(Get-ChildItem -LiteralPath $PackageRoot -Force).Count -ne 0) { throw 'Use an empty package directory' }
$sourceRoot=Split-Path $PSScriptRoot -Parent
$buildManifest=Get-Content -LiteralPath (Join-Path $StageRoot 'build-manifest.json') -Raw | ConvertFrom-Json
$sourceCommit=& git -C $sourceRoot -c maintenance.auto=false rev-parse HEAD
if($buildManifest.SourceCommit -ne $sourceCommit){throw 'Staged binaries were built from another source commit; rebuild before packaging'}
if($buildManifest.Mode -ne 'All' -or @($buildManifest.Architectures).Count -ne 2 -or $buildManifest.Architectures -notcontains 'x86' -or $buildManifest.Architectures -notcontains 'x64'){throw 'Packaging requires a complete All/Both staged build'}
$productChanges=& git -C $sourceRoot -c maintenance.auto=false status --porcelain --untracked-files=normal -- Assembly-CSharp Memoria.Scripts Memoria.Patcher/StreamingAssets/Data Memoria.Patcher/StreamingAssets/Shaders Memoria.Patcher/FF9_Data Memoria.ScreenReader Memoria.XInputDotNetPure Memoria.Prime Memoria.Launcher Memoria.MSBuild assets docs/accessibility native/descriptions native/spatial native/memoria-support UnityEngine.UI_LICENSE tools/Build-FF9Access.ps1 tools/Build-DescriptionSpeech.ps1 tools/Package-FF9Access.ps1 tools/FF9Access.Deployment.ps1
if($productChanges){throw 'Commit and review product changes, then rebuild before packaging'}
$nativeRoot=Join-Path $sourceRoot 'native/speech'
$speechManifest=Get-Content -LiteralPath (Join-Path $nativeRoot 'manifest.json') -Raw | ConvertFrom-Json
foreach ($file in $speechManifest.Files) {
    $path=Get-FF9OwnedPath $nativeRoot $file.Path
    if ((Get-FF9Hash $path) -ne $file.SHA256) { throw "Pinned speech dependency changed: $($file.Path)" }
}
$files=New-Object 'System.Collections.Generic.List[object]'
function AddPayload([string]$From,[string]$Relative,[string]$Role,[string]$Architecture) {
    if (!(Test-Path -LiteralPath $From -PathType Leaf)) { throw "Missing staged payload: $From" }
    if (!(Test-FF9AllowedPayload $Relative)) { throw "Path is outside payload policy: $Relative" }
    if($From.StartsWith($StageRoot+'\',[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetExtension($From) -in '.dll','.exe') {
        $stagedRelative=$From.Substring($StageRoot.Length+1)
        $record=@($buildManifest.Files | Where-Object {$_.Path -eq $stagedRelative})
        if($record.Count -ne 1 -or $record[0].SHA256 -ne (Get-FF9Hash $From)){throw "Staged payload differs from the complete build manifest: $stagedRelative"}
    }
    $to=Get-FF9OwnedPath $PackageRoot ('Payload/'+$Relative)
    [void](New-Item -ItemType Directory -Path (Split-Path $to -Parent) -Force)
    Copy-Item -LiteralPath $From -Destination $to
    $files.Add([pscustomobject]@{Path=$Relative;Role=$Role;Architecture=$Architecture;SHA256=(Get-FF9Hash $to);Bytes=(Get-Item -LiteralPath $to).Length})
}
# The support manifest lists the complete reviewed public default corpus and only the
# three named community native libraries, with both architectures pinned independently.
$supportRoot=Join-Path $sourceRoot 'native/memoria-support'
$support=Get-Content -LiteralPath (Join-Path $supportRoot 'manifest.json') -Raw|ConvertFrom-Json
if($support.Schema -ne 1 -or $support.Id -ne 'ff9-memoria-support'){throw 'Invalid pinned Memoria baseline support source manifest'}
foreach($record in $support.Files){
    $path=Get-FF9OwnedPath $sourceRoot $record.SourcePath
    if((Get-Item -LiteralPath $path).Length -ne $record.Bytes -or (Get-FF9Hash $path) -ne $record.SHA256){throw "Pinned Memoria support source changed: $($record.SourcePath)"}
    AddPayload $path $record.Path 'MemoriaBaselineSupport' $record.Architecture
}
AddPayload (Join-Path $supportRoot 'manifest.json') 'accessibility/memoria-support/manifest.json' 'MemoriaBaselineManifest' ''
AddPayload (Join-Path $StageRoot 'Scripts/Memoria.Scripts.dll') 'StreamingAssets/Scripts/Memoria.Scripts.dll' 'MemoriaScripts' 'MSIL'
foreach ($arch in @('x86','x64')) {
    foreach ($name in @('Assembly-CSharp','Memoria.Prime','Memoria.ScreenReader','UnityEngine.UI','XInputDotNetPure')) {
        AddPayload (Join-Path $StageRoot "Game/$arch/$name.dll") "$arch/FF9_Data/Managed/$name.dll" 'GameManaged' $arch
    }
    AddPayload (Join-Path $StageRoot "DescriptionSpeech/$arch/Memoria.DescriptionSpeech.dll") "$arch/FF9_Data/Managed/Memoria.DescriptionSpeech.dll" 'DescriptionSpeechNative' $arch
    $spatialRoot=Join-Path $sourceRoot 'native/spatial'
    $spatial=Get-Content -LiteralPath (Join-Path $spatialRoot 'manifest.json') -Raw | ConvertFrom-Json
    $record=@($spatial.files | Where-Object {$_.architecture -eq $arch})
    if($spatial.schema -ne 1 -or $spatial.version -ne '4.6.1' -or $record.Count -ne 1 -or $record[0].file -cne "$arch/Memoria.SpatialAudio.dll"){throw 'Invalid spatial dependency manifest'}
    $spatialDll=Get-FF9OwnedPath $spatialRoot $record[0].file
    if((Get-FF9Hash $spatialDll) -ne $record[0].sha256 -or (Get-Item $spatialDll).Length -ne $record[0].bytes){throw 'Pinned spatial dependency differs'}
    AddPayload $spatialDll "$arch/FF9_Data/Managed/Memoria.SpatialAudio.dll" 'SpatialAudioNative' $arch
    foreach ($name in @('Newtonsoft.Json.dll','System.Runtime.Serialization.dll')) {
        $path=Join-Path $StageRoot "Game/$arch/$name"
        if (Test-Path -LiteralPath $path) { AddPayload $path "$arch/FF9_Data/Managed/$name" 'ManagedDependency' 'MSIL' }
    }
    $bits=if ($arch -eq 'x86') {'32'} else {'64'}
    foreach ($name in @('prism.dll','Tolk.dll',"nvdaControllerClient$bits.dll")) {
        $path=Join-Path $nativeRoot "$arch/$name"
        AddPayload $path "$arch/$name" 'SpeechNative' $arch
        AddPayload $path "$arch/FF9_Data/Managed/$name" 'SpeechNative' $arch
    }
}
AddPayload (Join-Path $StageRoot 'Launcher/Memoria.Launcher.exe') 'FF9_Launcher.exe' 'Launcher' 'x86'
AddPayload (Join-Path $StageRoot 'Launcher/Memoria.Launcher.exe.config') 'FF9_Launcher.exe.config' 'LauncherConfig' ''
foreach ($name in @('Memoria.ScreenReader.dll','Microsoft.Bcl.AsyncInterfaces.dll','SharpCompress.dll','System.Buffers.dll','System.Memory.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','System.Text.Encoding.CodePages.dll','System.Threading.Tasks.Extensions.dll')) {
    AddPayload (Join-Path $StageRoot "Launcher/$name") $name 'LauncherDependency' 'MSIL'
}
foreach ($name in @('prism.dll','Tolk.dll','nvdaControllerClient32.dll')) { AddPayload (Join-Path $nativeRoot "x86/$name") $name 'SpeechNative' 'x86' }
foreach ($file in Get-ChildItem -LiteralPath $nativeRoot -File -Recurse) {
    $relative=$file.FullName.Substring($nativeRoot.Length+1).Replace('\','/')
    if ($relative -notmatch '^x(86|64)/') { AddPayload $file.FullName ('accessibility/speech/'+$relative) 'NoticeOrSource' '' }
}
AddPayload (Join-Path $sourceRoot 'LICENSE') 'accessibility/LICENSE' 'Notice' ''
foreach($name in @('description-speech.cpp','README.md')){AddPayload (Join-Path $sourceRoot "native/descriptions/$name") ("accessibility/description-speech/"+$name) 'NoticeOrSource' ''}
$descriptionRoot=Join-Path $sourceRoot 'assets/audio-descriptions'
$descriptions=Assert-FF9DescriptionPack $descriptionRoot (Join-Path $sourceRoot 'docs/accessibility/audio-descriptions.json')
AddPayload (Join-Path $descriptionRoot 'manifest.json') 'accessibility/audio-descriptions/manifest.json' 'AudioDescriptionCatalog' ''
foreach($clip in $descriptions.clips){AddPayload (Get-FF9OwnedPath $descriptionRoot $clip.file) ('accessibility/audio-descriptions/'+$clip.file) 'AudioDescriptionRecording' ''}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'assets/footsteps') -File) { AddPayload $file.FullName ('accessibility/footsteps/'+$file.Name) 'WorldFootstepAsset' '' }
$exitAudio=Get-Content -LiteralPath (Join-Path $sourceRoot 'assets/navigation/manifest.json') -Raw | ConvertFrom-Json
foreach($record in @($exitAudio.original,$exitAudio.playback)) {
    $path=Get-FF9OwnedPath (Join-Path $sourceRoot 'assets/navigation') $record.file
    if((Get-FF9Hash $path) -ne $record.sha256 -or (Get-Item -LiteralPath $path).Length -ne $record.bytes){throw 'Exit recording differs from its pinned source manifest'}
    AddPayload $path ('accessibility/navigation/'+$record.file) 'ExitCueRecording' ''
}
AddPayload (Join-Path $sourceRoot 'assets/navigation/manifest.json') 'accessibility/navigation/manifest.json' 'ExitCueManifest' ''
$proximity=Assert-FF9SpatialRecordings (Join-Path $sourceRoot 'assets/navigation')
foreach($cue in $proximity.cues){foreach($record in @($cue.original,$cue.playback)){AddPayload (Get-FF9OwnedPath (Join-Path $sourceRoot 'assets/navigation') $record.file) ('accessibility/navigation/'+$record.file) 'ProximityCueRecording' ''}}
AddPayload (Join-Path $sourceRoot 'assets/navigation/proximity-manifest.json') 'accessibility/navigation/proximity-manifest.json' 'ProximityCueManifest' ''
foreach($name in @('manifest.json','LICENSE.md','README.md')){AddPayload (Join-Path $sourceRoot "native/spatial/$name") ("accessibility/spatial/"+$name) 'SpatialAudioNotice' ''}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'docs/accessibility') -File | Where-Object {$_.Extension -in '.md','.csv','.json'}) { AddPayload $file.FullName ('accessibility/docs/'+$file.Name) 'Documentation' '' }
$manifest=[pscustomobject]@{
    Schema=1;ForkId='ff9-access-local';AccessibilityApi=1;CreatedUtc=[DateTime]::UtcNow.ToString('o')
    SourceCommit=$sourceCommit
    StagedBuild=$buildManifest
    Policy='Explicit payload and pinned public Memoria support only; preserve original Unity executables, firstpass, UnityEngine, native game plugins, packed game assets, saves, and existing preferences. No stock patcher, Windows OS DLLs, or stale release payload.'
    Files=$files.ToArray()
}
Write-FF9Json (Join-Path $PackageRoot 'package-manifest.json') $manifest
[void](Assert-FF9Package $PackageRoot $GameRoot)
Write-Host "PASS Package: $($files.Count) verified files; x86+x64; source $($manifest.SourceCommit)"
