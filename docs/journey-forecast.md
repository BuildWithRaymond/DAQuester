# Journey resource forecast integration

Core's `JourneyResourceForecast` takes a supplied `Func<DateTimeOffset>` clock. `Calculate(queue, observed, loadJob, helpers, sharedPicks)` reads snapshots and the existing journal/evidence rules. The desktop adapter is `DesktopRuntime.ForecastQueue(character, clock = null)`. It supplies the same helper ownership restrictions and persisted shared flower-pick records as the existing flower flow. It does not inspect credentials, acquire helpers, send commands, save estimates or change choices.

UI consumers can call `PlanQueue` and `ForecastQueue` on existing runtime change notifications, and refresh the forecast when the display clock advances. Bind the returned data to the separately developed dashboard/sidebar/character surfaces; these changes include no layout work. Planning and forecast data must never be passed into the runner as observed character state.

| Field | UI interpretation |
| --- | --- |
| `Entries` | Rows in unique saved queue order. `Covered=false` means an unavailable estimate, not zero cost. |
| `Items.RemainingRequired` | Remaining actions' item demand after recorded progress and separately identified declared prior turn-ins. |
| `Items.UsableOwned`, `Supplies` | Verified supplies reserved for this row, with the owning character named. Null means unavailable facts. |
| `Items.AcquisitionShortfall` | Items still to obtain, without crediting future picks or loot as owned inventory. |
| `Gold.Budget` | Gold consumed by remaining child handoffs, or a shield purchase budget at the existing configured price. The basis text labels live-price uncertainty. |
| `Gold.MinimumStartingBalance` | The execution rule's starting floor for the next action: 5,000 for a child handoff, 750 to buy the next missing shield, or the starter's 50,000 starting floor. This is not another amount to add to consumption. |
| `Gold.AllocatedOwned`, `Gold.Shortfall` | Gold reserved once from the observed balance in queue order; subsequent rows cannot reuse it. Null means unavailable. |
| `KnownGoldBudget` | Covered rows' budgets only. Unknown rows must stay visible. |
| `MinimumStartingBalanceLowerBound` | The larger of cumulative modeled budgets and starting floors, assuming no future gold income and configured prices. Uncovered costs can increase it; live prices can change the purchase budget. |
| `UsableOwnedGold`, `KnownGoldShortfall` | Sum of row allocations/shortfalls when all allocations are known. A preceding uncovered quest makes subsequent allocations unknown. |
| `Capacity` | One acquisition/transfer slot where the rules require it; not a requirement to carry every future flower or shield at once. Weight estimates are unavailable. |
| `Timing.EarliestEligibility` | Earliest modeled date only when current rules and known preceding modeled waits permit it. A blocked/unknown requirement has no date. Missing prerequisite leveling durations are never guessed. |
| `Timing.FirstActionNotBefore`, `LastActionNotBefore` | Optimistic bounds on the modeled flower handoffs/offerings. These are not guaranteed completion dates and do not establish eligibility. |
| `Timing.MinimumWait`, `KnownWaitLowerBound` | Known configured waits, including an initial cooldown. Queue order is respected; waits that run while an earlier quest is waiting are not added twice. |
| `ResourcesComplete`, `TimingComplete`, `Unknowns` | Coverage and uncertainty for modeled item/gold totals and waits. Even complete modeled totals exclude unavailable item weights, travel/combat time and final reward verification. |

A result never writes completion evidence or authorizes spending. The real runner's shop price, action gates, server refusals, reconciliation and reward proof still govern execution.

## Synthetic sample output

These examples are checked by `JourneyResourceForecastTests`. The supplied clock is October 13, 2026 at 10:00 a.m. America/Chicago (15:00 UTC); time values in the API are `DateTimeOffset` values.

| Scenario | Remaining demand | Usable supply | Gold budget / next-action floor | Known wait lower bound |
| --- | --- | --- | --- | --- |
| Sick Child, no prior turn-ins, daily mode | 5 White Blossoms | 0 | 25,000 / 5,000 | 96 hours, if the first pick is available now |
| Sick Child, 2 recorded and 1 declared prior turn-in; one flower owned; last shared pick one hour ago | 2 White Blossoms | 1 | 10,000 / 5,000 | 23 hours for the remaining pick |
| Altar, no prior offerings or shields | 10 Leather Shields | 0 | Up to 7,500 / 750 at configured price | 27 hours between first and last offerings |
| Altar, 2 recorded offerings, last one hour ago; 3 shields owned | 8 Leather Shields | 3 | Up to 3,750 / 0 | 2-hour initial wait plus 21 hours of spacing = 23 hours |
| Sick Child with three confirmed eligible helpers, all able to pick now | 5 White Blossoms | 0 | 25,000 / 5,000 | 24 hours under the current eligible-helper scenario |

A queue with Sick Child then Altar, 25,000 gold and five verified White Blossoms forecasts 32,500 gold in modeled budgets. It allocates 25,000 to Sick Child and zero to Altar, reporting a 7,500 shortfall. Its modeled starting balance is 32,500; the next child handoff still requires only 5,000. Gold or rings awarded later are not credited.

For Altar then daily Sick Child, with the last shared flower pick one hour before the supplied clock, the Altar's 27-hour spacing overlaps the remaining 23-hour pick cooldown. The first child pick can be modeled at hour 27 and its fifth at hour 123. The bound is 123 hours, not 146 hours from blindly adding the initial cooldown to both quests' waits.

## Limits

Only Cured the Sick Child and Altar Worship have material/action forecasts. First Steps exposes its existing starting-gold floor while reporting its spending and duration as unavailable. Any uncovered predecessor makes later gold and owned-item allocations unknown, because it could change those resources. Unsupported rows never receive assumed loot or zero-cost claims.

Recorded receipts are subtracted using existing progress rules. Player-entered prior turn-ins remain separate and explicitly labeled. A pending transaction is not subtracted; reconciliation can change the forecast. A saved verified completion or fresh own completion mark needs no new materials. Ten recorded altar offerings with no verified final reward require no further shields, while the reward remains unverified.

Helpers require explicit choices, current availability and fresh inventory. Busy or unlisted helpers are excluded; unknown helper eligibility makes the flower wait unavailable. A saved pick alone cannot prove a held flower. Only one verified held helper flower per supported trade visit is credited. Pick cooldowns include both flower quests' shared records and refusals. Estimates assume the currently eligible helpers remain available and successful acquisition; new facts require recalculation.

The three-hour altar spacing is the configured automation policy. It is not proof that the server enforces that interval. Neither spacing, five handoffs nor ten recorded offerings substitutes for the existing completion evidence. No travel/combat/login duration or guaranteed completion time is available.

No saved formats change. The shared inventory-capacity constant comes from the existing 59-slot rule; its derived free-slot property is excluded from serialization.
