# AislingExchange parcel qualification

Prepared 29 October 2026. Source inspection and synthetic tests are separate
from live game evidence. No production parcel profile is qualified yet.

The default desktop receipt policy accepts no profile, route or sender framing.
The private worker likewise advertises no return capability. Keep both gates off
until the following actual observations are captured and reviewed. An unknown
fee is not an eligibility gate: the selected policy records actual postage after
each proven send, as described in the [actual-postage design](../superpowers/specs/2026-10-29-aislingexchange-actual-postage-design.md).

| Capability | Required observed evidence |
| --- | --- |
| Public NPC approach | Exact profile, map dimensions, reachable public tile, fresh mundane identity and current menu; hidden parcel operations need not appear as choices |
| Receive | Sender-bearing trusted NPC/type-3 reply, exact item and whole-bag gain; unrelated chat cannot supply proof |
| Other personal mail | Separate sender proof and bag gain, including the same item as a provider unit |
| Empty/full/weight | Fresh terminal reply tied to the submitted receive; no inferred success from time elapsed |
| Partial collection | Earlier proven units retained while the capacity/refusal outcome stops the loop |
| Send one unit | Exact current bag slot, source acknowledgement, whole-bag debit of one and independently fresh gold observation matching the paid amount |
| Stack send | Direct hidden send with fixed quantity one and recipient, exact one-unit debit from the selected stack and observed item-specific fee |
| Lost/delayed reply | Original generation/revision and durable intent, later evidence reconciliation without resubmission |
| Worker intake | One registered unit, independently proven sender/item and same-session whole-bag gain |
| Worker bank deposit | Fenced intent, exact bag debit and fresh same-session bank increase of one; no intervening bank mutation |

Client reports, public `submitted` acknowledgements, packet writes, quotes and
elapsed time are not receipts. No parser may invent a sender from an item delta,
or treat a missing fee as zero. Excalibur source establishes command framing;
only scoped live evidence establishes the outcomes below.

The inspected hidden merchant operations work independently of visible parcel
choices: receive is `0x0064`, and direct send is `0x0063` with
`02 | slot | ASCII 1 | String8 recipient`. The same one-unit send framing is
used for a stack. Menu operation `0x0060` is useful for observation but is not a
financial send or a prerequisite for the direct packet. These source facts do
not qualify a sender parser, a fee or a successful stack debit.

Retained historical controller receipts corroborate source item/gold decreases
and show that fees vary by item. They do not bind a current supported profile.
The retained receive frames lack trusted sender identification, and legacy bank
journals lack a fresh post-deposit bank observation. These gaps remain open;
the historical records cannot activate a receipt policy.

On 29 October, a separate authorized live phase used Maria on map 498, Rucesion
Inn, from the stable public tile (3,6). Her ordinary menu offered no parcel
choices. The [restricted live excerpt](../../content/evidence/datester-7.41/aislingexchange-hidden-parcels.json)
retains these observations, with runtime identities normalized:

- Direct `0x0060` returned `What do you wish to send? ` without changing the bag
  or gold.
- One direct `0x0064` returned `Let me see...ah yes, here it is.` followed by the
  type-3 active message `Nothing here for you, I'm afraid.` The whole bag and
  gold were unchanged. The first page alone did not prove collection.
- One direct `0x0063`, quantity one to Exchanger, removed exactly the selected
  nonstackable Wolf Claw from slot 26. Every other bag slot was unchanged. A
  fresh gold-bearing `0x08` reduced gold from 928,950 to 926,850, followed by the
  item-removal `0x10` and Maria's `0x2F` reply:
  `You've given 2100 coins. It will be done.`

The bound paid-postage receipt, exact whole-bag debit and matching actual charge
of 2,100 gold prove this parcel was Sent. They do not prove recipient collection,
a trusted sender-bearing receive, stack debit or a bank deposit. The public
capture classifier now derives `SourceAccepted` / `CaptureSent` from those
original observations, including the older capture schema, without rewriting
the raw evidence or sending again. The running application's original journal
has not yet been reconciled using that new build. A separate read-only analysis
using the tested assembly classified that exact original record as
`CaptureSent` / `SourceAccepted`, with postage 2,100, while verifying that the
original file and raw intent/observations were unchanged. No game session was
constructed. This charge does not price other items or become a fallback.

The capture amendment passed 68 focused cases and both complete xUnit projects:
2,146 Core plus 584 App tests, with no failures or skips. These synthetic tests
are separate from the read-only analysis of the original live evidence.

The player amended the approved private diagnostic to receive only. Its staged
implementation submits one guarded receive, records bounded native observations
and keeps a canonical review hold. It never deposits items, credits available
stock or clears its lock through generic administrative resolution. Ordinary
quest supplies stay in the quest character's bag. Post-quest leftover return
accounting remains separate. See the [receive-only design](../superpowers/specs/2026-10-29-aislingexchange-private-parcel-qualification-design.md).

Builds and synthetic tests never operate real characters. A separate authorized
live phase must select an owned, available character and isolated quantities,
use the existing login/control gates, and stop on any uncertain transaction.
Capture sanitized packet replays and type-3 active messages. Preserve the old
intent across reconnect instead of retrying its financial packet.

Before activation, verify the complete worker release on its supported platform,
review the exact catalog and economic/fee budgets, and complete consecutive
supply requests plus collection, return intake and bank deposit. Retain the
original certification baseline and immutable fixtures. A configuration flag
or synthetic fixture cannot replace capability certification.

The earlier frozen candidate passed 18 selected Linux synthetic fixtures and an
isolated Unix socket permission probe. The subsequent immutable actual-postage
candidate passed 19 selected Linux fixtures and the same isolated probe under
unchanged confinement. The subsequent receive-only candidate passed 23 selected
Linux fixtures and the Unix-socket probe, including its controller, canonical
guards, scheduler integration and native packet capture. It also passed a full
TypeScript compile and three supporting local checks. These results do not
replace the complete new-release verifier or outstanding live receipts above.
No new worker was deployed and no live collection or banking occurred.

Two worker qualification gaps remain explicit. The inspected ordinary client
refresh does not provide a complete bag resnapshot, so the conservative bag/gold
freshness gate may expire while idle; no read-only refresh route is qualified.
An inspected gold-only refresh supplies no new bag evidence. Bridge publication
counters cannot renew bag age. The new staged diagnostic instead uses a native
decoded-frame counter for its submission/capture boundary; that counter still
cannot invent a complete bag resnapshot.
Native return intake currently expects one exact sender/item. It does not route
an out-of-order mailbox arrival to another registered return. Mailbox arbitration
and safe handling of unrelated parcels need qualification before concurrent
production returns can be advertised.
