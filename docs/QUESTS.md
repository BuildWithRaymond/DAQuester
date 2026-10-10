# Quest details and observed behavior

Use **Start quest** on an eligible connected character. The shipped profile contains actual acceptance and completion fixtures. It banks excess gold if necessary, follows Callough's menus, gathers missing branches, and verifies turn-in through server replies, inventory and experience. **Auto-calibrate** captures additional evidence; **Review calibration** inspects the profile.

Branch gathering rotates the three Mileth areas, then visits the East Woodlands
entrance through the world map. The Woodlands entrance sweep uses client collision
data and avoids exits; its branch drops still need live capture. Gathering keeps
going until six branches arrive in server inventory. Movement recovery waits for
fresh server coordinates after F5 before sending another step. Same-map threshold
transfers preserve the map's walls and wait for the observed landing.

For **Mundane Birth**, choose it in the quest selector, select a connected character
and click **Start quest**. Choose Spring, Summer, Fall or Winter, any whole-number
age from **12 through 40**, and Abel, Mileth, Piet, Rucesion, Suomi or Undine before
any walking starts. The form shows only the player's known examples: 12 gives
Deoch 25; 40 gives before Deoch 1. Other ages go unchanged to the server.

DAQuester visits Riona in Mileth Inn, follows the recorded conversation, submits
the chosen season/age/town and waits for `She winks, "It's on your legend now"` in
the status bar. It saves this actual server message and the chosen settings under
a separate character/quest journal. Resume uses saved choices. Birth never runs
on a repeat schedule and stops if Riona offers to change an existing birth, which
can cost up to 100,000 XP. A missing final response remains pending and never
repeats the town submission blindly. Nuclear and Clock completed this flow live;
their actual server notices and chosen settings are preserved in the shipped evidence.

For **Perfect Hairstyle**, select it and click **Start quest**. DAQuester checks
your own legend, walks through the recorded world-map gateways to Eeva in Suomi
Inn, accepts **Ponytail**, reopens her and submits **Yes I did** once. No silk or
other items are required by the documented Ponytail bug. Reserve two slots and
two weight for possible rings. A fresh Eeva response plus correlated rewards proves
each weekly attempt. **The quest is done only when Loved by Suomi Mundanes is
verified on your own legend.** Rings/XP without the mark save an attempt and
schedule another try seven days after its server reply. Each attempt retains its
time, actual rewards and reply; Pause/Stop retains the deadline. Restart restores
the schedule paused; Resume starts waiting again. Existing Suomi marks satisfy
the goal. Ambiguous submissions pause without resending. Suomi's entry notice
and Eeva's captured Next pages advance automatically. Nuclear's manually assisted
run received Emerald Ring and 15,000 XP; its actual packet replay verifies the
corrected conversation. Clock then completed the corrected full flow automatically,
including the Suomi notice and every Eeva Next page, receiving the same rewards.
His own legend still lacked the mark, so another attempt is scheduled a week later.

For **Entered Muisir Village**, select it and click **Start quest** on a registered,
living character of **level 1 or above**. DAQuester checks the character's own
legend and skips an existing mark. Otherwise it gathers only the missing **five
Undine Mushrooms**, picking up the actual ground sprites while approaching
Kiril and patrolling Undine. Other players trigger detours; a dry patrol keeps
running. Inventory updates prove collection.

At Kiril, DAQuester says **Tiny Cave** and follows recorded Next pages. A first
introduction can require a second conversation: this happens only after a fresh
own-legend inspection proves no claim and unchanged mushrooms/potions. Every
claim has a durable baseline. **Five consumed mushrooms, four new Shrinking
Potions and Entered Muisir Village on the own legend** prove completion.
Unexpected replies and ambiguous transactions pause for reconciliation without
blindly repeating speech. This quest has no repeat schedule. Potions remain
carried; entering the training cave is outside this recipe.
Aware's assisted calibration and Vows's fully automatic run both verified the
mark and four potions. Vows started with zero mushrooms; every pickup, dialog
and own-legend check was automatic. The shipped replay preserves that run.

Patrol rotates **armor-shop trees around (18,56)**, then **(54,43)** and **(64,44)**. Armor Shop entrance is (18,62); its adjacent tree park gets a radius-four sweep. Central/east areas retain radius three and live branch evidence. Actual DATester collision filtering yields 90 waypoints. Occupied patrol tiles are skipped and revisited on later laps; other players trigger detours. Server inventory proves collection; movement stops at six branches. A dry patrol never pauses gathering.

Callough's child-reminder reply saves **permanent completion** and disables repeats. Clock's actual response and optional Oak Stick are captured in the shipped replay. Passive tracking detects this reply during manual play as well. The full reply and server dialog IDs remain in the character's durable journal.

An ordinary reward completes the current attempt, without permanent completion.
The journal shows **Completed for now** and the next attempt eight hours after
the observed completion reply, including single runs and offline characters.
Validated Callough replies are retained across manual pauses; later correlated
branch/experience packets can complete confirmation without resuming automation
or submitting again. Missing or stale evidence keeps the transaction pending.

## Current operational limits

Three characters completed Spare a Stick in game. Nuclear received Stick,
Wooden Shield and 3,500 XP; Clock received Oak Stick, Wooden Shield and 3,500 XP,
with permanent completion. Awesome's corrected patrol run received 3,500 XP,
50 gold, Stick and Wooden Shield. Runs included manual pauses and reconnects.
The latest completion variants are replay-tested. Automatic excess-gold banking,
cooldown refusal and live player detours await live validation. Perfect Hairstyle's
manually assisted Nuclear run and fully automated Clock attempt are captured.
Both received Emerald Ring/15,000 XP; their weekly legend goals remain unfinished.
Mundane Birth completed live on Nuclear and Clock. Ambiguous submissions pause without resending.
Login and reconnect login are manual. Account manager edits character names and
stores optional passwords encrypted for the current Windows account.

The catalog contains **70 quests with legend rewards**, filtered from 129 guides.
Chance and conditional marks remain; 59 entries without quest legend rewards are
removed. Spare a Stick, Mundane Birth, Perfect Hairstyle and Entered Muisir Village have executable recipes.
Remaining entries are research metadata, with inferred
capability/dependency/cooldown notes marked for review.
