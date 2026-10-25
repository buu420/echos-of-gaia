# Native movement contracts

## Field follower

`FieldMapActorController.MovePC` consumes the last entry of `movePaths` first. Navigation therefore installs a reversed, privately planned waypoint list. It resets native count, squared segment length, current target and still-frame state; the existing native walk/run choice and collision resolution execute the movement. Navigation never assigns player coordinates.

An ownership token on the controller protects replacement paths. Manual input, a mouse destination, loss of control, scene detach and native interaction/still-frame clears revoke only the matching token. `ClearMoveTarget` is deliberately not a full-clear notification: the native follower also calls it between waypoints. Owned waypoint completion uses at most four units, and its step is capped at the remaining waypoint distance; body radius and collision remain native. A valid owned path can move with neutral analog controls. Foreign paths retain the game's speed and completion rules. Reaching an approach is not proof that an exit transitioned.

Actor approaches are sampled around the actual collision and talk radii, including the direction toward the player. The planner validates those samples against current walkmesh geometry. The native NPC collision selection, talkability and facing checks establish readiness; the mod does not infer readiness from the sample alone.

`MAPJUMP`/`WMAPJUMP` notify navigation only after the native opcode actually executes. Only the selected trigger UID can establish an exit transition. No scripted request or Confirm is synthesized.

## Field interaction readiness

`WalkMesh.Collision(controller, 4, out distance)` reads the actual ordered NPC selection and native angle weighting without invoking input handling. `EventCollision.CollisionAngle` supplies the strict `-1024 < angle < 1024` talk arc. Readiness additionally requires a talk entry, native request level greater than one and the map-specific live guards from `IsNPCTalkable`. Guard variables are read from copied bytes; `getVarManually` is avoided because it changes EBin evaluation scratch state. `listener`, `sSysAngle`, event requests and native input state remain untouched.

Automated tests establish waypoint ordering, ownership isolation, stale-scene rejection, collision selection and distinct approach/interruption outcomes. They do not establish live engine traversal; captured native path/run/approach/manual-takeover evidence is recorded separately during deployment validation.

## Separate field geometry contracts

Native geometry has three representations. Support and active-triangle height interpolate the actual cached `WalkMeshTriangle.originalVertices` in XZ, then add the floor's current-minus-original Y. Ordinary native body-radius checks use those originals unchanged; map 2952 alone adds the floor delta on all three axes. BGI crossing uses raw vertices plus current BGI/floor offsets and its native projection plane. A single current-position formula cannot represent all three.

Runtime neighbor arrays are copied at load. BGI animations can rewrite native neighbors and source edge clones without rewriting that runtime array. Traversal requires the runtime permission gate and native neighbor/clone agreement, matching portal geometry, support continuity and crossing continuity. Radius recursion instead follows its own native runtime-neighbor permission rule; it does not invent a shared-support or runtime-enabled restriction. A presented outer edge requires negative adjacency in both representations.

Routes use connected native portals and midpoint samples of the actual clear portal interval, retaining necessary slope heights. Captured field 611 Object A/Object B regressions cover the connected ramp and short interior portals. Swept body clearance remains local to the connected native surface. The planner is conservative: unsupported YZ/XY crossings, incompatible moving-floor discontinuities and malformed native references report unavailable. Universal path completeness and moving-floor parity are not claimed.

Typed geometry fingerprints cover support/radius/crossing coordinates, floor deltas, both adjacency arrays, clone values, flags, attribute masks and native normal/plane inputs. They avoid per-frame reflection-based diagnostic serialization. Bounded diagnostic captures record successful plans as well as failures, with detached raw cached/runtime/BGI state copied on the game thread. Raw capture arrays are created only during capture, never as part of each fingerprint read.

## World snapshot and point probes

The engine's world initialization loads the current disc's 480 blocks before normal movement. Navigation copies the already registered current `ActiveWalkMeshes` in slices of 24 blocks. It never loads or instantiates a block, changes a form or invokes a native raycast. Canonical positions use each block's initial indices and mesh-to-block transforms, so visual wrapping does not change route geometry. The disc, control mode, all forms and readiness state form the route fingerprint.

Native movement limits are copied from the active `w_moveCHRControl` table; high mask bits cover topography 32–63 and low bits cover 0–31. `flg_gake` rejects ground/water crossings. Control modes 2/3 remap raw IDs 56/57 to 54. Ordinary movement uses a point probe, with radius reserved for the native get-off search. Navigation does not board, land or disembark automatically.

The downward probe starts at current Y + 2.34375 (or +400 for flying). Inspection of both local and upstream `WMPhysics`/`WMBlock` shows that the supplied 2.8 distance is never checked: adding a distance cutoff would be an inaccurate restriction. Normal probes skip IDs 4078/4088/2040 and normals with upward component <=0.1; flying probes accept these. The first triangle hit in the first active mesh wins, with the native special rejection of mesh hits on ID 0x31EE outside plane control. Specified cached triangles bypass ordinary normal/ID exceptions exactly as the native path does.

The native 10-entry ring is copied in its actual lookup order and evolves only in a private cache during route validation. Bridges (topography 49/52) bypass that cache outside plane control. Every returned route leg is sampled through the copied point-probe rules. Wrapped shared edges weld at 1/256-unit precision. Routes avoid other entrance event cells; only a selected town's own cell is an eligible destination. Region points remain observed points, and the shortest reachable candidate is selected.

Runtime snapshot reads compare actor positions, all native collision caches, physics flags, and block forms/readiness before and after copying. A changed sentinel rejects that read. Automated fixtures establish the pure contract; live captured terrain examples and sentinels remain deployment validation evidence.

Pure world search runs on one cancellable worker with private context, candidates, collision cache and native rule copies, sharing only immutable geometry indexes. All native reads, input, movement and speech stay on the game thread. Generation, target, transport, fingerprint and height freshness guard worker completion; cancellation or replacement cannot install an old result. Initial mesh capture/index construction remains on the game thread and can cause a short initial pause; later pure search and pending cancellation were exercised separately.

## World movement and town autopilot

`WorldSteeringCommand` contains only LX, LY and movement-local throttle integers. `ff9.w_moveGetPadStateLX/LY/R` consume that overlay; it never enters Hono's gamepad state, `EventInput`, `ETb.KeyOn`, or native script requests. The alternate-flight control override also preserves zero vertical movement. Ground direction is transformed through the current native camera rotation; boats/airships rotate toward the next validated leg and throttle only inside three degrees. Native movement, collision, speed smoothing and manual input continue to execute normally.

Only active airship statuses 9/10 and a catalogued town's actual location ID can start the native town autopilot. The native HUD contract sets `w_frameAutoid` before `w_movementAutoPilotON`; releasing matching ownership calls the actual `OFF`, including its camera-state restoration. Boat control and observed region IDs never enter that path. Town completion uses wrapped position and the native stopping distance, not region equality.

Vehicle approach candidates refresh from live positions and the native talk-distance terms. A moved vehicle invalidates its old approach so it can be replanned. Arrival announces an approach and leaves boarding/landing to the ordinary game action. Physical forward, reverse, axes, triggers, camera bumpers, Confirm, loss of focus/control and scene changes revoke owned travel before movement consumers. Compiled-IL checks enumerate overlay consumers and reject event/input-request calls; live cancellation and no-unwanted-activation checks remain deployment validation.
