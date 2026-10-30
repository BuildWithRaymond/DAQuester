# AislingExchange quest supplies

Prepared 28 October 2026. Approved by the player on 28 October 2026, with the
additional requirement that this integration must not reveal the private server.
Production enablement still requires the deployment and qualification inputs
listed below.

The player approved [actual postage accounting](2026-10-29-aislingexchange-actual-postage-design.md)
on 29 October. That amendment supersedes precomputed-fee and hard gold-reserve
promises for new parcel sends: record the actual charge, allow one threshold
overrun and stop subsequent sends. Item protection and receipt qualification
remain required.

## Requested behavior

Anyone using DAQuester may use the optional supply service. No AislingExchange
website account, registration, approved-user list, or manual verification is
required. Use the character's existing game login for automatic proof of control.
The feature defaults off. Enabling it permits automatic character pairing when
supplies are first needed; each supply offer still requires the player's acceptance.

Before normal material acquisition, offer only missing quest quantities that the
service can supply. Accept partial offers, receive proven arrivals, then acquire
the remaining deficit normally. At the actual end of the selected journey, return
attributable unused supplies while preserving personal inventory and later quest
reservations. Keep quest success separate from outstanding return work.

Player text uses **AislingExchange supplies** and **Provided by
AislingExchange.com**. Offer actions are **Use supplies** and **Gather/buy
normally**. The actual game recipient remains `Exchanger`. Public source,
contracts, logs, screenshots, errors and documentation for this integration use
AislingExchange branding and contain no private infrastructure identifiers.

## Security boundary and alternatives

The desktop is an untrusted public client. A fork can alter HTTP requests and
invent inventory, quest progress, journey IDs and receipt reports. A logged-in
desktop knows its own character; that fact becomes trustworthy to the service
only when the service independently observes proof through the game connection.
No distributed application secret or executable obfuscation changes this boundary.

Three approaches were considered:

| Approach | Tradeoff |
| --- | --- |
| Separate public service; private worker pulls jobs outbound | Recommended. No new public worker listener; bounded job interpreter and durable queue required. |
| Separate public service calls a narrow private worker endpoint | Less queue plumbing, but adds inbound access to the private host. |
| Publish supply routes on the existing worker gateway | Smallest change, but couples public traffic to privileged gateway authentication and routing. |

Use the first approach. The public service holds bounded intents and status
projections. The existing private worker remains authoritative for game ownership,
pairing proof, stock, reservations, dispatch limits, game actions and receipts.
It initiates authenticated outbound polling and result publication. Its panel,
administration, control bridge and broader API are never reverse-proxied through
the public supply route. Existing private administration access stays separate.

The worker uses a separate server-only service identity. The desktop receives
only its own character-scoped supply credential. Neither service credentials,
game passwords, ingest credentials nor administrative credentials are distributed
to desktop clients. Public service compromise must still encounter the worker's
fixed operation allowlist, independently checked identity bindings and budgets.

## Automatic pairing using the existing login

Pairing is not a website account or a player approval workflow. It runs through
the already owned game session after the feature has been enabled.

1. DAQuester requires a fresh, connected own-character snapshot and existing
   queue/standalone ownership. It generates a local installation ID, a random
   verifier of at least 256 bits, and a durable pairing operation ID. It saves
   the verifier in a separate protected integration credential store.
2. The public service receives the server profile, expected canonical character,
   installation ID and SHA-256 verifier challenge. These claims alone authorize
   nothing. A rate-limited pairing intent produces a random nonce of at least
   128 bits, a five-minute expiry and the fixed expected recipient `Exchanger`.
3. The worker registers the bounded intent. DAQuester waits for that readiness
   before sending the exact nonce once through a qualified game whisper command.
   Bind the command to the actual character, generation and ownership. Persist
   its submission boundary and poll pairing status after an uncertain send;
   a transport timeout never requests another whisper automatically.
