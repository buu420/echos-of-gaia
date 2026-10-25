# Natural Fish narration pack implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline, with one whole-change review before deployment. Continue under the existing implementation and installation authorization.

**Goal:** Replace the 496 narration recordings with the accepted personal Fish voice and natural pacing, retaining the louder opening and all native content and timing constraints.

**Architecture:** Generate independent takes with four bounded API workers and one local ASR verifier. Reuse the three accepted takes. Reject wrong words, clipped or silent output, long function-word holds and scene-window overruns. Rebuild a separate pack from the existing public catalog, then apply the existing offline opening mastering and independently check the final recordings.

**Tech stack:** Fish Audio S2.1 Pro Free SDK; local faster-whisper medium.en; Python, NumPy, FFmpeg; existing Memoria release tools.

**Spec:** The original opening repair plan and the creator's accepted natural-speech comparison, with the caveat that some synthetic timbre remains.

## Constraints and review focus

- Preserve every caption, cue ID, film hash, native time window, field observer and player setting. Change recording data and truthful provider provenance only.
- Send only verified original human voice recordings and the approved captions to Fish. Never send the third-party performance reference, secrets or game video.
- Use only the exact free model string. No paid fallback. Four requests stay below the documented starter concurrency of five; back off on transient failures.
- Bind resumable accepted takes to input catalog, voice-reference hashes, generation settings, waveform hashes and QA. Keep failed takes and diagnostics outside the public payload.
- No manual word-check overrides. Accept only exact words or the existing narrow recognizer spelling equivalents. Time/pitch edits remain absent.
- Preserve canonical 24 kHz mono PCM16, runtime memory budgets, true-peak headroom and native scene margins. The existing opening limiter runs offline.
- Snapshot and verify current saves, settings, navigation data and narration; deploy both architectures only while the game and launcher are closed.
- Automated word, timing and signal checks do not establish absence of synthetic timbre, full listener approval or live gameplay synchronization.

### Task 1: Rendering and replacement contracts

**Files:** tools/audio_descriptions/fish_render_support.py; tools/audio_descriptions/render_fish.py; tools/audio_descriptions/build_pack.py; tests in tools/audio_descriptions/test_fish_render.py and test_build_pack.py.

**Interfaces:** Renderer consumes the existing manifest plus private reference profiles; produces hash-bound accepted WAV/JSON pairs. The replacement builder consumes those pairs, preserves native catalog metadata, and supplies a truthful unmastered public candidate.

- [x] Add failing regressions for stretched function words, finite/headroom-safe normalization, changed reference/candidate evidence and replacement-pack preservation.
- [x] Implement the minimal helpers, resumable renderer and replacement builder; run the focused tests.

### Task 2: Generate and verify all recordings

**Interfaces:** Accepted pairs feed the replacement builder; its public candidate feeds the existing opening master; the mastered pack feeds an independent final word/format/window/peak audit.

- [x] Render all 496 cue identities using the accepted references and simpler direction; preserve the three accepted auditions. Retain and retry failures.
- [x] Rebuild and master a separate complete candidate; audit its actual final bytes and all native budgets. Inspect representative and outlier signal evidence without claiming audible approval.
- [x] Update truthful provenance, documentation and embedded catalog; obtain an independent whole-change review.

### Task 3: Release and deploy

- [ ] Commit reviewed changes, build All/Both and run relevant core, audio integration, emitted-assembly, WPF, pack and original-Mono checks.
- [ ] Snapshot the current installation and player data, package, deploy, and independently verify payload, rollback and preservation.
- [ ] Report installed coverage and remaining listening/timbre limitations accurately.

Execution evidence and final installation status are recorded in the private release report and ordinary installed-build/rollback manifests.
