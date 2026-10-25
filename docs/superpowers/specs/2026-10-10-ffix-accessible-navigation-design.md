# FFIX accessible navigation and launcher design

- Date: 2026-10-10, America/Chicago
- Status: specification and implementation plan approved; Native execution in progress
- Source baseline: BlindGuyNW/Memoria, `0b5f29fcded398e1ffbc2e09ed55b1117af8a7f7`
- Local branch: `codex/ffix-accessible-navigation`

## 1. Intended result

Extend the existing Memoria accessibility fork so a blind player can identify and approach the same exits, people, objects, towns, regions, and vehicles that the game presents to a sighted player. Add optional automatic movement and an accessible launcher. Preserve the fork's dialogue, menu, and minigame announcements while moving its speech facade to Prism.

The user approved this scope in conversation: field navigation, world regions and towns, live vehicle and parked-airship tracking, automatic walking, native airship autopilot, configurable familiar keyboard controls, an R3 spoken controller menu, launcher accessibility, and deployment for both installed architectures. Boarding, landing, interaction, and Confirm remain deliberate player actions.

Success means the installed game exposes useful information and reliable controls in actual play. A successful build alone does not establish accessibility. Ordinary encounters, collisions, locked passages, and story restrictions continue to apply. No navigation command teleports, grants items, changes progression, automates combat, or presses Confirm.

This is one integrated delivery. Its components can be developed separately, but the requested world navigation, regions, vehicle tracking, automatic movement, speech, launcher, and deployment are all acceptance requirements.

## 2. Research baseline and limits

Read-only research used ILSpy, Ghidra, the linked GameFAQs guide, the Memoria source, and the current Blind Soldier and Foresight source. The research exports cover all 74 installed managed-binary paths and 22 unique native game/runtime/plugin binaries. Ghidra recovered 112,006 functions; 111,996 have pseudocode and 10 have assembly/error records. These are approximate recovered representations, not the original Unity engine source or a rebuildable native engine.

The installed x86 and x64 `Assembly-CSharp.dll` files currently match their stock backups. Leftover Memoria support libraries and old logs do not establish an active accessibility installation. The current stock launcher is a .NET Framework 4.5 WPF application with image-based mouse controls. The maintained Memoria launcher source targets .NET Framework 4.8 and provides the better replacement foundation.

Research evidence is retained under `D:\FF9Access\research\2026-10-10`, especially `research-findings.txt`, `decompilation-coverage.json`, the managed exports, the Ghidra projects, and the native/localized world-region text exports. No game process, live speech, controller hook, or launcher UI Automation behavior was tested during research.

Primary source references:

