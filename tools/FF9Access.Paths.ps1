function Resolve-FF9Path([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or ![IO.Path]::IsPathRooted($Path)) { throw 'An absolute path is required' }
    [IO.Path]::GetFullPath($Path).TrimEnd([char[]]@('\','/'))
}
function Assert-FF9SeparatePaths([string]$GameRoot,[string]$OtherRoot) {
    $game = Resolve-FF9Path $GameRoot
    $other = Resolve-FF9Path $OtherRoot
    if ($game.Equals($other,[StringComparison]::OrdinalIgnoreCase) -or
        $game.StartsWith($other + '\',[StringComparison]::OrdinalIgnoreCase) -or
        $other.StartsWith($game + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Game and staging/backup paths must not overlap' }
    # Existing directory junctions must not bypass the separation check.
    foreach ($path in @($game,$other)) {
        $parent = $path
        while ($parent) {
            if (Test-Path -LiteralPath $parent) {
                $item = Get-Item -LiteralPath $parent -Force
                if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse-point directory is not an installation/staging root: $parent" }
            }
            $parent = Split-Path $parent -Parent
        }
    }
}
