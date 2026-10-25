param([Parameter(Mandatory=$true)][string]$SourceRoot, [Parameter(Mandatory=$true)][string]$OutputRoot)
$ErrorActionPreference = 'Stop'
$SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$vcvars = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvarsall.bat'
if (!(Test-Path -LiteralPath $vcvars)) { throw 'MSVC desktop tools are required' }
$sources = 'Tolk.cpp ScreenReaderDriverJAWS.cpp ScreenReaderDriverNVDA.cpp ScreenReaderDriverSA.cpp ScreenReaderDriverSNova.cpp ScreenReaderDriverWE.cpp ScreenReaderDriverZT.cpp ScreenReaderDriverSAPI.cpp fsapi.c wineyes.c zt.c'
foreach ($arch in @('x86','x64')) {
    $out = Join-Path $OutputRoot $arch
    [void](New-Item -ItemType Directory -Path $out -Force)
    $script = Join-Path $out 'build.cmd'
    @"
@echo off
call "$vcvars" $arch >nul
if errorlevel 1 exit /b 1
cd /d "$SourceRoot\src"
cl /nologo /O2 /EHsc /LD /MT /D_EXPORTING /DUNICODE /Fe:"$out\Tolk.dll" $sources User32.lib Ole32.lib OleAut32.lib
exit /b %errorlevel%
"@ | Set-Content -LiteralPath $script -Encoding ASCII
    & $env:ComSpec /d /c ('"' + $script + '"')
    if ($LASTEXITCODE -ne 0) { throw "Tolk $arch build failed" }
    Copy-Item -LiteralPath (Join-Path $SourceRoot "libs/$arch/nvdaControllerClient$(if ($arch -eq 'x86') {'32'} else {'64'}).dll") -Destination $out -Force
}
"PASS Tolk x86+x64; static MSVC runtime; unmodified source"
