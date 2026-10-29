Echos of Gaia 0.1.2 beta
Final Fantasy IX accessibility mod by buu420

Supported: 64-bit Windows 10/11, Windows Steam FFIX app 377840 build 5378074.
Microsoft .NET Framework 4.8+ and Visual C++ v14 14.51+ x86/x64 are required.
Official Microsoft prerequisite downloads (install both C++ architectures):
https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170
https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48

Close FFIX and its launcher. Extract this ZIP outside the game folder. Open
PowerShell here and run:

powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-EchosOfGaia.ps1 -GameFolder 'YOUR FINAL FANTASY IX FOLDER'

Start your screen reader, then launch normally through Steam. The Memoria
launcher's Accessibility tab contains speech testing, navigation bindings,
description volume and positional-sound settings. Continue loads the automatic
checkpoint; use Load to select a manual save.

U/O: categories. K/semicolon: targets. L: status. I: spoken guidance.
P: automatic walking on/off. R: repeat. Shift+I/Shift+P: guidance interval.
Hold either L2/LT or R2/RT for controller navigation: D-pad left/right changes
categories, up/down selects targets, L3 starts spoken guidance, R3 starts
automatic travel, and Circle/B stops. Release the trigger after choosing.
Use the other trigger or keyboard if a trigger is mapped to a core game action.
In Hot and Cold, the configured status stick click reads the minigame's status.
Use normal game controls to interact, choose dialogue and confirm actions.

The installer prints a rollback manifest under:
%LOCALAPPDATA%\EchosOfGaia\Backups
Keep that path and this extracted release. To undo one standalone deployment,
close the game and run:

powershell -NoProfile -ExecutionPolicy Bypass -File .\Restore-EchosOfGaia.ps1 -GameFolder 'YOUR GAME FOLDER' -BackupManifest 'THE PRINTED BACKUP-MANIFEST.JSON'

Originals are backed up. Changed files are preserved and reported for review.
For an interrupted installation, retain the newest backup manifest and use
the same restore command. For a manager installation, use the manager's own
update and uninstall operations.

Detailed controls and coverage: Payload\accessibility\docs\navigation-controls.md
Narration coverage: Payload\accessibility\docs\audio-descriptions.md
Dependency notices: Payload\accessibility\memoria-support\THIRD-PARTY-NOTICES.md

The 608 recordings include all 42 reviewed original FMVs, generic field cues
and 112 additional Disc 1 recordings. Field coverage is selective. Some
coordinated actions, props and optional branches remain gaps. Complete Disc 1
coverage, a complete playthrough, physical controller coverage and all-cue
in-game listening/synchronization remain unverified.

Other mods replacing the same managed assemblies need compatibility testing.
Modified FMVs can fail the original-footage narration hash checks. Use Echos
of Gaia releases to update this engine; its stock Memoria updater is disabled.
Diagnostics: %LOCALAPPDATA%\FF9Access (speech.log and navigation.log).
Open that path with Windows+R. Memoria.log is in the main game folder.
For speech problems, include speech.log and Memoria.log in your report.
0.1.2 retries rejected Prism output through the bundled compatibility backend
and records its native error code. Affected-PC speech and the reported
Alt-Tab cutoff still need listener confirmation.

Source, current releases, prerequisite links and reports:
https://github.com/buu420/echos-of-gaia
https://github.com/buu420/echos-of-gaia/releases/tag/v0.1.2

Based on BlindGuyNW/Memoria and Albeoris/Memoria. The code retains the MIT
license; dependency and supplied sound notices remain separate. Final Fantasy
IX belongs to Square Enix. This is an independent community mod.
