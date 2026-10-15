# Fisk and Cap/Band of Danaan automation

The player approved the [automation design](../superpowers/specs/2026-10-13-fisk-danaan-automation-design.md)
on 2026-10-13 after completing live discovery. This implementation extends that
research into the existing app, runner, recipes, journal, queue and MCP. It does
not change Excalibur or add a controller. No real character is operated by a
build or synthetic test. The [implementation checklist](../superpowers/plans/2026-10-13-fisk-danaan-automation.md)
tracks validation separately from the original research milestone.

## Routes available in the app

**Fisk's Secret** (`fisk-secret`) follows only the observed secrecy path: Fisk,
Thulin, five Wine, Water Lilies, Fisk, Eeva, ten Grapes, Petunias, Fisk, Vivianne's
donation and final Pink Rose Next, Fisk's complete returned flower bundle,
Viveka's bouquet acceptance, collection check, Fisk's letter conversation,
Bertil's secret-preserving delivery and Fisk's final reward. The observed total
was 32,600 gold and the final reward was 750,000 XP with the own secrecy legend.
Purchases buy only missing quantities at current stock prices. Payments and
deliveries require their actual gold/item receipts. Nothing is discarded.

After Viveka consumes the flowers and charges 20,000 gold, the journal saves her
exact acceptance, timestamp, flower receipt and gold baseline. An hour permits
a check; it never proves collection. A fresh refusal schedules another check a
minute later, retaining the original paid acceptance. Collection requires the
new Special Bouquet. The runner never pays for a second bouquet automatically.

When other ready quests are already queued, Fisk yields its saved continuation
to them. The queue keeps Fisk and its due time without creating a completed
history entry. An active unresolved quest checkpoint is never preempted. After
other ready work finishes, the character returns for the check. While waiting
without other ready work, the shared allocator chooses a reachable, unoccupied
Suomi Tavern tile from **(10,1) through (14,1)** and verifies facing **down**.
Reservations prevent two local runners choosing the same spot and are released
when a character leaves. A full row preserves the paid wait at the safe approach;
no occupant is moved. Pause, disconnect and app exit preserve the saved wait.

**Cap of Danaan** and **Band of Danaan** both use one family recipe and journal
(`cap-band-of-danaan`), so the same character cannot queue two claims. The
available route is Rolan, Blaise, armed Erika, Blanchefleur, Tir's Loures Saber,
the Astrid Beggar's Baguette, his cousin's donation, Varuna's Gland, Greim's
supplied-Mold rite, Dreamer, Donnan/Cail, unarmed Meaveen's fruit, Kyros's Leather
Greaves, Donnan and Erin. The observed cost was 6,620 gold and the Cap reward
was 1,000,000 XP plus the matching own worthy legend. Carrying capacity is checked
at each acquisition and delivery. An owned usable weapon is required for the
armed shrine; unarmed destinations use guarded removal and bag receipts.

Every character must explicitly select **Donnan / Cail** and accept the
**supplied-Mold rite risk**. Choices are saved before running and lock once
progress starts. The rite cannot run without the actual Mold. Consecration,
reversion, Neal/Ceannlaidir and unknown alternate menus are excluded. Erin's
reward claim, final Next item, matching XP notice, fresh own legend and terminal
popup close are separate receipts. Saved cleanup after a reconnect closes only
that verified terminal page; it never claims the reward again.

The final item can be Cap or Band when the offered dialogue follows the captured
route: its actual inventory delta must match its fresh own worthy mark. **Band
delivery has synthetic coverage only.** Different Band wording pauses until
captured. The guide warns of varying dialogue; one live Cap character does not
certify every variant or determine what selects the item.

## Receipts and recovery

NPC identities, choices and page bindings come from current state. Exact
captured pages use fresh generation, entity and revision; recipe files contain
no captured packet IDs. Root greetings may vary, but the intended current choice
must exist. Unknown pursuit pages remain open for inspection.

The journal writes the transaction baseline before every choice, Next, payment,
purchase, equipment change and pickup. Later pages, inventory/gold changes,
actual transport and correlated active reward messages resolve those writes.
Uncertain submissions pause with their baseline; resume cannot blindly repeat
them. New-session balances do not resolve old payments or item handoffs.
Between completed stages, resume continues at the saved stage with current
identities. A transaction interrupted across connections may require manual
reconciliation rather than automatic replay.

Building entry uses the existing bridge's inward step. Quest-specific travel
keeps Room 7 at (2,3), exits Monk Hall north to (2,6) then (6,8), uses Aoife's
actual guidance confirmation and moves away from variable arrivals. Monastery
access uses its fresh respect menu and a northward arrival step. Interrupted
access transport reconciles actual arrival and moves away without resending Yes.
An unrelated Pattern Walker popup is never selected or advanced; a foreign
popup blocking an interaction pauses safely.

