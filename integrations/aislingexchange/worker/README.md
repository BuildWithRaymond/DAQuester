# Outbound worker adapter

`adapter.ts` is an embedding library for the current authoritative worker. It
uses structural subsets of the inspected `ExchangerLedger` and
`ParcelSendController.start` APIs. It creates no game client, listener, generic
packet sender, shell transport or parallel stock ledger. It has no timers that
start game actions by themselves.

`whisperQualified` and `dispatchEnabled` default false. A qualified profile,
explicit private budgets and real scheduler callbacks are required before
enabling them. The public broker's catalog and credential timestamps are not
authority for private dispatch. The adapter checks its own observed pairing
binding and proof lifetime before admitting new work.

A private worker binding is prepared in isolated staging. It uses the existing
central SQLite authority, fenced owner RPC, serialized scheduler and native
withdrawal/parcel controllers. It has not been activated. All qualification and
dispatch gates remain off; native return intake remains hard disabled.

Focused native tests and typecheck pass. The original release fixtures were
recovered and hash-verified against the unchanged certification baseline. The
latest receive-only candidate passes 23 selected Linux fixtures and an isolated
Unix socket permission probe; full release certification and observed game
qualification remain outstanding. Complete platform/resource preflight and the
full verifier before activating the private binding.

## Required embedding

- Supply the existing authoritative ledger and `withExclusiveOwner`, backed by
  the existing Exchanger scheduler/session ownership. It must not preempt an
  active customer, lottery, withdrawal, banking or uncertain parcel checkpoint.
- `reserveSupply` must validate the private eligible catalog/recipe quantities,
  fresh own stock and bank evidence, all existing reservations/holds, capacity,
  per-character allowance, outstanding work, global stock reserve, daily unit
  budget and maximum parcel-fee budget **in one authoritative reservation
  transition**. It queues existing controllers and returns promptly; it cannot
  claim source acceptance before their actual receipts. The inspected ledger's
  ordinary `reserve` method handles retail orders and is not this new atomic
  supply reservation. The prepared private binding implements this transition
  and deducts its holds from ordinary retail reservations in the same authority.
- `registerReturn` must reserve mailbox work under the same scheduler. Only
  publish `registered` after its accepted manifest and ownership are durable.
  Reconcile actual collection with existing item-bank deposit receipts; client
  reports cannot establish arrival, bank credit or allowance credit.
- `reconcile` resumes a known operation or cancels only uncommitted units. Its
  input includes desired revoke/cancel flags; a durable dispatch-block marker
  is saved before this optional callback. Previously submitted or uncertain
  units retain their obligations. Absence of this callback keeps work held.
- Call `pollOnce()` from an appropriate worker tick, and forward qualified
  incoming whisper observations to `observeWhisper()`. The inspected handler
  uses game `0x0A` channel 0 and filters outgoing echoes. This adapter accepts
  the exact synthetic framing `"Character" AEQ-<43 base64url characters>` only.
  That framing still needs fresh observed qualification. Never pass HTTP data,
  screenshots, public chat, unqualified strings or reflected outgoing text as
  a trusted observation. No whisper text enters public logs.
- Refresh `publishCatalog()` from private, confirmed Exchanger availability
  before its two-minute public projection expires. Subtract every commitment;
  never pass the aggregate stock-report total from other actors. Local disabled
  dispatch/whisper qualification always publishes readiness as false.
- Existing controller receipt callbacks use `publishOperation()` with a durable
  increasing sequence and exact unit snapshots. Publishing retries use the
  same sequence/body. A lost proof HTTP response is reconciled from its saved
  trusted observation, without sending another game whisper.

Use `submitOneUnit` only after an accepted private manifest exists. It checks the
bound recipient/item/unit, saves a no-replay marker before invoking the existing
controller, and returns only a submission identity. It cannot prove source
acceptance or delivery. Connect the actual controller's finished event and
correlated trusted NPC/type-3 outcome to the operation's unit receipts.
`canStartOwnedParcel` must enforce current session/generation, existing recovery
holds, authoritative inventory/NPC arguments, current gold and the next-send fee
threshold. Unknown postage does not block a supported item: settle and record
the exact server charge after proof, even if that one send crosses the threshold,
then stop later sends. Do not accept other players' chat as a parcel acknowledgement.
Feed current packets/observations through the existing controller handlers.

The current server implementation explicitly notes pharmaceutical parcel
refusal. Do not advertise Ard Ioc Deum/Red Potion parcel support without new
observed evidence. Receive-all, public NPC routes, quantity paths and return
attribution also remain separate live qualification requirements.

## Fixed outbound routes

All calls use a dedicated worker bearer, HTTPS certificate validation, no
redirects, a ten-second transport timeout, bounded JSON and fixed paths under
`/worker/v1`. Configure only the official **public** service origin. No private
host is sent to, configured by, or exposed through the broker.

| Route | Body/result |
| --- | --- |
| `POST /jobs/claim` | `{}` → one bounded job or null, stable target ID, fence, lease token and 30-second lease |
| `POST /jobs/{id}/result` | lease token, fence, ready/rejected for pairing or accepted for a journaled operation |
| `POST /pairings/{id}/proof` | exact observed nonce, canonical sender, profile, observation timestamp |
| `POST /pairings/{id}/status` | `{}` → bounded proof status for uncertain HTTP recovery |
| `POST /catalog` | private qualified readiness, bounded eligible offers/modes; no other actors or raw bank totals |
| `POST /operations/{id}/status` | durable sequence, allowed state/error code and complete unit receipt snapshot |

Jobs have a closed set of kinds. Unknown fields, packet bodies, commands,
callbacks, URLs or substituted manifests fail before any game callback. Queue
leases are transport ownership, not permission to replay a game submission. The
worker saves admission before callbacks; a redelivered uncertain admission moves
to review and never calls reservation again. The existing game owner stays
exclusive across reconciliation regardless of public lease expiry.

Status publication has a durable outbox. A lost HTTP response retries the same
canonical sequence after restart without repeating reservation or game actions.
An acknowledgement cannot clear a newer receipt that arrived while HTTP was
outstanding. Game uncertainty stays a private durable review hold.

The broker retains late receipts after desktop credential expiry. Expiry requires
fresh proof for new work/status access and does not erase accepted obligations.
Cancellation is asynchronous: once observed at the worker, its durable local
block forbids starting additional units. Keep polling cancellation work before
new dispatch; a disconnected worker must not assume public consent is still
current.
