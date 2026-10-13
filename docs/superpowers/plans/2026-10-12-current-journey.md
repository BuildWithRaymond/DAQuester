# Current journey implementation

1. Add regression tests that reproduce library selection appearing as a journey.
2. Bind the card only to current run ownership; render idle without a run.
3. Remove individual planner commands and controls, retaining queue controls.
4. Move selected quest planning actions into Quest details, retaining choice locks.
5. Preserve Scheduler ownership during launch/run/cleanup and character isolation.
6. Update current documentation and remove unused individual action styles.
7. Run focused tests and both complete xUnit projects; inspect native preview
   captures of idle, paused and minimum-size planner states.
