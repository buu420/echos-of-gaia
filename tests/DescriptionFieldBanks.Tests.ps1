$ErrorActionPreference='Stop'
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'tools/FF9Access.Deployment.ps1')
function RejectBanks($Banks,$Films,$Clips,[string]$Label) {
    $rejected=$false;try{Assert-FF9DescriptionFieldBanks $Banks $Films $Clips}catch{$rejected=$true;if($_.Exception -is [Management.Automation.CommandNotFoundException]){throw}}
    if(!$rejected){throw "Accepted $Label"};Write-Host "PASS FieldBankReject $Label"
}
$clips=@{};0..512 | ForEach-Object {$clips["field-$_"]=[pscustomobject]@{bytes=46}}
$good=[pscustomobject]@{id='group-a';fields=@(50,51);clips=@('field-512')}
Assert-FF9DescriptionFieldBanks @($good) @() $clips
Write-Host 'PASS FieldBankGenericExcludesAssignedMembers'
RejectBanks @([pscustomobject]@{id="group-a`n";fields=@(50);clips=@('field-512')}) @() $clips 'bank ID with trailing newline'
RejectBanks @($good,[pscustomobject]@{id='group-b';fields=@(51);clips=@('field-1')}) @() $clips 'duplicate field owner'
RejectBanks @($good,[pscustomobject]@{id='group-b';fields=@(52);clips=@('field-512')}) @() $clips 'duplicate clip owner'
RejectBanks @([pscustomobject]@{id='group-a';fields=@(50);clips=@('missing')}) @() $clips 'missing clip'
RejectBanks @($good) @([pscustomobject]@{cues=@([pscustomobject]@{id='field-512'})}) $clips 'movie clip'
foreach($field in @('50',-1,2147483648,$true)) {
    RejectBanks @([pscustomobject]@{id='group-a';fields=@($field);clips=@('field-512')}) @() $clips 'invalid field value'
}
$allClips=@{};0..4096 | ForEach-Object {$allClips["field-$_"]=[pscustomobject]@{bytes=46}}
$banks=@(0..8 | ForEach-Object {$n=$_;[pscustomobject]@{id="group-$n";fields=@($n);clips=@(($n*512)..([Math]::Min(($n+1)*512-1,4096)) | ForEach-Object {"field-$_"})}})
RejectBanks $banks @() $allClips '4097 clips'
$allClips.Remove('field-4096');Assert-FF9DescriptionFieldBanks @($banks[0..7]) @() $allClips
Write-Host 'PASS FieldBank4096IndependentClipLimit'

# Exercise the public validator itself above the former whole-pack 512 limit.
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('FF9FieldBanks-'+[guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $fixture)
$stream=New-Object IO.MemoryStream;$writer=New-Object IO.BinaryWriter($stream)
$writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'));$writer.Write([int]38)
$writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '));$writer.Write([int]16)
$writer.Write([int16]1);$writer.Write([int16]1);$writer.Write([int]24000);$writer.Write([int]48000)
$writer.Write([int16]2);$writer.Write([int16]16);$writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
$writer.Write([int]2);$writer.Write([int16]4096);$bytes=$stream.ToArray();$writer.Dispose();$stream.Dispose()
$sha=[Security.Cryptography.SHA256]::Create();$hash=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','').ToLowerInvariant();$sha.Dispose()
$rows=@(0..513 | ForEach-Object {$id="cue-$_";[IO.File]::WriteAllBytes((Join-Path $fixture ($id+'.wav')),$bytes);[pscustomobject]@{id=$id;text='Test visible action.';file=$id+'.wav';sha256=$hash;bytes=46;duration=(1/24000.0)}})
$movies=@(0..41 | ForEach-Object {[pscustomobject]@{movie=('FMV{0:000}' -f $_);duration=5.0;sourceSHA256=('0'*64);sourceBytes=100;cues=@([pscustomobject]@{id='cue-0';start=1.0;end=3.0})}})
$public=[pscustomobject]@{schema=1;clips=$rows;films=$movies;fieldBanks=@([pscustomobject]@{id='group';fields=@(1);clips=@(2..513 | ForEach-Object {"cue-$_"})})}
$manifestPath=Join-Path $fixture 'manifest.json';$public | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8
$accepted=Assert-FF9DescriptionPack $fixture
if(@($accepted.clips).Count -ne 514){throw 'Public validator dropped clips above 512'}
Write-Host 'PASS PublicFieldBank514Clips'
$public.fieldBanks[0] | Add-Member -NotePropertyName privateReference -NotePropertyValue 'private.wav'
$public | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8
$rejected=$false;try{[void](Assert-FF9DescriptionPack $fixture)}catch{$rejected=$true}
if(!$rejected){throw 'Public bank metadata accepted private properties'}
Write-Host 'PASS PublicFieldBankRejectPrivateMetadata'
Write-Host "PASS FieldBankAll; retained fixture $fixture"
