# Changelog

## Unreleased

- New quest: **Cured the Sick Child**. Pick one flower a day on the quester, or let
  DAQuester log in your other saved characters to pick flowers at Mehadi, trade
  them over in Paulin's room and log them back out, finishing in one day. Every
  flower is given with the 5,000 coins so it reaches the child. Characters that
  can't pick yet are shown with the time they picked or were refused.
- New quest: **Aided the Pact of Anaman**, the same flower run with Golden Blossoms
  from Mehadi and the same helper option. Each flower goes plainly to Paulin for
  the chief physician, with no gold. Its legend mark is brown and other players
  can't see it; the quest says so before you start. A character's one flower a day
  counts across both quests.
- Cured the Sick Child and Pact of Anaman block each other once either legend mark
  is on a character, since only one of the two can be earned.
- Launch stays available while another character is still logging in. Overlapping
  launches open their game windows right away and type their logins one at a time,
  so they never fight over window focus.
- Fast mode is now on for every character unless you switch it off.
- Your own characters connected in DAQuester no longer count as nearby players, so
  they don't slow each other's fast walking. Other players still do.
- Clicking the ground or your own character in a game window, for example to bring
  it to the front, no longer pauses automation. Clicking an NPC, monster or player
  still does, and the log now names the character and what was clicked.
- Only the game's action messages, such as "You received 20,000 gold coins.", are
  kept in replays and the diagnostics log. Chat, world shouts and popups stay out.
- Replays now include trades between characters: each step either side takes and
  each trade update the server sends back.
## 1.2.0 — 2026-10-11

- Combine the current journey and compact character queue in a full-width card,
  giving the quest library more height and removing the separate NPC panel.
- Group individual quest controls at the bottom, use centered icons in compact
  windows, and add the current quest to its character's queue without duplicates.
  Single-quest and queue starts have distinct icons; idle controls look disabled.
- Show recipe step counts and an activity animation that never advances saved
  progress. Retain explicit individual-quest start labels and saved-progress Resume.
- Render original DAT sprites using observed outfit, direction and actions, with
  a faint 5.5× nearest-neighbor accent. Save each character's observed outfit by
  server profile for logout/restart; remembered artwork never becomes quest evidence.
- Add circular portraits to the character chooser, Characters page and Run history.
  Face-zero portraits include original body head skin, rather than showing only hair.
- Remove saved home-location settings and controls, retaining legacy-data migration.
- Use Lucide icons, equal navigation widths, aligned Launch/chooser heights and
  permanently outlined controls throughout the shared dark theme.
- Split view models and queue persistence/execution into files by responsibility,
  expand dense project-owned code, and document contributor entry points. Keep
  upstream sprite decoder attribution and distribute its and Lucide's notices.

- Emerald Dawn's choice dialog shows each recipient's rewards: both legend marks,
  gold and the experience range. Rogue Order shows plainly that it gives no second
  mark. The Gans visit is gone; he takes nothing and gives nothing.
- A saved Emerald Dawn or Mundane Birth choice can be changed from Quest controls
  with **Change choices**, until the insignia is handed over or the birth is
  submitted. Previously the choice was locked as soon as the first conversation
  happened.
- The recipients only respond after Berg's full story has been heard. The runner
  now hears it again when a character has the mark but no saved story receipt,
  instead of walking to a recipient who ignores them.
- A tolerant walk no longer fails at the world map when the exact target tile is
  a prop; it reaches the map and picks a free tile beside it.
- Run history labels every finished quest "Completed", whether it ran in a queue
  or on its own; "Attempt recorded" stays for tries that can be repeated for the
  legend mark.
- Open quest guide looks like a button before you hover over it.
- Selling the insignia to Skan is confirmed by the server's "You received 1,000,000
  gold coins." message, not by a change in your gold, so gold picked up on the way
  can't fake it.
- Server messages such as "You received 20,000 gold coins." are kept in replays and
  written to the diagnostics log. They say exactly what happened, which character
  state alone can only suggest. Player chat stays out.

## 1.1.1 — 2026-10-11

- Remove the Developer tools panel. Nothing it showed is lost: every diagnostic line
  goes to `Data\diagnostics.log` and the control bridge `log` method, calibration and
  the connection profile are read and saved through the bridge (`calibration`,
  `settings`), evidence recording is always on, and sanitized replays are saved
  automatically when a character disconnects and when DAQuester exits.
- Quest conversations no longer need one exact tile: a blocked or occupied approach
  tile is replaced by the nearest free tile beside the NPC, so two of your own
  characters never fight over a spot.
