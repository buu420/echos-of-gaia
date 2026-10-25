# Story navigation and shared navigation audit

The user wants the navigation repaired across FFIX and a Story category like
Blind Soldier's: follow the main story, with optional people, items and objects
in their appropriate categories. Existing authorization covers implementation
and deployment; the user's instruction to stop asking for approval supersedes
the skill's repeated approval stages.

## Behavior

Story uses the current native story counter, map and available interactions to
offer the next required objective. It must cover the main game through the final
area, including intermediate rooms and world travel. It must not publish every
chapter simultaneously or substitute an optional detour for the required path.
Navigation to a local Story target uses the existing validated physical target;
an objective outside the currently presented area gives useful directions but
does not create an unseen actor or an invented walking connection.

Guide references establish chapter order and context. Installed event scripts,
field/scene tables, current trigger availability and native geometry establish
bindings and progress. Objective wording is original and concise; do not copy
walkthrough prose. Blind Soldier's priority, nearest-milestone, native-condition
and transit coverage are the implementation reference.

People retain visible NPCs and party actors. Items contain presented pickup
interactions; Objects contain other visible interactions and landmarks. Optional
content stays out of Story. No hidden reward, buried treasure, future encounter
or unobserved actor position is spoken. Retained landmarks continue to disclose
last-seen evidence and expire on native replacement or removal.

## Architecture

Keep native reads separate from a detached core Story catalog. An offline audit
of installed scripts records progress milestones, ordinary transitions and
exact script provenance. Runtime reads only current cached progress/presentation
data; no full asset scan, guide access, Thread.Sleep, scene creation or script
execution runs during gameplay. Story wraps an existing physical target with a
separate identity so category selection remains unambiguous. Routing and native
arrival resolve that physical identity, while speech uses the objective label.

The broad audit fixes demonstrated shared errors individually. First correct
ScriptSnapshot's native variable widths and signedness. Add reproductions for
other confirmed selection, category, exit or approach defects before changing
their behavior. Preserve full collision radius, portal and height checks.

## Validation and delivery

Use native-format variable regressions, independently specified main-story
checkpoints and transit coverage, category/selection/physical-target tests,
modified-script rejection and captured field/world route regressions. Validate
both emitted game assemblies in the original x86/x64 Mono runtimes and the
launcher. Obtain a read-only whole-change review and address material findings.

Commit, build All/Both from the exact commit, package and deploy after normal
game closure. Preserve the newest saves, settings, bindings and original game
files. Independently verify installed hashes and record static, runtime and
live-game evidence separately. A full live playthrough is not implied by
offline script coverage.
