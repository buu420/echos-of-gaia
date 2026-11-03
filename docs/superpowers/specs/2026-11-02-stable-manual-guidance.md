# Stable manual navigation

The user confirms the ticket route works. Manual directions change too sharply
after sideways movement and should describe useful turns/corrections without
announcing an unchanged path as a reroute. Counts remain approximate distance,
not grid squares. Preserve native controls, all working automatic walking,
movement/collision/timers/Confirm, and existing screen-reader ownership.

Evidence: `ReadPresentation` currently chooses keyboard orientation whenever
the stick returns idle, while native `MovePC` chooses the control basis from
actual digital/stick input. `RouteInstruction` combines path nodes against the
player-to-node bearing, so a sidestep splits an unchanged straight run.
`RouteGuidanceProgress` also uses the automatic follower's four-unit tolerance
for manual progression. Finally, static exits select a nearest presentation
sample as Position, and both validation and speech can mistake that sample or
an equivalent rebuilt RoutePlan instance for a changed route.

Use the actual native movement input choice, retained while idle. Re-evaluate
its current native angle when the camera/control frame genuinely changes;
never remap controls or pretend old left/right remain valid after a rotation.

Manual guidance follows a stable geometric run to its next bend. Small drift
stays quiet when the forward segment is verified clear. Larger drift receives
an explicit correction to the nearest point on that same run, then the
remaining direction. Return to the current run rather than a point already
passed. Use body clearance for human turn tolerance, and verify the next
segment before advancing a corner. Tight turns, genuine diagonals, wrapped
world geometry, failure, arrival and blocked rejoin paths stay explicit.

An unchanged static exit approach or equivalent refreshed run must not trigger
new-route chatter. Refresh unsafe/stale geometry without pre-announcing a
reroute. A genuinely different next turn or needed correction still speaks
promptly, with rejected delivery retryable. Status/Repeat remain deliberate.

Build and install a local candidate for user testing, preserving all current
saves/settings/608recordings and unrelated game files. No public release,
game launch or generated gameplay/controller/Confirm input. Offline tests,
native assemblies/Mono and the user's eventual live manual test are distinct.

Approach choice: retain geometric legs and native input frames. Keeping every
tiny-node bearing recreates the reported confusion; freezing/remapping keys
across camera rotations changes native controls. The user has already opted
out of further approval prompts, so implement this scoped repair inline.
