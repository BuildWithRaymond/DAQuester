# Changelog

## 1.0.2 — 2026-10-10

- Fix stale button corners and text escaping partial repaint regions. Unify
  native dark button, checkbox and card rendering with Fluent 2 foundations,
  clear interaction/focus states, consistent spacing and interface typography.
- Add native visual regression checks for clipping, repeated repaints, keyboard
  actions and scaled control geometry alongside standard/compact dialog renders.
- Add optional fast walking per character: Excalibur's 160 ms pace when alone,
  returning to 430 ms around other players. Changes apply while quests run.
- Match Excalibur's immediate fast-walk presentation and bounded movement queue
  during quest travel. Show the current pace; retain authoritative recovery and
  drain pending steps before doors, inventory goals and further actions.
- Choose travel routes by collision-safe walking distance, comparing every door,
  arrival point, world-map landing and directed shortcut. Replan when blockers or
  authoritative positions change; avoid extra NPC approach steps already in range.

## 1.0.1 — 2026-10-09

- Include the full Velopack MIT notice and exact .NET runtime licenses/notices in
  installer, portable and update packages.
- Require these files in package verification. Quest and update behavior is unchanged.

## 1.0.0 — 2026-10-09

- First public open-source release of DAQuester.
- Fully dark midnight quest journal, custom window chrome and dark native controls.
- Player-first character roster, quest stages, rewards and conversations.
- Searchable library of 70 legend quests and four executable recipes.
- Optional Developer mode for calibration, recording, diagnostics and profiles.
- Optional startup update checks, explicit downloads and safe-exit installation.
- Stable Windows user data storage with non-destructive legacy migration.
- Self-contained x86 installer, portable package and GitHub update feed.
- Automated protocol, recovery, update policy and native UI validation.
