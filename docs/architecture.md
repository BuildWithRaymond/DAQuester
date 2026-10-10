# Architecture and extension contract

Recipes select their own action handlers. Mundane Birth uses `MundaneBirthActions`
and immutable per-character `BirthOptions`; Spare a Stick retains its existing
handler and calibration. Callough's permanent-outcome check applies only to the
stick recipe. The app keeps one runner per character and awaits the previous
owner before switching quests. Each quest has a separate journal key.

Birth records a pending transaction before its final town choice. A fresh server
status notice (0x0A) stores generation, revision, message type and observation time.
Only the recognized legend-confirmation bar text is recorded; whispers and other
messages are excluded. Confirmation reconciles this notice against the saved
submission and never resends after ambiguity. An unfinished conversation restarts
safely with saved settings before the final submission.

The proxy forwards gameplay and serializes automation through the same upstream send lock. Each accepted connection owns encryption, ordinals, movement counter, cancellation and immutable state. A redirect gets a unique one-shot listener tied to its actual remote endpoint. There is no shared redirect FIFO that could hand one player's connection to another.

Automation checks cancellation before acquiring and using the send lock. Once a frame begins writing it completes with the connection lifetime token, avoiding partial frames when the player pauses. Old connections cannot send using a new generation.

Every server response reaches the game client. Automated movement additionally presents Excalibur's self-walk animation after its server acknowledgment. A corrected move presents authoritative position. Synthetic packets share an ordered client send path; later server traffic is re-encrypted with its own client-facing ordinal sequence. Manual walking keeps the game's own prediction and does not get a second animation. Synthetic presentation never changes observed character state or proves quest progress.

Character identity comes from the client's transfer handshake name plus the server's 0x05 avatar ID (DATester 7.41), with 0x52 accepted where available. Stats remain nullable until observed. Inventory slot packets replace absolute quantities. NPC entities and dialog choices keep their current IDs. Merchant choices return their server pursuit ID; pursuit responses use received pursuit ID, received step plus one and the selected option.

Map collision comes from the client's lod map files and sotp.dat inside ia.dat. Missing data blocks movement. Portal graphs contain 1,432 local Excalibur map records. Grid paths avoid entities and unintended portals. Known portal endpoints may be wall-flagged door sprites, so only that recorded destination gets Excalibur's endpoint exception. Intermediate walls remain blocked. Movement waits for a server acknowledgment or map change, replans on correction, and stops after bounded recovery.

Player appearances, object movement and removal update occupied tiles. Navigation
rechecks the next tile after cadence waits. Rejected walking or missing movement
evidence triggers a real F5 refresh, never a Look interaction. Consecutive
recovery attempts reset after acknowledged progress. A temporarily inaccessible
patrol target yields to the next waypoint and is retried on later laps; static
route failures and critical travel destinations retain bounded recovery.

NPC visits use the current entity position and a reachable interaction tile within three tiles. Callough's guide coordinate (6,7) is behind the counter; (6,8) was unreachable. The live grid instead selects (6,10) from the weapon-shop landing (6,13). A map correction during approach pauses instead of navigating old coordinates in the new map. Approaching promptly clears the entrance before doorway pushback.

A QuestDefinition names a version, capabilities and steps. The runner's action registry resolves each step to an IQuestAction. New recipes must use authored data and handlers; website prose never executes. Transitions may be implemented by an action using observed state and checkpointing. Unsupported capabilities stop before commands. Add a handler and characterization/replay evidence before claiming a new capability.

Each action reconciles current state, saves pending consequential transactions, sends necessary commands, waits for matching server evidence, and saves progress. Journals use a temporary file, forced flush and atomic replacement. Restart restores jobs paused. Completion is a validated Callough terminal response plus a six-branch decrease and an experience increase after the recorded baseline; existing reward items cannot prove it. Reward-driven level gain does not run the preflight level gate again during confirmation.

The recorder exports only whitelisted server state packets, snapshots and parsed NPC selections, excluding login packets, encryption negotiation and session secrets. Exact acceptance/completion text and observed patrol coordinates are server-profile calibration. Automatic calibration correlates selections, replies and inventory/experience changes before enabling the recipe. Optional refusal/cooldown samples accumulate later; unmatched responses always pause.

MCP and the local CLI share a current-user named pipe into MainForm's services. Each character has one control or quest owner; actions use the same GameSession send path. Manual input, Stop and disconnect cancel that owner. MCP does not own a separate game connection or bypass observed state. Calibration runs in the background and exposes its durable job through state queries.

Spare a Stick banks carried gold down to 999, confirms acceptance, collects missing branches, turns in, and records completion, experience and actual item deltas against the pending transaction's inventory baseline. Inventory goals are checked before each movement send, including after cadence waits, so a long patrol segment stops when six branches are observed. Reward recording uses the terminal reply's snapshot after split XP/item packets. Optional repeats wait eight hours from verified completion; server refusal still blocks execution.

Acceptance is scoped to connection generation: reconnecting reactivates collection before resuming gathering. Pending transactions block this reactivation until reconciled. Patrol rotates all three player-confirmed areas; observed drop positions are separate evidence and never narrow the configured route. Both normal collection and calibration keep rotating through random no-drop periods until six branches are observed. There is no branch-progress deadline. Eligibility, cancellation, and movement acknowledgment/recovery still govern execution.

Callough's child-reminder pursuit reply is a player-specified permanent outcome, now captured live on Clock. The journal saves its exact text and dialog identifiers, disables repeats, and retains any unproved pending transaction for audit. A correlated turn-in still records actual XP/item rewards. Passive observation first cancels and awaits the current writer, then reads its final checkpoint and records the permanent outcome. A permanent job stays terminal across restart, Pause and Start even if Repeat is selected.

Party coordination, combat credit, permanent choices, expiring stages and other catalog capability families require additional handlers. They are intentionally unsupported until implemented and evidenced.

Perfect Hairstyle uses captured Eeva menu choices and only recognized Next pages.
Travel acknowledges the exact type-4 town-entry notice with Next before retrying
movement. Final submission is checkpointed once; Eeva's validated thanks reply
and correlated rewards prove an attempt even when that reply still has Next.
The own-legend read decides whether the goal is complete. Non-legend outcomes
append a durable attempt with actual reply time and rewards, then wait seven
days. Pause/disconnect preserves the date; restart restores paused. A verified
Suomi legend stops weekly retries. Unproved transactions never resend automatically.

Entered Muisir Village uses a separate `MuisirActions` handler and non-repeating
recipe. Known unregistered own legends block before gathering; level 1+ is valid.
Ground items retain server ID, sprite and position separately from blocking
entities. Collection prefers reachable sprite-569 mushrooms, rechecks after each
acknowledged step and picks up with the pinned Get layout. Removal alone cannot
prove pickup; inventory quantity must change. Approaching Kiril uses this same
collector before wider town patrol. Players trigger detours and temporarily
inaccessible items are revisited on later passes.

A Muisir speech transaction saves inventory, own legend, connection generation
and current Kiril entity before sending Tiny Cave once. Only captured Kiril or
player-named pursuit pages with matching entity/generation advance. The final
introduction page saves a distinct `muisir-briefing` transaction. A new claim is
allowed only after a fresh own-legend read and unchanged mushroom/potion counts
prove that introduction did not award anything. Repeated introductions are
bounded. A timeout or unknown page preserves the original pending claim.
Completion requires a new own mark, exactly five consumed mushrooms and four
new potions relative to the baseline; actual terminal page and legend are saved.
