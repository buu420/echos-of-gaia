# Audio descriptions

Audio descriptions add recorded narration of visible action to the existing
dialogue reader. The narrator uses the creator's approved personal voice
recordings previously used for Foresight and Blind Soldier. This revision uses
Fish Audio S2.1 Pro with the creator's conversational and descriptive samples,
simpler delivery cues and checked natural speech pacing. Some synthetic vocal
character can remain; the accepted preview is not a claim of human performance.
Players need no voice account, network connection or synthesis software.

In the Memoria launcher's Accessibility settings, enable **Audio descriptions**
and set **Description volume** from 0 to 200. The default is enabled at 100;
zero mutes descriptions. This volume affects narration only. Original music,
effects, dialogue, movie files and the system mixer retain their own settings.

The title sequence and new-game opening have louder narration: their 24 clips
receive a pre-rendered gain with oversampled peak limiting. The original 496
recordings retain their approved voice and bytes. Additional field recordings
use the same personal references and word/pacing checks. This adds no live
synthesis or audio processing during battles.
Description volume and mute still apply normally.

## Coverage

The pack describes all 42 installed FMV sequences, including the opening,
ending and changes in the closing credits. Its 284 movie cues were written
from the actual installed footage, reviewed in Descript and against local
native-frame contact sheets. The closing song has a long clear stretch without
narration. Credit transitions are described; every individual credit name is
not read. Eighteen other movie assets are animated field backgrounds rather
than standalone FMVs and do not receive an independent timed narration track.

Field descriptions cover 84 reviewed character/object poses and 128 observed
movements: entering or leaving the view, jumping and turning around. There are
212 fixed captions. A cue requires the exact reviewed model and actual sampled
animation, or the observed movement of a reviewed subject. Starting an event
script or finding a suggestive animation filename does not establish a gesture.
Appearance wording avoids exposing names before their introduction and remains
correct when the player customizes party names.

Field coverage is selective. Alternate costumes, many NPC models, subtle facial
expressions, particles and unreviewed poses remain unsupported. The static
research found 9,550 scripted animation uses, of which the 84 reviewed poses
occur at 1,114 sites; those counts are not a claim that every such site was
observed in gameplay. Foreground scenery occlusion is not yet proven in the
live renderer: an actor may pass the screen/mesh visibility checks while hidden
behind an opaque overlay. Full scene choreography and every field action are
not covered. See [field-action-descriptions.json](field-action-descriptions.json)
for the exact model, clip, movement and wording catalog.

The Disc 1 expansion adds 112 recordings alongside those generic captions:
109 exact animation bindings and five factory-object bindings sharing three
object recordings. Its reviewed additions extend through the theater,
Alexandria, Evil Forest, Ice Cavern, Dali, cargo ship, Lindblum, the gates,
Gizamaluke's Grotto and Burmecia. A changed script, different actor model or
different animation cannot inherit the scene-specific wording. Some actions
also need a later native sequence, actual movement or another visible actor
before their caption is eligible. See
[disc1-field-descriptions.json](disc1-field-descriptions.json) for the precise
bindings and current coverage status. This remains a selective expansion;
the file explicitly records that complete Disc 1 coverage has not been proven.
Five split animations use bounds and actual native clip lengths verified
against the installed assets and Unity code. Late-phase captions wait for
the meaningful part of the action. Significant factory objects earn their
descriptions from visible sampled presentation; running and moving objects
also require observed animation or position changes.

Coordinated rescues, some telescope, food and detached-weapon interactions,
fade-obscured actions and optional scenes absent from the reference still
need coverage. A full Disc 1 playthrough and listening review remain pending.

## Playback and dialogue

FMV cues follow the frame time actually uploaded by the native movie player.
Late or expired cues are skipped rather than read over a later scene. Native
pause, skip, replacement, fading and focus loss suspend or end owned recording
playback. A stalled movie frame cannot leave a free-running narration clock.
The source movie's bytes and SHA-256 must match the reviewed film before its
descriptions can play. An incompatible source reports an accessible warning.

