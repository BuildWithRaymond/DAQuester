# Danaan captured-run audit, 2026-10-14

The player's Glioca and Donnan failures exposed two implementation omissions.
The NPC cache can retain off-screen mundanes, and Lancelot's observed Donnan
referral was absent from the recipe. These are recorded behavior, not changes
in the game or hypothetical dialogue variants.

`danaan-lancelot-variation.json`, observation 140, contains the exact referral:
`A meditative master of colorful martial arts trained too much for his equipment's good.`
It follows `You have completed it. Good! So, for the final task...`. The retained
original run instead gives the Ranger's unused-jail referral. Both refer to the
same observed Kyros task and now belong to that exact page in all three Danaan
journeys. Neither actor class nor pursuit/step numbers select the wording.

The captured-run tests read incoming dialogue from retained evidence independently
of the recipe. They audit 207 executed Danaan/Fisk transcript transitions and 50
informational Next transitions across five raw Danaan packet captures. The audit
includes the original, Loures orders, and court paths and found no other missing
successors on their recorded quest transitions. Unselected NPC topics and stock
menus do not become quest choices. Separate existing tests retain branch selection,
Ase's alternative labels, Varuna's complete endings, doorway consent and departure,
armament receipts, paid travel, reward notices, own legends and terminal closure.

An additional omission guard enumerates the retained Danaan quest-NPC pursuit
pages, including raw packets. It requires a recipe page or an explicitly
accounted context page: Kyros's greeting before `Need Help?`, Blanchefleur's two
visit-topic pages (closed without selecting their choices in N9), and Beggar's
hungry reply to the recorded `I will be back in a moment.` refusal in N49. Those
facts remain retained; none is mistaken for a quest handoff or progress receipt.
The later captured Donnan `You have not finished your task` reply is also
accounted for as an assignment refusal, never a successful stage receipt.

A stitched Cail continuation uses Lancelot's actual pages from Donnan acceptance
through Meaveen, Kyros, Donnan's final report and Erin's reward/close. Travel and
carrying the fruit and greaves early are synthetic. Assertions require each item
handoff and reward claim once, the Cap, XP, fresh own worthy legend and final
server popup closure. This is regression coverage, not a new live completion.

Mundane approach, direct opening and quest arrival validation share a ten-step
screen-range check (Manhattan tile distance), a unique named non-player entity
and the actual map/position. Cached identity alone cannot stop movement. Both
normal and fast approaches stop at that range, including the navigation used
before Glioca speech and unanchored visits. Stable doorway departure remains
required before using the mundane.

The existing paused Donnan submission is checked against its original generation,
entity, page and later reply. Tests preserve pending state for unknown text,
wrong identity/generation, stale revisions or missing offered Next. No uncertain
handoff, payment or reward is authorized for replay by accepting this wording.

After reconnect, that durable matching reply continues to Kyros without
querying Donnan again. The referral assigns the next task on the server;
`Are you the son I seek?` is not a repeatable read-only progress query.
Reopening it checks the unfinished Kyros task and returns the captured refusal.
The earlier recovery implementation incorrectly restarted this topic.

The player's subsequent reconnect exposed an incorrect receipt-boundary check
in that recovery. The actual saved checkpoint has pending revision 2575,
submitted page 2576 and received referral 2577. `QuestContext.BeginAsync`
intentionally journals the boundary one revision before the observed page.
The initial synthetic recovery fixture had assigned the page's own revision
instead, allowing this bug to escape its checks.

Recovery now requires the writer's exact boundary and a reply later than the
submitted page, alongside the existing identity, generation and stage checks.
The normalized [actual checkpoint](../../content/evidence/datester-7.41/danaan-donnan-reconnect-checkpoint.json)
reproduces the original pause and then advances to task 27 on a new connection
without opening Donnan in simulation. Additional tests create the pending record through
`BeginAsync` itself and reject both equal and older incorrect boundaries.
No player journal was edited and no live character was operated.

Read-only inspection of the player's owned replay established that Meaveen
accepted the fruit, Donnan confirmed completion and assigned the meditative
master, and Kyros had not been visited. The bag still contained Leather Greaves.
That bag state does not prove any handoff. The normalized
[assignment recovery capture](../../content/evidence/datester-7.41/danaan-donnan-assignment-recovery.json)
retains the original owned root selection, Next action, confirmation/referral
snapshots and later failed requery checkpoint.

The runner now saves the assignment independently of popup presentation. A new
connection continues it without old dialog commands or item/payment inference.
For the already damaged checkpoint, the app projects only identity/dialogue
receipts from matching sanitized saved replays. Recovery requires the owned
root selection, submitted confirmation Next and exact later referral with their
original generation, entity and revision bindings. Missing/conflicting evidence
or later Kyros/Erin progress preserves the pending checkpoint. A matching open
refusal is closed once before travel; an uncertain close is reconciled separately.

The independent captured Cail continuation also disconnects after assignment,
before referral acknowledgement and after closure. Each run must still deliver
the fruit and greaves once, claim Erin's reward once, inspect the fresh matching
own legend and close the terminal popup. These are synthetic regressions using
actual pages, not a new live automatic completion.
