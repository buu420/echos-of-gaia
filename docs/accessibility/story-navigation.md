# Story guidance

Select Story with U from Exits or Towns. Use K and semicolon to cycle its
targets, L for status, I for spoken directions and P for automatic movement.
Custom bindings in the launcher still apply. Hold a navigation trigger on the
controller, then use D-pad left/right for categories and up/down for targets.

Story follows the current plot through Alexandria, the journeys across Gaia,
Terra and the final area. It gives the next required scene, local interaction
or travel destination. On the world map, it uses destinations available with
your current transport and helps you approach the appropriate parked vessel.
The four-shrine sequence advances Water, Fire, Wind, then Earth.

In 0.1.6, Pinnacle Rocks keeps fragment collection ahead of the final
conversation. Fossil Roo chooses the next step from the actual reversible
switch bits and the player's verified current floor component. Mount Gulug
keeps its lever step until the native completion flag changes. These rules
require exact original script fingerprints; unsupported state provides current
scene information without an invented walking approach. They wrap only
ordinary, already presented interactive targets. Fossil flower/boarding steps
without such a target remain manual. The player still chooses every dialogue,
lever action and Confirm press.

Story remains selected when you change maps, board a vehicle or advance the
plot. It selects the new objective and stops the old movement. Press P when
you want to walk to the next one. Ordinary Confirm, conversation choices,
boarding, landing, digging and combat remain your actions.

When a reviewed step has a required action, its Story heading keeps that
action while travelling. At the opening Vivi step it says to show the ticket
at the ticket booth, instead of reading only a room name. The installed
catalog regression follows street102 through square103 to booth104; an
optional item-shop108 visit returns to102 without advancing the objective.
Only the actual ticket-completion counter moves guidance to alley105. Missing
presented approaches retain an informational objective with no walking target.

Some steps involve a player-script interaction, dialogue, a puzzle or a scene
without a demonstrated walking target. Those entries give the required action
and explain that automatic walking is unavailable. They have no directional
bearing or invented location. The opening cargo-room candle has a verified
walking approach through its four native touch regions. Story stays on that
step until the room's native phase changes, even though the scenario counter
was already set before control returned. Press Confirm when the question-mark
bubble appears, then choose Light the candle yourself. An unknown or changed
opening script gives current-scene information without a walking target or a
preview of Vivi's later scene. A physically bound ticket-booth step can use the
shown ticket seller even though that interaction runs through Vivi's listener.

Optional towns, side quests, people, objects and discovered pickups remain in
their ordinary categories. Item contents are read from the game's displayed
result. A last-seen target retains its observed position and reports that it
is outside the current view. Its current story eligibility is checked again.

## Research and coverage

Chapter order and travel context were checked against [bover_87's GameFAQs
guide](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891),
including the [four-shrine sequence](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/breaking-the-seal).
The November 1 audit reviewed all 44 main-guide chapter bodies and all 23
optional topic bodies. Wording in the mod is original. Native scripts and
engine code supply progress and physical bindings. See the
[complete comparison and remaining gaps](guide-coverage.md).

The current complete native census contains 818 fields. The older Story
inventory contains 817 fields (it omits field70); Story metadata retains 804
gameplay fields, including Disc 4's Black Mage Village, and excludes 13 ending
fields. It contains 49 reviewed chapter ranges, 364 scene checkpoints, 2,741
potential field transitions and 10,470 entry hashes. Counter-floor writes are
scene checkpoints; they are not treated as proof that talking to an actor
advances the story. Optional Daguerreo and free-time cutscenes are excluded
from required objectives.

World objectives use native marker IDs, so a translated town label does not
break its Story binding. The marker audit also accounts for the world's text
table offset and multiple entrances. The ship's shrine objectives use its
departure rooms rather than unreachable cutscene rooms.

Local Story eligibility follows current native Talk/Push branches and verified
requested functions in detached variable buffers. Unsupported conditions or
unverified dependencies stop that proof. The transition graph supplies
direction between scenes, while current presentation, native guards and the
ordinary route planner establish the selected approach. It is not a complete
simulation of every future map or puzzle state.

The regression suite covers native variable formats and word offsets, guarded
ship interaction, chapter checkpoints, shrine order, retained guard changes,
physical route identities, category progression and non-positional speech.
Both game assemblies also require original-Mono and emitted-integration
checks before deployment. These checks do not establish a complete live
playthrough. Every field's live entrance presentation, controller delivery,
speech and puzzle sequence still needs player validation.
