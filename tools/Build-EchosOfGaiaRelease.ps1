[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PackageRoot,
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [Parameter(Mandatory=$true)][string]$ReleaseRoot,
    [Parameter(Mandatory=$true)][string]$CatalogRoot,
    [string]$Version='0.1.0',
    [string]$AuthorCli='amm-author.exe',
    [string]$PythonExecutable='py'
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Assert-EchosOfGaia.ps1')
$sourceRoot=Split-Path $PSScriptRoot -Parent
$PackageRoot=Resolve-FF9Path $PackageRoot
$ReleaseRoot=Resolve-FF9Path $ReleaseRoot
Assert-FF9SeparatePaths $GameRoot $ReleaseRoot
Assert-FF9SeparatePaths $PackageRoot $ReleaseRoot
if($Version -cnotmatch '^\d+\.\d+\.\d+$'){throw 'Use a three-part release version.'}
if((Test-Path -LiteralPath $ReleaseRoot) -and @(Get-ChildItem -LiteralPath $ReleaseRoot -Force).Count -gt 0){throw 'Use a new empty release directory.'}
$package=Assert-FF9Package $PackageRoot $GameRoot
$sourceCommit=& git -C $sourceRoot rev-parse HEAD
if($LASTEXITCODE -ne 0 -or $package.SourceCommit -cne $sourceCommit){throw 'Release package must match the current source commit.'}
if(& git -C $sourceRoot status --porcelain --untracked-files=normal){throw 'Commit the reviewed release source before export.'}
$compatibility=Get-Content -LiteralPath (Join-Path $sourceRoot 'packaging/compatibility.json') -Raw | ConvertFrom-Json
Assert-EchosOfGaiaGame $GameRoot $compatibility
[void](New-Item -ItemType Directory -Path $ReleaseRoot -Force)
$standalone=Join-Path $ReleaseRoot 'standalone'
$manager=Join-Path $ReleaseRoot 'manager-source'
$release=[pscustomobject]@{Schema=1;Product='Echos of Gaia';Version=$Version;Channel='beta';SourceCommit=$sourceCommit;SteamAppId='377840';SteamBuildId='5378074'}
$toolNames=@('FF9Access.Paths.ps1','FF9Access.Deployment.ps1','Deploy-FF9Access.ps1','Restore-FF9Access.ps1','Test-FF9AccessInstall.ps1','Assert-EchosOfGaia.ps1')
foreach($destination in @($standalone,$manager)){
    [void](New-Item -ItemType Directory -Path $destination)
    Get-ChildItem -LiteralPath $PackageRoot -Force | Copy-Item -Destination $destination -Recurse
    [void](New-Item -ItemType Directory -Path (Join-Path $destination 'tools'))
    foreach($name in $toolNames){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $destination "tools/$name")}
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'packaging/compatibility.json') -Destination (Join-Path $destination 'compatibility.json')
    Write-FF9Json (Join-Path $destination 'release.json') $release
}
foreach($name in @('Install-EchosOfGaia.ps1','Restore-EchosOfGaia.ps1','README.txt')){
    Copy-Item -LiteralPath (Join-Path $sourceRoot "packaging/$name") -Destination (Join-Path $standalone $name)
}
if([version]$Version -ge [version]'0.1.3'){
    $installerBuild=Join-Path $ReleaseRoot 'installer-build'
    & (Join-Path $PSScriptRoot 'Build-FF9Installer.ps1') -OutputRoot $installerBuild
    if(!$?){throw 'Standalone installer did not build from release source.'}
    Copy-Item -LiteralPath (Join-Path $installerBuild 'Install-EchosOfGaia.exe') -Destination (Join-Path $standalone 'Install-EchosOfGaia.exe')
}
Copy-Item -LiteralPath (Join-Path $sourceRoot 'packaging/Manager-PreInstall.ps1') -Destination (Join-Path $manager 'Manager-PreInstall.ps1')
$null=Assert-EchosOfGaiaRelease $GameRoot $standalone
$null=Assert-EchosOfGaiaRelease $GameRoot $manager
$standaloneZip=Join-Path $ReleaseRoot "EchosOfGaia-v$Version-beta-windows.zip"
$managerZip=Join-Path $ReleaseRoot "EchosOfGaia-v$Version-beta-amm.zip"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$standaloneArchive=[IO.Compression.ZipFile]::Open($standaloneZip,[IO.Compression.ZipArchiveMode]::Create)
try{
    foreach($file in Get-ChildItem -LiteralPath $standalone -File -Recurse){
        # Windows PowerShell's Framework ZipFile helper can emit backslashes.
        # Use canonical ZIP paths explicitly for portable, unambiguous extraction.
        $relative=$file.FullName.Substring($standalone.Length+1).Replace('\','/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($standaloneArchive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)
    }
}finally{$standaloneArchive.Dispose()}
& $AuthorCli package build --source $manager --game final-fantasy-ix --version $Version --target-platform windows --output $managerZip --project $CatalogRoot --json
if($LASTEXITCODE -ne 0){throw 'Amethyst package build failed.'}
$archive=[IO.Compression.ZipFile]::Open($managerZip,[IO.Compression.ZipArchiveMode]::Update)
try{
    $entry=$archive.GetEntry('manifest.json')
    $reader=New-Object IO.StreamReader($entry.Open())
    try{$manifest=$reader.ReadToEnd() | ConvertFrom-Json}finally{$reader.Dispose()}
    if($manifest.gameId -cne 'final-fantasy-ix' -or $manifest.pluginId -cne 'buu420' -or $manifest.modVersion -cne $Version -or $manifest.preInstall.executable -cne 'files/Manager-PreInstall.ps1' -or !$manifest.preInstall.failureFatal -or !$manifest.preInstall.runOnUpdate -or $manifest.preInstall.installToGameFolder -or $manifest.preInstall.runFromGameFolder -or $manifest.preInstall.needsAdmin -or $manifest.postInstall -or $manifest.postUninstall){throw 'Unexpected manager identity or read-only lifecycle contract.'}
    # All runtime writes belong to the manager's receipts. The package layout and
    # validation tools are transient staging inputs, never game-folder actions.
    $manifest.installActions=@($package.Files | ForEach-Object {[pscustomobject]@{type='copyFile';source=('Payload/'+$_.Path);target=$_.Path}})
    $manifest.verify=@($package.Files | ForEach-Object {[pscustomobject]@{type='hashEquals';path=$_.Path;sha256=$_.SHA256.ToLowerInvariant()}})
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes(($manifest | ConvertTo-Json -Depth 100))
    $stream=$entry.Open()
    try{$stream.SetLength(0);$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}
}finally{$archive.Dispose()}
& $AuthorCli package validate --zip $managerZip --plugin buu420 --game final-fantasy-ix --version $Version --json
if($LASTEXITCODE -ne 0){throw 'Amethyst package validation failed.'}
$checksums=@($standaloneZip,$managerZip) | ForEach-Object { (Get-FF9Hash $_).ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_) }
[IO.File]::WriteAllText((Join-Path $ReleaseRoot 'SHA256SUMS.txt'),($checksums -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
& $PythonExecutable (Join-Path $PSScriptRoot 'verify_echos_release.py') --directory $ReleaseRoot --version $Version
if($LASTEXITCODE -ne 0){throw 'Independent release archive verification failed.'}
Write-Host "PASS Echos of Gaia $Version beta: source $sourceCommit; $($package.Files.Count) runtime files in each archive."
