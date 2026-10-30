# Quiet Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Replace debug map chatter and repeated controller tutorials with concise, state-backed speech.

**Architecture:** Guard and deduplicate PlayerWindow title writes. Read native field names in the existing context adapter shared by runtime and integration checks. Keep controller opening/help text in the coordinator rather than recomposing it in the runtime.

**Tech Stack:** C#, .NET 3.5/8, Windows API, existing PowerShell packaging and deployment.

**Spec:** docs/superpowers/specs/2026-10-30-quiet-navigation-design.md

## Global Constraints

- No hidden-state labels, input injection, automatic Confirm or changed movement rules.
- Preserve debug titles when screen-reader output is disabled.
- Help uses held trigger plus Triangle/Y; bare button keeps native behavior.
- Preserve saves, settings, unrelated game files and recordings during deployment.
- Source/package checks do not prove audible NVDA or physical-controller behavior.

## Review Focus

- Camera/FMV changes must not restore debug titles while speech is enabled.
- Missing or late location text must not produce a raw-number fallback.
- Rejected speech must not consume the new-location announcement.
- Help on remapped or held face buttons must not leak native Confirm/Menu.
- Repeated trigger use, map changes and route pauses must stay concise.

### Task 1: Quiet controller opening and explicit help

**Files:** Navigation/Core/NavigationControls.cs, HeldTriggerNavigation.cs, NavigationCoordinator.cs; Navigation/Native/NavigationRuntime.cs; tests/FF9Access.Tests/ControllerChordTests.cs.

**Interfaces:** Append NavigationCommand.Help; coordinator Tick accepts controller naming before formatting; existing input broker retains capture and edge semantics.

- [ ] Add failing behavioral tests for repeated opening, category/target preservation, device-neutral pause, help edge/no movement/capture, bare/both-trigger behavior and PlayStation/Xbox naming.
- [ ] Run focused suite and confirm the intended failures.
- [ ] Remove automatic tutorial, format opening once in coordinator, add explicit top-face help and pass configured naming into Tick.
- [ ] Run core suite and commit.

### Task 2: Stable title and readable map changes

**Files:** Memoria/Application/PlayerWindow.cs; Navigation/Native/NativeNavigationContext.cs; NavigationRuntime.cs; NavigationAnnouncements.cs; tests/NavigationContextIntegration.Tests; tests/FF9Access.Tests/AnnouncementTests.cs.

**Interfaces:** NativeNavigationContext.Read() sets native localized field CurrentLocation and optional CurrentRegion. PlayerWindow.SetTitle retains its public signature.

- [ ] Add failing real-adapter/title tests using only engine/Windows boundary fakes; add field name dedup, busy/dialogue gates, preference, late text and rejection retry cases.
- [ ] Confirm red failures before production changes.
- [ ] Stabilize/deduplicate title writes; share native name lookup; acknowledge location speech only after acceptance.
- [ ] Run core, context, menu, narration and Python suites and commit.

### Task 3: Review, package, deploy and publish tester update

**Files:** navigation-controls.md, README/version release text and reviewed release export; external evidence/checkpoints.

- [ ] Update controls documentation. Obtain one fresh read-only whole-change review and resolve important findings with failing tests first.
- [ ] Build both architectures and run original-Mono compatibility checks and package verification.
- [ ] Snapshot protected files; transactionally deploy without polling active manifests; verify after process exit.
- [ ] Export reviewed changes to clean public source, build exact-source 0.1.4 ZIPs, publish GitHub beta and append catalog row preserving concurrent entries.
- [ ] Verify anonymous assets, catalog and CI; report the recipient-PC/controller listening limit.
