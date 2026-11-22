# Ice Cavern, Dali and Treno tester repairs

The two supplied Memoria logs identify failures in the Ice Cavern field maps, Dali's weapon shop and Treno. They do not include the route snapshots, so the exact player position at each reported failure is unknown. Regressions use original native entrances and walkmeshes, rather than claiming an exact replay of the tester's session.

## Changes

- Route planning retains full body clearance and native floor connectivity. When a triangle-center corridor has an unsafe corner, a detached worker can search safe portal crossings and interior turns. Cancellation is checked throughout this fallback.
- Two exact original walls on Treno's tower street have a bounded clearance review for Steiner's native 120-unit body. The original movement controller traverses them using its ordinary wall forces. Different maps, changed wall coordinates and larger bodies retain the original conservative checks.
- Ice Cavern Story distinguishes the lower floor, upper path and side shelf. The lower floor offers the native Confirm climb before the upper exit. No jump or Confirm input is synthesized.
- Animated entrances, including the Pub in Treno's Slums, use the current scrolling camera area. They still require the active rendered door frame, matching camera, opaque backdrop and native trigger priority. Closed or inactive artwork supplies no entrance proof.
- Actor approaches include the native interaction radius on the player's connected floor. This reaches Dali's shopkeeper across the counter without walking into the disconnected floor behind it.
- Morrid's model airship is bound to its own native examine region. The stopping region leaves room for the follower's arrival tolerance, and the native facing guard can request a turn. Rendering, actor creation, native flags and request-level checks remain required.
- Shop selection and quantity speech explicitly interrupt for both keyboard and controller input.
- A saved character name that exactly matches a foreign-script native default can use the current language's native spoken name. This handles Japanese default Zidane in an English session. The displayed and saved name is unchanged; Japanese play and custom near-matches remain unchanged.
- Failed route summaries now include the failure reason, starting position, triangle, radius and candidate count in Memoria.log, alongside the existing route snapshot path.

## Verification and remaining player checks

Original controller replays cover the Ice Path corner, Treno tower street, Slums-to-Pub, weapon-shop approach and model-airship examine region at 30 and 60 units per step in both native Mono runtimes. The Story action provenance validator checks all reviewed bindings against the decoded original event inventory. Portable regressions cover current and hidden door artwork, camera mismatch, request guards, foreign names, interrupted speech and invalid geometry.

These checks do not establish a complete live playthrough. Testers should verify the Ice Cavern climbs, Treno Pub listing and entry, ordinary Confirm at the weapon shop and model airship, controller shop speech, and Zidane's name in dialogue, menus and battle. The logs alone cannot certify that every reported Dali Story transition is resolved.

For another navigation failure, include Memoria.log from the game folder and a ZIP of `%LOCALAPPDATA%\FF9Access\field-routes`. The JSON snapshots retain the exact start, target, geometry and failed waypoint; the Memoria log now provides a useful summary even when those files are missing.
