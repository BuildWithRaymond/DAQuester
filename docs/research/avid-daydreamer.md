# Avid Temuairian Daydreamer

Visit **Celesta in Pravat Cave**, choose **Avid Daydreamer**, and finish her
offered check-in page. DAQuester uses the existing Pravat South Entrance route,
then resolves Celesta and her current dialogue from the server.

Each queue run makes one daily attempt. An optional recurring schedule can log
in, check in and log out using the existing saved-login ownership and consent
rules. The next attempt waits at least **24 hours** after the server's reply and
until the next midnight in the saved calendar zone (America/Chicago by default).
The server's reported count is saved; DAQuester never adds a day locally just
because time passed.

The first goal completes when Celesta's correlated server reply confirms
**30 or more consecutive logins**. No additional prize opening or legend
conversation is needed for that completion rule. It stops at this goal by
default. **Continue daily check-ins after the first mark** explicitly permits
later daily attempts while retaining the first verified goal. Daily scheduling
and continuation after the first goal are separate settings.

## Registration and repeated marks

The player reports registration is required throughout all **30 days**, with no
known level requirement. A 30-day registration code needs every day's check-in;
if you already missed a day, a second code may be needed to finish. DAQuester
requires fresh registration status from the current login. A displayed
month/day expiration has no year and does not establish 30 remaining paid days.

The login server can send registration before redirecting to the game server.
DAQuester retains that original notice with its original generation, revision,
type, text and time. A one-shot redirect transfers it only after the replacement
client handshake matches all redirect metadata and the game server supplies the
character's own identity. The opaque redirect cookie is not the avatar ID.
The binding expires if not adopted within 45 seconds and is cleared on a new
login, mismatched or repeated handshake, disconnect or identity replacement.
It is session memory, never a saved account-wide registration flag.
Celesta choice and check-in sends recheck this status after waiting for ownership
and immediately before writing, so a cleared receipt also stops a queued action.

The player reports the first 30-day cycle earns **Avid Temuairian Daydreamer -1**,
and another 30 earns the next numbered mark. They also report a bug where the
mark number may return to **1 around mark 27** after repeated cycles. The exact
rollover is uncertain. Consider this before enabling continuation after the
first mark. DAQuester keeps the actual server count and the original completion
receipt; it does not manufacture a later mark number or alter the server's mark.

## Evidence and recovery

The supplied October 28, 2026 VPS reference describes an existing private worker controller
and packet primitives. Its sample identities, pursuits and steps are synthetic.
It is source-inspection evidence, not a new DATester 7.41 packet capture or a
live automatic run. The source identities are preserved in the sanitized
[reference summary](../../content/evidence/datester-7.41/avid-daydreamer-reference.json).
The player's later optional-repeat choice supersedes the reference's unconditional
stop-after-30 policy. No Discord notifications or unrelated reward operations
are imported.

The offered greeting is `Thank you for dropping by. I'll make sure to check you in!`.
The result recognizer accepts the server's ordinal or bare positive count in
`This is your 30th consecutive Login!` or `This is your 30 consecutive Login!`.
Only a fresh result correlated to a submitted check-in changes progress.
An already-checked-in reply can acknowledge the day without supplying a count.
It preserves the known count and schedules a conservative 24-hour wait.

Pending state is saved before Next. An uncertain reply or reconnect retains that
submission for reconciliation instead of sending Next again. A verified result
and terminal popup closure have separate saved state. Closing resumes without
another check-in.

On October 29, a read-only inspection of an existing client replay established
that the login server sent a successful login response and a separate active
registration expiration notice, then redirected to a new connection before the
own identity arrived. The redirect metadata matched that connection's handshake,
including one optional trailing NUL. This exposed the missing transfer between
the two trackers; no check-in was submitted during this inspection. Synthetic
loopback tests reproduce this sequence, including immediate lobby disconnect,
fragmented frames, invalid login replies and mismatched/replayed handshakes.
They verify the receipt transfer, not a live automatic Celesta run. Dialogue,
status-channel behavior and a full 30-day automatic journey still need live
qualification on this client.
