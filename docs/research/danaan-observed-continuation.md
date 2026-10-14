# Danaan: verified Cail route and implementation handoff

Player-approved **Donnan/Cail route completed on Fast** on 2026-10-13:
**one Cap of Danaan, 1,000,000 XP and the own legend
`Worthy of the Cap of Danaan, Deoch 223, Summer`**. Actual quest spending was
**6,620 gold**; one combat kill added 1 XP separately. This certifies one offered
route on a level-99 Monk, not every Cap/Band variant or prerequisite. This
supplements the [opening handoff](cap-band-of-danaan.md). No recipe was added
at this research milestone. The later approved [automation task](fisk-danaan-automation.md)
adds the shared Cail runner without claiming new live variant evidence.

N numbers refer to [native MCP captures](../../content/evidence/datester-7.41/danaan-native-mcp.json).
[Common-route packets, generation 5](../../content/evidence/datester-7.41/danaan-native-live.json),
[equipment/ingredients, generation 6](../../content/evidence/datester-7.41/danaan-cail-live.json)
and [tasks/reward, generation 7](../../content/evidence/datester-7.41/danaan-cail-task-live.json)
provide unchanged NPC and active-message parser evidence. Parsed own resources
remain separate. Recorded portals and collision are code metadata, not proof of
actual server transitions. N112's stale collector baseline was removed; N110
is the authoritative post-rite state before its inward step.

## Ordered stages and transition evidence

| Stage | Selected current action | Observed completion proof |
| --- | --- | --- |
| D0–D2 | Rolan → Blaise → Erika → Blanchefleur | Opening observations 240–292: dream, Ceannlaidir records, Loures girl and Coliseum guardian referrals. No resource change. Other opening variants unknown. |
| D3 | Tir: `Blanchefleur's Cap` → `Where did you get it?` → `I have something.` | N34 one Loures Saber bought for 1,500; N41 consumes it, names it and refers to poor man near Sapphire Stream; final Next closes. |
| D4 | Astrid Beggar: `The Cap` speech, food trade, `I might have something.` | N53 one Baguette bought for 10; N58 consumes it and refers to cousin in swamp. Return-later N49 consumes nothing. |
| D5 | Mehadi Beggar: `Cousin`, `How about 1,000 gold coins?`, cap-origin questions | N62 deducts exactly 1,000, acknowledges; N63–N65 Mukul referral. Kindness/backstory unselected. |
| D6 | Varuna: `The Cap`, `I have what you need.` with one Gland | N82 actual Viper's Gland pickup; N86 consumes one, acknowledges and offers dream choices. Generic request does not name item. |
| D7 | Varuna: boy question, then old-man interpretation | N87 loops without further consumption; N88 refers to summoner of dead spirits, warns of penalties, final Next closes. |
| D8 | Greim: dead-speaker topic, consequences, proper care; obtain decay token | N90–N93 and N102–N104 full soul-loss warning and unnamed token hint. N99 proves actual Mold +1. |
| D9 | Explicitly approved supplied-Mold rite, `I am willing to try.` once | N109 `Very well.`, Mold 1 → 0, weight 8 → 7, Hall of Souls 720 landing (14,27). N110 full 4,108 HP; N112 inward (14,26). No gold/XP charge. Initial automatic approval rejection sent no game action; user then approved this concrete rite. |
| D10 | Dreamer's story sequence below | N115–N134 actual pages, Avatar father/son referral, unchanged resources. |
| D11 | `Wait, I have more questions!` | N136 actual Rucesion Inn 498 landing (5,8); N137–N141 full HP, inward (5,7). “Back in the inn” does not prove Abel Inn. |
| D12 | Approved Donnan/Cail; Aoife `Guide Reception` → `Yes` | N159–N166 actual menu, guidance confirmation, 393 → 394 landing (7,6), inward (6,6), no charge. Walking (1,5)/(0,5) did not transport. |
| D13 | Donnan `Are you the son I seek?` → acceptance | N167–N171 `Yes. I did so much already, two more tasks are nothing.`, then Goddess of Love attendant referral and final Next close. No resource change. |
| D14 | Remove weapon; Meaveen `Need Help?`, buy ingredients, `I may have what you need.` | N184 guarded unequip clears Oak Stick slot 1 and adds it to bag. N193/N195 buy two Cherry for 100 and one Grapes for 10. N204 reconnect verifies that same held bundle before handoff. N208 consumes exactly two Cherry/one Grapes, thanks Fast so the lesson may continue; offered Next closes. N209 confirms weight 9 → 7, no extra charge/XP. Request did not name quantities; this is the verified accepted bundle, not a proved minimum. |
| D15 | Reopen Donnan's Son topic | N219 explicitly confirms first task and refers to keeper of Ranger's unused jail; final Next closes. No reward yet. |
| D16 | Kyros `Need Help?`, acquire Leather Greaves, `I have some for you.` | N251–N252 explicitly request replacement leather greaves. N260 stock/N262 purchase: one Leather Greaves, 4,000 gold, weight +4. N273 consumes one with terminal gratitude and no Next. N274 confirms weight 11 → 7, no extra charge/XP. |
| D17 | Reopen Donnan's Son topic | N285 terminal `It is done!` and daughter-of-Danaan referral. No Cap/XP yet. |
| D18 | Erin `I completed the tasks`, then offered Next | N294/N295 fresh root and baseline. N296 first congratulation page: XP 320,058,867 → 321,058,867, but no Cap yet in that raw packet snapshot. Final Next: Cap of Danaan +1, sprite 1059, weight 7 → 11, one slot added; terminal consecration guidance. Gold stays 960,780. Actual active type-3 revision 3538: `1000000 experience!`. Item and XP receipts occur separately. |
| D19 | Fresh `inspect-self` and state | N297/N298 own legend revision 3544 adds `Worthy of the Cap of Danaan, Deoch 223, Summer`. Fisk secrecy/reputation marks intact. N301 independently confirms completion resources after a failed root reopen. |
| D20 | **Explicitly close Erin's final terminal popup** | At player's direction N302 rechecks the exact current terminal page; N303 `close` gives actual server close revision 3606, dialog null, unchanged rewards. N304 confirms. Closing is required after reward reconciliation, not a consecration choice. |
| D21 | Read-only completed root; dismiss it | N305 fresh Erin root opens after the terminal close, still offering claim and `Original Cap or Band of Danaan`. Neither selected; no extra reward. N306 dismisses the merchant root locally, which is presentation only, not a new server close receipt. |

