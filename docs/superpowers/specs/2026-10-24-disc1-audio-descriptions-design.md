# Disc 1 visual narration

## Intent and scope

Extend the existing FFIX descriptions through the original Disc 1 ending at
Burmecia. Preserve the approved natural personal voice, louder opening, all
existing FMV narration, navigation, HRTF cues, native dialogue and player control.
The user authorizes extraction from the installed game and YouTube reference
footage, prefers ViddyScribe to an expired Descript subscription, and has waived
repeated approval questions. Proceed on the current local feature branch.

Installed FMVs are already described. The missing work is contextual in-engine
action, including meaningful gestures during dialogue. A native inventory is not
proof that every branch was seen. Maintain a scene coverage ledger with actual
video, model/clip and native-script evidence; never label an unreviewed branch
complete. Main-story chapters run from the theater ship to Burmecia. Optional
and revisited branches require separate evidence.

## Sources and authorship

Use the installed models/animations/event scripts as the native authority.
ViddyScribe supplies editorial drafts from the selected PC Disc 1 recording:
https://www.youtube.com/watch?v=PKML_rC1JoA. Inspect its footage and compare
matching installed clips before binding a description. Its enhanced backgrounds
do not establish colors or scenery in another installed renderer. The existing
native FMV hashes/timelines remain authoritative. Follow the supplied GameFAQs
guide only for chapter ordering, never as visual proof.

Use short original present-tense sentences describing visible physical action.
Avoid invented thoughts, future spoilers, reciting dialogue or names not yet
introduced. Customizable characters retain appearance wording. Record review
provenance privately; include ViddyScribe attribution in public credits. No
third-party footage, dialogue dump, credentials, human voice references or
rejected takes may enter the release.

## Chosen approach

Build on the actual field observation pipeline. Select contextual pose wording
with exact `map:sid:entrySHA256:instruction` plus model/clip identity, then apply
the existing sampled-progress, visibility and actor-lifetime proof. A generic
caption remains the fallback at other sites. Context-only clips must be eligible
for review without making their wording global. Field narration never follows a
recording's wall clock. Multi-actor or environmental facts require their own
positive native presentation proof; a script or dialogue opening alone is not
enough. Reviewed partial/reversed clips need explicit range proof, not removal
of existing guards.

Alternatives rejected: fixed-time field narration cannot follow user-paced
dialogue; generic opcode/name descriptions can narrate invisible or misleading
actions; regenerating the entire accepted voice pack wastes existing work.

## Memory and pack contract

Keep legacy movie banks and the existing generic field bank. Additional scene
recordings use explicit map/group banks in optional `fieldBanks` metadata:
`[{"id":"disc1-opening","fields":[50,51,52],"clips":["disc1-..."]}]`.
Each field belongs to at most one added bank. Each added clip belongs to one
declared bank; movie clips cannot be in field banks. Unassigned nonmovie clips
are the backward-compatible generic field bank. Validate every bank at <=512
clips and <=33,554,432 audio bytes. Cap the whole public pack at 4096 clips,
rather than treating its entirety as one runtime bank.

Use one replaceable added field bank, prepared on a detached worker at field
open. Reuse its verified owner/data across same-group scene changes and battles.
Bank ownership is separate from scene/cue lifetime. A late replaced bank cannot
publish; queued cues hold the correct bank and owner. Missing/loading/corrupt
audio uses the existing owned text fallback without blocking native gameplay.
Preserve existing dialogue ordering, cancellation, focus/pause and narration
priority.

## Voice and validation

Render only new reviewed cue IDs using the existing approved Fish references,
S2.1 Pro free-tier configuration, word/rhythm checks and 24 kHz mono PCM16 output.
No paid model fallback or top-up. A text-only new-cue render mode must bind job
identity to the cue script and source pack, preserving all private evidence.
Append verified recordings in an independent pack directory; all existing 496
recordings, movie definitions and opening-mastering metadata stay byte-identical.

Regression tests precede behavior changes. Verify site fallback/proof/lifetimes,
bank ownership/cache/replacement, public pack membership/privacy and preserved
old audio. Build/test both architectures and original Mono, run integration and
package checks, then snapshot and deploy with rollback and save/settings/audio
preservation verification. Report offline evidence separately from live gameplay
or listener approval, and report any remaining coverage gaps honestly.
