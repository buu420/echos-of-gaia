# Consistent navigation distance counts

The player wants one useful distance measure for keyboard, D-pad and stick;
walking or running changes how quickly it is consumed, not its meaning.

The runtime already reads the actual native position. Native field walking
uses one movement update and running uses two; partial analog movement and
slopes can change displacement, so estimating progress from held-input time
would be less reliable. Retain current position-based route progress.

The discovered error is in RouteInstruction.DirectionFor: the control-frame
projection replaces geometric distance with its dominant axis whenever the
bearing rounds to a cardinal direction. Rotating the input basis can therefore
change a count without any movement or route change. Use the horizontal map
length for all counts and the projection only to choose the spoken direction.
This is the smallest correction; input-time calibration would duplicate native
movement imperfectly, and changing movement speed would change the game.

Keep the existing field/world distance-unit sizes, route bends, corrections,
world wrapping, arrival and delivery gates. Counts remain rounded short
distances rather than exact animation footfalls or keypresses. Do not change
native inputs, walking/running behavior, camera settings or automatic walking.

Build and install the verified local candidate with save/settings/audio and
rollback preservation. No publication, game launch or generated gameplay input.
The user has opted out of further approval prompts; execute this scoped fix.

An unrelated existing edit removes NativeStreetScriptsProvePlainWalkingPassages
from tests/FF9Access.Tests/FieldPanoramaPresentationTests.cs. Preserve it exactly
and exclude it from this task's commits. Report core test totals from the actual
working-copy run; do not claim that unrelated edit as this fix.