- An open popup survives a same-map refresh, so the insignia discovery page is read
  instead of walked away from.
- Share durable JSON checkpoint writes and clean temporary files after canceled
  journal saves while retaining previous checkpoints and existing data formats.
- Consolidate packet tests and replace a three-minute test sleep with repeated
  synthetic empty patrols. Retain quest, protocol and recovery regressions.
- Check movement speed-selection rules deterministically; measure integration
  cadence from packet send times rather than delayed receive callbacks.
- Remove automated UI render matrices, the headless dependency and `Test-UI`.
  Retain account, planning, ownership and shutdown checks in `DAQuester.AppTests`;
  documentation screenshots are now an optional four-page capture.
- Package the tested CI build instead of compiling it again. Preserve release
  payload validation, checksums, licenses and previous-updater compatibility.
- Clarify contributor entry points and use conventional layout in changed code.
- Apply the supported client agreement bypass before a newly launched client
  resumes, with synthetic lobby/redirect coverage; existing clients and files
  on disk remain untouched.
- Add the DAQuester application icon to the executable and desktop windows.

## 1.1.0 — 2026-10-11

- Replace Windows Forms with an Avalonia desktop workspace: deep navy and slate surfaces,
  warm gold accents, crisp vector icons, readable quest rows and shared controls.
- Refine all four pages, dark choice dialogs, compact layouts, keyboard focus,
  drag insertion feedback and character-owned queue editing.
- Explain unavailable Launch actions, focus existing owned clients, and open a
  blank client without entering a login when no characters have been saved.
- Retain the upstream transport fix that reports interruption-handler failures
  while keeping the connection alive.
- Include functional walkability for 1,417 recorded DATester maps, allowing
  travel on another PC without a populated client map cache. Keep 15 unavailable
  maps explicit and continue excluding raw game files.
- Keep saved characters, encrypted passwords, observed levels and legends,
  queues, history, settings and checkpoints compatible with previous releases.
- Allow pending reward submissions to reconcile even when a recorded legend or
  level restriction prevents a new quest attempt. Preserve final-dialog cleanup,
  verified queue advancement and all existing quest policies.
- Keep Pause available during a running queue, retain explicit account removals
  during background refresh, and drain local actions safely before exit.
- Retain optional update checks off by default and separate check, download and
  install actions. Validate older Windows Forms packages against the new feed.


## 1.0.7 — 2026-10-11

- Close verified Spare a Stick and Perfect Hairstyle reward popups before
  advancing the queue. Resume unfinished cleanup without another turn-in,
  and keep unfamiliar pages open for review.
- Recognize Eeva's already-helped reply, close it and explain the weekly
  cooldown without inventing a new reward or legend completion.
- Follow Keane's Suomi Help → escort → Next menu path using live choice IDs.
- Keep fast walking moving when monsters update off the path. Still avoid
  blocked steps, reconsider occupied exits and slow down near other players.
- Require level 11+ for Shinewood Faerie because Forest 7 blocks lower levels.
  Enforce the limit in preflight and saved character planning.
- Package releases in a separate build folder so an open checkout remains
  untouched.

## 1.0.6 — 2026-10-11

- Rewrite the README, refresh native screenshots, organize historical design
  notes, and remove the retired concept page, duplicate library/dialog code, and
  completed implementation plans. Add isolated screenshot capture and require
  all six recipes in release verification.
- Keep character connection and password status on separate readable lines
  after the shared theme is applied.

- Remember observed levels and own legend marks per character and server, even
  offline or after restart. Block Spare a Stick outside levels 1–11 and show
  recognized finished legend goals as Completed without inventing reward receipts.
  Preserve pending reconciliation, add green ready/choice rows and muted unavailable
  states, and remove duplicate search-placeholder painting.
- Redesign the native workspace around the concept's quest library, numbered
  per-character queue, switcher, and current journey. Add search/filtering,
  drag ordering, keyboard moves, removal, clear, and durable queue recovery.
- Run one verified attempt per queue entry, preserving quest goals, repeat
  intent, cooldowns, pending reconciliation, and generation isolation. Add
  verified history and optional return to a character's observed saved home.
- Add per-character Launch and Set home actions. Saved login follows
  DAOrganizer's owned-window, verified-form, one-use ticket approach; manual
  sign-in remains available. Tests never operate real game characters.

- Handle the Faerie's observed failed bond and one-deoch wait, finish remaining
  spells and return by town song. Reconcile saved refusals after manual closing
  or returning without another adoption/song. Recognize Rucesion Threshold as
  a safe landing and numeric spell level suffixes for known spells/dachaidh.
