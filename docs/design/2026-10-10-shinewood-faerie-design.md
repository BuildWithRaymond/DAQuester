# Shinewood Faerie

Approved design for the requested pet, optional eligible spells, preparation
warning, and automatic return. The user approved the recommended design on
2026-10-10. Existing uncommitted work must remain intact.

## Approaches considered

Recommended: combine fresh class evidence with the Faerie's actual spell offers
and eligibility replies. This supports missing pure-status metadata without
guessing eligibility or claiming a spell was learned from dialog alone.

A strict metadata gate requires known class/master/pure state before selecting
any spell. It simplifies eligibility decisions but can leave eligible spells
unlearned when the server's own-look metadata is insufficient.

An offers-only approach delegates every spell eligibility decision to the NPC.
It tolerates incomplete metadata but creates unnecessary rejected interactions
and weaker preparation estimates. All approaches still verify actual rewards.

## Behavior

Add Shinewood Faerie to the quest selector and library as an explicit pet/spell
quest exception to the current legend-only catalog. Every class may attempt the
pet. Do not impose the priest spell level requirement on pet adoption.

Travel to Shinewood Forest 11, map 992, and approach the Faerie at (71,37), choosing
a reachable interaction tile around the observed entity. Say `Hello`. Acquire
one Pet Faerie unless current inventory/equipment or a durable verified adoption
receipt already satisfies the pet goal. Reserve an inventory slot and the
guide's five weight when a pet is still needed. Do not equip or summon the pet.

Learn every currently eligible missing spell in the same visit:

| Requirement | Spell |
| --- | --- |
| Priest, level 11+ | deo lamh |
| Priest, level 20+ | creag neart |
| Master Priest | deo searg gar |
| Pure Master Priest | deo searg |
| Master Wizard | fas spiorad |
| Pure Master Wizard | Wings of Protection |

These requirements accumulate where applicable. Existing spell slots satisfy
their respective goals. Read class/master metadata from fresh own server state;
never infer pure status from level 99, title, character name, or missing history.
The Faerie's actual available choices and explicit eligibility replies resolve
eligibility that current state cannot prove. Missing information remains unknown.
An unfamiliar reply pauses with the page preserved; it cannot count as learning.

Visits are started explicitly, without an automatic repeat timer. Revisit only
for goals still missing or spells newly unlocked after a previous visit. Do not
change the four existing quests' repeat and permanent-completion policies.

## Preparation and return

Before entering Shinewood, show the planned return method and recommend town
songs/scrolls or dachaidh, especially for lower levels. Check the character's
actual inventory, spell slots, usable item restrictions, and current mana. Do not
treat an arbitrary item containing `Song` or `Scroll` as a safe town escape.
Recall Scroll, which returns to the last death location, is not a town escape.

When no verified usable town song or dachaidh is available, show a shared dark
warning popup before any departure movement:

> Shinewood can be dangerous, especially at lower levels. You have no usable
> town song/scroll or confirmed ready dachaidh. Bring an escape item or prepare
> dachaidh for a quick return. If you continue without one, DAQuester will walk
> you back out, which may be dangerous. Continue anyway?

Use `Cancel` and `Continue and walk back`, with Cancel the default. Acknowledgment
applies to the current character/connection and visit, not every future run.
Starting through the control API must enforce the same preparation gate and
require an explicit acknowledgment of an unprepared walking return. Reassess
preparation when reconnecting or when the selected escape resource disappears.

After rewards or a definitive unavailable outcome are verified, choose a currently usable town song first, dachaidh
second, or walk out as the last resort. Resolve actual live item/spell slots at
send time. Do not buy escape items automatically. Verify actual server map and
position outside the forest before marking the return finished. Keep acquisition
receipts separate from the return checkpoint so Resume never repeats adoption
or spell learning merely because departure failed.

