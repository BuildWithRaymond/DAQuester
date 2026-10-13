# Manual input no longer pauses automation

The user explicitly requested removal of the automatic manual-play pause behavior.
Ordinary client movement, turns, merchant/pursuit replies and NPC clicks are
forwarded without emitting a session interruption. A manual walk reanchors
pending movement through server-confirmed position; navigation continues.

Remove Scheduler's manual-takeover cancellation and cleanup exceptions for both
the scheduled character and helpers. Scheduled work still closes the exact game
windows it opened when the run ends, including after player interaction. Keep
explicit Pause/Stop controls, disconnects, redirects, generation isolation,
ownership checks, durable checkpoint writes and evidence reconciliation.

Update player warnings and current documentation to explain this behavior.
Synthetic transport tests verify all affected packets and continued fast
navigation. Existing interruption fixtures continue covering connection/protocol
cancellation and saved evidence without representing ordinary player input.
Run focused tests, both complete xUnit suites, and review the native warning
preview. Apply the tested build only when DAQuester has no active work or clients.

The separately reported pending Callough turn-in retains its checkpoint. Read-only
inspection found no matching original-session evidence in the current replay
folder or older workspace replay copies. Do not reset or resend that submission
as part of changing manual-input behavior.
