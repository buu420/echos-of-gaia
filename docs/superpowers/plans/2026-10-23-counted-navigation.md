# Counted navigation implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement the two tasks in this session.

**Goal:** Replace confusing native-unit guidance with short instructions for each validated route leg.

**Architecture:** A pure instruction formatter consumes the existing context,
selected target and movement observation. The existing announcer tracks the last
spoken leg and bypasses its reminder interval when that leg changes. The native
runtime supplies world control orientation without changing movement.

**Tech Stack:** C#, original Unity Mono/.NET 3.5, .NET 8 offline tests, Windows MSBuild/PowerShell.

**Spec:** ../specs/2026-10-23-counted-navigation-design.md

## Global constraints

- Counts are fixed distance markers: 240 field units or 4 world units.
- Preserve controls, path geometry, native movement and interaction gates, auto-walk, HRTF cues, audio descriptions, settings and saves.
- Never automate Confirm, boarding, landing, choices or combat.
- Ordinary speech and dialogue retain priority.

## Review focus

- A tiny bend must not disappear merely because it rounds below one count.
- A final sub-count distance must retain a direction until the approach is reached.
- Native auto-walk waypoints and passive guidance must describe their own current leg.
- Target/transport changes or pending planning must not reuse an old route instruction.
- Busy speech must defer a turn without losing it or interrupting unrelated narration.

### Task 1: Counted route instructions and turn scheduling

**Files:** Navigation/Core/NavigationAnnouncements.cs; new Navigation/Core/RouteInstruction.cs;
Navigation/Core/NavigationModels.cs; Navigation/Native/NavigationRuntime.cs;
tests/FF9Access.Tests/AnnouncementTests.cs, IntegrationTests.cs.

**Interfaces:** RouteInstruction.Resolve(NavigationUpdate) returns text, direction,
route-leg identity and approach status. Existing NavigationAnnouncements.Status
and Format remain the player-facing interface. WorldControlType defaults to
unknown (-1); type 0 uses InputOrientationDegrees supplied from the real camera.

- [x] Write independent literal expectations for a 480-unit left leg followed by
  a 1200-unit up leg: "left 2", then "up 5" at the corner. Run RED.
- [x] Add regressions for countdown, collinear runs, preserved short bends,
  rotated field controls, camera-relative world travel and wrapped distances,
  no false arrival, no-route bearing and pending planning.
- [x] Implement the formatter and world presentation snapshot.
- [x] Add regressions for a turn before the interval, repeated same leg, busy
  own/unrelated speech, status consumption, gate/reset, new target/route/context.
- [x] Implement scheduling, run focused tests and the full core suite GREEN.
- [x] Update controls documentation and commit the complete behavior.

### Task 2: Validate and install

**Files:** research/2026-10-23/counted-navigation evidence outside checkout;
new stage/package directories; installed game payload via deployment script.

**Interfaces:** Existing Build/Test-FF9MonoSpeech/Package/Deploy scripts and their manifests.

- [ ] Obtain one independent review of the complete diff; resolve important
  findings with a reproducing test and green suite.
- [ ] Build All/Both and run original-Mono probes, existing integration tests,
  launcher checks and emitted artifact verification.
- [ ] Snapshot saves, settings and narration; package clean committed source.
- [ ] Deploy with the normal rollback installer; wait for exit before opening
  its manifests. Verify payload and user-data preservation.
- [ ] Report new behavior/controls and the limit of live validation.
