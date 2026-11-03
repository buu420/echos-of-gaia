# Stable manual navigation Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, then one fresh read-only final review.

**Goal:** Stable turn instructions and useful corrections on an unchanged route.
**Architecture:** Observe native input basis, retain geometric run endpoints,
validate correction/corner segments and compare delivered instructions by
meaning rather than RoutePlan object identity. Keep automatic movement intact.
**Tech Stack:** Existing C#/.NET3.5/Unity/Mono and net8/Python verification.
**Spec:** `docs/superpowers/specs/2026-11-02-stable-manual-guidance.md`.

## Global Constraints

No generated input, native-control remapping, hidden state, timer/collision
changes or automatic continuation across maps. Canonical source checkout only;
install the tested local candidate and preserve player/settings/audio files.

## Review Focus

Stick release and simultaneous digital input follow the actual native owner.
Sideways drift does not change the geometric next turn; blocked rejoin replans.
Human corner tolerance never shortcuts an unverified bend or confuses arrival.
Equivalent route samples/objects stay quiet; genuine turns and corrections speak.
World wrap, dialogue, rejected speech and automatic walking retain their gates.

## Tasks

- [x] Capture fresh logs; add a failing existing-API sidestep speech regression.
- [x] Observe native digital/stick basis and retain it while idle; add RED/GREEN native-context/basis checks.
- [x] Add origin metadata and stable manual-run/correction snapshots, with native segment validation and safe human progression; test drift, bends, blocked correction, arrival, world wrap and unchanged automatic movement.
- [x] Remove static-exit sample drift and equivalent-run chatter; quiet internal refreshes and retain actual new turns/Status/Repeat/rejected-delivery behavior; RED/GREEN and complete affected suites.
- [ ] Refresh audit, build both architectures, review once, address material findings, commit and verify exact native/Mono outputs.
- [ ] Package/deploy the local candidate while FFIX is closed. Wait for the installer owner to exit before reading manifests, independently verify preservation, then report the live manual test still pending.