Dreamer choices: `What is this place?` → `And what was that?` → `What happened
to the Cap of Danaan?` → `But these caps were never seen!` → `There is such proof
now?` → `What happened?` → `And why did the boy have those dreams?` → `A lure?`
→ `It is still not over!?` → `Wait, I have more questions!`. Every offered Next
between choices was read. Who/dead alternatives unselected. Father/son is a
prose referral, not a two-option menu here. User approved Donnan/Cail before
acceptance; whether that locks Neal remains unknown.

## Actual NPCs, approaches and requirements

IDs are observed examples; resolve afresh rather than hardcode them.

| NPC / actual map | NPC tile; successful public approach | Requirement / route |
| --- | --- | --- |
| ColiseumTir / 5232 | 17224 (6,4); (7,5) | Saber 1. N22 Threshold 5231 (0,1) → (0,0), actual landing (4,12), combined inward (4,11). Lower/upper components remain planner gap. |
| Gunnar / 165, Piet Weapon Shop | 10967 (8,7); (7,11) | Saber 1 × 1,500. (7,12) drifted to (7,13), closing menu; stale purchase blocked. |
| Astrid Beggar / 3060 | 10526 (4,8); (4,10) | `The Cap`, Baguette 1 accepted. Rock-enclosed NPC; other foods unknown. |
| Alleen / 162, Piet Restaurant | 10992 (6,4); (10,4) | Quotes: Baguette 10, Chicken 20, Beef 35, Grapes 10, Cherry 50. Baguette purchased. |
| Mehadi Beggar / 3071 | 10530 (2,1); (3,2) | `Cousin`, 1,000 donation. |
| Varuna / 3075, Rishi Mukul Gathering | 8312 (29,20); (30,21) | Gland 1; (30,20) blocked. Actual name differs from code's Unknown 3075. |
| Greim / 3018, Shrine of Sgrios - Dubha | 7798 (3,4); (4,4) | Mold 1 consumed on approved rite. |
| Dreamer / 720, Hall of Souls | 17715 (14,5); (15,6) | Actual 30×30; (14,6) blocked. Dialogue returns to 498; no invented portal. |
| Aoife / 393, Path Reception | 6312 (5,5); (3,5) | Guide Reception/Yes → 394. Dedication/stat/consecration topics unselected. |
| Donnan / 348, Chief Monk Hall | 6516 (2,5); (4,5) | (3,5) blocked. Actual route 394 → 395 → 399 → 343 → 344 → 348. Exit (10,5) → (11,5) → 344 (2,7), **north (2,6), then deeper (6,8)**. East (3,7) still bounced; N230–N232 and N287–N288 prove successful away sequence. |
| Meaveen / 3007, Shrine of Glioca - Milet | 6548 (3,4); (6,4) | Successful unarmed entry. Player supplied weapon rule; exact armed refusal not captured. |
| Kyros / 5210, Cedar Grove | 17169 (2,6); (4,6) | Rock-enclosed; speech `Need Help?` opens task. Monastery/Help New Monk/Founder topics untouched. |
| Steinar / 182, Abel Armor Shop | 11030 (3,5); (3,8) | Leather Greaves 4,000 actually quoted and paid. Other stock unpurchased. |
| Erin / 345, Chief Priest Hall | 6518 (6,2); (6,5) | Actual root offers `I completed the tasks` and `Original Cap or Band of Danaan`; latter untouched. |

