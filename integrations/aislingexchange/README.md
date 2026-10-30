# AislingExchange supply broker

Standalone .NET 10 public service plus an outbound-only Node 22 worker adapter.
These sources do not deploy a service, expose an existing private gateway, or
start a game client. Production dispatch remains disabled until the worker has
qualified the game profile and its existing scheduler owns the integration.

## Boundaries

The public process stores paired credentials, quotes, bounded submitted intents
and worker status projections. It cannot run commands, forward arbitrary URLs,
send packets or open files requested by a client. The private worker initiates
HTTPS requests to fixed `/worker/v1` routes and uses its existing authoritative
ledger and serialized game owner. The worker has no new inbound listener.

The public hostname must terminate on a separate public edge. Do not point it at
the private machine, reverse-proxy its existing gateway, or expose private
addresses through DNS. The broker has no upstream/private-host setting and no
public administrative routes. HTTP errors contain fixed codes only; Kestrel's
server header and development exception output are disabled. Any external edge
must also suppress upstream error bodies and internal/server-identifying headers.
`supplies.aislingexchange.com` is the dedicated public endpoint. It is deployed
with an empty catalog and no supply readiness behind a tunnel that forwards only
the fixed API prefixes. Origin TLS is validated; admin and metrics paths are not
routed. Existing website/API hostnames use Cloudflare proxying separately. This
does not erase historical DNS or conceal other services sharing the server.

No distributed application secret exists. Automatic character pairing uses a
five-minute `AEQ-` nonce plus a locally protected verifier. The challenge is
base64url SHA-256 of the UTF-8 **verifier text**. Only a worker-authenticated
direct-whisper observation can authorize issuance. Pairing status, completion
and lost-issuance revocation require that verifier in a JSON body over HTTPS.
Character credentials are opaque, hashed at rest and valid at most one hour
after the observed proof. No website account is required.

The desktop can lie about requirements and receipts. The private callback must
enforce its own catalog, recipe quantities, stock holds, per-character allowances,
outstanding work, global stock reserve, dispatch-unit limits and parcel-fee
budget. Multiple controlled characters can still use multiple allowances.
Client reports never create stock or renew allowances.

New supported parcel sends use server-calculated postage; an unknown or changed
item fee does not reject stock admission or return registration. The private
daily fee threshold counts each immutable completed source proof's actual charge
once, on its original UTC proof day. A charge may cross the threshold; it settles
before later sends stop. Receiving and banking returns consume no invented
outbound fee allowance. See the [approved actual-postage policy](../../docs/superpowers/specs/2026-10-29-aislingexchange-actual-postage-design.md).

## Running the public process

Required protected service environment:

| Variable | Value |
| --- | --- |
| `AISLINGEXCHANGE_STATE_DIRECTORY` | Absolute directory on a protected persistent local volume |
| `AISLINGEXCHANGE_GAME_SERVER` | Agreed profile identifier; no inferred default |
| `AISLINGEXCHANGE_WORKER_TOKEN_HASH` | Hex SHA-256 of a separately generated, at least 256-bit worker bearer token |
| `AISLINGEXCHANGE_CATALOG_LIMITS` | JSON object mapping explicitly allowed canonical item names to per-request limits, each 1–100 |

Only the private worker receives the plaintext worker token. The hash is
insufficient for a desktop credential and desktop credentials cannot call worker
routes. Protect environment and state files with the service account's filesystem
permissions. Never place these values in source, release examples or ordinary
logs. An empty eligible catalog is valid and cannot dispatch supplies.

Configure Kestrel with a real HTTPS endpoint and certificate using standard
ASP.NET hosting configuration. Plain HTTP receives `HTTPS_REQUIRED`; there is no
credential-bearing redirect and arbitrary forwarded-scheme headers are ignored.
Use TLS passthrough or an explicitly reviewed local TLS configuration. No CORS
or general browser API access is enabled.

```powershell
dotnet publish integrations/aislingexchange/src/AislingExchange.Broker.csproj -c Release
```

The JSON store uses a process-exclusive lock, one serialized writer, a flushed
temporary file and atomic replacement. Run **one process on one local persistent
volume**. Do not put the state directory on a network share, start multiple
replicas, discard the volume, or restore old state while the worker can act.
The worker ledger remains the authority after any restore. State-file corruption
fails closed. Capacity exhaustion rejects new work and retains existing records;
operation history is not automatically discarded.
The state file has a 16 MiB hard capacity; exceeding it rejects a transaction
before replacing the existing state. Plan reviewed storage migration before
that limit is reached.

