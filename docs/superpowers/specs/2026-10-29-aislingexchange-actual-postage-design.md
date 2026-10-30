# AislingExchange actual parcel postage

The player directed that an unknown item fee must not block an otherwise
supported item. On 29 October they selected actual postage accounting, explicitly
accepting that one send may cross a gold limit. This supersedes the earlier
implementation's exact-known-fee prerequisite. It does not enable unqualified
protocols, arbitrary items, or production dispatch.

## Behavior

New sends use server-calculated postage. A remembered item/profile fee is an
optional observation, never an eligibility requirement, flat fallback, zero
substitute or binding prediction. Known fees can change without making a proven
send uncertain. Catalog eligibility and return registration do not depend on fee
availability. Explicit protocol/route/source-receipt capability remains separate
from optional fee information, including stack support.

Send one unit under the existing owner, cancellation, generation, identity,
dialogue, full-bag, selected-slot, manual-input and durable-intent guards. Record
gold before submission. A qualified fresh source acceptance, exact one-unit
debit with all other items unchanged, and matching actual gold evidence establish
the committed send and its actual postage. Where the native receipt states the
paid amount, it must equal the measured debit. A net gold increase, missing or
contradictory evidence remains unresolved. Zero cost is evidence, not a default.
Both clients require an independently received post-submit gold-bearing
observation, including a fresh unchanged balance for zero cost. A newer dialogue
or bag revision carrying cached gold is insufficient.

The observed Wolf Claw debit of 2,100 gold is one item/profile transaction. It
does not price any other item or qualify a recipient collection or bank deposit.

## Accounting and limits

The protocol reveals postage after payment and has no maximum-fee field. The
selected policy therefore uses a stop-before-next-send threshold rather than
claiming an unenforceable precommit cap. Preserve hard item reserves. Require
fresh actual resources and apply existing non-fee admission/ownership limits.
After a proven send, durably settle its item and actual cost before evaluating
whether remaining gold or the accumulated fee threshold permits another send.
A remaining balance at or below the protected reserve stops another public
return send. Public returns retain that existing boundary without adding a new
postage-limit setting. Private outbound work uses its existing daily threshold:
actual accumulated charges at or above it stop another send.
A charge crossing a threshold or protected gold reserve is still a committed
send; do not lose its receipt, resend it, or falsely report a hard reserve
guarantee. Pause later sends and re-evaluate funded work from actual resources.
No automatic gold banking or extra fee-wallet journey is introduced.

Private fee accounting counts actual outbound charges exactly once. Receiving
and banking a registered return do not reserve an invented outbound fee. Stock,
customer obligations, per-character unit allowances, authenticated callers and
global financial-action serialization retain their existing rules. Unresolved
physical actions keep durable ownership and cannot be bypassed by day rollover.
Assign each private charge to the UTC day of its first immutable completed
native source proof, not request admission or receipt-RPC retry time. Freeze that
timestamp with the canonical proof; retries cannot move or duplicate a charge.

## Durable identity and recovery

New public intents explicitly distinguish server-calculated postage from legacy
fixed-quote intents. Existing persisted quote/zero semantics remain unchanged;
never reprice, relabel or resubmit an old pending intent. Persist actual fee and
before/after gold with the original per-unit source receipt before reporting.
Collection, source acceptance and bank deposit remain distinct receipts.
Once original observations prove a source send, later popup cleanup failure,
manual activity or cancellation cannot demote that proof. Save cleanup as a
separate obligation. Settle the original unit and cost even when newer current
resources differ; preserve those resources and hold uncertain attribution before
another send. Read-only reconciliation can observe that cleanup is no longer
needed, but cannot close a popup or repeat a financial packet.

Private outbound completion needs an immutable canonical source-receipt outbox
before receipt RPC, including original operation/session/intent identity, exact
bag proof and independently received gold versions. A lost native-receipt or
outbox-clear acknowledgement repeats only the same bookkeeping proof. A current
authenticated owner may settle the exact original completed proof after a
reconnect; it may not substitute newer evidence, mutate proof or overwrite newer
own stock/gold projections. Cancellation and stale cleanup release only their
exact owned controller token.
Completion and the source outbox must be durable together, or the outbox must
reconstruct deterministically from the original durable completed native proof,
including its original gold observations and proof timestamp. Recovery may not
fill a crash gap with later current gold.
An existing completed source outbox may settle while new supplies are disabled.
That recovery uses the current authenticated owner and original proof only;
without an outbox it performs no work, and it never claims a job, publishes a
catalog or invokes a native financial action while disabled.

## Validation and rollout

Use RED-to-GREEN regressions for unknown and changing fees, two differently
priced items, explicit zero, inconsistent/missing gold, thresholds crossed by one
send, immutable legacy intents, lost acknowledgements and reconnects. Verify
unsupported protocols still fail before packets and reports never create stock
or renew allowances. Run focused checks and both full public xUnit suites, then
private compile/regressions and the isolated Linux fixtures on a new immutable
candidate. Preserve previous candidates and evidence. Production remains off
until the separate live receipt and complete release qualification gates pass.
