# Quest library and compact character information implementation plan

The matching design was approved by the user. Writing-plans is unavailable in
the installed skill catalog; this file records the implementation steps.

1. Preserve local work in a stash, merge origin/main and resolve overlapping
   scheduler/quest/transport changes without discarding either feature set.
2. Add failing regression coverage for Available as default, permanent completion,
   guide separation, independent legend filtering and mixed scheduler status.
3. Implement catalog legend metadata, combined library predicates and counts.
4. Replace scheduler navigation caption with an accessible colored dot; compact
   saved schedule rows and move secondary actions into their context menu.
5. Present observed character legends as bounded wrapping chips and tighten the
   account cards while preserving editing, login and launch controls.
6. Verify new upstream quest acknowledgements also apply to scheduled quests.
7. Run focused tests, both full projects and Release build. Inspect isolated
   native Avalonia previews and resolve any clipping or confusing states.
