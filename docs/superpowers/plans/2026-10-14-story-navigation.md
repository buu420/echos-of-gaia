# Story navigation implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline, with a read-only whole-change review.

**Goal:** Repair shared navigation defects and provide a native-progress Story
category with appropriately classified optional content.

**Architecture:** Offline installed-script audit and detached catalog, thin
cached native progress bridge, existing physical targets and geometry.

**Tech stack:** C#/.NET 3.5 game code, .NET 8 regression harness, Python native
asset audit, original Mono x86/x64 and PowerShell packaging.

**Spec:** ../specs/2026-10-14-story-navigation-design.md

## Global constraints and review focus

- Preserve native radius/permissions/portals, saves/settings and existing keys.
- Ordinary Confirm, digging and combat remain player controlled.
- No hidden rewards, future encounters or invented actor positions.
- Cache asset-derived metadata and keep story selection inexpensive.
- Verify reloads, camera scroll, changed scripts, stale selection and transports.

## Tasks

1. Native-format script snapshot repair.
   - [x] Reproduce UInt16 progress, byte, signed and 24-bit conditions/writes.
   - [x] Correct read/write widths to EBin.VariableType and verify all core tests.
2. Installed-script and chapter audit.
   - [x] Read indexed GameFAQs chapter context and Blind Soldier's Story selection.
   - [x] Inventory native progress writes, transitions and interactions with hashes.
   - [x] Record chapter/transit checkpoints and unresolved exclusions explicitly.
3. Story catalog and physical target integration.
   - [x] Reproduce current-step, transit, empty/stale/modified-context and category cases.
   - [x] Add Story identities/labels, cached native progress and validated bindings.
   - [x] Reuse physical routing/arrival and give honest unavailable directions.
4. Broad navigation repairs and side-content categories.
   - [x] Evaluate baseline review findings against native evidence.
   - [x] Reproduce and fix material shared defects; classify visible pickups/NPCs.
   - [x] Verify captured geometry and menu/scene/camera invalidation regressions.
5. Release and deployment.
   - [x] Run core/native catalog audit, both candidate emitted checks, Mono and launcher checks.
   - [x] Obtain read-only whole-change review; resolve material findings.
   - [ ] Commit, rebuild exact All/Both, package, preserve newest player files, deploy.
   - [ ] Independently verify installation and document remaining live coverage.

The last two delivery gates require the committed artifacts and are recorded
in the external execution ledger and installed build manifest. This plan's
source snapshot precedes that final build. Live whole-game coverage is not
implied by these automated checks.
