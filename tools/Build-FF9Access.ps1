param(
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [Parameter(Mandatory=$true)][string]$StageRoot,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [ValidateSet('References','Game','Launcher','Tests','All')][string]$Mode = 'All',
    [ValidateSet('x86','x64','Both')][string]$Architecture = 'Both'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FF9Access.Paths.ps1')
Assert-FF9SeparatePaths -GameRoot $GameRoot -OtherRoot $StageRoot
$GameRoot = Resolve-FF9Path $GameRoot
$StageRoot = Resolve-FF9Path $StageRoot
$sourceRoot = Split-Path $PSScriptRoot -Parent
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = if (Test-Path -LiteralPath $vswhere) { & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find MSBuild/Current/Bin/MSBuild.exe | Select-Object -First 1 }
if (!$msbuild) { throw 'Full Windows MSBuild with .NET desktop build tools is required' }
[void](New-Item -ItemType Directory -Path $StageRoot -Force)
$cache = Join-Path (Split-Path $StageRoot -Parent) 'dependencies'
Assert-FF9SeparatePaths -GameRoot $GameRoot -OtherRoot $cache
[void](New-Item -ItemType Directory -Path $cache -Force)
$net35 = Join-Path $cache 'microsoft.netframework.referenceassemblies.net35.1.0.3'
if (!(Test-Path -LiteralPath (Join-Path $net35 'build/.NETFramework/v3.5/mscorlib.dll'))) {
    $archive = Join-Path $cache 'net35.1.0.3.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net35/1.0.3/microsoft.netframework.referenceassemblies.net35.1.0.3.nupkg' -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $net35 -Force
}
$frameworkRoot = Join-Path $net35 'build/'
$architectures = if ($Architecture -eq 'Both') { @('x86','x64') } else { @($Architecture) }
$referenceManifest = @()
foreach ($arch in $architectures) {
    $refs = Join-Path $StageRoot "References/$arch"
    [void](New-Item -ItemType Directory -Path $refs -Force)
    $managed = Join-Path $GameRoot "$arch/FF9_Data/Managed"
    foreach ($name in @('Assembly-CSharp-firstpass.dll','UnityEngine.dll','UnityEngine.UI.dll','XInputDotNetPure.dll')) {
        $original = Join-Path $managed $name
        if (!(Test-Path -LiteralPath $original)) { throw "Missing verified reference: $original" }
        Copy-Item -LiteralPath $original -Destination (Join-Path $refs $name) -Force
        $referenceManifest += [pscustomobject]@{Architecture=$arch;Path=$original;SHA256=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash}
    }
}
$referenceManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $StageRoot 'reference-manifest.json') -Encoding UTF8
function Invoke-FF9Build([string]$Project,[string]$Output,[string]$References,[string]$PlatformTarget = 'AnyCPU',[bool]$IsGame = $false,[string]$GameOutput = '') {
    [void](New-Item -ItemType Directory -Path $Output -Force)
    $arguments = @($Project,'/restore','/nologo','/v:minimal','/m:1',"/p:Configuration=$Configuration",'/p:Platform=AnyCPU',"/p:PlatformTarget=$PlatformTarget",'/p:EnableGameDeployment=false',"/p:SolutionDir=$sourceRoot\",("/p:OutDir=" + $Output + '\'),"/p:FF9StageRoot=$StageRoot","/p:FF9Architecture=$PlatformTarget","/p:FF9BuildTaskAssembly=$StageRoot\BuildTools\Memoria.MSBuild.dll","/p:GameReferenceRoot=$References",'/p:LangVersion=latest','/p:DebugType=portable')
    if ($IsGame) { $arguments += "/p:TargetFrameworkRootPath=$frameworkRoot" }
    if ($GameOutput) { $arguments += "/p:FF9GameOutputRoot=$GameOutput" }
    & $msbuild @arguments
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $Project ($LASTEXITCODE)" }
}
if ($Mode -in 'Game','All') {
    Invoke-FF9Build -Project (Join-Path $sourceRoot 'Memoria.MSBuild/Memoria.MSBuild.csproj') -Output (Join-Path $StageRoot 'BuildTools') -References (Join-Path $StageRoot 'References/x64')
    foreach ($arch in $architectures) {
        Invoke-FF9Build -Project (Join-Path $sourceRoot 'Assembly-CSharp/Assembly-CSharp.csproj') -Output (Join-Path $StageRoot "Game/$arch") -References (Join-Path $StageRoot "References/$arch") -PlatformTarget $arch -IsGame $true
    }
    # One AnyCPU script assembly serves both game architectures. Reference only the just-built
    # game, never a stale installed Assembly-CSharp or the project's legacy Output folder.
    $scriptArch=$architectures[0]
    Invoke-FF9Build -Project (Join-Path $sourceRoot 'Memoria.Scripts/Memoria.Scripts.csproj') -Output (Join-Path $StageRoot 'Scripts') -References (Join-Path $StageRoot "References/$scriptArch") -IsGame $true -GameOutput (Join-Path $StageRoot "Game/$scriptArch")
}
if ($Mode -in 'Launcher','All') {
    Invoke-FF9Build -Project (Join-Path $sourceRoot 'Memoria.ScreenReader/Memoria.ScreenReader.csproj') -Output (Join-Path $StageRoot 'LauncherSpeech') -References (Join-Path $StageRoot 'References/x86') -IsGame $true
    Invoke-FF9Build -Project (Join-Path $sourceRoot 'Memoria.Launcher/Memoria.Launcher.csproj') -Output (Join-Path $StageRoot 'Launcher') -References (Join-Path $StageRoot 'References/x64') -PlatformTarget x86
}
if ($Mode -in 'Tests','All') {
    if ($Mode -ne 'All') { Invoke-FF9Build -Project (Join-Path $sourceRoot 'Memoria.ScreenReader/Memoria.ScreenReader.csproj') -Output (Join-Path $StageRoot 'LauncherSpeech') -References (Join-Path $StageRoot 'References/x86') -IsGame $true }
    & dotnet build (Join-Path $sourceRoot 'tests/FF9Access.Tests/FF9Access.Tests.csproj') --configuration $Configuration --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Core tests did not compile' }
    foreach ($arch in $architectures) {
        $probe = Join-Path $sourceRoot 'tests/FF9Access.NativeProbe/FF9Access.NativeProbe.csproj'
        if (Test-Path -LiteralPath $probe) { Invoke-FF9Build -Project $probe -Output (Join-Path $StageRoot "NativeProbe/$arch") -References (Join-Path $StageRoot "References/$arch") -PlatformTarget $arch }
    }
    $launcherTests = Join-Path $sourceRoot 'tests/LauncherAccessibility.Tests/LauncherAccessibility.Tests.csproj'
    if (Test-Path -LiteralPath $launcherTests) { Invoke-FF9Build -Project $launcherTests -Output (Join-Path $StageRoot 'LauncherTests') -References (Join-Path $StageRoot 'References/x64') -PlatformTarget x86 }
}
if($Mode -in 'Game','All') {
    & (Join-Path $PSScriptRoot 'Build-DescriptionSpeech.ps1') -OutRoot (Join-Path $StageRoot 'DescriptionSpeech')
    if($LASTEXITCODE -ne 0){throw 'Owned description speech did not compile'}
}
$buildFiles = @(Get-ChildItem -LiteralPath $StageRoot -Recurse -File | Where-Object { $_.Extension -in '.dll','.exe' -and $_.FullName -notlike '*\References\*' } | ForEach-Object { [pscustomobject]@{Path=$_.FullName.Substring($StageRoot.Length+1);SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
[pscustomobject]@{SourceCommit=(& git -C $sourceRoot -c maintenance.auto=false rev-parse HEAD);Mode=$Mode;Configuration=$Configuration;Architectures=$architectures;Files=$buildFiles} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $StageRoot 'build-manifest.json') -Encoding UTF8
"PASS StagedBuild $Mode $Architecture"
exit 0
