# Current journey and queue-only planner

The user requested that the journey card represent a started run, rather than the
selected library quest, and directed removal of individual quest controls.

The card uses the selected character's active runner, running queue, paused queue
checkpoint or active Scheduler ownership. A planned queue, historical journal or
library selection cannot create a current journey. Stopping or finishing returns
the card to “No journey running”, with an Idle state and no progress/reward trail.
A queue between entries shows a generic in-progress title until its next active
quest is known. Changing characters presents only that character's journey.

The planner offers Run/Pause/Resume queue and Stop queue, including in its folded
bar. A one-entry queue follows the same verified-attempt and reconciliation rules
as longer queues. Start/Resume/Pause/Stop individual quest controls and the old
repeat checkbox are removed. Fast mode remains a character preference.

Add to queue and Change choices move into Quest details and follow library
selection independently of the current run. Required choice locking and queue
ownership restrictions remain. The optional Scheduler and its explicit warnings
remain available. Runtime entry points used by Scheduler and the control bridge
are preserved; this change concerns the player planner workflow.

Validation covers idle browsing/planning, saved checkpoints, queue transitions,
paused ownership, stopping/completing, character changes, scheduled ownership and
selected-quest planning. Both complete xUnit suites run, followed by manual
inspection of native isolated preview captures. No real game character is used.
