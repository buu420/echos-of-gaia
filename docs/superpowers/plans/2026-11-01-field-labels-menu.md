# Field Labels and Menu Responsiveness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Implement sequentially; one fresh whole-change review at the end. Naming evidence is being independently researched read-only.

**Goal:** Make field targets understandable and keep ordinary menus responsive.

**Architecture:** Gate captures at the managers and actual native menu state; preserve existing lifecycle cancellation and world loading preparation. Replace unsafe accessibility-only talk probes with bounded reads of the original event script. Keep player-facing reasons separate from logged technical evidence. Learn names at the native displayed-dialogue attribution boundary and add reviewed visible descriptions/observed exit names.

**Tech Stack:** C# net35 runtime/net8 integration fixtures, existing Python/PowerShell build and transactional release tooling.

**Spec:** docs/superpowers/specs/2026-11-01-field-labels-menu-design.md

## Global Constraints

- No hidden identities, future dialogue, guessed model-abbreviation names, synthetic Confirm or changed native movement rules.
- Native world loading preparation, valid interaction guards and existing controller bindings remain intact.
- Technical diagnostics belong in logs; unavailable information gets concise player-facing wording.
- Preserve saves/settings/unrelated files and all608recordings; no game launch for offline validation.

## Review Focus

- Menu close restores captures without losing last-seen targets or route cancellation.
- Loading and battle-return paths retain their existing readiness and movement gates.
- Additional event-command buffers cannot invalidate original-script talkability offsets.
- Fixed-position/multi-speaker dialogue and reused UIDs must not mislabel another actor.
- Unknown/duplicate labels and unseen exits retain truthful visible-only information.

### Task 1: Menu capture budget, talkability and concise speech

**Files:** AccessibleNavigationManager.cs, WorldMapAccessibilityManager.cs, NativeNavigationContext.cs, NavigationRuntime.cs, MainMenuUI.cs; navigation core helper(s); native/input/menu fixtures.

**Interfaces:** Native context identifies actual menus separately from loading. Managers skip capture when native observation is not permitted. Original-script talkability parses bounded event bytes. Speech formatting uses player-facing interruption reasons; internal diagnostics remain inspectable in logs.

- [x] Add and observe failing real-manager capture tests for field/world menus and last-seen retention; actual-context menu/loading tests; menu interrupt-boundary tests; core talk-script bounds and public-message tests.
- [x] Implement the minimal gating, original-script probe and speech changes.
- [x] Run affected and complete suites; commit.

### Task 2: Meaningful field labels

**Files:** NativeDisplayedIdentity/Dialog attribution boundary, FieldTargetCatalog, observation/identity model, reviewed visible labels and actual transition wiring; focused fixtures and evidence docs.

**Interfaces:** Consume the naming audit's concrete native attribution/data evidence. A displayed name wins over a reviewed visual fallback; a stable suffix differentiates duplicates. Previously observed exit destinations are optional evidence scoped to the actual source exit.

**User clarification and review refinement:** Visibility alone offers a presented target immediately. Destination learning cannot gate inclusion. Require the active native push invocation and direct executing object for exit learning, and discard changed offscreen identities. Story headings retain the reviewed required action; installed-catalog tests cover Vivi102→103→104, optional108→102, and actual1150 completion to alley105. Scene counter writes still control progression.

- [x] Record precise naming evidence and refine this task's file/API decisions before implementation.
- [x] Add failing attribution, identity-reuse, fallback and observed-transition tests; implement only evidence-backed labels.
- [x] Run core and native integration suites; commit.

### Task 3: Review, deploy and publish

**Files:** Controls/coverage/release documentation, exact public leaf export; private report/evidence.

- [ ] Obtain one fresh read-only review, reproduce/fix important findings and run the whole suite.
- [ ] Build All/Both, verify original Mono and launcher/installer behavior, validate exact-source archives.
- [ ] Snapshot once, deploy transactionally without active-manifest reads, verify preservation after the process exits.
- [ ] Publish GitHub beta and append the Amethyst catalog release, preserving concurrent entries; verify anonymous assets/catalog and CI.
- [ ] Report what passed and the remaining recipient-PC/controller listening limit.
