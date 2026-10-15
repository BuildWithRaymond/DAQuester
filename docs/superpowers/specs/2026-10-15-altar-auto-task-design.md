# Altar Worship placement

The player requested Altar Worship under Auto Tasks for everyone, and in both Auto Tasks and Quest planner for Priest, Bard and Master Priest characters. Unknown classes are shown the task only until a known class supports quest placement. Known saved class remains usable while offline.

Keep the same recipe ID, journal, offering receipts, queue membership and saved schedules. Add optional quest-class visibility metadata to task recipes, preserving task-only recipes and ordinary quests by default. Altar retains its existing quest artwork. Changing character or receiving fresh class state updates the library and clears a hidden Altar selection without altering its progress or queue.

The existing Altar priest-family predicate must recognize Priest, Bard and Master Priest consistently for placement, reward descriptions, no-mark acknowledgement and final legend verification. This is a player-supplied class clarification; do not invent a new reward receipt. Completion still requires the existing actual legend or item proof.

Task details show Altar supplies, ten-offering progress, three-hour next-offering time, class-specific rewards and existing schedule state. A paid offering wait stays a continuation, not a repeat cooldown or completion. Existing non-priest reward acknowledgement remains required.

Implementation: write failing placement/reward regressions; add metadata and visibility filtering; add Altar task details; verify focused tests, both full xUnit projects, a separate Release build and native full/compact layouts. Use synthetic fixtures only. No real characters, player schedules or Excalibur processes are changed.

Self-review: this records the player's final clarified design. No new task runner, duplicated progress, healing or grouping is included. The writing-plans skill is unavailable; the implementation sequence is recorded above.
