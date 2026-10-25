# Battle loading investigation, 2026-10-11

The reported session with installed build `5e04635` spent 7,194 ms inside battle
initialization and later stopped logging after world loading began on return
from battle. The player reported a permanent freeze and forced the game closed.
The stopped operation is not yet established; that log is insufficient to
attribute the hang to footsteps, world geometry, or the native asset loader.

The next session used the same installed build. The player completed four
battles, returned to the world each time, and quit normally. Startup times were
8,589, 2,882, 1,573, and 1,761 ms. Initialization alone took 7,477, 1,997, 655,
and 800 ms.
These are consecutive runs of unchanged code, not a before/after improvement.
The first delay occurred before the external tracer attached.

The traced final initialization spent 733 of its 800 ms creating battle data.
One model creation took 693 ms, including an asset load of 432 ms. Native audio
calls completed in milliseconds. The two fully traced world reloads took 381
and 423 ms and successfully reused the navigation topology. No freeze occurred
in that session. The player confirmed delayed starts only.

Standalone probes exercised the installed x86 and x64 Mono and SaXAudio
libraries, including natural voice completion, a Unity-style child domain,
four concurrent sounds, and full collections over a retained world-sized heap.
They passed after allowing asynchronous completion callbacks to drain. This
does not rule out an intermittent live-game hang. An initial callback assertion
checked too early; its retained failed artifact is not a reproduced game hang.

The native battle sequence starts SFX after `InitBattleScene`, so slow model
loading also postpones the opening sound. Source inspection found potentially
repeated monster animation paths, but their individual cost has not yet been
measured. No animation cache or speculative audio rewrite was applied.

The diagnostic update buffers at most 32 animation-load timings in the existing
startup record and adds initialization phases. World loading records seven
checkpoints per full load: sea prefab, visibility, four groups of five rows,
and return from block loading. It adds no polling, worker, asset preload, speech,
garbage collection, delay, or game-state change. It is instrumentation, not an
established fix for the freeze or startup delay.

After installing diagnostic build `6187ed7`, a fresh process started at
22:29:36 UTC. The player completed two battles with startup times of 990 and
954 ms, initialization times of 94 and 53 ms, and successful world returns
taking 348 and 354 ms. The checkpoints completed and topology reuse succeeded
both times. The first battle's individual animation loads took 0-5 ms;
the repeated monster paths completed below the 1 ms timing resolution.
The previous multi-second model-loading delay did not recur. These observations
do not establish why the earlier asset loading was slow, and the diagnostic
update is not claimed to correct the intermittent freeze.

The full x86/x64 build, 217 core tests, 17 launcher tests, emitted native
boundary checks, original Mono speech/planning probes, and original Mono/native
audio lifecycle/stress probes passed. Installation verified 77 payload files
and 1,777 unchanged protected files. All five saves and both settings files
matched the fresh pre-installation snapshot before the next player session.

Evidence is retained under
`D:\FF9Access\research\2026-10-11\footstep-battle-freeze`, including the reported
logs, `live-1-runtime.jsonl`, `live-1-navigation.log`, native lifecycle/stress
logs, and the current saves/configuration in `post-live-1-preservation`.
The late animation observer could not attach because the game was exiting; an
earlier attempt to bind an uninstantiated generic method was rejected and was
not repeated. Neither limitation establishes a game defect.
