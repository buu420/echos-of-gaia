# Double-click standalone installation

The user wants people installing the GitHub ZIP to avoid opening PowerShell or
typing commands. Their standing instruction is to proceed without further
approval requests. This design records implementation choices made by the
maintainer, rather than presenting them as choices the user explicitly selected.

## Approach

Add `Install-EchosOfGaia.exe`, a .NET Framework 4.8 Windows Forms application,
beside the existing standalone scripts. Native labelled controls expose a game
folder, Browse, Install, status, a readable log, and Close. Detect existing Steam
libraries when possible; let the user correct the path. A command-file wrapper
would remove the manual shell launch but retain a console-only interaction.
Replacing the transactional installer would duplicate preservation logic. The
chosen window reuses that installer instead.

The executable runs `Install-EchosOfGaia.ps1` in a hidden Windows PowerShell
child, passing the selected folder as data. Read both output streams without
blocking the UI, report the real exit code, and retain the log locally. A failed
installation must never be reported as successful. Keep the window open while a
transaction is running. The executable uses the current user's permissions;
Windows' Run as administrator remains available when a protected game folder
requires it. No game is launched by the installer.

A named Windows mutex excludes another instance of this standalone installer
for the same normalized game folder until its child has exited. This does not
coordinate unrelated command-line or mod-manager transactions. File-log failures
leave the in-memory output readable and cannot prevent restoring the controls.
Path values are independently base64-encoded and decoded into variables before
the fixed PowerShell command invokes the existing script.

## Constraints and scope

- Windows 10/11 x64; .NET Framework 4.8 and the existing game prerequisites.
- Standard keyboard traversal, explicit accessible names, native Browse dialog,
  and a focusable read-only status/log. Completion and errors are announced by
  moving focus to status; never claim audible NVDA confirmation from UI tests.
- Validate the package and game with existing scripts before deployment. Preserve
  existing transaction, rollback, settings, save, and unrelated-file behavior.
- Quote apostrophes, spaces, Unicode, dollar signs and shell metacharacters as
  literal data. Never invoke cmd.exe for installation.
- Add the EXE only to the standalone archive root, outside runtime payload and
  manager install actions. Keep all 1,897 payloads and 608 recordings unchanged.
- Publish version 0.1.3, retaining immutable 0.1.2 assets and all prior catalog
  rows. Update installation instructions and verify hosted assets and catalog.
- No game navigation, speech, narration, controls, or engine behavior changes.

## Verification

Exercise actual Windows controls and the real hidden process runner with tiny
temporary scripts: success, nonzero exit, paths containing special characters,
large simultaneous stdout/stderr, and missing package inputs. Test Steam
library discovery against temporary VDF/appmanifest fixtures. Run archive and
existing source regressions, validate emitted packages, and perform a guarded
local installation with before/after preservation evidence. Affected tester-PC
speech, full gameplay, physical controller operation and user listening remain
separate, unperformed checks.
