# Cap/Band of Danaan: research handoff and remaining captures

Status: **Fast completed the approved Donnan/Cail route and received Cap of Danaan**.
Research date: 2026-10-13. Treat Cap and Band as one quest family with variants.
Actual selected-route totals: **6,620 gold spent; 1,000,000 quest XP; one Cap;
new own worthy legend**. One combat XP is separate. This document preserves the
observed opening and Inn routes. The full ordered stages, final receipts,
reconnect, safe approaches and implementation handoff are in
[verified continuation](danaan-observed-continuation.md). Other variants,
prerequisites, Band determination and consecration remain unverified.

## Evidence boundary and requirements

The [guide](https://vorlof.com/quests/capofdanaan.html) explicitly warns of
variable dialogue. The catalog labels both entries level 50+, guide-only and
requiring metadata review. It lists Viper's gland, Mold, Wolf's Skin and Stick;
the guide's described branches do not explain the first two. The discrepancy
is now partly resolved: actual one-Gland and one-Mold consumption is captured
in the continuation. Other accepted tokens remain unknown.

[MCP observations](../../content/evidence/datester-7.41/fisk-danaan-research.json)
prove Fast is a living level-99 Monk with a known own legend and no Danaan mark.
Level alone does not prove eligibility or absence of hidden progress. No other
character or alternate route was tested. Completion on Fast is now proved by
N296–N298. Completed-menu reopen first timed out (N299/N301); after an explicit
final-popup server close N303, fresh root N305 opened with unchanged rewards.
Claim/reversion effects remain unknown; neither was selected again.

Observation 33 records the existing bridge's exact read-only response for the
guide-linked Rolan map: `Map 177 has no recorded dimensions or portals.` Main's
route table also lacks that record. Later visits 228–229 verify actual map 177,
Room 7, Rolan entity 17714 at (3,2). This ephemeral identity must be resolved
again, not hardcoded. Fisk's successful path is documented and complete. Reconnect 197
confirms Fast's preserved completion marks, gold/XP and no Danaan legend. The
improved existing MCP is active. See the observed Inn route and movement limits
below; entrance arrival does not prove a stable pause position.

## Observed start and Inn route

Observation 240 opens Rolan from reachable (4,4). His actual pursuit 1093/step 0
describes repeated dreams of the eight gods arguing in hats, asks for their
origin, and refers to old books in Loures. No choice is offered. Offered Next
241 closes the dialogue; no item/gold/XP change. Reopening from (2,3) at 247
returns the same dream/referral page. It does not prove a separate progress flag
or absence of existing hidden progress. No later quest reward is inferred.

| Source approach / entry | Actual map / landing | Inward and approach evidence |
| --- | --- | --- |
| Abel Port 502 (58,39) → (58,38) | Abel Inn 169, 12×12, (5,10) | 202–205: inward (5,9), stable later map read and Runa interaction. Her unrelated task was closed, not pursued. |
| Inn (1,10) → (0,10) | Corridor 1, 204, 5×15, (3,13) | 208–210: inward (3,12), collision read. |
| Corridor 1 (3,1) → (3,0) | Corridor 2, 205, 15×5, (2,3) | 212–214: inward (3,3), collision read. |
| Corridor 2 (4,1) → (4,0) | Room 7, 177, 8×8, (4,6) | 228–229: inward (4,5) acknowledged; later pullbacks still occurred. Reachable (4,4) opens Rolan (240); player-directed (2,3) also opens him (247). |
| Room 7 (4,6) → (4,7) | Corridor 2, (4,1) | 243–244 acknowledged inward (4,2), followed by later pullback. Successful contiguous exit 250–252 continues immediately to (2,3). |
| Corridor 2 (2,3) → (2,4) | Corridor 1, (3,1) | 253–254: inward (3,2), then walk to (3,13). |
| Corridor 1 (3,13) → (4,13) | Inn, (1,10) | 256–257: inward (2,10), then walk to (6,10). |
| Inn (6,10) → (6,11) | Abel Port, (59,39) | 259 actual landing differs from prior outside approach. Adjacency guard stopped guessed (58,40); 260 actual adjacent step (59,40), resources unchanged. |

The [Inn packet fixture](../../content/evidence/datester-7.41/danaan-inn-navigation.json)
preserves actual geometry, own movement and server door-state packets. Corridor
2 reports open doors at (1,0), (4,0), (7,0), (10,0), (13,0); destination is not
part of those packets. Probe (7,0) actually enters Room 8/map 178. Wrong guesses
(8,0), (6,1), (6,0) did not establish a map transition; each was reconciled
without resource use or blind repetition. Collision/graphics alone did not
identify actual trigger coordinates.

Every building entry must immediately leave its landing. The player additionally
directed Room 7's safe interior around (2,3). One inward acknowledgement at (4,5)
was insufficient to establish stability: map 177/205 alternated later without
recorded movement between those server transitions. Stop/reconcile changed maps
before another action. Source-map guards halted a planned exit batch before any
wrong-map move. The complete practical outward Inn route is now observed below; server internals
of doorway pullback remain unknown. The player-directed (2,3) held
through offered Next after 43 seconds (247–248). A contiguous outward sequence
249–260 then completed all Inn exits in approximately 12 seconds, without
pausing at landings. This establishes a practical route, not the pullback cause.

## Observed Blaise dialogue and variant referral

[Danaan NPC packets](../../content/evidence/datester-7.41/danaan-live.json)
retain the unchanged actual pages; parsed observations 263–272 retain current
choices and resource baselines. Loures Library/map 3041 is 16×16. Actual Blaise
entity 12077 stands at (9,11); reachable public approach (10,12) opens him.
His root includes `Gods and hats`. It leads to pursuit 1094/step 9, quiet warning
and `No, what boy?` / `Yes!`. The selected `Yes!` leads to step 17 with `Orders?`
and `What did you find?`, already different from the guide's fixed sequence.

`What did you find?` leads to old dream records and three information choices:
`What was said?`, `Which God?`, `Can I see the records?`. Their actual replies
describe a great trial (step 32), **Ceannlaidir** in the man's dream (step 36),
and guards confiscating the records with advice to speak to someone closer to
that god (step 40). Each offered Next returns to the same three-choice page;
it does not close or advance automatically. Resources stay unchanged. `Orders?`
and the negative boy response remain unselected. No permanent family choice
or later progress flag is inferred from this informational loop. The actual
Ceannlaidir hint supports inspecting that assistant next; Camille's route is
unverified on Fast.

## Observed Erika referral and shrine entry requirement

Actual shrine map 3019 is 25×16, server name `Shrine of Ceannlaidir - ` (truncated
as received). Erika entity 7752 stands at (3,4); approach (4,4) opens her (277).
Root `About the Records` → `*Tell her of the dreams of the boy and the old man*`
→ offered Next → `What is the legend?` → `What coincidences?` → offered Next
close (278–283). Her actual lore describes a girl in Loures castle wearing the
strange cap. No item, gold, XP or religious membership change was observed.
Compliment and unrelated fellowship/prayer/consecration options were unselected.

During approach, actual active type-3 message at revision 1372/map 3020 was
`Your must be armed to enter here.` (server spelling retained). The player
equipped Fast's carried Oak Stick to permit entry; own state 276 confirms Oak
Stick in equipment slot 1, Billed Cap in 16, four bag slots and unchanged gold,
XP and weight. This was the player's equipment action, not a purchase, quest
delivery or item loss. Town weapon-removal logic applies separately and must
not be used to satisfy this armed-entry condition. Exact rejection should stop
repeated doorway attempts until equipped-weapon state is reconciled.

The [alternate guide](https://darkages.fandom.com/wiki/Cap_of_Danaan_%28Quest%29)
suggests Blanchefleur in Loures second-floor bedroom after Erika, then Tir or
another supplied route. That is a starting hypothesis only; item requirements
and route destinations must come from current menus. It also lists Viper's
Gland and Mold. Their actual consumption on Fast is now captured in the
continuation; alternatives and drop origins remain limited.

## Observed Blanchefleur referral

Blanchefleur's actual map 117 is Loures 2 Floor Bed Room, 10×10; entity 12069
at guide/observed area (2,7), reachable approach (3,6). Observations 286–292:
`Strange Cap` → offered Next → `Where did you get it?` → Next →
`*Thank her for her time*` → final offered Next closes. She names the man guarding
the Coliseum as the giver. No cap is delivered by `Oh, here it is.`: bag, gold
and XP are unchanged. `Give me the cap.` was not selected, and no speculative
50,000 payment was sent. Tir's actual identity, one-Saber exchange and later route are now verified in
the continuation. The guide's alternate guardian/item branches remain untested.

## Selected route and unverified guide variants

The observed successful sequence is Rolan → Blaise → Erika → Blanchefleur →
Tir (Loures Saber) → Astrid Beggar (Baguette) → Mehadi Beggar (1,000 donation) →
Varuna (Viper's Gland) → Greim (approved Mold rite) → Dreamer → approved
Donnan/Cail → Meaveen (two Cherry/one Grapes) → Donnan → Kyros (Leather
Greaves) → Donnan → Erin (claim and final Next) → fresh own legend.
See the continuation for exact current choices, resources and proof at each stage.

Guide claims about Camille/Ase/Keefe/Logan openings, other guardian gifts,
Neal's Wolf's Skin/Stick tasks, level 50+, Band determination and consecration
have not been verified. Donnan/Cail was chosen explicitly before acceptance.
Whether this locks Neal remains unknown. No deity, consecration or reversion
choice was sent. Further permanent choices require the player's approval.

## Preliminary map metadata

This table preserves preliminary code/guide anchors. Only rows explicitly marked
actual are server observations; remaining names/maps are untested route variants.
Updated actual identities and approaches are in the continuation. Runtime IDs,
options and steps must come from new server replies.

| NPC | Preliminary map / anchor | Existing route metadata status |
| --- | --- | --- |
| Rolan | Actual 177, (3,2) | Actual dimensions 8×8; persistent portals missing; (2,3)/(4,4) open him. |
| Blaise | 3041, (9,11) | Library metadata exists; exit (8,15) → 123, planned landing (5,2). |
| Camille | 3013, (3,4) | Gramail metadata exists; exits (23,7)/(23,8) → 3012. |
| Ase | Guide link 195, (4,4) | Missing from current route table. |
| Keefe | 346, (6,2) | Chief Rogue Hall metadata exists; exits to 344. |
| Logan | 398, (6,2) | Record exists as Unknown 398; exits to 397. |
| Varuna | 3075, (29,20) | Record exists as Unknown 3075; exits to 3073. |
| Greim | 3018, (3,4) | Sgrios metadata exists; exits to 320. |
| Dreamer | Actual 720, (14,5) | Actual 30×30; supplied-Mold menu arrival and living return captured. Persistent portal must not be invented. |

Map names, dimensions, entrances, transitions and approach collision must be
checked against the actual server visit. Hall-of-Souls access and return are
especially important: a warning/teleport is not ordinary walkable routing.
Do not treat undocumented maps as permission to guess packets or modify game
assets. The formerly absent Abel Inn entrance has actual landing/door evidence above.

## Implementation and remaining work

[Verified continuation](danaan-observed-continuation.md) provides ordered stages,
current identities/approaches, purchases/weights, special transports, first-task
reconnect, final Next/item/XP/legend receipts, component reuse and suggested
regression tests. [Durable checklist](fisk-danaan-checklist.md) records completed
work and exact gaps. There is no quest recipe/catalog promotion in this task.

The existing bridge now supports guarded merchant quantities, fresh speech replies,
public-side interaction, adjacent entrance/inward movement, compact offered-Next
reading and equipment removal. These were tested synthetically and separately
used live; see [MCP improvements](mcp-discovery-improvements.md). Normal movement
still needs recorded route connectivity and source-map/landing reconciliation.

Remaining live captures: other eligible/low-level/prior-progress characters,
alternate openings and Neal/Band routes, lock behavior, full-capacity refusals,
duplicate-claim response and later progress menus, other pending-stage reconnects, and
explicitly approved consecration/reversion. Persistent route work remains for
Inn geometry, Threshold components, Reception, monastery consent and stable
Monk Hall exits. The recorded map transitions are observations, not invented
trigger coordinates. Synthetic tests cannot certify unobserved variants.

Fast remains living at Erin's interior approach (345,6,5), full HP, unarmed,
Cap in bag, balance 960,780, XP 321,058,867 and both Fisk/Danaan outcome marks.
Final popup was explicitly closed with a server receipt; completed root then
opened and was dismissed locally, without another claim or reversion. Future
automation must include that final close after reward proof. Pattern Walker was
ignored as unrelated. Leave this owned session open.
