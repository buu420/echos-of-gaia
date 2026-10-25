# Field navigation coverage

The October 16 audit covers every one of the 818 installed English field event assets. All script entries decode without errors; 816 fields have independently resolved floor geometry. The complete per-field quantities and review status are in [field-navigation-coverage.csv](field-navigation-coverage.csv) and [JSON](field-navigation-coverage.json). These are static research results, not a playthrough of every story state.

## Missing exits and current presentation

Chocobo's Forest field 2950 entry 20 already contains the ordinary native MJPOS/MOVQ world-return sequence. The missing exit was a presentation failure: all 39 qualifying outer-edge samples project outside the permitted camera view, while the real trigger begins inside the visible path. The reader now uses the actual trigger/floor intersection. It does not require that distant outer edge to appear on screen.

The full census found 141 plain walk-out regions affected by this screen-edge rule and 186 ordinary walk-outs inside the floor. The shared repair applies to those patterns throughout the game. Polygon/triangle clipping covers narrow intersections; candidate heights come from native floor triangles. Radius offsets use the floor plane's three-dimensional edge distance, including slopes. Every movement candidate must still pass the existing full native capsule/segment checks and select this region as the game's first applicable touch region. Overlapping or disabled triggers do not gain an approach.

A plain exit requires established MJPOS and MOVQ/requested native walk-out behavior plus a currently rendered background at an on-screen part of its trigger. A script's possible map jump alone is insufficient. Animated exits retain their own constant BGA reference, current camera, active overlay, enabled renderer, ordinary placement flags and actual live bounds. Live overlay observations are shared within a frame and refreshed next frame; geometry intersections are cached by geometry and polygon identity. Battle/loading contexts do not prepare field targets.

An identifiable entrance with no safe approach remains listed for information. Its informative floor/boundary samples never become autowalk or sound points. Labels remain Exit A, Exit B and so on until ordinary game presentation supplies more information; hidden destinations and outcomes are not announced.

Field 619 entry 7 mixes a plain walk-out with a separate animated branch. It now uses a detached current-branch check, including MAP5[31] and the native signed/rounded controlled-player height. Unknown height, the alternate mode, input, requests or an animation on the active path cannot supply plain evidence. The ordinary default object-property interpreter remains unknown; this narrow provider supplies only the controlled player's pre-movement height. No actor or reward properties are scanned.

## Reviewed authored surfaces

Eight camera-specific records in [field-exit-evidence.json](field-exit-evidence.json) pin the script, background and BGI hashes and exact live trigger polygon. They cover field 503 entries 9/10, 615 entry 5, 2509 entry 3, 3100 entry 19, 2508 entry 4 and 1608/1707 entries 6/3. The new kitchen-return stairs require their actual opening overlay 2; the Ipsen stair/arch requires overlay 16. Current rendering, viewport, request permissions and safe approach remain mandatory.

The Cargo Ship routes can show incidental asynchronous departure text. The field 615 staircase can start a cutscene and reload the same map. Neither record names a later destination or cutscene outcome. Confirm, dialogue and choices remain under player control.

Eight reviewed cases remain excluded from ordinary walking exits: fields 1607/1706 have unresolved conditional/occluded presentation, 350/2201/2209 have blocking or party/script sequences, 1423 is a scripted traversal, and 2504 entries 8/9 are falling-floor events. Earlier local classification missed required callees; those cases are not promoted by a terminal MAPJUMP. The old field 2201 boundary record was removed. Four additional static touch candidates lie only on player-blocked attribute-0x80 geometry (206 entry 24, 352 entry 14, 602 entry 7 and 1352 entry 11); they remain unresolved. Runtime permission changes still control actual geometry.

## People, objects and interaction state

The census includes 2,778 nonplayer People candidates and 980 Objects candidates. These are script-entry quantities, not simultaneously visible targets. Rendered actors and attributable native interaction bubbles supply runtime targets. Native talk inhibition, current script/request priority, collision inhibition and per-map talk/push rules now determine whether an observed actor is currently interactive. Empty or guarded handlers are read on detached variable copies. Any assignment or unsupported value remains unknown and keeps ordinary manual interaction; rewards and hidden state are never spoken. Reviewed player-owned conversations retain their intentionally empty listener handlers.

Sixteen individually reviewed talking-region bindings associate an already observed speaker with its actual ground/counter Confirm spot. They require exact actor/region hashes, model/UID, the same observed render-object identity, current region permission and the speaker's required request priority. Fifteen ambiguous, distant, multi-actor or self-interactive candidates remain unbound. See [interaction evidence](field-interaction-evidence.json). A region bubble does not refresh an offscreen actor's coordinates. Last-seen targets retain their observed position; changed or revoked interactions cannot steer a viewing route through an old talking region. Noninteractive visible objects use viewing points.

Native-icon-only objects remain ordinary manual interactions. A conversation or choice that later changes maps is not automatically an exit. These rules preserve ordinary exploration rather than listing hidden rewards or future actors.

## Exit recording

Blind Soldier's original exit recording is retained byte-for-byte with provenance in `accessibility/navigation/manifest.json`. A separately identified PCM conversion feeds an owned, cached audio output. Near a listed, currently usable exit with a clear same-floor segment, a pulse grows louder from 400 to 80 native units and repeats at most once every 3.2 seconds. Walls and stacked floors cannot supply proximity by distance alone. The nearest eligible exit sounds; the navigation list identifies every target.

The launcher has Exit proximity sounds and Exit sound volume (0–200 percent). Defaults are enabled and 100 percent. The game's sound-effects mute and volume also apply. Menus, dialogue, battles, Hot and Cold rounds, lost focus and map teardown suspend the cue. Its audio output owns only this recording; stopping it does not stop game sound, footsteps, speech or descriptions. No files are read on pulse playback.

## Reproduction and limits

Run `py -3 tools/Export-FieldNavigationAudit.py --game-root GAME --output-root RESEARCH --unitypy-root UNITYPY` from the source checkout. Omit the last option when UnityPy is already installed. Outputs must be separate from the game. The tool checks that every installed English field asset has a mapping and an audit row. Its raw external research files contain script disassembly and potential outcomes and are never runtime target data. Published coverage omits destinations, rewards, coordinates and dialogue.

Root reproduced the independent census with identical field records. The correct BGI edge convention is native neighbor n[(edge+1)%3] for vertex edge (v[edge],v[(edge+1)%3]); older October 10 exporter edge annotations used the wrong pairing. Reconstructed art supports authored review but does not prove current live rendering. Native managed engine rules and retained Ghidra native-audio analysis informed the repair; no original Unity project recovery is claimed.

Research, image provenance, failed/passing regressions and independent review are retained under `D:\FF9Access\research\2026-10-16\field-navigation-audit`. See [validation evidence](live-validation.md). Fresh live Forest departure, every repaired doorway/actor state, physical controller use and audible exit-cue listening remain unverified until exercised in the game.
