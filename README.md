# Echos of Gaia

Echos of Gaia is a screen reader accessibility mod for the Windows Steam
version of **Final Fantasy IX**, maintained by **buu420**. It continues
[BlindGuyNW's accessible Memoria fork](https://github.com/BlindGuyNW/Memoria)
and the [Memoria engine](https://github.com/Albeoris/Memoria).

**[Download 0.1.13 beta](https://github.com/buu420/echos-of-gaia/releases/tag/v0.1.13)**
or install **Final Fantasy IX — Echos of Gaia (Beta)** from the **Buu420**
source in Amethyst's Accessibility Mod Manager. The source catalog is
[buu-s-mods](https://github.com/buu420/buu-s-mods).

This beta includes the current navigation, screen reader and
narration work. Field narration and navigation still need broader gameplay
testing, including a full playthrough.

0.1.13 reads the highlighted battle command, ability, item, and target from
the active battle menu. If speech fails, it retries the current selection.
Garnet and Prison Cage are read as separate targets in Evil Forest. Both game
runtimes passed offline checks. Live controller speech is ready for tester feedback.

0.1.12 repairs manual guidance and automatic walking on the West Tower
stairs. Auto-walk aims inside the selected exit trigger. Repeated background
checks stay quiet when the same route is unavailable. Reviewed interaction
prompts load again, and route snapshots include the exit's approach points.
The West Tower routes were checked in offline replays with both game runtimes.

0.1.11 expands Story guidance to intermediate actions such as using a ladder,
operating a switch or boarding a Gargant. It checks the current story state
and routes to the usable side of the next exit. Exit labels use destination
names, with numbered areas for distinct rooms that share a name. The object
list includes more named NPCs, props and visible save spheres. During the
opening sword fight, the current command gets priority over narration and
uses the controller or keyboard labels for the active input device.

Some interactions still have generic labels, and alternative routes, full
playthrough progression and speech with a physical controller need more
in-game testing. See the [whole-game coverage audit](docs/accessibility/guide-coverage.md).

0.1.10 repairs Story guidance after the play: Zidane follows the castle stairs
instead of Blank's guard-door warning, and Steiner follows the current castle
route. Exits serving multiple floors use approach points proven for the selected
destination. The main-menu reader can recover from missing transition callbacks
after the visible native menu settles, and logs more specific readiness conditions.
Both architectures have automated and original-Mono checks. Live castle/controller
and audible menu testing are still needed; the tester's exact "Date modified"
failure is not confirmed resolved. See the [castle and menu repair](docs/accessibility/post-play-story-and-menu.md).

This includes 0.1.9's repairs to current-area entity and exit presentation, including the second
Alexandria alley exit, and adds distinct Area A/B/C names where different scenes
share a native location name. Opening Story guidance follows the alley encounter,
steeple ladder and rooftop walking checkpoints, with a current story-region
approach for the first Evil Forest rescue. Dialogue choices retain their first
letter and read the selected displayed option even when other options are disabled.
Filtered interaction approaches remain intact through route planning. Main Street
automatic walking also stops short steps within the native arrival tolerance.
These repairs have automated and original-Mono checks; the complete opening,
forest, controller and audible speech routes still need player testing. See the
[entity and story audit](docs/accessibility/entity-story-audit-2026-11-03.md).

This includes 0.1.8's manual directions on the same stretch of a route instead of chasing
small path points. Small safe sideways movement keeps the current instruction;
larger drift gives a correction followed by the remaining direction. Equivalent
route refreshes stay quiet. Releasing the stick retains the game's actual input
orientation, and changing the control angle no longer shortens the distance
count. Walking and running consume the same rounded map-distance units.
The opening Alexandria street exits also remain available across the reviewed
camera scrolls, improving the route to the ticket booth. See
[manual navigation](docs/accessibility/manual-navigation.md).

This includes 0.1.7's opening Story route through Alexandria. Travel targets name
the next area and use the current map's exit; the ticket booth is approached
from the square through its reviewed visible interaction spot. Informational
steps explain the next area without starting a route to a remote objective.
Story remains selected across maps; the next walking leg still requires the
normal deliberate command. See [opening route details and validation](docs/accessibility/opening-story-routes.md).

This includes 0.1.6's prerequisite guidance in Pinnacle Rocks, Fossil Roo and Mount
Gulug, expands reviewed chest/frog appearances, and repairs card details,
initial card focus and changing auction/Daguerreo numeric readings. Native
Confirm, puzzle choices and clocks remain under player control. Steps without
a verified walking target stay informative. The [guide coverage report](docs/accessibility/guide-coverage.md)
accounts for 818 fields, 44 main-story chapters and 23 optional guide topics,
with unresolved visual, timing and live-test gaps listed explicitly.

NPC names are learned from attributed native dialogue. The appearance catalog
now contains 47 reviewed models, including one conditional body description;
many other targets still have generic labels. Exits retain screen-relative
labels and destinations learned from actual trips. See
[field label coverage and limits](docs/accessibility/field-labels.md).

The quiet map titles and concise controller navigation from 0.1.4 remain.
Hold a navigation trigger and press Triangle/Y for full navigation controls.

The standalone Windows ZIP includes a double-click installer. Extract the
ZIP and open **Install-EchosOfGaia.exe**; the installer finds Steam libraries,
offers a labelled game-folder field and Browse button, and displays progress
and a readable log. Users do not need to open PowerShell or type commands.

This release retains 0.1.2's recovery when Prism is detected but rejects speech output. The
same announcement is retried through the bundled compatibility backend, and
native Prism error codes are logged. Listening on affected tester PCs and
the reported Alt-Tab speech cutoff still need confirmation.

## Features

- Native menu, dialogue and battle-target speech, using Prism with Tolk/SAPI
  fallback, and an accessible Memoria launcher with speech testing and settings.
- Field categories for Story, Exits, People, Objects and Items. World categories
  for Story, Towns, Regions and Vehicles, including parked transport.
- Spoken route legs such as "left 2" and "up 5", updated at turns, plus optional
  automatic walking using normal movement and collision checks. Counts describe
  route distance markers. The player still chooses interactions and Confirm.
- World destinations filtered by the area reachable with the current transport,
  region announcements and world-map footsteps.
- Distinct NPC, interactable and exit proximity sounds using Steam Audio HRTF.
  Headphones provide directional and elevation information.
- Chocobo Hot and Cold reading of the shown timer, cry meaning, displayed depth
  and results. The player moves and digs normally.
- **608 recorded audio descriptions** in the creator's personal narrator voice:
  284 cues for all 42 reviewed original FMVs, 212 generic field captions, and
  112 additional Disc 1 recordings. No online account or synthesis is needed
  while playing. Original music, dialogue and movie audio keep their own volume.

The Disc 1 additions cover reviewed actions through Burmecia. Coordinated
rescues, some prop interactions and optional scenes remain gaps. Full Disc 1
coverage, a complete playthrough, physical controller operation and all-cue
listening/synchronization have **not** been verified. See
[narration coverage](docs/accessibility/audio-descriptions.md) and
[validation](docs/accessibility/audio-descriptions-validation.md).

## Installation

Supported target: **64-bit Windows 10 or Windows 11**, **Steam app 377840,
build 5378074**. Both the game's x86 and x64 runtimes are included. The package
supplies its matching Memoria scripts, data, shaders and community native
support. It requires Microsoft **.NET Framework 4.8 or newer** and the
**Visual C++ v14 runtimes 14.51 or newer, x86 and x64**. The manager declares
these prerequisites. Standalone users can obtain them from Microsoft's
[C++ runtime downloads](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170)
and [.NET Framework 4.8 download](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48).
The release check verifies the original executables and Unity references;
different game builds require a compatibility review.

Close FFIX and its launcher before installation or updates. In Amethyst's Mod
Manager, enable the Buu420 source, refresh, select Final Fantasy IX and choose
Echos of Gaia's beta release. Its pre-install script reads the supported game
and package; ordinary manager file actions handle replacements and backups.
Use the manager to update or uninstall a manager installation.

For a standalone installation:

1. Download and extract EchosOfGaia-v0.1.13-beta-windows.zip into a separate
   folder outside the game directory.
2. Open **Install-EchosOfGaia.exe** from the extracted folder. Check the detected
   game folder, or use **Browse** or the editable field to choose the folder
   containing the game's `x86` and `x64` folders.
3. Choose **Install** and wait for **Echos of Gaia is installed**. Installation
   can take several minutes. Tab moves between the folder, buttons, read-only
   status and log; the installer remains open for errors and completion. Keep
   the backup manifest path in the log and extracted release for rollback.
4. Start your screen reader, then launch the game normally through Steam.
   The launcher retains the Memoria name; its **Accessibility** tab contains
   bindings, narration and positional-sound settings.

The installer runs the existing transactional script in the background. If it
reports access denied, close it and use Windows' **Run as administrator** on
Install-EchosOfGaia.exe. Saved installer logs are under
`%LOCALAPPDATA%\EchosOfGaia\Installer`; **Open log** opens the current log.

The standalone installer stores recoverable originals under
%LOCALAPPDATA%\EchosOfGaia\Backups. Close the game, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Restore-EchosOfGaia.ps1 -GameFolder 'YOUR GAME FOLDER' -BackupManifest 'THE PRINTED BACKUP-MANIFEST.JSON'
```

It preserves files changed after installation and reports conflicts for review.
Saves, existing preferences, original Unity executables, movies and unrelated
mod folders stay outside the replacement list. The supplied engine files
replace the corresponding Memoria files, with originals backed up. Other mods
that replace the same managed assemblies need compatibility testing. Modified
FMVs can fail the narration's original-footage hash checks. The stock Memoria
updater is disabled for this build; install Echos updates through its releases
or the manager.

## Controls

| Key | Action |
| --- | --- |
| U / O | Previous / next navigation category |
| K / semicolon | Previous / next target |
| L | Location, region, transport, selected target and route status |
| I | Toggle spoken guidance |
| P | Start or stop automatic walking/travel |
| R | Repeat the last accepted speech |
| Shift+I / Shift+P | Decrease / increase guidance interval |

Hold either L2/LT or R2/RT for Blind Soldier's navigation controls: D-pad
left/right changes category, up/down selects a target, L3 starts spoken guidance,
R3 starts automatic travel, and Circle/B stops. Release the trigger after choosing.
If a trigger is assigned to a core game action, use the other trigger or the
keyboard navigation controls.

Controller navigation uses the held-trigger controls. L3 and R3 are stick clicks.
In Hot and Cold, R3 reads status; its status button can be changed to L3 in the
launcher. Existing native battle controls retain their normal meaning.

**Continue loads the automatic checkpoint.** Use **Load** for a manual save;
testing New Game can replace the automatic checkpoint. Full controls and
behavior are in [navigation controls](docs/accessibility/navigation-controls.md),
[Manual navigation](docs/accessibility/manual-navigation.md),
[Story guidance](docs/accessibility/story-navigation.md) and
[Hot and Cold](docs/accessibility/chocobo-hot-and-cold.md).

## Development and reports

The source is based on BlindGuyNW/Memoria's
feature/screen-reader-accessibility at
0b5f29fcded398e1ffbc2e09ed55b1117af8a7f7. The public repository begins with
a reviewed release snapshot. Private research recordings, human voice
references, provider projects and keys are excluded.

[Build and transactional installation instructions](docs/accessibility/install-and-rollback.md)
describe the source-matched build. The public CI runs source regression tests;
building the actual game assemblies also requires local references from a
legitimate FFIX installation and Windows desktop build tools.

[Report an issue](https://github.com/buu420/echos-of-gaia/issues) with the game
location, target or control used, expected result and what was spoken.
Diagnostics are under %LOCALAPPDATA%\FF9Access. Review logs before sharing;
they can include local paths and gameplay text.

## Credits and licenses

Memoria: Albeoris and contributors. Original accessibility fork: BlindGuyNW
and contributors. Echos of Gaia accessibility development, narration and
maintenance: buu420. Navigation and sound work draws on Blind Soldier and
Foresight. ViddyScribe supplied text-only scene research; accepted descriptions
were checked against installed assets and video. Fish Audio generated the
creator-authorized personal narrator recordings. Descript supported earlier
FMV review. No provider connection is required for playback.

The code retains Memoria's [MIT license](LICENSE). Third-party software and
supplied sound assets retain their respective notices and provenance; see
[third-party notices](native/memoria-support/THIRD-PARTY-NOTICES.md),
[speech notices](native/speech/README.md),
[Steam Audio](native/spatial/LICENSE.md) and the
[original Memoria overview](docs/upstream/Memoria-README.md).
Final Fantasy IX belongs to Square Enix. This is an independent community mod.
