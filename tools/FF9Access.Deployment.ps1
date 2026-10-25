. (Join-Path $PSScriptRoot 'FF9Access.Paths.ps1')

function Get-FF9OwnedPath([string]$Root,[string]$Relative) {
    $Root = Resolve-FF9Path $Root
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or $Relative.Contains(':')) { throw "Invalid relative path: $Relative" }
    $result = [IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if (!$result.StartsWith($Root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw "Path escaped the explicit root: $Relative" }
    $parent=$result
    while ($parent -and $parent.Length -ge $Root.Length) {
        if (Test-Path -LiteralPath $parent) {
            if ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point is not an owned installation path: $parent" }
        }
        $parent=Split-Path $parent -Parent
    }
    return $result
}
function Assert-FF9NotRunning([string]$GameRoot) {
    foreach ($process in @(Get-Process FF9,Unity,FF9_Launcher,Memoria.Launcher -ErrorAction SilentlyContinue)) {
        $path=$process.Path
        if (!$path -or $path.StartsWith((Resolve-FF9Path $GameRoot)+'\',[StringComparison]::OrdinalIgnoreCase)) { throw "Close the game and launcher before deployment or rollback: $($process.ProcessName) ($($process.Id))" }
    }
}
function Get-FF9Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Write-FF9AtomicBytes([string]$Path,[byte[]]$Bytes) {
    $temporary=$Path+'.ff9access-'+[guid]::NewGuid().ToString('N')+'.tmp'
    $created=$false
    try {
        $stream=New-Object IO.FileStream($temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None,4096,[IO.FileOptions]::WriteThrough)
        $created=$true
        try {$stream.Write($Bytes,0,$Bytes.Length);$stream.Flush($true)} finally {$stream.Dispose()}
        if ([IO.File]::Exists($Path)) {[IO.File]::Replace($temporary,$Path,[System.Management.Automation.Language.NullString]::Value)} else {[IO.File]::Move($temporary,$Path)}
    } finally {if ($created -and [IO.File]::Exists($temporary)) {Remove-Item -LiteralPath $temporary -Force}}
}
function Copy-FF9Atomic([string]$Source,[string]$Path,[string]$Temporary) {
    $created=$false
    try {
        $target=New-Object IO.FileStream($Temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None,65536,[IO.FileOptions]::WriteThrough)
        $created=$true
        try {$inputFile=[IO.File]::OpenRead($Source);try {$inputFile.CopyTo($target);$target.Flush($true)} finally {$inputFile.Dispose()}} finally {$target.Dispose()}
        if ([IO.File]::Exists($Path)) {[IO.File]::Replace($Temporary,$Path,[System.Management.Automation.Language.NullString]::Value)} else {[IO.File]::Move($Temporary,$Path)}
    } finally {if ($created -and [IO.File]::Exists($Temporary)) {Remove-Item -LiteralPath $Temporary -Force}}
}
function Write-FF9Json([string]$Path,$Value) { Write-FF9AtomicBytes $Path ([Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Depth 12))) }

function Get-FF9PE([string]$Path) {
    $bytes=[IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 128 -or $bytes[0] -ne 77 -or $bytes[1] -ne 90) { throw "Invalid PE: $Path" }
    $pe=[BitConverter]::ToInt32($bytes,60)
    if ($pe -lt 64 -or $pe+256 -ge $bytes.Length -or [BitConverter]::ToInt32($bytes,$pe) -ne 17744) { throw "Invalid PE header: $Path" }
    $machine=[BitConverter]::ToUInt16($bytes,$pe+4)
    $count=[BitConverter]::ToUInt16($bytes,$pe+6)
    $optionalSize=[BitConverter]::ToUInt16($bytes,$pe+20)
    $optional=$pe+24
    $magic=[BitConverter]::ToUInt16($bytes,$optional)
    if ($magic -ne 267 -and $magic -ne 523) { throw "Unknown PE optional header: $Path" }
    $directory=$optional+$(if ($magic -eq 267) {96} else {112})
    $sections=@()
    for ($index=0;$index -lt $count;$index++) {
        $at=$optional+$optionalSize+$index*40
        if ($at+40 -gt $bytes.Length) { throw 'Truncated PE sections' }
        $sections += [pscustomobject]@{Address=[BitConverter]::ToUInt32($bytes,$at+12);Size=[Math]::Max([BitConverter]::ToUInt32($bytes,$at+8),[BitConverter]::ToUInt32($bytes,$at+16));Offset=[BitConverter]::ToUInt32($bytes,$at+20)}
    }
    function Offset([uint32]$Rva) {
        foreach ($section in $sections) { if ($Rva -ge $section.Address -and $Rva -lt ([uint64]$section.Address+$section.Size)) {
            $at=[long]$section.Offset+$Rva-$section.Address
            if ($at -lt 0 -or $at+4 -gt $bytes.Length) { throw 'PE RVA outside file' }; return [int]$at
        } }; throw 'PE RVA not mapped'
    }
    function NameAt([uint32]$Rva) {
        $at=Offset $Rva; $end=$at
        while ($end -lt $bytes.Length -and $bytes[$end] -ne 0 -and $end-$at -lt 512) { $end++ }
        if ($end -ge $bytes.Length -or $bytes[$end] -ne 0) { throw 'Invalid PE import name' }
        [Text.Encoding]::ASCII.GetString($bytes,$at,$end-$at)
    }
    $imports=@()
    $rva=[BitConverter]::ToUInt32($bytes,$directory+8)
    if ($rva -ne 0) {
        $at=Offset $rva
        for ($index=0;$index -lt 512;$index++) {
            if ($at+20 -gt $bytes.Length) { throw 'Truncated import table' }
            $nameRva=[BitConverter]::ToUInt32($bytes,$at+12)
            if ($nameRva -eq 0) { break }; $imports += NameAt $nameRva; $at+=20
        }
    }
    $clr=[BitConverter]::ToUInt32($bytes,$directory+14*8)
    $flags=if ($clr -ne 0) {[BitConverter]::ToUInt32($bytes,(Offset $clr)+16)} else {0}
    [pscustomobject]@{Machine=$machine;Managed=($clr -ne 0);CorFlags=$flags;Imports=$imports}
}

function Test-FF9AllowedPayload([string]$Path) {
    if ($Path.Contains('\') -or $Path.Split('/') -contains '..' -or $Path.Split('/') -contains '.') { return $false }
    if ($Path -match '^accessibility/[A-Za-z0-9_./-]+$') { return $true }
    if ($Path -eq 'StreamingAssets/Scripts/Memoria.Scripts.dll') { return $true }
    if ($Path -match '^StreamingAssets/Data/[A-Za-z0-9_./-]+\.(csv|png|seq|sfxmodel|txt)$') { return $true }
    if ($Path -match '^StreamingAssets/Shaders/[A-Za-z0-9_./ ()~-]+\.txt$') { return $true }
    if ($Path -match '^FF9_Data/EmbeddedAsset/[A-Za-z0-9_./-]+\.(png|mpc)$') { return $true }
    if ($Path -match '^x(86|64)/FF9_Data/Plugins/(SaXAudio.dll|soloud.dll|JoyShockLibrary.dll)$') { return $true }
    if ($Path -in @('FF9_Launcher.exe','FF9_Launcher.exe.config','Memoria.ScreenReader.dll','prism.dll','Tolk.dll','nvdaControllerClient32.dll','Microsoft.Bcl.AsyncInterfaces.dll','SharpCompress.dll','System.Buffers.dll','System.Memory.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','System.Text.Encoding.CodePages.dll','System.Threading.Tasks.Extensions.dll')) { return $true }
    if ($Path -match '^x(86|64)/(prism.dll|Tolk.dll|nvdaControllerClient(32|64).dll)$') { return $true }
    return $Path -match '^x(86|64)/FF9_Data/Managed/(Assembly-CSharp.dll|Memoria.Prime.dll|Memoria.ScreenReader.dll|UnityEngine.UI.dll|XInputDotNetPure.dll|Newtonsoft.Json.dll|System.Runtime.Serialization.dll|Memoria.DescriptionSpeech.dll|Memoria.SpatialAudio.dll|prism.dll|Tolk.dll|nvdaControllerClient(32|64).dll)$'
}

function Assert-FF9SpatialRecordings([string]$Root) {
    $catalog=Get-Content -LiteralPath (Get-FF9OwnedPath $Root 'proximity-manifest.json') -Raw | ConvertFrom-Json
    if($catalog.schema -ne 1 -or @($catalog.cues).Count -ne 2){throw 'Invalid spatial recording category catalog'}
    $seen=@{}
    foreach($cue in $catalog.cues){
        if($cue.kind -notin @('npc','interactable') -or $seen.ContainsKey($cue.kind)){throw 'Invalid or duplicate spatial recording category'}
        $seen[$cue.kind]=$true
        foreach($type in @('original','playback')){
            $record=$cue.$type;$expected='field-'+$cue.kind+$(if($type -eq 'original'){'-original.wav'}else{'.wav'})
            if($record.file -cne $expected){throw 'Spatial recording category filename mismatch'}
            $file=Get-FF9OwnedPath $Root $record.file
            if(!(Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -ne $record.bytes -or (Get-FF9Hash $file) -ne $record.sha256){throw 'Spatial recording differs from its confirmed source manifest'}
        }
    }
    return $catalog
}

function Assert-FF9DescriptionBankBudget([string[]]$Ids,[hashtable]$Clips) {
    $unique=@{};[long]$total=0
    foreach($id in $Ids){if(!$Clips.ContainsKey($id)){throw "Unknown description bank clip: $id"};if(!$unique.ContainsKey($id)){$unique[$id]=$true;$total+=$Clips[$id].bytes}}
    if($unique.Count -gt 512 -or $total -gt 33554432){throw 'Runtime description bank exceeds its memory budget'}
}

function Assert-FF9DescriptionFieldBanks($FieldBanks,$Films,[hashtable]$Clips) {
    if($Clips.Count -gt 4096){throw 'Public description pack exceeds 4096 clips'}
    if($null -eq $FieldBanks){$FieldBanks=@()}
    if($FieldBanks -isnot [array]){throw 'fieldBanks must be an array'}
    $movieIds=@{};$assigned=@{};$fields=@{};$bankIds=@{}
    foreach($film in $Films){foreach($cue in $film.cues){$movieIds[$cue.id]=$true}}
    foreach($bank in $FieldBanks){
        foreach($property in $bank.PSObject.Properties.Name){if($property -cnotin @('id','fields','clips')){throw 'Unlisted/private field bank metadata'}}
        if($bank.id -isnot [string] -or $bank.id -cnotmatch '^[a-z0-9][a-z0-9-]*\z' -or $bankIds.ContainsKey($bank.id)){
            throw 'Invalid or duplicate field bank identity'
        }
        $bankIds[$bank.id]=$true
        if($bank.fields -isnot [array] -or $bank.fields.Count -eq 0 -or $bank.clips -isnot [array] -or $bank.clips.Count -eq 0){
            throw 'Field banks require fields and clips arrays'
        }
        foreach($field in $bank.fields){
            if(($field -isnot [int] -and $field -isnot [long]) -or $field -lt 0 -or $field -gt [int]::MaxValue -or $fields.ContainsKey([long]$field)){
                throw 'Invalid or duplicate field bank map ownership'
            }
            $fields[[long]$field]=$true
        }
        foreach($id in $bank.clips){
            if($id -isnot [string] -or !$Clips.ContainsKey($id) -or $movieIds.ContainsKey($id) -or $assigned.ContainsKey($id)){
                throw 'Invalid or duplicate field bank clip ownership'
            }
            $assigned[$id]=$true
        }
        Assert-FF9DescriptionBankBudget @($bank.clips) $Clips
    }
    Assert-FF9DescriptionBankBudget @($Clips.Keys | Where-Object {!$movieIds.ContainsKey($_) -and !$assigned.ContainsKey($_)}) $Clips
}

function Assert-FF9DescriptionPack([string]$AudioRoot,[string]$CatalogPath) {
    $AudioRoot=Resolve-FF9Path $AudioRoot
    $manifestFile=Get-FF9OwnedPath $AudioRoot 'manifest.json'
    $catalog=Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
    if($catalog.PSObject.Properties.Name -contains 'fieldBanks' -and $null -eq $catalog.fieldBanks){throw 'fieldBanks must be an array'}
    if($catalog.schema -ne 1 -or @($catalog.clips).Count -lt 1 -or @($catalog.clips).Count -gt 4096 -or @($catalog.films).Count -ne 42){throw 'Incomplete or oversized audio-description catalog'}
    if($CatalogPath -and (Get-FF9Hash $manifestFile) -ne (Get-FF9Hash $CatalogPath)){throw 'Public and embedded audio-description catalogs differ'}
    $clips=@{};$expected=@{'manifest.json'=$true}
    foreach($clip in $catalog.clips) {
        if($clip.id -cnotmatch '^[a-z0-9][a-z0-9-]*$' -or $clip.file -cne ($clip.id+'.wav') -or $clips.ContainsKey($clip.id) -or [string]::IsNullOrWhiteSpace($clip.text) -or $clip.sha256 -cnotmatch '^[a-f0-9]{64}$'){throw 'Invalid description recording identity'}
        $recording=Get-FF9OwnedPath $AudioRoot $clip.file
        if($clip.bytes -lt 46 -or $clip.bytes -gt 2097152 -or (Get-Item -LiteralPath $recording).Length -ne $clip.bytes -or (Get-FF9Hash $recording) -ne $clip.sha256){throw "Changed description recording: $($clip.id)"}
        $bytes=[IO.File]::ReadAllBytes($recording)
        if([Text.Encoding]::ASCII.GetString($bytes,0,4) -ne 'RIFF' -or [BitConverter]::ToInt32($bytes,4) -ne $bytes.Length-8 -or [Text.Encoding]::ASCII.GetString($bytes,8,8) -ne 'WAVEfmt ' -or [BitConverter]::ToInt32($bytes,16) -ne 16 -or [BitConverter]::ToInt16($bytes,20) -ne 1 -or [BitConverter]::ToInt16($bytes,22) -ne 1 -or [BitConverter]::ToInt32($bytes,24) -ne 24000 -or [BitConverter]::ToInt32($bytes,28) -ne 48000 -or [BitConverter]::ToInt16($bytes,32) -ne 2 -or [BitConverter]::ToInt16($bytes,34) -ne 16 -or [Text.Encoding]::ASCII.GetString($bytes,36,4) -ne 'data' -or [BitConverter]::ToInt32($bytes,40) -ne $bytes.Length-44 -or $bytes.Length%2 -ne 0){throw "Noncanonical description WAV: $($clip.id)"}
        $duration=($bytes.Length-44)/48000.0
        if([double]::IsNaN($clip.duration) -or [double]::IsInfinity($clip.duration) -or [Math]::Abs($duration-$clip.duration) -gt 0.000001){throw 'Description duration differs from its recording'}
        $clips[$clip.id]=$clip;$expected[$clip.file]=$true
    }
    foreach($file in Get-ChildItem -LiteralPath $AudioRoot -Force){if($file.PSIsContainer -or !$expected.ContainsKey($file.Name)){throw "Unlisted or private file in audio-description payload: $($file.Name)"}}
    $movies=@{};$movieIds=@{}
    foreach($film in $catalog.films) {
        if($film.movie -cnotmatch '^FMV[0-9]{3}[A-D]?$' -or $movies.ContainsKey($film.movie) -or $film.sourceSHA256 -cnotmatch '^[a-f0-9]{64}$' -or $film.sourceBytes -le 0 -or @($film.cues).Count -eq 0 -or [double]::IsNaN($film.duration) -or [double]::IsInfinity($film.duration) -or $film.duration -le 0){throw 'Invalid or duplicate described movie'}
        $movies[$film.movie]=$true;$previous=0.0
        foreach($cue in $film.cues) {
            if(!$clips.ContainsKey($cue.id) -or [double]::IsNaN($cue.start) -or [double]::IsNaN($cue.end) -or [double]::IsInfinity($cue.start) -or [double]::IsInfinity($cue.end) -or $cue.start -lt $previous -or $cue.end -gt $film.duration -or $cue.end-$cue.start -lt $clips[$cue.id].duration+0.05){throw "Invalid description movie window: $($cue.id)"}
            $previous=$cue.end
            $movieIds[$cue.id]=$true
        }
        Assert-FF9DescriptionBankBudget @($film.cues | ForEach-Object {$_.id}) $clips
    }
    Assert-FF9DescriptionFieldBanks $catalog.fieldBanks $catalog.films $clips
    return $catalog
}

function Assert-FF9Package([string]$PackageRoot,[string]$GameRoot) {
    Assert-FF9SeparatePaths $GameRoot $PackageRoot
    $manifestPath=Get-FF9OwnedPath $PackageRoot 'package-manifest.json'
    $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.ForkId -ne 'ff9-access-local' -or $manifest.AccessibilityApi -ne 1 -or $manifest.Schema -ne 1) { throw 'Unknown or incompatible accessibility package' }
    $supportManifest=Get-FF9OwnedPath $PackageRoot 'Payload/accessibility/memoria-support/manifest.json'
    if(!(Test-Path -LiteralPath $supportManifest -PathType Leaf)){throw 'Source-matched Memoria baseline support manifest is required'}
    $paths=@{}
    foreach ($file in $manifest.Files) {
        if (!(Test-FF9AllowedPayload $file.Path) -or $paths.ContainsKey($file.Path)) { throw "Unapproved or duplicate payload path: $($file.Path)" }
        $paths[$file.Path]=$file
        $source=Get-FF9OwnedPath $PackageRoot ('Payload/'+$file.Path)
        [void](Get-FF9OwnedPath $GameRoot $file.Path)
        if (!(Test-Path -LiteralPath $source -PathType Leaf) -or (Get-FF9Hash $source) -ne $file.SHA256) { throw "Package hash mismatch: $($file.Path)" }
        if ([IO.Path]::GetExtension($source) -in '.dll','.exe') {
            $pe=Get-FF9PE $source
            if($file.Path -ceq 'StreamingAssets/Scripts/Memoria.Scripts.dll'){
                if($file.Architecture -cne 'MSIL' -or !$pe.Managed -or $pe.Machine -ne 332 -or ($pe.CorFlags -band 2) -ne 0 -or [Reflection.AssemblyName]::GetAssemblyName($source).Name -cne 'Memoria.Scripts'){throw 'Memoria.Scripts must be the source-built shared scripts assembly (AnyCPU)'}
            }
            if ($file.Architecture -eq 'x64' -and $pe.Machine -ne 34404) { throw "x64 architecture mismatch: $($file.Path)" }
            if ($file.Architecture -eq 'x86' -and $pe.Machine -ne 332) { throw "x86 architecture mismatch: $($file.Path)" }
            if ($file.Architecture -eq 'x86' -and $pe.Managed -and ($pe.CorFlags -band 2) -eq 0) { throw "x86 managed process architecture mismatch: $($file.Path); native x86 dependencies require CLR 32BITREQUIRED" }
            $system=Join-Path $env:WINDIR $(if ($file.Architecture -eq 'x86') {'SysWOW64'} else {'System32'})
            foreach ($import in $pe.Imports) {
                if ([IO.Path]::GetFileName($import) -ne $import -or $import.Contains(':')) { throw 'Invalid native import name' }
                if ($import -match '^(api-ms-win-|ext-ms-win-)') { continue }
                $folder=Split-Path $source -Parent
                $nativeOriginal=Join-Path (Split-Path (Get-FF9OwnedPath $GameRoot $file.Path) -Parent) $import
                $architectureImport=$null
                if($file.Path -match '^x(86|64)/'){$architectureImport=Get-FF9OwnedPath $PackageRoot ('Payload/x'+$Matches[1]+'/'+$import)}
                if (!(Test-Path -LiteralPath (Join-Path $folder $import)) -and !($architectureImport -and (Test-Path -LiteralPath $architectureImport)) -and !(Test-Path -LiteralPath $nativeOriginal) -and !(Test-Path -LiteralPath (Join-Path $system $import))) { throw "Missing native import $import for $($file.Path)" }
            }
        }
    }
    foreach ($arch in @('x86','x64')) {
        foreach ($name in @('Assembly-CSharp','Memoria.Prime','Memoria.ScreenReader','UnityEngine.UI','XInputDotNetPure')) { if (!$paths.ContainsKey("$arch/FF9_Data/Managed/$name.dll")) { throw "Required $arch managed core missing: $name" } }
        $bits=if ($arch -eq 'x86') {'32'} else {'64'}
        foreach ($name in @('prism.dll','Tolk.dll',"nvdaControllerClient$bits.dll")) {
            if (!$paths.ContainsKey("$arch/$name") -or !$paths.ContainsKey("$arch/FF9_Data/Managed/$name")) { throw "Required $arch speech dependency missing: $name" }
        }
    }
    foreach ($name in @('FF9_Launcher.exe','FF9_Launcher.exe.config','prism.dll','Tolk.dll','nvdaControllerClient32.dll')) { if (!$paths.ContainsKey($name)) { throw "Required launcher file missing: $name" } }
    if(!$paths.ContainsKey('StreamingAssets/Scripts/Memoria.Scripts.dll') -or !$paths.ContainsKey('accessibility/memoria-support/manifest.json')){throw 'Required source-matched Memoria scripts or baseline support manifest missing'}
    $support=Get-Content -LiteralPath $supportManifest -Raw|ConvertFrom-Json
    if($support.Schema -ne 1 -or $support.Id -ne 'ff9-memoria-support' -or @($support.Files).Count -lt 1000){throw 'Invalid pinned Memoria baseline support manifest'}
    $supportPaths=@{}
    foreach($required in $support.Files){
        if(!(Test-FF9AllowedPayload $required.Path) -or $supportPaths.ContainsKey($required.Path)){throw 'Invalid or duplicate pinned Memoria support path'}
        $supportPaths[$required.Path]=$true
        $record=@($paths[$required.Path])
        if($record.Count -ne 1 -or $record[0].SHA256 -ne $required.SHA256 -or $record[0].Bytes -ne $required.Bytes -or $record[0].Architecture -cne $required.Architecture){throw "Required pinned Memoria baseline support missing or changed: $($required.Path)"}
    }
    foreach($arch in @('x86','x64')){foreach($native in @('FF9_Data/Plugins/SaXAudio.dll','FF9_Data/Plugins/soloud.dll','FF9_Data/Plugins/JoyShockLibrary.dll')){if(!$supportPaths.ContainsKey($arch+'/'+$native)){throw "Required pinned native support omitted: $arch/$native"}}}
    foreach($required in @('StreamingAssets/Data/Items/Items.csv','StreamingAssets/Shaders/BattleCommon.txt','FF9_Data/EmbeddedAsset/UI/Sprites/changetodisc2.png','accessibility/UnityEngine.UI_LICENSE')){if(!$supportPaths.ContainsKey($required)){throw "Required pinned baseline asset omitted: $required"}}
    if($paths.ContainsKey('accessibility/navigation/proximity-manifest.json') -or @($manifest.Files | Where-Object {$_.Path -like '*/Memoria.SpatialAudio.dll'}).Count -gt 0){
        if(!$paths.ContainsKey('accessibility/navigation/proximity-manifest.json') -or !$paths.ContainsKey('accessibility/spatial/manifest.json') -or !$paths.ContainsKey('accessibility/spatial/LICENSE.md')){throw 'Spatial audio catalog or license omitted from package'}
        $recordings=Assert-FF9SpatialRecordings (Get-FF9OwnedPath $PackageRoot 'Payload/accessibility/navigation')
        foreach($cue in $recordings.cues){foreach($record in @($cue.original,$cue.playback)){if(!$paths.ContainsKey('accessibility/navigation/'+$record.file)){throw 'Spatial recording omitted from transactional package manifest'}}}
        $native=Get-Content -LiteralPath (Get-FF9OwnedPath $PackageRoot 'Payload/accessibility/spatial/manifest.json') -Raw | ConvertFrom-Json
        if($native.schema -ne 1 -or $native.version -ne '4.6.1' -or @($native.files).Count -ne 2){throw 'Invalid pinned spatial native catalog'}
        foreach($arch in @('x86','x64')){
            $records=@($native.files | Where-Object {$_.architecture -eq $arch -and $_.file -ceq "$arch/Memoria.SpatialAudio.dll"})
            $entry=@($manifest.Files | Where-Object {$_.Path -ceq "$arch/FF9_Data/Managed/Memoria.SpatialAudio.dll"})
            if($records.Count -ne 1 -or $entry.Count -ne 1 -or $entry[0].SHA256 -ne $records[0].sha256 -or $entry[0].Bytes -ne $records[0].bytes){throw "Pinned spatial dependency missing or changed: $arch"}
        }
    }
    if($paths.ContainsKey('accessibility/audio-descriptions/manifest.json')) {
        foreach($arch in @('x86','x64')){if(!$paths.ContainsKey("$arch/FF9_Data/Managed/Memoria.DescriptionSpeech.dll")){throw 'Owned description speech dependency omitted from package'}}
        $audioRoot=Get-FF9OwnedPath $PackageRoot 'Payload/accessibility/audio-descriptions'
        $descriptionCatalog=Get-FF9OwnedPath $PackageRoot 'Payload/accessibility/docs/audio-descriptions.json'
        $descriptions=Assert-FF9DescriptionPack $audioRoot $descriptionCatalog
        foreach($clip in $descriptions.clips){if(!$paths.ContainsKey('accessibility/audio-descriptions/'+$clip.file)){throw 'Description clip omitted from transactional package manifest'}}
    }
    return $manifest
}

function Get-FF9ProtectedSnapshot([string]$GameRoot,$Manifest) {
    $allowed=@{'Memoria.ini'=$true;'accessibility/installed-build.json'=$true}
    foreach ($file in $Manifest.Files) { $allowed[$file.Path]=$true }
    @(Get-ChildItem -LiteralPath $GameRoot -File -Recurse -Force | ForEach-Object {
        $relative=$_.FullName.Substring($GameRoot.Length+1).Replace('\','/')
        if (!$allowed.ContainsKey($relative) -and $_.Extension -notin '.log','.tmp') {
            [void](Get-FF9OwnedPath $GameRoot $relative)
            [pscustomobject]@{Path=$relative;SHA256=(Get-FF9Hash $_.FullName)}
        }
    })
}

function Merge-FF9NavigationSettings([string]$Path) {
    $legacy=1
    $lines=if (Test-Path -LiteralPath $Path) { [IO.File]::ReadAllLines($Path) } else {@()}
    $inside=$false; $found=$false; $insertion=$lines.Count; $known=@{}
    for ($index=0;$index -lt $lines.Count;$index++) {
        $line=$lines[$index].Trim()
        if ($line.StartsWith('[')) {
            if ($inside) {$insertion=$index}
            $inside=$line -ieq '[Accessibility]'; $found=$found -or $inside
        } elseif ($inside -and $line -match '^([^;#=]+)=(.*)$') {
            $key=$Matches[1].Trim();$value=$Matches[2].Trim();$known[$key]=$value
            if ($key -eq 'NavigationUpdateInterval') { $number=0;if ([int]::TryParse($value,[ref]$number)) {$legacy=[Math]::Max(1,$number)} }
        }
    }
    $values=[ordered]@{
        NavigationBindings='PreviousCategory=U|NextCategory=O|PreviousTarget=K|NextTarget=Semicolon|Status=L|ToggleGuidance=I|ToggleAutomatic=P|Repeat=R|DecreaseInterval=Shift+I|IncreaseInterval=Shift+P'
        NavigationIntervalMilliseconds=([Math]::Min(10000,[long]$legacy*1000)).ToString()
        AnnounceRegionChanges='1';ControllerNaming='Xbox';ControllerMenuButton='R3'
    }
    $added=@();$newLines=New-Object 'System.Collections.Generic.List[string]'
    foreach ($line in $lines) { $newLines.Add($line) }
    if (!$found) { $newLines.Add('');$newLines.Add('[Accessibility]');$insertion=$newLines.Count }
    foreach ($key in $values.Keys) { if (!$known.ContainsKey($key)) {
        $newLines.Insert($insertion++, "$key = $($values[$key])")
        $added += [pscustomobject]@{Key=$key;Value=$values[$key]}
    } }
    if ($added.Count -gt 0) { Write-FF9AtomicBytes $Path ([Text.Encoding]::UTF8.GetBytes(($newLines.ToArray() -join [Environment]::NewLine)+[Environment]::NewLine)) }
    return $added
}

function Undo-FF9NavigationSettings([string]$Path,$Added) {
    if (!(Test-Path -LiteralPath $Path)) { return }
    $inside=$false;$result=New-Object 'System.Collections.Generic.List[string]'
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        $trimmed=$line.Trim();$remove=$false
        if ($trimmed.StartsWith('[')) { $inside=$trimmed -ieq '[Accessibility]' }
        elseif ($inside -and $trimmed -match '^([^;#=]+)=(.*)$') {
            $key=$Matches[1].Trim();$value=$Matches[2].Trim()
            foreach ($entry in $Added) { if ($entry.Key -ieq $key -and $entry.Value -ceq $value) { $remove=$true } }
        }
        if (!$remove) {$result.Add($line)}
    }
    Write-FF9AtomicBytes $Path ([Text.Encoding]::UTF8.GetBytes(($result.ToArray() -join [Environment]::NewLine)+[Environment]::NewLine))
}

function Restore-FF9Transaction($Manifest,[string]$BackupDirectory,[string]$GameRoot,[bool]$AfterFailure) {
    foreach ($entry in $Manifest.Files) {
        if (!(Test-FF9AllowedPayload $entry.Path) -and $entry.Path -notin @('Memoria.ini','accessibility/installed-build.json')) { throw "Unapproved rollback path: $($entry.Path)" }
        $path=Get-FF9OwnedPath $GameRoot $entry.Path
        if ($entry.OriginalExists) {
            $original=Get-FF9OwnedPath $BackupDirectory ('Originals/'+$entry.Path)
            if (!(Test-Path -LiteralPath $original) -or (Get-FF9Hash $original) -ne $entry.OriginalSHA256) { throw "Rollback original missing or changed: $($entry.Path)" }
        }
        if ($entry.PSObject.Properties.Name -contains 'TemporaryPath' -and $entry.TemporaryPath) {
            if ($entry.TemporaryPath -notmatch ('^'+[regex]::Escape($entry.Path)+'\.ff9access-[0-9a-f]{32}\.tmp$')) {throw 'Unapproved transaction temporary path'}
            [void](Get-FF9OwnedPath $GameRoot $entry.TemporaryPath)
        }
        $exists=Test-Path -LiteralPath $path -PathType Leaf
        $current=if ($exists) {Get-FF9Hash $path} else {$null}
        $original=($entry.OriginalExists -and $current -eq $entry.OriginalSHA256) -or (!$entry.OriginalExists -and !$exists)
        if (!$entry.Applied -and !$original) {throw "Unjournaled installation change; rollback preserved it: $($entry.Path)"}
        if (!$AfterFailure -and $entry.Applied -and !$original -and $entry.Path -ne 'Memoria.ini' -and $current -ne $entry.NewSHA256) { throw "File changed after installation; rollback preserved it: $($entry.Path)" }
    }
    foreach ($entry in $Manifest.Files) {
        if (!$entry.Applied) { continue }
        $path=Get-FF9OwnedPath $GameRoot $entry.Path
        $current=if (Test-Path -LiteralPath $path -PathType Leaf) {Get-FF9Hash $path} else {$null}
        if ($entry.Path -eq 'Memoria.ini' -and !$AfterFailure -and $current -and $current -ne $entry.NewSHA256 -and $current -ne $entry.OriginalSHA256) {
            Undo-FF9NavigationSettings $path $entry.AddedSettings
        } elseif ($entry.OriginalExists) {
            $temporary=Get-FF9OwnedPath $GameRoot ($entry.Path+'.ff9access-'+[guid]::NewGuid().ToString('N')+'.tmp')
            Copy-FF9Atomic (Get-FF9OwnedPath $BackupDirectory ('Originals/'+$entry.Path)) $path $temporary
            if ((Get-FF9Hash $path) -ne $entry.OriginalSHA256) { throw "Rollback hash mismatch: $($entry.Path)" }
        } elseif (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
        if ($entry.PSObject.Properties.Name -contains 'TemporaryPath' -and $entry.TemporaryPath) {
            $temporary=Get-FF9OwnedPath $GameRoot $entry.TemporaryPath
            if (Test-Path -LiteralPath $temporary) {Remove-Item -LiteralPath $temporary -Force}
        }
    }
}
