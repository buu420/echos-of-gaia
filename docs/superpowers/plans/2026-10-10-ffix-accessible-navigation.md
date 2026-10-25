# FFIX Accessible Navigation and Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver and deploy native-state-backed FFIX navigation, region and vehicle tracking, optional automatic movement, Prism speech, and an accessible launcher for x86 and x64.

**Architecture:** Keep the existing Memoria accessibility entry points and screen-reader facade. Put decision-making, bindings, geometry, and route state in pure .NET 3.5-compatible navigation units; use small Unity adapters for observation, input capture, and movement. The launcher shares the binding parser and speech library, and installation uses an explicit verified package.

**Tech Stack:** C#, Unity 5.2.3/Mono, .NET Framework 3.5 game libraries, .NET Framework 4.8 WPF launcher, Prism C ABI, PowerShell, full Windows MSBuild, ILSpy, Ghidra, and a development-only .NET 8 console test runner.

**Spec:** [Approved design](../specs/2026-10-10-ffix-accessible-navigation-design.md), written-spec approval received in this Codex conversation. Executors read the entire specification and this plan before changing product code.

## Global Constraints

- "No navigation command teleports, grants items, changes progression, automates combat, or presses Confirm."
- "Boarding, landing, interaction, and Confirm remain deliberate player actions."
- "Maintain the game's .NET 3.5 compatibility and the launcher's .NET 4.8 compatibility."
- "Apply movement on the Unity/game thread; do not access Unity actors from a background speech or planner thread."
- "A jump opcode alone is not proof of a visible exit."
- "Position and availability are refreshed from native state; stale coordinates never authorize movement."
- "Targets use separate types for field object IDs, world navigation-location IDs, region IDs, and vehicle actor IDs."
- "The default bracket pair keeps the existing FFIX navigation target keys and preserves J."
- "Guidance defaults to the installed one-second interval, adjustable in half-second steps from 0.5 to 10 seconds."
- "Each observed region retains up to 16 distinct visited points."
- "Preserve the MIT license, upstream attribution, and Prism notices."
- "Preserve native Unity executables, game assets, saves, and unrelated settings."
- "An untested live case remains explicitly unverified."

## Review Focus

1. A previously visited region or named actor must not leak from a later save into a fresh game or overwritten save slot; Task 2 and Task 6 test identity changes and uncertain identity.
2. A held menu button, stick drift, or direct XInput read must not move/Confirm after R3 closes or focus changes, even with AlwaysCaptureGamepad=1; Task 3 tests all input views and release tails.
3. Streamed/discarded world blocks and changing terrain forms must not produce an invented route or mutate the scene; Task 9 tests missing data, private geometry copies, and generation changes.
4. A native follower clear, competing nearby NPC, or scene reconstruction after battle must not be mistaken for arrival; Tasks 2 and 8 test these outcomes.
5. An existing launcher preference, first-run migration, or stock upstream patcher must not overwrite the player's settings or this accessibility build; Tasks 12 and 13 test preservation and update eligibility.

## Workspace, files, and execution boundaries

Start from `D:\FF9Access\source`, branch `codex/ffix-accessible-navigation`, whose documentation baseline is `b697f5d`. Inspect status and applicable AGENTS instructions again at execution. Use an isolated managed worktree if concurrent work requires it, following using-git-worktrees at execution time. Never execute this plan inside the installed game's directory.

Runtime evidence and decompilation are under `D:\FF9Access\research\2026-10-10`. Game root is `D:\SteamLibrary\steamapps\common\FINAL FANTASY IX`. Build/package/backup roots are `D:\FF9Access\staging`, `D:\FF9Access\packages`, and `D:\FF9Access\backups`. A script accepts explicit paths, resolves them, and rejects overlap between staging/backups and the game root. No recursive deletion or movement outside a verified owned directory.

Use these responsibility boundaries; files listed under a task are exact intended destinations:

| Area | Files and responsibility |
| --- | --- |
| Pure navigation | `Assembly-CSharp/Memoria/Accessibility/Navigation/Core/`: models, session state, bindings, eligibility, observations, route algorithms, and announcement decisions. No Unity references. |
| Native navigation | `Assembly-CSharp/Memoria/Accessibility/Navigation/Native/`: fresh context/catalog/geometry readers, physical input, field/world movement, and lifecycle bridge. |
| Speech | `Memoria.ScreenReader/`: compatible facade, Prism/Tolk backends, native ownership, diagnostics, and announcement priority. |
| Launcher | `Memoria.Launcher/Launcher/Accessibility/`: accessible settings, binding conflict UI, semantic labels/status, and local-build update policy. |
| Meaningful automated checks | `tests/FF9Access.Tests/`, `tests/FF9Access.NativeProbe/`, `tests/LauncherAccessibility.Tests/`, and `tests/Deployment.Tests.ps1`. |
| Build/install | `tools/Build-FF9Access.ps1`, `tools/Package-FF9Access.ps1`, `tools/Deploy-FF9Access.ps1`, `tools/Restore-FF9Access.ps1`, and `tools/Test-FF9AccessInstall.ps1`. |
| Evidence | `docs/accessibility/field-exit-evidence.json`, `docs/accessibility/field-exit-coverage.md`, `docs/accessibility/native-movement-contracts.md`, and `docs/accessibility/live-validation.md`. |

The game and launcher are coupled by binding serialization and speech/deployment dependencies; this plan delivers one integrated local package. The launcher task remains independently testable before final package integration.

Task path shorthand is anchored to that map: Core and Native filenames are relative to their respective `Assembly-CSharp/Memoria/Accessibility/Navigation/` subdirectories; `Global/` and `UnityXInput/` paths are under `Assembly-CSharp/`; configuration paths are under `Assembly-CSharp/Memoria/Configuration/`; bare test-suite filenames are under `tests/FF9Access.Tests/`. Bare existing navigation-manager filenames refer to `Assembly-CSharp/Memoria/Accessibility/`. Native engine filenames are fully qualified below. No task creates a root-level `Global` directory.

## Test and commit conventions

The repository has no existing navigation test project. Task 1 establishes a small, dependency-free console runner targeting the installed .NET 8 SDK. Link pure production units into it; do not instantiate Unity or pretend a simulated fixture is a live game. `TestAssert` provides `True(bool)`, `False(bool)`, `Equal<T>(T expected, T actual)`, and `Near(float expected, float actual, float tolerance)`. Tests are named methods selected by suite. Any failed assertion exits 1; success prints `PASS <suite>: <count>` and exits 0.