4. The worker accepts only a fresh direct whisper on its trusted game connection
   whose server-formatted sender, recipient, exact nonce and server profile match
   the registered intent. Validate the game message type and sender framing;
   quoted player text, public chat, reflected outgoing whispers, screenshots,
   client reports and a generic thank-you cannot establish proof.
5. Consume the nonce once in the worker ledger and publish a verified binding.
   The desktop presents its verifier to finish pairing. Only the matching
   verifier can retrieve the limited credential; knowing a nonce or pairing ID
   is insufficient.
6. Issue a random opaque supply-only bearer credential with a one-hour lifetime,
   bound to the stable server/character subject and installation. Store its hash
   on the service and protect the desktop value with Windows DPAPI CurrentUser.
   Do not introduce refresh tokens in the first version. Automatically repeat
   game proof when a new credential is needed and safe ownership is available.
7. Completion retries preserve the same binding and installation identity. If a
   lost token response cannot be recovered safely, revoke that issuance and
   obtain fresh game proof. Preserve all existing operation IDs and obligations.
   Fresh proof also supports revoking a lost installation or all credentials
   for the same character without requiring a website account.

Pairing statuses and completion lookups require the verifier over HTTPS; never
put it in a URL. Pairing and token endpoints have strict payload, frequency,
expiry and outstanding-intent limits. Unused challenges expire without stock
actions. Challenge text, verifiers and tokens are removed before logs or replay
records are created. Existing replay privacy remains intact: preserve active
0x0A type-3 outcomes and continue excluding other chat/whisper types.

The tracked worker already observes direct game whispers and forwards observed
sender information. That is useful implementation evidence, not live
qualification of this new pairing flow. DAQuester has no implemented whisper
command for this feature yet. Unknown sender framing or unsupported whisper
behavior disables the integration safely; a typed name is never a substitute.

Use a stable game character ID when available. If the protocol supplies names
only, bind identity to server profile plus losslessly canonicalized name, require
fresh proof after expiry, and document rename/deletion/name-reuse limitations.
Do not assume two game servers share character identity.

## Public API and private job contract

Use a new versioned public namespace, proposed `/api/quest-supplies/v1`. The actual
production HTTPS origin has not been established by the handoff and must not be
guessed. Production clients use the configured official origin, normal TLS
certificate validation and no credential-bearing redirects. No origins, callback
URLs, hosts, file paths or packet bodies supplied by clients become worker jobs.

Pairing creation is the only unauthenticated application operation, with bounded
abuse controls; pairing status/completion requires the installation verifier.
All catalog, quote, supply, return and status routes require a valid supply
credential. Credentials cannot authenticate to administration or existing retail
ingestion routes. The service independently authorizes every referenced object.

| Operation | Meaning |
| --- | --- |
| `POST /pairings` | Register a bounded character-proof intent; no stock action. |
| `POST /pairings/{id}/status` | Verifier-protected readiness/proof lookup. |
| `POST /pairings/{id}/complete` | Exchange matching proof and verifier for limited credentials. |
| `POST /credentials/revoke` | Revoke the calling installation's credential; retain its operation history. |
| `POST /recoveries` | Use a fresh one-use character-proof grant to revoke lost installations and recover their existing operations. |
| `GET /capabilities` | Readiness, game profile, fixed recipient, eligible catalog, policy limits, qualified parcel modes. |
| `POST /quotes` | Read-only offer of bounded missing quantities; no reservation or game action. |
| `POST /requests` | Submit explicit acceptance of an exact owned quote and quantities. |
| `GET /requests/{id}` | Read owned per-line and per-unit progress. |
| `GET /operations/by-client-id/{id}` | Resolve a lost supply or return response. |
| `POST /requests/{id}/receipts` | Store bounded client receipt reports; never credit provider stock or renew allowance. |
| `POST /requests/{id}/cancel` | Cancel uncommitted work only. |
| `POST /returns` | Register a bounded return before any player parcel submission. |
| `GET /returns/{id}` | Read separate reported, collected and banked progress. |
| `POST /returns/{id}/sent` | Report player-side source acceptance without claiming provider receipt. |
| `POST /returns/{id}/cancel` | Cancel only unsubmitted return units. |

