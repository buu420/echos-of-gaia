# Field navigation audit and exit cues implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task by task. The user has already authorized implementation and deployment and explicitly requested no repeated approvals.

**Goal:** Audit all installed English fields, repair systemic omissions of ordinary exits and presented actors/objects, and announce proximity with the user's Blind Soldier exit recording.

**Architecture:** Preserve native target identities, live rendering, script guards and ordinary interaction ownership. Bind exits to their walk-out script and actual reachable trigger surface, rather than requiring the remote end of the walkmesh to be in view. Run one reproducible census across every field; record unsupported cases honestly. Exit cues consume the same current catalog as navigation and never reveal an unavailable or hidden target.

**Tech stack:** Existing Memoria C#, original Mono x86/x64, Unity native field/event state, UnityPy read-only asset census, Ghidra native-audio evidence, existing owned WinMM audio output.

## Global constraints

- Develop in D:\FF9Access\source on codex/ffix-accessible-navigation; preserve the established dedicated source checkout and deployment workflow.
- No native script execution, flag writes, Confirm, digging, combat, save changes, or automatic interaction.
- Preserve all existing speech, navigation keys, audio descriptions, personal voice recordings, settings and saves.
- Offline evidence is not live gameplay proof. Every field needs an audit row, not an invented completeness claim.
- Use live native guards and current actor/quad identity. No static reward or future-destination disclosure.
- Cache the small exit recording once. No new game audio engine, process-wide audio stops, polling threads, or asset scans on cue pulses.

## Review focus

- Large exits whose actual trigger starts inside the floor, while their outer edge projects beyond the current viewport.
- Narrow or sloped regions, overlapping regions and collision radius, including false safe approaches.
- Changed branch guards, actor replacement, camera scrolling, map/battle/dialogue transitions and focus loss.
- Invisible backgrounds/actors, unsupported jump patterns, dialogue teleport outcomes and unopened rewards.
- Multiple nearby exits, stacked floors, through-wall distances and muted/disabled audio without interrupting native speech.

### Task 1: Native census and field trigger repair

- [x] Reproduce the Forest omission using captured geometry and the installed entry 20 script; inspect the reconstructed path art.
- [x] Audit every installed English field and each script entry for exit, actor and native-icon patterns; independently check inventory counts with Claude.
- [x] Add failing regressions for missed trigger surfaces and narrow/sloped/overlapping regions.
- [x] Implement bounded candidate generation from real polygon/triangle intersections and native first-region checks; use visible trigger surfaces for established plain walk-outs.
- [x] Fix additional supported systemic target/eligibility findings from the census, with regressions; publish field-level coverage and unsupported cases.

### Task 2: Blind Soldier exit proximity cue

- [x] Verify the source recording and tracker behavior and record provenance/hash.
- [x] Add failing tests for range/pulse, current eligibility, same-floor/clear path, current map, lifecycle and mute gates.
- [x] Implement a pure cadence/spatial decision core and an owned cached audio bridge, using the navigation catalog before Story aliases are appended.
- [x] Add accessible launcher enable/volume settings, package ownership and emitted wiring checks.

### Task 3: Review, build and install

- [ ] Run relevant core/native integration, original Mono and launcher checks for both architectures; review the branch independently and repair actionable findings.
- [ ] Commit final source, build exact HEAD with Build-FF9Access.ps1 -Mode All -Architecture Both, package and validate.
- [ ] With the game closed, snapshot current player files, deploy transactionally, and independently verify installed hashes and preserved player files.
- [ ] Report implemented, offline verified and live-unverified coverage separately.
