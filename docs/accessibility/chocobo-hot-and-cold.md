# Chocobo Hot and Cold

Move Choco and dig with the game's ordinary controls. The reader speaks the meaning of the cry shown after a dig:

- "Nothing nearby."
- "Far."
- "Close."
- "Treasure located; keep digging."

Once the game shows depth, updates say "Depth 80", for example. Rapid pecks keep the latest displayed depth pending instead of queuing old numbers. The found counter is read when it changes. Repeated cries with the same meaning stay quiet after that meaning has been spoken.

Press L for status: shown time remaining, the last displayed cry meaning, depth while its window is shown, and the found count. Press R to repeat the last accepted announcement. These are the normal navigation Status and Repeat bindings and can be changed in the launcher. During this minigame, the configured navigation stick click (R3 by default, optionally L3) reads status directly when it does not conflict with the game's own bindings.

The ordinary timer, music, sound effects, movement and digging continue normally. Sparse warnings occur when the shown running clock crosses 10 and 5 seconds. Native bonus time rearms those warnings. Item names, inventory-full messages, bonuses, points, totals and beak-level changes are read only after their result dialogs are shown. Results and requested status get priority over passive digging feedback.

Already-shown results can finish speaking after their windows close, while ordinary navigation becomes available again. Starting another round clears any old backlog. Output is counted as read only when the speech backend accepts it; temporarily rejected results remain pending in the current field. Pause, background, loading and scene changes discard pending speech.

Automatic navigation and its full controller menu stay gated during the round. Afterward, ordinary field navigation becomes available again. The reader does not locate buried treasure, reveal unrecovered prizes, dig, or advance dialogs for you.

The reader covers the Forest, Lagoon and Air Garden presentation and verifies bindings against all seven installed language tables. Its concise accessibility phrases are currently English; result text uses the game's selected language. A changed or unsupported cry falls back to ordinary dialog speech. Dual-language presentation and live localized rounds have not been validated.

Restart FFIX after installing. First try an ordinary Forest round, listen for changes between far and close, then for located and depth, and use L during digging. Check that results finish speaking and that another round starts without old depth or found values. Live round listening and physical controller testing remain required; automated checks do not establish those results.