The token determines the permitted character and game server. A conflicting
payload character is rejected. Quotes, manifests, returns and operation lookups
are owned by that stable subject and installation; opaque IDs are not authority.
Re-pairing the same installation retains its operation history. Cross-installation
recovery requires fresh game proof and an explicit bounded recovery operation.
Pairing intents declare ordinary issuance or recovery as their purpose before
the challenge is sent. Recovery proof produces a single-use grant for that same
server/character only. Recovery restores access to original records and their
idempotency namespaces; it never creates replacement shipment obligations.

Every supply/return mutation has a durable client operation ID, canonical body
and `Idempotency-Key`. Persist them before first transmission. The same owner,
key and body identify the same logical operation across credential expiry and
reconnect. A different body with the same key returns `409
IDEMPOTENCY_CONFLICT`. Durable unique constraints handle concurrent retries.
History/tombstones survive expiry and cancellation. Gateway failures never mean
an authoritative lookup found no operation.

Public persistence returns `202` with state `submitted`, meaning only that the
intent is durable. The private worker then atomically validates the binding,
quote, available stock, quotas and budgets, and records its reservation or a
rejection. Only its result may report `reserved`. A changed quote produces a
fresh offer requiring new consent; it cannot silently change accepted items.
This deliberately distinguishes public queue acceptance from stock reservation.

Check credential validity when accepting or reserving new work. Later expiry
does not cancel a durable accepted operation or erase its receipt obligations;
refresh character proof to regain authorized status access. Explicit revocation
blocks and cancels only remaining uncommitted dispatch. Already submitted or
uncertain units retain their holds and reconcile through the worker journal.
Record late worker receipts even when the originating desktop credential is no
longer valid. Fresh proof restores access without submitting the operation again.

Worker commands are a closed set: register pairing, validate/reserve supply,
cancel uncommitted supply, register return, reconcile known operation, and
publish bounded status. Per-unit dispatch and banking use existing controllers,
not generic remote commands. The worker validates schema/version, canonical
items, quantities, binding, destination, expiry and ownership again.

Use durable job IDs, atomic claims, bounded leases, fencing tokens and one
serialized game owner. Queue redelivery or lease expiry resumes the same worker
ledger operation; it never authorizes a repeated uncertain game action. A worker
must retain ownership through submitted-action reconciliation even if a public
lease expires. Persist results before acknowledging queue completion.

## Public access and bounded abuse

No mechanism can prove that a public client is an unmodified release or that its
claimed quest deficit is real. Character proof prevents another player from
simply typing a victim's name; it does not establish a real material requirement.

The private worker enforces eligible recipe/catalog quantities, per-character
allowances and outstanding quantities, one unresolved dispatch per character,
global stock reserves, daily parcel-fee and dispatch-unit budgets, and bounded
queued operations. Apply supplementary IP and installation rate limits to the
public service. Quotes use cached projections and cannot force repeated bank
probes. Public requests never trigger purchases or automatic replenishment.

Policy values live in private service configuration and are advertised only as
appropriate player limits. A catalog item without an explicit allowance is
unavailable. Missing global budgets disable new dispatch. Reservation, allowance
consumption and dispatch intent share the authoritative worker transaction.
Cancelled unsent units release only their corresponding uncommitted allocations;
uncertain units remain held. Client reports cannot reset a quota or trigger a
replacement shipment. Reinstalling or making new journey IDs cannot reset the
character's allowance.

Multiple genuinely controlled characters can still consume multiple allowances.
Public availability therefore accepts a bounded supply cost, capped by the
global budgets; it cannot promise zero stock abuse. This limitation does not
grant access to the private host or its broader commands. An operator kill switch
blocks new dispatch while retaining status and reconciliation work.

## Quest integration and inventory accounting

