# Optional quest scheduler

Status: approved by the user on 2026-10-12.

## Requested behavior

Add a Scheduler button beside the header Launch button. It opens a separate
popup using the shared dark theme. Scheduler is entirely optional and disabled
by default. It runs only while DAQuester is open, including when minimized;
closing the popup does not stop an enabled scheduler. Closing DAQuester stops
scheduling. No Windows service, startup task, or automatic app launch is added.

Users choose a saved character and an executable quest, then either a local
date/time or the earliest eligible time. One-time scheduling is available for
every currently executable quest, subject to its requirements and saved choices.
Guide-only catalog entries cannot execute and explain that limitation. A chosen
time never overrides a verified cooldown or terminal quest policy.

Recurring schedules run at the next eligible time for supported repeatable
quests. Spare a Stick uses eight hours after verified completion and stops on
Callough's permanent outcome; a Mileth legend alone does not end its repeats.
Perfect Hairstyle uses seven days after a verified attempt and stops when the
Suomi legend is verified. Existing quest requirements, including Stick's level
range, remain authoritative.

For quests with waits inside their ongoing progress, scheduler mode performs
the currently eligible work, saves its checkpoint, closes its owned client,
and resumes when the next work is due. This includes Altar Worship offerings
and daily flower progress. A scheduled wait is not quest completion. Existing
ordinary runs and queues retain their behavior.

## Alternatives and recommendation

1. An app-managed scheduler is recommended: it reuses the credential vault,
   quest journals, connection tracking and game client ownership, and is easy
   to stop and inspect in DAQuester.
2. Windows background scheduling could run with DAQuester closed, but adds
   process coordination, background startup and separate shutdown behavior.
   The user chose scheduling only while DAQuester is open.
3. Extending the existing Repeat checkbox would be smaller, but would not
   provide an explicit, separately enabled login/run/logout mode or general
   one-time scheduling. Use a separate scheduler.

## Popup and explicit acknowledgement

The popup contains a prominent warning at the top, an Enable scheduler switch,
a schedule editor and the saved schedules. Each saved row shows character,
quest, mode, next local run time, status and actions to edit, pause or remove it.
Needs-attention rows show the reason and an explicit review/re-enable action.
No diagnostic IDs, calibration or protocol controls appear in this window.

Warning copy:

> WARNING: Scheduled logins can boot you from this character.
> When a schedule becomes due, DAQuester will use the saved login, run the
> quest and close the game window it opened. If you are playing this character
> in another session, the scheduled login can disconnect that session.
> Keep DAQuester open for schedules to run.
> If the server says the account is already logged in, scheduled logins for
> this character stop until you review and explicitly re-enable them.

Enabling scheduling requires an unchecked acknowledgement:

> I understand that scheduled logins can boot my existing session and that
> DAQuester will close the game windows it opens for scheduled runs.

Saving or re-enabling an armed schedule repeats the warning and requires an
explicit acknowledgement. Merely opening the popup, selecting a quest, or
using Launch does not enable scheduling. Existing Repeat choices do not create
or arm schedules. Each new schedule defaults to a single run; recurring mode
requires an explicit selection. The popup has Pause all and the main Stop all
action also disables scheduling so it cannot immediately launch more work.

## Execution and ownership

Require a saved login and saved quest-specific choices before a schedule can
be armed. Choices are collected in the scheduler setup flow without requiring
an unattended run to answer a modal dialog. Fresh observed state is checked
after authentication before any quest action. Missing or invalid choices,
unknown requirements and failed prerequisites require user attention.

Dispatch due jobs in due-time order, one scheduled run at a time. Reserve the
character across launch, authentication, quest work, checkpoint settlement and
cleanup. Do not conflict with queues, helpers, manual launches, individual
quests or control actions. A character already connected or with an existing
client known to DAQuester is blocked for review; scheduler never adopts and
closes an existing player window. DAQuester cannot reliably discover sessions
on another computer, which is why the login warning is mandatory.

Launch once, confirm the expected character and connection generation, execute
one verified attempt or eligible progress segment, durably record its result
and next deadline, then close only the exact client created for that run. Wait
for disconnect and replay export to settle. Never close Excalibur or unrelated
clients. If manual intervention takes over the scheduled client, stop automation,
retain the checkpoint, leave that client open and require explicit review.

If a known cooldown is in the future, wait while logged out. Recheck cooldowns
after login; if newer evidence advances the deadline, save it and close the
scheduler-owned client without submitting early. Use quest-specific scheduling
boundaries for internal waits rather than interrupting an arbitrary transaction.

## Login failures and recovery

Expose the server's login response through the existing owned-login transport.
Bind it to the specific login operation across redirects. Do not invent an
already-logged-in result code: preserve the observed response text and reject
any login denial safely, including unknown denial values.

An already-logged-in response creates a durable character-level login block.
It blocks all that character's schedules, including any scheduler-triggered
helper login, and survives popup closure, timer ticks, app restarts and schedule
edits. No automatic retry is permitted. Only an explicit review/re-enable
action can clear it. Other failed or unconfirmed logins also require review
rather than automatic retries. Ordinary helper retry behavior remains unchanged
outside scheduler mode; scheduler-triggered helpers must honor these blocks.

Unexpected dialogs, disconnects, missing evidence or ambiguous submissions
pause the job for attention. Save checkpoints and reconcile actual evidence
before any later execution. Never automatically resubmit an uncertain action.
Release scheduler-created clients on failure after saving unless a player has
taken over their window; failure cleanup must not clear the attention state.

## Persistence and restart

Store schedules separately from quest journals in user Data, with atomic writes.
Include stable identity, character, server profile, quest/recipe version,
requested time, recurrence, next due time, acknowledged/armed state, attempt
checkpoint and attention reason. Do not duplicate passwords into this store.
Store times in UTC and display local times with an offset when needed to
resolve daylight-saving ambiguity.

Previously acknowledged, enabled future schedules remain armed on app restart.
An interrupted active run needs explicit review and reconciliation. Overdue
schedules execute at most one due run after restart or system wake; they never
replay every missed interval. Unconfirmed login failures remain blocked.
Permanent quest completion retires its schedule. Normal shutdown first prevents
new dispatch, then cancels and awaits active work, saves progress and releases
only scheduler-owned clients without manual takeover. Update exit-safety checks
include pending launches and scheduled runs.

## Validation

Use deterministic clocks, synthetic sessions, login response packets and fake
client ownership. Cover due-time ordering, no early execution, saved-choice
gating, default-off/acknowledgement behavior, restart and wake, duplicate
dispatch, cooldown recomputation, permanent completion, character/generation
isolation, persistent login blocks, helper login blocks, manual takeover,
ambiguous submissions, checkpoint write failure, client cleanup and shutdown.

Run focused tests and both complete xUnit projects. Manually review the popup,
warning prominence, disabled states, keyboard operation, long schedule rows and
minimum window size using isolated preview data. Do not operate real game
characters, Excalibur, or its working copies for implementation or verification.
