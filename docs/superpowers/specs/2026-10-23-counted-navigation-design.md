# Counted route directions

The player wants Blind Soldier-style instructions, such as "left 2", changing to
"up 5" at the corner. Current FFIX speech combines a direction to the next
waypoint with distance to the final target, in large native units. That is
misleading on any bent route.

## Approach

Use the existing validated waypoints, with a small counted distance to the
current leg. Merge only collinear consecutive legs; retain every bend, including
short ones. A turn speaks promptly without the periodic reminder delay. Status
includes the selected label and the same instruction. Routine guidance stays
short. This is the implementation choice under the user's standing instruction
to proceed without repeated approval requests.

Keeping raw bearings would not address the reported confusion. A new grid or
invented left-then-up path would cut across the game's walkmesh and is rejected.
Blind Soldier's source supplies the speech pattern, not FFVII geometry or speeds.

## Behavior and limits

- Field instructions follow the native field input twist. World ground travel
  uses the native type-0 camera/control transform. Turning vehicles and Chocobos retain
  compass bearings, since a held left key turns the craft rather than strafing.
- Counts are fixed distance markers: 240 field units or 4 world units. The field
  count is eight ordinary 30-unit walking updates; world walking is 112/256 per
  update. These are similar short travel intervals, not literal footfalls or
  timed key holds. Running, slopes and analog input alter travel time.
- Short remaining legs say "a little". Only reaching the final validated route
  point can say "Route approach reached"; no rounding to zero implies arrival.
- No connected route: explicitly say "Bearing only" and "No walking route".
  During planning, do not present a direct bearing as a route instruction.
- Ordinary speech and dialogue retain priority. A new turn may replace the
  mod's own previous guidance, but not unrelated accepted speech. A turn deferred
  by speech remains pending as current state, not as a stale queued sentence.
- Target, map, save, player, transport, route or gate changes invalidate prior
  speech progress. The next valid instruction must not inherit an old timer.
- Preserve controls, path geometry, native movement and interaction gates,
  auto-walk, HRTF cues, audio descriptions, settings and saves. Never automate
  Confirm, boarding, landing, choices or combat.

## Evidence and verification

Reference: canonical Blind Soldier FieldNavigationSpokenCue.cs and its speech
policy. FFIX FieldMapActorController.MovePC and WorldSteering.For determine input
directions. Unity's [inverse-rotation documentation](https://docs.unity3d.com/460/Documentation/ScriptReference/Quaternion.Inverse.html)
supports reversing a rotation; the actual game code remains authoritative.
Read-only Ghidra review checks the installed Mono identity/invocation seam;
this managed change adds no native hooks.

Regress bent routes, countdowns, collinear runs, short bends, orientation/wrap,
false arrival, turn timing, busy speech, planning and context changes. Build both
architectures, run the core and existing integration/Mono checks, review the
change, then package and deploy with rollback and user-data preservation checks.
Offline evidence does not establish live headphone, speech or gameplay behavior.
