# Queued travel and woodland wolf interruptions

The player reported several Resume interventions during a seven-quest queue on
October 18, 2026. Read-only diagnostics separate three actual interruptions from
ordinary completed-attempt switches and Fisk's durable paid-bouquet deferral.
The [restricted evidence](../../content/evidence/datester-7.41/queue-travel-wolf-variants.json)
omits character identities, other players, unrelated resources and local paths.

## Faerie walking return

On map 545, movement acknowledgements arrived after F5 was requested. Actual
position advanced from (36,35) to (33,35), followed by the refresh's authoritative
position. There was no popup. The fast walker treated the existence of its
interruption callback as an interruption whenever refreshed position changed.
That returned before reaching the exit; the exit helper then waited for a map
change it had never triggered. Evaluate the callback instead of stopping merely
because one exists. Genuine popup, generation and position barriers remain.

## Glioca doorway

The actual map 3007 landing was (22,6). The selected south step toward (22,7)
was followed by a pullback to map 3006 at (12,19). A later resumed approach
acknowledged (22,7), returned through (22,6), then walked west through (21,6)
and (20,6) before opening Meaveen at (10,6). An acknowledged adjacent tile alone
did not establish a safe pause.

For this known doorway region, NPC approach now uses the observed westward
interior at (20,6), with current collision/occupancy checks. Unexpected arrivals,
entry refusals and changed generations retain their reconciliation guards.

## Woodland wolf

The exact known entrance warning arrived at (47,25), then (48,25), before the
configured (48,26) target. Travel preserved the popup, while Resume required the
exact target. The runner now handles the fresh known warning at the recorded
positions after that guarded travel stop. It saves the Yes submission before
sending and never repeats an uncertain submitted Yes.

The player also reported Fifleaf deum; fresh own state confirmed that item and
the exact Wolf legend. This extends the observed reward names beyond Vanilla
Potion. Automatic reward verification still needs a new named potion, new own
legend and 60,000 XP against the same turn-in generation and saved baselines.
Cross-connection observations alone cannot settle an old reward claim.

If manual play completes Wolf while only an unsubmitted approach is saved, the
fresh exact own legend can complete that checkpoint without game commands or
invented potion/XP receipts. Submitted entry and reward checkpoints retain their
guards. The paid Fisk continuation is independent and is not reset by this fix.

All corrected paths were tested synthetically. The player's manually continued
run does not certify a new uninterrupted automatic queue.

## Fisk delivery entrance

The later Fisk interruption occurred outside Bertil's building, before any
delivery submission. The walker sent one west step from Suomi (16,86) toward
the recorded entrance (15,86). The server returned direction 4 with the same
position, then F5 confirmed (16,86). No entrance occupant or popup was observed.
The underlying server rejection reason is unknown. See the
[restricted movement excerpt](../../content/evidence/datester-7.41/fisk-doorway-rejection.json).

The walker added the rejected entrance to its local avoidance set. Subsequent
route refreshes retained that exclusion and reported an occupied destination
without another entrance attempt. Recorded exit goals now remain available
for the existing bounded retry after movement settles. Intermediate rejected
steps still use detours; actual doorway occupancy, active entry refusals,
unanswered movement barriers and changed generations retain their guards.
Three failed attempts still stop instead of repeatedly pressing a blocked door.

The player's later Resume completed delivery and Fisk's final reward: 750,000
XP, the exact fresh own legend and final popup closure were saved. No player
checkpoint or paid-bouquet wait was edited, and no reward is claimed again.
