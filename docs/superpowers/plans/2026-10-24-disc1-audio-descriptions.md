# Disc 1 audio-description implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development for independent product tasks; root owns footage/editorial review and final installation.

**Goal:** Add reviewed native-event descriptions throughout Disc 1 and prepare an installable release candidate.

**Architecture:** Extend exact-site pose selection, add bounded map-group audio banks, and render/append only new recordings. ViddyScribe text drafts remain private until visually reviewed and natively bound.

**Tech stack:** C#/.NET 3.5/Unity Mono, .NET 8 offline tests, Python Fish/ASR tools, ViddyScribe MCP, PowerShell packaging.

**Spec:** ../specs/2026-10-24-disc1-audio-descriptions-design.md

## Global constraints

- No gameplay, control, Confirm, combat or original-audio writes.
- Each runtime bank <=512 clips and <=33,554,432 bytes; whole pack <=4096 clips.
- Preserve all existing 496 recordings, movie definitions and opening mastering.
- No private media, voice references, keys, rejected takes or dialogue dumps in release.
- No claim of complete Disc 1 coverage for an unreviewed or unbound branch.

## Review focus

- Same clip at another script site must not acquire contextual story wording.
- Partial/reversed/offscreen/interrupted animation must not imply a complete action.
- Battle and same-group returns must cause no additional audio preparation/read work.
- An old bank completion or queued owner must never play in a newer scene.
- Appending must preserve existing mastered audio and enforce membership/privacy limits.

### Task 1: Bounded field bank metadata and runtime selection

**Files:** NativeDescriptionCatalog.cs, NativeAudioDescriptions.cs, FieldMap.cs (one field-open call); tools/audio_descriptions/build_pack.py; tools/FF9Access.Deployment.ps1; focused bank/pack/integration tests.

**Interfaces:** Optional public `fieldBanks` array per spec. NativeAudioDescriptions.FieldOpened(int field) prepares/selects the bank. Existing QueueFieldCue(string,string,Func<bool>) and proof observer API stay unchanged. Bank selection follows cue membership and queued bank owner, not an inferred filename.

- [ ] Reproduce global-cap, membership and ownership failures with tests.
- [ ] Parse/validate explicit banks, keep the old generic-bank behavior when absent.
- [ ] Add detached preparation, same-group/battle reuse, correct queue ownership and cleanup.
- [ ] Add `--append-fields` mode with `--replace-from`, `--voice-dir`, `--out`, `--catalog`, `--qa`, `--field-banks`, preserving existing recordings/mastering. New field cue JSON uses `{cues:[{cueId,voiceText}]}`.
- [ ] Run focused and full relevant suites; independent review and fixes.

### Task 2: Text-only generation input for new approved voice cues

**Files:** tools/audio_descriptions/render_fish.py and focused Python tests.

**Interfaces:** Add optional `--append-script` path, JSON `{cues:[{id,text}]}`; existing --source-pack remains the validated immutable baseline. Render only these new IDs, rejecting duplicate/old IDs. Bind job identity to the append script hash; preserve existing CLI behavior and natural delivery/QA.

- [ ] Write failing script identity/selection/privacy tests.
- [ ] Implement the mode without placeholder public WAVs or regeneration of old clips.
- [ ] Run tests; independent review and fixes.

### Task 3: Reviewed contextual pose selection and content

**Files:** FieldDescriptionModel/Tracker/Catalog.cs, NativeFieldDescriptions.cs; generated contextual catalog and public evidence/coverage data; focused tests and native emission checks.

**Interfaces:** Exact contextual key `map:sid:entrySHA256:instruction`, model, clip. Three-argument pose lookup selects contextual entry or generic fallback. Native start computes site for context-only candidate pairs. Publishing retains existing Cue signature/current predicate.

- [ ] Reproduce global wording leakage and context-only eligibility failures.
- [ ] Add exact-site selection with unchanged native visibility/progress guards.
- [ ] Review ViddyScribe scene notes against reference/native footage; bind each accepted cue to source evidence and actual observer proof. Keep unresolved rows explicit.
- [ ] Add reviewed range/multi-actor proof only where real evidence requires it; never weaken guards to fill a coverage row.
- [ ] Generate only accepted new voice cues; append them with explicit banks and immutable-old-audio proof.

### Task 4: Release candidate verification and deployment

- [ ] Whole-change independent source/content review; resolve material findings.
- [ ] Build All/Both; core, native context, narration, launcher, emitted and exact-Mono checks.
- [ ] Verify actual pack WAV/word/bank/catalog hashes and old-audio preservation.
- [ ] Snapshot player settings/saves/audio, package clean committed source and deploy.
- [ ] Verify installed payload/protected files/rollback and user-data preservation; publish an honest coverage/validation report, without remote release publication.
