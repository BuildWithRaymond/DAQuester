# Wolf of West Woodlands

Implement the player's requested one-shot addition using the existing quest runner,
queue, scheduler and journal. Implementation and synthetic checks stay separate
from live discovery.

## Source boundary

The player supplied a Vorlof walkthrough: Throttler on map 454 at (21,20),
the wolf-warning reactor on map 463 at (48,26), a Wolf on map 464, and a
reward of Hydele deum, 60,000 experience and the exact legend
`Hunted down a wolf of West Woodland`. The older catalog index instead lists
100,000 experience. October 18 live discovery supersedes those item/XP
hypotheses: the actual reward was one Vanilla Potion (stackable, sprite 62),
60,000 XP and the exact bare own legend, with unchanged gold. Throttler's
terminal reward popup was explicitly closed after verification. Wolf sprite 2
is now observed on map 464; the guide's level 70 threshold remains provisional.

Persistent MCP handled live travel, dialogue and claiming. The player manually
killed the Wolf barehanded because the already running host stopped before
attack with a weapon requirement. This discovery is not a fresh automatic run
of the newly implemented runner. Existing source support for barehanded combat
is separate from what that older running host exercised.

The supplied area diagram establishes an area outline, not warp coordinates.
Read-only WorldLogs inspection initially lacked the upper passages; later live
discovery supplied their actual exits, landings and departure tiles. Add no
guessed portals, teleport identifiers, menu pursuits or dialogue transcripts. Unknown paths,
unfamiliar choices and uncertain submissions pause with durable progress.

## Execution

Use separate preflight, briefing, entry, hunt, return and confirmation steps.
Resolve Throttler from the current map/NPC state; read bounded offered Next
pages. Bind the observed WoodLand branch and Sure. acceptance to their exact
fresh pages; keep the separate daily-kill quest unselected. Preserve every
unknown choice. Save the page identity and
connection before each submission, including open, Next, entry and reward
pages. A timeout only authorizes reconciliation, never another submission.

Entry requires the actual wolf-warning page at the supplied reactor tile,
one Yes selection, an authoritative map-464 landing and immediate safe departure
from that landing. Collision, bounds and occupancy determine that departure.
Keep an unsettled movement checkpoint through timeouts.

Hunt only observed Wolf sprite 2 using the existing melee engine and its health,
stranger and cancellation protections. Retain target-bound own-monster receipts
from an explicit own-source 263/278 killing effect, or the captured sequence of
an own-source effect 268 on a target previously observed living, same-target removal, numeric
XP growth and an exactly matching active XP message. The correlation is bounded
to three seconds, one candidate and no other visible players; another source's
hit invalidates it, and map refresh, identity change or disconnect clears it.
The receipt's Evidence string distinguishes this correlation from an explicit
killing effect. XP or disappearance alone cannot finish the hunt; the captured
manual kill does not certify the automatic runner or finish the quest by itself.
A displayed zero health bar alone is not a defeat. Retain the prior living
observation through further own hits at zero until an explicit killing effect,
removal, map refresh, identity change or disconnect clears it.

Capture own inventory, experience and fresh legend baseline before returning
to Throttler. Complete only with a new Vanilla Potion, new exact own legend and
60,000 XP delta or a fresh exact XP active message. Preserve actual reward
messages. Cross-generation inventory cannot settle an unresolved old claim.
Save rewards before closing the terminal popup; a separate cleanup checkpoint
resumes without claiming again. Do not clear another conversation.

## Integration and validation

Expose the existing catalog entry as a nonrepeatable recipe, with level and
legend planning checks and ordinary dark-theme quest captions. No new settings
or diagnostics surface. Preserve existing synthetic coverage for known/unknown
menus, delayed replies, no-resend behavior, exact target kills, reward baselines,
generation isolation, journal persistence and cleanup. The player's latest
validation instruction is compilation only, with no additional test runs.
Review changed interface content manually.

The player requested implementation in one shot; no intermediate approval or
commit is needed. Live discovery receipts and the remaining fresh full-run
verification gap stay explicit in docs/research/west-woodland-wolf.md.