Actual spending: Saber 1,500 + Baguette 10 + donation 1,000 + Cherry 100 +
Grapes 10 + Leather Greaves 4,000 = **6,620 gold**. Balance after Fisk
967,400 → 960,780. No additional observed transport/turn-in charge or reward
gold. Quest XP **1,000,000**, combat XP **1** separate. Full completion HP 4,108.
No quest timer was encountered on this selected route.

Each token used one additional slot; Saber weight 7, Baguette/Gland/Mold weight
1 each; two Cherry stacked at weight 1 plus Grapes at weight 1; Greaves weight
4. Handoffs free those items/weights. Final Cap uses one slot and weight 4.
Maximum weight 127 observed; 59 bag slots is a code fact. Reserve a slot for
weapon removal, two for fruit, then one for final reward. Full-capacity refusals
and minimum capacity across variants remain unknown.

[Resource capture](../../content/evidence/datester-7.41/danaan-resource-acquisition.json)
separates visible monster/ground observations from own receipts. Garden 622 fight
used fresh sprite 49; actual names blank. [Guide metadata](https://vorlof.com/map.php?map=622)
calls it Viper, a source claim. One bounded fight produced a fresh sprite-327 drop;
pickup proves Gland +1. Combat added **1 XP** separately and reduced HP 4,108 →
4,084 before full regeneration. No automatic looter: its extra-item discard
behavior could waste items. Fighter equipped Oak Stick; equipment is not delivery.

Crypt 1-1/map 1 already had sprite-338 ground at (4,10); pickup proves Mold +1
without more combat/XP/spending. Origin unknown; no Mouse kill/drop rate proven.
Sprite 338 has shared names; inventory identifies Mold. [Item guide](https://vorlof.com/items.php?id=338_Mold)
drop sources remain hypotheses.

## Special entrances and safe movement

| Source / action | Actual arrival and immediate safe continuation | Evidence |
| --- | --- | --- |
| Threshold 5231 upper (0,1) → (0,0) | Coliseum Entrance 5232 (4,12) → (4,11), then Tir (7,5) | N22/N37 combined entry. Two blocked collision rows separate Threshold components; reconcile actual upper landing. Earlier paused entries caused doorway displacement. |
| Greim supplied-Mold choice | 720 (14,27) → (14,26), Dreamer (15,6) | N109–N115. This is a menu transport, not an ordinary portal. |
| Dreamer's final choice | 498 (5,8) → (5,7) | N136–N141; alive, resources reconciled. |
| Aoife Guide Reception/Yes | 394 landing variants (7,6) → (6,6), (7,7) → (6,7), (6,7) → (5,7) | N166, N215–N216, N281–N282. Use actual landing; do not hardcode one variant. Walking reception (1,5)/(0,5) did not transport. |
| Donnan hall (10,5) → (11,5) | 344 (2,7) → **north (2,6)** → deeper (6,8) | N230–N232/N287–N288. East (3,7) acknowledged but still bounced between 348/344 without recorded movement. Player explained door pullback. |
| Sapphire Stream Pass 5220 (1,7) → (0,7) | Fresh unnamed reactor asks `Will you be respectful of the monastery?`; current `Yes` grants 5210 entry | N245/N247 and N264/N266. Entity type 4, observed pursuit 707/step 5; resolve anew. Ordinary entry consent, no religion choice. |
| Monastery consent | 5210 variants (14,13) → (14,12), or (14,12) → (14,11) | N247/N249 and N266/N268. No charge; never repeat Yes after uncertain transport. |
| Cedar Grove (14,9) → (15,9) | Pass 5220 (1,4) → (2,4) | N254/N276 combined exit and away step. |
| Path Temple 1 /395 (6,2) → (6,1) | 396 (6,10) → (6,9); then 396 → 397 → 342 → 345, Erin (6,5) | N292–N294. Prior automatic approach stopped at 395 (4,3); guarded stairs succeeded. Pattern Walker popup belongs to another quest and was ignored at player's direction. |

Actual observed world-map node **Sapphire Stream is 5220**, decoded with the
existing typed world-map layout. Guide link 5219 did not establish a live route;
public image links were unavailable. Pass probes (0,4)/(0,1)/(0,8) did not
transport. Walking through (0,7) revealed the real consent gate. Do not add
those failed probes as portals. All other transit maps and actual landings
are retained in raw fixtures' `ObservedMapTransitions`. Last source positions
and nearby moves are temporal observations, not necessarily trigger tiles.

## Recovery, completed state and unknowns

Task menus reopened after acquisition at Tir, both Beggars, Varuna, Greim,
Meaveen and Kyros. Pause at verified approaches away from doors with resources
reconciled. Donnan's actual progress page proves a prior task; held items alone
do not. Supplied-Mold transport and living return are verified. Tokenless/
alternative-token rites, soul-loss penalties and rift failure were not tested.

User closed Fast's client after fruit purchases and before Meaveen delivery.
N198–N199 record disconnected/unknown inventory; cached counts were not live
proof. After approved saved login, N203–N204 confirm a new generation with the
same two Cherry/one Grapes, unchanged gold, full HP and no equipped weapon.
A fresh Meaveen request authorized N208 once. No repeat purchase/delivery.
This proves this pending-task reconnect, not every stage or an ambiguous
in-flight handoff. One earlier login attempt was also closed by the user.

Final item/XP/fresh legend prove completion. Post-reward Erin root open N299
returned `Server evidence timeout.`; N301 still showed the same terminal
consecration page, Cap, XP, mark and full HP. No claim/payment was retried.
The player required final popup closure: N302 rechecked that current terminal,
N303 closed it with a server receipt, N304 confirmed null dialog. A new root
open then succeeded at N305, showing the same claim/reversion choices without
extra rewards. This sequence proves a practical recovery after close; it does
not establish server internals of the earlier timeout. N306 dismissed that root
locally; cached server root may remain in state, as documented for merchant close.
Actions ended at stable interior (6,5). **Duplicate-claim behavior remains
unverified**. `Original Cap or Band of Danaan` proves an offer, not reversion.
No consecration speech or deity choice was sent.

## Focused implementation handoff

Reuse `GameSession` ordered movement/dialogue/speech, bounded reader, merchant
quotes/item arguments, generation/revision/page guards, guarded unequip,
`inspect-self`, `FileQuestJournal`, pending `Transaction` baselines and
`QuestRunner.ReconcileAsync`. Emerald Dawn's explicit choice/locking structure
is useful; its predicates/rewards are not Danaan evidence. Preserve ownership
and all four repeat policies. [MCP improvements](mcp-discovery-improvements.md)
are live discovery capabilities, not a quest runner.

A separate implementation should persist family variant, approved Avatar branch,
story/task receipts, held-token baselines, committed handoffs, each menu transport
and final claim/Next independently. Add Reception/reactor routing and safe Monk
Hall exits; preserve actual landing variants. Generic token text and pursuit/step
alone cannot identify a stage. Inspect matching own worthy mark before starting.
Existing mark with missing Cap is reconciliation, not permission to reclaim.
Final completion requires correlated matching item, numeric XP, active message
and fresh own mark. XP and final Next/item receipt remain independent. Then use
the existing bound `close` and require actual server dismissal of Erin's final
popup before concluding the run. Persist this cleanup independently: an uncertain
close cannot invalidate a verified reward or authorize another claim.

Exact remaining captures:

- Eligibility/refusals, hidden-progress prerequisites, prior-completion menus on
  another approved available character. Catalog level 50+ remains guide-only.
- Camille/Ase/Keefe/Logan opening variants, other guardian/food/token routes,
  Neal's tasks and whether Donnan acceptance locks Neal.
- Band determination, its actual item/legend/reward; Cap proved on Fast only.
- Later completed Rolan/Donnan menus, duplicate-claim refusal and natural
  completion reconnect. Erin's completed root availability is now observed;
  its claim/reversion effects are not. Do not repeat a reward to fill a fixture.
- Consecration offers/tradeoffs, explicit deity choice, then actual item/stat/
  legend change and reversion if requested. All unselected.
- Other pending-stage and interrupted transaction reconnects, remaining source
  triggers and Threshold routing. No whole-family completion claim.

Suggested regressions: captured pages parse unchanged; information loops cannot
repeat gifts/donations; changed page/NPC/generation sends nothing; ambiguous
delivery/removal/transport stays pending; reconnect requires fresh known resources;
reception/gate consent differs from dedication; actual landing determines away
step; one acknowledged wrong-side step is not a safe pause; terminal Kyros/Donnan
pages send no invented Next; Erin XP may precede Cap; final Next, item, own mark
and active message are independently required; final terminal close is required
after proof and uncertain cleanup never resends a claim; an existing worthy mark prevents
automatic claim; Band/other branches remain unknown until their own evidence.
Synthetic tests never establish a live alternative outcome.
