# Local FFIX accessibility installation

The package contains the maintained launcher, both architecture-specific managed game builds, Prism, matching Tolk/NVDA fallback libraries, notices and source for the fallback. It is a local `ff9-access-local` build with accessibility API 1. The launcher does not apply the stock Memoria updater to this build.

Close FFIX and its launcher. Build and package outside the game directory. In PowerShell, run the tools with explicit absolute paths:

```powershell
& D:\FF9Access\source\tools\Build-FF9Access.ps1 -GameRoot 'D:\SteamLibrary\steamapps\common\FINAL FANTASY IX' -StageRoot D:\FF9Access\staging\development -Mode All -Architecture Both
& D:\FF9Access\source\tools\Package-FF9Access.ps1 -GameRoot 'D:\SteamLibrary\steamapps\common\FINAL FANTASY IX' -StageRoot D:\FF9Access\staging\development -PackageRoot D:\FF9Access\packages\reviewed
& D:\FF9Access\source\tools\Deploy-FF9Access.ps1 -GameRoot 'D:\SteamLibrary\steamapps\common\FINAL FANTASY IX' -PackageRoot D:\FF9Access\packages\reviewed -BackupRoot D:\FF9Access\backups
```

Use a new empty package folder for each build. If Windows PowerShell blocks these local scripts, `powershell -NoProfile -ExecutionPolicy Bypass -File ...` applies the policy only to that process. No machine policy change is required.

Deployment checks every payload hash, native architecture and direct import, required dependencies and destination paths. It records and verifies all originals before replacement. It preserves original Unity executables, firstpass/UnityEngine, native game plugins, assets and saves, and hashes all other installation files except logs/temp files. New navigation options are merged into Memoria.ini; existing values, legacy interval, icon/cage and other preferences are retained. A failed deployment restores the files it changed.

`accessibility/installed-build.json` records the source commit, external package manifest and exact backup manifest. Copy its BackupManifest value into:

```powershell
& D:\FF9Access\source\tools\Restore-FF9Access.ps1 -GameRoot 'D:\SteamLibrary\steamapps\common\FINAL FANTASY IX' -BackupManifest 'D:\FF9Access\backups\TIMESTAMP-ID\backup-manifest.json'
```

Rollback restores original bytes and removes files created by that deployment. It preserves unrelated files and settings added afterwards. If an installed binary changed afterwards, rollback stops before overwriting it; the originals remain in the backup. Changed navigation preferences are also preserved. Empty package-created directories may remain.

Every changed file has its expected new hash and recoverable write intent flushed to an atomic manifest before replacement. Payloads and merged settings are copied to transaction-owned temporary files, flushed, and replaced atomically. A restore accepts either the original or expected replacement after interruption; it rejects unjournaled changes instead of reporting a false success.

Temporary fixture tests cover both architectures, rejected missing imports, backup before replacement, caught copy failures, abrupt child-process interruption, preserved preferences, legacy interval conversion, protected assets/saves, exact rollback and unrelated later changes. They do not start the game. Live validation has separate evidence.
