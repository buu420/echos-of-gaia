# Audio-description validation

## Disc 1 scene expansion, 2026-10-24

The expansion adds 112 personal-voice recordings: 109 exact animation-site
captions and three significant-object captions shared by five native factory
entries. The pack now contains 608 recordings. All 496 original WAVs and
manifest rows, all 42 movie definitions, the approved voice format and the
24-clip opening treatment remain unchanged.

Independent data review reread the actual installed scripts for all 46 fields
used by these additions. Entry hashes, executing or presented-object SIDs,
instruction positions, model assignments and actual animation names match.
The five partial-range assets were reread and their native sample counts
and sampling times recomputed. Ghidra analysis of the matching installed
Unity executable establishes that legacy clip length follows its animation
curves; the unrelated serialized muscle-clip stop time is not used for these
legacy assets. The proven native counts are 60, 80, 116, 34 and 50.

The pipe-hanging and knight-toppling captions require the later visible phase
of their actions. Pipe hanging also requires the later exact native sequence
and observed movement. Descriptions involving a visible companion retain its
identity and lifetime; the plant-cage relation also requires the reviewed
attachment. Factory appearances are separate from animation-start events:
static presentation, advancing animation and actual movement have distinct
positive proof requirements. Initialization during a fade or off screen does
not consume a description.

All 112 new recordings pass exact automatic word checks and pacing checks,
with zero manual word-check overrides. Rejected drawn-out takes were retained
privately and repaired through fresh takes or clearer wording. Speech was
not time-stretched. ViddyScribe supplied text-only source notes; Fish generated
the approved personal narrator. The reference copies used to obtain later
drafts ran at twice normal speed, and accepted facts were checked against
original-speed footage and installed pose assets. Reference times never drive
user-paced field narration.

The final PCM files total 87,553,426 bytes. Eleven new scene banks share 46 map
owners; the largest new bank has 20 clips and 3,253,994 bytes. The retained
generic field bank remains 25,751,092 bytes. Every individual bank stays below
the 512-clip and 32 MiB limits. The manifest SHA-256 is
`86fdafc7a1af78e9df7c6865fd9d4dfbb0a7a14621440c732ddf699657de8894`.

Source verification passes 527 core checks, 63 narration integration checks,
eight native navigation-context checks, 30 audio-tool tests, six mastering
regressions and the strict PowerShell bank/pack checks. Independent reviews
resolved the early pipe and knight captions, removed repeated renderer work
after an object description, and strengthened attachment proof to read the
live bone-parent hierarchy rather than historical UID/bone metadata alone.

This is a selective Disc 1 expansion. Coordinated choreography, some prop
interactions, unrecorded optional branches and fade-obscured actions remain
gaps. Automated word checks and native-runtime probes do not establish
listener approval, full in-game synchronization, controller operation or a
complete Disc 1 playthrough. Those live checks remain pending.

## Natural voice revision, 2026-10-17

All 496 recordings now use the creator's accepted Fish personal voice and
natural pacing: 284 FMV cues across 42 native movies and 212 selective field
captions. Every caption, cue identity, source-movie hash, native scene window
and field observer is preserved. The 24 title/new-game recordings retain the
offline 10 dB opening treatment with oversampled peak limiting. The revised
recordings add no synthesis or audio processing to the game loop.

The final mastered files independently passed all 496 word checks using
unprompted local recognition. The generation evidence contains
477 exact-text recordings and 19 existing spelling/homophone equivalents, with zero manual word-check overrides.
Disputed recognition was rechecked against the same actual waveform with
independent local models; original failures, job identities, crops and selected
waveform hashes remain preserved in the private research archive. Speech was
neither time-stretched nor pitch-edited.

All final files are canonical 24 kHz mono PCM16 with matching public hashes,
native-window margins and runtime budgets. Total audio is
69,773,484 bytes; the field bank is
25,751,092 bytes (24.558 MiB), below
32 MiB. No clipped samples were found; the highest 192 kHz reconstructed peak
was -1.763 dBFS. Median full-clip RMS is
-13.82 dBFS for the opening recordings and
-20.44 dBFS for the other recordings.

Fifteen focused offline-tool tests pass, including independent input/QA
preservation, interrupted-take retention, reference/job/hash bindings, sample
clock conversion and word/rhythm/window guards. Independent review found no
unresolved blocker. The public payload contains generated WAVs and their public
catalog; human references, service credentials, rejected takes and source video
remain outside it.

The creator approved the representative natural-pacing preview. That is not
approval of all 496 takes or proof of in-game synchronization and soundtrack
balance. Some synthetic vocal character can remain. Full in-game listening,
controller coverage and a complete playthrough remain unverified.

Catalog SHA-256:
`3ff922ace37d019df40eb10c01a11e3801fc13904701d24f6d3d0bf7e47533c9`.

