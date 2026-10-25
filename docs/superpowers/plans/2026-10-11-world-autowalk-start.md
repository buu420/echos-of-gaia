# FFIX world autowalk startup implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task by task.

**Goal:** Reduce the delay between requesting an automatic world route and movement starting.

**Architecture:** Preserve FFIX's native transport and segment checks. Index candidate triangles in native order, prepare immutable geometry on a bounded worker ahead of navigation requests, and reuse it only for the same live geometry identity.

**Tech Stack:** Unity 5.2.3p2, .NET 3.5 / original Mono 3.0.40818, C#, Windows MSBuild, native Prism speech.

**Spec:** ../specs/2026-10-11-world-autowalk-start.md

## Global constraints

- Original x86 and x64 Mono runtimes; no newer framework APIs.
- Unity object access stays on the game thread; background inputs are detached managed data.
- Copy at most 24 native blocks per frame. One preparation worker and one owned route worker at most.
- Preserve native hit order, live cache priority, native terrain masks and every segment check.
- Preserve saves, settings, launcher, K/L/semicolon controls and player interaction ownership.

## Review focus

- Bounding-box tolerance and periodic seams must not omit a triangle the native probe accepts.
- Overlapping heights and special mesh rejection must retain original probe order.
- Data/rule changes must invalidate stale results without rebuilding unchanged topology for a mere context generation change.
- Preparation replacement/cancellation must keep the single-worker bound and discard obsolete output.
- Cold preparation must not move expensive indexing onto the Unity thread or repeatedly allocate an entire graph.

## Task 1: Establish the lookup regression and preserve probe semantics

Files: tests/FF9Access.Tests/WorldGeometryPerformanceTests.cs; Assembly-CSharp/Memoria/Accessibility/Navigation/Core/WorldGeometry.cs.

- [x] Add and run an old-code dense-probe benchmark with a broad latency ceiling and native linear-order equivalence cases.
- [x] Index candidate faces by periodic spatial cells, keeping native order and boundary tolerance.
- [x] Evaluate formatted adjacency keys; retain them because constructor work is detached and measured request latency is now 611 ms. Native triangle cache keys use value keys.
- [x] Run all core regressions and record before/after preparation and lookup timings.

## Task 2: Prepare and reuse detached topology

Files: Core/WorldGeometryPreparation.cs; Core/WorldGeometry.cs; tests/FF9Access.Tests/WorldGeometryPreparationTests.cs.

- [x] Test queued preparation, one-worker replacement, cancellation, failure, and fresh generation/rule/cache views.
- [x] Implement bounded background preparation and shared immutable geometry/index views.
- [x] Verify stale results cannot escape their geometry identity and request rules/cache remain privately owned.

## Task 3: Integrate early preparation and measure requests

Files: Native/NativeWorldGeometry.cs; Native/NavigationRuntime.cs; Core/OwnedWorldPlanner.cs; tests/FF9Access.Tests/NativeReaderVerification.cs; tests/FF9Access.MonoProbe/WorldPlanningProbe.cs.

- [x] Keep Unity capture on the game thread, limit capture to one slice per frame, and refresh data identity before reuse.
- [x] Warm world geometry while ordinary world control is available; pending preparation reports the existing loading state.
- [x] Record capture/build/search/request durations without changing player-facing control behavior.
- [x] Verify emitted integration and both original Mono runtimes, including background preparation.

## Task 4: Review, package, deploy and validate

Files: docs/accessibility/navigation-controls.md; docs/accessibility/live-validation.md; existing build/package/deploy tools.

- [x] Request independent code review and resolve material findings.
- [x] Commit and rebuild a complete All/Both package; run exact package checks.
- [x] Back up the current saves/settings and perform checked deployment.
- [x] Measure a live world route, preserve subsequent human gameplay, and record explicit limits. Player P started owned movement in 611 ms; further agent input stopped when human input was detected. No manual saves or combat inputs were issued by the agent.
