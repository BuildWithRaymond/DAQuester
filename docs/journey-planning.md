# Journey planning integration

`DesktopRuntime.PlanQueue(character)` returns Core's `JourneyPlan` in saved queue order. It reads current snapshots, the existing availability rules, journal choices and memory. It does not save projections. Call it again on the runtime's change notification after edits, choices or fresh state; there is no cached plan to invalidate. `PlanningAvailability(character, quest)` evaluates an unqueued candidate at the end of the current queue. The library uses this for selection only. `Availability` still describes real execution.

Each entry has `State` (`ReadyNow`, `Conditional`, `Blocked`, `Unknown`), `Reason`, `Conditions` and `ChoicesRequired`. Each condition names `EarlierQuest` and explains the goal that must be observed. Missing choices produce `Unknown` with `ChoicesRequired`; they can be configured through the existing dialog. An unknown entry with a supported earlier condition can be selected for planning; its missing facts still block dispatch. Projected goals in `Projection` are display data, never snapshots, completion evidence or execution permission.

For a synthetic level-1 character with 50,000 gold, fresh inventory and adequate carrying capacity:

| Queue entry | Planning state | Explanation |
| --- | --- | --- |
| Peasant's First Steps | Ready now | Existing starter requirements met; the existing briefing is still required to run. |
| Crypt Climb | Conditional | First Steps supplies Wooden Shield, Wind Belt, Sea Necklace and Loures Saber. |
| Emerald Dawn | Conditional after saving its recipient choice | Crypt Climb reaches at least level 11; current inventory, capacity, experience and legend requirements must also be known and satisfied. |

Before its recipient is chosen, Emerald Dawn is `Unknown`, with the Crypt Climb condition and `ChoicesRequired=true`. Putting Crypt Climb before First Steps makes it blocked by missing gear. Removing Crypt Climb leaves Emerald Dawn blocked by level. Neither starter quest can promise leftover gold: purchases use live prices and hunting repairs vary, so a subsequent gold requirement remains unknown unless it is already a known shortfall, which stays blocked. No other quest projects levels, items, gold, legends or outcome choices.

Queue start checks the head's actual availability and configures choices through existing dialogs. Later entries can remain conditional or unknown. Immediately before each runner dispatch, identity, generation, connection, availability, resources, cooldowns and saved choices are checked again, including after asynchronous runner preparation. Reconciliation continues through existing receipts. A failed attempt retains its checkpoint and queue. A completed predecessor whose observed goal is absent cannot authorize the next entry; the queue pauses with the unmet requirement.

The Windows integration uses `JourneyOverviewVM` as a shared, read-only presentation of the existing `PlanQueue` and `ForecastQueue` adapters. Queue rows show readiness and the named earlier quest, with full explanations in their tooltips and the forecast disclosure. Original checkpoint/attempt text stays separate. Dashboard list rows and expanded character details use the same presentation object for each character; choosing a dashboard character does not change the planner's selected character or a run's owner.

`MainWindowViewModel.Refresh` rebuilds these presentations after queue edits, choice saves and fresh runtime state. The existing two-second display refresh also updates clock-sensitive waits. No queue, journal or character-memory format changes are needed. Runtime run/pause commands and all existing dispatch checks remain the execution boundary.

The sidebar includes the correction from `fafe841`: a ready first quest does not claim that conditional or unknown later quests are ready. An unchosen first quest explicitly asks for its required choices. At minimum height, opening the forecast uses the current-journey card's space temporarily so the queue and Run/Stop controls stay reachable; closing it restores the current journey.
