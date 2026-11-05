# FFIX Story, entity and timed-prompt parity

The user wants the functional Story/entity workflow used by Blind Soldier and
Foresight, and reports that IX still gives area-level advice with no usable
interaction. Compare the actual reference implementations before changing the
IX resolver. Chapter/guide metadata alone is not proof of an actionable story
step. The comparison must cover current native phase, local target binding,
travel, arrival/interaction readiness, labels and coverage tests.

The sword-fight report has two source-confirmed causes: all dialogue currently
waits behind description recordings and is batched in order, including expired
timed commands; button glyphs always resolve through keyboard bindings.

Timed prompts must be delivered immediately and individually. Entering the
native Chanbara phase stops owned description audio, discards description cues
for that timed section and never replays expired commands afterward. A rejected
prompt may retry only while that exact native presentation is still current.
Ordinary dialogue and description scheduling must retain their existing behavior
outside the timed section. No input, timer, score or game-speed changes.

Input names follow the device actually used, retaining the last real input source
through idle frames. A connected unused controller must not force controller
names for keyboard play. Resolve the current mapping and the image's fixed versus
configurable binding semantics. Reuse the launcher's existing ControllerNaming
setting for PlayStation, Xbox or Generic labels. Timed keyboard letter names may
use unambiguous phonetics; keyboard remaps and mode changes must be covered.

Root owns audio scheduling, Dialog/button input naming, narrow native input-source
observation and their tests. Claude first owns a read-only comparative Story/entity
audit; Navigation implementation ownership will be assigned after its findings.
Do not modify either reference mod. Preserve the unrelated panorama test, saves,
settings, recordings, native control and rollback. Work in the canonical checkout;
deploy after verification with the game closed. This task does not authorize a new
public release. User has already opted out of repeated design/permission gates.
