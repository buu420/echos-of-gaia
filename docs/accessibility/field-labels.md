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
