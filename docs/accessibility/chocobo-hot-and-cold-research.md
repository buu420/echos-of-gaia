# Chocobo Hot and Cold accessibility research

The useful foundation is the game's displayed feedback and existing field
navigation. The player should move and dig normally, receive clear equivalents
of Choco's displayed cries, read the changing depth and found count, and hear
the visible timer and results. Buried treasure coordinates are not navigation
targets.

## Evidence

The installed English field scripts and backgrounds were read without changing
the game. The retained extraction is under
`D:\FF9Access\research\2026-10-12\chocobo-accessibility`.

| Field | Native scene | Event script |
| --- | --- | --- |
| Forest, 2950 | FBG_N14_CHCB_MAP300_CH_FST_0 | EVT_CHOCO_CH_FST_0 |
| Lagoon, 2951 | FBG_N14_CHCB_MAP301_CH_HLG_0 | EVT_CHOCO_CH_HLG_0 |
| Air Garden, 2952 | FBG_N14_CHCB_MAP302_CH_FGD_0 | EVT_CHOCO_CH_FGD_0 |

Field text zone 945 provides instructions, proximity responses and result
messages. Text must be split using the engine's STRT and TXID rules; ENDN alone
does not preserve native message IDs. The extracted English table verifies:

| Native text ID | Displayed role |
| --- | --- |
| 34, 35 | Controls and the meaning of Choco's cries |
| 274 | Found count, using displayed number variable 7 |
| 275 | Depth label |
| 276 | Separate depth number, using displayed number variable 6 |
| 277 | Time expired |
| 278-281 | Nothing nearby, far, close, and treasure located |
| 282-293 | Further shown-window variants of nothing nearby, far and close |
| 294-320 | Visible item, inventory-full, bonus, point, total and beak-level results |

The installed `mainData` ResourceManager identifies the field text assets
for US, UK, JP, FR, GR, IT and ES separately from their battle text assets.
All seven field tables have now been checked using native STRT/TXID parsing.
JP has found/depth IDs 274-276 and cry IDs 278-298; FR, GR, IT and ES have
found/depth IDs 275-277 and cry IDs 279-299. Those five languages include
additional cold cry variants. Their instruction dialogs verify the same
four spoken meanings. Depth-label and numeric-window coordinates also vary
by language and are explicitly checked. The runtime uses only active shown
dialogs and their rendered numeric replacements, never latent script variables
or future prizes. The locale mapping and extracted message evidence are under
`D:\FF9Access\research\2026-10-12\hot-cold-reader`.

`Dialog.AnnounceDialogForScreenReader` already speaks normal opened dialogs,
including the cry messages. Its overlay exclusion in zone 945 concerns the
Gysahl Greens purchase quantities. It does not explain missing Hot and Cold
feedback. `Dialog.UpdateMessageValue` resets the displayed parser when numbers
change, but does not announce those changes. The depth label and number also
occupy separate windows, so an isolated numeric announcement needs context.
Existing cry, instruction and reward speech still needs a live round audit.

`TimerUI.Time`, `Enable`, `GetDisplay` and `Play` expose the ordinary timer.
The timer is set, started and displayed through the existing TIMERSET,
TIMERCONTROL and TIMERDISPLAY instructions. Reading it does not require
changing the timer, speed, input, or game state.

`NativeNavigationContext` currently gates navigation inspection and movement
while `EventHUD.CurrentHUD` is a minigame. A Hot and Cold status reader therefore
needs an explicit inspection path that leaves automatic movement gated.

## Recommended implementation

1. Bind only active, shown Hot and Cold dialogs in these fields. Give the four
   cry responses concise semantic speech: "nothing nearby", "far", "close",
   and "treasure located; keep digging". Avoid duplicate generic cry speech.
   Preserve ordinary game audio and native movement and digging controls.
2. Observe the finished displayed number replacement for the depth window and
   announce its value with the depth label. Track the displayed found count and
   ordinary shown item, bonus, points and beak-level results. Do not read hidden
   item identities before the result message appears.
3. Let the existing status command report displayed time, the last displayed
   proximity response, depth when shown, and found count. Add sparse time
   warnings, with dig feedback taking priority over timer chatter.
4. Keep already observed field landmarks available as "last seen" when the
   camera scrolls, so the player can return to Mene. Navigation to a remembered
   point uses its observed location, never an unseen position update.
5. Process only active minigame presentation. Reset pending speech on a round
   end or scene change and remain silent in loading, menus, battles and the
   background. Use current references and displayed values; no per-frame scene
   searches, asset loads, route jobs, or synchronous logging are needed.

The camera-scroll retention is implemented in the previous change. The
dedicated Hot and Cold reader is now implemented in `ChocoboHotColdReader`
and `NativeChocoboHotCold`. Opened cry/result dialogs use the existing
dialog-complete hook. Numeric updates reuse the active dialog list and retain
only a given window/parser's last rendered number while its replacement is
pending. Paired depth-label/number windows are required. New presentation
identity, hidden windows, scene changes and round boundaries clear stale data.
No parser is forced to render, and no new scene search, asset load, route job,
game command or timer change is introduced by this reader.

The inspection path bypasses target-catalog and route work during Hot and Cold.
Only Status and Repeat keyboard commands, plus an available configured stick
click for Status, are captured. Movement, digging and Confirm pass through.
Pending feedback is coalesced, item/results are combined without losing their
shown text, and passive cues cannot interrupt results or requested status.
Already-shown results have a speech lifetime independent of a completed window;
normal closure/gaps can drain them without holding navigation in minigame mode.
Opening/typewriter ordinary dialogs and their automatic narration take priority.
Starting a new round clears old result backlog. The facade's `TrySpeak` returns
actual acceptance, and only an accepted reader proposal retires queued results
or marks a cue spoken. Rejected output retries at a bounded interval in the
current valid presentation.
An exception disables dedicated ownership for that process and preserves the
ordinary dialog reader, with a spoken status-unavailable report.

## Validation needed for the dedicated reader

Capture one ordinary player-controlled forest round to verify opened cry
dialogs, updated numeric text, timer visibility, reward text, and round
boundaries. Then validate localized bindings and equivalent Lagoon and Air
Garden presentation. Tests should cover repeated pecks without overlapping
speech, depth reaching zero, timer warnings and bonus time, full-inventory
results, stale dialogs, pause/background gates, and reset between rounds.

## Primary references

- [Memoria dialog implementation](https://github.com/Albeoris/Memoria/blob/main/Assembly-CSharp/Global/Dialog/Dialog.cs)
- [Memoria timer implementation](https://github.com/Albeoris/Memoria/blob/main/Assembly-CSharp/Global/TimerUI.cs)
- [Memoria minigame HUD implementation](https://github.com/Albeoris/Memoria/blob/main/Assembly-CSharp/Global/Event/EventHUD.cs)

The native Unity/Mono Ghidra analysis is retained at
`D:\FF9Access\research\2026-10-10\ghidra\x64`. The displayed message and timer
seams above come from the recovered managed engine and installed assets;
this design does not introduce guessed native offsets.
