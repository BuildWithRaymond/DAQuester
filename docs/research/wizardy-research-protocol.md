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
The player withdrew forced grouping and exkuranum preparation. A zero-health
reading may be transient; neither death nor immunity to death is established.

## Evidence still needed for live certification

No new server capture establishes Dar's deposit acceptance wording, rejection or
bank-capacity response, actual bank balance changes, research success/failure
pages, assistant acceptance, or skull behavior. Preserve these as open evidence
gaps. Source inspection supports implementing the request encoding and guarded
recovery, while synthetic tests cannot certify an automatic live research visit.

## Implemented boundaries

The Tasks runner uses player-reported choices and fresh generation/revision/page
guards. A successful attempt requires exactly one consumed Parchment Scroll,
an increased named inventory item ending in ` Song`, and a newer reply from the
same reactor. It reads offered Next pages before settling that receipt. Other
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