Field recordings wait for a dialogue boundary. If a new native page appears
while a recording is finishing, its text is retained and spoken in order;
closing or advancing the window does not discard an already displayed line.
Confirm, choice selection and the game's own timing remain under player
control. Queued visual observations expire at scene, focus, menu, battle and
actor-lifetime boundaries. The global observer also runs while the field loop
is paused, including the quit confirmation.

Recordings use a dedicated Windows waveOut device. They are read, hashed and
validated on detached workers, with a movie bank, a generic field bank and one
replaceable bank of additional scene recordings. Each bank is limited to
512 clips and 32 MiB. Maps in the same group reuse a prepared bank, including
returns from battle. Playback does not create a game sound engine, alter actor state, issue
input, start battle work or search asset files on each frame.

If a recording is missing or its device fails, standalone descriptions use a
separate Windows SAPI voice whose queue belongs to this mod. It can pause,
resume and stop independently of the screen reader. This emergency voice uses
the system's default SAPI voice rather than the personal recordings. Native
dialogue remains on Prism. If both recording and owned SAPI output fail, the
caption is submitted to the screen reader as a final fallback. That shared
queue cannot cancel an individual caption; an accepted caption can finish
after a boundary. It is never globally silenced at the expense of native
dialogue. A caption combined with already displayed native text likewise
retains native speech ownership.

## Rebuilding and evidence

The public script is [audio-description-script.json](audio-description-script.json).
The runtime catalog is [audio-descriptions.json](audio-descriptions.json), also
embedded in both game assemblies. Only generated PCM16 WAV files and public
captions/timings/hashes enter the release. Human voice recordings, private voice
profiles, API keys, model IDs and game video are excluded.

Offline generation uses `tools/audio_descriptions/render_fish.py`, verified
private human reference profiles and local Whisper ASR. Its job identity binds
the catalog, reference hashes and synthesis settings. Four bounded requests use
the exact `s2.1-pro-free` model; there is no paid fallback. Failed takes remain
private. Word checks, held-word checks and native scene limits reject unsuitable
takes, and interrupted work preserves raw evidence.

Rebuild into an independent directory with
`build_pack.py --replace-from <existing-pack> --voice-dir <accepted-takes>
--out <new-unmastered-pack> --qa <private-report> --voice-description <provider>`.
The replacement preserves native movie and field identities and removes the old
opening-mastering marker. Run `master_pack.py` on that new unmastered pack,
then verify the final bytes before promotion. Private QA must stay outside the
voice inputs and public pack. Runtime players need only the resulting WAVs and
catalog; generation dependencies and account access are not installed with them.

For additional field cues, `render_fish.py --append-script <text-only-script>`
renders only new IDs against an immutable `--source-pack` baseline. Then use
`build_pack.py --append-fields <approved-script> --replace-from <baseline>
--voice-dir <accepted-takes> --field-banks <new-bank-membership>
--out <new-pack> --catalog <public-catalog> --qa <private-report>`.
This mode preserves all old WAV bytes, movie definitions and existing opening
mastering. Keep its original baseline and generation job inputs for repairs.

Native movie inventories, visual proof sheets, Ghidra Unity animation evidence,
field coverage ledgers, generation checks and rejected takes are retained in
the local research archive. The authoritative field hooks come from Memoria's
managed engine; Ghidra's native Unity analysis does not recover an original
Unity project.

Audio-description source notes for the Disc 1 expansion were generated with
[ViddyScribe](https://viddyscribe.com), then checked and rewritten against
the reference footage and installed game assets. ViddyScribe supplied text;
the shipped narration uses the creator's approved Fish personal voice.
The reference was MilanRefreshedGaming's
[Disc 1 game movie](https://www.youtube.com/watch?v=PKML_rC1JoA).
Reference video, account credentials and human voice samples are not included
in the distribution.

Automated word recognition, voice-reference comparisons, WAV integrity and
scene-window checks do not establish listener approval or live synchronization.
In-game listening, physical controller use and a complete playthrough remain
unverified for this addition. Existing navigation/battle test observations do
not serve as audio-description gameplay proof.
