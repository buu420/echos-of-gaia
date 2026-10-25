param(
    [ValidateSet('Policy','StagedBuild')][string]$Mode = 'Policy',
    [string]$GameRoot,
    [string]$StageRoot
)
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $sourceRoot ('.superpowers/sdd/2026-10-10-ffix-accessible-navigation/build-isolation-' + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $scratch -Force)
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
try {
    # Run each actual target condition, substituting only its external deployment
    # side effect. Executing the original unguarded task could touch Steam.
    foreach ($relative in @('Assembly-CSharp/Assembly-CSharp.csproj','Memoria.Prime/Memoria.Prime.csproj','Memoria.ScreenReader/Memoria.ScreenReader.csproj','UnityEngine.UI/UnityEngine.UI.csproj')) {
        Write-Host "Evaluate deployment policy: $relative"
        [xml]$project = Get-Content -LiteralPath (Join-Path $sourceRoot $relative)
        $targets = @($project.SelectNodes('//*[local-name()="Target"][*[local-name()="Deploy"]]'))
        if ($targets.Count -ne 1) { throw "Unexpected deployment targets in $relative" }
        $target = $targets[0]
        $deploy = $target.SelectSingleNode('*[local-name()="Deploy"]')
        $sentinel = Join-Path $scratch 'installation-was-modified.txt'
        $probePath = Join-Path $scratch 'deployment-probe.proj'
        $props = [System.Security.SecurityElement]::Escape((Join-Path $sourceRoot 'Directory.Build.props'))
        $targetCondition = [System.Security.SecurityElement]::Escape($target.GetAttribute('Condition'))
        $taskCondition = [System.Security.SecurityElement]::Escape($deploy.GetAttribute('Condition'))
        $escapedSentinel = [System.Security.SecurityElement]::Escape($sentinel)
        @"
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$props" />
  <Target Name="Probe" Condition="$targetCondition">
    <WriteLinesToFile File="$escapedSentinel" Lines="deployment" Overwrite="true" Condition="$taskCondition" />
  </Target>
</Project>
"@ | Set-Content -LiteralPath $probePath -Encoding UTF8
        & $msbuild $probePath /t:Probe /nologo /v:quiet
        if ($LASTEXITCODE -ne 0) { throw "Could not evaluate $relative" }
        if (Test-Path -LiteralPath $sentinel) { throw "Unguarded deployment target: $relative changed a fake installation with default properties" }
        & $msbuild $probePath /t:Probe /p:EnableGameDeployment=true /nologo /v:quiet
        if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $sentinel)) { throw "Explicit opt-in is not propagated by $relative" }
        Remove-Item -LiteralPath $sentinel
    }
    . (Join-Path $sourceRoot 'tools/FF9Access.Paths.ps1')
    foreach ($paths in @(@('C:\game','C:\game'),@('C:\game','C:\game\stage'),@('C:\game\installed','C:\game'))) {
        $rejected = $false
        try { Assert-FF9SeparatePaths -GameRoot $paths[0] -OtherRoot $paths[1] } catch { $rejected = $true }
        if (!$rejected) { throw "Overlapping stage/game paths were allowed: $paths" }
    }
    Assert-FF9SeparatePaths -GameRoot 'C:\game' -OtherRoot 'C:\game-stage'
    if ($Mode -eq 'StagedBuild') {
        if (!$GameRoot -or !$StageRoot) { throw 'StagedBuild requires GameRoot and StageRoot' }
        Write-Host 'Read installed binary inventory'
        $files = @(Get-ChildItem -LiteralPath $GameRoot -File -Recurse | Where-Object { $_.Extension -in '.dll','.exe' })
        Write-Host "Hash $($files.Count) installed binaries"
        $before = @{}
        foreach ($file in $files) { $before[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
        Write-Host 'Start guarded staged compilation'
        & (Join-Path $sourceRoot 'tools/Build-FF9Access.ps1') -GameRoot $GameRoot -StageRoot $StageRoot -Mode All
        if ($LASTEXITCODE -ne 0) { throw 'Staged compilation failed' }
        $guardAssembly = [System.Security.SecurityElement]::Escape((Join-Path $StageRoot 'BuildTools/Memoria.MSBuild.dll'))
        $taskProbe = Join-Path $scratch 'compiled-task-probe.proj'
        @"
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <UsingTask AssemblyFile="$guardAssembly" TaskName="Memoria.MSBuild.Deploy" />
  <Target Name="Probe"><Deploy SolutionDir="unused" TargetPath="unused" TargetDir="unused" TargetName="unused" /></Target>
</Project>
"@ | Set-Content -LiteralPath $taskProbe -Encoding UTF8
        & $msbuild $taskProbe /t:Probe /nologo /v:minimal
        if ($LASTEXITCODE -ne 0) { throw 'Compiled deployment task failed to refuse default deployment' }
        foreach ($arch in @('x86','x64')) {
            $expected = if ($arch -eq 'x86') { 'X86' } else { 'Amd64' }
            foreach ($name in @('Assembly-CSharp','Memoria.Prime','Memoria.ScreenReader','XInputDotNetPure','UnityEngine.UI')) {
                $assemblyPath = Join-Path $StageRoot "Game/$arch/$name.dll"
                $actual = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath).ProcessorArchitecture.ToString()
                if ($actual -ne $expected) { throw "Staged $arch $name architecture: expected $expected, got $actual" }
            }
        }
        foreach ($file in $files) {
            if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $before[$file.FullName]) { throw "Installed file changed: $($file.FullName)" }
        }
        "Unchanged installed PE files: $($files.Count)"
    }
    'PASS BuildIsolation'
} finally {
    $resolvedScratch = [IO.Path]::GetFullPath($scratch)
    $ownedPrefix = [IO.Path]::GetFullPath((Join-Path $sourceRoot '.superpowers/sdd/2026-10-10-ffix-accessible-navigation/'))
    if ($resolvedScratch.StartsWith($ownedPrefix,[StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $resolvedScratch -Recurse -Force }
}
