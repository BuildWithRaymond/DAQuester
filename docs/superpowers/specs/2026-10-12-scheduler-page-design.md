# Scheduler navigation page

Requested on October 12, 2026: move Scheduler from a header popup into its own
sidebar page, between Characters and Run history. Keep its optional login
warnings, explicit acknowledgement, enable/pause controls, choices, scheduled
times, saved entries and review behavior. Navigating away must retain the draft
and must neither pause nor enable scheduling. Refresh the character picker when
saved accounts change. Use the main window as owner for quest-choice dialogs.

The page uses the shared dark shell, with a scrolling warning/form/list area and
fixed Enable scheduler/Pause all footer. Stack the form and saved list in narrow
windows. Move the saved-schedule status indicator into the sidebar. Remove the
header Scheduler button and dialog Close/Escape behavior.

Keep the new gold quest medallion in the brand image, shared window icon and
executable resource. Verify compiled icon frames against the source ICO. Verify
navigation never changes scheduler state, run focused and both full xUnit suites,
and manually inspect normal/minimum-size native previews with synthetic data.
