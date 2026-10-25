# Chocobo's Forest navigation

The October 16 repair also restores the ordinary Forest walk-out to the Exits
list. The old camera test searched only the far outer floor edge, just outside
the camera; the actual native exit trigger begins on the visible path. The
reader now intersects that trigger with the permitted floor and validates safe
native-first-region approaches. This is a shared repair for the same omission
elsewhere in the game. Blind Soldier's exit sound indicates proximity to the
listed usable zone. A fresh live Forest departure and cue listening test remain
pending; the captured 175-triangle Forest route regression passes offline.

The People target for raised Mene routes to the game's ground talking area.
When the native region is selected and its interaction bubble is presented,
navigation announces that you are ready to interact. Press the game's ordinary
Confirm key to speak to him and choose Hot and Cold. Automatic walking never
presses Confirm or starts a round.

The rendered object without a talk handler uses a reachable viewing point.
Arrival says that the object has no interaction. It is not a buried treasure
target and does not provide digging guidance.

Both changes preserve the observed actor's position and identity. Camera
scrolling retains only the last observed talking region, with the existing
"last seen" announcement. An unseen actor or region change does not update
that snapshot. A replaced actor, scene/save/player change or removed actor
clears it. A changed presented interaction region invalidates the old route.

The talking-region binding is restricted to field 2950, its Forest scene,
Mene's model/entry and the verified installed actor-9/quad-19 script hashes.
The noninteractive viewing policy similarly checks actor 11's model and script.
Modified or different localized scripts fall back to existing navigation until
their bindings are verified. Approach calculation runs only when requested;
it retains the captured native portals, height and full player-radius checks.
There is no walking connection to the raised mesh and none is invented.

The regression fixture retains all 175 triangles and their native crossing,
radius, neighbor and edge-clone data from the player's failed Forest routes.
Tests verify every successful route leg against that geometry and retain the
original disconnected raised route as a negative case. The second fixture
records a post-conversation departure failure; live object departure and a
Hot and Cold round still require testing.

The October 12 live session reached the talking area, and player-controlled
Confirm opened Mene's dialogue. The subsequent object attempt supplied another
capture: the player was within wall clearance after moving near the talking
area. The planner now permits a departure only when every existing wall contact
recedes from the starting position and regains the full radius before the first
support leg ends. Later contacts, motion deeper into a wall and blocked portals
still reject the path. The fallback corridor adds an inward point on the
current triangle before proceeding. It does not change the player's radius,
position, collision flags or native push-away behavior.

Native low flag 0x80 is radius/traversal visitation scratch and is excluded
from the geometry fingerprint. The high-byte collision attribute 0x80 remains
part of both permission and fingerprint checks. Nine focused Forest cases and
285 core cases pass with the follow-up captured position. Live object departure
and a Hot and Cold round remain pending.

The native interaction rules are recovered managed engine code, checked against
the [Memoria walkmesh source](https://github.com/Albeoris/Memoria/blob/main/Assembly-CSharp/Global/Walk/WalkMesh.cs).
Installed field scripts establish the ground-region binding. The retained
Ghidra Unity/Mono analysis is under `D:\FF9Access\research\2026-10-10\ghidra\x64`;
this fix adds no native executable offsets.
