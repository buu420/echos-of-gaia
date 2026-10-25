# FFIX audio descriptions implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline. Claude owns
> bounded field/native research; obtain an independent final code review.

**Goal:** Add synchronized own-voice FMV and visible field-action descriptions.

**Architecture:** A detached tested scheduler and waveOut output; thin native
movie/action observations; reviewed external narration assets and manifest.

**Tech stack:** C#/.NET 3.5, original Mono x86/x64, native owned SAPI fallback,
Python/Fish Audio SDK research, approved local Qwen3-TTS voice generation,
FFmpeg, Descript, native Ghidra evidence and existing PowerShell deployment.

**Spec:** ../specs/2026-10-15-audio-descriptions-design.md

## Global constraints and review focus

- Preserve original game audio/video, controls, progression, saves and keys.
- No guessed gestures, offscreen actors, stale narration or hidden information.
- No API credentials, voice model IDs, human recordings or game films in release.
- No blocking audio generation, asset scanning or bulk file reads on game frames.
- Test paused/replaced movies and actors, busy dialogue, late worker completion,
  missing/rejected audio and failed native buffer cleanup.

## Tasks

1. Film inventory, visual review and voice assets.
   - [x] Inventory source hashes/durations and recover prior project/voice evidence.
   - [x] Review all native FMVs in Descript and locally; record original cue script.
   - [x] Generate approved-voice clips; verify words, PCM, loudness and time windows.
2. Detached scheduling and safe native audio output.
   - [x] Add failing tests for native-time windows, pause/resume, skip/replacement,
     focus loss, stale loads, playback rejection and ownership failure cases.
   - [x] Implement AudioDescriptions/Core/MovieDescriptionSession.cs and
     DescriptionWaveOutput.cs using narrow injectable playback/native interfaces.
   - [x] Verify the tests, original-Mono compatibility and actual muted waveOut.
3. Native movie and field-action integration.
   - [x] Bind MovieMaterial observations to native uploaded frame/ownership and
     all pause/stop/destruction paths in AudioDescriptions/Native/.
   - [x] Resolve field action semantics through actual rendered proof; add exact
     bindings, visibility/lifetime tests and an honest coverage ledger.
   - [x] Coordinate accepted recordings and following dialogue; verify menu,
     battle, focus and scene cancellation without synthesized game input.
4. Accessible settings, packaging and deployment.
   - [x] Add description enable/volume controls and preservation/UIA checks.
   - [x] Package narration through explicit owned paths and manifest validation.
   - [x] Complete core/native/emitted/Mono/launcher/audio checks and fresh review.
   - Release execution: commit, rebuild exact All/Both, package, snapshot, deploy
     and independently verify both installed copies and newest player files.
     The installed-build and rollback manifests record completion of this step.

Research and service job state live in
D:\FF9Access\research\2026-10-15\audio-descriptions. The source plan does not
replace the external release and live-validation ledger.
