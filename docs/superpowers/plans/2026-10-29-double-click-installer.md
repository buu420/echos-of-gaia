# Double-click Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Install the GitHub standalone ZIP through an accessible executable,
without requiring users to open a shell or type commands.

**Architecture:** A small Windows Forms application locates Steam FFIX and runs
the existing transactional script in a hidden process. The standalone archive
includes this application; manager ownership and game payloads stay unchanged.

**Tech Stack:** C#, .NET Framework 4.8, Windows Forms, Windows PowerShell 5.1,
existing PowerShell and Python packaging tools.

**Spec:** `docs/superpowers/specs/2026-10-29-double-click-installer-design.md`

## Global Constraints

- Preserve all 1,897 runtime payloads and 608 recordings; no game-code changes.
- Expose native keyboard-accessible controls and exact installation errors.
- Never launch FFIX, terminate a transaction, or interpolate paths into shell syntax.
- Release 0.1.3 without modifying existing published release assets/history.
- Keep current backups, saves, settings and unrelated game files intact.

## Review Focus

- Selected paths with apostrophes, Unicode and shell metacharacters stay literal.
- Heavy stderr and stdout cannot deadlock the child process or GUI thread.
- Invalid folders, missing package helpers and nonzero exit codes remain errors.
- Closing or pressing Install again cannot interrupt or duplicate a transaction.
- Steam library discovery must return an existing FFIX folder, never a guessed
  unrelated installation; users can always enter a path manually.

### Task 1: Accessible installer and real-process checks

**Files:** Create `EchosOfGaia.Installer/{Program,InstallerForm,InstallerService,SteamLocator}.cs`,
`EchosOfGaia.Installer/EchosOfGaia.Installer.csproj`,
`tests/Installer.Tests/{Program.cs,Installer.Tests.csproj}`,
`tools/{Build-FF9Installer,Test-FF9Installer}.ps1`.

**Interfaces:** `InstallerService.RunAsync(packageRoot, gameFolder, Action<string>)`
returns `Task<int>`; `SteamLocator.FindGameFolder(IEnumerable<string>)` returns a
real folder or null. `InstallerForm` consumes those operations and exposes normal
Windows accessibility peers.

- [ ] Write and run tests that reject missing UI/project functionality first.
- [ ] Implement native controls, Steam discovery and hidden process execution.
- [ ] Run tests for special paths, exit status, output drainage, missing inputs,
      actual UI names, tab order, progress focus and busy-close protection.
- [ ] Commit the tested implementation and record results in the private ledger.

### Task 2: Package, document and review

**Files:** Modify `tools/Build-EchosOfGaiaRelease.ps1`,
`tools/verify_echos_release.py`, `tests/test_release_archives.py`,
`.github/workflows/build.yml`, `README.md`, `packaging/README.txt`,
`packaging/Install-EchosOfGaia.ps1`, `packaging/amm/final-fantasy-ix-game.json`.

**Interfaces:** Standalone archive root contains `Install-EchosOfGaia.exe` for
0.1.3 and later. Manager files/actions and runtime manifests exclude it.

- [ ] Write failing archive tests for missing/empty EXE and installer leakage into
      payloads, preserving validation of older release layouts.
- [ ] Build the installer from the exact release source and include it in the
      standalone ZIP. Update versions, instructions, script stage messages and CI.
- [ ] Run all appropriate source/preflight/archive/installer checks.
- [ ] Obtain fresh whole-change review; fix material findings with regression
      evidence, then commit the reviewed source.

### Task 3: Build, preserve, deploy and publish

**Files:** Public checkout exports, release evidence, catalog new row only.

**Interfaces:** Both 0.1.3 archives share the same runtime payload; GitHub assets
and the catalog row carry their verified SHA256 and source commit.

- [ ] Export reviewed leaf changes into clean public source; verify tree identity.
- [ ] Build both game architectures from final public source; compare runtime
      bytes with 0.1.2 and package/verify both release archives.
- [ ] Capture local preservation inputs, deploy using the executable's real
      service and script, wait for process exit, and independently verify results.
- [ ] Publish GitHub prerelease and Amethyst update using existing authorization.
- [ ] Download anonymously, verify public source/assets/catalog and CI; report
      exact double-click steps and unperformed live-listening/gameplay checks.