Carried Gland/Mold skip acquisition. Ground sprites are candidates, not item
proof: the inventory name proves the token. The existing melee helper can seek
the recorded Viper sprite and guide-listed Mouse; uncertain or wrong drops stop
and remain in the bag. Mouse farming for Mold remains a guide hypothesis, not
an observed origin. Low health uses the existing retreat behavior.

Existing own secrecy or Cap/Band worthy marks finish without another claim.
Unjournaled intermediate progress with a different NPC menu pauses for review;
it is not guessed from the guide. Guide-listed minimum levels (21 and 50) remain
provisional. Lower-level eligibility, other openings, permanent alternative
branches, exact timer threshold, reconnect during the live paid wait and reward
bonus variants still need live captures.

## Components and validation

The approved [item preparation design](../superpowers/specs/2026-10-14-quest-item-preparation-design.md)
adds a separate `QuestJob.Preparation` checkpoint. The ready queue's known
consumable demand is combined before NPC visits, with current inventory and
remaining handoffs determining the shortfall. Recorded prices help choose an
affordable queue prefix; actual quotes still gate spending and current mandatory
payment funds are reserved. It preserves saved dialogue task
indexes, paid waits, consent, and uncertain submissions. Danaan's common supplies
can be acquired before the opening; branch-only purchases wait for the captured
opening to identify their branch. Existing Branch and Mushroom collectors also
participate where their verified eligibility and source gates permit it. Timed
flowers and stage-produced items retain their existing unlock rules.

