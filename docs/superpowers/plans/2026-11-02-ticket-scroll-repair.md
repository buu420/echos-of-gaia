# Ticket route scrolling repair Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline; one fresh final review.

**Goal:** Restore the opening Story's local street passage and stop repeated informational failures.
**Architecture:** Expand only proven plain exit presentation to the active camera's native scrollable panorama. Keep actual target approaches and native geometry unchanged; acknowledge informational speech once.
**Tech Stack:** C#, .NET3.5/Unity Mono, existing net8 tests and Windows packaging tools.
**Spec:** `docs/superpowers/specs/2026-11-02-ticket-scroll-repair.md`.

## Global Constraints

No hidden actor/item information, native script execution, generated Confirm,
camera/state changes, automatic cross-map continuation or save/settings/audio changes.
Work in the canonical source checkout on the dedicated repair branch.

## Review Focus

Vertical and horizontal camera pans keep the same physical exit.
Native camera clamps, behind-camera points and altered backgrounds remain unavailable.
Animated/closed doors cannot become plain panorama passages.
Rejected speech remains retryable; deliberate status and repeat still speak.
Directional guidance and current-map identity remain intact.

## Repair task

- [x] Add failing tests for accepted informational guidance repeating, changed limitations, deliberate repeat/status, and rejected delivery.
- [x] Implement acknowledgement-based informational repetition control in `NavigationAnnouncements.cs`; run focused and full core tests.
- [x] Add failing native-camera panorama cases for vertical100 and horizontal101 plus native clamps, finite/depth checks and plain-passage requirement.
- [x] Add the tested `FieldPanoramaPresentation`/`FieldPanoramaEvidence` predicates and use them in `NativeFieldTargets.VisibleBoundary`; require original3passage identities, rendered tile, current camera and native trigger checks.
- [x] Replay original street scripts through the real catalog path, verify original map-local geometry and document the live evidence limits. Private static323-triangle connectivity probe passes all3streets at live-session radius80; no live movement claim.
- [ ] Build both architectures, run affected suites and original Mono/emitted-reader checks; fresh final read-only review, address material findings and commit.
- [ ] Package and transactionally install the local candidate while FFIX is closed; wait for installer exit before checking manifests; verify saves/settings/recordings and report the remaining real playthrough test.