## Opening revision, 2026-10-16

The title and new-game opening retain their original narration words, source
identities and exact sample counts. Their 24 recordings now have a 10 dB gain
before oversampled peak limiting; measured RMS increases are approximately
5.8–10 dB. Independent checks at 44.1, 48 and 192 kHz found no clipping; the
highest reconstructed peak was -1.75 dBFS. The other 472 recordings are
byte-identical. The current catalog and PCM pack pass the same strict pack
validators, plus six mastering regressions covering timing, headroom, silent
inputs, original preservation and rejection of accidental double processing.
The whole core suite passes 426 checks. Live listening of this revision is
pending. At this point the Fish voice auditions remained outside the installed pack
while delivery was refined with the creator.

Prior catalog SHA-256:
`5bdd89427c485f215663a630bc60f0b534698a996464a0a460a19ee85363085f`.

## Initial pack verification, 2026-10-15

This addition has offline and native-runtime evidence. It has not received
in-game listening approval or a complete playthrough.

| Check | Observed result |
| --- | --- |
| Reviewed movies | 42 native FMVs; 284 original timed descriptions. Descript drafts were corrected against native-frame sheets, including dense action and ending sequences. |
| Field catalog | 84 reviewed poses and 128 observed movements; 212 fixed captions. Exact model/clip and native observation are required. This is selective coverage. |
| Final recordings | 496 canonical 24 kHz mono PCM16 WAVs; 74,741,504 audio bytes, approximately 25.94 minutes. All hashes, durations and source/text identities validated. |
| Word/audio checks | 482 exact recognizer matches and 14 approved spelling/homophone equivalents. Zero manual word-check overrides. Outer silence only was trimmed; speech was not stretched or pitch edited. |
| Memory budget | Field bank 26.396 MiB; each movie bank also satisfies the 32 MiB/512-clip limit. The field bank is retained once and movie banks replace one another. |
| Core regressions | 375 passed, including native-time scheduling, WAV/driver ownership, detached bank verification, field visibility/lifetimes and navigation regressions. |
| Central runtime integration | 51 scenarios passed against actual runtime sources with controlled engine/device/speech fakes. Includes ordered held dialogue, pause/quit/focus/skip, stale actors, device errors, late recovery and rewinds. |
| Emitted game assemblies | Eleven IL verification groups passed on each architecture, including descriptions and field observers plus existing input, speech lifetime, battle, world, Forest and Story contracts. |
| Original Mono | Both installed Mono 3.0.40818 runtimes passed actual emitted catalog loading under fr-FR, WAV parsing, silent waveOut pause/resume/completion, owned SAPI control and verified bank/reset probes. No game process was attached. |
| Owned fallback | Separate native probes passed 31 silent checks per architecture; the actual managed wrapper independently passed eight checks per architecture. Includes independent queues, wrong-thread rejection, purge while paused and idempotent pause/resume. |
| Launcher | Twenty actual WPF checks passed: accessible names/patterns, keyboard reachability, description enable/gain persistence and validation, cursor resources, settings preservation and intercepted x86/x64 launch requests. Real game starts: zero. |
| Pack validators | Six Python tests and eight PowerShell checks passed. Both independent final validators accepted the actual pack and matching public catalog. Both emitted resources were independently extracted and matched byte-for-byte. |
| Review | Independent source and native-runtime review found and verified corrections for boundary, speech preservation/recovery, actor lifetime and mid-jump classification defects. No unresolved material finding remained within the reviewed scope. |

Catalog SHA-256:
`1cb2e4b5cecec6b9aae97265c9b6a1eed723d99e02eccc726b8d9952c4b983d5`.
Release source, file hashes and rollback provenance are recorded by the ordinary
package and installed-build manifests rather than hardcoded into this document.

The launcher cursor check exposed a URI that resolved against a hosting test
assembly. The two cursor resources now name their owning launcher assembly,
using Microsoft's documented [referenced-assembly pack URI form](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/pack-uris-in-wpf#referenced-assembly-resource-file).

Unverified: audible voice quality and user preference; live FMV synchronization
and native soundtrack balance; full field choreography; foreground scenery
occlusion; alternate costumes/unreviewed NPC gestures; controller use and a
complete playthrough. An emergency shared-screen-reader caption can finish
after a boundary if both recorded and owned SAPI output fail, because the shared
queue has no individual-cancellation API. Native dialogue takes priority over
globally silencing that queue. See [audio-descriptions.md](audio-descriptions.md).

The private source recordings, voice profile, model identifiers, service uploads,
API credentials, original game movies and research intermediates are excluded
from the payload. The local research archive retains successful and rejected
generation evidence, native proof sheets, review probes and detailed logs.
