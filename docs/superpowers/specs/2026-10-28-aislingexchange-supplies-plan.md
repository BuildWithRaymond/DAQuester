# AislingExchange supply implementation plan

Approved design: [quest supplies](2026-10-28-aislingexchange-supplies-design.md).
No private infrastructure identifiers, credentials or endpoints may appear in
public client code, API responses or documentation. Only the public provider
origin is a desktop connection target. The private worker initiates outbound
connections and keeps its existing control/admin routes separate.

1. Add deterministic security and recovery tests before implementing each unit.
   Preserve unrelated working-tree edits and all existing quest policies.
2. Add public wire models, bounded HTTPS transport, safe branded errors and
   isolated DPAPI credential storage. Test wrong origins, redirects, malicious
   response bodies, invalid manifests and lost mutation responses.
3. Add optional Core provider/consent and qualified parcel ports. Persist journey
   provenance and HTTP operation identity outside transient preparation. Test
   exact missing quantities, partial receipt/remainder, decline, disabled mode,
   pending ambiguity, continuation reserves and recovery.
4. Compose the provider into existing desktop ownership. Add a default-off
   player option and exact-manifest consent using current dark controls. Keep
   connection diagnostics and service configuration in Data/control tools.
   No unqualified game packet or bulk operation is advertised as supported.
5. Add an isolated public broker with a durable bounded intent queue and
   verifier-bound game pairing. Implement owned idempotent status/return paths
   and a separate authenticated outbound worker contract. Keep game reservations
   and receipts authoritative in the private worker, not in the broker.
6. Provide sanitized API/worker setup and protocol qualification instructions.
   Actual worker controller integration requires its current source snapshot;
   production deployment requires an official public origin and explicit budgets.
7. Run focused regressions, broker tests and both complete existing xUnit suites.
   Manually inspect interface changes using isolated synthetic app state. Run
   release tooling only if packaging changes. Do not operate real characters,
   change Excalibur working copies or restart processes as part of verification.

The referenced writing-plans skill is not installed in the available skill
roots; this plan records the approved work directly. No additional design
approval is required for routine implementation choices within this scope.
