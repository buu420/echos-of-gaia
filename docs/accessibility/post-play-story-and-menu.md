# Post-play castle navigation and main-menu speech

The opening play, Zidane's castle search and Steiner's search share scenario
counter values. Story guidance now also checks the controlled character, native
script hashes and current scene phase. It does not use the presence of another
character as proof that the player is controlling them.

After the disguise scene, Story directs Zidane up the castle stairs. The native
script waits for the player to ascend; nearby guard-door regions instead trigger
Blank's warning and are not the story objective. The approach is calculated from
reachable native stair geometry, with clearance for the navigation arrival
radius. This adds a height goal without changing the existing rooftop goals.

Steiner's objective follows the current castle rooms toward the west tower.
Chapter-specific transit limits keep later scripted shortcuts out of this
route. When one exit region serves multiple floors, each destination is bound
to the surface points where the reviewed native script proves that destination.
Story uses only the points for its selected next room. Unproven points stay
unavailable. This analysis observes prospective player coordinates; it does not
move actors, change scripts or reveal hidden items.

The main-menu reader can recover when a presentation callback fails to complete,
provided the current native menu is active, selected and fully settled. It still
requires foreground ownership, native Loading to finish, visible labels and idle
fade/character animations. Existing opening and selection logs include the
foreground, activity, callback and animation conditions; no per-frame logging is
added.

The supplied tester log records selections with `ready=False` and
`loading=False`, but lacks the other conditions. It cannot establish whether
the reported "Date modified" speech came from focus loss or a blocked callback.
Callback recovery has regression coverage; the exact tester failure still
requires a new gameplay/controller and audible check.

Validation includes source and integration tests, both game architectures,
emitted assembly checks and detached probes using the game's original Mono.
These checks do not constitute a complete live playthrough of the castle or
listener confirmation of menu speech.
