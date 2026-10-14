# Fisk's Secret: verified secrecy path and implementation handoff

Status: **approved secrecy path completed on Fast; eligibility/alternative limits below**. Research date:
2026-10-13. Fast was the only approved character. Fisk's secret remains intact.
Five Wine cost 2,500 gold and were exchanged for five Water Lilies, delivered to
Fisk. Ten Grapes cost 100 gold and were exchanged for five Petunias, also delivered.
A 10,000 donation obtained Pink Rose; Fisk returned the complete flower bundle
after his final referral Next. Viveka consumed it and charged 20,000. Total paid
32,600; actual collection, secret delivery, 750,000 XP, both own-legend marks and
completed-character response are verified. This does not certify untested branches.

The later approved [automation task](fisk-danaan-automation.md) implements this
secrecy route. The live completion below used discovery commands; it does not
certify a fresh run of the new full automatic recipe.

## Evidence and scope

- [Parsed MCP observations](../../content/evidence/datester-7.41/fisk-danaan-research.json): numbered observations, actual revisions, relevant own state and current menus. This is not a replay-schema fixture.
- [Initial server packet fixture](../../content/evidence/datester-7.41/fisk-secret-initial.json): 15 unchanged NPC dialogue/menu packets, 12 recorded dialogue actions, normalized character/generation and observed map transitions. No login, own-look, player appearance or chat packets are included.
- [Later server packet fixture](../../content/evidence/datester-7.41/fisk-secret-live.json): actual later NPC pages, own-resource receipts and map landings. [Quantity defect witness](../../content/evidence/datester-7.41/fisk-wine-quantity.json) retains the observed argument prompt and malformed old reply. Raw packet bodies are unchanged; event identity/generation are normalized.
- [Durable checklist](fisk-danaan-checklist.md) tracks outstanding captures.
- [Guide](https://vorlof.com/quests/fisksecret.html) is a hypothesis for later stages, not completion evidence.

The existing installed MCP and its current-user control bridge were used. The
installed runtime exposes six recipes and predates main's merchant item-list
observations and `GameSession.Buy` support. A narrow existing-bridge repair and
merchant quantity correction were needed; see [repair notes](purchase-bridge-repair.md).
Other characters' saved queues were left intact. Fast's missing saved login was
configured in the existing Windows-encrypted vault, outside research artifacts.

## Observed requirements and starting state

Observation 2 proves Fast connected as a living level-99 Monk: HP 4,108, weight
7/127, five occupied inventory slots, zero carried gold and XP 319,308,866.
The own legend was known at revision 33 and had no Fisk, Danaan or Unregistered
mark. This establishes this character's starting state, not the absence of
hidden quest flags or a universal registration requirement.

Fisk offered and accepted the quest on this character. The catalog's level 21+
requirement remains guide-only; the actual minimum, other prerequisites,
ineligible replies remain untested. Completed-character behavior on Fast is
verified in F24 below.

## Ordered stages and transition proofs

| Stage | Action and state | What actually proves the transition |
| --- | --- | --- |
| F0: initial offer | Open the currently observed Fisk; select `Secret Admirer`. | Observations 6–7: fresh Fisk menu and the favor/secret question. |
| F1: promise | Select `I will help you and keep your secret.` | Observation 8: fresh `Ah, wonderful!` response. This is acceptance, not completion. |
| F2: first task | Read three offered Next pages, choose `Alright. Where do I begin?`, finish offered Next. | Observations 9–13: Bertil story, flower request, explicit five-water-lily task naming Thulin, then server close. |
| F3: supplier request | Open Thulin, choose `Water Lilies`, then `Sure. What do you need me to do?`, read and finish Next pages. | Observations 18–22: five bottles of wine requested; Viveka named; explicit instruction to say `wine` on returning. No lilies were granted. |
| F4: Wine acquisition | Open observed Viveka from (10,8), choose `Buy`, select `Wine`, answer the actual quantity prompt `5`. | Observations 60–62: Wine quoted 500; prompt argument Wine; five Wine received, gold 1,000,000 → 997,500, weight 7 → 8, one added slot. |
| F5: supplier exchange | At Thulin (3,9), say `wine`, inspect the later fresh response, choose `Yes, here they are.` | Observations 64–66: speech result initially preceded the reply; fresh state 65 binds Thulin's question. 66 proves five Wine removed, five Water Lily received and `Here are the flowers you asked for.` Gold unchanged. |
| F6: supplier secrecy | Read Next; choose `I cannot say. They are very much appreciated, though.`; finish offered Next. | Observations 67–69: actual betrayal/secrecy choices, `Alright...farewell, aisling`, then close. Betrayal was not selected. |
| F7: lily delivery | Open Fisk, choose `Secret Admirer`, then actual `Yes, here they are.`; read all offered pages to close. | 71–77: existing-progress question; `takes the flowers from you`, five Water Lily removed, weight 8 → 7, slot freed; five Petunias/Eeva referral and secrecy reminder. Includes self-spoken `Certainly.` between NPC pages. |
| F8: Petunia supplier request | From the Inn public side (6,4), open Eeva, select `Petunias`, then `Sure`; finish Next pages. | 81–85: live Eeva menu/favor, Alvar referral, exact ten bunches, `:grapes:` speech trigger, then close. |
| F9: Grapes acquisition | Alvar `Buy` → `Grapes` → actual quantity prompt `10`. | 103–106: fresh stock 10/unit, quantity prompt, ten Grapes received, gold 997,500 → 997,400, weight 7 → 8, one added slot. |
| F10: Petunia exchange/secrecy | At Eeva (6,4), say `grapes`; inspect fresh reply; choose `Yes, here they are.`, read Next, choose `I cannot say. I am grateful for them, though.`, finish Next. | 108–113: speech response 109, ten Grapes removed/five Petunia received 110, actual secrecy question 111, `Hmmm...` and close 112–113. Weight/slots stay 8/6, gold unchanged. Guide spelling `greatful` differs from actual `grateful`. |
| F11: Petunia delivery | Fisk `Secret Admirer` → actual `Yes, here they are.`; read all offered pages to close. | 115–121: current Petunia question, `takes the flowers from you`, five Petunia removed, weight 8 → 7 and slot freed; Shrine of Deoch referral, self-spoken `No problem.`, secrecy reminder, close. |
| F12: Pink Rose explanation/payment | Open Vivianne, choose `Pink Rose`, then `I need a pink rose. Would you happen to have one?`; choose `I am helping someone make a bouquet, but I cannot tell you who.`; Next; `Certainly.` | 123–128: actual extra request page, three explanations (two untested), secrecy-preserving response, donation quote 10,000 and gold 997,400 → 987,400. `Thank you, kindly. Here you go.` still has no Pink Rose in returned snapshot. |
| F13: Pink Rose receipt | Finish Vivianne's offered Next. | 129: dialog closes, Pink Rose +1, weight 7 → 8 and one added slot. Payment alone is not item receipt. |
| F14: Pink Rose delivery/secrecy confirmation | Fisk `Secret Admirer` → actual `Yes, here it is.`; read each offered page. | 131–140: Pink Rose removed (133), weight/slot reduced; self-spoken `Yes, I had to...` concerns the bouquet explanation, not revealing his name. 135 asks his name; 136 says only an unnamed person was making a bouquet; 137 replies `That's good to know.` Then actual Viveka referral and rose-in-middle instruction. |
| F15: carry flower bundle | Finish Fisk's final offered Next. | 141: close plus five Water Lily, five Petunia and one Pink Rose returned to inventory. Weight 7 → 10; occupied slots 5 → 8. This bundle receipt is distinct from the earlier three consumed deliveries. |
| F16: bouquet quote/acceptance | Viveka `Flower Bouquet` → `How much?` → actual `Here you go. Put the pink rose in the middle.` | 143–146: quote 20,000; gold 987,400 → 967,400; five lilies, five Petunias and Pink Rose removed; weight 10 → 7, three slots freed. Server `Return to me in one hour...` at 2026-10-13T16:21:46.1953068Z, revision 1322, no Next. This is paid acceptance, not collection. |
| F17: early wait check | Close acceptance popup, reopen actual Viveka menu, select `Flower Bouquet`. | 148–150: at 16:23:47.2699292Z (about 121 seconds after acceptance), `I can't rush perfection. Come back in a bit.` No Next, no Special Bouquet, unchanged gold/bag. No second payment. |
| F17b: later wait check | Reconcile client movement/doorway transition, return to the verified approach, reopen actual menu and choose `Flower Bouquet`. | 157–162: same wait reply at 17:11:57.9013443Z, about 50m 11.7s after acceptance, still no bouquet or charge. Paid wait persists through the observed Tavern/Suomi map changes. |
| F18: actual collection | After the hour, reopen actual Viveka menu and choose `Flower Bouquet`. | 164–167: fresh baseline/menu, server `Here is your flower bouquet` at 17:22:34.3445539Z, Special Bouquet +1, weight 7 → 8, one added slot, no charge. Then terminal close. Collection occurred ~60m 48.1s after acceptance; exact threshold is only bounded by refusal at ~50m 11.7s and this receipt. |
| F19: letter attachment | Open Fisk with Special Bouquet and choose actual `Secret Admirer`; read offered Next, then close terminal instruction. | 169–172: `takes the bouquet and Attaches a letter to it`, followed by `Deliver this to Bertil and do not tell her who it is from.` Same Special Bouquet count/name remains; no new letter item or resource delta. Server dialogue proves the attachment stage, not a distinct inventory item. |
| F20: delivery offer | At actual Bertil, open her menu and choose `Special Delivery`, then Next. | 174–177: map 416/approach (7,2), fresh entity 11255, actual option, reads attached note, sender question. Bouquet still carried while note is read. |
| F21: secret delivery receipt | Choose `They are from a secret admirer, who I cannot reveal to you.`; Next, close terminal instruction. | 178–180: Special Bouquet consumed exactly at secrecy choice, weight 8 → 7, one slot freed, gold/XP unchanged; `I wonder who...`, then asks to tell sender the flowers are Lovely. Betrayal option was not selected. |
| F22: final return/reward | Open Fisk after secret delivery and choose actual `Secret Admirer`; read every offered page to terminal thank-you, then close. | 182–188: XP 319,308,866 → 320,058,866 already on first `You have returned!` page, delta 750,000; actual active messages at revisions 2346/2347 are `750000 experience!` and `You gain 750,000 experience!`. Reaction pages end `Thank you so much!`; gold/items unchanged. Reward is committed before the remaining pages. |
| F23: own outcome proof | `inspect-self`. | 189: fresh own legend revision 2356 includes `Kept Fisk's Secret - Deoch 223, Summer` and `- Reputable - 1 `. Neither was present initially. This proves the approved outcome; client GUI visibility of the reputation mark was not inspected. |
| F24: completed-character behavior | Reopen actual Fisk menu, choose `Secret Admirer`, finish offered Next. | 191–193: `Thank you for keeping my secret.`, then close/active `Fisk has a big smile.` No new task, XP, gold or item reward. Do not automatically repeat this completed path. |

Fisk and Thulin repeatedly return pursuit **0**, step **1** on distinct pages.
Never identify a stage by those numbers alone or assume a monotonically
increasing step. Bind the actual NPC, generation, fresh revision, page text and
available choices/Next. Use the offered label, with IDs resolved at send time.

## Verified navigation and NPC approaches

The observed outbound map chain was
`498 → 505 → 502 → 3008 → 504 → 3016 → 503 → 415`.
The packet fixture records the first fresh position after each map change.
Its `LastObservedSourcePosition` is an adjacent/previous position, **not proof
of the doorway trigger tile**.

| NPC | Observed map and NPC position | Successful approach | Recorded entrance metadata and observed landing |
| --- | --- | --- | --- |
| Fisk | 415, (5,5) | (7,6) | Suomi 503 (13,17) is route metadata; server landing (7,13), revision 518. |
| Thulin | 6806, (2,9) | (3,9) | Suomi (11,29) is route metadata; server landing (20,16), revision 569; another arrival at revision 578. |
| Viveka | 413, (6,5) | (10,8), across the bar; opening her succeeded | Observed chain 6806 → 503 → Restaurant 412 → Tavern 413. Landings (8,13) at revision 789 and (10,13) at revision 810. |
| Eeva | 950, (3,3); fresh entity 19160 | (6,4), across the counter | Actual landing (10,2), revisions 407/643; return to Suomi (41,48), revisions 433/680. Metadata entrance (39,48); guide (32,54) alternate remains untested in this visit. |
| Alvar | 3004, (3,6); fresh entity 11271 | (10,4) opens successfully; (10,5) also opened but had doorway transitions | Suomi (61,41) → landing (10,5) confirmed in replay. Move off the landing before purchasing. |
| Vivianne | 3017, near (3,4); fresh entity 6540 | (4,4) | Actual 503 → 3016 landing (15,14), rev 877; 3016 → 3017 landing (22,6), rev 893. Return landings (12,7) in 3016, rev 958, then (39,1) in Suomi, rev 972. Metadata Shrine entrance (10,7). |
| Bertil | 416, (5,2); fresh entity 11255 | (7,2), verified open | Actual Suomi White Magic Master landing (13,7), rev 2195; return to Suomi (16,86), rev 2241. Metadata entrance (15,86). Last source position alone is not doorway-trigger proof. |

Theatre `visit` without an anchor timed out after arrival because Thulin was
not visible near the door. Observation 15 reconciled map 6806, position (20,16),
unchanged inventory/gold and no control owner. Supplying the guide-area anchor
(3,9) then resolved the actual NPC and succeeded. A subsequent door bounce is
retained in the map-transition fixture; it was not an item transaction.

Viveka's guide-side (6,6) and the recorded Tavern landing (10,13) are in separate
static collision components. The route planner refused that target. Suomi
(80,15) also proved collision-blocked. The guide's direct (81,15) entrance is
absent from the recorded portals and was not forced. Navigating to the verified
public-side (10,8) succeeded and exposed Viveka, whose merchant menu opened.
This existing route is sufficient for the observed menu interaction; the direct
door and northern-room traversal remain unverified.

Eeva's guide-side (3,4) is in a different collision component from the recorded
Inn entrances. The planner refused that approach without moving or committing
resources (78). The inspected map (79) supported public-side (6,4), which
succeeded (80–81). `visit` also filters NPCs by Manhattan distance ≤4 from the
anchor: Viveka at (6,5) is six tiles from (10,8), so a `visit` using that public
anchor waits despite visibility (56). `navigate` followed by `open` is verified
for these counters. Neither timeout establishes an NPC refusal.

Alvar's (4,6) target is statically blocked (86–87). Even inspected walkable
targets (5,6)/(7,6) produced an occupancy-planner refusal (88,92,94); that planner
cause remains unresolved. Replay and state confirmed actual map 3004 arrival
at (10,5), Alvar visible at (3,6), and later doorway transitions with no Grapes
quantity packet. Attempt 98 returned `No dialog.` before sending anything.
Gold/bag remained unchanged (99). After a confirmed adjacent step to (10,4)
(102), the menu and successful purchase were stable (103–106). This establishes
a practical route and safe interrupted-prompt recovery, not a general fix for
the planner or proof of the doorway transition's cause.
Read-only post-visit collision comparison (156) found no changed tiles; both
rejected approaches remain walkable in that fresh view. No stale-asset fix is
claimed. The successful off-door approach is the implementation candidate.

During the paid wait, client movement placed Fast at Tavern (14,1). Attempt 157
returned Suomi (82,15), and replay records additional 503 → 413 landings at
(13,4), with no proven inbound trigger tile. Return 159 reached public approach
(10,8); 160–161 proved Viveka's current menu and continued paid wait. These
unrecorded transitions need separate capture/reconciliation. They do not verify
the guide's direct (81,15) entry, and a navigation return alone does not prove
the requested map/target was reached. The earlier Restaurant route remains the
verified implementation candidate.

## Purchases, capacity and historical bridge blocker

The current server stock packet quotes Wine at **500 gold each**. Observation 62
proves the five-item purchase charged **2,500**. The stock also offers
`Rose Bouquet` for 50,000; it must not be mistaken for the uncollected quest
`Special Bouquet`.

The installed bridge's stock snapshot has no items/prices, no choices, no text
prompt and no Next. Its command schema has no `buy`. Main already parses
`Dialog.Items` and implements verified `GameSession.Buy`, but main's control
allowlist also excludes `buy`. `choose` resolves menu choices, and `input`
requires a text-input prompt; neither supplies a supported purchase operation
for this menu. No guessed merchant packet was submitted.

The player supplied funding and clarified that the agent must make purchases.
The corrected existing bridge handled the fresh Wine quantity prompt, retaining
its server-supplied item argument. The pre-fix malformed request and recovery are
documented separately; reconnect observation 54 reconfirmed unchanged gold/bag.
Fast's balance after Wine was 997,500. Five Wine stacked in one new slot and
increased weight by one. Five Water Lilies also occupied one slot at weight 8;
Fisk's delivery reduced weight to 7 and freed that slot. Later Grapes/flowers
are now verified above; peak quest bundle required three additional slots and
weight 3. Full-capacity refusal behavior remains untested.

Observed costs: Wine 500/unit ×5 = 2,500; Grapes 10/unit ×10 = 100; Vivianne
donation 10,000; bouquet crafting 20,000. Total 32,600. The peak observed quest
bundle uses three slots and weight +3 over Fast's baseline. Supplies are bought
as stacked quantities; canonical bag names are `Wine`, `Grapes`, `Water Lily`,
`Petunia`, `Pink Rose`, `Special Bouquet`.

The server promises one hour, refuses at ~2m 1s and ~50m 11.7s, and grants the
actual bouquet at ~60m 48.1s. Exact minimum readiness time is untested. Final
observed reward is 750,000 XP, no gold/item reward, and the two fresh own-legend
strings above. Fast carried `VDay Bonus Exp-Ap(x5)` throughout; this run shows
750,000 XP despite that carried item, not proof of a general bonus rule. The
guide calls reputation invisible; its mark is observed in protocol own-legend
inspection, while GUI visibility remains unknown.

## Pause, recovery and untested branches

Current state: completed (183–193), original bag only, 967,400 gold and XP
320,058,866. Fast is living at Fisk (415,7,6), no pending resource transaction,
active queue or control owner. Same-generation completion is proven. The
research checklist and captures preserve progress; DAQuester has no Fisk recipe
checkpoint yet. Do not infer that its generic selected-quest job tracks Fisk.

Before resuming, read fresh ownership, identity, inventory, gold and own legend.
Capture new baselines and reconcile current task menus before delivering.
Never reaccept from an assumed
initial stage. Observation 42 proves the initial lily reminder after natural
reconnect. Observation 72 reconfirms that stage after the authorized research
runtime reload. This establishes that stage's persistence, not later payment or
timer recovery.
Post-completion owned reload (197) proves both Fisk marks, XP 320,058,866,
967,400 gold and original inventory persisted in a fresh generation. This
completed-state reconnect does not test the earlier paid timer across reconnect.

For future resource submissions, persist intent and the complete relevant
baseline before sending. Reconcile with matching later server pages, item/gold
changes and own legend. An ambiguous receipt blocks further actions on Fast.
A one-hour deadline only permits a collection check; only a correlated new
Special Bouquet establishes collection. Do not deliberately reconnect, betray
Fisk, spend twice or consume extra items to test recovery.

## Implementation handoff

Reuse `QuestRunner`/`QuestContext.BeginAsync`, `Transaction`,
`FileQuestJournal`/`AtomicJsonFile`, `EvidenceHistory`, `CharacterMemory`, the
queue ownership gates, `MapRepository`/`TravelPlanner`, `NpcApproach`, ordered
`GameSession` sends, `DialogPackets`, `inspect-self`, existing item-list parsing
and `GameSession.Buy`. Use quest-specific evidence predicates beside the other
`*Evidence.cs` classes. Preserve all existing quest repeat policies; research
does not establish Fisk's repeat/completed-character policy.

Separate future checkpoints for supplier acceptance, each purchase, each
flower handoff, bouquet payment/acceptance, collection, letter attachment,
delivery and final confirmation. Record the approved secrecy choice durably.
Use the observed boundaries above, especially returned flower bundle, paid timer,
collection item, letter acknowledgement, delivery consumption at secrecy choice,
and reward before final dialogue ends. Require the fresh own completion legend
alongside correlated reward evidence. Existing completion must not claim a new
reward or reaccept the task.

Missing capabilities/captures:

1. A separate full quest runner/evidence predicates and durable checkpoints. No Fisk recipe/catalog promotion was added.
2. Exact minimum level, prerequisites, ineligible replies and other characters' existing hidden progress. Level 99 Monk success establishes this character only.
3. Paid-timer persistence across natural reconnect, precise readiness threshold, and interrupted committed handoff/reward recovery. Do not disconnect merely to fill these gaps.
4. Alvar occupancy-planner refusals and unrecorded Tavern transitions; exact missing trigger tiles/route constraints need live captures. Proven public approaches remain available.
5. Unchosen Vivianne explanations, betrayal paths and destructive alternatives remain unknown; no deliberate betrayal or wasted-item tests.
6. General bonus rules and reputation GUI visibility; actual reward/own-legend data above must take precedence over guide labels.

Suggested regression coverage: captured initial pages and type-4 Wine stock;
distinct pages sharing pursuit 0/step 1; stale dialog/NPC/generation rejection;
cross-bar merchant interaction; missing anchors and post-arrival timeouts;
purchase/handoff baselines and partial inventory updates; no duplicate send on
ambiguous payment; timer expiry without bouquet acquisition; collection before
letter/delivery; own legend plus correlated final reward receipt; reconnect
without using new-session balances to prove an old payment; explicit secrecy
choice and preservation of other characters' ownership.

## Validation

Capture extraction consumed the observed stock packet exactly and confirmed its
Wine quote. The existing DAQuester parser validated all 15 packets: 13 non-close
dialogue/menu pages, including nine pages sharing pursuit 0/step 1. No synthetic
server conversation was added. Artifact structure/privacy and local links passed.

Bounded dialogue MCP changes passed complete Core 1,036/1,036 and App 182/182.
Final approved equipment-extension build passed Core 1,043/1,043 and App 183/183;
the unchanged timing failure and successful rerun are tracked separately in
[MCP improvements](mcp-discovery-improvements.md). Actual
packet regressions protect quantity arguments, secrecy spelling/pages and
acceptance/early wait without collection. Earlier unrelated dashboard and 300 ms
combat timing failures are recorded in the checklist/improvement notes; their
code was preserved. Builds/tests used isolated outputs and no real characters.