Commands below run from the source/worktree root. Each behavioral task writes its named tests, runs them RED, implements the specified interface, reruns GREEN, then commits only its listed changed files with `git -c maintenance.auto=false add -- <paths>` and a task-specific message. Do not use `git add -A`. A missing symbol/compile failure is an acceptable initial RED only when it identifies the not-yet-implemented interface. Fixture values come from verified native rules; add captured native examples alongside small geometric edge cases.

Do not build the entire upstream solution or run its patcher/setup script as a shortcut. Only the required library/launcher dependency graph is built, after Task 1's deployment guard is proven. Do not broaden already passing checks unless a new change or unresolved concern requires it.

---

### Task 1: Prove isolated builds and establish verification tools

**Files:** Modify `Directory.Build.props`, `Assembly-CSharp/Assembly-CSharp.csproj`, `Memoria.Prime/Memoria.Prime.csproj`, `Memoria.ScreenReader/Memoria.ScreenReader.csproj`, `UnityEngine.UI/UnityEngine.UI.csproj`, and `Memoria.MSBuild/Deploy.cs`. Create `tools/Build-FF9Access.ps1`, `tests/BuildIsolation.Tests.ps1`, `tests/FF9Access.Tests/FF9Access.Tests.csproj`, `tests/FF9Access.Tests/Program.cs`, and `tests/FF9Access.Tests/TestAssert.cs`.

**Interfaces:** `Build-FF9Access.ps1 -GameRoot <absolute> -StageRoot <absolute> -Configuration Release -Mode References|Game|Launcher|Tests|All -Architecture x86|x64|Both` acquires read-only references or builds the selected dependency graph, returns a nonzero exit on failure, and emits a build manifest. Architecture defaults to Both; game builds run sequentially with separate reference/output directories. Tests builds the test projects available at that task, including x86/x64 native probes and the launcher test executable when present. New MSBuild property `EnableGameDeployment` defaults to `false`; all four actual Deploy targets run only when it is explicitly `true`. `Deploy.Execute()` also refuses deployment unless its supplied enable flag is true.

