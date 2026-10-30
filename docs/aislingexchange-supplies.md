# Optional quest supplies

Optional quest supplies provided by AislingExchange.com are implemented behind
readiness gates and are not yet available for production dispatch. Once qualified,
anyone using the program can participate without a website account. The pairing
flow uses the existing character login and an independently observed challenge
sender before issuing a limited credential. Game passwords never go to the
supply API.

The option is off by default in Settings. Each offer asks before dispatch and
contains only missing quest quantities. Declining keeps normal gathering and
buying. Proven partial arrivals reduce the remaining acquisition; quotes and
timeouts do not prove delivery. Pause, Stop, opt-out and reconnect preserve
submitted operations for reconciliation.

Journey accounting preserves personal baseline, unknown arrivals, equipment and
supplies reserved for continuations. Returns have a separate status from verified
quest completion and cannot credit provider stock from desktop reports alone.

## Current readiness

Client transport, protected pairing/checkpoints, quest integration, durable
collection reports and return registration/send checkpoints are implemented for
synthetic validation. The dedicated
public broker is deployed behind a Cloudflare Tunnel with an empty catalog.
Production dispatch remains unavailable: the concrete game parcel port requires
an observed, exact-profile send/receive policy. Its default policy qualifies
nothing. Excalibur's `/rec all` repeats individual receives rather than sending
a bulk packet. The port uses hidden merchant operations against a fresh nearby
mundane NPC; visible parcel menu choices are not required. Direct sends encode
one item for both single items and stacks. Neither receive nor return sending is
enabled by a configuration boolean or a passing synthetic test.

The private worker binding is prepared in isolated local staging against the
existing serialized scheduler and shared stock/reservation ledger. Original
release fixtures were recovered and verified against the unchanged certified
hashes; stale emitted files in the mutable checkout explained the mismatch.
Worker activation, full release certification and observed game qualification
remain outstanding. Readiness and dispatch stay off. See [broker and worker setup](../integrations/aislingexchange/README.md)
and the [approved design](superpowers/specs/2026-10-28-aislingexchange-supplies-design.md).

The actual-postage candidate passed 19 selected Linux fixtures; the latest
receive-only candidate passed 23 selected Linux fixtures, supporting local
checks and a real isolated Unix socket permission probe. Both complete public
xUnit suites pass. Complete immutable-release certification remains outstanding;
the selected fixtures do not replace it. Genuine static inputs for the complete
verifier have now been recovered and hash-verified. Full runs still require
passing platform preflight and sufficient isolated test capacity.

Once game qualification and worker activation are complete, the protected user
Data directory may contain `aisling-exchange/provider.json` with this public API
base:

```json
{
  "ApiBase": "https://supplies.aislingexchange.com/api/quest-supplies/v1/"
}
```

The HTTPS endpoint is live, but does not advertise supply readiness. There is no
production endpoint default in the desktop. Missing/invalid configuration uses
ordinary acquisition before any submitted operation. Credentials and pairing
intents have separate Windows DPAPI stores; never paste credentials into this
configuration. Connection details remain outside the player window.

## Origin privacy

The client connects only to approved public AislingExchange HTTPS names. It has
no private worker hostname, address, server password or administrative key. API
errors are bounded branded codes, not raw worker errors or proxy details. The
private worker initiates outbound requests; the broker cannot run arbitrary
commands, request arbitrary URLs, choose arbitrary recipients or construct packets.

The dedicated public supply hostname terminates at Cloudflare and routes only
the fixed supply API prefixes to the isolated broker. Other paths return 404.
Public checks confirmed bounded authentication errors and no origin identifiers
or upstream server headers. The broker and tunnel metrics bind only to loopback;
the connector validates origin TLS against its dedicated local CA. No general
game-worker/admin gateway is routed through this tunnel.

A tunnel does not erase historical DNS or hide an address published by other
services. This tunnel is verified active. Historical DNS and unrelated public
services on the shared host still prevent any guarantee that the existing server
address is undiscoverable.

## Inspected Excalibur parcel commands

Read-only inspection on October 28, 2026 found the handlers in
`ProxyBase/Client.cs` in the main Excalibur reference working copy. No Excalibur
files, builds, processes or characters were changed. These are inspected source
facts, not live DAQuester parcel captures. Private filesystem paths are omitted.

- `/rec all`, around lines 6104-6143, selects the first visible mundane NPC,
  submits Receive Parcel, closes the popup and waits 1,100 ms before another
  receive. There is no sender filter or receive quantity field. The bounded copy
  stops on cancellation/disconnection or its 30-second continuation deadline.
