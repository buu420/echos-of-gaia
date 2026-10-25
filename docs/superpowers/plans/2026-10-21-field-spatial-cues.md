# Field spatial cues implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline, followed by one independent whole-change review.

**Goal:** Give nearby presented NPCs, interactables and exits distinct HRTF cues.

**Architecture:** Native catalog snapshots feed a pure proximity selector and a dedicated worker. Steam Audio transforms mono samples into stereo waveOut buffers, with bounded voices and queue lifetime.

**Tech Stack:** Memoria C# on game Mono/.NET 3.5; Steam Audio 4.6.1 x86/x64; WinMM; WPF launcher; existing build/package scripts.

**Spec:** ../specs/2026-10-21-field-spatial-cues-design.md

## Global constraints

- Current native presentation only; no hidden targets or automatic interactions.
- Preserve 80/400-unit attenuation, 3200 ms cadence, 250 ms onset spacing.
- Game-thread snapshots only; no file IO, rendering or worker waits during gameplay.
- Snapshot expiry 750 ms; three 512-sample blocks at 24 kHz maximum queue.
- Preserve both architectures, narration, preferences, observations, saves and rollback evidence.

## Review focus

- Remembered targets and Story aliases must never become audible duplicates.
- Disappearing targets, focus/control loss, map/save changes and stalls must stop voices.
- Native ABI and resource cleanup must work on old Mono, including partial initialization failure.
- Headphone direction must agree with navigation across rotated field controls.
- Slow devices or loader failure must not block battles or corrupt driver-owned buffers.

### 1. Proximity selection

- [x] Add failing core scenarios for the three kinds, fresh evidence, identity changes, pulses and rotation.
- [x] Run the focused suite and observe RED; implement `FieldSpatialCueTracker.Update` returning detached `FieldSpatialCue[]` with current point/direction/gain and pulse serial.
- [x] Run focused and existing exit tests; keep compatibility for the old exit helper while the native runtime moves to the shared tracker.

### 2. HRTF worker and playback

- [x] Add failing rendering/output lifecycle tests with deterministic fake native/output boundaries.
- [x] Implement pinned `SteamAudioHrtf` and `SpatialWaveOutput`, then `FieldSpatialAudioWorker`; validate native exports with Ghidra and official headers/docs.
- [x] Test stereo direction, tails, queue bounds, stale suppression, failure and cancellation. Probe actual x86/x64 game Mono.

### 3. Native integration, assets and controls

- [x] Confirm sound assignment, normalize supplied recordings without changing duration/pitch/tempo and pin original/playback hashes.
- [x] Replace the old mono exit runtime with the shared snapshot bridge, preserving the existing gate/lifecycle call sites.
- [x] Add NPC/interactable launcher controls and regressions for UIA/validation/preservation.
- [x] Extend package dependency and asset validation; update player documentation and emitted-assembly checks.

### 4. Delivery

- [ ] Run required focused/core/native-context/launcher/Mono checks and build All/Both.
- [x] Independent whole-change review, resolve findings and recheck affected paths.
- [ ] Commit final product, rebuild/package against clean source, snapshot player data and deploy while game is closed.
- [ ] Verify installed/protected/rollback hashes after installer exits; never read a manifest while it is being replaced.
- [ ] Report implemented behavior, preserved data, and remaining live/listener checks.