- [ ] Write `tests/BuildIsolation.Tests.ps1` assertions: all evaluated Deploy targets default disabled; staging paths cannot equal/contain/be inside game paths; dependency projects inherit the guard; a sentinel installed-file hash set remains identical after a build. Do not run an unsafe original build just to demonstrate RED; use an isolated fake installation for the initial guard test.
- [ ] Run `powershell -NoProfile -File tests/BuildIsolation.Tests.ps1 -Mode Policy`; expect failure identifying unguarded Deploy targets.
- [ ] Implement the guard before invoking MSBuild. Resolve full Windows MSBuild/WPF build tools; if missing, install the [official .NET desktop build tools](https://learn.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-build-tools) under the existing user authorization, with no forced restart. Supply .NET 3.5 compile references through pinned [Microsoft.NETFramework.ReferenceAssemblies.net35 1.0.3](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net35/), using [Microsoft's documented reference-assembly mechanism](https://learn.microsoft.com/dotnet/framework/migration-guide/reference-assemblies). .NET 4.8 references are already installed; .NET 8.0.423 and 10.0.203 SDKs were observed during planning. Populate required game references from verified installed managed libraries, with hashes, without executing `SetupProjectEnvironment.ps1` or extracting unknown password-protected dependencies.
- [ ] Implement explicit compile includes for the console runner, since `Directory.Build.props` disables default items. Build the required game libraries and launcher separately into staging; reconcile x86/x64 reference differences and validate all resulting assembly imports. Run the policy test again and `powershell -NoProfile -File tests/BuildIsolation.Tests.ps1 -Mode StagedBuild -GameRoot 'D:\SteamLibrary\steamapps\common\FINAL FANTASY IX' -StageRoot 'D:\FF9Access\staging\baseline'`; expect `PASS BuildIsolation`, zero changed installed hashes, and successful staged compilation. A baseline incompatibility must be resolved in staging before the other tasks.
- [ ] Commit the guarded build/test tooling: `build: isolate FFIX accessibility builds from the installed game`.

### Task 2: Define native context, typed identities, and persistent route intent

**Files:** Create Core `NavigationModels.cs`, `NavigationSession.cs`, and `ObservedIdentityStore.cs`; Native `NativeNavigationContext.cs`, `NavigationLifecycleBridge.cs`, and `NavigationSaveIdentity.cs`; tests `SessionTests.cs` and `TestFixtures.cs`. Modify `Assembly-CSharp/Assembly-CSharp.csproj`, `Global/Field/Map/FieldMap.cs`, `Global/WM/WMScriptDirector.cs`, `Global/WinIosAndrSharedDataSerializer.cs`, and `Global/TitleUI.cs` only at their relevant lifecycle/load boundaries.

**Interfaces:** `NavigationPoint(float x, float y, float z)` is a value type. `TargetIdentity(TargetKind kind, string mapKey, int nativeId)` has value equality; `TargetKind` separates FieldActor, FieldExit, WorldLocation, RegionPoint, and Vehicle. `NavigationContext` supplies `Generation`, `LogicalMapKey`, `SaveIdentity`, `PlayerIdentity`, `Position`, `Transport`, `CanControl`, `Foreground`, `GateReason`, and `BattleTransition`. Define shared `TransportIdentity`, `NavigationSignal`, `RouteMode`, `StopReason`, `GateReason`, and `BattleTransition` in `NavigationModels.cs`. `NavigationSession.Update(NavigationContext context, NavigationSignal signal)` returns `RouteDecision` containing `Mode`, `IsAutomatic`, `ReleaseMovement`, `RequireReplan`, and `Reason`. `NativeNavigationContext.Read()` supplies a fresh context. `ObservedIdentityStore.SwitchSave(string verifiedIdentity)` clears/replaces knowledge before exposure; null identity means session-local knowledge. `Record(TargetIdentity id, string displayedLabel)`, `TryGetLabel(TargetIdentity id, out string label)`, and `KnownCount` expose only names observed under the active identity.

- [ ] Write `RandomBattleRebuildRebindsWithoutOldReferences`, `ScriptedBattleRequiresResume`, `ControlUnknownDoesNotChooseFirstActor`, `FocusReturnDoesNotAutoResume`, `SaveSlotReplacementClearsKnowledge`, and `SuccessfulOrdinarySaveCarriesOnlyCurrentLineage`. Assertions include:
  ```csharp
  TestAssert.True(afterBattle.RequireReplan);
  TestAssert.False(afterFocus.Mode == RouteMode.Moving);
  store.SwitchSave("different-save-content");
  TestAssert.Equal(0, store.KnownCount);
  TestAssert.False(contextWithUnknownControl.CanControl);
  ```
- [ ] Run `dotnet run --project tests/FF9Access.Tests/FF9Access.Tests.csproj -- --suite Session`; expect the missing session contract or behavioral failure.
- [ ] Implement public, Unity-free models plus native control validation using the actual controlled UID/actor. Keep session intent outside per-scene objects, invalidate all old actor/mesh references on teardown, and classify only positively identified native random-battle returns as eligible for fresh resume. Verify battle-return markers against decompiled source and later native logs; uncertain markers remain paused. Include successful ordinary load, autoload/cloud restore, successful save/autosave, and new-game boundaries in the identity bridge. Derive a lineage/content identity rather than trusting slot number alone. A successful save may associate current observed knowledge with its verified new content identity; an unrelated overwritten/loaded slot cannot inherit it. With unavailable identity, clear and use session-local observations. Store companion observation data separately; never alter game-save contents for identity tracking.
- [ ] Run Session GREEN. Verify field/world teardown always returns `ReleaseMovement=true`, battle intent retains stable IDs only, and no load changes actual save contents. Build the game dependency graph through Task 1's script.
- [ ] Commit: `feat: define FFIX navigation context and lifecycle ownership`.

### Task 3: Implement bindings, physical input capture, and the R3 menu

**Files:** Create Core `NavigationControls.cs`, `NavigationBindings.cs`, and `NavigationInputBroker.cs`; Native `NativeNavigationInput.cs`; tests `InputTests.cs` and `BindingTests.cs`. Modify `Global/Hono/HonoInputManager.cs`, `UnityXInput/Input.cs`, `UnityXInput/XInputManager.cs`, `Global/Field/Map/Actor/FieldMapActorController.cs`, `Global/WM/WMPad/WMPad.cs`, `Global/ff9/ff9.cs`, and configuration `Structure/AccessibilitySection.cs` / `Access/Accessibility.cs`. Register compile items explicitly.

**Interfaces:** `NavigationBindings.Parse(string serialized)` returns `BindingParseResult`; `FindConflicts(IList<GameBinding> nativeBindings)` returns conflicts without consuming those game controls. `NavigationInputBroker.Update(NavigationContext context, PhysicalInputSnapshot physical)` returns `NavigationCommand[]`. `FilterButton(NavigationButton button, bool pressed)` and `FilterAxis(NavigationAxis axis, float value)` supply the filtered game view. `NativeNavigationInput.ReadPhysical()` supplies raw input before filtering. `NavigationCommand` includes category/target/status/guide/auto/repeat/interval/menu actions. Input broker state exposes `MenuOpen` and capture-tail status.

- [ ] Write `JConflictLeavesR2Available`, `ShiftIntervalDoesNotStartMovement`, `R3IsStickClickNotTilt`, `CrossReleaseCannotConfirm`, `DirectFieldDpadIsCaptured`, `WorldThrottleIsCaptured`, `StickDriftDoesNotTakeOver`, and `FocusLossWithAlwaysCaptureRetainsReleaseMask`:
  ```csharp
  TestAssert.False(broker.FilterButton(NavigationButton.Confirm, true));
  TestAssert.Near(0f, broker.FilterAxis(NavigationAxis.MoveX, 1f), 0f);
  TestAssert.True(rawSnapshot.ConfirmPressed);
  TestAssert.False(conflictingJCommand.IsEnabled);
  ```
- [ ] Run `dotnet run --project tests/FF9Access.Tests/FF9Access.Tests.csproj -- --suite Input` and the same command with `--suite Bindings`; expect RED before implementation.
- [ ] Implement defaults exactly as spec §5: U/O, brackets, K/I/P/R, Shift+I/P, and R3. Share the pure binding/control files with the launcher through linked compile items. Store serialized bindings in `[Accessibility] NavigationBindings`; store new `NavigationIntervalMilliseconds` and `AnnounceRegionChanges` without removing old fields. Missing milliseconds migrate the explicitly configured old seconds value; fresh defaults use 1000 ms, clamp 500-10000 in 500 ms steps. Preserve all cage/icon options. Capture down/held/up tails and all direct/persistent readers, including raw XInput D-pad/steering/throttle; keep an unfiltered physical snapshot for takeover and R3. Use native dead zones and device mappings, including JoyShock/Steam Input paths. Navigation stays foreground-only while ordinary background capture follows the preserved preference after tails clear.
- [ ] Run Input and Bindings GREEN; add the native-path integration assertions that each enumerated raw reader passes through filtering. Check no navigation key leaks a booster, camera action, Confirm, or Cancel. Build staged game libraries.
- [ ] Commit: `feat: add isolated navigation bindings and spoken controller controls`.

### Task 4: Replace the speech backend while preserving every existing caller

**Files:** Create `Memoria.ScreenReader/ISpeechBackend.cs`, `PrismNative.cs`, `PrismSpeechBackend.cs`, `TolkSpeechBackend.cs`, and `SpeechDiagnostics.cs`; tests `SpeechTests.cs`; `tests/FF9Access.NativeProbe/FF9Access.NativeProbe.csproj` and `Program.cs`. Modify `ScreenReaderManager.cs`, its csproj, and process-quit ownership in `NavigationLifecycleBridge.cs` / `TitleUI.cs` as needed. Record dependency versions/hashes/notices in `native/speech/manifest.json` and `native/speech/LICENSES/` during execution.

**Interfaces:** `ISpeechBackend` provides `bool Initialize()`, `bool Output(string text, bool interrupt)`, `void Stop()`, `bool IsSpeaking()`, `string Name`, and `void Dispose()`. Preserve all current `ScreenReaderManager` methods/properties. Add `LastAnnouncement`, `BackendDiagnostic`, and `SpeechGeneration`; provide an internal injectable factory for tests. `PrismNative` exposes verified cdecl/UTF-8 imports and handles; `Initialize` is idempotent and final cleanup is process-lifetime. Probe targets net48 and builds to `StageRoot/NativeProbe/<architecture>/FF9Access.NativeProbe.exe`; it accepts `--architecture x86|x64 --library <absolute> --mode abi|output` and does not report output testing from ABI-only mode.

- [ ] Write `RepeatedInitializeOwnsOneBackend`, `SceneShutdownDoesNotFreeSpeech`, `PrismFailureUsesOneReportedFallback`, `RecoverAfterScreenReaderStarts`, `DisabledPreferenceSurvivesRecovery`, and `Utf8AndRepeatPreserveActualAcceptedText`:
  ```csharp
  TestAssert.Equal(1, fakeBackend.InitializationCount);
  TestAssert.Equal(0, fakeBackend.DisposalsAfterSceneTeardown);
  TestAssert.Equal("Alexandrie — entrée", manager.LastAnnouncement);
  TestAssert.Equal(1, acceptingBackend.OutputCount);
  ```
- [ ] Run the Speech suite; expect RED for missing backend/lifetime behavior.
- [ ] Implement a .NET 3.5-compatible native wrapper using the shipped Prism headers/exports, not guessed signatures or by-value config structs. Confirm actual boolean width, config-null support, backend ownership, export availability, and architecture dependencies. Use manual UTF-8 handling. Prism is preferred; reported Tolk/SAPI compatibility fallback uses the correct architecture. Return useful diagnostics and retry availability without silently disabling the user's enabled state. Preserve braille/silence/IsSpeaking behavior to the extent provided by a verified backend; do not fabricate backend capabilities. Scene shutdown detaches navigation only; application quit disposes speech once.
- [ ] Run Speech GREEN, build Tests through Task 1, then run each `StageRoot/NativeProbe/<architecture>/FF9Access.NativeProbe.exe --architecture <architecture> --library <absolute matching prism.dll> --mode abi`; expect `PASS Prism ABI <architecture>`. For uncertain ABI behavior consult the exported native library/Ghidra and official Prism source before further output. An output probe logs its actual selected backend and result; audible output remains a separate live check. Confirm title/menu/dialogue/minigame calls still use the facade.
- [ ] Commit: `feat: add process-lifetime Prism speech with observable recovery`.

### Task 5: Build the eligible field target catalog and exit evidence

**Files:** Create Core `NavigationTargets.cs` and `FieldTargetCatalog.cs`; Native `NativeFieldTargets.cs` and `FieldExitEvidence.cs`; tests `FieldTargetTests.cs`; `tools/Export-FieldEntranceAudit.ps1`, `docs/accessibility/field-exit-evidence.json`, and `docs/accessibility/field-exit-coverage.md`. Modify `Memoria/Accessibility/AccessibleNavigationManager.cs` to delegate target collection, and register new source/data items.

**Interfaces:** Define `NavigationCategory`, `VisibilityEvidence`, `FieldTargetObservation`, and `ExitPresentation` in `NavigationTargets.cs`. `NavigationTarget` supplies typed `Id`, `Category`, `Label`, `Position`, `Evidence`, `Generation`, and `NavigationPoint[] ApproachCandidates`. `CatalogResult` supplies `IList<NavigationTarget> Targets`, diagnostics, `Contains(TargetIdentity id)`, and `Find(TargetIdentity id)`. `FieldTargetCatalog.Build(NavigationContext context, IList<FieldTargetObservation> observations, ObservedIdentityStore identities)` returns that result. `NativeFieldTargets.Read(NavigationContext context)` returns `IList<FieldTargetObservation>`; `FieldExitEvidence.TryResolve(string sceneKey, int triggerId, out ExitPresentation evidence)` supplies reviewed visibility/approach evidence.

- [ ] Write `HiddenActorIsNotSelectable`, `VisibleUnnamedActorGetsStableLabel`, `DoorWithoutIconRemainsAvailable`, `ConditionalHiddenJumpIsExcluded`, `FutureItemAndBattleOutcomesStayUnknown`, and `DistanceChangeDoesNotRenameExit`:
  ```csharp
  TestAssert.Equal("Exit A", catalog.Find(exitId).Label);
  TestAssert.Equal("Exit A", rescannedCatalog.Find(exitId).Label);
  TestAssert.False(catalog.Contains(hiddenActorId));
  TestAssert.False(catalog.Find(chestId).Label.Contains("Potion"));
  ```
- [ ] Run the FieldTargets suite; expect RED.
- [ ] Implement visible actor/interaction/presentation checks, stable scene/object IDs, neutral labels, and observed names. Keep an actor's visible identity informative even when its interaction is unavailable. Replace byte-pattern outcome scanning with instruction-aware candidate discovery that never assumes an unobserved branch outcome. Audit actual field entrance candidates and match them to background doors/edges and enabled approaches. Store evidence with scene/map variant, native trigger identity, source fingerprint, and visible cue; embed the reviewed JSON in Assembly-CSharp as `Memoria.Accessibility.FieldExitEvidence.json`. A missing icon does not exclude a visible door; an unreviewed hidden candidate is not exposed just to improve counts. Resolve and report ordinary visible-exit gaps; coverage records distinguish verified, excluded-with-evidence, and unresolved cases.
- [ ] Run FieldTargets GREEN; test captured representative native field data and a real background-only entrance in both guide/auto approaches later. Confirm no hidden destination/item/gil/battle names survive through the old manager. Build staged game.
- [ ] Commit: `feat: expose field targets with stable labels and visible exit evidence`.

### Task 6: Build world locations, multiple vehicle tracking, and regions

**Files:** Create Core `WorldTargetCatalog.cs`, `WorldCoordinates.cs`, and `RegionObservations.cs`; Native `NativeWorldTargets.cs` and `NativeRegionReader.cs`; tests `WorldTargetTests.cs` and `RegionTests.cs`. Modify `WorldMapAccessibilityManager.cs`, with observation lifecycle via Task 2's bridge.

**Interfaces:** `WorldCoordinates.Difference(NavigationPoint from, NavigationPoint to)` returns `NavigationPoint` wrapped delta; `float Distance(NavigationPoint from, NavigationPoint to)` and `string Compass(NavigationPoint delta)` derive from it. Define `WorldTargetObservation` in `WorldTargetCatalog.cs`. `WorldTargetCatalog.Build(NavigationContext context, IList<WorldTargetObservation> observations, RegionObservations regions)` returns `CatalogResult`. `NativeWorldTargets.Read(NavigationContext context)` returns `IList<WorldTargetObservation>` for native-available towns and visible/marked vehicles. `RegionObservations.Observe(string saveIdentity, string mapKey, int nativeRegionId, string nativeLabel, NavigationPoint position, TransportIdentity transport)` retains at most 16 points; `IList<NavigationTarget> BuildTargets(NavigationContext context)` supplies one target per known region, with its observed approach candidates and the label "previously visited point". Task 9 resolves the nearest reachable candidate; a preliminary direct bearing is explicitly unvalidated.

- [ ] Write `WrapUsesShortDistanceAtBothSeams`, `RegionIdCannotEqualTownIdentity`, `UndiscoveredProximityDoesNotNameTown`, `BoatAndAirshipBothRemainInCatalog`, `VehiclePositionRefreshes`, `HiddenUnmarkedVehicleIsExcluded`, `WaterAreaZeroUsesNativeDisplayRule`, `RegionPointsAreBounded`, and `UnknownSaveDoesNotImportRegionKnowledge`:
  ```csharp
  TestAssert.Near(2f, WorldCoordinates.Distance(P(1535, -20), P(1, -20)), 0.001f);
  TestAssert.Near(2f, WorldCoordinates.Distance(P(20, -1279), P(20, -1)), 0.001f);
  TestAssert.Equal(16, observedRegion.Points.Count);
  TestAssert.False(regionTarget.Id.Equals(townTarget.Id));
  ```
  `P(x,z)` is Task 2's fixture helper for y=0.
- [ ] Run WorldTargets and Regions RED.
- [ ] Implement native marker/text availability, active map coordinates, and the native special location-name mapping. Enumerate shown actor indices 3-10 as well as HUD pointers so the shared plane pointer cannot drop a second parked vehicle. Resolve vehicle names by actor identity; read live position each update. Follow native current-region display guards and localized world text, keeping town and region IDs separate. Sample observed region points with meaningful spacing/entry changes and a bounded replacement policy; persist only under verified save/map identity. The full 64-name table is a localization lookup, not an undiscovered target list.
- [ ] Run WorldTargets and Regions GREEN with captured source marker examples. Confirm +X/east and +Z/north from native map projection and verify actor-relative orientation later. Mark valid but unreachable vehicles informative rather than filtering them out. Build staged game.
- [ ] Commit: `feat: add wrapped world targets and observed region navigation`.

### Task 7: Implement the field geometry planner

**Files:** Create Core `FieldGeometry.cs`, `FieldRoutePlanner.cs`, and `RoutePlan.cs`; Native `NativeFieldGeometry.cs`; tests `FieldRouteTests.cs`. Modify the existing manager's routing delegation and compile includes without unrelated walkmesh refactoring.

**Interfaces:** `RoutePlan` has `Success`, `RouteFailure Failure`, `TargetId`, `Generation`, `Transport`, `ApproachPoint`, and `NavigationPoint[] Waypoints`; define `RouteFailure` in `RoutePlan.cs`. Define `FieldGeometrySnapshot` and its triangle records in `FieldGeometry.cs`. `NativeFieldGeometry.Read(NavigationContext context)` returns that snapshot with current BGI/runtime enabled flags, triangle vertices/neighbors, floor/attributes, radius, and generation. `FieldRoutePlanner.Plan(NavigationContext context, NavigationTarget target, FieldGeometrySnapshot geometry)` returns `RoutePlan`.

- [ ] Write `DetoursAroundObstacle`, `DisabledBgiTriangleIsRejected`, `DisconnectedFloorHasNoRoute`, `RadiusRejectsNarrowCorridor`, `CostRelaxationFindsBetterPath`, and `NoRouteNeverCreatesStraightAutoPath`:
  ```csharp
  TestAssert.True(detour.Success);
  TestAssert.True(detour.Waypoints.Length > 1);
  TestAssert.False(narrowCorridor.Success);
  TestAssert.Equal(RouteFailure.Disconnected, disconnected.Failure);
  ```
- [ ] Run FieldRoutes RED.
- [ ] Implement correctly relaxed A* over permitted native triangle adjacency, then corridor/funnel smoothing and actor-radius/height/attribute validation. Keep search state in private arrays; never mutate `WalkMesh` shared path links. Reconcile dynamic BGI and runtime flags rather than relying on triangle constructor defaults. Provide target approach candidates without writing actor positions. Direct bearing remains an informational fallback only.
- [ ] Run FieldRoutes GREEN with obstacle and native captured geometry examples; verify every returned segment clears the same collision constraints. Build staged game.
- [ ] Commit: `feat: plan field routes over current native walkmesh geometry`.

### Task 8: Apply field routes through the native follower and prove approach outcomes

**Files:** Create Native `NativeFieldMovement.cs` and `NativeInteractionProbe.cs`; tests `FieldMovementTests.cs`. Modify `Assembly-CSharp/Global/Field/Map/Actor/FieldMapActorController.cs`, `AccessibleNavigationManager.cs`, and the lifecycle bridge at narrow ownership/clear notifications. Extend Core `NavigationModels.cs` with the outcome models named below. Create initial `docs/accessibility/native-movement-contracts.md` evidence entries.

**Interfaces:** Define `MovementStartResult`, `FieldOutcome`, `FieldMovementOutcome`, and `InteractionReadiness` in `NavigationModels.cs` / `NativeInteractionProbe.cs`. `NativeFieldMovement.TryStart(NavigationContext context, NavigationTarget target, RoutePlan route, long ownershipId)` returns `MovementStartResult`; `void Release(long ownershipId, StopReason reason)` revokes only its path; `FieldMovementOutcome ReadOutcome(NavigationContext context, NavigationTarget target)` carries Moving, ReadyToInteract, ArrivedExit, TurnNeeded, Interrupted, or Unavailable with native evidence. `InteractionReadiness NativeInteractionProbe.Read(NavigationContext context, NavigationTarget target)` returns side-effect-free range, selected-collision/talkability, and facing evidence.

- [ ] Write `FollowerConsumesCorrectWaypointOrder`, `ManualMovementReleasesOwnedPathOnly`, `NativeClearIsAnnouncedAsInterrupted`, `CompetingNpcDoesNotProduceReady`, `TalkabilityAndFacingAreRequired`, and `BattleRebuildNeverUsesOldFollower`:
  ```csharp
  TestAssert.Equal(expectedFirstWaypoint, fakeFollower.FirstConsumedWaypoint);
  TestAssert.False(competingNpcOutcome == FieldOutcome.ReadyToInteract);
  TestAssert.Equal(0, fakeInteraction.ConfirmRequests);
  TestAssert.False(scriptOwnedPath.WasCleared);
  ```
- [ ] Run FieldMovement RED using a narrow follower/interaction adapter fake; document that this is an ownership contract test, not proof of engine movement.
- [ ] Implement the native click-to-move bookkeeping/last-waypoint-pop contract and normal walk/run behavior. Observe clears from talk, lost control, and the native still-frame threshold; distinguish arrival from interruption. Compute interaction readiness through the native collision selection/talkability/facing conditions without invoking `CheckNPCInput`/`CheckQuadInput` or changing their global side effects. Preserve the player's actual Confirm and ordinary contact exits. Camera/heading guidance uses the actual keyboard/stick twist settings.
- [ ] Run FieldMovement GREEN and staged build. Reinspect ILSpy/native movement seams if compiled behavior differs. Capture native trace evidence during Task 14 for path order, run/walk, approach, manual takeover, and interruption.
- [ ] Commit: `feat: apply field navigation through owned native movement paths`.

### Task 9: Implement side-effect-free streamed world terrain routes

**Files:** Create Core `WorldGeometry.cs`, `WorldTerrainRules.cs`, and `WorldRoutePlanner.cs`; Native `NativeWorldGeometry.cs`; tests `WorldRouteTests.cs`. Record terrain/streaming findings in `native-movement-contracts.md`.

**Interfaces:** Define `WorldGeometrySnapshot`, `TransportRules`, and `WorldSurface` in `WorldGeometry.cs`. `NativeWorldGeometry.Read(NavigationContext context, NavigationTarget target)` returns that snapshot with canonical wrapped native mesh triangles/portals, terrain IDs, current world-disc/form fingerprint, transport rule snapshot, and explicit missing-data status. `bool WorldTerrainRules.CanTraverse(TransportRules transport, WorldSurface from, WorldSurface to)` checks native limits/crossing/height/radius. `RoutePlan WorldRoutePlanner.Plan(NavigationContext context, NavigationTarget target, WorldGeometrySnapshot geometry)` selects the nearest reachable approach candidate.

- [ ] Write `WalkingCannotCrossOcean`, `BoatCannotCrossLand`, `ChocoboCapabilitiesUseNativeMasks`, `WrappedPortalsJoinWorldSeams`, `NarrowBeachApproachRespectsRadius`, `MissingBlockDoesNotInventRoute`, `FormChangeInvalidatesRoute`, and `PrivateReadDoesNotMutateSceneCache`:
  ```csharp
  TestAssert.False(WorldTerrainRules.CanTraverse(walker, land, ocean));
  TestAssert.False(WorldTerrainRules.CanTraverse(boat, ocean, land));
  TestAssert.Equal(RouteFailure.TerrainUnavailable, missingBlockRoute.Failure);
  TestAssert.Equal(beforeNativeState, afterNativeState);
  ```
- [ ] Run WorldRoutes RED.
- [ ] Implement wrapped portal/triangle routing with transport-aware A* and radius-valid segments. Copy available `WMBlock.ActiveWalkMeshes` and terrain tangents. Streaming loads/discards blocks, so obtain missing geometry from the same current-disc/current-form prefab mesh data into private copies in bounded game-thread work; do not call `WMWorld.LoadBlocks(false)`, instantiate live block objects, change forms, or touch actor/shared collision caches to make a route. Use only the native current terrain variant. Match `w_movementRoundCheck` / `w_movementCheckTopographID` restrictions, including ground/water transition flags. If data is still loading, report that state and wait/retry a bounded observation; keep valid guidance available. Geometry is for routing, not announcing undiscovered names/events.
- [ ] Run WorldRoutes GREEN with captured native terrain examples and before/after state sentinels. Resolve any uncertain collision rule through source, Ghidra exports, and runtime tracing before enabling that automatic transport. Build staged game.
- [ ] Commit: `feat: plan transport-valid routes over wrapped world terrain`.

### Task 10: Apply world steering and native airship autopilot without Confirm leakage

**Files:** Create Native `NativeWorldMovement.cs` and `WorldMovementOverlay.cs`; tests `WorldMovementTests.cs`. Modify `Assembly-CSharp/Global/ff9/ff9.cs` only at movement-local input/autopilot seams and `WorldMapAccessibilityManager.cs` delegation. Extend Core `NavigationModels.cs` with world outcome/steering models and extend native contract evidence.

**Interfaces:** Define `WorldSteeringCommand`, `WorldOutcome`, and `WorldMovementOutcome` in `NavigationModels.cs`. The command supplies movement axes and movement-local forward/reverse throttle, with no event/button mask. `void WorldMovementOverlay.Set(NavigationContext context, WorldSteeringCommand command, long ownershipId)` and `void Clear(long ownershipId)` operate only within native movement readers. `MovementStartResult NativeWorldMovement.TryStart(NavigationContext context, NavigationTarget target, RoutePlan route, long ownershipId)` chooses native town autopilot for actor status 9/10 or verified steering for other eligible routes; `void Release(long ownershipId, StopReason reason)` calls actual native autopilot OFF and clears overlays; `WorldMovementOutcome ReadOutcome(NavigationContext context, NavigationTarget target)` uses typed target evidence.

- [ ] Write `BoatDoesNotStartAirshipAutopilot`, `BothAirshipStatusesUseNativeAutopilot`, `CancelCallsNativeOff`, `SyntheticThrottleNeverSetsEventConfirm`, `ManualReverseStopsOwnedTravel`, `TownArrivalDoesNotUseRegionEquality`, and `VehicleArrivalUsesFreshBoardingApproach`:
  ```csharp
  TestAssert.Equal(0, boatPort.AutoPilotStarts);
  TestAssert.Equal(1, airshipPort.AutoPilotStops);
  TestAssert.Equal(eventBitsBefore, eventBitsAfterThrottle);
  TestAssert.False(wrongRegionOutcome == WorldOutcome.Arrived);
  ```
- [ ] Run WorldMovement RED.
- [ ] Implement steering inside the native movement-local LX/LY/R/throttle readers; physical input remains separate and takes precedence. Native town autopilot uses the actual location ID, checks status 9/10, and is cancelled with `w_movementAutoPilotOFF()`. Arbitrary observed region/vehicle points use verified movement-local airborne/ground rules; they never masquerade as town IDs. Stop at a reachable native approach and announce the remaining ordinary action. No descent/landing/boarding/transport switch/Confirm synthesis. Verify the overlay cannot reach `EventInput`/`ETb.KeyOn` before enabling it; unverified paths report a concrete limitation and remain informative.
- [ ] Run WorldMovement GREEN and a staged compiled-IL check proving the overlay is movement-local. Task 14 must show actual native autopilot cancellation, no unwanted entrance activation, and preserved manual boarding/landing. Build staged game.
- [ ] Commit: `feat: add cancellable native world travel and airship autopilot`.

### Task 11: Integrate navigation speech, status, guidance, and complete lifecycle behavior

**Files:** Create Core `NavigationAnnouncements.cs` and `NavigationCoordinator.cs`; Native `NavigationRuntime.cs`; tests `AnnouncementTests.cs` and `IntegrationTests.cs`. Extend `NavigationModels.cs` with the common movement observation. Replace old key/routing/teleport logic in `AccessibleNavigationManager.cs` and `WorldMapAccessibilityManager.cs` with coordinator delegation, preserving unrelated accessibility helpers and native hooks.

**Interfaces:** Define a common `MovementObservation` in `NavigationModels.cs` to carry field/world outcome evidence. Define `NavigationUpdate` in `NavigationCoordinator.cs` and `SpeechState` / `NavigationAnnouncement` in `NavigationAnnouncements.cs`. `NavigationUpdate NavigationCoordinator.Tick(NavigationContext context, NavigationCommand[] commands, CatalogResult catalog, MovementObservation movementOutcome)` contains session decision, selected typed target, requested route operation, and announcement intents. `IList<NavigationAnnouncement> NavigationAnnouncements.Format(NavigationUpdate update, int intervalMilliseconds, SpeechState speechState)` returns priority-tagged announcements. `void NavigationRuntime.Tick()` reads fresh native adapters and applies only current-generation operations; `void DetachScene(StopReason reason)` releases movement before losing native references.

- [ ] Write `ManualTakeoverKeepsOptionalPassiveGuidance`, `PStopsBothModes`, `IResumesGuidanceOnly`, `TargetChangeStopsOldTravel`, `RegionChangeDeduplicates`, `DialogueIsNotInterruptedByRepeatedGuidance`, `UnavailableCatalogSpeaksReason`, and `RepeatUsesFacadeLastAnnouncement`:
  ```csharp
  TestAssert.Equal(RouteMode.Guiding, afterGuidanceResume.Session.Mode);
  TestAssert.False(afterPStop.Session.IsAutomatic);
  TestAssert.Equal(1, unchangedRegionAnnouncements.Count);
  TestAssert.Equal("Navigation unavailable: player control not established.", unavailableText);
  ```
- [ ] Run Announcements and Integration RED.
- [ ] Integrate the spec's complete lifecycle table and independent selection/guidance/movement ownership. Update/deduplicate on meaningful changes, announce empty categories/errors, and preserve native-valid status when routing fails. Enforce 500-10000 ms interval limits without delaying release. Repeat the last accepted accessible output. Suspend guidance during dialogue/menus/battle, explicitly resume after interaction/focus, and fresh-replan only verified random-battle returns. Retire hidden-outcome parsing, wrong arrival IDs, first-actor fallback, Ctrl+Enter teleport, and F9 debug interception from the navigation path.
- [ ] Run all core suites once, staged game build, and compiled public-facade/API checks. Verify existing dialogue/menu/icon/cage/QuadMist call paths remain intact; any discovered regression gets a focused reproducer and correction rather than broad unrelated refactoring.
- [ ] Commit: `feat: integrate spoken FFIX navigation and route lifecycle`.

### Task 12: Make the maintained launcher accessible and preserve the custom build

**Files:** Modify `Memoria.Launcher/MainWindow.xaml`, `MainWindow.cs`, `MainWindow_ModManager.cs`, `Launcher/UiLauncherButton.xaml`, `UiLauncherButton.xaml.cs`, `UiLauncherPlayButton.cs`, `UiGrid.cs`, the affected `SettingsGrid_*.cs` controls, and the csproj. Create `Launcher/Accessibility/AccessibilitySettingsPanel.cs`, `AccessibleStatus.cs`, and `AccessibilityBuildPolicy.cs`; tests `tests/LauncherAccessibility.Tests/LauncherAccessibility.Tests.csproj` and `Program.cs`. Link Task 3's two binding/control files and reference the speech project; update `Languages/en.xml` and use the existing localization fallback for other languages.

**Interfaces:** `AccessibilitySettingsPanel.Load(IniFile ini)` / `TrySave(IniFile ini, out string validationMessage)` reuse the shared parser/conflict rules. `AccessibleStatus.Report(string message, bool important)` exposes readable UIA status and optional single speech output. `AccessibilityBuildPolicy.CanApplyUpdate(string installedForkId, string candidateForkId, int candidateAccessibilityApi)` requires compatible known build identity; no configured fork update source means updates are unavailable with a readable reason, not a fallback stock patcher download.

- [ ] Write STA/UI Automation integration checks `PlayHasNameAndInvokePattern`, `CheckboxHasLabelAndState`, `KeyboardOrderReachesAccessibilitySettings`, `BindingConflictKeepsPreviousValue`, `OpeningSettingsDoesNotLaunchGame`, `FirstRunPreservesGraphicsAudioAndModChoices`, and `StockPatcherCannotReplaceCustomBuild`. Use temporary settings and an injected process launcher; assert:
  ```csharp
  TestAssert.True(playPeer.GetName().Length > 0);
  TestAssert.Equal(0, fakeProcessLauncher.GameStarts);
  TestAssert.Equal(existingGraphicsValue, settingsAfterOpen.GetSetting("Graphics", "AntiAliasing"));
  TestAssert.False(AccessibilityBuildPolicy.CanApplyUpdate("ff9-access-local", "stock-memoria", 0));
  ```
- [ ] Build `-Mode Launcher` and `-Mode Tests` through `tools/Build-FF9Access.ps1 -Configuration Release -GameRoot 'D:\SteamLibrary\steamapps\common\FINAL FANTASY IX' -StageRoot 'D:\FF9Access\staging\launcher-tests'`, then run `D:\FF9Access\staging\launcher-tests\LauncherTests\LauncherAccessibility.Tests.exe --stage-root 'D:\FF9Access\staging\launcher-tests'`; expect the unnamed/keyboard/settings/update assertions to fail initially. The launcher test project targets net48, shares `TestAssert.cs`, and runs an STA entry point.
- [ ] Supply real control semantics and explicit AutomationProperties names, label associations, states, focus order, keyboard activation, and readable dialogs/status. Cover generated settings/mod rows and existing dialog buttons, not just Play. Add accessible binding/interval/region/menu-trigger/backend controls. Preserve architecture selection, working directory, Steam launch requirements, user AutoRun preference, and accessible launch failures. Guard upstream first-run migrations so enabling accessibility does not reset existing graphics/audio/mod preferences. Preserve CheckUpdates preferences while blocking incompatible stock patcher application to this custom local build; do not publish/configure an invented release endpoint. Actual Play can start the game; merely opening the settings panel cannot.
- [ ] Run launcher tests GREEN and inspect the actual staged window's UIA tree/focus behavior. Separate fake-launch tests from a real keyboard-only launch in Task 14. Avoid duplicate UIA/Prism speech; show backend errors through ordinary accessible controls if speech itself fails.
- [ ] Commit: `feat: make the FFIX launcher and accessibility settings keyboard accessible`.

### Task 13: Package, back up, deploy both architectures, and verify rollback

**Files:** Create `tools/Package-FF9Access.ps1`, `Deploy-FF9Access.ps1`, `Restore-FF9Access.ps1`, `Test-FF9AccessInstall.ps1`, `tests/Deployment.Tests.ps1`, and `docs/accessibility/install-and-rollback.md`. Modify only explicit dependency-copy/build items and launcher config required by the verified payload.

**Interfaces:** Package script accepts `-StageRoot`, `-PackageRoot`, and explicit `-GameRoot`; produces `package-manifest.json` with relative paths, destination role/architecture, source SHA-256, fork build identity, dependency/license entries, and protected-path policy. Deploy accepts `-PackageRoot -GameRoot -BackupRoot`; restore accepts `-BackupManifest -GameRoot`. Install test accepts `-GameRoot -PackageManifest -BackupManifest`. All scripts return nonzero on mismatched paths/hashes/dependencies or a running target process. Deployment is a separate explicit operation, never a build side effect.

- [ ] Write a temporary-install fixture asserting correct x86/x64 native libraries, missing-import rejection, preservation of existing accessibility/cage settings and legacy interval migration, unchanged native executables/assets/saves, backup-before-replacement, file-copy failure recovery, and byte-for-byte rollback. Assert all destinations stay within the explicit root and no file is copied from a stale Release 4/6 package.
- [ ] Run `powershell -NoProfile -File tests/Deployment.Tests.ps1`; expect missing-script/manifest-policy failures initially.
- [ ] Implement an explicit payload manifest including patched `Assembly-CSharp`, `Memoria.Prime`, `Memoria.ScreenReader`, built `UnityEngine.UI`/`XInputDotNetPure` when required, verified managed/transitive native dependencies, x86/x64 Prism and matching Tolk/controller-client fallback, launcher/config, and notices. Resolve imports against the package plus preserved native installation. Protect original Unity executables and all game assets/saves. Merge only new accessibility options, preserving existing values. Back up every changed file/config before copy, record originals/new hashes, and stop or restore on a partial failure. Restore exactly manifest-owned changes, including created files and merged config state; preserve unrelated changes.
- [ ] Run deployment fixture GREEN, package from the final staged build, verify all package hashes/PE machines/imports, then deploy to the authorized installed game after confirming no game process is running. Run `Test-FF9AccessInstall.ps1`; expect exact payload hash matches, active accessibility types in both managed cores, matching speech architecture, unchanged protected files, and a complete recoverable rollback set. Restore/redeploy testing occurs in the temporary fixture; do not unnecessarily overwrite the user's real install twice.
- [ ] Commit tooling/docs: `build: package and deploy FFIX accessibility with verified rollback`.

### Task 14: Validate player-facing behavior, review the branch, and report evidence

**Files:** `docs/accessibility/live-validation.md`, `native-movement-contracts.md`, `field-exit-coverage.md`, and the user-facing navigation controls/help documentation. Fix only demonstrated defects in their owning task files and rerun affected checks.

**Interfaces:** `live-validation.md` records architecture, installed build/hash manifest, native save/map/transport context, exact action/result, actual selected speech backend, log evidence, and Pass/Fail/Unverified for each required case. No fixture or ABI probe is labeled live gameplay.

- [ ] Read current game/mod logs and installed manifests before controlling play. Check ordinary available saves and all requested field/world/vehicle contexts without modifying progression. Launch through the accessible launcher and verify actual Prism announcements. Use native diagnostics and player/device input to observe behavior; do not simulate combat or silently press story Confirm for validation.
- [ ] Demonstrate field target selection, stable exits, obstacle walking, actual interaction readiness/facing, and native clear/cancellation; demonstrate region changes, town markers, relocated/multiple parked vehicles, wrapped guidance, transport-valid routes, native airship autopilot start/OFF, and manual boarding/landing/Confirm. Test R3 menu held/release tails, raw input isolation, keyboard/controller orientation, focus loss, disconnect, menus/dialogue, and real random-battle return. Repeat relevant core behavior in x86 and x64; record unavailable saves/devices/cases as Unverified and continue any independent checks.
- [ ] Verify actual launcher UIA names/roles/states, tab order/shortcuts, errors, settings persistence, and keyboard-only Play. Check existing game dialogue, menu, icon, cage/minigame speech remains audible. A confirmed silent information path or continuing owned movement blocks success until repaired; produce a focused regression test for meaningful defects.
- [ ] Complete the execution-method-required independent review using a fresh reviewer on the most capable available model. Provide the approved spec, this plan, branch diff, source/native evidence, and actual test/deployment/live results. Verify findings against files and repair relevant issues before repeating their affected checks. Run the final appropriate core/build/package checks only after those changes; verify installed hashes still match the reviewed package and protected files remain intact.
- [ ] Commit final evidence/help and report what was implemented, what was locally deployed, which actual live cases passed, and what remains unverified or concretely impossible. Do not claim the entire original Unity engine was recovered, and do not publish/push a remote release without separate scope. Keep rollback instructions and controls accessible to the user.

## Spec coverage and review handoff

| Spec sections | Owning tasks |
| --- | --- |
| 1-2: complete requested result and evidence limits | 1, 13, 14 |
| 3: eligible visible targets, stable IDs, known regions | 2, 5, 6 |
| 4: component boundaries and native data flow | 2, 3, 7-11 |
| 5: controls, conflicts, and capture | 3, 11, 12 |
| 6: field guidance/walking/interaction | 5, 7, 8, 11, 14 |
| 7: wrapped world travel and vehicles/airships | 6, 9, 10, 14 |
| 8: release, pause/resume, battle rebuild, errors | 2, 3, 8, 10, 11 |
| 9: preserved speech and diagnostics | 4, 11, 12, 14 |
| 10: accessible launcher | 12, 14 |
| 11: staged build, package/deploy, preservation/rollback | 1, 4, 13 |
| 12-13: automated/native/live acceptance and technical obligations | every task's verification, then 14 |

Plan self-review: every requested component has an owner and acceptance check; interfaces are shared by name; movement/input/geometry uncertainty has explicit test and runtime obligations rather than silent omissions. No product builds, dependencies, deployment, or game control have occurred while writing this plan.

**Execution recommendation: Native.** These tasks share tightly coupled native context, input, route ownership, and speech interfaces. Implementing them sequentially in this session keeps those contracts consistent; a fresh whole-branch review and real native validation remain required. Subagent-driven execution is also available if the user prefers an independent implementation/review gate for each task.

Written-plan review and an execution-method choice are required before product implementation. Approval of this file starts the chosen execution workflow; it does not require another routine confirmation for the already authorized local build, fixes, or backed-up deployment.
