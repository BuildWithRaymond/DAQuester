# Tasks and wizardy research

Status: approved by the player on October 15, 2026. Implementation and synthetic
verification follow this scope; live certification remains a separate activity.

## Scope and selected approach

Add a Tasks navigation tab. Its first entry is Wizardy research, which creates
scrolls using the Specific / sorcery research route in Mileth. Reuse the existing
quest runner, journal, helper leases and scheduler, with task metadata separating
Tasks from the quest library. Existing recipes remain quests by default.

This keeps one owner per character and preserves checkpoint recovery and the
four existing quest policies. A separate task engine would duplicate these
rules. Putting research only in the quest planner would omit the requested Tasks
surface. No other activity moves between tabs as part of this change.

## Player workflow

The Tasks page follows the shared dark theme, typography and navigation patterns.
Show the selected character, task requirements, current progress, parchment,
Songs banked, assistant, and next eligible time. Provide Run, Resume,
Pause and Stop actions according to the existing ownership rules. Explain missing
requirements and uncertain submissions in ordinary game terms.

Before starting a new visit, show one setup dialog with these choices:

- Alone, or an optional registered wizard assistant selected from saved
  characters. Ask whether the player has an additional wizard to assist; if so,
  validate and bring that character to the research room.
- Run once, or opt in to a recurring schedule. Use the same repeat prompt for
  other schedule-eligible repeatable quests, displaying their own cadence while
  preserving quest-specific repeat and timed-continuation rules.

The repeat prompt says: "This is a repeatable quest every 12 hours. Would you
like to schedule this to auto run every 12 hours?" Other eligible activities use
their configured interval. Scheduling requires explicit boot-risk consent and
includes: "A scheduled login can disconnect this character if you are using it
in another program. We recommend using characters you do not play frequently.
This also applies to assistants DAQuester logs in."

Saving a schedule retains the existing distinction between saving and turning on
Scheduler. When it is off, show that the saved schedule is off and provide the
existing explicit enable action. Do not silently enable other saved schedules.
Existing schedules are reused or reviewed rather than duplicated. Resume uses
saved setup and reconciles pending actions before allowing setup changes.

## Travel and optional assistant

Travel to Mileth Black Magic Master, map 401 in existing route metadata. Leave
each actual doorway landing immediately and continue to a stable interior before
unrelated actions.

If the player selects a saved character to bring as assistant, use existing
helper ownership and login gates and walk it to a free nearby non-trigger tile.
Require fresh wizard and registration eligibility before using Assistant. Do not
take a reserved character, stop another queue or log out a player-opened session.
Declining an assistant uses Alone.

The player withdrew the proposed forced grouping and exkuranum preparation.
This feature does not require or perform grouping, a Blackstar supply trip,
potion purchases or automatic healing.

## Research loop

Use an available, reachable trigger tile among (6,9), (5,9), (4,9) and (3,9).
Choose using actual occupancy and collision, rather than walking every trigger.
Bind every selection to the current connection generation, dialog revision,
speaker/reactor identity, current page and exact offered choice. The first page
reported by the player is:

> Do you need help before beginning your wizardy research? If you don't know about research you should seek help, and thereby make sure you bring your ingredients, reagents, or other materia.

Select the reported sequence:

1. No, I am ready to begin wizardy research
2. Specific
3. sorcery
4. Alone, or Assistant followed by the selected character's exact name in the
   freshly offered name prompt.

Preserve the game's spelling. Read all offered Next pages. Unknown branches or
unproven prompts pause instead of selecting a plausible option.

Each research attempt needs one parchment scroll. Use an already carried one
first; otherwise buy exactly one from Dar in this room for the reported 500 gold.
Resolve Dar and current stock freshly. A different quote pauses for review.
Require both purchase receipts. Do not buy a batch of parchment. Before buying
another, settle the previous research outcome and any reported health changes.

Several attempts are allowed in one visit. A successful scroll is an internal
receipt, not the end of the visit. A proved poor outcome may consume parchment
and health without creating a scroll; record that separately. Neither parchment
consumption nor a health change alone proves success or failure. Preserve exact
outcome text and actual item, health and active-message receipts.

Stop the loop on this exact player-reported cooldown page:

