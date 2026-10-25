# Echos of Gaia public beta release plan

> **For agentic workers:** Use executing-plans for the release, with independent package and publication review before publishing.

**Goal:** Publish Echos of Gaia 0.1.0 beta to buu420/echos-of-gaia and the existing buu420 Amethyst Mod Manager catalog.

**Architecture:** Preserve the reviewed navigation and narration runtime. Complete the explicit package with its source-matched Memoria support. Amethyst installs every runtime file through its ordinary file actions and receipts, preceded by a read-only compatibility/package check. The standalone ZIP uses the existing transactional installer.

**Tech stack:** Windows PowerShell, Windows MSBuild, .NET 8 verification harnesses, Amethyst Author CLI 0.30.0, GitHub CLI.

**Spec:** User request to release the installed mod on GitHub and Amethyst under the exact name Echos of Gaia.

## Global constraints

- Exact product spelling: Echos of Gaia. Initial version: 0.1.0, beta channel.
- Preserve the internal ff9-access-local identity, navigation bindings, approved narration and original movie audio.
- Preserve saves, settings and unrelated mod directories. Archive every replaced engine file before writing.
- Include source-matched required scripts/data/shaders and native support; exclude original Unity executables, UnityEngine/firstpass, game movies and private generation inputs.
- Publish selective Disc 1 coverage and pending live gameplay/controller/listening checks accurately.
- Retain Memoria/BlindGuyNW, Unity UI, speech, spatial, audio and launcher dependency notices.
- Use a clean release-source snapshot with upstream attribution; private review history does not enter the public repository.

## Review focus

- Fresh Steam installation lacks Memoria support: the package must contain all required support and validate its architecture/imports.
- Manager updates skip uninstall: all runtime writes must be manager-owned actions, with no scripted deployment or settings mutation.
- Interrupted manual installation: the existing external recoverable journal and recovery tools must remain available.
- Another mod changes an installed file: uninstall must preserve the change and report recovery instructions.
- Publication replaces unrelated catalog entries: compare every pre-existing game/release after the update and verify anonymous asset hashes.

## Tasks

- [ ] Complete explicit baseline packaging and dependency notices; verify missing/tampered support rejection.
- [ ] Add read-only manager preflight and manual entrypoints; verify compatibility rejection and package action closure, and rerun the existing transactional recovery checks.
- [ ] Add public README, controls, compatibility, attribution, beta limitations and release notes; replace inherited stock patcher workflows with source verification.
- [ ] Review the public source and exact archives; build both architectures, run appropriate core/native/launcher/package checks and verify preservation on deployment.
- [ ] Publish the source and GitHub beta assets, add the manager game/release entry without changing existing entries, and verify public downloads/catalog state.
