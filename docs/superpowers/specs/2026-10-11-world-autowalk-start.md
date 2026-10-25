# FFIX world autowalk startup

The user reports a long pause before autowalk, asks to compare Blind Soldier's implementation, and authorizes adapting that approach. The installed 4b134dc session records P at 16:08:50.599Z, search queued at 16:08:52.822Z, and movement started at 16:08:55.997Z. Preparation and search both contribute to the 5.4-second wait.

Blind Soldier retains its native map/triangle index and searches connected triangles. FFIX must retain its own native terrain masks, cache priority, ordered first hit, layered heights, wrap coordinates, special mesh rejection, and unwanted-entrance restrictions. Reuse the caching and indexing approach; do not substitute FFVII terrain rules.

Build a spatial candidate index that preserves native surface order. Retain exact native probes and every existing segment validation. Prepare detached world topology on one bounded background worker ahead of P, copying at most 24 native blocks per frame on the Unity thread. Reuse immutable topology within the same native world/data identity; sample fresh transport rules, scene generation and native cache for each request. Replace data when the world instance, disc, forms, readiness or mesh data identity changes. Stale or cancelled preparations never supply a route.

Use .NET 3.5-compatible code on both original Mono runtimes. Unity object access remains on the game thread. Keep K/L/semicolon and all existing speech, launcher, input ownership, manual takeover and lifecycle behavior. No Confirm, boarding, landing, combat, save or game-state automation is added.

Acceptance: demonstrate old/new lookup and route timings; verify native hit-order equivalence, boundary tolerance, wrap seams and overlapping layers; verify preparation ownership/cancellation/replacement and fresh context/rule/cache views; build both architectures; run emitted native checks and both original-Mono probes; create and install a backed-up package; measure a live route request where UI access permits. Clearly distinguish synthetic measurements from live gameplay.
