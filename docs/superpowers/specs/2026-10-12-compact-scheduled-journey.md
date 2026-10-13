# Compact scheduled journey

The user requested these changes directly: current journeys must follow scheduled
runs, reduce the card height, move Fast mode to Settings, reopen quest details at
the top, indicate enabled schedules below Scheduler, and remove the sidebar
progress note. These directions define the approved scope.

Scheduler owns the journey from launch through preparation, running and logout.
Its card must never inherit the reason or next quest from an unrelated stopped
queue. Saved pending actions still show their reconciliation state. Selection and
character isolation stay intact. Without a started run the card remains idle.

Use the shared dark palette and existing typography. The expanded card is 180 px
in regular windows and 200 px in narrow windows, with bounded scrolling for long
messages and queues. Keep queue controls and the folding animation.
The character sprite keeps its existing 112 by 112 display area and renderer;
the shorter card must not shrink it. Text scrolls independently beside it.
Fast mode appears under Movement in Settings, remains character-specific and saves
immediately. A small colored dot and label below Scheduler describes unfinished
schedules globally: Enabled, Running, Paused or Needs attention. This is a status
indicator and never enables scheduling by itself.

Opening the details expander or selecting another quest resets its scroll offset.
Routine progress refreshes must preserve the reader's position. Remove the
sidebar progress note and redundant queue add button; library checkboxes and
quest details continue to add quests.

Validate ownership, phase and indicator behavior with synthetic regressions, run
both complete xUnit projects, and manually review native preview captures at
regular and minimum sizes. No real characters or Excalibur processes are used.
