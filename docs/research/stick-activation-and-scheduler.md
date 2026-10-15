# Callough activation and scheduler controls

On 2026-10-14 the player reported that a scheduled Spare a Stick run patrolled
without collecting branches until the player accepted the quest at Callough.
The preparation coordinator called the branch patrol before the recipe's bank
and acceptance stages. Its new synthetic test incorrectly expected that order.
The prior branch-drought design already required fresh Callough acceptance.

Preparation now includes branches only after acceptance on the current
connection. An older saved branch-preparation checkpoint without that proof
yields to acceptance instead of continuing its patrol. The collection helper
also requires the proof, including when preparing an already accepted queued
quest. Freely obtainable items for other ready queued quests still prepare
early. Quest order, carried-branch credit, random-drought patrols, pending
turn-in reconciliation and the eight-hour/permanent completion rules remain.

The same run stopped at weapon shop 132 (13,1), with no mundane entities
observed and a server-evidence timeout. Both acceptance and turn-in used an
unanchored visit, which could wait at the distant map entry for an offscreen
Callough. Both now supply the previously recorded reachable public-side
approach (6,10). Normal doorway departure and first-visible stopping apply;
the runner neither needs to reach that exact tile nor walks behind the counter.
The live position/timeout are observed facts; native fixtures use synthetic
collision and NPC appearance to test the approach.

## Scheduler save, enable and resume

The player's saved Awesome repeat entry was `Needs attention`, with reason
`Stopped. Review before resuming saved progress.` Scheduler itself was already
enabled and there was no login block. Saving edits correctly preserved the
stopped entry; enabling the global scheduler did not rearm it. The interface
hid rearm in a menu and cleared the shared agreement checkbox after Save,
forcing another acknowledgment before Enable.

The scheduler page now explains each action and shows Resume schedule directly
on stopped entries. Save feedback explicitly says whether the saved entry is
still stopped. The global button says Turn on saved schedules, or Scheduler is
on when enabled. Agreement remains checked across Save, Resume and Turn on
within the page; changing character, quest or editor resets it. Success feedback
is not styled as an error. Login-block review, per-entry rearm, global enable
and all existing runtime ownership/consent gates remain separate.

The change does not edit private scheduler data or automatically re-enable a
stopped player schedule. Manual interface review uses isolated synthetic preview
data; tests and builds do not operate game characters.
