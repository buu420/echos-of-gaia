# Opening Story route repair

The tester cannot path to the early ticket-booth objective. Story must route
through the currently presented local exit toward the required area, keep the
same objective across map changes, and select the actual local interaction on
arrival. When no verified walking approach exists, explain the next area and
current limitation instead of attempting to walk to a remote goal.

Initial investigation establishes that the existing opening fixtures invented
square103/exit25-to-booth104 (actual25 goes to110), and used the wrong alley
exit ID. The real square enters104 through player30/tag1 after region28 sets
the native interaction phase. Existing generic transit hints also need testing
against full native street exit functions, including their visual wipe math.

Use original script/asset evidence and the Alexandria guide only to establish
expected order; current native presentation, script fingerprints, guards and
geometry establish physical targets. No remote coordinates, hidden NPC names,
guessed approaches, native script execution, synthetic Confirm, changed timers
or automatic movement across map changes. Walking remains an explicit command
for each current local leg. Preserve existing controls, menu/battle gates,
saves, settings and recordings. Full live/controller/NVDA proof stays separate
from source/fixture/build/package evidence.
