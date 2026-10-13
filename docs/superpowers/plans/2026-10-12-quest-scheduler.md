# Quest scheduler implementation plan

The user approved the design in the matching spec. Writing-plans is unavailable;
this plan records the implementation and verification steps directly.

- [ ] Add failing scheduler regression tests for default-off, acknowledged arming,
  due dispatch, durable attention blocks, cooldowns, restart and cancellation.
- [ ] Implement atomic scheduler storage, due policy and a serial coordinator with
  injected clock and execution callback for synthetic tests.
- [ ] Add owned-login response observation and refusal propagation; test responses
  and connection isolation without logging credentials.
- [ ] Add quest-run scheduling boundaries for flower/altar waits; keep ordinary
  runs unchanged and test verified checkpoints rather than elapsed completion.
- [ ] Integrate runtime launch/ownership, client cleanup, helper refusal handling,
  stop-all, shutdown and update safety with scheduler reservations.
- [ ] Add the Scheduler button and dark popup with mandatory acknowledgement,
  schedule choices, next run times, pause/remove/re-enable and prominent warnings.
- [ ] Run focused tests, both full xUnit projects and an isolated UI review. Update
  getting-started documentation. Never operate real game characters or Excalibur.
