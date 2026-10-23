# Wizardy research protocol references

These notes distinguish player reports, inspected implementation and unobserved
behavior. No game character was operated, and no Excalibur file or process was
changed for this inspection.

## Dar hax deposits: inspected code

The player explicitly requested reading Excalibur's source to understand item
deposits through Dar, who does not normally offer deposits. Read-only inspection
on October 15, 2026 found the following in the local Excalibur reference working
copy. Its repository HEAD was `813418e37dc54ed1c9e3d55fe15a474c45f580a2`;
these observations describe the inspected working files, not a certified clean
snapshot of that commit. Private filesystem paths are intentionally omitted.

- `ProxyBase/Client.cs`, `AutoDeposit`, around lines 25919-25995, selects a visible
  mundane NPC and sends the direct deposit operation for each actual item stack.
  This path does not navigate a normal banking menu.
- `ProxyBase/Client.cs`, `DialogueRespond`, around lines 23708-23735 and
  23866-23904, encodes stacked `Deposit <slot> [<quantity>]` using client merchant
  opcode 57 (`0x39`), entity type 1, current NPC ID, operation 84 (`0x0054`),
  argument marker byte 1, raw inventory-slot byte, and String8 decimal quantity.
  The nonstacked operation is 83 (`0x0053`) with a raw slot byte. A normal request
  for the Deposit menu is operation 67; it is not the hax deposit submission.
- `ProxyBase/Client.cs`, `PopupClose`, around line 24035, encodes a following
  client pursuit close, opcode 58 (`0x3A`), for the same NPC with pursuit 0 and
  step 1. Existing session framing is separate from the semantic request fields.
- `ProxyBase/Client.LawMode.cs`, `TrySendLawModeRewardDeposit`, around lines
  198-212, demonstrates the same direct deposit then close sequence.
- `Excalibur.Tests/SpawnsHaxBankingTests.cs`, around lines 37-72, compares that
  sequence with the service implementation for stacked and single items. These
  are synthetic tests, not captured server responses from Dar.

DAQuester's generic `ClientMerchantMessage` supports either a raw slot or string
arguments. The inspected stacked-deposit payload combines a raw marker/slot with
a string quantity; encoding it as two generic string arguments would be wrong.
Implement and test the specific payload rather than guessing from the gold
deposit path.

The requested DAQuester task must resolve Dar specifically from current visible
state, rather than copying the reference's first-nearby-NPC selection. Save own
character, connection generation, NPC ID, exact Song name, current slot, quantity
and inventory baseline before submission. Verify actual server receipts before
continuing. Never copy the reference's time-based retry or predicted deposited
count as proof that the bank accepted an item.

## Player-reported research behavior

Research triggers are (6,9) through (3,9) in Mileth Black Magic Master. The
reported route is No, I am ready to begin wizardy research / Specific / sorcery /
Alone, or Assistant with a registered nearby wizard's name. Dar sells parchment
for a reported 500 gold each; buy and use one at a time. Several attempts can
occur before the reported 12-hour rest.

The exact reported cooldown page is:

> You have researched already. You cannot discover anything new until you have given your mind a long rest of four Temuairan days.

The player requests remote deposits through Dar when any carried Song type
reaches 25. These are user reports and requested behavior, not packet captures.
The original scope withdrew forced grouping and exkuranum preparation. On October
18 the player requested and approved Exkuranum preparation and healing. The
October 23 instruction uses the last carried potion before restocking to 30 at
Blackstar Village Research, and restores actual HP to its maximum immediately
after research damage. Forced grouping is still excluded.
A zero-health reading may be transient; neither death nor immunity to death is
established. See [the approved change](../superpowers/specs/2026-10-18-research-exkuranum-design.md).

## Evidence still needed for live certification

No new server capture establishes Dar's deposit acceptance wording, rejection or
bank-capacity response, actual bank balance changes, poor-research pages,
assistant acceptance, or skull behavior. The opening and one status-only success
are recorded in the later excerpts below. Preserve the remaining open evidence
gaps. Source inspection supports implementing the request encoding and guarded
recovery, while synthetic tests cannot certify an automatic live research visit.

## Implemented boundaries

The Tasks runner uses player-reported choices and fresh generation/revision/page
guards. A successful attempt requires exactly one consumed Parchment Scroll,
an increased named inventory item ending in ` Song`, and either a newer reply
from the same reactor or the observed terminal status described below. It reads
offered Next pages before settling a popup receipt. Other
outcomes, including unrecognized poor-research replies, remain pending and stop
further spending. This does not claim full failure-path automation.

