# Consistent navigation distance counts Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline and one fresh read-only review at the end.

**Goal:** Identical path length produces identical counts across native input orientations and walking/running progress.
**Architecture:** Retain actual native position-based progress; stop replacing geometric length with a rotated axis projection during speech formatting.
**Tech Stack:** Existing C#/.NET3.5/Unity/Mono, net8 and Python verification.
**Spec:** docs/superpowers/specs/2026-11-02-distance-counts.md.

## Global Constraints

Canonical source checkout and dedicated branch; preserve the unrelated dirty
panorama test file. No input synthesis, native movement/camera changes, hidden
targets, publication or game launch. Install only exact verified All/Both output.
Never read active installed/backup manifests before the installer owner exits0.

## Review Focus

Cardinal rounding and diagonals must use the same length. Field and world
controls must remain directionally correct. Correction and follow-on counts
must retain native clear-path checks. Walking/running must consume the same
fixed unit by actual position, without timers. Existing arrival, quiet refresh,
rejected speech and automatic followers must retain their gates.

## Task 1: Count map length independently of control orientation

Files: RouteInstruction.cs, new DistanceCountTests.cs, manual-navigation.md and
the source-fingerprint guide audit. Interface RouteInstruction.Resolve remains
unchanged; DirectionFor returns geometric horizontal distance and a key label.

- [x] Add field/world/correction/bearing regressions and observe the current count mismatch.
- [x] Remove projected-axis distance replacement, retain direction selection, and verify targeted GREEN plus full core/context/Python suites and audit.
- [x] Verify native walking/running update behavior and actual-position progress; document fixed distance units without claiming exact footfalls.
- [ ] Commit only scoped files, obtain one fresh read-only final review, and fix material findings with regression evidence.
- [ ] Build All/Both, verify exact native CIL/original Mono and count regression on both architectures.
- [ ] Package/install while closed, wait for owner exit0, independently verify current player/settings/audio/protected/rollback files, and report local readiness with live proof pending.
