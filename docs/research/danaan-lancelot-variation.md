# Danaan variation on Lancelot

Research started October 13, 2026, America/Chicago. The player explicitly asked
to navigate the current Lancelot variation through the existing MCP, record it
and add automatic support. Other agents' work, characters, queues and sessions
remain preserved; no Excalibur changes, release or commit.

## Durable checklist

- [x] Inspect instructions, existing evidence, current recipes and journal rules.
- [x] Identify Lancelot's connected state and paused family checkpoint.
- [x] Confirm saved Donnan/Cail route and supplied-Mold rite consent.
- [x] Capture the unresolved Blaise root and later matching initial response.
- [x] Observe a genuinely different Blaise response after the player's one Resume.
- [x] Follow current offered dialogue through MCP without repeating submitted pages.
- [x] Record variant-specific requirements, routes, choices and resource receipts.
- [x] Capture convergence with the previous Varuna/Cail ending.
- [x] Capture final item, XP message, own legend and terminal popup close.
- [x] Extend the approved recipe/journal design using exact captured branch selectors.
- [x] Add failing regression tests, implement and run both complete xUnit suites.
- [ ] Reconcile the character's actual checkpoint and report any remaining gap.

## Initial evidence

Lancelot was alive at Loures Library 3041 (10,12), level 99, carrying 215,961 gold,
with 50 occupied bag slots and 63 free weight. No Danaan completion mark or quest
supplies were present. The queue was paused with its family checkpoint intact;
no control action was running. Saved choices were Cail and accepted supplied-Mold
rite risk. No new permanent choice is selected without player approval.

The pending action was Blaise's `Gods and hats` root selection. Its later server
page matched Fast's captured `Ssshhh!! Quiet. You are here to help that boy in
Abel, right?`, offering `Yes!`. The initial pause exposed a separate timing gap:
plain `choose` sends without waiting for a response, while the runner immediately
checks for its successor. A bounded receipt wait is needed, without retries.

The player resumed once. The existing submitted `Yes!` then produced a different
page: `Don't be so loud. We received orders not to ever touch the subject, and
the guards make sure the orders are followed.` Its actual choices were
`Do you know anything of the boy's dream?` and `Why is the subject forbidden?`.
This is observed variation; its cause is unknown. Lancelot's class or statistics
must not be assumed to select it without comparative server evidence.

Only this already-paused queue is released for authorized MCP research. Its
saved pending action and route baseline are retained. Discovery records actual
current pages and receipts; it does not blindly replay the pending `Yes!`.
Sanitized captures include quest items, bag count and relevant own marks only.
Unrelated personal legends, credentials, login packets and other-player state
are excluded.

## Live completion and ordered stages

The player approved unrestricted necessary spending/combat and existing saved
choices. No consecration, reversion, betrayal or destructive alternative was
selected. The [179-observation capture](../../content/evidence/datester-7.41/danaan-lancelot-variation.json)
includes every discovered page, resource receipt and final own-mark inspection.
Fast's earlier opening and its fixtures remain intact. Variation cause remains
unknown; never select a branch from class, level, nation or guessed history.

