. (Join-Path $PSScriptRoot 'FF9Access.Deployment.ps1')

function Assert-EchosOfGaiaGame([string]$GameRoot, $Compatibility) {
    $GameRoot = Resolve-FF9Path $GameRoot
    if ($Compatibility.Schema -ne 1 -or $Compatibility.SteamAppId -cne '377840' -or $Compatibility.SteamBuildId -cne '5378074') {
        throw 'Unsupported game compatibility identity.'
    }
    $required = @('x86/FF9.exe','x64/FF9.exe','x86/FF9_Data/Managed/UnityEngine.dll','x64/FF9_Data/Managed/UnityEngine.dll','x86/FF9_Data/Managed/Assembly-CSharp-firstpass.dll','x64/FF9_Data/Managed/Assembly-CSharp-firstpass.dll')
    $seen = @{}
    foreach ($file in @($Compatibility.Files)) {
        if ($required -cnotcontains $file.Path -or $seen.ContainsKey($file.Path)) { throw 'Unapproved compatibility path.' }
        $seen[$file.Path] = $true
        if ($file.SHA256 -cnotmatch '^[0-9A-Fa-f]{64}$' -or $file.Bytes -le 0) { throw 'Invalid original-game fingerprint.' }
    }
    if ($seen.Count -ne $required.Count) { throw 'A complete original-game compatibility set is required.' }
    foreach ($file in $Compatibility.Files) {
        $path = Get-FF9OwnedPath $GameRoot $file.Path
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -ne $file.Bytes -or (Get-FF9Hash $path) -ine $file.SHA256) {
            throw "Unsupported or changed game file: $($file.Path). This beta supports Windows Steam FFIX build 5378074. Restore the original game files or report the new build before installing."
        }
    }
}

function Assert-EchosOfGaiaRelease([string]$GameRoot, [string]$PackageRoot) {
    $GameRoot = Resolve-FF9Path $GameRoot
    $PackageRoot = Resolve-FF9Path $PackageRoot
    Assert-FF9SeparatePaths $GameRoot $PackageRoot
    Assert-FF9NotRunning $GameRoot
    if (![Environment]::Is64BitOperatingSystem -or [Environment]::OSVersion.Version.Major -lt 10) { throw 'Echos of Gaia requires 64-bit Windows 10 or Windows 11.' }
    $netRelease = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release -ErrorAction SilentlyContinue).Release
    if (!$netRelease -or $netRelease -lt 528040) { throw 'Install Microsoft .NET Framework 4.8 or newer before installing Echos of Gaia.' }
    foreach ($architecture in @('x86','x64')) {
        $available = $false
        foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64,[Microsoft.Win32.RegistryView]::Registry32)) {
            $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,$view)
            try {
                $key = $base.OpenSubKey("SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\$architecture")
                if ($key) { try { if ($key.GetValue('Installed') -eq 1 -and $key.GetValue('Major') -eq 14 -and $key.GetValue('Minor') -ge 51) { $available = $true } } finally { $key.Dispose() } }
            } finally { $base.Dispose() }
        }
        if (!$available) { throw "Install Microsoft Visual C++ v14 14.51 or newer ($architecture). Download links are in README.txt." }
    }
    $compatibility = Get-Content -LiteralPath (Get-FF9OwnedPath $PackageRoot 'compatibility.json') -Raw | ConvertFrom-Json
    Assert-EchosOfGaiaGame $GameRoot $compatibility
    $release = Get-Content -LiteralPath (Get-FF9OwnedPath $PackageRoot 'release.json') -Raw | ConvertFrom-Json
    $package = Assert-FF9Package $PackageRoot $GameRoot
    if ($release.Schema -ne 1 -or $release.Product -cne 'Echos of Gaia' -or $release.Version -cnotmatch '^\d+\.\d+\.\d+$' -or $release.Channel -cne 'beta' -or $release.SourceCommit -cne $package.SourceCommit) {
        throw 'Release identity does not match the verified package.'
    }
    return $package
}