- `DialogueRespond`, around lines 23736-23757 and 23866-23898, uses merchant
  opcode `0x39`. Semantic receive fields are `01 | NPC UInt32BE | 00 64`.
  Send-menu operation is `0x0060`; direct send operation is `0x0063`, followed
  by fixed bytes `02 | inventory-slot | 31 | String8 recipient`. The fixed byte
  `31` is ASCII `1`; there is no variable quantity parameter in this path.
  Arbitrary stack behavior and fees still require actual receipts.
- `PopupClose`, around line 24035, follows with pursuit opcode `0x3A` and
  semantic fields `01 | NPC UInt32BE | 00 00 00 01`. Session dialog headers and
  encryption are separate from these semantic fields.
- `Server.Messages.cs`, around lines 1844 and 2993, stops the receive loop only
  from type-3 active messages matching `Nothing here for you, I'm afraid.`,
  `Your inventory is full.`, or the prefix `Whoa there, you don't have enough room `.
  Empty mail and insufficient capacity must remain different DAQuester outcomes.
- `Packet.cs`, around line 3264, writes String8 using a byte length and CP949
  text. The command path does not journal per-unit sender/item receipts or
  reconcile inventory/gold after an uncertain send. DAQuester must add those
  boundaries rather than copy the timing loop as proof of success.

Exact inspected working-file SHA-256 values:

| File | SHA-256 |
| --- | --- |
| `ProxyBase/Client.cs` | `61be56877e659ff3170ab01dbedf50557c81c39acecfef48e8780859d71f8c0f` |
| `ProxyBase/Server.Messages.cs` | `fdd6272ee67ccf49727cd0909e8f9144827af0688a12c248f050084f1419b64a` |
| `ProxyBase/Packet.cs` | `60a8dc4eb11040c5cc085bd4ba5b566cc6bdea62d3d35da5852d00d8cae84012` |

## Collection and return recovery

Each parcel check has its own durable claim and bounded individual receives.
Collected quest supplies stay in the character's bag for quest use. Collection
does not bank them. Leftover return registration is a separate ended-journey
operation and preserves quest and personal item reserves.
Only a qualified sender-bearing receipt plus the exact own-bag change can assign
an item to an Exchanger unit. Other proven arrivals become personal baseline,
including an identical item from another sender. Empty mail, full bag, excessive
weight and refusal remain separate outcomes. A timeout retains the submitted
intent; reconciliation reads observations and journals without sending packets.
A proven pre-submission refusal permits a later explicit Resume.

Collection reports save an immutable report ID and unit list before HTTP. A lost
reply repeats that report, never collection. Existing locally resolved receipts
are migrated into pending reports. The broker now admits a following request
once every dispatch unit has trusted source acceptance or cancellation; it does
not need to turn an untrusted client report into recipient proof or bank credit.

Returns freeze attributable leftovers before registration and wait for private
worker registration before sending. Supplied leftovers and journey surplus keep
their distinct origins, including when they share an item name. Each unit saves
its intent before sending exactly one item to Exchanger. The port checks the
whole bag, trusted source acknowledgement and actual paid postage. New sends
do not require a known fee. A remembered item/profile fee is advisory; the server
calculates the charge. A fresh post-send gold observation must match the paid
amount, including fresh unchanged gold for a proven zero charge. Missing or
contradictory evidence stays pending.

Item reserves remain hard boundaries. Gold reserves and the private daily
postage threshold stop later sends: one actual charge may cross that boundary.
Settle and record the committed send before stopping. Existing fixed-quote
checkpoints retain their original semantics. See the approved
[actual-postage amendment](superpowers/specs/2026-10-29-aislingexchange-actual-postage-design.md).
Saved report IDs survive lost replies and opt-out. Ended journeys retain their
manifests and remain discoverable for later reconciliation.

Unknown prior attribution stays unknown after later collection. A reconnect can
reconcile a submitted unit from durable original evidence, but identical bag
counts alone cannot authorize new sends from an old journey. Personal/unknown
stock is never swept into returns to resolve that uncertainty. Partial or
rejected batches do not hide other batches still awaiting collection or deposit.
Cached HTTP acknowledgements confirm only their saved report and cannot replace
newer recipient, collection or bank evidence obtained during reconciliation.

Desktop source acceptance, private collection and bank deposit have separate
checkpoints. Desktop reports never establish bank credit or reset allowances.
Return intake and banking still require native qualification and release checks;
the production worker keeps return support disabled. See the
[qualification checklist](research/aislingexchange-parcel-qualification.md).
