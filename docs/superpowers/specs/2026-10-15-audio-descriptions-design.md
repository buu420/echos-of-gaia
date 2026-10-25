# FFIX audio descriptions

The user requested visual descriptions throughout FFIX, including physical
actions during dialogue and FMVs, in their established narration voice. The
Blind Soldier and Foresight chats and source are reference implementations.
Existing instructions authorize research, generation and checked deployment
without repeated approval requests.

## Intended behavior

Describe visible action, expression, staging and important screen text at the
time they appear. Dialogue remains the existing screen reader's responsibility.
Use actual footage and rendered native observations, never hidden event flags
or an animation number alone as a description of a character's gesture. Keep
character identification consistent with what has been presented to the player.

Inventory all 42 installed FMVs (42.69 minutes) and distinguish the 18 animated
background films, whose rendered actors can be separate. Review actual films in
Descript and locally, author original timed cues, generate the approved voice,
and validate wording, waveform and scene-window fit. Keep an explicit coverage
ledger for unsupported or unverified field gestures rather than guessing them.

## Playback architecture

Use native movie ownership and uploaded-frame time, with immediate pause,
resume, stop and destruction notifications. No second free-running story timer
may continue after skip, scene replacement or loss of foreground. Expired cues
must not accumulate. The original movie, soundtrack, game controls, Confirm,
combat and progression remain untouched.

Use an owned Windows waveOut device for narration, independent of the game
audio engine and footsteps. Canonical narration is 24 kHz mono PCM16 WAV with
manifest-bound text, hash and duration. Keep driver-owned buffers alive until
unprepare succeeds. Audio read/verification happens outside frame-sensitive
game work. Contain errors and provide an accessible failure or screen-reader
fallback rather than silently losing information.

Field narration requires visible native action proof and a current scene/actor
identity. Coordinate descriptions with native dialogue transitions; following
dialogue must not cancel an accepted recording accidentally. Document the
screen reader's actual completion capability and do not invent completion
timers. Scene/menu/battle/focus/quit changes invalidate stale queued work.

## Voice and privacy

Reuse the creator's approved natural-voice reference from Foresight/Blind
Soldier. Fish generation is a development tool, not a player dependency. Keep
credentials, private model IDs, human recordings, uploads and original game
video outside the source and release. Ship generated narration, captions,
timings, provenance and required runtime support only.

## Validation and delivery

Test time jumps, pauses, natural completion, skips, foreground loss, replacement,
stale asynchronous results, busy dialogue, output rejection and driver cleanup.
Verify both emitted assemblies and original Mono runtimes, launcher settings,
all generated audio and cue windows. Commit and build the exact source, package,
preserve current saves/settings, deploy both architectures and independently
verify payload/protected files. Automated checks do not establish live listening
or complete field-action coverage.
