# Narrow purchase bridge repair

The player funded Fast and confirmed that quest purchases are the agent's job.
The installed MCP lacks purchasing and main's bridge excludes the already
implemented `GameSession.Buy`. Fast is currently disconnected; the player has
explicitly authorized launching him through DAQuester again.

The selected design exposes `kind: buy`, `text: <observed item name>` through the
existing MCP and control bridge. A request selects the offered item through the
existing ordered send path, then either observes an item receipt or returns the
actual quantity prompt. The bridge binds the current
server-observed shop dialog, rather than accepting a caller-supplied dialog.
Existing queue, scheduler, connection and control ownership gates remain in force.
Stock details already parsed on main are retained in state.

No quest recipe, replacement bridge, raw packet API or bulk purchase loop is
introduced. Native UI purchasing is unavailable in this environment. Research
uses durable pre-submit baselines and verifies each observed item/gold receipt;
an uncertain outcome requires inspection, never an automatic resend.

Implementation order:

1. Add schema and runtime-admission tests; observe their expected failures.
2. Add Buy to the existing schema/allowlist and bind the observed dialog.
3. Run focused checks and both complete xUnit projects, preserving unrelated failures.
4. Build an isolated local research runtime. Inspect current owners and owned
   clients before changing only the idle DAQuester process started by this task.
5. Launch Fast with his existing encrypted saved login, inspect his new generation,
   balance, own legend and live quest menus, then perform the required purchases.

The full quest remains a discovery/documentation task. Excalibur processes,
working copies, other characters, user-opened clients and existing queues are
outside this repair.

Validation: schema and runtime admission regressions both failed for the expected
missing Buy capability before production edits. After exposing Buy and binding
the observed stock dialog, focused Core checks passed 17/17 and App checks 26/26.
Complete Core passed 1,008/1,008; complete App passed 181/181. Existing queue files
were unchanged across the local runtime transition. The old research app had
already exited before any stop action; no game process was closed. The repaired
app was opened visibly at the player's request, and Fast's authorized saved login
confirmed a new generation with 1,000,000 gold. No full quest recipe was added.

Live Wine selection exposed a second defect. Observation 47 showed Viveka's
type-3 quantity prompt, with server argument `Wine`. Input `5` sent only the
quantity (captured client 0x39 at revision 723), omitting the item argument. No
Wine arrived and the gold remained 1,000,000 through observation 51. The current
stock has no item named `5`. Read-only Excalibur source confirms the item-name,
quantity contract; that source is not proof of a server purchase outcome.
`fisk-wine-quantity.json` retains the four relevant packets, including the
malformed request. No identical retry was made.

Two new synthetic regressions failed before the correction: retaining the
type-3 prompt argument, and returning a fresh quantity prompt from Buy without
claiming receipt. `DialogPackets.Continue` now sends the observed prompt argument
followed by input. `GameSession.Buy` recognizes only a newer, same-generation,
same-NPC quantity prompt naming the selected stock item. It never automatically
answers or repeats the purchase. Focused Core passed 19/19 and full App 181/181.

The idle research-owned runtime and its research-owned Fast client were replaced
to load that correction after exporting evidence and proving there were no other
connected characters, active queues or control owners. Other client processes,
Excalibur and saved paused queues were preserved. Reconnecting must inspect gold,
inventory and Fisk's task again before any new purchase.
