# Shortest recorded travel routes

The player reports long detours between maps and requests the fastest travel
available, with a comparison against Excalibur. Preserve the preceding fast-mode
feature and all quest evidence, cancellation and recovery rules.

## Compared behavior and cause

Read-only Excalibur commit `5143a1a63f5a3e427ed943ef641ce19228560327`
uses breadth-first map search in `AutoWalkRouteService.FindRoute`.
`Client.AutoWalk.FindAutoWalkPath` then chooses the recorded `ConnectedTo` point
for the next map. Tile walking uses collision-aware paths and blocks unintended
portals. Neither map search nor DAQuester's current search prices total walking.
DAQuester also discards other entrances to an already-visited map, and selects
the first door in file order. A one-map route can therefore walk much farther
than a route through two nearby doors. Current same-map transfer selection only
considers shortcuts when direct walking is impossible.

## Design

Plan over separate `(map, arrival tile)` states. Price edges with actual shortest
collision-safe walking distances to each recorded portal or directed same-map
transfer. Include the last walk when the command provides a target tile. Prefer
fewer transitions when walking costs tie. Count world-map selection as an extra
action; ordinary doorway entry already costs its walking step. This minimizes
known movement/action cost rather than claiming knowledge of future network
latency, unseen players or unrecorded transport abilities.

Distance searches stop at portal and transfer trigger tiles instead of walking
through them. Only recorded portal endpoints receive the existing wall-flag
exception. Record distinct arrivals even when they reach the same map. Compare
direct walking with shorter directed transfers, including transfer chains. Do
not create reverse transfers or arbitrary wall crossings.
Once the destination map is reached, final approach stays in that map so route
optimization cannot replace authored quest patrol coverage with an excursion.

Cache static distance fields and reprice current-map occupancy separately.
Replan after authoritative map/position changes and when blockers change while
walking toward a door. Preserve bounded refresh recovery if all available exits
are temporarily occupied. Missing collision data must never authorize walking.

When the world map opens, rank its actual available nodes using recorded landing
tiles for the observed entrance and remaining walking costs. Use only live node
coordinates/checksums for the selection packet. If the entrance/landing cannot
be established, retain conservative graph selection and replan from the actual
server arrival instead of inventing a position.

NPC approach chooses the closest reachable interaction tile by walking distance
within the existing allowed range; being in range already requires zero steps.
NPC IDs and positions remain server-observed. No static NPC position is invented
to price travel before entering its map.

## Validation

Failing synthetic regressions demonstrate first-door detours, shorter routes
with more map changes, distinct arrivals into one map, final-target costs, walls,
directed transfers, unexpected portals and NPC range. Exercise loopback map
transitions, world selection fields, occupancy replanning and cancellation.
Compare representative installed collision routes offline without operating a
character. Run focused tests, all automated tests, the Release build and native
Test-UI. No Excalibur working copy or process is changed.