| Stage | Current menu / movement | Proof in observations |
| --- | --- | --- |
| Blaise 3041 (10,12) | Dream question, then why forbidden; read each Next and close loop | 1–6: confiscated book, Camille referral, no resource change |
| Camille 3013 (4,4) | About orders → nicely tanned → Abel job → why soldiers | 9–13: actual pages; final Next closes |
| Ase 195 (7,2), public side | Unusual events → Ainmeal → combined `Tenes? Ealagad?` → Soldiers → Gods | 24–29: actual loops and Fiosachd referral; guide option presentation differs |
| Aoife 393 (3,5) | Guide Reception → current guidance question → Yes | 35–38 and later visits: 394 transport and immediate left step; no dedication |
| Keefe 346 (6,4) | Seek answers → Fiosachd → will not tell of cap | 40–43: Luathas referral, final Next closes |
| Logan 398 (6,4) | Luathas/the Cap → eager to help boy → what next | 44–47: marsh/beggar referral, final Next closes |
| Beggar 3071 (3,2) | Say `Cousin`; offer 1,000; courtesy loop; cap → origin → strange | 49–54: 215,961 → 214,961 gold; Mukul referral |
| Varuna 3075 (30,21) | Say `The Cap`; Next; return with desired item | 55–59: actual request; navigation timeout reconciled from arrived state before speech |
| Enchanted Garden 622 | Existing fight controller, priority sprite 49, goal Gland sprite 327 ×1 | 60–65: new Gland, full health, controller ends at goal; combat XP separate |
| Varuna delivery | Same speech; have what needed; boy cannot understand gods; old man/book | 66–69: Gland consumed; dead-man referral; final Next closes |
| Crypt 1 | Existing fight controller, priority sprite 7, goal Mold sprite 338 ×1 | 70–72: Mold received, full health; exact drop origin still needs raw corroboration |
| Greim 3018 (4,4) | Talk to dead → departed soul → consequences → proper care → willing | 75–82: warning, saved risk consent and Mold checked; Mold consumed; 720 (14,27) → (14,26) |
| Dreamer 720 (15,6) | Read all offered story choices/Next; final `Wait, I have more questions!` | 83–94: complete transcript through Goodbye; real redirect interrupts control |
| Hometown return | Saved reconnect; Asilon Inn 10004 (10,5) → (9,5) | 95–100: new generation, Mold absent, gold/XP unchanged, full health |
| Medenia travel | Lenoa in Asilon 10000: current quoted Yes; Caravan 10999; Noam/Habab → Tagor Inn 1960 | 101–108: first 10,000 quote/charge; another 10,000 during onward native travel; replacement generation and arrival (3,7) → (3,6) |
| Donnan 348 (4,5) | Aoife access; seek son; accept two tasks | 109–115: actual Cail assignment and Meaveen referral |
| Meaveen 3007 (6,4) | Remove weapon **and shield**; say `Need Help?` | 116–126: weapon removal, shield refusal and guarded shield removal; ingredient request |
| Alleen 162 (10,4) | Current Buy stock: Cherry ×2 at 50, Grapes ×1 at 10 | 127–131: items and exact −110 gold |
| Meaveen delivery | Same speech; Next; have what needed | 132–135: Cherry ×2/Grapes ×1 consumed; lesson continuation stated, popup closes |
| Donnan report | Aoife access; same son menu | 136–141: first task explicitly complete, Kyros referral |
| Kyros 5210 (4,6) | 5220 (1,7) → (0,7); fresh respect/Yes; inward north; `Need Help?` | 142–149: consent, 5210 (14,13) → (14,12), greaves request |
| Steinar 182 (3,8) | Actual Buy: Leather Greaves ×1 at 4,000 | 150–152: item, weight +4, gold −4,000 |
| Kyros delivery | Fresh consent; arrival (14,12) → (14,11); same speech, have some | 153–162: greaves consumed, gratitude terminal explicitly closed |
| Donnan final report | Aoife access; son menu; close terminal referral | 163–171: both tasks done, Erin referral |
| Erin 345 (6,5) | Completed tasks; Next; own legend; final close | 175–178: XP notice, Cap, new worthy mark and actual server close |

Monk Hall exit throughout: (10,5) → door (11,5), corridor 344 landing (2,7),
immediately north (2,6), then stable (6,8). Do not pause at east (3,7).
Aoife's landing varied; derive and validate the adjacent left tile from actual
arrival. Monastery consent is the actual unnamed reactor, not a guessed ID.

### Coast Bar correction and routes

Observation 20 is the player's pause at 195 (7,1), not the first landing.
Observation 30's purpose misidentified that tile; its probe did not transition.
Observation 179 corrects this using the retained replay: entry 185 **(7,29)** →
195 **(8,5)**; exit 195 **(9,5)** → 185 **(8,29)**. Enter, immediately move to
195 (7,5), then public (7,2). Exit, immediately move to 185 (9,29).
Counter-side (5,4) was unreachable. Inspected Excalibur WorldLogs has no 195.json;
the imported routes were incomplete. Add only the observed pair and 10×10 size.

### Costs and actual reward

Quest costs **5,110 gold**: cousin 1,000 + fruit 110 + greaves 4,000. Lancelot's
Medenia return added **20,000** travel gold. Final **190,851** matches
215,961 − 25,110. First caravan quote is directly captured; second charge is
observed from balances during native onward travel. Do not attribute extra
travel cost to every character or assume unchanged future quotes.

