# One-click updates

The player explicitly selected one Update click followed by automatic download,
installation and relaunch. This supersedes the earlier requirement for separate
download and installation actions. It does not enable unattended updates.

## Behavior

Keep optional startup checks off by default and check-only. Settings offers
Check for updates and one primary Update and restart action. Update checks when
necessary, downloads once, and authorizes only the verified pending version.
An existing downloaded package can be used after a fresh Update click. Consent
exists only in the current process; startup and ordinary exit do not install.

Download can proceed while work is active. Installation waits automatically for
quests, saves, control operations, active or connecting proxy sessions, pending
login redirects and DAQuester-launched game clients to finish. Never pause work,
close clients or log out characters to make an update possible. Retry readiness
from the existing resident timer before admitting scheduled work. Recheck and
claim the idle state before shutdown can cancel owners. Seal new proxy admission
for the final handoff; a failed pre-handoff attempt restores admission.

Save and quiesce before arming Velopack's bounded exit-wait helper. Keep startup
auto-apply disabled; use the existing safe exit handoff with restart enabled.
Failure retains the downloaded package but clears transient installation intent
so another Update click is required. Relaunch alone is not installation proof.

## Verification and release

Use synthetic backends and sockets only. Cover checks without consent, chained
success, existing downloads, duplicates, errors, changed pending versions, late
safety blockers, pending redirects, save failures and safe single handoff.
Review the dark Settings surface manually. Run focused checks, both complete
xUnit projects, release packaging, payload verification and older-updater
compatibility. Ship as 1.8.1 without altering published 1.8.0 assets. Do not apply
an update to a live player session as part of verification.
