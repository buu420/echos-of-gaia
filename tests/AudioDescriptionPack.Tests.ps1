$ErrorActionPreference='Stop'
$source=Split-Path $PSScriptRoot -Parent
. (Join-Path $source 'tools/FF9Access.Deployment.ps1')
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('FF9DescriptionPack-'+[guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $fixture)
$audio=Join-Path $fixture 'test.wav'
$stream=New-Object IO.MemoryStream
$writer=New-Object IO.BinaryWriter($stream)
$writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'));$writer.Write([int]48036)
$writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '));$writer.Write([int]16)
$writer.Write([int16]1);$writer.Write([int16]1);$writer.Write([int]24000);$writer.Write([int]48000)
$writer.Write([int16]2);$writer.Write([int16]16);$writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
$writer.Write([int]48000);$writer.Write((New-Object byte[] 48000))
[IO.File]::WriteAllBytes($audio,$stream.ToArray());$writer.Dispose();$stream.Dispose()
$clip=[pscustomobject]@{id='test';text='A visible gesture.';file='test.wav';sha256=(Get-FF9Hash $audio).ToLowerInvariant();bytes=48044;duration=1.0}
$films=@(0..41 | ForEach-Object {[pscustomobject]@{movie=('FMV{0:000}' -f $_);duration=5.0;sourceSHA256=('0'*64);sourceBytes=100;cues=@([pscustomobject]@{id='test';start=1.0;end=3.0})}})
$manifest=[pscustomobject]@{schema=1;clips=@($clip);films=$films}
$manifestPath=Join-Path $fixture 'manifest.json'
function Save {$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8}
function Reject([scriptblock]$mutation,[string]$label) {
    & $mutation;Save;$failed=$false;try{[void](Assert-FF9DescriptionPack $fixture)}catch{$failed=$true}
    if(!$failed){throw "Accepted $label"};Write-Host "PASS DescriptionPackReject $label"
}
Save;[void](Assert-FF9DescriptionPack $fixture);Write-Host 'PASS DescriptionPackValid'
$oversized=@{};0..16 | ForEach-Object {$oversized["$_"]=[pscustomobject]@{bytes=2097152}}
$budgetRejected=$false;try{Assert-FF9DescriptionBankBudget @($oversized.Keys) $oversized}catch{$budgetRejected=$true}
if(!$budgetRejected){throw 'Accepted runtime bank beyond 32 MiB'};Write-Host 'PASS DescriptionBankBudget'
$clip.sha256='f'*64;Reject {} 'changed audio hash';$clip.sha256=(Get-FF9Hash $audio).ToLowerInvariant()
$films[0].cues[0].end=1.5;Reject {} 'voice exceeding scene window';$films[0].cues[0].end=3
$films[1].movie=$films[0].movie;Reject {} 'duplicate movie';$films[1].movie='FMV001'
$clip.file='../test.wav';Reject {} 'unsafe recording path';$clip.file='test.wav'
$private=Join-Path $fixture 'reference.m4a';[IO.File]::WriteAllText($private,'private voice reference')
Reject {} 'private or unlisted file';Remove-Item -LiteralPath $private
Save;[void](Assert-FF9DescriptionPack $fixture);Write-Host "PASS DescriptionPackAll; retained evidence $fixture"
