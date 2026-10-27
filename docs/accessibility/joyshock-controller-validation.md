# JoyShock stick-click and trigger separation

Validated offline on 2026-10-26. `GamePad.GetState` previously mapped JoyShock
`ZL`/`ZR` (L2/R2) to XInput stick clicks and ignored `LClick`/`RClick` (L3/R3).
The merge now uses the actual click bits. Trigger pressure remains independent.

## Evidence

The [official header](https://github.com/JibbSmart/JoyShockLibrary/blob/023dfb6f83d27134bf0cb0be085bfab4a251ffa5/JoyShockLibrary/JoyShockLibrary.h)
defines click masks `0x40`/`0x80` and trigger masks `0x400`/`0x800`.
The [official input decoder](https://github.com/JibbSmart/JoyShockLibrary/blob/023dfb6f83d27134bf0cb0be085bfab4a251ffa5/JoyShockLibrary/InputHelpers.cpp)
keeps those inputs separate. These sources establish the published mask meaning;
they do not establish the installed binary's build provenance.

Ghidra 12.1.2 analyzed the exact installed
`x64\FF9_Data\Plugins\JoyShockLibrary.dll` in a fresh private project:

- Size: 245248 bytes; SHA-256:
  `81A1095BC2E61CE61CED0C2D3F4CA00E37653437AAEEB266B46FCA29B7B795ED`.
- Architecture: `x86:LE:64:default`; preferred image base `0x180000000`.
- `JslGetSimpleState` at `0x18000D4A0` copies 28 bytes from device offset `0x6C`.
  `JslGetButtons` at `0x18000C590` reads the first four bytes there. Left/right
  trigger getters at `0x18000CEF0`/`0x18000D2C0` read offsets `0x70`/`0x74`.
- The packet-decoding function at `0x1800077C0` preserves packet-byte bits
  `0x40`/`0x80` as those same button bits. It separately stores trigger pressure
  and ORs `0x400`/`0x800` when the corresponding pressure is positive.

These are static decompiler observations from the installed binary. Identifying
that decoder branch as the DualShock 4 path uses its packet-layout agreement with
the official source. Neither observation proves live device delivery or speech.

Private evidence, scripts, pinned source copies, and logs are retained under
`D:\FF9Access\research\2026-10-26-controller-menu\joyshock`:
`ghidra-state-audit.txt`, `ghidra-state-writers.txt`, and the Ghidra console logs.

## Change and automated validation

The original per-device merge was extracted into the production
`GamePad.MergeJoyShockState` method. A comparison against the retained original
block confirms identical behavior except for the two approved mask corrections.
Polling, device selection, packet number, existing button merges, additive axis
values, pressure quantization, clamping, and dead-zone processing remain as before.
No native DLL was replaced.

`JoyShockInputTests` exercises the real merge with literal native masks and checks
the resulting public button/trigger state. Before the correction,
`LeftStickClickUsesJoyShockBitSix` failed with `Expected Pressed, got Released`.
After the correction, all nine tests pass. Coverage includes both clicks, trigger
buttons without false clicks, a click combined with independent trigger pressure,
analog pressure without digital bits, ordinary button mappings, existing XInput
state, additive axes/pressure, and saturation.

Run the focused suite from the source root:

```powershell
dotnet run --project tests/FF9Access.Tests/FF9Access.Tests.csproj -- --suite JoyShockInput
```

Retained logs are `tests-red.log` and `tests-green.log`. The first combined-suite
snapshot stopped at the concurrently developed
`ControllerChordTests.BothTriggerChoicesUseTheSameDirections` test; its failure is
retained in `tests-all-snapshot.log`. That snapshot does not establish a full-suite
pass. The completed controller changes subsequently passed the combined 553-test
core suite, including 17 held-trigger navigation cases and these nine JoyShock
cases. Final staged-build and deployment evidence is retained with the local
controller/menu update research.

No game, physical-controller, audible-screen-reader, Steam-launch, or recipient-PC
validation was performed for this narrow repair. In particular, live L3/R3 and
L2/R2 behavior still needs physical-controller confirmation after deployment.
