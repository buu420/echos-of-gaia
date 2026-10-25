# Forest interaction approaches implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this repair inline. Use a read-only reviewer after verification.

**Goal:** Let Forest navigation reach Mene's native ground interaction and a reachable viewing point for the rendered noninteractive object.

**Architecture:** Retain observed actor identities and positions. Bind the known Forest scripts to a separate native interaction region; generate safe surface approaches only when a route is requested. Read the actual quad selection and shown bubble before announcing readiness. A noninteractive object receives an explicitly announced viewing point on connected geometry.

**Tech stack:** Existing C# Unity/Mono engine integration, detached navigation core, .NET 8 regression harness, PowerShell dual-runtime packaging.

**Spec:** The user's report that Forest People and Objects cannot be navigated to; captured routes at 16:19 UTC, native actor 9 and quad 19 scripts, and actor 11's rendered model without a talk handler.

## Constraints and review focus

- Preserve observed-only actor presentation, last-seen snapshots, existing keys, saves and settings.
- Keep full native radius, portal, height and surface checks. Never add a connection to the raised mesh.
- Confirm remains player controlled; no script requests, timer, treasure or physics writes.
- Bind only the verified Forest scene, actor models and script fingerprints; unsupported scripts retain existing behavior.
- Replaced, hidden, changed or stale targets must not reuse an interaction region.
- A different selected quad or missing bubble must not announce ready to interact.
- Noninteractive object arrival must describe a viewing point, never imply a usable interaction.
- Approach calculation runs on a route request, with no new per-frame mesh search or asset load.

### Task 1: Safe Forest approaches and interaction readiness

**Files:** Core NavigationTargets, FieldTargetCatalog, FieldActorApproaches, FieldRoutePlanner, RoutePlan and NavigationCoordinator; Native NativeFieldTargets and NativeInteractionProbe; EventCollision read-only quad helper; ForestRoutesTests and captured fixture.

- [x] Preserve the complete native geometry from the failed capture and write failing Mene/object route regressions with the verified quad polygon.
- [x] Add script-scoped binding metadata and snapshot copying. Calculate safe candidates inside the native talk region and connected viewing points without relaxing mesh checks.
- [x] Add read-only native quad readiness, validated against actual selection, native talkability and presented bubble.
- [x] Verify captured route legs, native disconnection, false readiness, observed-target retention and honest viewing-point speech.
- [x] Run the complete core suite, build x86/x64, emitted integration checks, original Mono probes and launcher checks.
- [x] Obtain a read-only review and resolve material findings.
- [ ] Commit the verified product, package it, snapshot current saves/settings after normal game closure, deploy, and separately verify installed hashes and preservation.
- [ ] Ask for a Forest route test and continue the Hot and Cold reader test once Mene is reachable.

### Follow-up from the live Forest test

Mene's ground interaction succeeded in the October 12 session. The new object
capture at 17:40 UTC starts on triangle 100 within wall clearance. Stationary
clearance rejects it, so all object candidates fail before departure. Native
RadiusValid applies push-away forces and uses low flag 0x80 as visitation
scratch, rather than a physical permission bit.

- [x] Retain the second native capture and reproduce the failed departure.
- [x] Permit only receding initial contacts that reach full clearance on the
  first support leg; add an inward corridor point without changing the radius.
- [x] Exclude only low visitation flag 0x80 from fingerprints and test that the
  high-byte blocking attribute still changes the fingerprint.
- [x] Verify nine Forest cases and the complete 285-case core suite.
- [ ] Review the changed geometry rules, verify both exact runtimes, commit,
  package, deploy after normal closure and preserve the player's newest saves.
- [ ] Repeat the live object route from the captured position.