- Fix the Faerie greeting after arrival: the live Faerie is a map reactor with
  no drawn NPC. Say Hello from her verified approach and bind the real reply ID
  instead of timing out while waiting for a creature that never appears.
- Add Shinewood Faerie for every class: adopt one pet and learn all eligible
  missing Priest/Wizard spells in the same visit. Keep durable acquisition and
  return receipts, preserve unfamiliar pages, and reconcile ambiguous replies.
- Recommend town songs/scrolls or ready dachaidh. Show a dark warning with Cancel
  as default before an unprepared journey. Return by town song, dachaidh or
  documented walking exits; observe actual landings and resolve current slots.
- Include the requested pet/spell quest in the library despite no legend reward.
  Explicit later visits check newly unlocked spells without a repeat timer.
  Existing quest policies remain unchanged; full live Faerie validation is pending.
- Add Emerald Dawn with explicit Knights, Necromancer and sale outcomes, optional
  Gans consultation, Shinewood insignia collection, live dialog IDs and durable
  recovery/submission checkpoints. Verify own legends and inventory/gold evidence;
  record actual XP and preserve ambiguous claims without resending them.
- Add dark outcome choices, journal stages, library availability and local/MCP
  start options. Existing four quest policies remain unchanged. Guide-derived
  implementation has synthetic coverage; full live Emerald Dawn validation is pending.
- Keep the journal current during long quest actions by publishing each durable
  checkpoint. Ground collection uses the existing fast-walk pipeline across
  patrol and pickup paths, replans for newly visible loot and drains movement
  before pickup; player proximity and inventory confirmation remain enforced.
- Preserve walking discovery popups, finish the insignia's observed Next pages
  even when inventory updates arrive first, and checkpoint each reply before
  sending. Search stays in Shinewood Entrance; unfamiliar popups pause intact.
- Replace Emerald Dawn's proximity patrol with a closed sweep of every reachable
  non-door entrance tile. Preserve fast walking on straight runs and repeat the
  full sweep until server inventory confirms the insignia.
- Use Keane outside Suomi Inn for departures whose recorded route passes Abel.
  Resolve the live travel choice and Next pages, replan from actual arrival, and
  preserve unanswered submissions without repeating travel. Fall back to walking
  when the NPC is unavailable before interaction.

## 1.0.5 — 2026-10-10

- Close Undine weapon warnings, remove the equipped weapon and wait for server
  confirmation before continuing automated travel. Pause if inventory is full or
  removal is unanswered, without repeatedly sending equipment commands.
- Dismiss town roleplay notices before fast-walk recovery so Suomi entry no
  longer stalls behind a popup or repeatedly rejected steps. Replan from the
  destination when closing a notice finishes a map transfer.
- Save the eight-hour next-attempt deadline for every verified ordinary Spare a
  Stick reward, including single runs. Use the observed Callough reply time.
- Retain completion replies and reconcile later reward packets after manual
  interruption without resuming automation or submitting the turn-in again.
  Keep pending transactions when evidence is missing, unrelated or stale.
- Show completed ordinary attempts and their next date in the journal while
  offline. Preserve their cooldown when resuming or pausing a completed run.

## 1.0.4 — 2026-10-10

- Allow explicit Run again after a completed Spare a Stick attempt. Restore the
  eight-hour cooldown when enabling repeat after a single run; preserve permanent
  server completion, pending transactions and the other quest policies.
- Restore quest controls after an action finishes; use live ownership instead of
  saved Running/Scheduled status. Retain each character's repeat choice.
- Open Quest journal, Quest library, Account manager and Settings as main pages.
  Keep library filters and quest progress while navigating; opening Settings no
  longer stops quests.
- Scroll focused controls into view so keyboard navigation can reach quest
  actions on compact screens.
- Add one editable account table for character names and masked passwords, with
  bulk save, add/remove, duplicate validation and immediate journal updates.
  Blank passwords preserve existing credentials; renamed accounts retain them.
- Remove placeholder Aisling from new profiles and character editing from
  Settings. Keep client and update preferences in Settings.

## 1.0.3 — 2026-10-10

- Fix stale button corners and text escaping partial repaint regions. Unify
  native dark button, checkbox and card rendering with Fluent 2 foundations,
  clear interaction/focus states, consistent spacing and interface typography.
- Add native visual regression checks for clipping, repeated repaints, keyboard
  actions and scaled control geometry alongside standard/compact dialog renders.
- Size quest action columns from their labels so actions remain readable on
  small screens. Add a 1024 px window regression check.
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
