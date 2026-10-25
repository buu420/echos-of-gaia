$ErrorActionPreference = 'Stop'
$source = Split-Path $PSScriptRoot -Parent
$helper = Join-Path $source 'tools/Assert-EchosOfGaia.ps1'
if (!(Test-Path -LiteralPath $helper)) { throw 'Echos of Gaia release preflight is missing.' }
. $helper
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('echos-preflight-' + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $fixture)
$paths = @('x86/FF9.exe','x64/FF9.exe','x86/FF9_Data/Managed/UnityEngine.dll','x64/FF9_Data/Managed/UnityEngine.dll','x86/FF9_Data/Managed/Assembly-CSharp-firstpass.dll','x64/FF9_Data/Managed/Assembly-CSharp-firstpass.dll')
$files = @()
foreach ($relative in $paths) {
    $path = Join-Path $fixture $relative
    [void](New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force)
    [IO.File]::WriteAllText($path, 'immutable fixture ' + $relative)
    $files += [pscustomobject]@{Path=$relative;SHA256=(Get-FileHash -LiteralPath $path).Hash;Bytes=(Get-Item -LiteralPath $path).Length}
}
$compatibility = [pscustomobject]@{Schema=1;SteamAppId='377840';SteamBuildId='5378074';Files=$files}
function Reject([scriptblock]$Action,[string]$Expected) {
    $rejected = $false
    try { & $Action } catch { $rejected = $_.Exception.Message.Contains($Expected) }
    if (!$rejected) { throw "Expected rejection: $Expected" }
}
$before = @($files | ForEach-Object { $_.SHA256 }) -join '|'
Assert-EchosOfGaiaGame -GameRoot $fixture -Compatibility $compatibility
Write-Host 'PASS supported original executables and Unity references'
$original = $files[0].SHA256
$files[0].SHA256 = '0' * 64
Reject { Assert-EchosOfGaiaGame -GameRoot $fixture -Compatibility $compatibility } 'Unsupported or changed game file'
$files[0].SHA256 = $original
Write-Host 'PASS changed original binary rejected'
$saved = $compatibility.Files
$compatibility.Files = @($saved | Select-Object -Skip 1)
Reject { Assert-EchosOfGaiaGame -GameRoot $fixture -Compatibility $compatibility } 'complete original-game compatibility set'
$compatibility.Files = $saved
$savedPath = $files[0].Path
$files[0].Path = '../outside.exe'
Reject { Assert-EchosOfGaiaGame -GameRoot $fixture -Compatibility $compatibility } 'Unapproved compatibility path'
$files[0].Path = $savedPath
$compatibility.Files = @($saved) + @($saved[0])
Reject { Assert-EchosOfGaiaGame -GameRoot $fixture -Compatibility $compatibility } 'Unapproved compatibility path'
$compatibility.Files = $saved
Write-Host 'PASS incomplete, escaping and duplicate target sets rejected'
$files[0].SHA256 = 'not-a-hash'
Reject { Assert-EchosOfGaiaGame -GameRoot $fixture -Compatibility $compatibility } 'Invalid original-game fingerprint'
$files[0].SHA256 = $original
$compatibility.SteamAppId = '39140'
Reject { Assert-EchosOfGaiaGame -GameRoot $fixture -Compatibility $compatibility } 'Unsupported game compatibility identity'
$compatibility.SteamAppId = '377840'
Write-Host 'PASS malformed fingerprint and wrong game identity rejected'
$after = @($paths | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $fixture $_)).Hash }) -join '|'
if ($before -cne $after) { throw 'Preflight modified a fixture file.' }
Write-Host 'PASS preflight is read-only'
Write-Host "Evidence retained: $fixture"
