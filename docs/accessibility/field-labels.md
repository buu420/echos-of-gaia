# Field target labels

Echos of Gaia uses the original native message subject to associate a
displayed speaker heading with an NPC. Bubble tails and fixed-position text no
longer determine whether that association survives. Names come from the page
actually displayed, not future dialogue or a lookup of unrevealed characters.
Unattributed region text stays unassigned. Reused actor IDs require the same
model, script entry and original script fingerprint before a remembered name
can be reused. Older unscoped names must be observed again.

Before an attributed name appears, 43 reviewed person/animal models and four
object models have appearance descriptions in 0.1.6. These include six added
chest/frog models and a long-coat model whose body must actually be rendered;
a hidden body receives no coat description. The exact entries and visual
features are in `reviewed-field-labels.json`; they were checked against rendered
installed meshes. For example, an unnamed child can be “Child with pigtails A”.
Unreviewed models retain a generic label. Stable letters distinguish matching
appearances; this update does not claim every NPC or object is named.

The November 4 development update also examines speaker headings in each
person's own Talk handler. Supported role headings can label a visible person
before approaching, while a proper name must already be known from displayed
dialogue in this save or the current party. Dialogue requested from another
actor cannot supply this label; ambiguous headings remain unavailable.
Visible character models can remain in People even when they have no current
conversation. Their presence in the list does not promise an interaction.
Object/prop names beyond the reviewed appearances and duplicate-role
landmarks remain incomplete.

Two additional inns now use their exact native actor/region association for
interaction approaches. Field touch cues retain the actual source UID, and
one reviewed touch-only confirmation listener has a bounded read-only readiness
adapter. These changes do not execute an interaction or press Confirm.
The [whole-game guide comparison](guide-coverage.md) records all fields and the
remaining generic-label and icon-only backdrop-region gaps.

0.1.7 also recognizes the ticket booth's visible sign in the opening Alexandria
square. Its current rendered overlay, original art/floor, native interaction
polygon and player/director scripts must match. It does not require prior
bubble discovery. This is a reviewed backdrop binding, separate from the 47
model descriptions. Native Confirm and booth choices remain player actions.

Presented exits use their current projected screen position, such as
“Upper-left exit A”. Visible actors and presented exits are listed immediately;
they do not require an interaction, visit or saved observation to appear.
An exit may acquire “Last led to Alexandria Item Shop”
after the player actually uses it. Learning requires a previously presented
exit, a successful native player-trigger request, a directly matching executing
owner and active push invocation, the executed field jump, a normal scene departure and attachment, and a
matching arrival with control and native location text. The association is
scoped to that exit's map, ID, script entry, original script and polygon.
There is no reverse-exit inference, guessed destination or hidden script
destination lookup. Indirect or unmatched jumps remain unknown. “Last led to”
describes the observed trip, not a guarantee about a conditional exit.

Names and exit observations remain in the normal save-scoped observations
companion files. Loading/replacing a save clears pending attribution and exit
journeys. Observations are persisted alongside ordinary saves; this feature
does not modify the game save or automate Confirm.

The supplied recording also contained older debug window titles, long
controller tutorials and keyboard pause instructions that were removed in
0.1.4. The 0.1.5 package includes those corrections. Internal entrance and
movement evidence remains in the logs; spoken exceptions are replaced with a
brief unavailable message.

Native field/world target captures stop while menus are open, and main-menu
focus speech interrupts a previous focus announcement. These changes remove
confirmed unnecessary work and queued focus speech. They do not alter native
menu input, fades or animation readiness. Automated capture and speech-boundary
tests are not measurements of live frame rate or proof of audible behavior on
the tester's NVDA/controller setup.

## November 4 object and counter update

Every installed model used by a non-player field actor was rendered from its
exact installed mesh and texture (front, back and side) and reviewed. The
appearance table now has 326 entries (279 new). In the static script census,
3,696 of 3,758 non-player actor entries now carry a reviewed appearance, for
example “Soldier in a tall cone helmet”, “Cook with a frying pan”, “Chocobo”,
“Barrel” or “Chest”. Labels describe what is drawn only. They never name a
story character, reveal a reward, or speak an asset code. Twenty models whose
mesh is flat or ambiguous even in its own idle animation stay generic rather
than guessed; they are listed under `deferred` in
`reviewed-field-labels.json`. Model resolution follows the game's own upscale
and rename tables, so variants that load another model's mesh share its label.

Shop, inn and other counter conversations are bound to the person behind the
counter when the game itself proves the pairing. Field scripts list these
counters in the native QuadTalkableData table, or the counter's Confirm handler
requests one person's own dialogue or shop menu. Twenty-nine such
region-mediated conversations in 27 fields now use this binding (previously
18), including Treno's card stadium clerk, Wayne's synthesis counter and the
later Lindblum innkeeper reached through the inn's director script. The person
stays listed with the counter as its interaction while the player looks away;
the game's facing rule applies only when the player is standing at the
counter. Native rendering, request level, talk permission, map rules and the
handler's own guard still decide presence. The
counter's own Story evidence now travels with the person, because the counter is
not listed separately.

A bound person may take a reviewed role heading from the speech that the
counter requests, such as “Innkeeper”, “Shopkeeper”, “Card Seller” or “Lazy
Carpenter”. The role list gained counter staff, workers and guards. Personal
names, titles and relations in those headings still wait until the game shows
them on that actor. A heading that only restates age or sex, such as “Man”,
does not replace a richer reviewed appearance.

Contact props such as chests have a push handler instead of Talk. The game
runs it while the player touches the prop, and the prop shows its own bubble
before waiting for Confirm. The contact reader therefore reports a chest as
ready only while the presented bubble belongs to that chest. Touching an
opened or busy chest, or one whose bubble a nearby person's bubble replaces, is
in range but not ready, and no turn is requested. People whose Talk is open,
including reviewed listeners, keep the ordinary talk check.

These are offline reviews and native-reader fixtures. The counter and contact
readiness readers still need the shared interaction probe to call them. Live
camera framing, every story state, and audible delivery on a player's screen
reader still need in-game confirmation.

## November 4 background objects and label wording

Seventeen Confirm spots whose object is drawn in the field's own background
art now carry a reviewed visible-form label, for example the Gargan Roo
“Lever”, Fossil Roo “Switch lever”, the Black Mage Village forest “Signpost”,
South Gate's “Carved stone marker” and “Stone monument with a plaque”, and the
Ice Cavern “Ice wall” spots. Each was reviewed on an installed scene render of
the listed camera. Such an object is listed only while that art is on screen in
the same scene and camera, its single live region may be requested at Confirm
(level, talk bytes, map rule, guard not proven unavailable), and a walkable
approach exists where it is the first Talk region. Reviewed rows and the
evidence class of every studied case are in `reviewed-background-objects.json`.

A bubble the game presents for something that is not reviewed — hidden
pickups, signs whose art cannot be identified offline, model-less touch props —
is an “Interaction point”. Its contents, reward and any future name are never
read or spoken. Region scenes that run several speakers or a director are
never credited to one person. Automatic walk-in scenes are classified for the
Story owner rather than listed as people.

Target names no longer receive arbitrary letters. A unique description is
spoken as it is (“Innkeeper”, “Lever”). Identical descriptions in the same
category are numbered in the order they were first presented in the map
(“Chest 1”, “Chest 2”); a target already spoken alone keeps its plain name and
later twins continue from 2. Unknown descriptions keep the honest words
“Person”, “Object” or “Item”.

These readers need the shared field-target hooks to be wired, and live camera
framing still needs in-game confirmation.