Fixed abuse limits include 16 KiB JSON requests, depth 12, 32 unique item lines,
100 total units, bounded pairing/quote/job/history counts, and bounded per-IP and
global request counters. Duplicate and unknown JSON members are rejected. These
controls supplement the worker's durable economic budgets; they do not replace
them. Quotes are bounded cached offers, not bank probes or reservations.

## Shared wire contract

Public prefix: `/api/quest-supplies/v1`. JSON names use camel case. GUIDs are
durable client/server IDs. Public status never includes worker hostnames, IPs,
paths, raw upstream replies or private stock totals.

| Route | Authentication and behavior |
| --- | --- |
| `POST /pairings` | Public, rate-limited; installation/client IDs, character, gameServer, verifierChallenge, purpose `quest-supplies` or `recovery` |
| `POST /pairings/{id}/status` | `{verifier}`; returns registering/ready/observed/expired |
| `POST /pairings/{id}/complete` | `{verifier}`; pending or paired credential; lost response requires explicit issuance revocation and fresh proof |
| `POST /pairings/{id}/revoke` | `{verifier}`; revoke lost issuance without another whisper |
| `POST /credentials/revoke` | Supply bearer; revoke this installation and request cancellation of uncommitted work |
| `POST /recoveries` | One-use recovery bearer from fresh recovery-purpose proof; original installation IDs, no replacement obligations |
| `GET /capabilities` | Supply bearer; bounded catalog and qualified profile/modes |
| `POST /quotes` | Supply bearer; character, gameServer, journeyId, `{item, quantity}` lines |
| `POST /requests` | Supply bearer + UUID Idempotency-Key; clientOperationId, same journeyId, character/gameServer, quoteId, accepted lines |
| `POST /operations/find` | Supply bearer; `{clientOperationId}`, authoritative owned lookup |
| `GET /requests/{id}` | Owned supply status and stable unit IDs |
| `POST /requests/{id}/receipts` | Owned client report; clientOperationId and unitIds, never delivery authority |
| `POST /requests/{id}/cancel` | Owned clientOperationId; only worker may settle/release uncommitted units |
| `POST /returns` | Supply bearer + idempotency; journey, source request IDs, `{item,quantity,origin}` lines |
| `GET /returns/{id}` | Owned return status, collection and bank receipts |
| `POST /returns/{id}/sent` | Owned client report; clientOperationId and unitIds |
| `POST /returns/{id}/cancel` | Owned cancellation intent |

Every supply/return mutation requires a UUID Idempotency-Key. The persisted
canonical body, route, stable owner and client operation ID prevent changed-body
reuse. Re-pairing preserves installation identity. Cross-installation recovery
uses fresh, purpose-bound proof, revokes lost installations, and restores access
to original operation namespaces.

`202 submitted` means only durable public acceptance. It does **not** mean
reserved, dispatched, received or registered for returns. The private worker
later publishes `reserved` or `registered`, or a bounded rejection. A return
must await `registered` before the player sends anything. Source acceptance,
recipient observation, collection and banking remain distinct unit flags.
Client receipt flags remain reports. Uncertain units cannot be cancelled as
unsent, and only actual bank deposits make returned stock available again.

A source-confirmed supply in `sent`, or `partial` with every unit independently
accepted or cancelled, releases its outstanding dispatch slot. The operation
still accepts later recipient updates; its generic terminal-state rules are
unchanged. Client receipt reports do not establish recipient proof, stock credit
or renewed allowances. Repeated receipt/sent reports use their original report
IDs and immutable unit lists after lost acknowledgements.

The worker contract and embedding requirements are in [worker/README.md](worker/README.md).
No parcel protocol, receive-all capability or public game approach is qualified
by these synthetic tests.

## Verification

```powershell
dotnet build integrations/aislingexchange/tests/AislingExchange.Broker.Tests.csproj --no-incremental
dotnet test integrations/aislingexchange/tests/AislingExchange.Broker.Tests.csproj --no-build
node --experimental-strip-types --test --experimental-test-isolation=none integrations/aislingexchange/worker/adapter.test.ts
```

The Node test isolation flag avoids spawning a child process in restricted
workspaces. Tests use synthetic characters, temporary files and in-memory worker
ports. The host test binds loopback only. None operate game characters, restart
clients, deploy a service or change a private host.