- [Memoria fork](https://github.com/BlindGuyNW/Memoria), including `Assembly-CSharp/Memoria/Accessibility`, `Global/ff9/ff9.cs`, `Global/World/WorldHUD.cs`, `Global/WM/WMUIData.cs`, `Global/Walk/WalkMesh.cs`, and `Global/Field/Map/Actor/FieldMapActorController.cs`.
- [Andrew Testa's FFIX guide](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/29957), used for player-facing controls and travel context. Native state and code determine eligibility and movement.
- Local Blind Soldier and Foresight source, used as behavioral references. New FFIX implementation remains independent; game-specific hooks and GPL code are not copied into this MIT fork.

## 3. Information eligibility and identity

Every selectable target carries an identity, current position, category, display label, eligibility evidence, and a context generation. Position and availability are refreshed from native state; stale coordinates never authorize movement.

### Fields

- Categories: Exits, People, and Objects. Empty categories speak that they have no available targets.
- Include actors only when the current scene actually presents them, applying native show/render, activity, and interaction conditions. A script object that exists but is hidden is not a visible person or object.
- Visible background exits without rendered actors require evidence that the exit is presented in this scene. Build an entrance-candidate audit from active, running native triggers, control flow reachable without assumptions about unobserved state, and enabled walkmesh approaches; match those candidates to visible doors or screen edges through native presentation evidence or reviewed background/map metadata. A jump opcode alone is not proof of a visible exit. Do not require an exit icon that the game does not draw. Resolve unexplained gaps in visible-exit coverage rather than silently filtering out ordinary doors; retain evidence/exclusions for hidden or conditional candidates.
- Use current visible labels, visible dialogue identity, or identity already established to the player. A visible but unnamed actor receives a neutral label such as "Person A" or "Object B"; an unknown exit receives "Exit A". Do not read future script branches to announce destination names, item contents, gil, battles, or other outcomes before interaction.
- Assign neutral labels in a deterministic order from stable native scene/object identifiers, not distance. Labels remain stable while the player moves, across category changes, and when a temporary target disappears and returns in the same scene.
- Persist only observed identities and necessary navigation preferences. Scope records to the save/profile and map variant; loading another save invalidates the observation store before any catalog is exposed. Without a reliable save identity, keep observations session-local instead of sharing them across playthroughs.

### World map

- Categories: Locations, Vehicles, and Regions. Towns and other entrances use native location availability and the active map's marker table. A proximity threshold alone does not expose an undiscovered town name.
- An entrance may also be included when its model or native prompt is presently visible and establishes its identity. An unknown visible entrance receives a neutral label until the game supplies its name.
- Vehicles use native world HUD marker availability and also enumerate live shown world actors with character indices 3-10, excluding the controlled actor as a parked-vehicle target. The HUD has only one plane pointer shared by boat and airships; it cannot be the sole catalog source when more than one vehicle is visible. Distinguish Blue Narciss, Hilda Garde III, and Invincible from their actor status rather than assuming that pointer always means an airship. Chocobo availability follows native rendering/marker conditions. A HUD marker permits tracking its native location; otherwise require the actor's current rendered/presented visibility, not merely its existence in a world object list.
- Refresh parked vehicle coordinates every observation. A vehicle stays informative even when the current transport cannot reach it; speak the route limitation instead of deleting a valid marker.
- Use the game's localized 64-entry `worldloc.mes` table when native state establishes a region name. The current-region announcement follows the game's own location-display conditions, including its water/area-zero distinction. The label is available in the native main-menu location header; K exposes that same current information during travel. Automatic region-change announcements are configurable, enabled by default for the requested region tracking.
- Regions become selectable when the game has exposed their names through the current location or another native display. Do not present the complete internal table as a list of undiscovered destinations. Each observed region retains up to 16 distinct visited points, sampled on entry or after meaningful displacement, with their map variant and traversal context. Selecting it chooses a reachable observed point and explicitly says "previously visited point". This avoids inventing a region center. Current-region status does not require a saved point.
- A location or vehicle can include its region when that region identity has already been exposed. If it has not, retain the target's valid name and direction and omit the unknown region association.

Targets use separate types for field object IDs, world navigation-location IDs, region IDs, and vehicle actor IDs. Arrival must never compare a region ID with a town ID.

## 4. Components and data flow

Keep the existing accessibility entry points and speech-call interfaces. Extract navigation responsibilities into small units instead of adding more unrelated responsibilities to the existing large manager files.

| Component | Responsibility and contract | Dependencies |
| --- | --- | --- |
| Context reader | Returns a fresh snapshot: scene/map generation, validated controlled actor, native control availability, HUD/menu/dialogue/battle state, foreground state, and transport. An unknown actor is reported as unavailable; never substitute the first actor. | Native field/world state and UI state |
| Target catalogs | Return eligible targets with stable IDs, evidence, current positions, and localized labels. Preserve selection by ID after rescans. | Context reader, native marker/render/interaction state, observed identity store |
| Region observations | Announce native current-region changes and retain eligible observed region points per save/map variant. | Native terrain/location display and localized text |
| Field route planner | Returns a cleared path over currently enabled native walkmesh geometry, or an explicit failure. Does not move an actor. | Walkmesh snapshot, actor radius, target approach conditions |
| World route planner | Returns a wrapped, transport-valid route to a target approach point, or an explicit failure. Does not modify world actors or shared collision state. | Native terrain/collision rules, world dimensions, transport capabilities |
| Movement controller | Owns only the accessibility route it started. Applies/revokes native field paths, movement-only world steering, or native airship autopilot. | Context, current target, route plan, input broker |
| Input broker | Resolves commands, captures navigation-menu inputs and their release edges, and detects physical manual takeover before synthetic movement. Capture applies to raw/persistent input consumers as well as normal logical-button/axis reads. | Native keyboard/controller input seams and binding configuration |
| Announcer | Formats concise status/guidance, handles priority and deduplication, and calls the compatible screen-reader facade. | Validated observations and route state |
| Launcher accessibility | Supplies named semantic controls, keyboard navigation, readable validation/status, and navigation binding settings. | Maintained Memoria WPF launcher and its existing launch/settings logic |

Each update reads context, validates or rebuilds the catalog, processes physical commands, advances the route only if allowed, then emits announcements. Scene, target-eligibility, and transport changes invalidate the relevant snapshots. Apply movement on the Unity/game thread; do not access Unity actors from a background speech or planner thread.

Selection and suspended route intent live outside per-scene MonoBehaviours. Scene teardown always revokes movement and invalidates actor/geometry references, but a positively identified random-battle suspension retains only stable IDs and route intent. Rebuilt field/world objects attach fresh observations; native battle-return markers plus the same logical map/variant distinguish a battle return from actual travel to a new map.

## 5. Player controls

### Keyboard defaults

Use familiar navigation functions, with defaults adapted to FFIX's existing mappings:

| Key | Action in a controllable field/world HUD |
| --- | --- |
| U / O | Previous / next category |
| [ / ] | Previous / next target |
| K | Current location/region, selected target, and route status |
| I | Toggle spoken guidance; when a route is paused, explicitly resume guidance |
| P | Start, stop, or explicitly resume automatic movement for the selected target |
| R | Repeat the last accessible announcement |
| Shift+I | Decrease guidance interval |
| Shift+P | Increase guidance interval |

The launcher exposes configurable bindings and a familiar J/L preset. J is FFIX's default R2 key, so that preset cannot silently consume its camera action: it must resolve the conflict by selecting a different navigation binding or by an explicit player remap of the game binding. The default bracket pair keeps the existing FFIX navigation target keys and preserves J.

Check conflicts against current game bindings, movement keys, other navigation commands, and existing accessibility/mod shortcuts, including modifiers. Preserve M for the existing gil announcement and 0 for the existing icon-reading toggle, and include Memoria's Ctrl+Shift+S and F12 combinations in conflict detection. A conflicting imported configuration leaves the game control available, disables the conflicting navigation command, and reports the conflict through accessible settings and one concise in-game diagnostic. F1-F9 retain their game/Memoria functions. Remove navigation debug/teleport shortcuts from the player navigation path. Do not use Escape or Ctrl+Enter as automatic movement commands.

Navigation commands are active only in the foreground field/world HUD. Ignore them in text entry, game menus, battle, and dialogue. A native movement direction cancels automatic movement without requiring a navigation key. P also provides an explicit stop during controllable play; focus/control transitions revoke movement independently.

I controls spoken guidance independently of movement. Starting automatic movement with P also enables guidance; pressing P during movement stops both. Manual movement cancels automatic movement but may retain already-enabled passive guidance. Choosing a different target/category stops both modes before announcing the new selection. I resumes only guidance from a paused state; it never restarts automatic movement. Guidance defaults to the installed one-second interval, adjustable in half-second steps from 0.5 to 10 seconds; settings and interval commands announce the resulting value. Cancellation and important state changes remain immediate.

### Controller menu

R3 opens a spoken navigation menu in the field/world HUD, pauses an active accessibility route, and announces the category, selected target, and controls. R3 closes it while leaving a paused route paused. Within this menu:

| Physical control | Action |
| --- | --- |
| L1 / R1 | Previous / next category |
| D-pad up / down | Previous / next target |
| D-pad left | Read location and route status |
| D-pad right | Repeat the last announcement |
| A / Cross | Close menu and start/resume spoken guidance |
| X / Square | Close menu and start/resume automatic movement |
| B / Circle | Close menu and stop the accessibility route |

Use the physical device's button identity for this menu and describe it using the selected controller naming convention. Capture menu buttons, axes, and their held/release tails at the native input source so A/Cross cannot also Confirm an event and D-pad cannot move the player. Raw physical movement outside the menu cancels automatic movement, using the native dead zone to reject stick drift; synthetic steering does not count as manual input. L2/R2 keep their ordinary world-camera behavior outside the menu.

Interception must cover Unity button/axis reads and Hono's persistent reads, field movement's direct XInput D-pad/stick state, and the world's direct XInput steering/right-stick/throttle reads. Keep a raw device snapshot for navigation commands and a filtered game-input view; do not mistake a filtered zero axis for lack of manual movement. R3 means the physical stick-click, not Hono's mobile right-analog tilt status.

Foreground-only navigation takes precedence over `AlwaysCaptureGamepad` for accessibility commands and synthetic movement. On focus loss, close the navigation menu, revoke the route, and keep owned menu inputs masked through their release/neutral tails, including direct XInput reads. Preserve the player's `AlwaysCaptureGamepad` setting: after the capture tails clear, ordinary physical game input follows that setting, while accessibility movement remains paused until an explicit foreground resume.

If R3 conflicts with a custom game binding or is absent on the device, expose a configurable controller-menu trigger with the same conflict policy. Navigation must remain usable through the keyboard while that device conflict is being resolved.

## 6. Field guidance and automatic walking

Guidance announces the selected target and its relative direction from the controlled player/camera, distinguishing direct bearing from a planned route. Directions use the same keyboard versus analog orientation conventions as the native movement code. Manual walking can follow guidance without enabling automatic movement.

Build field routes from a snapshot of enabled walkmesh triangles and adjacency, respecting native BGI enabled flags, floor, height, attribute-mask, and transition restrictions. Reconcile BGI and runtime triangle flags; do not assume a constructor's default active bit proves the triangle is currently traversable. Use a search that relaxes costs correctly, followed by corridor/funnel smoothing and actor-radius clearance checks. The existing `FindPathReversed` search is not assumed to be optimal or sufficient merely because it returns a list. Avoid mutating its shared path-search links while the game is using them.

Automatic walking feeds a validated route into the existing field click-to-move follower using its waypoint ordering and route bookkeeping contract. It runs or walks according to that native follower and the game's running restrictions; event scripts observe its movement as native click-to-move, including the native movement-held signal. It does not write the actor's position. If route planning fails, speak "No walkable route" and retain direct directional information; never turn that failure into blind straight-line automatic movement.

For people and objects, select a reachable approach point using the native interaction distance and facing rules. Announce "Ready to interact" only when the native talk-range/collision selection, NPC/quad talkability, and facing checks actually pass for this target; no fixed generic radius or nearby competing actor suffices. Use a side-effect-free equivalent of those conditions, not the input-consuming interaction routine. Face only as ordinary movement allows; otherwise announce the required turn. Do not depend on an icon that the player's HideSteam setting can suppress. The player presses Confirm. For exits, follow a valid route to the native entrance/contact boundary. An ordinary automatic transition caused by walking can occur; no extra Confirm is synthesized.

Revalidate enabled geometry and target position as they change. A moving actor can trigger a bounded replan. Detect lack of progress using native displacement and route progress, not just elapsed time. On persistent obstruction, release the route and explain the blockage. Do not loop indefinitely or increase movement force to overcome native collision.

## 7. World guidance, routes, and airships

Use `ff9.PosDiff` or an equivalent verified calculation for the native 1536-by-1280 wrapped world. Bearings, distance ordering, route edges, guidance, and arrival checks must all use the same wrapped coordinate convention. Test the sign and compass orientation against actual native headings.

Speak one of eight compass directions plus useful distance/proximity. Numeric distances are explicitly called "world units" using the native coordinate scale; do not invent a meter conversion. Proximity thresholds must fit the world's actual dimensions and native approach radii. Update distance when the player moves or a vehicle moves; do not read stale cached target distances.

On foot, chocobo, or Blue Narciss, automatic movement follows a route that obeys that controlled actor's native terrain and crossing rules. Snapshot collision/terrain data and evaluate the same restrictions without moving the actor or altering shared collision cache. Route edges must stay passable for the actor, including terrain transitions and reachable entrance/vehicle approaches. Ground/world steering enters only the native movement pad readers such as `w_moveGetPadStateLX`, `w_moveGetPadStateLY`, and `w_moveGetPadStateR`, with a separately validated vehicle-throttle seam; no event-input Confirm bit is set, even where the engine reuses Confirm as a vehicle throttle signal internally. Automatic world steering cannot be enabled until that separation is verified.

Use native airship autopilot for an eligible town marker while controlling Hilda Garde III or Invincible, matching native actor status 9 or 10. A boat is not an airship. Starting autopilot uses the native location identity; stopping calls `w_movementAutoPilotOFF()` and releases any accessibility steering. Clearing the mod's flag alone is not cancellation.

For eligible non-town targets, such as an observed region point, use movement-only steering under native airborne rules rather than repurposing a town ID. Automatic travel stops at the destination approach; it never descends, lands, boards, disembarks, or changes transport automatically. Announce the remaining player action using current native availability. Do not choose a landing site using hidden event or encounter data.

Town arrival requires wrapped approach distance and valid native entrance/context evidence, rather than equality with the region number. Parked vehicle arrival requires its refreshed position and native boarding range. Region-point arrival is arrival at the announced observed point. Any remaining uncertainty about a specific entrance's native identity must disable its automatic entrance claim while preserving its valid marker and directional guidance.

## 8. Route lifecycle and cancellation

Represent selection, spoken guidance, and movement ownership separately. A selected target survives a stopped route while it remains eligible. Route state is Idle, Guiding, Moving, Paused, Arrived, or Unavailable, with a precise reason for Paused/Unavailable.

| Event | Required behavior |
| --- | --- |
| Physical movement, explicit stop, or selecting a different target/category | Release owned movement immediately. Keep valid selection; announce the stop once. |
| Player presses ordinary Confirm outside the navigation menu | Pause owned automatic movement before passing the real interaction input to the game. Preserve the player's Confirm action; never synthesize an additional press. |
| Game menu or dialogue opens | Release movement and pause guidance. After it closes, remain paused until I/P or the controller menu explicitly resumes. |
| Ordinary random encounter begins | Release movement and suspend guidance. After a positively identified native random encounter, resume only the previously active mode if the same scene/map, transport, eligible target, and native player control are freshly validated. Replan before moving. Scripted or uncertain battles remain paused until explicit resume. Otherwise stay paused and explain why. |
| Focus loss, controller disconnect, or input/context uncertainty | Release movement immediately. Stay paused after recovery until an explicit resume. |
| Actual field/map travel, excluding a positively identified random-battle rebuild | Revoke the previous scene's route. A transition through the selected exit completes that route when verified; otherwise announce the new native location and leave navigation stopped. Do not carry a field-object pointer across scenes. |
| World map variant, controlled actor, or transport changes | Invalidate route/approach and stop. Refresh targets and require an explicit restart. |
| Selected target becomes unavailable | Release movement and announce its loss. Do not silently switch to a different actor or destination. |
| Path becomes invalid or movement persistently stalls | Stop, retain valid directional information, and announce route failure. |
| The native field follower clears the owned path | Detect the clear, distinguish arrival from talk/control-loss/stall where native state permits, and announce the outcome. Never silently consider an interrupted path complete. |
| Arrival | Release movement; announce approach/arrival accurately and preserve manual interaction. |

Release means revoke only the path or steering owned by accessibility and stop its native autopilot where applicable. Never clear a script-controlled actor's unrelated movement. Input capture remains active through held/release tails even when a navigation-menu command closes the menu.

Every output and movement decision uses the current context generation. Random-battle return rebinds stable IDs into a new generation; it never reuses cached scene references or waypoints. A valid native position can still support a location/status announcement when routing is unavailable. Treat missing information as an accessibility defect: provide explicit availability/error speech instead of an unexplained empty category or silent command.

## 9. Speech and diagnostics

Preserve the `ScreenReaderManager` public methods and existing callers so dialogue, menus, minigames, and other speech remain functional. Implement Prism through a .NET 3.5-compatible UTF-8/cdecl native wrapper, explicit ownership of its context/backend, and correct architecture loading. Deploy its license and dependency notices.

The speech facade has process lifetime and idempotent initialization. Repeated Initialize calls from title, field, or world code do not create duplicate backends. Scene/navigation teardown does not free the backend; final native cleanup occurs at application quit. Backend replacement/recovery stops and frees the old backend through its verified ownership contract.

Prism is the preferred backend. If Prism cannot initialize, retain the installed Tolk path as a reported compatibility fallback where it is functional; log the selected backend and failure reason. Never speak through both backends at once. If neither backend is available, the launcher exposes the problem accessibly before play and the game writes a clear diagnostic; initialization can be retried after a screen reader starts. Do not catch an exception and silently disable all announcements.

Explicit navigation requests may interrupt earlier navigation guidance. Automatic guidance does not repeatedly interrupt dialogue, minigames, or high-priority menu announcements. Deduplicate unchanged directions and region labels; announce meaningful turns, target changes, pauses, arrivals, and failures. Honor a configurable announcement interval without delaying cancellation.

R repeats the actual last accessible announcement. K supplies current native location/region, selected target, transport where relevant, and route state. Optional diagnostic logging records context generation, eligibility reason, route failure, input conflict, and backend changes. Player speech uses game terms; internal IDs and route traces remain in logs.

## 10. Accessible launcher

Extend the maintained Memoria WPF launcher. Replace or repair image-only and custom-template controls with semantic controls and explicit accessible names. In particular, a custom button's visual Label is not assumed to supply its UI Automation name.

Expose names, roles, values, states, label associations, selection, and validation messages for Play, architecture selection, display options, mod settings/lists, accessibility controls, and dialogs. Provide a predictable tab order, visible focus, keyboard activation, and access keys where helpful. Controls that cannot currently run are announced as disabled with a readable reason.

Preserve the existing game's working directory, Steam launch requirements, architecture selection, and settings behavior. Opening an accessibility setting must not launch the game. Play and its resulting error/status must be accessible using keyboard and a screen reader. Use ordinary UI Automation for controls and Prism for status only where necessary, avoiding duplicate speech.

Add accessible settings for navigation bindings, announcement interval, controller naming/menu trigger, and Prism/backend diagnostics. Show conflicts before saving and keep the previous valid binding if a replacement is invalid. Do not change existing graphics/mod settings merely to enable accessibility.

## 11. Build, package, deployment, and rollback

Before any product build, bypass or redirect the source project's automatic deployment targets. `Memoria.MSBuild/Deploy.cs` otherwise finds the real Steam installation and copies output into both architectures. Build into an explicit staging directory and verify it before installing. Do not run the interactive/elevating setup script without separating its required reference acquisition and framework prerequisites from installation side effects.

Maintain the game's .NET 3.5 compatibility and the launcher's .NET 4.8 compatibility. Resolve build references from the verified installed/research baseline. Validate native speech dependencies for x86 and x64 separately. Preserve the MIT license, upstream attribution, and Prism notices.

Create a timestamped backup and manifest of every installed file that will be replaced, with original and staged hashes. Deploy an explicit file list covering managed game assemblies, Memoria support libraries, the correct native speech libraries for each architecture, and launcher/dependencies. Preserve/package Tolk and its matching controller-client dependencies for the documented fallback; an x64 fallback DLL is not an x86 dependency. Preserve native Unity executables, game assets, saves, and unrelated settings. Merge new accessibility defaults without overwriting existing Memoria choices.

After copying, verify installed hashes, PE architecture, dependency resolution, launcher path, and core accessibility types. Retain a rollback procedure that restores exactly the changed files and merged configuration state. Keep staging and backups outside game asset/save directories. External publishing, GitHub releases, and remote pushes are separate from the authorized local development/deployment task.

## 12. Verification and acceptance

Use meaningful tests around routing and native-state decisions rather than tests that duplicate implementation. Run the appropriate builds/checks after changes and repeat only when new evidence requires it.

| Area | Required evidence |
| --- | --- |
| Eligible information | Hidden actor/quad and undiscovered-location fixtures stay excluded; visible unnamed targets retain stable labels; future script item/battle/destination outcomes are not spoken. |
| Field routing | Obstacle detour, narrow corridor/radius clearance, disconnected and disabled triangles, moving target, waypoint order, native interaction range, and no direct-walk fallback on failure. |
| World routing | Wrapped bearings/order at both seams, transport-specific terrain and crossing restrictions, separate town/region IDs, live vehicle updates, water region conditions, and airship-versus-boat eligibility. |
| Lifecycle | Manual takeover, explicit stop, dialogue/menu pause, random-battle fresh resume, focus loss, disconnect, scene transition, target loss, stuck movement, and actual native autopilot cancellation. |
| Input | Current keyboard conflicts, R3 menu capture, held/release tails, camera controls outside the menu, analog orientation, and absence of leaked Confirm or synthetic manual-takeover signals. |
| Speech | Correct UTF-8, native ABI/architecture, Prism initialization/output/stop/recovery, fallback diagnostics, preservation of existing speech, repeat, and guidance priority/deduplication. |
| Launcher | UI Automation names/roles/states, label associations, keyboard focus/activation, accessible validation/errors, settings persistence, and keyboard-only launch. |
| Deployment | Staging cannot deploy implicitly; explicit package manifest, both architectures' dependencies, installed hashes, preserved settings/saves/assets, and a verified rollback file set. |

Live validation must then demonstrate field selection and walking, region changes, towns and relocated vehicles/parked airship, world travel, all relevant cancellation paths, controller-menu isolation, actual Prism speech, and launcher keyboard/UI Automation behavior. Use ordinary available saves and game state; do not force story flags or invent runtime evidence to claim coverage.

Test both x86 and x64 processes. Static checks, test fixtures, build success, and local deployment are reported separately from live gameplay results. An untested live case remains explicitly unverified. A confirmed absence of important speech or unsafe movement blocks a completion claim until repaired.

## 13. Decisions that runtime work must resolve

The design is specific about player behavior; these are bounded technical validation obligations, not permission to omit requested features:

- Establish field background-exit visibility evidence and per-entrance approach identity without exposing future script outcomes.
- Validate a side-effect-free view of native world terrain/collision data before enabling automatic ground/world steering.
- Identify the movement-only throttle/steering injection point without setting event-input Confirm, including non-town airship targets.
- Confirm arrival/boarding approach tolerances and physical controller mapping against real native state.
- Confirm native random-battle return markers and fresh scene/target rebinding before permitting automatic resume.
- Confirm Prism's shipped ABI and dependencies in both architectures and the launcher's screen-reader output.

If a requested behavior cannot be achieved using verified native state and input, report the concrete limitation before substituting a different behavior. Preserve the independent valid information and controls while investigating; no silent omission, hidden-state shortcut, or unsafe route is an acceptable substitute.