Checkpoint teleport use before sending. An unanswered teleport is ambiguous:
pause/reconcile rather than consume another song, cast again, or start walking.
A definitive failure may select the next verified escape method. Walking uses
collision-safe recorded exits back through Shinewood Entrance and to an actual
safe town, replanning from server coordinates. Validate the walking return before
allowing a player to acknowledge that fallback and depart.

## Integration and evidence

Use existing QuestRunner, character/quest journals, GameSession commands,
navigation, and shared dark dialog controls. Add a quest-specific action/evidence
handler and recipe; keep protocol detail in Developer mode. Track enough fresh
spellbook/class/mana state to distinguish unavailable data from an empty book.
Clear connection-specific knowledge across disconnect/generation replacement.

Use actual observed Faerie entity, pursuit, step, and choice IDs. Main menu,
pet-failure and non-Priest/Wizard replies have now been captured; successful
pet/spell rewards remain unverified live. Follow only
recognized pet/spell choices and bounded fresh pages from the expected Faerie.
Preserve unrelated popups and unknown prompts. Do not fabricate observed fixtures
from the guide or label synthetic test dialogs as captured gameplay.

Each consequential choice has a durable baseline before submission. Pet
completion requires actual Pet Faerie inventory/equipment evidence; learning
requires the corresponding server spellbook entry. A new reward must correlate
with its saved submission and connection generation. Preserve separate receipts
for partial progress, stale/wrong replies, manual interruption, and reconnect.
Fresh own state may reconcile a prior receipt; uncertainty cannot resend a
potentially committed choice. Timers and generic thank-you text never prove goals.

The player's captured 2026-10-10 follow-up clarifies a definitive pet failure:
“Sorry, no faeries are willing to bond with you. Come back in one deoch.” Save
this bound terminal reply as a pet-wait receipt, close it, finish other eligible
spell goals and return immediately. The visit can finish without a pet, while
its summary explicitly reports failure and the one-deoch wait. Do not schedule
an automatic retry or convert a deoch to an invented real-world due date.
Resume may reconcile this already saved terminal reply after manual closing or
return without another adoption or song. Recognize the observed Rucesion
Threshold landing as a safe return; woodland thresholds remain unsafe.

Public guide confirms the interaction and spell table:
[Shinewood Faerie](https://vorlof.com/general/shinewoodforest11.html).
[Map 992](https://vorlof.com/map.php?map=992) confirms the map ID and 100 by 100 size.
The normalized local route graph already reaches 992 from Entrance 542 through
565, 564, 543, 544, 545, 546, and 547. The 992 record is currently named Unknown
992; its archived display name agrees with the public map.

The base route graph has no complete walking path from 992 back to Entrance 542.
Public connected-map listings establish the missing directed exits: 547 to 546
at (29,49), 546 to 545 at (0,9), and 545 to 544 at (0,35). Treat these as documented
trigger tiles with unknown reverse landings; check collision before walking and
use each actual server landing. Do not invent reverse landing coordinates.

## Verification

Write and observe failing focused tests before implementation. Cover pet adoption
for every class, already carried/equipped pet, capacity limits, all six spell
thresholds, cumulative master/pure eligibility, existing spells, unknown state,
server refusal, partial rewards, menu reordering, wrong/stale dialogs, generation
isolation, interruption, disconnect, and reconciliation without duplicate sends.

Cover song priority, current slot changes, dachaidh readiness, excluded non-town
scrolls, missing escape popup and Cancel, explicit walking acknowledgment through
UI/control, preparation changes, teleport arrival/failure/ambiguity, and recorded
outbound/return collision paths. Verify acquiring rewards before a failed return
does not lose or repeat those rewards on Resume.

Run focused tests, the full suite, Release build, and tools/Test-UI.ps1 with
isolated sample data. Verify the warning's fully dark appearance, keyboard focus,
default Cancel, readable long text, and compact window sizing. Update quest,
control, provenance, and validation documentation with actual results. Packaging
scripts are required only if packaging changes. No tests operate real characters,
modify Excalibur working copies, or stop/restart/relaunch Excalibur.exe.
