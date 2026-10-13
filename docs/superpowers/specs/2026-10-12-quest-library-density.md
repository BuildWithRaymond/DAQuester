# Quest library and compact character information

Approved by the user on 2026-10-12.

The library defaults to Available, showing quests eligible for queue planning,
including offline planning and saved choices. Completed shows terminal goals,
not ordinary repeatable attempts. Guides shows entries without an automatic
recipe. All quests is last and remains opt-in. Search combines with every filter.
An independent Has legend marks toggle uses catalog givesLegend evidence to
restrict any category to quests offering legend rewards. Off includes both.
Counts follow the legend restriction, while search limits the visible rows.

Scheduler navigation always shows one status dot: red when scheduling is off,
yellow when any schedule is paused, needs attention or has a login block, and
green when scheduling is enabled without those blockers. Mixed healthy and
blocked schedules stay yellow. Tooltip and accessible help explain the state.
The dot remains visible in the compact navigation rail. No Enabled caption.

Saved schedules become dense rows with the existing game portrait, character,
quest, local date/time, recurrence and state. Secondary actions move to a menu;
failure explanations remain visible. Narrow windows stack the two panes.
Character cards keep account editing and launch controls, with a separately
labeled legend area containing wrapping chips and bounded vertical scrolling.
Observed and remembered legends retain their provenance, including unknown
and observed-empty states. Full text remains available in tooltips.

Retain the shared dark theme, Georgia headings, Segoe UI copy and original DAT
portraits. Preserve upstream quests, hunting and recording, plus local scheduler
ownership, checkpoint and connection rules. Never launch game clients for tests.

Validation: focused behavior tests, both complete xUnit projects, Release build,
and manual inspection of isolated Avalonia preview captures at full and compact
sizes. No packaging changes are included.
