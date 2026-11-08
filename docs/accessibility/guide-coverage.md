# Whole-game guide comparison

This is an offline audit, not a complete accessibility or playthrough certification.

The current English event bundle has **818 fields and 16,067 entries**. All were decoded and matched to retained native fingerprints. The comparison accounts for **44 main-guide chapters, 363 Story checkpoints and 23 optional topics** (87 native entry bindings). Every field and guide topic has an explicit result in `guide-coverage.json`.

Story checkpoints are scenario-counter metadata. Native Story step bindings are generated separately (`tools/Export-FF9StorySteps.py`, `story-steps.json`, `story-step-coverage.json`): **192 current-state step bindings** (60 position, 34 region-confirm, 60 region-walk, 38 talk) and 4132 typed transitions (1953 player, 1 position, 2178 scripted). 3138 analysis records describe unproven waits, setters or instances. They do not count missing required steps.

Reviewed packs supply **178 explicit actions, 16 native loop actions and 424 state rules**. Several rules can cover the same action or different entrances to one room. The native approach and current interaction still have to pass at runtime.

The main reference is [bover_87's guide, version 2.01](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891); [Andrew_Testa's guide](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/29957) is the user's additional reference. Short checklists here are original summaries. Guide information is never loaded by the runtime or used to disclose hidden NPC names, rewards or coordinates.

## Corrections from this audit

- Opening Story uses captured street transitions and names the next local area. The square is the physical approach to the ticket objective; the booth dialogue runs in a separate room. Its reviewed visible sign/hotspot is listed before bubble discovery. Informational steps explain the next area without starting a route to a remote objective.
- Pinnacle Rocks now keeps fragment collection ahead of the final conversation. Only currently presented, interactive fragments can supply walking targets.
- Fossil Roo uses the actual switch bits and current connected floor section. Reviewed flowers and boarding spots are listed separately and use the corresponding native interaction.
- Desert Palace follows the required lamps in their native order. After backtracking, the current floor and lamp state can select a reachable recovery action instead of a closed staircase.
- Memoria and the Crystal World expose three rendered save spheres with verified approaches. Four hidden save points receive a name only after the game shows their prompt.
- Mount Gulug keeps the lever step until its native completion flag changes. The player still chooses the lever action.
- Card speech uses the displayed face grades, includes placed-card defenses and reads initial hand/board focus. Triple Triad follows its displayed layout and hidden-arrow setting.
- Auction bids/current price/funds and Daguerreo quantities read changes from completed, visible native text parsers. Original instructions and clocks remain intact. Only the verified English numeric layouts are supported.
- Exact source UIDs are retained for field touch bubbles. Two additional native inn actor/region bindings and a narrowly verified touch-only confirmation state are recognized.
- The reviewed appearance catalog includes 326 models. Exact native interaction and actor identities have separate bindings.

## Remaining limits

All rows retain `fullyAccessibleProven: false` and/or `liveValidated: false`. Script presence, guide order and passing fixtures do not establish that every NPC, prop, exit, puzzle or optional activity works in all live states. There are 345 constant actor models; many still use generic labels before an attributed native name appears.

695 region entries have a Talk/Push handler and still require the current native icon rather than a verified backdrop/actor binding. This is a static candidate count, not a count of visible missing props. Generic icon-only observations also lack a durable offscreen art-visibility proof.

Chocograph picture descriptions, world cracks/bubbles/tracks/shadows, jump-rope cadence, race progress, shuffle tracking, guard-facing/timed tasks, Blackjack and several puzzle sequences still need dedicated presentation evidence or live validation. Desert Palace and Pandemonium have reviewed required routes; alternate-state recovery and live movement still need verification. The optional checklist lists each limitation.

This audit did not launch the game or synthesize gameplay/controller input. Full transitions, audible NVDA/controller delivery and complete main/optional playthroughs remain player tests.

## Reproduce the checks

Run `python tools/guide_audit.py --check` and `python tools/compile_story_prerequisites.py --check`. These validate the committed census accounting, all guide topics/checkpoints and current source fingerprints. They do not re-extract the installed game. A fresh extraction/research input refresh is required when the event-bundle identity changes.

## Main-story chapters

