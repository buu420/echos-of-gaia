# Speech output recovery implementation plan

> For agentic workers: use superpowers:executing-plans inline. This continues the existing dedicated controller/menu repair branch; the user has already authorized fixes and deployment without further approval prompts.

**Goal:** Deliver menu and navigation announcements when a speech backend initializes but rejects output, and diagnose the native rejection precisely.

**Architecture:** ScreenReaderManager already owns the preferred Prism and compatibility Tolk factories. On preferred output rejection or exception, dispose that failed instance, initialize the compatibility backend once, and retry the same announcement. Retain a successful compatibility backend. When both fail, report both failures and keep the existing bounded retry. Preserve accepted-output serial and repeat semantics. PrismSpeechBackend exposes its existing native LastError for diagnostics.

**Spec:** Downloads/Memoria.log records repeated accepted=False / Speech output failed: Prism NVDA for native Item/Ability/Equip/Status/etc selections. The supplied two audio reports describe silence and interrupted screen-reader speech. Native Prism output succeeds in this machine's isolated current-NVDA probe, so the reporter's exact native error remains unknown. Ghidra confirms the bundled x64 output export returns the integer status with no return-width mismatch. Do not claim the reporter's device or audible gameplay fixed from isolated tests.

**Files:** Memoria.ScreenReader/ScreenReaderManager.cs (recovery ownership); Memoria.ScreenReader/PrismSpeechBackend.cs (existing native error exposure); tests/FF9Access.Tests/SpeechTests.cs (actual manager regression scenarios). Investigate focus/interrupt behavior independently before any change there.

## Review focus

- Preferred initialization succeeds but the first output fails: same text must reach compatibility once.
- Preferred output throws: same recovery and disposal ownership.
- Successful compatibility persists across later output without preferred reinitialization.
- Both backends reject: no accepted serial/text update, no tight retries, no repeated fallback within one call.
- Disabled/disposed managers and healthy preferred output do not initialize compatibility.

## Tasks

- [x] Add failing manager regressions for the above behavior and run Speech suite before production changes.
- [x] Implement the bounded recovery and native numeric diagnostic. Update existing negative fixtures to reject on both backends so they still exercise a genuinely undeliverable request.
- [x] Run full core, menu/context and narration integration checks; build both game architectures and launcher; run original-Mono speech validation.
- [x] Review actual focus/interrupt paths against the recordings; fix only a reproduced failure.
- [ ] Package/deploy the reviewed build with saves, settings, protected files and narration preserved; verify the completed installation and rollback.
- [ ] Report source/native/automated evidence separately from live/audible and recipient-PC results.
