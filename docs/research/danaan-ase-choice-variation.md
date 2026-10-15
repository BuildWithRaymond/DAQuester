# Ase opening choice variation

On October 13, 2026 (America/Chicago), the player reported that Pixel's automatic
`loures-orders` journey paused at Ase with `Unverified Ase route: Hello.` Native
MCP state inspection confirmed Coast Bar 195, stable public approach (7,2), and
the exact `Hello.` pursuit page. The offered choices were:

- `Nice weather is it not?`
- `Many warriors fighting lately?`
- `Seen anything unusual lately?`

Lancelot's retained observation 24 in `danaan-lancelot-variation.json` has the
same greeting and first two choices, but its third label is
`Have you seen anything unusual lately?`. The runner rejected Pixel's menu
because that longer label was required; the greeting itself was recognized.
The label's cause remains unknown and is not inferred from character class.

The sanitized state capture is
`content/evidence/datester-7.41/danaan-ase-choice-variation.json`. It retains the
actual dialog identity, generation, revision and option numbers; those numbers
do not authorize a stage or choice. No live selection, movement, reward claim,
queue change or paid-wait change was performed during this inspection.

The recipe retains Lancelot's original label and adds Pixel's exact shorter
alternative only to this Ase page. Matching still binds the page text, speaker,
entity and generation. Exactly one offered accepted label must match; the
runner sends that actual label with the current page and revision guards.
Unknown labels and ambiguous menus continue to pause. Later replies still need
their existing exact receipts, and pending submissions are reconciled without
resending the opening choice.

Regression execution uses the packaged Ase stage with synthetic successor
pages, identities and revisions. Pixel's successor pages remain uncaptured, and
this inspection does not certify a new complete automatic run or quest reward.

Validation: 21 focused checks passed, including nine Ase cases. The Release
solution build succeeded. The full core suite passed 1,145 of 1,147 tests inside
the sandbox; its two isolated `ControlPipeTests` failed with named-pipe access
denied and both passed when rerun outside the sandbox. The complete app suite
passed 227 of 228 tests. The failing
`PresentationRefreshTests.Movement_updates_reuse_display_receipts_until_an_explicit_refresh`
checks presentation-cache journal reads in existing separate workspace changes;
those files were not edited for this fix.

The player then requested launching the build. The first launch inherited an
isolated desktop and was invisible to the player. That newly created empty
instance was replaced with a normal desktop launch; the actual DAQuester window
was verified visible and in the foreground. No Excalibur process was changed.
Pixel was disconnected, and no quest was resumed. Read-only saved-state checks
confirmed `loures-orders`, task 4/page 0 with no pending submission, and the
verified Fisk paid wait due October 14 at 12:03 AM America/Chicago. A fresh
automatic continuation still needs the player's reconnect and Resume.

The subsequent player reconnect exposed a different Ase successor. Later
authorized MCP discovery and the requested automatic branch are documented in
[Loures court discovery](danaan-loures-court.md). The uncaptured-successor limits
above describe the earlier root-menu inspection, not that later prefix.
