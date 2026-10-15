# Blanchefleur's Fiosachd referral

The authorized 2026-10-14 live segment captured an original-opening variation
after Blanchefleur's exact `Where did you get it?` choice. Her reply describes
a sailor and a tale from Abel involving Fiosachd, rather than the original Tir
referral. The route is selected from that exact fresh reply, never from class
or an inferred reason for the variation.

The offered Next reaches `I paid little attention to him.` The player approved
`Something involving Fiosachd?`. Its reply refers to Fiosachd and his acolyte
Aundy, then offered Next pages return to the sailor paragraph and the same
two-choice menu. The captured route explicitly closes that returned menu. It
does not select the clue again or skip to Tir.

Keefe's answers and Logan's Cap pages then matched the existing Loures route.
They join at the Mehadi Cousin. Research stopped on a stable map-3071 tile
(3,2), after closing the Cousin's unpaid 1,000-gold quote. No payment, Gland
handoff, rite, later assignment or final completion is inferred by this segment.

Evidence is retained in `danaan-blanchefleur-sailor-referral.json`,
`danaan-blanchefleur-fiosachd-live.json` and
`danaan-blanchefleur-recovery.json` under `content/evidence/datester-7.41`.
The last file projects actual submitted actions and owned server states,
including null-dialog closure receipts. Actor and generation are normalized;
exact revisions, identities, page text, choices, gold and XP remain unchanged.
Unrelated inventory, entities, credentials and other-player data are omitted.

Recovery is narrowly scoped to the old pending Blanchefleur origin selection.
It requires the original source page, the entire informational tail, Keefe and
Logan, and each actual server closure in the original generation. It saves a
receipt and continues at Cousin without sending any game command. Missing or
conflicting evidence keeps the submission pending. Newer later-stage actions,
including Cousin payment, prevent an older replay from rewinding progress.

Logan's first approach stopped at Wizard Hall (398), (6,9); a later own state
showed corridor 397 (7,2) and no popup. The fresh-page guard rejected the stale
selection before sending. After navigating to (6,8), the complete conversation
remained in Wizard Hall. The entry correction uses that proven interior from
the observed (6,9)/(6,10) entry tiles before allowing a visible-NPC shortcut.
The map change is observed; its precise client/server timing is not inferred.

Synthetic tests cover branch selection, the informational loop, reconnect
recovery, missing/conflicting receipts, Varuna and Donnan continuation, and
native doorway departure. This segment does not certify a fresh full automatic
run or an observed Band reward.

## Local NPC continuation after the opening

The resumed automatic run exposed a second independent variation: Varuna
returned the already captured `Many spirits roam in a dark land.` ending,
rather than the Loures gatekeeper paragraph copied into the new path. The
opening did not determine this later reply. The Gland was already handed over;
the authorized final informational Next closed Varuna without another delivery.
`danaan-blanchefleur-varuna-spirits.json` retains the actual full stage,
checkpoint, handoff and closure under a research alias.

The runner now resolves an unfamiliar informational successor against every
captured version of that same task. NPC/map, resource contract and every
already-submitted page must agree. A unique exact fresh reply selects only the
remaining continuation, preserving completed opening steps and the saved
transaction. Its source pages and original dialog identities are saved and
revalidated on reload. Ambiguous, unknown, stale or choice-bearing successors
do not authorize a fallback. Explicit captured choice branches remain separate.

The spirits ending therefore follows the observed Greim warning regardless of
the earlier opening. Tests mix that ending with Blanchefleur and both Loures
openings, preserve the existing gatekeeper paths, recover the actual stopped
Gland stage across reconnect, and reject conflicting handoff contracts and
rewritten prefixes. This supports observed combinations rather than assuming
all later dialogue from an early character-specific path.

## Dreamer return without a fixed inn

The next automatic segment completed Greim's warning, the supplied-Mold rite
and all Dreamer pages. The final Next changed the map from the spirit realm
720 to Mileth Threshold 3079, with authoritative position (3,1). The old fixed
inn-destination check stopped there. The player explained that a threshold is
the inn substitute for characters without town citizenship, and corrected the
departure direction to Up. That bounded up step was accepted; the capture also
retains the later actual world-travel map change without inferring another
quest transaction.

`danaan-dreamer-threshold-return.json` keeps the exact final source page,
submitted Next and original owned map/position receipts. A map change after
that bound final prompt now proves return without a destination whitelist.
Position is still awaited before movement. Known inn departure steps remain
scoped to their observed maps; other returned maps use normal navigation,
including the threshold's up exit to the world map. Verified Dreamer server
redirects also accept the actual changed map rather than a fixed inn list.

Reconnect recovery requires the original final Next, original source state
and actual return receipt, with unchanged gold/XP and no later handoff. It
advances to the Avatar without resending Dreamer's choice or final Next.

## Resumed automatic completion

After the return fix, the existing queue reconciled the original Dreamer
receipt and continued automatically through Cail, Glioca, Meaveen, Donnan,
Kyros and Erin. Donnan confirmed both the intermediate task and final
completion. No manual dialogue was needed in this final segment.

`danaan-blanchefleur-resumed-completion.json` retains Erin's exact reward
selection, offered Next and terminal close, with owned server receipts. XP
increased from 864,703,409 to 865,703,409 and the active message stated
`1000000 experience!`. The Cap arrived in inventory before the fresh own
`Worthy of the Cap of Danaan, Deoch 223, Summer` legend. The final consecration
information popup closed on the server; consecration was not selected. The
queue recorded completion and moved directly to Fisk's Secret. A later own
equipment snapshot showed the Cap equipped.

Fisk's bouquet acceptance then established a paid wait until 2026-10-15
03:55:04 UTC. The queue yielded to its already queued Shinewood Faerie,
verified Pet Faerie acquisition and Rucesion Song return, and parked at
Tavern 413 (10,1), facing down, while preserving Fisk's due time. Bouquet
collection and final Fisk completion have not yet been observed in this run.

This is a verified resumed Cap completion. Earlier discovery and checkpoint
recoveries remain part of its history; it does not certify a new uninterrupted
automatic run or a live Band reward.
