# Opening Story travel

The ticket objective first leads through Alexandria's streets to the square.
Each travel target names the next area and binds an exit in the current map.
It does not send the local walking planner to a coordinate in another map.
Story remains selected across transitions; walking the next leg still requires
the normal deliberate automatic-walk command.

The booth is a stand in the square. Its visible TICKET sign, original backdrop,
current camera/rendered overlay, floor, native region and player/director
scripts now bind its front interaction region before a bubble has appeared.
Approach the booth, press ordinary Confirm and use the native dialogue choices.
The close-up booth room records ticket completion. After the game records that
step, Story selects the real alley exit from the square.

If a usable local exit or approach cannot be verified, Story states the next
area or explains that the objective is in the current area. Starting walking
or directions on an informational step reads that explanation without
scheduling a geometry plan. Native input, Confirm, choices and clocks remain
unchanged.

## Evidence and validation

The original street functions include a pure angle calculation used to center
a visual transition. The detached interpreter retains that value as unknown;
discarding it no longer hides a later constant field jump. A branch or shadow
variable depending on the unknown angle stays unknown, and original buffers
remain unchanged.

Seventeen freshly decoded native functions replace the earlier invented
square-to-booth exit fixture. The real square's exit25 leads to110, and its
alley exit is22. Entering the booth uses region28 and player30's own script;
it is not an ordinary walking exit. The physical navigation goal is square103,
while native completion remains in room104.

Tests cover actual street scripts with both wipe modes, wrong-way exits, the
item-shop detour, map/category retention, next-area fallback, informational
walking/direction commands, changed or hidden booth evidence and the native
alley step after ticket completion. An independently decoded151-triangle
square fixture includes the original planes, normals, neighbors and edge
clones. It supplies a clipped route from the incoming street region to the
booth at several clearance radii. Native bridge fixtures and emitted-assembly
checks cover the actual integration boundaries.

Artwork fingerprints and original summaries are recorded in
`opening-ticket-booth.json`. The independent [Alexandria guide](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891/alexandria)
supplies expected story order; current native evidence supplies presentation
and walking approaches. No guide file is loaded by the game.

These are offline/source and controlled-boundary checks. Actual live rendering,
dynamic NPC obstacles, controller/NVDA delivery and a complete opening
playthrough remain player tests. No gameplay input or game launch was automated.
