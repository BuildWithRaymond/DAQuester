# Research Exkuranum implementation

1. Add failing synthetic regressions for supply, immediate healing and retained
   research status. Confirm failures before implementation.
2. Add task-local purchase and healing checkpoints; keep research Pending intact.
3. Prepare 30 potions via Blackstar Research using current merchant receipts.
4. Wake research receipt waits on HP loss, heal from actual own health, and retain
   result evidence across potion messages and saved recovery.
5. Update isolated fixtures and current documentation for the approved scope.
6. Run focused research/merchant tests, both complete suites and the app build;
   review the final diff without changing unrelated work.
