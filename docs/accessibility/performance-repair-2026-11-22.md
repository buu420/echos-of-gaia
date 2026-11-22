# Castle autosave performance repair

The supplied autosave loads Zidane in Alexandria Castle's Hallway, Area 2 (field 1215, scenario 5050), with about 18 minutes 21 seconds remaining. This is distinct from the earlier Steiner reports. The main save and its matching Memoria autosave companion were decoded and loaded together; the user's original profile was backed up before testing.

## Cause and changes

The three native exit regions present 490, 445 and 308 approach points. `FieldExitNames.Parts` repeatedly scanned those arrays inside membership checks and sort comparisons. `NavigationPoint` used the runtime's reflective value-type equality, boxing coordinates at every comparison. In the actual game, stationary exit naming took roughly 330 milliseconds per refresh and contributed eight garbage collections per slow rendered frame.

- Implement typed point equality with the installed Mono runtime's existing coordinate semantics. Normalize signed zero in hashing: that runtime considers positive and negative zero equal but gives their raw floats different hashes. Equal-point Story proof lookups now reuse the same cached proof instead of recomputing it for opposite signed zeros; the existing memory and scope invalidation remains in force.
- Index the current approved approaches once per exit. Preserve the first occurrence's rank, duplicate removal, destination-specific points, menu offerings, names and existing fallback rules. No candidates are capped or removed for performance.
- Share positive, live native component lookups within one rendered frame and navigation generation. Missing or destroyed objects are looked up again. The cache holds object identities only; control, input, actor, dialogue and menu fields are read live on every call. It never invokes a singleton accessor that can create a game object.
- Keep single-architecture build selections as arrays so `x64` does not become the character `x` when choosing script references.

## Evidence

Private in-process timing builds measured the actual supplied save. Exit naming fell from approximately 330 milliseconds to approximately 0.7 milliseconds per refresh. The repaired diagnostic build sustained roughly 47-50 FPS in the stationary castle scene. Diagnostic scopes overlap, so their totals must not be added together. All temporary timing code was removed from the final build.

A separate .NET 8 allocation regression reproduces the three castle-sized exits. Three refreshes allocated about 1.91 GB before the change and about 1.39 MB afterward. This is a portable allocation measurement, distinct from the in-game timing result. Assertions retain every approach in order, destination ownership and the readable labels.

Verification covers 1,035 portable checks, both architecture builds, emitted input/field interruption/exit/Story/Forest checks, and 20 original-controller route replays across five fields at walking and running speeds in the installed x86 and x64 Mono runtimes. Separate native-runtime probes verify NaN inequality and signed-zero equality/hash membership. Ghidra inspected the original field 1215 BGI (293 triangles across five floors), rather than replacing its native geometry. A read-only Claude Teammate review found no blocking defect.

This establishes the reproduced castle stall and the listed regression coverage. It does not establish a complete game playthrough, the tester's unknown hardware/settings, listener-confirmed speech or live controller testing. Private saves and captures are excluded from the source and release packages. Test configuration and the original local saves are restored after the live checks.

The generic equality mechanism is documented in [Microsoft's collection comparison reference](https://learn.microsoft.com/en-us/dotnet/standard/collections/comparisons-and-sorts-within-collections); the installed Mono edge cases above were measured separately.
