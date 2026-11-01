# Field target labels

Echos of Gaia 0.1.5 uses the original native message subject to associate a
displayed speaker heading with an NPC. Bubble tails and fixed-position text no
longer determine whether that association survives. Names come from the page
actually displayed, not future dialogue or a lookup of unrevealed characters.
Unattributed region text stays unassigned. Reused actor IDs require the same
model, script entry and original script fingerprint before a remembered name
can be reused. Older unscoped names must be observed again.

Before an attributed name appears, 38 reviewed person/animal models and two
object models have appearance descriptions. The exact entries and visual
features are in `reviewed-field-labels.json`; they were checked against rendered
installed meshes. For example, an unnamed child can be “Child with pigtails A”.
Unreviewed models retain a generic label. Stable letters distinguish matching
appearances; this update does not claim every NPC or object is named.

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