Use `QuestItemPreparation.Requirements` and its existing eligibility, activation,
branch, payment, bag and weight checks. Compute deficits from fresh authoritative
own inventory, not from catalog restocking needs. Never bypass Callough acceptance
or an unresolved quest/game transaction. Bind consent to the exact manifest,
character and generation; recompute if inventory or chosen work changes.

Scheduled or unattended work persists an awaiting-consent offer and yields using
the existing ownership/readiness rules. It never autoaccepts or waits on an
invisible blocking dialog. An expired offer needs a fresh quote and consent;
already committed supply work remains eligible for reconciliation.

Quoting precedes the optional preparation batch's purchase affordability filter:
an available accepted supply may reduce estimated purchases, while mandatory
quest payments and actual capacity/reward reserves remain mandatory. Quotes
alone do not reserve stock or justify spending reserved gold.

Deliverable stock is confirmed worker inventory plus confirmed withdrawable bank
stock, minus all commitments and holds. Other collectors' inventory, stale or
partial aggregate reports and expected returns are not deliverable stock.
Dispatch uses only the eligible catalog and exact canonical aliases. Unknown
stock remains unknown. Withdrawing and sending must not resurrect stale bank
counts or interfere with customer reservations and existing recovery holds.

Add optional provider and consent interfaces to `QuestContext`/`QuestRunner`,
with app composition in `DesktopRuntime.Quests.cs`. Keep the HTTP client in an
application service and game parcel operations in DarkAges. A disabled provider
preserves existing acquisition behavior and introduces no network dependency.

Persist a separate journey supply ledger, not only `QuestJob.Preparation`, which
is cleared after acquisition. Persist the journey ID, personal baseline,
accepted request manifests, stable parcel units, actual receipts, known gathered
or purchased surplus, consumption, later-quest reservations and returns. Share
the ledger across queue transitions and give standalone runs an equivalent
journey identity. Use the existing durable atomic-file conventions and serialize
updates under the owning runtime.

After verified receipt, recompute the actual missing quantities and acquire only
that remainder. A committed or uncertain request must be reconciled before
gathering possible duplicates. Missing configuration, unavailable service before
submission, no stock, unsupported capability or a decline uses normal preparation.
Disabling, Pause, Stop, disconnect, cancellation and application restart preserve
all submitted shipment and return obligations.

## Parcel qualification and recovery

The supplied one-unit packet fixtures are reference evidence. They do not prove
DAQuester support, a supported public NPC approach, arbitrary bulk quantities or
literal `/receive all`. No new parcel action is enabled from a synthetic test
alone. Use fresh NPC/page/slot/item arguments and the existing generation and
dialogue guards. Never hardcode a captured NPC serial.

Qualify source acceptance using actual item debit and correlated server parcel
acknowledgement. Track source acceptance separately from recipient arrival.
Persist each one-unit intent before submission; do not invent a bulk packet by
changing fixed bytes. Budget actual offered/observed parcel fees and preserve
required player gold reserves. Movement uses observed routes and immediate
departure from entry triggers.

Advertise receive-all only after success, empty, full-bag, weight, partial and
refusal behaviors are observed on the exact supported profile. It may collect
unrelated mail; explain that in the offer. Preserve those personal items. Credit
request units only within the source-accepted manifest and correlated own
inventory/sender evidence. Ambiguous identical-item mail stays unresolved.
An HTTP request ID is not a game parcel identity. Delayed receipts, manual
interference or generation changes trigger reconciliation rather than resubmission.

## Journey-end returns

The end boundary is the selected queue after ready work and saved continuations
are resolved, or a standalone journey ending. A quest transition, paid wait,
Pause, Stop or disconnect is not that boundary. Integrate queue cleanup and
scheduler logout safety under existing character ownership.

Compute deterministic attributable surplus after reserving everything needed by
remaining work. Never take personal baseline, equipment, rewards, escape items
or uncertain merged-stack quantities. Broader personal donations require a
separate player action. Distinguish supplied leftovers from newly gathered or
purchased journey surplus in the manifest.

