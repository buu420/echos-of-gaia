[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputRoot)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot -Parent
$vswhere='C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find MSBuild/Current/Bin/MSBuild.exe | Select-Object -First 1
if(!$msbuild){throw 'Windows MSBuild and .NET Framework 4.8 desktop tools are required.'}
[void](New-Item -ItemType Directory -Path $OutputRoot -Force)
& $msbuild (Join-Path $sourceRoot 'tests/Installer.Tests/Installer.Tests.csproj') /restore /nologo /v:minimal /m:1 /p:Configuration=Release ("/p:OutDir="+[IO.Path]::GetFullPath($OutputRoot)+'\')
if($LASTEXITCODE -ne 0){throw 'Installer tests did not compile.'}
& (Join-Path $OutputRoot 'Installer.Tests.exe')
if($LASTEXITCODE -ne 0){throw 'Installer checks failed.'}
