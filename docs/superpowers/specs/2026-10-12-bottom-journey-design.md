# Current journey below queue controls

Approved in conversation on October 12, 2026: split the workspace roughly 75%
quest library and 25% sidebar, with a minimum sidebar width of 280 pixels. The
latest direction places the journey below the queue's Run/Pause and Stop buttons.

The library uses the full workspace height. Its list and open quest details have
bounded scrolling at the minimum window size. The sidebar contains the queue
heading, scrolling queue list, readiness text, Run/Pause and Stop controls, then
the current journey. Expanding the journey changes only sidebar allocation.

Start expanded so players see the character sprite, and persist their choice in
application settings. The sprite remains 112 by 112, anchored at the lower right
of the expanded panel. Journey status, message, progress, next action and rewards
scroll beside it. The expanded panel is 280 pixels high, or 180 pixels in short
windows. Collapse folds only journey details into a 64-pixel bar; queue controls
remain visible. Labels name the current journey alone. The collapsed chevron
points up and the expanded chevron points down. Keep the shared dark theme,
artwork, reversible animation, keyboard focus and reduced-motion behavior.

Preserve queue ownership, command gating, observed progress and checkpoints.
Verify preference persistence and planner/journey behavior, run both complete
xUnit projects, and manually review isolated native normal/minimum-size captures
with expanded and collapsed journeys. No game characters or Excalibur working
copies are involved.
