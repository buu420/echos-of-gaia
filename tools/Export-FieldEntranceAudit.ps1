param([Parameter(Mandatory=$true)][string]$GameRoot,[Parameter(Mandatory=$true)][string]$Output,[string]$UnityPyRoot='D:\FF9Access\research\2026-10-10\tools\unitypy')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FF9Access.Paths.ps1')
Assert-FF9SeparatePaths -GameRoot $GameRoot -OtherRoot (Split-Path -Parent ([IO.Path]::GetFullPath($Output)))
& py -3 (Join-Path $PSScriptRoot 'Export-FieldEntranceAudit.py') --game-root $GameRoot --output $Output --unitypy-root $UnityPyRoot
exit $LASTEXITCODE