Gland source candidates include Mehadi Hema, Ellama, Shashi, Heart West, and
Heart East. [Vorlof's drop list](https://vorlof.com/items.php?id=327_Viper%27s+Gland)
uses monster sprite 49 for their Anala and Malini Vipers. Selection favors the
current map or the lowest available recorded travel cost. This is guide metadata,
not a new datester capture or proof of guaranteed drops. Inventory still proves
the exact item name. Unknown routes are not invented.

Shared mundane approaches, including travel services, stop on visibility after
safe entry departure. Room 7 and the Monk Hall corridor retain their deeper
stable approaches. Undine popup/status handling uses guarded removal and requires
the named weapon's bag receipt; unknown outcomes cannot send another removal.

The full Fisk regression preserves captured dialogue and resource deltas while
rebasing the synthetic carried balance for early Wine and Grapes. That normalized
purchase timing is synthetic coverage, not evidence of a new live runner.

The two recipes use `ObservedJourney` for bounded dialogue, current-stock
purchases, guarded equipment, token acquisition and known travel. Existing
`QuestRunner`, `QuestContext.BeginAsync`, atomic journals, queue coordinator,
character memory, map/navigation helpers, melee helper and `GameSession` commands
remain the execution path. App choices, catalog aliases, scheduling and MCP start
all share the same saved state. Protocol tracking recognizes the exact captured
active XP notices on the two reward maps; replays retain active messages as before.

Regression coverage includes full captured Fisk dialogue/resources with actual
parsed XP packets; final Next receipts; ambiguous payment/purchase/pickup and
equipment outcomes; foreign menus; interrupted guidance; stable inward routes;
waiting-row occupancy, direction and reservation release; deferred queue
persistence; own completion detection; final popup cleanup and reconnect;
synthetic Band receipts; app eligibility, choices and saved-wait readiness.
Build outputs and UI previews are isolated under ignored artifacts. Complete
suite results and manual review are recorded in [validation](../validation.md).

Live evidence remains in [Fisk research](fisk-secret.md),
[Danaan research](cap-band-of-danaan.md) and
[Cail continuation](danaan-observed-continuation.md). Those completions used MCP
discovery, not these new full automatic recipes. A fresh full automated live run
of each recipe still needs an eligible approved character; completed Fast must
not be used to repeat either reward. Unknown variants must obtain captures and
choice approval before extending automation.

## Changed-file map

| Area | Changes |
| --- | --- |
| Recipe and catalog | `fisk-secret.v1.json`, `cap-band-of-danaan.v1.json`; three catalog entries and existing artwork aliases |
| Core | `FiskDanaanActions`, `FiskDanaanEvidence`, `ObservedQuestState`, `ObservedJourney.*`, `FiskWaitingArea`; shared runner/contracts, memory, queue and repeat policy |
| App | Shared family aliases, eligibility, saved Cail/rite choices, paid-wait status, queue continuation, scheduler and control integration; `DesktopRuntime.Bouquet` |
| Protocol/MCP | Exact reward-notice recognition and existing start schema/description |
| Tests | Focused Fisk/Danaan, captured flow, purchase/equipment/gather, doorway, reward/cleanup, queue and app regressions |
| Documentation | This handoff, approved spec/plan, quest/control/provenance/validation notes and research milestone links |

Shared files also contain other agents' work. No commit, merge, release or raw
game assets are added by this task.

The tested desktop build was opened after checking that no saved schedule was
due to run. Existing scheduler state remains intact. Native MCP read-only state
confirms both recipes are loaded, zero connected characters and no control
actions. The existing chat-MCP registration now targets that tested executable;
the running chat transport may retain its earlier tool schema until reconnect.
An offline `tools/list` check of the new executable verifies both recipe IDs,
the Cail option and a false default for rite consent. No character was launched
or operated during implementation validation.

## Later Danaan variation and final wrap

[Lancelot's variation](danaan-lancelot-variation.md) adds the observed
Camille/Ase/Keefe/Logan/cousin opening as `loures-orders`, selected from the exact
fresh Blaise successor. The original Erika/Blanchefleur/Tir/Beggar opening and
all earlier evidence remain intact. Unknown openings still pause. Lancelot's
MCP discovery verified Cap, 1,000,000 XP, own legend and final popup closure;
179 sanitized observations record the flow. The observed Medenia hometown
return, caravan fares, Coast Bar doorway and shield removal are documented there.

The later changes passed 62 focused regressions, 1,100 complete core tests and
209 complete app tests. They are source changes, not deployed to the current
running host. The player requested wrap-up and no further MCP work. A fresh
full automatic live run, live Band outcome and other openings remain unverified.
Lancelot's old read-only intro checkpoint has tested reconciliation support but
has not been reconciled live. His completed reward must never be claimed again.

[Loures court discovery](danaan-loures-court.md) adds the player-approved
`loures-court` child path for the exact new Ase soldiers reply. Courtney's request,
75-gold Oren Brandy purchase and item handoff join the existing route at Logan.
Complete own replay receipts can recover that manually observed prefix from its
saved Ase checkpoint to the Cousin. This does not certify a full automatic run,
change the supplied-Mold consent or infer any final reward.

[Varuna's gatekeeper finale](danaan-varuna-gatekeeper.md) corrects a missed
paragraph already retained in Lancelot observation 67 for both Loures routes.
Pixel's authorized MCP final Next closed it without another Gland handoff.
Full original-stage replay receipts can recover this manual cleanup to Mold
collection; incomplete proof retains the saved pending informational Next.

[Travel recovery](danaan-travel-recovery.md) addresses Pixel's later repeated
pauses: read-only position refresh retries, single-submission Next waits through
latency, and transport reconciliation waits for actual landing coordinates.
The exact Monk Nightmare status on Path Temple 395 remains recorded but no
longer blocks the unrelated Danaan approach. Synthetic checks and the player's
message identification are separate from the retained read-only MCP evidence;
this change does not certify final reward or a full automatic live run.

[Reward cleanup, hub routing and dachaidh preparation](quest-reward-route-dachaidh.md)
documents the later final-reply timing fault and Pixel's actual Danaan/Fisk
completion reconciliation. Automatic cleanup retains the original resource,
active-message and own-legend proofs. Known world hubs can bridge incomplete
route data; the observed map-345 exit and upper Coliseum recovery are scoped
to their receipts. Dachaidh preparation checks own prerequisites and the actual
trainer quote, requires a player-supplied Wizard Skull, and preserves paid waits
and uncertain payments. Live Priest requirements/acquisition and a fresh full
automatic run remain unverified.

The later original-opening [Varuna successor](danaan-varuna-gatekeeper.md)
retains both captured final paragraphs. Agent's full recorded Gland handoff and
player final Next can reconcile to Greim's warning without repeating delivery.
Loures recovery still continues at Mold; the original warning is not skipped.

The [captured-run audit](danaan-captured-run-audit.md) adds Lancelot's exact
meditative-master Donnan referral alongside the original jail-keeper wording,
checks recorded transcript/packet successors, and exercises Cail continuation
through reward closure. NPC-cache membership is bounded by actual screen range.

[Blanchefleur's Fiosachd referral](danaan-blanchefleur-fiosachd.md) adds the
captured sailor opening, its informational loop, Keefe and Logan, then rejoins
at Cousin. Complete original replay receipts recover the manually finished
prefix without repeating the saved origin choice. Cousin payment and every
later handoff remain separate guarded stages.
