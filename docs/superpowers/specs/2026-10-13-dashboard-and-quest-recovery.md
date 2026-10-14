# Character dashboard and quest recovery

Approved by the user on October 13, 2026, using the supplied card and list mockups.

## Dashboard

Add Dashboard to the existing Avalonia navigation shell and make it the initial
page. Use the shared dark palette, Georgia headings, Segoe UI body text and
original character portraits. Cards are the default view. A list view shows
compact rows and details for the selected character. Both views show saved
characters, observed level and class, actual connection/run state, the current
journey, saved stage progress and queue. Search matches character, quest and
location; status filters show real counts. Planner and pause/resume actions use
existing runtime ownership and eligibility checks. Offline observations are
labelled. Unsaved accounts do not appear as runnable dashboard entries.

Use actual journal outcomes for recent activity. Do not invent last-seen times,
inventory blockers, estimates, classes or quest state from mockup example data.
No new diagnostic controls belong in the player window. Empty results explain
how to add a character or clear a filter. Cards wrap and list details reflow at
the minimum window width. Preserve planner and account state across navigation.

## Character feedback

Each fresh active game message matching numeric `experience!` produces a brief
gold `+XP · Kill` popup above that character's sprite. An observed level increase
produces a gold level-up burst and `Level N!`. Feedback appears in dashboard
sprites and the planner's current journey. It is transient presentation, not
quest-completion evidence. Identical messages with distinct revisions both count;
ordinary refreshes, offline memory and a new connection never replay old effects.
Character identity and generation isolate events. Reduced-motion settings retain
text feedback while suppressing movement. Preserve active-message logs and replays.

## Quest corrections

Crypt Climb returns through the existing Crypt Vestibule retreat before recording
completion. Cancellation or failed travel keeps the durable run resumable, and
return travel also runs when resuming at level 11 after an incomplete return.

Recognize the observed Undine weapon-restriction popup, close it, remove the
equipped weapon only when that restriction occurs, await authoritative equipment
confirmation, then continue travel. Preserve inventory-space checks and unrelated
popups. Do not guess NPC identities or resend committed quest transactions.

Emerald Dawn requires confirmed registration, using the existing registration
evidence model and clearly explaining unknown or unregistered states. Faerie
minimum level becomes 20 provisionally, pending live confirmation of the actual
Shinewood Forest 7 threshold. Update availability, catalog text and regression
fixtures consistently without changing the four quest policies or reconciliation.

## Verification

Add focused behavior regressions for filtering, independent character actions,
generation-isolated feedback, return-before-completion, restriction handling and
quest eligibility. Run both complete xUnit projects. Build the desktop app and
manually inspect dashboard cards, list, empty states and compact layout with
isolated synthetic data. Never operate real characters or modify, restart or
close Excalibur. No packaging changes are planned.