The direct deposit implementation currently recognizes `It will be done.` together
with the exact named inventory decrease, fresh Dar identity and current connection.
That phrase appears in the inspected Excalibur synthetic service test; it has not
been captured from Dar here. A different actual acceptance needs evidence before
recognition is expanded. Inventory loss alone is never a bank receipt. Stacked
deposits encode marker/slot bytes and a decimal String8 quantity; normal bank-menu
selection is not used. Final popup cleanup is saved separately.

Repeat setup is shared with eligible repeatable quests. It retains existing
permanent-completion and timed-continuation policies and does not silently turn
on the global scheduler. Registered assistants use owned helper leases; busy
characters and player-opened windows retain existing ownership protections.

## Parchment purchase follow-up

The October 15 local diagnostics recorded repeated pauses saying Dar's parchment
quote differed from 500 gold, followed by `Unknown research opening; popup
preserved.` after the player bought parchment manually. The stock quote itself
was not captured by those log messages. Code inspection and a synthetic delayed
reply reproduce the purchase bug: selecting Buy sends a request without waiting
for the new stock list, while the research runner immediately inspected the old
menu. The runner now awaits the fresh server reply, validates Dar's identity and
the 500-gold offer, and buys exactly one parchment with both item and gold receipts.

Starting with zero carried gold is blocked even when parchment is carried. With
no parchment, starting also requires at least 500 gold. The Tasks page explains
the cost. Spending the last 500 gold during an already started visit still allows
research using that purchased parchment; another purchase requires more gold.
Pending transactions and final popup cleanup remain reconciled before these gates.

The stalled opening's actual text was unavailable after the player's power outage;
the read-only control bridge also timed out. The player reconfirmed the supplied
prompts. Research now recognizes the unique authorized Begin choice on the fresh
research popup instead of requiring the complete explanatory paragraph to match.
Known choice labels tolerate whitespace, capitalization and trailing periods;
submission uses the exact offered label and still binds the current full page
text, revision, connection and reactor. Unknown or ambiguous choices remain
paused. No new research outcome or opening wording is inferred. Unrecognized
openings are saved in the research checkpoint with their exact text and choices.

When starting on the known research opening without parchment, the runner first
closes that opening, prepares supplies through Dar, then returns to the research
trigger. The same preparation applies when carried Songs need banking. Closing
is checkpointed before submission; a timeout requires proof that the popup closed
and never repeats the close or proceeds to a purchase while it remains uncertain.

## Observed ready-choice spelling

A later read-only inspection on October 15 captured the current opening and its
saved paused checkpoint. Both the paragraph and ready choice use `wizardry`,
where the original player report used `wizardy`. This exact mismatch caused the
remaining first-popup pause. The sanitized opening is in
[the evidence excerpt](../../content/evidence/datester-7.41/wizardry-research-opening.json).
It offers `No, I am ready to begin wizardry research`, `Help`, and
`I don't want to research`. It also reports HasNext while choices are offered;
the ready choice takes precedence over automatically advancing Next.

The matcher now accepts these two explicit spellings for the authorized ready
choice and sends the exact offered text. If both spellings appear as separate
choices, it pauses as ambiguous. No other unknown labels are accepted by this
change. Regression tests reproduce the captured page and stop after the opening
submission; they do not assert an unobserved continuation or research outcome.
Inspection submitted no game actions and did not alter the character's popup.

## Observed status-only result and continuation

The player's first completed submission left a saved `research-submit` baseline
of one Parchment Scroll and 9,500 gold. A read-only inspection found no remaining
parchment, three new Piet Songs, unchanged gold, no open dialog, and a fresh type-3
active status: `You have no more available Parchment Scrolls`. The restricted
[resource/status excerpt](../../content/evidence/datester-7.41/wizardry-research-status-result.json)
omits unrelated inventory and character metadata. It is not a packet replay.

The runner previously waited for a newer result popup, so that valid result stayed
pending and prevented every later purchase, including on Resume. It now accepts
this exact terminal status together with the one-parchment debit and named Song
gain, unchanged gold, matching actor/generation and a status newer than the saved
submission. The intermediate `You research the arcane art of Sorcery` message
alone does not settle a running batch. The terminal message is not a cooldown.
Unknown, stale or incomplete receipts remain pending without resubmission.

After settling, buy one new parchment from Dar, then revisit the trigger. Travel
to the preparation tile leaves the trigger; the existing trigger helper also
steps off before returning when necessary. No F5 refresh is required. Continue
until the separately recognized rest refusal, a resource blocker, or an unknown
outcome. These repeated visits are covered synthetically; this excerpt proves
only the single observed success.

Resume can settle the original pending result from matching in-memory observations
or the app's existing automatic disconnect/exit archive. Its reader projects only
the needed resource/status fields, validates the server profile and server-event
identity, and preserves original generations. Current reconnect balances alone
cannot prove an earlier submission. Recovery records the earned Songs once and
sends no game command; subsequent procurement uses fresh current state.
