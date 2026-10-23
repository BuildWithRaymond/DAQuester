# Research potion preparation and healing

The October 18 player request adds Exkuranum preparation and healing to Wizardy
research. This supersedes the original exclusion of potion behavior, while
forced grouping remains excluded. The player identified Blackstar Research as
the intended shop. Existing route data names map 3214 Blackstar Village Research;
reference code approaches (9,7). These are player reports and code facts, not a
new live capture of that merchant or its stock at the time of the initial change.

The runner resolves a unique visible merchant, accepts only its fresh Exkuranum
offer, and buys 30 minus the carried quantity. Purchase proof includes the named
inventory increase and exact quoted gold debit. Research and potion submissions
have independent checkpoints, so using a potion cannot replace a pending research
result. Each dose requires a one-potion debit and actual own HP at maximum.

Result waits wake on health loss and preserve observed research status messages
before potion messages can replace them. Health cannot establish research
success or failure. Unknown results remain pending even after successful healing.
Current resource state cannot establish a dose from an earlier connection;
pending actions are preserved without sending them again.

Validation uses synthetic in-memory sessions and local TCP peers. No live
character is operated during builds or tests. Those initial checks did not
certify the Blackstar merchant stock, price, potion effect or a full automatic
live journey.

October 18 validation completed with a clean Release solution build (zero
warnings and errors), 175 focused Core cases, 33 focused app cases, and both
complete xUnit projects: 1,693 Core cases and 370 app cases passed, with no skips.
The complete projects ran with normal Windows permissions because the restricted
sandbox denied access to the synthetic tests' uniquely named local pipes.

## Observed purchase and continuation failure

The player's subsequent October 18 scheduled run reached Jana in Blackstar
Research at (9,8). Its saved server replay records Exkuranum offered at 100,000
gold each, the quantity prompt carrying the exact `Exkuranum` argument, a
3,000,000-gold debit and 30 Exkuranum received. The
[restricted evidence excerpt](../../content/evidence/datester-7.41/ciad-greeting-research-shop.json)
contains only the relevant own receipt fields and merchant page. Other players,
unrelated inventory, credentials and local paths are omitted.

The runner then paused with `Server evidence timeout.` while closing the shop.
The scheduler closed its owned client after that pause; the diagnostics do not
establish a native client crash. Code inspection identifies the mismatch:
merchant dismissal is local presentation and retains the last authoritative
server page, while potion preparation waited for the pursuit-specific server
close receipt. The corrected continuation records a successful local dismissal,
retains an exact-page return-to-research checkpoint, and resumes a verified
purchase without paying again. The checkpoint remains recoverable until actual
research-room arrival. Unknown pages and uncertain submissions remain guarded.

This capture establishes the observed merchant offer and purchase only. Potion
healing and a complete corrected automatic research run remain unqualified live.

## Empty-only restocking and setup recovery

On October 23 the player requested using the remaining potions, including the
last one, then refilling to 30 only at zero. This supersedes topping up before
each visit. Fresh inventory still supplies the count across all bag stacks;
an existing purchase checkpoint must reconcile even when potions are carried.
Actual research/result reconciliation and the full-health requirement still
precede another supply trip or experiment.

The saved history records ten experiment results between the October 17 and
October 18 observed rests, split across resumed sessions. Earlier carried-batch
receipts are not single-parchment attempt counts, and this history does not
prove a universal server cap. The requested empty-only policy is independent
of that observed count.

The later interruption followed a successful result and two verified failed
experiments with Exkuranum healing. At the next Begin choice, the server sent
the Specific menu and immediately closed it. A bot selection and a forwarded
client selection were observed 69 milliseconds apart; the extra client command's
cause is unknown. The saved transaction was setup-only `research-menu`, not
`research-submit`. Original-connection balances were unchanged. See the
[restricted excerpt](../../content/evidence/datester-7.41/wizardry-research-setup-closure.json).

Restart only the recognized pre-experiment menu cursor after closure or
reconnect. Preserve earlier attempts, rewards and choices; record no new result,
completion or rest. Unknown popups and conflicting result/cleanup state stay
guarded. Purchases, deposits, potion doses and actual experiment submissions
retain their original receipt requirements. No player Data is edited to recover.
