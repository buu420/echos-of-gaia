# Positional field proximity sounds

The player wants two supplied effects to identify nearby NPCs and interactable objects, plus genuine HRTF positioning for the existing exit effect. `FL_GFX_UI_SciFi_Interface_04.wav` is confirmed for NPCs and `FL_GFX_UI_Game_Interface_29.wav` for interactables. Standing user instruction waives repeated approvals; no additional approval gate applies to this authorized local implementation and deployment.

## Behavior and boundaries

Reuse the current native field catalog, before Story aliases are appended. Only current, interactive targets qualify: no remembered landmarks, hidden entities, synthetic Story targets, viewing-only objects, or targets from another map/visit. Items use the interactable effect. Each kind selects the nearest approach within the existing 400-unit envelope, requiring the existing native clear-segment check. The full list remains available through navigation.

Walking and cues share the same native floor/interaction-region preparation, including raised actors such as Mene. Source position stays at the actor for HRTF. Current plain walk-out exits may carry ObservedRegion evidence; remembered exits instead carry ObservedFieldLandmark and stay silent. Cheap horizontal bounds avoid preparing geometry for distant targets.

Keep Blind Soldier's 80/400-unit attenuation and 3200 ms pulse interval. Space pulse starts by at least 250 ms across kinds. A moving or disappearing target updates or removes its active voice; no backlog of cues survives. Gates, map/save/player identity changes and foreground loss clear voices and cooldowns. Battle, native menu/dialogue/control locks and Hot and Cold inspection remain silent. Ordinary walkable prompts retain navigation permission.

Directions use the exact field input orientation used by spoken navigation: right is positive X, ahead is negative Z in Steam Audio, and native Unity Y provides elevation. Update direction and attenuation at the existing 200 ms field proximity probe. A target coincident with the listener uses a neutral forward direction; invalid coordinates produce silence.

## Audio architecture

Use Valve Steam Audio 4.6.1's built-in HRTF, matching the existing Blind Soldier backend and explicitly binding x86 stdcall-decorated and x64 undecorated exports. Verify native ABI and imports with Ghidra and actual probes in the original game Mono runtime. Ship pinned native DLLs and license/third-party notices. No Unity plugin, MelonLoader, game audio replacement, or process-wide mixer changes are required.

A dedicated worker owns recording reads, HRTF resources, three effect voices, and a small stereo waveOut ring. The game thread submits detached cue snapshots only. It never loads/decodes recordings, renders an entire sound, waits for an audio worker, or joins that worker during a transition. Bound queued audio to roughly 64 ms (three 512-sample blocks at 24 kHz). Worker snapshots expire after 750 ms without a fresh field update, preventing persistent sound if the game loop stalls. Stop requests wake the worker and reset queued buffers; driver-owned memory is freed only after successful reset/unprepare.

Keep narration's existing mono output independent. Missing assets/backend fail audibly once through accessibility speech and are logged; no stereo-pan substitute is labeled HRTF. Independent NPC/object toggles and 0-200 percent volumes join the existing exit controls in the accessible launcher. All honor the native sound-effect mute and sound volume.

## Verification and delivery

Core tests cover presentation, walls/floors, map/save/player/generation changes, cooldowns, distinct kinds, volume/mute and coordinate transforms. Audio tests cover bounded output, voice removal, stale snapshots, driver failures and HRTF lifetime. Native probes validate both ABI variants, stereo energy/delay differences, direction changes and teardown in game Mono without launching gameplay. Launcher checks cover UIA names, validation and settings preservation. Build both architectures, review the final diff, package explicit assets/dependencies, snapshot and deploy with rollback. Preserve saves, settings, observed identities, narration and native game components. Report listener and live gameplay verification separately.
