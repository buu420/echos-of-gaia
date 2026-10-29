[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputRoot)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot -Parent
$vswhere='C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find MSBuild/Current/Bin/MSBuild.exe | Select-Object -First 1
if(!$msbuild){throw 'Windows MSBuild and .NET Framework 4.8 desktop tools are required.'}
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
[void](New-Item -ItemType Directory -Path $OutputRoot -Force)
& $msbuild (Join-Path $sourceRoot 'EchosOfGaia.Installer/EchosOfGaia.Installer.csproj') /restore /nologo /v:minimal /m:1 /p:Configuration=Release ("/p:OutDir="+$OutputRoot+'\')
if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath (Join-Path $OutputRoot 'Install-EchosOfGaia.exe') -PathType Leaf)){throw 'Installer did not build.'}
Write-Host 'PASS standalone installer build'
