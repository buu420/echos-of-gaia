# Controller navigation and ordinary menu speech

**Status:** Complete. The user subsequently chose to remove the standalone stick-click navigation menu. The final implementation uses held-trigger navigation only; the separate Hot and Cold status click remains. Both local updates were built, reviewed and deployed with preservation checks before the 0.1.1 tester release.

**Goal:** Match Blind Soldier's held-trigger navigation on FFIX, repair the ordinary Triangle/Y menu reader, and deploy the verified result locally.

**Architecture:** Use Memoria's existing input broker with the held-trigger state machine and release capture. Remove the standalone stick-click menu. Repair menu speech from the actual UI selection and displayed labels. Correct JoyShock stick-click translation using official/native evidence.

**Constraints:** Preserve native movement and interaction, combat and minigame controls, custom keyboard bindings, saves, other mods, and all 608 narration recordings. No automatic Confirm. No public update is requested in this turn. User already authorized changes and deployment and explicitly opted out of further approval questions.

**Approach:** Reuse current controller sampling and capture rather than introducing another loader or input hook. Either trigger modifies D-pad left/right (categories), up/down (targets), L3 (guidance), R3 (automatic walking), Circle/B (stop). Unmodified stick clicks pass through during exploration. Native menu/battle/minigame contexts do not activate held-trigger navigation. Focus, reconnect, scene changes and held releases cannot restart a command.

## Tasks

- [x] Establish source/input baseline and exact Blind Soldier mapping; record log locations.
- [x] Add failing broker tests for both trigger choices, directions, explicit starts, release tails, focus/reconnect/map/menu/minigame gating and unchanged Triangle/Confirm; implement and run full core suite (554 passed after standalone-menu removal).
- [x] Independently trace and repair ordinary menu speech using actual-source regression tests (26 passed, independently reviewed).
- [x] Verify and correct JoyShock physical stick-click mapping using primary web sources and Ghidra, with failing raw-input regressions (nine passed).
- [x] Review combined diff; run appropriate native/context/Mono and staged build checks; commit clean private source.
- [x] Package both architectures, deploy transactionally, independently verify source identity and preserved saves/settings/narration; document unperformed live/controller listening checks.

**Review focus:** Trigger released before stick click; held inputs over focus/reconnect/map/battle boundaries; custom native Confirm and menu bindings; visible first menu selection during tween; genuine PS-controller clicks versus trigger flags.

**Execution:** Root implements broker and deployment; menu and JoyShock agents own disjoint files. Existing private feature checkout is used on codex/ffix-controller-menu-20261026; no public history or release changes.

**Rulings:** A button held on the trigger-opening sample requires a fresh press, matching Blind Soldier's current edge handling. A trigger remapped to native Confirm, Cancel, Menu, Special, Pause or Select keeps its native action; the other trigger and keyboard navigation remain available. Keyboard navigation shortcuts remain active, while native logical controls share the existing controller capture. Runtime/deployment evidence is kept in the private dated research folder, separate from source-level tests and live gameplay confirmation.