| Chapter | Comparison result |
| --- | --- |
| [1. Alexandria](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/alexandria) | Opening travel uses actual native street exits toward the square. The rendered ticket sign binds its native front hotspot; room104 is the dialogue/completion scene. Unknown routes identify the next area without creating a walking target. Full scene transitions, puzzle inputs, controller and speech still need live validation. |
| [2. Evil Forest](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/evil-forest) | Numerous writes belong to player/director scene functions rather than a talk target. Verify Baku and party-recruitment listener phases, then escape movement; static writes do not prove a selectable actor. |
| [3. Ice Cavern](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/ice-cavern) | Ice-wall treasure detours must remain optional. Verify current ice interactions, mist hazards and changing party/player identity separately from the seven scene floors. |
| [4. Village of Dali](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/village-of-dali) | Windmill/lift subactions and inn reunion require native state eligibility. The optional machine-room door, treasures and resting option are not mandatory story objectives. |
| [5. Cargo Ship](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/cargo-ship) | The eight cargo-area floors include scene-driven transitions. Verify the playable bridge/exterior approach and battle-to-scene handoff without automating input. |
| [6. Lindblum](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/lindblum) | The telescope, guard disguise and timed Hunt are local/native minigame states. Scene-floor presence alone does not prove usable telescope or Hunt guidance; preserve time and player control. |
| [7. Exploring Lindblum Kingdom](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/exploring-lindblum-kingdom) | Do not force optional frog catching, blue magic or Chocographs into Story. Ordinary categories and optional-topic audit handle them. |
| [8. Journey to Burmecia](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/journey-to-burmecia) | Bell possession/consumption, moogle nut interaction and South Gate NPC movement share broad counter phases. Verify each required current actor/object; the four Grotto and two Gate scene floors do not enumerate all bell/worker steps. |
| [9. Burmecia](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/burmecia) | The family rescue is optional. Confirm the floor/balcony/bell state and required approach; native bell pickup at757/sid3 has a direct3840 milestone, unlike many automatic scene floors. |
| [10. Journey to Treno](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/journey-to-treno) | Station boarding and reunion interactions need exact current listener proof. Optional Dali or cave detours should not replace Treno as the story destination. |
| [11. Treno](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/treno) | Gargan Roo has native tag3 milestones4510,4520 and4530 for its controls. Verify lever order/current state and keep auction, Stellazzio, shops and optional Dali detours outside mandatory Story. |
| [12. Journey to Cleyra](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/journey-to-cleyra) | This is world travel leading to the next Cleyra checkpoint, not a separate native interaction floor. |
| [13. Cleyra](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/cleyra) | The initial trunk lever, sand routing and civilian dialogue branches need local guard review; correct civilian directions affect optional rescues/rewards and must not be silently auto-selected. |
| [14. Return to Alexandria](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/return-to-alexandria) | Timed escape and cage input need specific native phase proof. The purple candelabra and moving platform are required local actions, while Tantarian is an optional encounter. |
| [15. Pinnacle Rocks](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/pinnacle-rocks) | Current fragment bits precede the final conversation. Only an already presented, interactive fragment can supply a walking target. Unshown fragments have no inferred position. Full scene transitions, puzzle inputs, controller and speech still need live validation. |
| [16. Return to Lindblum](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/return-to-lindblum) | Shopping and town errands are optional. Verify the departure listener and one-way story transition at the fountain before routing toward the gate. |
| [17. Journey to the Outer Continent](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/journey-to-the-outer-continent) | Current switch bits and the verified floor component choose the route step. Reviewed flowers and boarding spots are separate targets tied to their native prompts; a route with no current approach remains informational. Full scene transitions, puzzle inputs, controller and speech still need live validation. |
| [18. Outer Continent](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/outer-continent) | Do not route to the blocked mountain exit before the village lead. Optional shops, ATE collection and marsh visits remain separate. |
| [19. Magdalene Forest](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/magdalene-forest) | The guide heading is unusual; native area is Black Mage Village/Dead Forest, not a runtime location rename. Owl puzzle state and mandatory village conversations need native guard checks; optional ATE ordering is not a mandatory story checklist. |
| [20. Codie Petie Mountain Path](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/codie-petie-mountain-path) | Preserve the native Conde Petie spelling. The second wedding and colored stones are optional. Verify the ceremony gate, ladder/vine movement and party change independently of scene floors. |
| [21. Madain Sari](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/madain-sari) | Cooking choices affect a scene, not main progression success. All wall/dinner/pot substeps need current native eligibility; eleven scene floors do not alone prove each moogle/pot target. |
| [22. Journey to the Iifa Tree](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/journey-to-the-iifa-tree) | Friendly Nymph is optional. The travel interlude has no separate required native counter floor. |
| [23. Iifa Tree](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/iifa-tree) | Treasure-dropping buttons are optional. Verify the final inspect-then-Dagger order and elevator approach; avoid exposing unseen creature or reward data. |
| [24. Return to Madain Sari](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/return-to-madain-sari) | The post-duel field icon and party reformation are required substeps. Static actor presence does not prove which person is currently shown or talkable. |
| [25. Battle of the Iifa Tree](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/battle-of-the-iifa-tree) | The repeated Mistodon encounters and changing party control need scene-transition proof. Do not convert optional encounter repetition into a story requirement. |
| [26. The Home Front](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/the-home-front) | Hippaul, Stiltzkin and shopping have narrow optional windows. Verify player changes before selecting any actor or boat target. |
| [27. Card Tournament](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/card-tournament) | The final result affects an optional prize, while losing earlier rounds permits retries. Dali keys/coffee and ATEs must remain optional. Card-game/menu accessibility is separate from walking stage coverage. |
| [28. Trouble on the Home Front](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/trouble160on-the-home-front) | Tantarian is optional. Native battle/menu assignments and changing controlled characters require separate proof. The source slug contains160 because of encoded whitespace; the chapter is not absent. |
| [29. The Aftermath](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/the-aftermath) | Potion acquisition is local persistent quest state. Verify each missing potion target and the all-three return guard; numerous director floors and a late scene floor do not prove intermediate target selection. |
| [30. On the High Seas](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/on-the-high-seas) | This guide chapter is an optional exploration round, not a requirement to complete every sidequest or acquire a Chocobo upgrade. |
| [31. Picking Up the Trail](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/picking-up-the-trail) | Wrong sandpits are encounters, not the palace entrance. Verify current world marker/discovery and party selection; do not reveal hidden pit identity from static coordinates. |
| [32. Forgotten Continent](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/forgotten-continent) | Oeilvert has several direct/indirect native milestones across projections. Their runtime request graph/current switch state must be verified; optional continental treasures and Chocographs are not required. |
| [33. Desert Palace](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/desert-palace) | The required candle route has explicit native actions and named room transitions. In the Shadow Chamber, the current floor distinguishes a recoverable lamp change after backtracking from an unsupported position. The Cid cue reads the native timing state; audible delivery and playability have not been tested. Full scene transitions, puzzle inputs, controller and speech still need live validation. |
| [34. Lost Continent](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/lost-continent) | The presented lever remains the objective until the native completion flag changes. Pull direction and Confirm stay under player control. Full scene transitions, puzzle inputs, controller and speech still need live validation. |
| [35. Aerial Exploration](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/aerial-exploration) | Do not force optional towns, equipment, Chocobo upgrades or bosses into Story. Current vehicle landability and ordinary marker discovery remain native gates. |
| [36. Ipsen's Castle](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/ipsens-castle) | The fresco and pot puzzles open optional treasure paths. Rescue and return have specific10575/10580/10590/10595 floors; verify their current actor/ladder approaches without routing directly out too early. |
| [37. Breaking the Seal](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/breaking-the-seal) | Existing world destination overrides match the four dropoffs. Verify each current party/vehicle branch and native pilot choice; a Sky Chocobo does not substitute for the airship story trigger. |
| [38. Preparing to Leave](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/preparing-to-leave) | The preparation chapter has no mandatory shopping/grinding checklist. Shimmering Island is a deliberate story departure with later availability changes; those guide facts must not become hidden runtime predictions. |
| [39. Portal to Another World](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/portal-to-another-world) | Generator timing, lift setting and maze teleports contain native state beyond the scenario floors. Field2709/sid3 has a player-helper request but empty Story hints. Exact remaining-action mapping is unresolved; reject guesses and preserve the30-second challenge. |
| [40. Return to Gaia](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/return-to-gaia) | Endgame quest windows and changed shops are optional topics. Native arrival floors do not establish that all optional content is accessible or already complete. |
| [41. Second Battle of the Iifa Tree](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/second-battle-of-the-iifa-tree) | The exact Invincible portal trigger differs from reaching Iifa on foot or by Chocobo. Verify presented marker/vehicle action and the battle-to-field transition. |
| [42. Memoria](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/memoria) | The main route is largely linear; optional phantom card players, hidden treasures and Hades must remain optional/currently revealed. Nero checkpoints are separate optional persistent state, not all main-story milestones. |
| [43. Final Preparations](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/final-preparations) | This chapter supplies optional preparation opportunities. A one-way return warp must be narrated from its presented native choice; do not automate it or require grinding. |
| [44. The Final Battle](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/the-final-battle) | After12000 there is no further walking objective in the catalog. Verify final approach, menu handoff, ending and recovery states; static routing does not prove battle/menu narration. |

## Native Story step bindings by chapter

Counts are stage milestones in the chapter whose counter write has a native player-action step, a native player-position wait, or only arrival/automatic scene writers. Arrival-only does not mean broken: those scenes play on entering the field, and the route must still reach that field through walkable transitions.

| Chapter | Stages | Player action | Position wait | Arrival/automatic only |
| --- | --- | --- | --- | --- |
| Alexandria | 14 | 4 | 1 | 10 |
| Evil Forest | 23 | 6 | 0 | 17 |
| Ice Cavern | 7 | 0 | 0 | 7 |
| Dali | 14 | 4 | 0 | 10 |
| the cargo ship | 8 | 0 | 0 | 8 |
| Lindblum | 18 | 3 | 0 | 15 |
| Gizamaluke's Grotto | 4 | 0 | 1 | 3 |
| South Gate | 2 | 0 | 0 | 2 |
| Burmecia | 9 | 3 | 1 | 5 |
| South Gate | 10 | 6 | 0 | 4 |
| Treno | 11 | 4 | 0 | 7 |
| Gargan Roo | 5 | 3 | 0 | 2 |
| Alexandria | 1 | 0 | 0 | 1 |
| Cleyra's Trunk | 2 | 0 | 0 | 2 |
| Cleyra | 11 | 2 | 2 | 7 |
| the Red Rose | 3 | 0 | 0 | 3 |
| Alexandria | 11 | 2 | 4 | 5 |
| Gargan Roo | 1 | 0 | 0 | 1 |
| Pinnacle Rocks | 5 | 1 | 0 | 4 |
| Lindblum | 5 | 1 | 0 | 4 |
| Fossil Roo | 2 | 1 | 0 | 1 |
| Conde Petie | 3 | 1 | 0 | 2 |
| Black Mage Village | 5 | 3 | 0 | 2 |
| Conde Petie | 4 | 1 | 0 | 3 |
| Conde Petie Mountain Path | 3 | 0 | 0 | 3 |
| Madain Sari | 11 | 5 | 0 | 6 |
| Iifa Tree | 7 | 1 | 0 | 6 |
| Madain Sari | 9 | 0 | 0 | 9 |
| Iifa Tree | 10 | 0 | 0 | 10 |
| Alexandria | 9 | 0 | 4 | 5 |
| Treno | 5 | 4 | 0 | 1 |
| Alexandria | 7 | 0 | 0 | 7 |
| Lindblum | 12 | 2 | 0 | 10 |
| Blue Narciss | 1 | 0 | 0 | 1 |
| Black Mage Village | 4 | 0 | 0 | 4 |
| Desert Palace | 7 | 2 | 0 | 5 |
| Oeilvert | 14 | 11 | 0 | 3 |
| Desert Palace | 14 | 1 | 0 | 13 |
| Blue Narciss | 0 | 0 | 0 | 0 |
| Esto Gaza and Mount Gulug | 2 | 0 | 0 | 2 |
| Lindblum | 5 | 0 | 0 | 5 |
| Alexandria and Hilda Garde III | 1 | 1 | 0 | 0 |
| Ipsen's Castle | 12 | 6 | 0 | 6 |
| the shrines | 6 | 0 | 0 | 6 |
| Terra | 6 | 4 | 0 | 2 |
| Bran Bal | 8 | 1 | 0 | 7 |
| Pandemonium | 9 | 3 | 0 | 6 |
| the Invincible and Black Mage Village | 2 | 0 | 0 | 2 |
| Memoria | 21 | 7 | 0 | 14 |

## Optional topics

| Topic | Remaining work |
| --- | --- |
| [Tetra Master](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/tetra-master) | Validate attack-choice, result, draw/rematch and postgame selection in a real match. |
| [Jump Rope Mini-Game](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/jump-rope-mini-game) | No dedicated presentation-backed jump-window/cadence reader identified. Generic dialogue can announce instructions/results but does not establish timely access to the transient field icon or current count. |
| [Mognet](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/mognet) | No independently validated optional mail-route guide or full chain traversal. Some moogles retain generic appearance labels before a displayed identity; party/letter/capacity/story guards must stay native. World crack discovery and travel ability requirements need visible-clue access rather than hidden destination coordinates. |
| [Friendly Creatures](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/friendly160creatures) | Exact random-encounter battle-script bindings were not decoded in this field-only audit. Validate battle requests, item targeting and result speech; do not announce undiscovered encounters, completion bits, or future boss effects. |
| [Ragtime Mouse](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/ragtime-mouse) | No dedicated visible-symbol/time reader or exact random battle binding reviewed here. A question reader must preserve the native timer and never supply the correct answer from guide facts or quest bits. |
| [Frog Catching](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/frog-catching) | Native catchability guard exists, but current position, appearance and catch/release transitions still need live validation. Do not reveal hidden respawn clocks, pond slots or future prizes. |
| [Chocobo Hot & Cold](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/chocobo-hot-and-cold) | Dedicated round reader does not make the entire six-part quest accessible. G01 acquired Chocograph picture has no reviewed visual description; ability/result sprite semantics need a reader. G02 current world target collectors do not expose a presentation-backed crack/bubble/track/shadow inspection adapter. Validate owned clue menus, Paradise, the beach registration sound and travel transitions. |
| [Morrid's Coffees](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/morrids-coffees) | No visual sleep-state reader or reviewed semantic labels for these background examination points identified. Static pickup presence does not prove key availability or the second chest action remains reachable. |
| [Kupo Nuts](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/kupo-nuts) | Generic dialogue exists; no full validation of repeated nut deliveries and per-story inventory transitions. Do not announce a concealed random reward or capacity-based future nut before the game presents it. |
| [Stiltzkin Shopping](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/stiltzkin-shopping) | Baseline model 212 lacks a reviewed appearance fallback; the displayed name may be learned after native dialogue. Full eight-stop timing, availability and final reward transitions need live proof. |
| [Treno Auction House](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/treno-auction-house) | Auction pacing, competing-bid announcements and cancellation require live tests; do not delay the auction clock. |
| [Stellazzio](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/stellazzio) | Not every coin region has a reviewed semantic object label. Visible coin prose, fountain repetition, servant reward collection and final-coin state need live coverage. Do not label an undiscovered hidden coin with its reward identity. |
| [Weapon Shop Monsters](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/weapon-shop-monsters) | Only a static challenge/menu binding is proven here; four changing opponents and rewards need live transition tests. Do not reveal which later monster or reward will replace the current one. |
| [Conde Petie Colored Stones](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/conde-petie-colored-stones) | No reviewed per-setting/device semantic appearance label identified; these actions are in player-script handlers rather than named independent NPCs. Validate each icon/region and insertion menu without naming the unrevealed reward. |
| [Oglop Trading](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/oglop-trading) | The exchange actor is bound, but the exact catching entity/visual event was not traced in this audit. Baseline model 420 lacks a reviewed semantic appearance label; catch, turn-in and repeatability need live validation. |
| [Hippaul Racing](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/hippaul-racing) | No dedicated presentation-backed race progress reader found. Dialogue can expose start/results and shown level, but in-race relative position and finish timing remain unproven. No turbo, automatic presses or clock changes are appropriate. |
| [Double or Nothing](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/double-or-nothing) | Native wager and answer text can speak, but no presentation-backed shuffle-tracking narration was identified. Static actor UID or final answer-variable disclosure would bypass the visual task and is not an acceptable replacement. |
| [Daguerreo](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/daguerreo) | Lever position, visible hole/rod, water/lift level, acquired access and shop availability need semantic visual adapters and live validation. Do not supply a puzzle solution from hidden mechanism state. |
| [Magical Fingertip](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/magical-fingertip) | Old Man model 647 lacks a baseline appearance fallback. Auction numeric-entry gap applies; item appearance prerequisites and completion remain static/unvalidated. Expose the old man identity and requested item only as native dialogue reveals them. |
| [Nero Brothers](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/nero-brothers) | No optional checkpoint/visit reader identified. A static message list does not prove checkpoint credit or completion; do not announce unvisited future family names or a hidden checkpoint total. |
| [The Master Synthesist](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/the-master-synthesist) | The static player-script dialogue binding is not a visible target or an interaction-region proof. The deliberately absent icon makes presentation-backed spatial exploration important; no Hades-labeled pre-disclosure target is justified. Find/challenge/battle/shop/revisit chain needs live proof. |
| [The Ultimate Monster](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/the-ultimate-monster) | No exact presented rock-to-interaction region or live target proof established here. Battle access is not a claim of accessible Ozma combat; hidden AI, weaknesses and future rewards must remain silent unless native UI discloses them. |
| [Storyline Mini-Games](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/storyline-mini-games) | Generic dialogue, choice speech and ordinary field/battle targeting provide partial coverage only. No dedicated readers found for rope-like timing prompts, hunt clock/score, palace guard facing or Blackjack hands/hand totals. Blackjack buttons may inherit generic UILabel speech, but no card/hand/wager outcome reader is present in EndGame modules. Cage swinging is an extra story interaction outside this seven-subtopic guide page and needs its own validated adapter audit. |
