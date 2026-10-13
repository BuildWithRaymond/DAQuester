# Quest scheduler implementation plan

The user approved the design in the matching spec. Writing-plans is unavailable;
this plan records the implementation and verification steps directly.

- [x] Add failing scheduler regression tests for default-off, acknowledged arming,
  due dispatch, durable attention blocks, cooldowns, restart and cancellation.
- [x] Implement atomic scheduler storage, due policy and a serial coordinator with
  injected clock and execution callback for synthetic tests.
- [x] Add owned-login response observation and refusal propagation; test responses
  and connection isolation without logging credentials.
- [x] Add quest-run scheduling boundaries for flower/altar waits; keep ordinary
  runs unchanged and test verified checkpoints rather than elapsed completion.
- [x] Integrate runtime launch/ownership, client cleanup, helper refusal handling,
  stop-all, shutdown and update safety with scheduler reservations.
- [x] Add the Scheduler button and dark popup with mandatory acknowledgement,
  schedule choices, next run times, pause/remove/re-enable and prominent warnings.
- [x] Run focused tests, both full xUnit projects and an isolated UI review. Update
  getting-started documentation. Never operate real game characters or Excalibur.

Validation: 852 Core tests and 128 desktop tests passed. Scheduler regressions
include persistent login refusals, interrupted helper logins, timed progress,
cleanup after multiple helper failures, pausing during logout and cancellation
when scheduler storage is locked. Native previews were inspected at full and
minimum popup sizes using isolated synthetic data. No live characters were used.
