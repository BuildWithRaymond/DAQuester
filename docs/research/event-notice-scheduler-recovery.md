# Red Moon notice and scheduled Stick recovery

## Observed evidence — October 16, 2026

Awesome's scheduled launches at 17:22 and 17:33 connected successfully, immediately
reported `One completion verified`, then logged `scheduler closed its game window`.
The saved Stick receipt was dated October 15, with its eight-hour deadline already
past. The schedule retained `Resume = true` and ended with `Next scheduled time is
not in the future`. This was scheduler cleanup after reusing the old receipt;
the captures do not establish a game-client crash.

Convict connected at 17:23, paused Wizardy research with `Fresh wizardy research
popup required; unrelated popup preserved`, then underwent the same owned-client
scheduler cleanup. His current page was an unnamed reactor popup beginning
`The full Red Moon shines brightly in the sky.` The player identified it as the
Halloween event and requested automatic closure. The sanitized
[single-page capture](../../content/evidence/datester-7.41/red-moon-notice.json)
retains only that pursuit packet and its decoded dialog. It excludes character
bindings, broad snapshots, inventory, credentials and other players.

The observed shape is reactor type 4, pursuit 0, step 1, popup type 0, empty name,
no choices or prompt, and an offered Next. No later event page or live close
receipt was captured. Do not infer an event continuation from this evidence.

## Code behavior

Scheduled attempt selection now compares a completed repeatable quest receipt
with the schedule's due time. An older completed receipt cannot satisfy the new
run merely because `Resume` survived a previous interrupted schedule. In-progress
steps, pending submissions, reward cleanup, current receipts and permanent goals
keep their existing recovery paths. The runner still enforces the actual
cooldown; this does not reset a game limit or rearm a stopped schedule.

The exact captured Red Moon shape and text are recognized at quest boundaries
and by travel notice handling. Closure uses the current observed page, generation
and revision. It sends Close, never the event's offered Next. Unknown text,
choices, prompts, NPC pages and stale generations are preserved. An uncertain
close pauses rather than selecting a continuation or automatically retrying.
No passive observer sends actions to player-opened sessions.

## Qualification boundary

Synthetic regressions reproduce stale-completion reuse and the research pause.
Movement tests replay the original server page and require the exact Close packet
plus a simulated server close receipt before movement. These establish code
behavior, not a fresh live automatic run or an observed event-close receipt.
The running application and all real characters remain untouched by tests.
