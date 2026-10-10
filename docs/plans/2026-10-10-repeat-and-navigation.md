# Repeatable quest controls and main navigation

Spare a Stick retains a successful attempt in its journal. That checkpoint must
allow an explicit new attempt once the eight-hour cooldown expires, without
treating one reward as permanent quest completion. Resume continues checkpoints;
Start (Run again after completion) explicitly requests another attempt. Only
correlated permanent server evidence prevents further Spare a Stick runs.
Ambiguous transactions must reconcile before a new attempt can reset progress.
Nonrepeatable quests and Perfect Hairstyle's weekly-until-legend policy stay intact.

Button availability follows live runner ownership, rather than persisted status.
A stopped or restored job remains resumable. The repeat choice follows the
selected character and quest; confirmed permanent completion disables it.
Completed attempts show their cooldown or a clear Run again action.

The persistent left rail selects one main page: Quest journal, Quest library,
Account manager, or Settings. Reusable native WinForms page controls share the existing
dark Fluent foundation, typography, spacing, inputs and buttons. Navigation keeps
journal selection, library filters and quest execution intact. Choosing a library
quest returns to its journal. Account manager owns one editable table of names
and masked passwords, bulk save, add/remove and duplicate validation. New profiles
have no placeholder character. Saved names update the journal immediately. Blank
passwords keep existing credentials and rename preserves the saved password.
Settings contains only client/update preferences. Both pages save explicitly;
updates retain separate check, download and install actions and existing exit gates.
Secondary pickers and birth choices remain focused dialogs.

Verify synthetic runner regressions, all four quest policies, live ownership,
page navigation and state retention, standard/compact/small-screen rendering,
the full suite and native UI smoke checks. Never execute real character actions
or alter personal journal data during verification.