> You have researched already. You cannot discover anything new until you have given your mind a long rest of four Temuairan days.

The player specifies a 12-hour cooldown. Persist its deadline and source receipt.
If no earlier verified deadline exists, use the first fresh refusal time plus 12
hours conservatively. Reading the same receipt again never extends the deadline.
Time passing permits a new attempt, not a claim of eligibility or completion.
An initial cooldown refusal with no work performed is recorded as cooldown,
not a successful scroll-production visit.

## Remote Song deposits through Dar

Check the fresh own inventory before research and after each resolved outcome.
For every carried item whose exact display name ends in " Song", aggregate its
quantity across slots. At 25 or more of that exact Song type, deposit the entire
carried quantity of that type into the character's own bank through Dar, without
leaving the research shop. Different Song types have independent thresholds.
Bank one observed stack at a time and reconcile it before handling another.

Dar is not a normal deposit-menu NPC. Use the hax deposit protocol found through
read-only inspection of Excalibur, described in
[the protocol reference](../../research/wizardy-research-protocol.md). In the
inspected implementation, a stacked item uses merchant operation 84 with the
actual inventory slot and quantity, followed by an NPC popup close. The operation
does not require an offered Deposit option. These are source facts, not a new
successful live deposit receipt.

Bind this operation specifically to the current visible Dar in map 401, current
character/generation, and actual slot, name and quantity. Persist the deposit
baseline before submission. Require the exact inventory decrease together with
a fresh correlated bank/deposit confirmation; sending the packet or removing the
item locally does not prove banking. Preserve any verified deposit before final
popup cleanup. A stale NPC, changed slot, refusal, full bank, unrecognized reply,
timeout or reconnect pauses for reconciliation, without resubmitting or silently
traveling to a normal bank. Do not continue research while a deposit is pending.

## Health observations

Research may reduce health, but the player is uncertain whether a brief zero
health reading represents actual danger. Do not classify zero HP alone as skull,
death or proof of a research outcome. Stop further research submissions while
zero HP is unresolved and wait within the existing bounded receipt timeout for
fresh state. Resume normal checks if health recovers and no skull signal exists;
otherwise pause with the actual observed state. A confirmed skull/coma pauses
for the player. Do not use a potion, escape scroll or revival automatically.

## Persistence and evidence

Persist setup, purchase/deposit receipts, each submitted action and its baseline,
resolved attempt outcomes, and cooldown before exposing progress. After ambiguous
purchase, research or deposit submission, reconcile fresh state; do not resend
the uncertain action. Connection changes invalidate page and resource assumptions
without erasing durable pending receipts.

The opening text, choice sequence, trigger tiles, Dar's reported price, cooldown
page and interval, remote Dar banking and 25-Song threshold are user-supplied
evidence. They are not new packet captures. Existing map records and the inspected
Excalibur deposit implementation are code facts. DAQuester's current deposit
implementation handles gold only; guarded item deposits require new support.

Unknown success/failure pages, assistant replies, live Dar deposit confirmations,
merchant details and skull signals must remain explicitly
unverified until observed. Synthetic fixtures exercise guard behavior without
being labeled live evidence. Unknown observations pause safely. Record sanitized
captures and gaps under docs/research and content/evidence/datester-7.41 when
available; synthetic verification cannot certify a full automatic live run.

## Validation

Add focused regressions for Tasks/library separation, per-character setup,
assistant eligibility and ownership, individual parchment purchases, multiple
research outcomes, exact cooldown matching and durable deadlines. Cover per-type
Song thresholds at 24, 25 and above, quantities split across slots, all Song
variants, direct Dar deposit packet encoding, stale NPC/slot guards, correlated
receipts, full bank, delayed acknowledgements, no double deposit, and final popup
cleanup. Test transient and unresolved zero health, reconnect and pending-action
recovery. Cover repeat prompts, explicit scheduling consent, scheduler-off state,
saved choices and ownership conflicts without changing the four quest policies.

Run focused tests, both complete xUnit projects and an application build. Manually
review minimum window size, long text, disabled states, focus and keyboard
actions with synthetic data. No packaging changes are planned. Build and test
without operating real game characters, modifying Excalibur working copies,
or closing/restarting Excalibur.exe.
