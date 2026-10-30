# Bag sorting implementation plan

The user approved the accompanying design and category mode.

1. Adapt DAOrganizer's pure category, special-slot and swap planning rules into
   Core, retaining MIT attribution. Verify complete item conservation, duplicate
   identities, full bags and exact source layouts with synthetic fixtures.
2. Add one `sort-bag` GameSession command. Keep its action semaphore throughout;
   capture manual-input guards before waiting for ownership, validate under the
   transport lock and before write, and await complete authoritative swaps.
3. Add DesktopRuntime operations using existing control ownership. Freeze target
   identities, reject conflicting owners and stale connections, skip unavailable
   targets and cooperate with Stop/Shutdown.
4. Use a compact ordinary button, as amended by the user on October 29.
   Wire selected-character/all-character scope and concise inline outcomes into
   the existing dark toolbar. Capture targets at activation and keep command
   concurrency, eligibility and runtime ownership checks.
5. Run focused tests, review isolated synthetic toolbar renders at normal and
   minimum sizes, then run both complete xUnit projects. Record limits clearly;
   no live character qualification is part of this implementation.

Work is split across planner, protocol and runtime owners; the main agent owns
the button, view model, toolbar, integration and final review.
