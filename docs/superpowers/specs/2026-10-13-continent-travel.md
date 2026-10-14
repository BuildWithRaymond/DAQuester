# Medenia and Temuair travel

Extend the existing route graph with directed NPC transfers, preserving walking
portals, world-map selection, generation isolation and ambiguous-action recovery.
The requested example is Noam Village to Mileth Village.

Read-only Excalibur source identifies Habab's Carpet Merchant in Noam as the
return to Tagor Inn. The outward route uses Quard's Express Ship in Loures Harbor
Shop to Asilon Harbor, then Lenoa's Caravan to Noam to Caravan Road. Existing
recorded walking and world-map edges connect the remaining legs.
Habab's Caravan to Asilon or Hwarone connects Noam to Caravan Road for local
Medenia trips, preventing a detour through Temuair. When an existing walking and
world-map route connects the endpoints, it retains priority over paid transfers.

NPC edges carry an interaction anchor, service label and estimated planning
arrival. Estimates rank routes only; they never prove arrival. Execution finds
the current named NPC, approaches a reachable tile, selects unique live service
and destination choices, and advances fresh owned Next pages. No pursuit IDs,
option numbers or live landing coordinates are imported from Excalibur.

Unknown menus, missing NPCs, insufficient observed gold and missing replies pause
travel. A submitted transfer remains pending until authoritative arrival in its
expected map and position; resumption must not submit it twice. Existing NPC
dialogs are preserved, including when a new GameSession has no pending state.

Alternatives considered: copying Excalibur's fixed packet sequence would discard
observed-dialog boundaries; special-casing Noam only in execution would leave
planning and world-map ranking unaware of the connection. Explicit NPC edges
serve both planning and execution with a small extension to existing types.

Validation uses packaged collision route regressions and loopback synthetic
transport fixtures, followed by both complete xUnit projects. No Excalibur
working copy is modified and no real character is operated.

## Verification

- The Release solution build succeeded with no errors. Its one NU1900 warning
  reports unavailable NuGet vulnerability metadata because the package service
  could not be reached.
- All 74 focused travel checks passed, including Noam Village, Noam Inn and Noam
  Plains to Mileth, the outbound ship/caravan chain and Noam's local caravan.
- The complete core suite passed: 1,007 tests, none skipped. It ran outside the
  sandbox with `xUnit.ParallelizeTestCollections=false` and
  `xUnit.MaxParallelThreads=1` using the documented
  [xUnit RunSettings options](https://xunit.net/docs/config-runsettings).
- The complete app suite passed: 179 tests, none skipped.
- The sandbox denied the isolated test pipe connection. That test passed outside
  the sandbox. A parallel core run exposed two existing timer-dependent obstacle
  assertions; both passed isolated and in the complete serialized run. Their
  tests and production movement code were not changed for this task.
- The built executable exists and its bundled route data matches the source.
  Live travel was not tested; Excalibur and real characters were untouched.
