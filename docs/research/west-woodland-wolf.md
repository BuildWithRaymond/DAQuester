# Wolf of West Woodlands: live discovery and runner limits

## Source boundary

The October 18, 2026 player-supplied [walkthrough](https://vorlof.com/quests/wolfofwestwoodlands.html)
describes Throttler on West Woodland 12-1 (454) at (21,20), a wolf-warning
reactor on West Woodland 19-2 (463) at (48,26), and a Wolf on West Woodland
20-1 (464). It claims Hydele deum, 60,000 XP and the legend
"Hunted down a wolf of West Woodland". The older
[quest index](https://vorlof.com/quests.html) instead lists 100,000 XP and level
70+. Those reward claims are hypotheses superseded by the live receipts below.
Level 70+ remains a provisional guide threshold; no lower-level boundary was tested.
The live character was level 99.

The public [463](https://vorlof.com/map.php?map=463) and
[464](https://vorlof.com/map.php?map=464) pages and the player's area diagram
provided an area outline, not authoritative portal coordinates. Initial read-only
inspection of local WorldLogs, a 1,432-record archive and a derived metadata
index found no records for 461, 463 or 464. Subsequent live discovery supplied
the actual passages. The earlier archival gap is not a remaining route blocker.

## October 18 live discovery

Persistent native MCP handled travel, Throttler's dialogue, wolf-area entry,
return and claiming. The player manually killed the Wolf barehanded. The
already running host had stopped its fight before attacking with
"No usable melee weapon is equipped or in the bag, so the fight stopped."
Current source support for barehanded combat was not exercised by that older
host. No host or Excalibur restart was performed for this discovery.

The [sanitized capture](../../content/evidence/datester-7.41/wolf-live-discovery.json)
keeps the relevant offered pages, route observations and own reward receipts.
The successful mixed manual/MCP discovery does **not** certify a fresh full
run of the new automatic recipe.

Throttler's opening offered "WoodLand" separately from
"West Woodlands Daily Kill Quest". The observed wolf path followed "WoodLand",
all offered informational Next pages, and the fresh "Sure." choice on
"Could ya do me a favor?". Repeated pursuit or step numbers do not identify
an acceptance stage. The active objective was
"((Find a Wolf in the path off West Woodlands 19-2))".

The exact entry page at (48,26) was:

> You can feel it in the air.. the viciousness of the wolves nearby.  These must be the wolves Throttler was talking about. Do you want to go in?

It offered Yes and No, with HasNext also true. Those offered choices must be
resolved from the current page; the Next flag alone does not authorize another
Next. Yes transported to map 464 at (0,23). Immediate departure to (1,23),
followed by movement to (25,25), established the interior approach. A visible
Wolf used sprite 2 and was observed at (21,27); that location is a sighting,
not a fixed spawn guarantee.

## Correlated own Wolf defeat receipt

The replay has no explicit 263/278 killing animation for this manual kill.
It supplies this sequence for the same known nonplayer Wolf target, previously
observed living on map 464, with no other visible players:

| Revision | Server observation |
| --- | --- |
| 2610 | Own-source effect 268 on the Wolf at 07:59:38.672658 UTC |
| 2613 | That Wolf removed by 0x0E at 07:59:39.602420 UTC |
| 2614 | Numeric XP increased from 19,998,523 to 19,998,524 |
| 2615 | Fresh type-3 active message: "1 experience!" |

The Wolf's displayed health bar was already zero at revision 2605 and at the
last own hit, revision 2610, while the entity remained present until removal
at 2613. A zero health bar alone is not a kill receipt. The parser retains the
prior living observation and permits further own hits at zero; an explicit
killing effect, removal, map refresh, identity change or disconnect clears
that life observation.

The additional parser path binds the own hit and removal within three seconds,
then requires one candidate and exact agreement between numeric XP and the
active XP message within the bounded three-second window. Another source's hit
invalidates ownership; map refresh, identity change and disconnect clear pending
correlation. Other visible players prevent this receipt. Target removal, a hit
or XP in isolation is insufficient.

The receipt's Evidence field is
"own-hit-removal-numeric-and-active-experience", distinct from
"own-killing-effect". This is correlated server evidence of the player's own
manual Wolf defeat, not an explicit death-animation receipt or a live test of
the new automatic combat runner. The later Throttler acknowledgement and final
item, XP and legend receipts remain separate quest-outcome proof.

## Observed upper route and return

These are directed live passages. Reverse travel is recorded independently,
and no neighboring exit tile or teleport identifier is inferred.

| Source and exit | Destination and landing | Immediate departure |
| --- | --- | --- |
| 457 / 15-1, (0,23) | 461 / 18-1, (48,23) | (47,23) |
| 461 / 18-1, (23,0) | 463 / 19-2, (25,48) | (25,47) |
| 463 / 19-2, warning at (48,26), Yes | 464 / 20-1, (0,23) | (1,23), then (25,25) |
| 464 / 20-1, (0,23) | 463 / 19-2, (45,25) | (44,25), then (25,25) |
| 463 / 19-2, (25,49) | 461 / 18-1, (23,1) | (24,1), then (48,23) |
| 461 / 18-1, (49,23) | 457 / 15-1, (1,23) | (1,24) |

The return then used existing navigation to Throttler's public-side approach
on map 454 at (19,20), beside his (21,20) anchor. Existing nearby WorldLogs
passages remain archival route evidence; new upper passages derive from this
live capture. An out-of-bounds archival (25,-1) record remains excluded.

## Actual reward and final close

The reward baseline is the own state immediately before claiming, after the
manual Wolf kill. Kill XP is not included in the quest's reward delta.

| Receipt | Before | After or delta |
| --- | --- | --- |
| Experience | 19,998,524 | 20,058,524; +60,000 |
| Gold | 44,745,115 | 44,745,115; unchanged |
| Item | Own inventory baseline | +1 Vanilla Potion; sprite 62, stackable |
| Own legend | Completion mark absent | Hunted down a wolf of West Woodland |
| Final Throttler popup | Terminal reward page observed | Explicit Close, then server dialog closed |

The observed item was **Vanilla Potion**, not Hydele deum. The own legend was
the exact bare text, without a dated suffix. Final item, XP, gold and legend
proof are separate from the terminal popup's actual close receipt. This run
does not establish different class, level or reward variants.

On return, the fresh opening again offered WoodLand. Its observed continuation
was, in order:

1. "Yes!  Thank you.  Now there be a dead wolf."
2. "Ya did yer Old buddy Throttler a big favor, I'll give ya present in return."
3. "Take this potion, I made it meself with the local flora."
4. "Come back some other time!  I may a have few things fer ya to do."

The first three pages offered Next. The final page had no choices or Next,
and was explicitly closed using its current observed identity. The fresh active
message was "60000 experience!"; the server then acknowledged dialog closure.

## Runtime guarantees and remaining validation

The recipe is nonrepeatable, with separate briefing, entry, hunt, return and
confirmation checkpoints. Fresh page identity and connection are saved before
submissions. Bounded informational reading stops for unknown choices and
prompts; an uncertain submission is reconciled rather than resent. Entry
requires the current scoped warning, actual landing and collision-checked
departure. Active messages remain in diagnostics and replay evidence.

Automatic combat uses the existing melee engine's target whitelist, health,
stranger and cancellation protections. Target-bound own-monster receipts can
come from an own-source 263/278 killing effect or the conservative correlated
hit/removal/numeric-and-active-XP sequence observed above. The Evidence field
keeps those sources distinct. Existing synthetic coverage and the captured
manual defeat remain separate from full automatic-run validation; an own
defeat alone never proves quest completion.

Before claiming, the runner saves inventory, gold, XP and fresh own legend
baselines. Completion requires a new observed potion (Vanilla Potion or Fifleaf
deum), exact new own legend
and correlated 60,000 XP proof. Reward proof is saved before terminal cleanup;
resuming cleanup cannot claim again. An unresolved old submission cannot be
settled from unrelated inventory after reconnect.

Existing runner, planning and protocol tests are synthetic and remain labeled
as such. The remaining release qualification is a **fresh full automatic run**
of the new runner, including its barehanded combat, own kill, route, menu,
reward and terminal-close checkpoints. Discovery completion does not satisfy
that qualification. Unseen openings, lower-level eligibility and other server
variants remain unknown.

## Later queued-run observations

The player's later October 18 queue reached the entrance warning at (47,25)
and (48,25), before the configured (48,26) navigation target. The exact warning
text and Yes/No choices matched discovery. Travel preserved that expected popup,
then Resume required the exact target position and timed out. Entry now accepts
the recorded warning positions and handles that known, current page after the
guarded navigation stop. Its Yes submission remains checkpointed once; new
generations or uncertain submitted entry never authorize another Yes.

The same player reported receiving **Fifleaf deum** from Throttler. Read-only
own state confirmed one Fifleaf deum (sprite 56) and the exact Wolf legend.
The original Vanilla Potion observation remains valid. The
[restricted queue excerpt](../../content/evidence/datester-7.41/queue-travel-wolf-variants.json)
separates these later observations from the original complete receipt baseline.
Automation supports both observed item names and records the actual gain while
retaining the same-generation item, new legend and 60,000 XP requirements.

If manual play completes the goal while only an unsubmitted approach is saved,
a fresh exact own legend can finish that queue checkpoint without game commands.
It records no inferred potion, XP or kill receipt. An uncertain submitted entry
or reward claim retains its reconciliation guards. These fixes have synthetic
coverage; the player's manually continued run is not a fresh full automatic run.
