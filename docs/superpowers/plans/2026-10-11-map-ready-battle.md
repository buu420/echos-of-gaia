# FFIX map readiness and battle overhead implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans inline; one fresh whole-change reviewer before installation.

**Goal:** Remove post-control topology preparation and unnecessary battle input work while preserving displayed information.

**Architecture:** The native block loader supplies detached batches to one incremental indexing worker. One pure retained topology can be rebound after full content validation; live Unity references are released on teardown. Numeric edge keys replace allocation-heavy strings.

**Tech Stack:** C#, Unity 5.2.3, .NET 3.5 original Mono x86/x64, Prism, .NET 8 verification.

**Spec:** docs/superpowers/specs/2026-10-11-map-ready-battle.md

## Global constraints

- Never delay native player control to hide preparation; never replace native terrain, segment, input ownership or fresh-state checks.
- One preparation worker; native Unity object access only on the game thread; no partial or obsolete publication.
- One retained pure world topology, no destroyed Unity references retained through combat.
- K/L/semicolon and all existing bindings remain unchanged. No Confirm, boarding, combat, Save or game-state automation.
- Enemy name speech uses displayed native UI; no hidden HP/MP or pre-encounter identity.

## Review focus

- A worker blocked waiting for another batch must wake on cancellation; replacement cannot start until exit.
- A reloaded block can have the same form/count but different data; content mismatch rejects reuse.
- Different wrapped loader order must preserve native face order inside each block and cache identity.
- A navigation key held through combat must not trigger an action on return, and combat controls pass through.
- Cold preparation readiness must be observed against native control without lengthening the native gate.

### Task 1: Compact incremental world topology

Files: Core/WorldGeometry.cs, Core/WorldGeometryPreparation.cs, tests/FF9Access.Tests/WorldGeometryPerformanceTests.cs and WorldGeometryPreparationTests.cs, MonoProbe/WorldPlanningProbe.cs.

Interfaces: retain the public six-argument snapshot constructor and TryStartOwned. Add TryBegin(long), TryAppendOwned(long,WorldSurface[]), Complete(long), TryBindReady(long,WorldGeometrySnapshot), RetainReady(). Streaming snapshot accepts Func<WorldSurface[]> and Func<bool>; only the worker consumes detached batches.

- [x] Allocation RED: 270,933,112 bytes exceeds the fixture's 128 MiB ceiling; numeric ordered shared-edge keys GREEN at 101,069,872 bytes.
- [x] Test incremental publication only after completion, cancellation wakes a blocked worker, replacement ownership, streamed native-order probes, fresh context and complete-only reuse; observe RED.
- [x] Implement the interfaces and allocation-bounded incremental construction; test All GREEN, including nonmanifold/quantized edges and seams.

### Task 2: Native loading and exact reload reuse

Files: Native/NativeWorldGeometry.cs, Native/NavigationRuntime.cs, WMWorld/WMWorld.cs, Global/ff9/ff9.cs, Native/NavigationLifecycleBridge.cs, NativeReaderVerification.cs.

Interfaces: BeginLoad(WMWorld), CaptureLoadedBlock(WMWorld,WMBlock), CompleteLoad(WMWorld), Detach(). Capture scalars during native loading; retain a single pure completed topology, validate every loaded block before rebinding it. Existing Warm/Read remain fallback paths with their shared per-frame capture budget.

- [x] Write emitted checks for loader hooks, exact identity and no native worker accesses; observe missing hooks RED in installed product.
- [x] Wire detached incremental capture into existing loading, publish only after w_blockReady and complete identity. Drop Unity references on teardown. Rebind exact-matching content; rebuild on mismatch.
- [x] All checks and both original Mono probes GREEN; loader call order precedes native control. Actual readiness remains a Task 4 live measurement.

### Task 3: Combat input and displayed enemy information

Files: NativeNavigationInput.cs, NavigationInputBroker.cs, BattleHUD/BattleHUD.cs, battle/BattleSwirl.cs, HonoluluBattleMain.cs, SceneDirector.cs, NativeReaderVerification.cs.

- [x] Reproduce captured-input/held-key return boundary with broker Suspend behavior RED; implement cheap inactive-scene pass-through and safe resumption GREEN.
- [x] Remove the raw enemy-stat announcement branch; rely on native selected-label speech. Verify displayed messages/Libra are preserved.
- [x] Add transition-only elapsed/GC-count traces for encounter effect, scene loading and battle initialization; collect a small record and write once after startup. No normal combat frame logs, heap scans/collections or engine behavior changes.

### Task 4: Review, full verification and installation

- [x] Fresh whole-change review; fix important findings with regression evidence. One Minor test-synchronization improvement remains deferred.
- [x] Full Windows All/Both build, All 204 core checks, 17 WPF checks, both emitted integration checks and both exact original Mono probes.
- [x] Commit, package, verify, deploy product 28ddbb4 with complete rollback; 65 payload files and 1,777 protected files verified. Manual saves/settings preserved, current player autosave progress retained.
- [x] Cold world readiness observed on first controllable x64 tick; three real battle returns reused matched geometry with zero rebuild before control. Three encounter startup timings recorded; listener audio, physical controller and full vehicle arrival remain unverified.
