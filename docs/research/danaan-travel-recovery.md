# Danaan travel recovery — October 14, 2026

Pixel's automatic run paused repeatedly during position refresh, the supplied-Mold
transport, the Dreamer return and the final Path Temple approach. Native MCP was
used read-only to inspect the existing log, queue and own state. No live commands
were sent during implementation or synthetic validation.

The final approach latched the exact active status `You are not a Monk.` as an
entrance refusal. The player identifies it as the Monk Nightmare tile response.
The sanitized [capture](../../content/evidence/datester-7.41/danaan-monk-nightmare-status.json)
confirms Path Temple map 395, the active message and the queue's repeated block.
The later position does not establish the precise trigger tile when the message
arrived. No final Danaan item, XP, legend or popup closure is proved here.

Code inspection found three timing problems. F5 stopped after one unanswered
five-second refresh. Submitted Next waited for a reply on the old map even when
the server transported the character. Transport reconciliation could accept a
closed popup before the landing coordinates arrived and skip its inward step.

The updated walker retries only read-only F5 while the original connection
remains live. It sends no new movement until an authoritative position or world
map receipt arrives. A late refresh remains valid against the original baseline;
HP updates alone do not release movement. Pause/Stop cancels the wait, and actual
disconnect or replacement preserves the generation gate and saved checkpoint.

A captured automatic Next is submitted once and may wait through temporary
latency until cancellation. Its reply can establish a map transition. Final
transport reconciliation retains the submission until a declared destination
has actual landing coordinates, then uses the existing resource proof and inward
step. Unexpected maps or fresh unknown popups retain the pending submission and
pause without guessing a step or resending the action. Payment, handoff, choice,
reward and terminal popup proof requirements remain intact.

The exact Monk message on map 395 no longer latches an entrance block. It stays
in active status, diagnostics and replay evidence. Actual armament refusals,
the armed-shrine rule and other class refusals retain their existing guards.

Regression fixtures use synthetic sessions and encrypted loopback protocol
traffic. They cover an unanswered first F5 followed by a real position, unrelated
HP packets, delayed single-submission Next, map transition, missing landing,
unknown destinations/popups, cancellation, disconnect and the Monk status.
They do not certify a fresh automatic live completion or a Band reward.