Register returns before sending, then wait for the private worker to confirm the
accepted manifest and reserved mailbox work. Public `submitted` acknowledgement
alone does not permit a parcel send. Rejection or timeout preserves the unsent
checkpoint. Journal and verify every player-side send.
The private worker serializes collection with existing mailbox work, verifies its
own item gains and bank deposits, and updates stock only from those receipts.
Client reports cannot establish bank credit or renew a character's allowance;
any allowance credit also requires unambiguous sender attribution and protection
against double credit. Unavailable service preserves outstanding returns and
does not revoke earned quest completion.

## Interface, secrets and diagnostics

Use the shared dark theme and current consent-dialog patterns. Show player terms:
offered items, exact quantities, normal-acquisition alternative, arrival/return
status and provider attribution. No endpoint, token, protocol, calibration or
connection-profile fields go in the player window. Keep setup and protocol
details in protected Data and the existing control bridge.

Use a dedicated credential-store namespace; do not reuse saved game passwords.
Plaintext settings hold only the opt-in and public configuration. Redact secrets
before constructing diagnostics, including HTTP authorization, pairing challenges,
verifiers and raw upstream responses. Map upstream failures to safe branded error
codes while retaining every sanitized diagnostic line and required type-3 active
outcome. No credentials or private paths enter source, examples or release assets.

## Delivery and validation

The work has three coordinated deliverables: DAQuester client/ledger/consent,
public API with shared schema and contract tests, and a private-worker adapter
using the current local controllers. The handoff says some necessary worker
modules are untracked in its source environment; tracked repository source does
not establish their availability. Obtain that current source snapshot before
changing worker behavior. Existing website account verification is not the new
account-free supply pairing API.

Client implementation can proceed against a deterministic in-process fake HTTP
provider while the service and worker are prepared. Keep production dispatch
disabled until the actual official origin, explicit policy budgets, ready worker,
matching schemas and supported game capabilities are supplied. These are
deployment inputs, not extra player registration requirements.

Required coverage includes disabled/declined fallback, existing inventory,
partial/stale stock, concurrent final-unit reservations, unsupported quantities,
wrong-character access and object IDs, expired/replayed/wrong-sender pairing,
token loss/revocation, malicious job fields, budget exhaustion, duplicate POSTs,
credential expiry without history loss, worker queue redelivery, cancellation
during partial dispatch, every uncertain game commit boundary, delayed receipt,
full bag/weight, unrelated and identical-item mail, baseline preservation,
continuation reserves, separate quest success/return status, deposit-only stock
credit and recovery after restart/reconnect.

Run focused regressions and both complete xUnit projects for implementation:
`tests/DAQuester.Tests/DAQuester.Tests.csproj` and
`tests/DAQuester.AppTests/DAQuester.AppTests.csproj`. Manually review changed UI.
Packaging changes additionally require both release build and verification tools.
Builds and synthetic tests do not operate live characters, restart Excalibur or
modify its working copies. Any later live qualification is a separately
authorized activity using isolated quantities and characters.

## Sources and evidence limits

- User-supplied 28 October handoff: proposed workflow and embedded source/packet
  excerpts; explicitly not deployed endpoints or verified receive-all support.
- DAQuester working tree: preparation, atomic persistence, queue/scheduler
  ownership and DPAPI credential-storage implementation. Existing unrelated
  working-tree edits were not modified during this design.
- Connected source inspection: existing worker whisper observation and outbound
  ingestion are code facts, not proof that the new service is ready.
- [RFC 8252, section 8.5](https://www.rfc-editor.org/rfc/rfc8252.html#section-8.5):
  native clients cannot protect a shared distributed secret.
- [RFC 9700, section 2.3](https://www.rfc-editor.org/rfc/rfc9700.html#section-2.3):
  restrict token audience and privileges. The proposed game-pairing protocol is
  a new integration design, not a claim of an existing OAuth implementation.