Erin gave **Cap of Danaan ×1**, sprite 1059, **+1,000,000 XP**, active type-3
`1000000 experience!`, and fresh own legend
`Worthy of the Cap of Danaan, Deoch 223, Summer`. XP 73,908,048 → 74,908,048;
29 earlier combat XP is separate. Cap weight +4 and one slot. Final health full,
53 bag slots, 228/287 weight. No HP reward inferred from shield equipment effects.
Final consecration information explicitly closed; no consecration selected.

Successful fruit consumption proves the supplied 2 + 1 quantity; lesser amounts
remain untested. Missing Mold, destructive rites, other avatars, refusal and
consecration were not tested to fill gaps.

### Evidence limits and crash

Parsed MCP captures retain the complete newly discovered dialogue and relevant
resources, own legend and final closure. Original packets exist in selected
earlier successful exports only. First Medenia disconnect crashed the x86 app
while allocating a whole replay string. Final raw export also returned
OutOfMemoryException. **Do not claim a complete raw final replay.** New export
streams to an atomic temporary file, preserving schema 3 and active messages.
Original Fast evidence remains unchanged.

## Automation handoff

Keep the original **32-stage default** and add **26-stage `loures-orders`**.
Select only from the exact fresh Blaise successor and both offered choices;
persist the branch without resetting prior stages or the submitted Yes. Unknown
variants remain pending. Reuse ObservedJourney, native NPC/Next reader, guarded
purchases/equipment, combat item goals, maps, journal, queue and own-legend rules.
Collect Mold before Greim, following the observed single supplied-Mold visit,
to avoid a warning-only visit and another long round trip.

Separate active status from filtered quest reward evidence. First entry refusal
stops movement. Reserve bag capacity and remove slots 1 and 3 with separate
receipts for unarmed shrines. Await late root, stock and guidance replies instead
of repeating actions. The old read-only Blaise intro can settle from a current
own completed mark; pending purchases or deliveries cannot.

Same-generation travel needs actual arrival/away proof. Known Dreamer return
may reconcile across generations only with saved final return submission,
recorded redirect and correct owned hometown arrival. Native NPC transport saves
its mapped redirect destination; uncertain fares remain blocked. Only an already
active queue paused for that connection handoff resumes automatically, with
saved Cail/risk consent, eligibility and no competing owner. User Pause/Stop,
idle research queues and unknown destinations never restart automatically.

Regression tests cover both openings/unknown reply, branch persistence, late
root/stock, both armaments, first refusal, doorway/public approach, only recorded
owned redirects, wrong map/character/no proof, explicit interrupted MCP error,
existing mark versus pending purchase, streamed replay, and existing final
item/XP/legend/close and Fisk waits. Complete project results follow validation.

Still needed: fresh end-to-end live run of the new automatic branch; live Band
wording/reward; further openings and their causes; Neal; other hometown returns;
full raw final replay; controlled Mold drop origin. Two Cap characters and
synthetic tests do not certify those variants.

## Wrap status

The player ended further MCP work. No additional live actions, deployment,
restart or release were performed during wrap-up. The current game session is
left open; the new source is not deployed to the running host.

Validation passed: 62 focused regressions, all 1,100 core tests and all 209 app
tests. The app suite also built the Windows x86 desktop project. The initial
full core run exposed eight regressions from widening filtered quest notices;
that change was corrected by keeping active status separate, then the complete
core suite passed. No new interface layout was introduced; live review of the
new variant progress display remains part of the fresh automatic run.

The final observed character state is alive with full HP, at Chief Priest Hall
345 (6,5), with Cap and own completion legend verified and no dialog open.
The research queue is Idle. Its earlier read-only Blaise Yes checkpoint remains
in saved Data because manual discovery did not advance that journal. A narrow
completion-mark reconciliation is implemented and tested, but was not applied
live. Do not manually edit Data or repeat a reward claim to clear it.

All 179 sanitized observations are saved. The original 32-stage route and its
evidence remain intact alongside the 26-stage variation. Four unrelated saved
queue files retain their baseline hashes, and main remains unchanged. Complete
raw final packet coverage is still unavailable; the export failure does not
invalidate the separately captured reward, legend and closure observations.
