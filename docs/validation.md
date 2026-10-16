# Validation status

## Character legend popup — October 15, 2026

Each saved character now has a Legend marks button beside Remove inside its
expanded details, following the player's placement correction. It opens that
character's compact ordered icon/text list without changing the selected
character. The inline chips were removed. Full dates and long text
remain visible through wrapping and scrolling; Close and Escape return to the
card. Existing dark theme resources, fonts and chrome are preserved.

Own self-look packets retain actual legend icon IDs alongside the existing quest
text array. The same character-memory identity, revision and generation guards
persist these presentation entries. Old text-only or mismatched metadata uses a
neutral fallback, never an icon guessed from wording. Known empty, unobserved
and remembered marks remain distinct. Six new regressions failed before these
fields and view-model rows existed, then passed after implementation.

Focused checks passed **56 Core** packet/memory cases and **20 app** presentation
cases. Both complete xUnit projects passed with no failures or skips:
**1,431/1,431 Core** with serial collections and **285/285 app**, using normal
local Windows pipe access. The Release solution build succeeded with zero errors
and the existing `NU1900` warning for unavailable NuGet vulnerability data.

The placement follow-up passed six focused character/legend checks and repeated
both complete suites (**1,431 Core**, **285 app**, no failures or skips). Native
review confirmed the button beside Remove in expanded details and absent from
collapsed headers at minimum width; the popup remains unchanged.

The isolated native preview produced **69 synthetic captures**. Manual review
covered six legend popup states (observed, narrow, scrolled, remembered, empty and
unobserved), collapsed and expanded cards at minimum width, original installed
mark sprites, full long-row dates, Close and Escape. Updated documentation images
are `docs/screenshots/characters.png` and `docs/screenshots/legend-marks.png`.

The self-contained x86 app is `artifacts/legend-button-placement-fix/DAQuester.exe`;
its three managed assemblies and native DLL match the tested build. Reports are retained
under `artifacts/legend-popup-validation`, with placement verification under
`artifacts/legend-button-placement-validation`. Tests and previews use isolated
sample data. No real characters, existing sessions or Excalibur working copies were
operated or changed.

## Greim's carried Rotten Apple receipt — October 15, 2026

Read-only native MCP diagnosis and the restricted original replay showed the
successful rite consumed one Rotten Apple while leaving Mold intact, then
delivered the active trance message and actual Hall of Souls arrival. The old
resource contract required Mold consumption and repeatedly timed out at the
already submitted final page. No live gameplay, queue or session commands were
issued during diagnosis; the paid Fisk wait was preserved.

The exact Greim stage now accepts the proven apple alternative while retaining
normal Mold supply and consumption. Original-generation inventory, unchanged
gold/XP, fresh trance message and authoritative arrival are required. The saved
landing resumes its inward step; a character already farther into the hall is
not returned to the landing. Replay recovery requires the complete original
nine-action stage and receipts, preserves conflicting or incomplete checkpoints,
and advances only to Dreamer without repeating the rite. Rotten Grapes remains
an unverified player suggestion. See [research](research/danaan-greim-decay.md).

The regression suite was also run against an isolated copy of the previous Core
assembly: five cases failed, including the actual resource timeout, missing
restart recovery and acceptance of conflicting double consumption. All **27/27**
rite cases pass with the fix. Focused app checks passed **48/48**, including saved
page recovery without a pending submission, missing final Next, newer conflicting
replay data and preservation of actual landing/status fields in replay reads.

The Release solution build passed with zero errors and the existing `NU1900`
warning for the unavailable NuGet vulnerability-data endpoint. Both complete
xUnit projects passed using normal local Windows pipe access: **1,429/1,429 Core**
with serial collections and **281/281 app**, with no failures or skips. Reports
are retained under `artifacts/danaan-rite-validation`.

The self-contained x86 app is `artifacts/danaan-rite-fix/DAQuester.exe`. Its three
managed assemblies and native DAWindower X DLL match the tested build. It also
includes the preceding Faerie return and Danaan default-route changes. No live
application was restarted, no Excalibur working copy was modified, and these
synthetic checks do not certify a new full automatic Danaan run.

## Faerie return boundary and fixed Danaan route — October 15, 2026

The player reported an unnecessary Mileth trip after the Faerie visit and asked
to remove the single-route Danaan questions. The walking regression reproduced
map 500 where map 542 was expected; two saved-return cases paused in Shinewood 1.
Native departure tests initially rejected the missing settle command. Desktop
and scheduler regressions reproduced the Danaan popup, missing default options,
Choices status, and visible Change choices action. These nine cases failed before
the first fix. Two further regressions reproduced the redundant walking warning
after an already safe Faerie return, then passed with the shared escape check.

Faerie completion now records a safe Shinewood Forest 1 return or supported town
escape, after leaving the actual landing on a validated non-trigger tile. The
walking route validator ends at Shinewood 1. Pet/refusal and spell receipts still
prove the outcome; an uncertain escape reconciles without another submission.
Desktop and scheduler starts save Cail and supplied-Mold acceptance without a
choice popup. Existing locked or unsupported options remain guarded, and direct
MCP option validation is retained. No new automatic live run is certified.

The Release solution build passed with zero errors and the existing `NU1900`
warning for the unavailable NuGet vulnerability-data endpoint. Focused tests
passed **98 Faerie cases** and **13 app cases**. The complete app suite passed
**277/277**, with no failures or skips, using normal local Windows pipe access.
The complete Core/protocol suite passed **1,402/1,402** with serial collections
and no skips. Final reports are `core-full.trx` and `app-full.trx` in the retained
validation folder.

The isolated native preview captured **63 sample states**. Manual review of Cap
and Band confirmed Ready status and removal of Change choices. The removed
Danaan question is absent from capture flow; original game artwork is read-only.
The published app is `artifacts/faerie-danaan-fix/DAQuester.exe`; all three managed
assemblies and the native DLL match the tested build. Reports are retained under
`artifacts/faerie-danaan-validation`. Tests and preview use synthetic characters;
player Data, existing sessions and Excalibur were not operated or modified.

## Current-source integration and README — October 15, 2026

The README now covers all 14 automatic journeys, queue supply preparation,
captured Danaan branches and recovery, the successful Faerie bond offer,
Callough acceptance before gathering, original artwork caches, dashboard views,
scheduler actions, and DAWindower X managed launches. It distinguishes current
source from published release notes and retains the existing live-evidence limits.
The documentation index links the newer research and the current screenshots.

A clean, symbol-free Release solution build passed with zero errors and the
existing `NU1900` warning for the unavailable NuGet vulnerability-data endpoint.
Focused regressions passed **61 Core/protocol cases** and **26 app cases**;
the complete app suite passed **271/271**, with no skips. Sandbox current-user
pipe access initially failed; the same pipe and windowing fixtures passed with
normal local permissions. No production code or test assertions were changed
during this documentation refresh.

The complete Core/protocol suite passed **1,397/1,397** with serial collections
and no skips. A preceding parallel run had one timing-sensitive fast-walking
failure; that case passed in the focused run before the complete serial pass.
Reports are retained under `artifacts/readme-validation`, including
`readme-core-full-serial.trx` and `readme-app-full-local.trx`.

The optional native capture produced **65 isolated sample states** using
read-only installed artwork. Eight documentation images were selected: planner,
paused journey, dashboard cards/list, characters, scheduler, history, and settings.
Manual review also covered compact dashboard/scheduler and long-text states.
Sprites and quest icons are original game artwork; character identities and
progress are synthetic. Physical keyboard delivery and live quest execution were
not exercised. No real game character, player Data, or Excalibur was modified.

`Build-Release.ps1 -NoBuild` passed to
`artifacts/readme-release-20261014/release`, followed by an independent
`Verify-Release.ps1` pass. An initial packaging attempt exhausted disk space;
verified unused generated build outputs were removed after checking their
resolved paths, running executable paths, and absence of Data/game files.
The three packaged application assemblies and native DLL match the tested build.
Verification covers assets, checksums, feed, licenses, private-data exclusion,
and functional walkability for **1,420 of 1,435** maps. These local packages were
not published as a release.

## Readable journey markers — October 14, 2026

Long recipes previously shrank every step into a single narrow line beside the
sprite. Trails now keep 12-pixel numbered markers, use the full card width, and
wrap into rows with at least 24 pixels between centers. A final partial row keeps
the same columns and reading order. The active marker pulses at a row boundary
without drawing a diagonal through other stages. Number layouts and pens are
reused between animation frames. Checkpoint counts and completion rules are unchanged.

The planner's foreground sprite occupies a separate area below the trail, while
its faint enlarged accent remains behind it. Dashboard cards grow with their
content. Fixed-height compact journey panels scroll, keeping all captions and the
character reachable without compressing or covering markers.

Six focused layout regression cases cover long journeys, narrower widths, quest
changes, marker spacing and stage order. The original three measurement cases
failed before the fix because the trail reserved no row height. The focused
layout/progress/motion run passed all 16 cases. Optional isolated native captures
include Danaan at stage 2 of 32, a wrapped-row transition, completion, normal and
minimum planner/cards/list views, and scrolling to the character and captions.
No real game character or active player host was operated for validation.

The full Release solution build passed with zero warnings and errors. All 1,168
Core cases passed with test collections serialized. The first parallel run hit
Fisk's 10-second synthetic replay cutoff and the fast-walking fixture's
220-millisecond inventory timing expectation; both passed in isolation before
the complete serial rerun. No production timing or test assertions were changed.
Concurrent Danaan research and code edits remained outside this UI commit;
these results describe the built snapshot, not a final integration of that work.
The complete desktop project passed all 243 cases with normal runner settings.
The final isolated native preview produced 63 sample states. Manual review
confirmed the long trail, completed and wrapped-stage markers, normal and minimum
dashboard/planner layouts, and the scrolled character/captions. Captures use
read-only installed artwork; physical keyboard input and live game execution were
not used for this change.

## Queue display performance — October 13, 2026

Queue traffic previously posted a full-window refresh for every notification and
loaded the same checkpoint up to 70 times in one pass. Display invalidations now
share one pending callback at background UI priority, delayed 50 ms. Dependency
plans are shared within a pass and overviews are built on demand. Saved display
receipts survive movement-only updates; writes invalidate their affected entries.
Explicit refreshes read saved state again, while external changes are scanned in
the background on the existing two-second tick. Late scans cannot overwrite newer
receipts. Execution reads, write ordering, game messages, diagnostics and replay
recording keep their existing paths.

An isolated probe compared the prior integrated assembly and this implementation
on the same host: seven sample characters, six queued quests each, persisted sample
journals, two warm-up passes and 20 measured passes. It also read the selected queue,
current journey and dashboard sidebar properties. Mean movement-driven model refresh
time fell from **484.97 ms to 2.16 ms**; allocation per update fell from **10,387,042
bytes to 138,085 bytes** (98.67% less). A 50-notification burst posted **one** display
callback instead of 50. Explicit full reloads fell from **469.18 ms to 101.08 ms**.
These measurements describe synthetic model work, not live frame rate or end-to-end
render latency. No real clients or game characters were used by the probe.

The final Release solution build passed with zero warnings and errors. All **42
focused app cases** passed, including nine new refresh/cache tests. Both complete
xUnit projects passed with normal settings and no skips: **231 app tests** and
**1,147 Core tests**. That Core count includes the concurrent Danaan chat's nine
Ase cases; those quest changes are separate from the performance commit. Build
input hashes stayed unchanged throughout the final validation.

The native isolated preview produced **53 sample states** using read-only installed
artwork. Manual review covered original sprites in the current journey, Fisk's
paid wait, normal/minimum planner and character layouts, and the hidden forecasts.
Framework disclosure/reorder/removal events passed; physical keyboard delivery and
live before/after frame rate were not measured. The active player host and its
quest queue were not restarted or operated for these checks.

## Forecast visibility and character spacing — October 13, 2026

Whole journey forecast panels and the dashboard forecast summary are temporarily
hidden. Planning, queue readiness and real dispatch gates remain available. Expanded
character details now occupy a separate full-width row below the header actions,
with a single card inset rather than an unused Launch/Select column beside the form.

The Release solution build passed with zero warnings and errors. Focused disclosure,
dashboard and planning tests passed **16/16**; both complete xUnit projects passed
with normal settings: **1,138 Core tests** and **222 app tests**, with no skips.
Manual native preview review covered expanded/collapsed character cards and the
planner/dashboard at normal and minimum window sizes. The isolated capture produced
53 sample states; framework Space events opened and closed character details through
the two-way binding. Physical keyboard delivery was not exercised. No game clients
or live characters were operated; the active player host was not restarted.

## Merged worktree validation — October 13, 2026

The completed quest/dashboard checkpoint (`a00b975`) and planning/UI branch
(`dd34318`) are combined on main. Eleven overlapping files were reconciled while
retaining both observed quest families, saved bouquet waits, real dispatch gates,
reward-popup cleanup, character feedback and shared planning/forecast views.
Forecasts remain presentation estimates and cannot supply execution receipts.

The merged Release solution build passed with **zero warnings and errors**.
Focused runs passed **102 Core/protocol cases** and **59 app cases**. Both
complete xUnit projects then passed with their normal settings: **1,138 Core
tests** and **222 app tests**, with no failures or skips. The final Core run used
normal collection parallelism. Earlier timing failures did not recur in this
run; no walking assertion or timing threshold was relaxed. Local pipe access
was provided for synthetic fixtures. An initial sandbox-only NuGet audit lookup
warning was cleared by restoring with network access; auditing stayed enabled.

Two additional integration cases verify that Fisk and Danaan remain available
alongside shared forecasts, that unsupported resource estimates stay explicitly
unavailable, and that rendering a forecast changes neither observed character
state nor the saved quest checkpoint. New and merged C# files use the repository
formatter settings, with explanatory comments around receipts, ownership,
reconnects, waits and forecast uncertainty.

The merged native preview produced **55 isolated sample states**. Manual review
covered normal/minimum planning and dashboard layouts, partial-supply forecasts,
Fisk's saved paid wait, Danaan library filtering and Cail/rite consent. Framework
queue reorder/removal events passed; physical keyboard delivery was not exercised.
No real game character or Excalibur process was operated. Builds and previews
remain in ignored artifacts; the running player host was not replaced.

Changed JSON parses and Git whitespace/conflict checks pass. Private Data, test
reports, generated images and binaries are excluded from source commits. Fresh
full automatic live runs and Band-specific live outcomes retain the limitations
in the [automation handoff](research/fisk-danaan-automation.md).

## Earlier worktree integration preflight — October 13, 2026

Before integration, the main worktree built in Release with **zero warnings and errors**.
The focused quest/recovery selection passed **79/79**, the complete app suite
passed **209/209**, and the complete Core/protocol suite passed **1,100/1,100**
with test collection parallelism disabled. There were no skipped tests in these
passing runs. The serial Core invocation used xUnit's documented
`-- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1`
[runner settings](https://xunit.net/docs/config-runsettings).

Default parallel Core runs exposed intermittent walking assertions and a journal
reader-lock deadline. The affected walking cases passed in isolation; their
assertions and timing bounds remain unchanged. A separate Windows socket teardown
race was corrected in the synthetic walking fixture: only a wrapped
`SocketError.OperationAborted` after deliberate cancellation is ignored. Other
transport faults still fail. Sandbox-denied current-user pipe tests passed with
local pipe access. These results do not establish that the intermittent parallel
failures were resolved. The later normal parallel merged run is recorded above.

New C# files follow the existing formatter settings. Added comments explain
resource receipts, write-ahead submissions, reconnect boundaries, paid waits,
doorway departure and reward/popup separation. Generated `TestResults` folders
are ignored. Changed JSON parses successfully, whitespace checks pass, and the
source/documentation/content scan found no private home paths or token-pattern
matches. These checks supplement the existing sanitized-evidence boundaries.

Manual isolated native previews were reviewed for Fisk's paid wait, Cap/Band
library filtering, unselected and accepted Cail consent, and normal/compact dark
dashboard layouts. No real character or Excalibur operation occurred. Fresh full
automatic live runs retain the limits in the [automation handoff](research/fisk-danaan-automation.md).

At this preflight checkpoint, other chats were still editing the sibling
planning/UI worktree. The preview identified overlapping availability, queue
dispatch and character state; later completed UI files added further overlaps.
The merged build, tests and manual review above supersede this earlier checkpoint.

## Fisk and Danaan automation — October 13, 2026

The approved [automation design](superpowers/specs/2026-10-13-fisk-danaan-automation-design.md)
adds Fisk secrecy and the shared observed Donnan/Cail Cap/Band journey. Isolated
complete xUnit runs passed **1,079 Core/protocol tests** and **190 app tests**,
with no failures or skips. Focused regressions cover actual Fisk pages/resources
and parsed XP packets, delayed final Next rewards, ambiguous transactions,
guarded equipment and token pickups, current route choices, paid wait/queue
deferral, waiting-row occupancy/down direction, interrupted guidance arrivals,
own completion detection and final popup cleanup/reconnect. Existing app recovery
calls and artwork alias coverage also pass.

Manual isolated native previews reviewed the unselected route/unchecked risk
state, accepted Cail confirmation and saved bouquet wait. Review found a generic
restored message during the wait; its regression failed, the message was fixed,
and the complete app suite passed again. Previews cannot open listeners, launch
clients or operate characters. Four unrelated saved queue files remain unchanged.
Sanitized research fixtures and documentation links pass the existing privacy
and structural checks. No packaging change, release, commit or Excalibur change
was made.

Live research completed Fisk secrecy and one Cail/Cap route on Fast through MCP
discovery. That evidence does **not** certify a new full automatic live run.
Band-specific pages, other Avatar routes, lower-level eligibility, reward bonus
variants, the exact bouquet threshold and paid-wait reconnect still need live
captures. Unknown menus stop safely. See the
[implementation handoff](research/fisk-danaan-automation.md) for supported routes,
saved state, component changes and exact remaining limits.

## Release 1.4.0 candidate — October 12, 2026

This release includes today's merged Altar Worship and melee combat work from
PRs #11 and #12, plus the queue play button, folding journey card, library
selection explanations, saved Stick reward recovery, sprite anchoring and
verified completion effects. The flower quests and helper behavior from 1.3.0
remain included.

The symbol-free Release solution build and clean isolated packaging build
completed with zero warnings or errors. All **832 Core/protocol tests** and
**119 app behavior tests** passed, with no failures or skips. The packaged app,
Core and DarkAges assemblies match the tested assemblies byte for byte.

`Build-Release.ps1` and release verification passed. All nine recipes are required
in the portable and update archives, including Altar Worship. Both distributions
include the setup README and current release notes, matching their source files.
Checksums, feed metadata, runtime and third-party notices agree. Walkability covers
1,417 of 1,432 recorded maps; 15 remain unavailable. Packages exclude user Data,
raw game assets, symbols and private checkout paths.

The pinned public 1.0.7 updater discovered and verified the local 1.4.0 package
with unchanged application identity and updater bytes. No installation occurred.

Manual native review covered the fold transition, quest selection states,
matching-character completion effects and an unselected character's notice in a
compact, collapsed planner. Completion GIFs use synthetic saved outcomes and the
real effect renderer. Four documentation screenshots were refreshed with isolated
sample data and no configured game folder. These reviews are not a pixel-test gate.
Builds and checks operated no real characters and made no Excalibur changes.

Live limits remain explicit. Altar Worship's first automated offering was verified
on Ferda; its complete ten-offering run and final rewards still need live validation.
The earlier melee run on Cerda verified a Druid's weapon swap, pursuit, skills,
cooldowns, kills and roaming; it does not establish live behavior for every class.
The captured repeat Stick reply has replay coverage, while missing reward evidence
still leaves a saved submission paused. A full live queue run and broader sign-in
recovery remain unverified. See [quest details](QUESTS.md) and
[source provenance](provenance.md) for the specific evidence and remaining limits.

## Release 1.3.0 candidate — October 12, 2026

The release includes merged flower quests from PR #10 (`e63aaeb`): Cured the Sick
Child, Aided the Pact of Anaman, helper picking/trading/logout, shared daily
flower records, overlapping login launches, fast-mode defaults, own-character
walking exclusions and action-message/trade replay recording.

An isolated, symbol-free Release solution build and the separate clean packaging
build both completed with zero warnings or errors. All **73 focused flower,
trade-recording, fast-walking and transport tests**, **783 complete Core/protocol
tests** and **69 complete app behavior tests** passed with no failures or skips.
The final Core/protocol run took 1 minute 57 seconds with normal Windows named-pipe
access. Isolated test output retained the directory depth needed by source-fixture
lookups and used installed test runtimes rather than the packaged app's local host.

`Build-Release.ps1` and a separate `Verify-Release.ps1` run passed. Both new recipes
are now required in the portable and update archives, alongside all six existing
recipes. Assets, hashes, update feeds, runtime and third-party notices agree.
Functional walkability covers 1,417 of 1,432 maps (15 unavailable); packages exclude
user Data, raw game files, symbols and private checkout paths. The packaged app,
Core and DarkAges assemblies match the tested assemblies byte for byte.
The pinned public 1.0.7 updater verified the local 1.3.0 package with unchanged
identity and updater bytes; no installation was performed.

Native manual review covered the planner and both flower-choice dialogs with
isolated synthetic characters, including disabled helper/confirmation controls
and scrollable helper content in the shared dark theme. Capture artifacts remain
outside tracked documentation. No test operated real game characters, changed
Excalibur working copies or restarted Excalibur.

## Release 1.2.0 candidate — October 11, 2026

The planner, original character previews and maintainability changes are integrated
with current `origin/main`, `bdd0bb0`, including PRs #8 and #9 and the server-message
logging change. All 17 overlapping files were reconciled; no conflict markers or
unmerged paths remain. Incoming Emerald story receipts, outcome evidence, editable
choices, replay messages, tolerant travel and corrected reflection arguments are
retained alongside the new layout and portrait bindings.

A fresh, symbol-free Release solution build completed with zero warnings/errors.
All **134 focused merge/sprite/migration tests**, **745 Core/protocol tests** and
**69 app behavior tests** passed. The final complete Core suite took 3 minutes
30 seconds, with no timing failures. The app suite includes an integration check
that keeps incoming choice controls available before submission and disables
them during a run or after submission.

`Build-Release.ps1` and a separate `Verify-Release.ps1` run passed for 1.2.0.
Installer, portable ZIP, full update package, feeds, checksums and licenses agree;
functional walkability covers 1,417 of 1,432 maps (15 unavailable). Packages exclude
user Data, raw game files, symbols and private checkout paths. The three packaged
application/Core/DarkAges DLLs match the tested assemblies byte for byte.
The pinned public 1.0.7 updater verified the local 1.2.0 package with unchanged
identity and updater bytes; no installation was performed.

Native manual reviews covered idle/running/paused compact action controls,
saved face-zero portraits, history portraits, the shorter journey/taller library,
and both ends of the incoming Emerald reward dialog. Four optional documentation
screenshots were regenerated from the packaged app with isolated synthetic data
and no configured game folder, so no game artwork enters documentation or packages.
This is manual review, not an automated render matrix or pixel-test gate.
No build or test operated real characters or restarted Excalibur.

Sprite preview matches observed appearance, direction and actions using installed
original assets. It does not synchronize the native client's exact animation
frame; saved appearance remains presentation-only. Historical live-evidence
limits below are retained; incoming Emerald live results are documented in
[quests](QUESTS.md).

## Release 1.1.1 candidate — October 11, 2026

Integrated the latest `origin/main` merge, `5d29a39`, with the maintainability,
supported-client agreement bypass and application icon changes. A fresh,
symbol-free Release solution build completed with zero warnings or errors.
All **16 focused integration/launcher/checkpoint tests**, **719 Core/protocol
tests** and **46 app behavior tests** passed. The complete Core/protocol suite
took 1 minute 38 seconds; the app suite took 19 seconds.

The installer, portable ZIP and full update package passed release verification:
checksums and feed metadata agree, required recipes and licenses are present,
functional walkability covers 1,417 of 1,432 maps, and user Data, raw game assets,
symbols and private checkout paths are excluded. The packaged app, Core and
DarkAges DLLs match the tested assemblies byte for byte.
The pinned public 1.0.7 updater discovered and verified the local 1.1.1 package
with unchanged application identity and updater bytes; no installation occurred.

Four optional native documentation previews completed with isolated synthetic
data. The planner and Settings previews were reviewed for the new icon, retained
dark theme and removed Developer panel. Automated render/pixel matrices are no
longer a release gate; nonvisual account, planning, ownership and shutdown
regressions remain. No test launched a real game client or operated a character.
Live native-client login and quest-evidence limits below still apply.

## Local DAOrganizer agreement bypass and redirect checks — October 11, 2026

At the player's request, the launcher applies DAOrganizer's two agreement-skip
branches before resuming a newly created supported client. The failed fixed-click
attempt is removed. No installed game file, existing client, Excalibur process
or user data is changed.

An isolated copy of the committed baseline plus these launcher/test changes was
used because a concurrent maintainability task was moving projects in the shared
working tree. Its Release solution builds with zero warnings/errors. All **15
focused launcher, login-reader, saved-login and owned-transport tests** pass,
including three early/fragmented lobby redirect cases with immediate lobby EOF
and delayed handler setup. All **46 presentation/lifecycle tests** and
`tools/Test-UI.ps1` (**206 checks, 80 renders**) pass.

The full skip-build Core/protocol run completed **708 tests**: 706 passed and
two existing walking cadence tests failed timing assertions. Both failed tests
passed when run alone. The three redirect cases added after that full run passed
in the focused run. Walking behavior and timing assertions are unchanged.

The reported terms-skip lobby/redirect packet drop was not reproduced. Synthetic
tests verify buffered packets, preserved transfer metadata, replacement identity
and separate generations. The exact skip branches retain normal stipulation
processing; handlers are configured before packet pumps start. No production
transport patch was made without a demonstrated failure. These checks do not
establish live native-client success. This is a local build, not a published
package.

## Superseded terms-click attempt — October 11, 2026

The initial saved-login change waited for the active Agreement pane, accepted once, confirmed
closure and only then opens and fills login. Synthetic coverage includes an
early menu, delayed terms, delayed acceptance, missing terms, acceptance timeout,
cancellation, unstable pane/menu snapshots and terms over an underlying login.

The focused login run passed 15 tests; the subsequent full build also includes
the additional underlying-dialog regression. Release solution build passed with
zero warnings/errors. The final full Core/protocol run passed **714 tests** in
4 minutes 32 seconds; **46 presentation/lifecycle tests** also passed, for
**760 total**. `tools/Test-UI.ps1` passed **206 behavioral checks** and **80
renders** with isolated synthetic data.

The first sandboxed full run hit Windows current-user named-pipe access denial
and two previously documented walking timing failures while validation helpers
overlapped. The isolated pipe test and final full suite passed outside that
sandbox. Walking code and timing assertions are unchanged.

Player feedback and read-only launch diagnostics subsequently established that
the fixed accept coordinate missed the actual OK button. This attempt is removed
in favor of the player's requested DAOrganizer agreement bypass. Its synthetic
test results did not establish live acceptance. No real character was operated
by tests; Excalibur files/processes and user data were unchanged.

## Release 1.1.0 — October 11, 2026

The Avalonia 11.3.12 Release solution builds with zero warnings or errors. All
**46 presentation and lifecycle regressions** pass. `tools/Test-UI.ps1` passes
**206 behavioral checks** and produces **80 actual Skia renders** of the
application. Native `tools/Capture-Screenshots.ps1` passes **201 checks** and
captures **76 renders**, including the four updated documentation screenshots.

The full Core and protocol suite passes **707 tests**, with zero failures or
skips (4 minutes 24 seconds). Together with the presentation suite, **753 tests**
pass. The final test-project build also has zero warnings or errors.

All **36 focused fast-walking tests** and **28 focused launcher, transport,
account and update regressions** pass. Timing-sensitive checks run alone,
without builds, packaging or rendering competing for resources.
Earlier full runs exposed scheduling races in the navigation inventory and
player-arrival fixtures, plus stopwatch-based walking failures. The inventory
fixture and all nine shortest-travel tests pass alone; both classes now share
the existing nonparallel walking collection. The final full run passes with all
validation helpers idle. Production movement, assertions and timing thresholds
are unchanged.

The inspected matrix includes all four pages at 1450 × 930, 1024 × 768,
900 × 600 and the 900 × 560 minimum, plus 125%, 150% and 200% render scaling.
It covers empty and populated workspaces, disconnected, running, paused,
completed, blocked, loading and error states; keyboard focus, hover, press and
drag insertion; saved and locked choices; dropdown menus; disabled Launch actions
with connection explanations; and the dark client picker. Native validation also
confirms the preview window's immersive dark
Windows title bar. Birth's full warning and stop condition remain visible at
its default size, and compact queue actions and account-save controls remain
reachable.

Native CI caught an oversized window request whose 1450 × 930 content remained
larger than Windows' accepted 1028 × 749 client area. The shell now follows the
accepted viewport without changing future window-size requests. A focused
regression covers initial sizing, a rejected oversized request and later supported
growth, including valid layout and visible header/stop actions. Native captures
wait for stable layout, record accepted dimensions and preserve failure images;
the strict clipping assertions remain in place.

Behavioral coverage exercises per-character queue ownership and persistence,
keyboard and pointer reordering, pause/stop checkpoints, failed and pending
saves, own observed levels and legends, repeat and cooldown policies, pending
final-dialog reconciliation, and the saved one-deoch Faerie wait without a pet
claim. Account checks use isolated encrypted credentials, blank-password
preservation, renaming, duplicate rejection, twenty-account batches and removal
back to a genuinely empty workspace. Empty and per-character Launch actions
reach the guarded launcher. An unstarted production runtime disables saved-account
Launch actions and exposes the connection reason through card text, disabled
tooltips and accessibility help. Updates remain opt-in; checking, downloading and
installing stay separate, and loading/error states remain usable.

The shipped self-contained payload also passes all **206 UI checks**. Release
packaging uses a separate output folder and rebuilds every project without
symbols; its compilation finishes with zero warnings or errors. Installer,
portable and full update assets pass checksum, feed, content, dependency,
license and private-data verification. The package uses only the .NET core
runtime and contains no Forms/WPF implementation libraries or PDB files. An
independent binary audit confirms that all three project DLLs contain no
CodeView record or embedded checkout path, and all 24 UI dependency entries
retain usable license metadata and notice arrays.

Portable walkability covers **1,417 of 1,432 recorded maps**, with **3,861,594**
source-audited tile flags and 15 explicitly unavailable maps. All **30 focused
collision and navigation regressions** pass. Using the actual bundled grids and
an empty client folder, the normal map repository plans from Mileth (500) at
(33,37) to Suomi Tavern (413) at (3,13) in **430 modeled actions**, through
500 → 3006 → 3210 → 3081 → 502 → 3008 → 504 → 3016 → 503 → 412 → 413.
This is an offline route calculation, without movement or a game session.
The shipped UI checks also exercise the real bundled-grid fallback with an empty
client cache and confirm the map-97 entrance exception. Both release archives
validate functional-grid coverage, dimensions, bit encoding and source digests,
and reject raw map or collision archive files.

The updater DLL shipped in the public **1.0.7** package is byte-identical to
the current Velopack DLL. An isolated local-feed probe using that updater
discovers **1.1.0**, keeps the existing application identity, and downloads and
verifies the candidate full package into a unique synthetic cache. It never
calls an install or apply operation. Native and headless render manifests
retain separate image copies, so subsequent validation cannot relabel earlier
captures by overwriting shared filenames.

The compatibility fixture is the published 1.0.7 full package, kept separately
from local release builds. Both downloaded and cached fixtures must match its
pinned public SHA-256 digest. The check rejects a locally rebuilt package before
starting the updater probe.

These checks use synthetic sessions and unique files under ignored `artifacts`.
They never connect to a game, start a client, apply an update, access real user
Data or operate Excalibur. Live gameplay remains outside this validation.

## Release 1.0.7 — October 11, 2026

The full Release suite passes **689 tests**, with zero failures or skips
(4 minutes 8 seconds). The focused quest, queue, travel, memory and recovery
run passes **315 checks**. All 36 fast-walking tests pass in the full suite,
including continuing past off-path monster updates and avoiding a monster that
blocks the next step. Existing player-proximity and doorway checks remain intact.

New completion regressions cover Callough and Eeva popup cleanup before the next
queue entry, a terminal reward discovered during timeout reconciliation, failed
close recovery without another submission, weekly Hairstyle outcomes, unfamiliar
page preservation, Keane's two-menu escort path, and level-11 Faerie access.
Captured replay tests explicitly allow the final close while rejecting a repeated
turn-in. No reward, completion date or cooldown deadline is inferred from Eeva's
already-helped response.

The Release solution build passes with zero warnings or errors in a separate
output folder. The running checkout and all game processes remain untouched.
Native UI checks pass, including live and saved offline level-10 Faerie gates,
queue ownership, dark rendering, scaled controls and retained quest policies.
Installer, portable ZIP and full update packages pass asset, checksum, feed,
license and private-data verification. Packaging uses its own build folder and
does not replace the running checkout.

Both GitHub workflows pass for release commit `44f70d6`. All seven public 1.0.7
assets pass release verification and match GitHub's recorded SHA-256 digests.
The actual public **1.0.0** and **1.0.6** apps report **1.0.7 available**; the
public **1.0.7** app reports no newer version. All report no pending install.
These read-only checks do not apply an application update or operate characters.

This patch still needs live confirmation; build and regression runs operate
synthetic sessions only.

## Release 1.0.6 — October 11, 2026

The full Release suite passes **670 tests**, with zero failures or skips
(4 minutes 12 seconds). All 34 focused fast-walking regressions also pass.
An earlier full run overlapped packaging and native rendering and failed three
wall-clock movement assertions; the focused rerun and subsequent full run passed
without changing movement code or weakening assertions. Keep timing checks
separate from packaging and rendering when running locally.

Native UI checks pass for the planner, character memory, queue ordering, history,
and character status wrapping after theming. Four documentation screenshots
render the actual application at 1450 × 930 with isolated sample characters.
The screenshot mode opens no proxy/control listeners, game clients, user data,
or credential vaults. It checks actual level-99 and existing Muisir legend gates.

Release packaging and verification check installer/portable assets, full update
packages, feed versions and hashes, all six recipes, upstream/runtime licenses,
and private-data exclusion. Repository checks find no private paths, game
binaries, user Data, or build output in the publish set. Local Markdown links
resolve and content JSON parses. Historic design notes are grouped under
`docs/design`; superseded UI/dialog code and completed task plans are removed.

Both GitHub workflows pass for release commit `7fd48cf`. All seven published
assets pass release verification and match GitHub's recorded SHA-256 digests.
The actual public **1.0.0** and **1.0.5** portable apps report **1.0.6 available**;
the public **1.0.6** app reports no newer version. All three report no pending
installation. These read-only checks never download or apply an application
update, open user Data, or operate a game character.

Full live validation remains pending for Emerald Dawn, successful Faerie rewards,
the queue, automatic saved login, own-legend refresh, and queue completion. The existing
quest policies, observed-evidence boundaries, and reconciliation rules remain
covered by synthetic regressions.

## Remembered character facts and planner states — October 11, 2026

The normal Release solution build passes with zero warnings and errors. The
full suite passes **670 tests**, with no failures or skips (4 minutes 31 seconds).
The focused memory, Muisir, level, legend, queue and reward checks pass 129 tests.
The full run used the required current-user named-pipe context outside the
restricted sandbox. All existing quest execution policies remain covered.

Regressions reproduced known offline level 99 and pending submissions being
selectable, Muisir's own mark being presented as ready, and ready rows lacking a
green tint. Coverage now verifies persisted levels and complete own legend lists,
character/server separation, disconnect and unknown-state preservation, stale
generation rejection, fresh empty-list replacement, cancellation, and safe
character filenames. Known Birth, Muisir, Emerald final marks and the Hairstyle
goal prevent another selection; repeatable Stick marks and partial Emerald
recovery remain unfinished. Planning memory contains no runtime transaction
evidence and cannot fabricate a reward receipt.

`tools/Test-UI.ps1` passes against the normal Release executable. Added native
checks verify saved level 99 gating without a session, disabled checkbox/Space
selection, Completed filtering and journey status, choices after a fresh own
observation, and Resume for an existing pending queue. Render checks cover mint
Ready/Choices surfaces, muted Blocked/Unavailable titles, and a single custom
search hint with the native cue suppressed across focus and clearing. The
ready-state, saved-completion, state-sheet and search renders were inspected.
The expanded smoke run has a four-minute timeout; the earlier two-minute watcher
expired while a successful run continued rendering its remaining checks.

No real character, game command or Excalibur process was used in validation.
Own legend refresh after real login remains live validation work. Documentation
and the approved design follow-ups describe the saved-fact boundaries. Packaging
configuration is unchanged.

## Quest planner and saved-character launch — October 10, 2026

The Release solution build passes with zero warnings and errors. The full suite
passes **640 tests**, with no failures or skipped tests (3 minutes 58 seconds).
Focused queue, runner, and login regressions pass 196 tests; the final queue,
saved-ticket, native-reader, owned-login transport, and control checks pass 49.
The current-user pipe check requires execution outside the restricted sandbox;
the successful full-suite run used that context.

`tools/Test-UI.ps1` passes with isolated synthetic data. Its planner checks cover
real catalog search, persisted membership, button/keyboard/drag ordering, retained
keyboard focus, cross-character drag rejection, character isolation, offline
start gating, durable history, next entry, recent activity, and per-character
Launch actions. Runtime ownership regressions cover manual input and
disconnect between entries, Stop despite a failed checkpoint write, and Stop
overlapping a pending queue-order save. The native finish checks include expanding
scroll content, wrapped history receipts at 1024px, dark surfaces, action labels,
and 100/125/150/200% scaled controls. Actual planner, queued, Characters, history,
activity, dialog, and minimum-size renders were inspected under `artifacts/`.

Saved login has synthetic form and encrypted loopback transport coverage; real
login and live queue navigation remain untested in this UI change. No real
character was operated and no Excalibur process or working copy was changed.
Packaging configuration is unchanged. User instructions, architecture, source
provenance, and DAOrganizer's upstream MIT notice match the implementation.

The Faerie pet-wait follow-up (2026-10-10) passes 76 focused Faerie checks and
all 577 native Windows tests (4 minutes 1 second). Regressions first reproduced
the observed no-bond reply blocking return, the real spell-menu wording being
unrecognized, Rucesion Threshold being rejected as a safe landing, and saved
terminal refusals failing to reconcile after manual closing/return. They now
verify the one-deoch wait without pet rewards, immediate Rucesion Song return,
the non-Priest/Wizard refusal, and Resume without duplicate adoption or songs,
including reconnect. Mismatched replies and another/disconnected character
cannot clear the saved submission. Numeric spellbook level suffixes work for
already-known spells, newly learned spells and current dachaidh slots; an old
ranked baseline never proves a new reward.

The separate Release build has zero warnings/errors. `tools/Test-UI.ps1` passes
against it, including an isolated failed-bond journal with the one-deoch wait
visible offline and no pet-success claim. Its dark journal render was visually
inspected. The original policy, warning, keyboard and layout checks still pass.
Core and DarkAges assemblies match the test binaries by SHA-256. No builds or UI
checks ran alongside the final full suite. No real character was operated,
no running application was stopped, and Excalibur was untouched.
After DAQuester was no longer running, its usual launch folder was rebuilt with
zero warnings/errors. The executable and App/Core/DarkAges assemblies match the
UI-tested separate build by SHA-256. Restart and live Resume are player actions.

The sanitized fixture captures the player's actual failure and non-caster
dialogues; the manual Rucesion Song landing supplies return evidence. Successful
pet adoption, Priest/Wizard learning and a complete automated live visit remain
unverified. Packaging is unchanged.

The Faerie reactor greeting fix (2026-10-10) passes 122 focused quest, travel,
fast-walking and collection checks, plus all 559 native Windows cases in the
final isolated full run (4 minutes 7 seconds). Runner and transport regressions
first reproduced the failed visible-NPC gate. They now verify greeting from
(73,38) without a drawn NPC, a blank-name reactor introduction with Next, binding
the reply's real entity ID, successful synthetic adoption/return, and refusal to
greet away from the verified approach. Existing popup and reconciliation guards
remain covered. An initial full run overlapped build/UI work and hit three
stopwatch-based walking failures; those unchanged tests pass alone and in the
final full run. No cadence thresholds or production walking safeguards changed.

Both normal and isolated Release builds have zero warnings/errors. Their App,
Core and DarkAges assemblies match by SHA-256. `tools/Test-UI.ps1` passes against
the isolated build, including all six quests, dark preparation warnings and the
existing keyboard/layout/policy checks. No running app or game was stopped by
the agent; the normal launch folder was rebuilt after DAQuester was closed.

Read-only replay inspection confirms the original pause five seconds after
arrival, no drawn Faerie, and the player's manual greeting producing a type-4
reactor introduction. Full live acquisition/return still needs verification.
Walking replies arrive in roughly half-second to one-second bursts without
rejected steps or nearby players. This is consistent with the bounded fast
queue waiting for acknowledgments; send timestamps were not recorded, so the
capture does not isolate network delay from server processing or local scheduling.

Shinewood Faerie (2026-10-10) passes 117 focused quest, state, transport, catalog,
Emerald Dawn and Suomi travel checks and all 556 native Windows automated cases
(4 minutes 3 seconds). The sandbox denied the existing current-user pipe test;
the isolated native pipe test and complete native suite pass.
Coverage includes all six classes, cumulative spell thresholds,
unknown pure status resolved from actual offers, carried/equipped pets, current
item/spell slots, mana, dangerous walking acknowledgment, real landing checks,
stale/wrong replies, manual interruption and reconnect without duplicate reward
or teleport submissions. Controlled reply ordering rejects the previous menu as
a new selection reply, without short timer assumptions. Actual pet/spellbook
rewards can also complete when the server closes the dialog. Travel dispatch
preserves newly opened popups before Faerie visits and the final return walk.

The Release build has zero warnings/errors. `tools/Test-UI.ps1` passes with six
selectable quests, the Faerie library entry, dark danger warning, default/focused
Cancel, and disabled walking confirmation when its route is unavailable. Journal,
library and both warning renders were visually inspected under ignored artifacts/.
The existing keyboard, compact-layout and four original quest-policy gates pass.

A read-only offline probe using installed collision and the production planner
validates all eight documented walking-return exits and the entrance-to-Mileth
route. Outbound travel to the Faerie uses eight transitions and 751 modeled
actions. Missing reverse landings remain unknown; runtime uses actual server
positions after each exit. No connection or character is operated by the probe.

Full live Faerie menu/reward/return validation remains pending. Automated quest
dialogs and transport peers are explicitly synthetic. Existing uncommitted work,
user data, running game characters and Excalibur working copies/processes are
preserved. Packaging was unchanged.

The entrance tile-sweep follow-up (2026-10-10) passes 65 focused Emerald Dawn,
progress and fast-collection cases and all 501 native Windows automated cases
(4 minutes 17 seconds). The new regression rejects the old proximity patrol
and verifies 519 distinct swept tiles, no door tiles, straight legs, a closed
lap, inclusion of the observed manual acquisition position and exact agreement
between the recipe and calibration. A read-only offline check using installed
collision and the production pathfinder confirms all 519 reachable non-door
tiles are walked in 620 steps through 110 turn/end waypoints, with none missed.
The old repeated loop covered only 114 tiles and missed the manual discovery
position (10,11). This proves a coverage gap, not the server's discovery rule
or an animation-related cause.

The separate `artifacts/emerald-dawn-entrance-sweep/DAQuester.exe` Release build
has zero warnings/errors and contains the verified sweep. `tools/Test-UI.ps1`
passes against it with isolated sample data; `git diff --check` passes. Existing
fast movement, popup handling and durable quest behavior remain covered. Live
acquisition with the new sweep remains unverified. No real game characters were
operated by tests; neither the running application nor Excalibur was restarted.

The discovery-popup and Suomi shortcut follow-up (2026-10-10) passes 90 focused
quest, collection, routing and transport cases and all 500 automated cases in
the native Windows full suite (about four minutes). New regressions first
reproduced search popups being closed or ignored, walking out of Suomi rather
than interacting with Keane, and inventory arriving before its late discovery
page. The observed discovery fixture verifies item-before-popup wire order,
the actual unnamed reactor Next page and its server close. Synthetic cases
cover unknown popup preservation, saved Next requests, late item confirmation,
reconnect without resending, both Keane greetings, reordered pursuit/merchant
choices, actual-arrival replanning, unavailable NPC fallback and missing travel
reply reconciliation.

The separate `artifacts/emerald-dawn-popup-keane-fix/DAQuester.exe` Release build
has zero warnings/errors. `tools/Test-UI.ps1` passes against that build with
isolated sample data, including all five quest policies, dark surfaces, Emerald
Dawn outcome choices and fast-mode settings. `git diff --check` passes. The
read-only offline route probe confirms Berg-to-Shinewood travel passes Abel;
no Keane landing is invented. Search remains exclusively in Entrance 542.
Full live Emerald Dawn and Keane shortcut validation remain pending. No real
game characters were operated by tests, and neither the live application nor
Excalibur was changed or restarted.

The Shinewood search follow-up (2026-10-10) passes 20 focused movement/progress
checks and all 482 automated cases in the native Windows run (3 minutes
54 seconds). New regressions first reproduced collection waiting for each
movement reply and a runner retaining the previous dialogue reason during a
long action. Collection now uses the existing fast movement queue for patrol
and pickup approaches, replans when matching loot appears, and drains pending
moves before pickup. Durable action checkpoints update the runner and its UI
notification immediately after the journal write succeeds.

The separate Release build has zero warnings/errors; tools/Test-UI.ps1 passes
against it using isolated sample data. Read-only live state showed both
introduction checkpoints complete, no pending transaction and no open dialogue
while gathering in Shinewood. No game commands were sent. Live insignia
acquisition was unverified at that check; the later manual discovery capture is
documented in provenance. Ground-drop evidence and final outcomes remain
unverified; the running application was not replaced or restarted.

Emerald Dawn (2026-10-10) passes 73 final focused quest/MCP/status cases and all
478 automated cases in the native Windows run (3 minutes 47 seconds). The Release
build has zero warnings/errors. The full suite includes the original four quest
policies, navigation, recovery, transport and update checks. A restricted sandbox
could not connect the existing current-user test pipe; its isolated native check
and the complete native suite pass. The delayed-notice fixture uses deterministic
event ordering rather than a short wall-clock delay.

The native tools/Test-UI.ps1 gate passes with five selectable/available quests,
all three explicit Emerald Dawn choices, optional Gans visits, restored choices,
dark surfaces, unclipped buttons and the existing compact/keyboard checks.
Emerald Dawn journal, library and outcome renders were visually inspected under
ignored artifacts/. Full live Emerald Dawn validation remains pending. Conversations,
rewards and status packets in the new tests are synthetic; sprite 70 is guide
metadata and only server inventory proves pickup. No real game character or
Excalibur file/process was operated.

The 1.0.5 maintenance candidate passes 134 focused completion, ownership,
recovery and movement cases and all 425 automated cases (native full suite:
4 minutes 11 seconds). The Release build has zero warnings/errors. New tests
first reproduce completion lost after interruption, missing single-run cooldowns,
offline completion visibility, calibrated branch-name checks, town notices
blocking fast-walk recovery, and old-map walking after notice dismissal.
Synthetic Undine warnings verify close-before-unequip, delayed server confirmation,
full-inventory pause, explicit Resume after the warning closes, and preservation
of unrelated popups. Tests do not operate real characters or Excalibur.
The native UI gate passes independently, including offline completion and the
visible next deadline at compact sizes. A concurrent run completed its checks
just after the gate's 120-second limit; the isolated rerun passes without code or
timeout changes. Existing walking timing tests also pass in the final focused
and complete runs without changing their thresholds.

The 1.0.4 runner changes pass 162 focused quest cases and all 392 automated cases
(native full suite: 3 minutes 28 seconds). A sandbox-only current-user pipe access
failure was resolved by running the same isolated suite with native permissions.
The Release build has zero warnings/errors. Native UI verification covers main
page navigation, retained filters, Run again/cooldown/live ownership controls,
20-account bulk edits, duplicates, empty lists and immediate journal updates.
Isolated password batches verify Windows encryption, blank-field preservation,
rename and name swaps. The swap regression first reproduced a password overwrite;
all original passwords are now read before writing renamed targets. A 1024 px
keyboard-focus regression reproduced hidden quest actions; scrollable panels now
reveal focused controls. No real character actions or personal credential writes
are used for verification.

The 1.0.3 candidate adds a 1024 px native window check after release CI found
the equal-width quest columns truncating "Start quest" at 95 px. The added
check reproduced the failure locally at 94 px before the fix. Columns now share
space by measured label, padding and margin requirements, recalculated on
container, font and DPI changes. The full native UI gate passes, including this
new case; its small-screen render is visually inspected. Publication checks
remain enabled. All 385 automated cases pass again (3 minutes 35 seconds),
with zero Release warnings/errors and verified 1.0.3 packages.

The October 10 native UI refinement passes 159 focused cases and all 385 cases
(full suite: 3 minutes 28 seconds), with a Release build at zero warnings/errors.
The extended `tools/Test-UI.ps1` gate passes. Tests first reproduced opaque
buttons retaining corner pixels, button/checkbox text writing beyond the paint
clip, and square card backgrounds under rounded borders. The shared painters
now repaint complete surfaces and preserve clipping.

Native checks cover hover/press/disabled states, keyboard press and cancellation,
visible focus, Enter/Escape/mnemonics, property invalidation, tiny sizes,
transparent parents, repeated repaints, action label fit and native bitmap printing.
The label-fit check caught shortened file-picker actions and a long calibration
action; picker columns and measured toolbar widths now fit those labels. State sheets
exercise 100/125/150/200% geometry/font scales and visible labels; these are
synthetic scales, not physical mixed-DPI monitor validation. Standard/compact
workspaces and dialog states are rendered and visually inspected. The shared
design contract is [DESIGN_SYSTEM.md](DESIGN_SYSTEM.md). No game characters,
saved user data or Excalibur processes are operated by these checks.

All 327 automated cases pass on the public release candidate, including 19 update
consent, safe-exit and data migration cases, as well as the
three-minute drought regression, full Vows introduction/claim replay, and observed
two-ring hairstyle reply. The packaged native UI smoke test also passes with the
isolated sample settings and explicit update download/install states. Installer,
portable and update feed verification passes. Four-project .NET 10 solution; self-contained
win-x86 app. UI screenshots verify fully dark surfaces and unclipped dialog buttons.

The initial fast-mode working build passed all 345 automated cases, including 18 new
synthetic cadence/player tests. The current-user named-pipe case requires running
the suite outside the restricted sandbox. Timing tests run separately from other
test collections to avoid load-induced timing failures. Native Test-UI passes
default-off, per-character persistence, case-insensitive names, reopening and
character switching, plus standard/compact dark renders. The Release build has
zero warnings and errors. That initial version retained acknowledgment gating,
which limited walking and delayed animation; the October 10 follow-up below
replaces this behavior for safe fast travel. No live fast-mode
run is claimed; no real game characters or Excalibur processes were operated.

The initial combined fast-mode and shortest-travel build passed all 367 cases
(full suite: 3 minutes 8 seconds), with 67 focused movement/navigation cases.
The 22 additional travel regressions cover alternate doors, distinct arrivals,
final target distance, collision barriers, directed shortcut chains, live world
selection, occupancy changes, correction recovery, reload position barriers and
NPC interaction range. Release builds with zero warnings/errors; Test-UI passes.
Seven installed-collision route samples were calculated offline. Among five
samples with a valid direct-walking baseline, four became shorter and one tied:
Mileth (33,37) to Suomi Inn fell from 421 to 320 modeled actions; the nearby
Mileth Inn landing fell from 27 steps to 1. Repeat Suomi planning took about
10 ms after caching on this machine. These are synthetic/offline checks,
without live character movement; future server latency and unseen occupancy
are not measured. Local audit output is ignored under `artifacts/travel-audit/`.

The October 10 fast-travel correction passes all **385** cases (full suite:
3 minutes 27 seconds) and **80** focused movement/navigation cases. Eighteen
additional cases verify immediate presentation, 160 ms sends with 500 ms replies,
bounded projection without invented snapshots, duplicate suppression and client
ordinals, player arrival with pending steps, inventory stop draining, doors and
same-map transfers, late replies, timeouts, manual input, Stop/resume, bounded
patrol recovery, and incomplete replies that cannot confirm movement. Timing
fixtures run in the nonparallel walking collection. The full suite uses normal
test output paths because existing evidence fixtures resolve content from there.
The final Release build has zero warnings/errors and Test-UI passes, including
the pace explanation and standard/compact dark renders. The executable is under
ignored `artifacts/fast-mode-fix/`. A passive state query found three players
within the slowdown radius during the reported quest; normal pace there is
intentional. No real character commands or live walking tests were performed,
and Excalibur working copies and processes were untouched.

Recent Vows hairstyle intervention was caused by a Windows journal replacement
lock before final submission, followed by an unrecognized two-ring response after
manual submission. Real sharing-lock tests cover successful retry, cancellation
and bounded failure without corrupting the previous journal. The first durable
claim now includes NPC ID and own-legend baseline. The actual Vows reply proves
25,000 XP and one new Emerald/Jade Ring. Its verified historical receipt is saved
for fresh-state reconciliation without resubmission. A live current own-legend
check is still required; no Suomi mark is assumed from rings.

Nuclear's 21:00:48 movement capture shows delayed movement followed by same-map
refreshes. Tests reproduce the erroneous correction count when map-size clears
position and the premature step before refreshed coordinates. Recovery now waits
for authoritative position; delayed progress resets the no-progress budget.
Cancellation stops that wait and prevents further movement. Existing player
detours and temporary patrol-target skips pass. Threshold tests prove directed
same-map transfer handling with solid wall rows, for both 3079 and 3080. The
Mileth transfer has actual live evidence; Woodlands' equivalent uses Excalibur
source and matching collision layout and awaits validation on the new build.

The branch route preserves 90 Mileth waypoints and adds 69 entrance waypoints in
East Woodland Crossroads (600), as requested. A simulated session verifies that
dry Mileth areas lead to Woodlands and six observed branches stop the sweep. This
extension is authored coverage; it is not yet a captured Woodlands branch drop.

## Entered Muisir Village

Clock's actual status refusal required registration; level remains **1+** per
the player. Aware (registered, level 99) supplied the nine-page Kiril introduction,
actual ground-item sprite 569 and the claim pages. On October 9 at 20:23:40 CDT,
his own legend showed **Entered Muisir Village - Deoch 223, Spring**, all five
Undine Mushrooms were consumed and four Shrinking Potions were newly carried.
Ground collection, travel and Tiny Cave speech were automatic. Calibration
advanced one claim Next and closed the terminal page through control; the runner
then reconciled completion without repeating speech. This was an assisted
calibration run, not a full automatic claim.

`entered-muisir-village.json` retains actual safe packet events, those two control
dialog actions, a separately labeled own-legend snapshot and verified journal.
`muisir-introduction.json` retains the earlier introduction. The final handler
replays the captured claim pages automatically and inspects the own legend.
An introduction-only outcome authorizes a second speech only after fresh legend
and unchanged mushroom/potion counts prove no claim occurred. Interrupted claims
remain pending; ambiguous speech is never repeated automatically.

New tests cover level 1/41/99, unknown state and unregistered legends, existing
marks, zero/partial/five carried mushrooms, dry patrols, cancellation, pickup wire
layout, matching sprites, items appearing during walking, player detours, and
removal without inventory credit. Simulations cover fresh introductions,
bounded repeated introductions, interrupted-introduction reconciliation,
existing-potion false positives, fresh own-legend proof, reconnect/no-resend and
two independent characters. Actual Aware packet and dialog fixtures replay.

The recipe first checks mushrooms while approaching Kiril, then covers Undine
if needed. It stops pickup at five, does not use potions and has no repeats.
Vows (registered, level 99) then completed the entire final build automatically,
from Undine Inn, starting with zero mushrooms. Five pickups occurred while
approaching Kiril. Nine introduction pages closed without rewards; fresh own
legend and unchanged inventory authorized the second Tiny Cave speech. Both
claim pages, final Close and own-legend inspection were automatic. Completion
at **2026-10-09 20:41:40 CDT** saved the mark, five consumed mushrooms and four
new potions with no pending transaction or repeat schedule. Only start/state/
export control requests were used during that run. `entered-muisir-village-vows.json`
contains 152 safe server events, 11 actual dialog actions and separately labeled
initial/final own-state observations. Registered level-one live eligibility
remains unobserved; the player-corrected level gate is simulated.

## Live results — DATester 7.41, 2026-10-09

| Character | Turn-in evidence | Actual rewards | Durable outcome |
|---|---|---|---|
| Nuclear | Six branches consumed; Callough pursuit 177, step 82; XP 301 ? 3,801 | 3,500 XP, Stick, Wooden Shield | One completion; repeats off |
| Clock | Six of seven branches consumed; Callough pursuit 177, step 103; XP 351 ? 3,851 | 3,500 XP, Oak Stick, Wooden Shield | Permanent completion; repeats disabled |
| Awesome | Six branches consumed; Callough pursuit 177, step 92; XP 9,101 to 12,601; gold 1 to 51 | 3,500 XP, 50 gold, Stick, Wooden Shield | One completion; repeats off |

Nuclear completed at 12:53 CDT; Clock's terminal reply arrived at 13:23 CDT.
Awesome completed at 18:21 CDT. His coin reply and rewards are frozen in
`content/evidence/datester-7.41/awesome-completion.json`; existing equipment is
excluded from reward counts by comparing the turn-in inventory baseline.
Actual packet/action fixtures: `content/evidence/datester-7.41/spare-a-stick.json`.
Pre-identity inventory packets are attributed using later identity in the same
unique generation; original nullable snapshot identity remains intact.

Player confirmed visible walking in Nuclear's client. Live testing found and
fixed wall-flagged doors, counter approach, doorway pushback, pursuit closing,
and client movement presentation. Both runs included manual pauses/reconnects.
Reaccepting after Nuclear's reconnect restored drops. Clock reconnected after
gathering and resumed turn-in. Older runner paused because its fixture did not
recognize Clock's child-reminder reply; permanent-outcome policy now handles it.

Patrol now rotates the armor-shop park (18,56), central (54,43), and east (64,44).
Armor Shop's recorded entrance is (18,62). The previous (40,40) western
substitution omitted the guide's shop-side tree area; its replacement uses a
radius-four collision-filtered sweep. Current route has 90 waypoints.
Central/east drops observed; exact armor-area drop not yet captured. All three
remain in route. Clock collected a
seventh branch on a long old-build segment. Final movement checks inventory
before each send, including after cadence waits; local proxy test stops at six.

Older reward recorder used Nuclear's early 2,000-XP packet before remaining XP
and items. Final recorder uses terminal snapshot, covered by split-reward tests
and actual packet replay. Live journals are reconciled from action baselines and
terminal snapshots without resending turn-in.

Awesome's manual Cassidy deposit exposed the actual menu label, `Deposit Money`,
with menu pursuit 66 and a separate text-input prompt pursuit 82. The recorded
gold change was 1,000 to 1: the player deposited 999 manually. Sanitized fixture:
`content/evidence/datester-7.41/cassidy-deposit.json`. Banking now prepares the
live prompt before saving a pending transaction, submits through that prompt's
current entity/pursuit, and calculates only the excess from fresh carried gold.
The local proxy replay verifies the automatic 1,000-gold case submits `1`,
leaving the intended 999. Missing-menu failures create no pending transaction;
an unacknowledged amount submission still cannot be resent automatically.

Awesome spoke to Callough at 17:45 CDT, selected "I'll gather them now," and
received the captured acceptance reply before collecting a branch at 17:46 CDT.
That branch appeared at (45,40) while traveling. Fresh-attempt tests with one
and six carried branches prove acceptance still occurs before gathering.

After the route correction, Awesome reaccepted at 18:19 CDT with five carried
branches. The capture proves 45 acknowledged walking updates inside radius four
of the armor-shop park before the sixth branch arrived at (42,45). Turn-in then
returned "Thank ya so much. Here's a few coins for your effort." The old profile
did not recognize this reply; the updated profile and reward recorder confirm it
from history without sending another turn-in. Seven additional cases cover this
exact reply, packet replay, gold deltas, armor sweep and pending reconciliation.

Awesome's old build paused on both the three-minute branch deadline and
"No walkable route to destination." The deadline is removed from normal runs
and calibration; both pass an actual elapsed drought exceeding three minutes,
then continue through branch increments until six. Player appearance (0x33),
movement and removal now update occupancy. Navigation rechecks occupancy after
cadence waits, replans around new blockers, and skips temporarily inaccessible
patrol targets while retaining static-route failures. Successful walking resets
the consecutive recovery budget. Recovery uses F5 refresh (0x38), replacing
Look (0x09), which could trigger sign messages. Local proxy tests cover both
rejected and unacknowledged movement; their recovery never sends Look.

## Automated coverage

Mundane Birth adds actual Awesome/Riona packet fixtures for both the initial and
existing-birth dialog branches. The old recorder omitted status opcode 0x0A;
completion-status packet tests are synthetic and separate from live dialog evidence.
Nuclear and Clock subsequently completed automation live at 18:55 and 18:59 CDT;
their exact server notices, selected birth settings and completion times are
preserved as durable journal observations in `mundane-birth-live.json`.
Every age 12 through 40, all season/town pairs at both age boundaries,
missing/invalid choices, changed NPC IDs, reordered menus, text-input wire
serialization, cancellation, pending reconnect/no-resend, and independent quest
journals are covered. Birth does not inherit stick eligibility or Callough's
permanent completion policy. The native smoke checks both quest choices and the
mandatory birth form, including its exact returned settings.

- Levels 1/11 accepted, 12 rejected; unknown stats/inventory wait; death/cooldown gates.
- Gold threshold/deposit amounts; actual Cassidy menu and amount-prompt replay;
  fresh balance baselines; missing-menu failures; ambiguous transactions without duplicate sends.
- Capacity for six unstacked branches; partial/already-sufficient inventory.
- Fragmentation/coalescing, corrupt-prefix recovery, crypto vectors and ordinals.
- Live merchant/pursuit IDs, reordered menus, changing NPC IDs, stale dialogs, Next/Close.
- Collision/portal goals, missing maps, NPC counter approach, position correction.
- Proxy pass-through, visible automated walking, manual walking without duplication.
- Generation isolation, reconnect activation, cancellation, checkpoints, two characters.
- Correlated completion, existing-reward false positives, optional rewards, level gain.
- Actual Nuclear/Clock packet replays, terminal reward ordering, optional Oak Stick.
- Child-reminder policy, unrelated/stale replies, permanent persistence, passive observation, repeats disabled.
- Three-area preservation even when only one yields drops; movement stopping at six.
- Three-minute dry patrols in both runners; occupied targets, moving players,
  post-cadence occupancy checks, refresh recovery, and fresh acceptance with carried branches.
- Current-user named pipe, stdio MCP handshake, tool discovery and state calls.

## Remaining live checks

The corrected navigation build completed Awesome's acceptance, armor sweep,
gathering and turn-in; its newly observed coin terminal required this final
profile update. The final confirmation is covered by the actual replay.
Live player detours, excess-gold banking by the fixed automation, cooldown/refusal
dialogs and an armor-area drop remain unobserved. Nuclear/Clock carried zero gold; Awesome's manual banking
sequence was captured and replay tested. Login/reconnect login remain manual.
Ambiguous banking/turn-in requires reconciliation or player inspection. Other
catalog quests beyond the four executable recipes remain research metadata. Excalibur source/settings/processes
were untouched.

Perfect Hairstyle reached Eeva through Mileth, Abel Port, Undine and Suomi live.
Nuclear's manually assisted run received Emerald Ring and 15,000 XP at
2026-10-09 19:19:31 CDT, increasing level 3 to 5. No silk was carried or consumed.
His Suomi legend was not awarded, so the quest remains unfinished. The next
attempt is due 2026-10-16 19:19:31 CDT. Actual packet/action fixture:
`content/evidence/datester-7.41/perfect-hairstyle.json`.

The replay exposed Suomi's type-4 role-playing notice, several omitted Eeva
Next pages and a completion reply that still has Next. The corrected automation
replays the entire recorded conversation, advances only recognized Eeva pages,
closes the terminal page and correlates Emerald Ring/XP with its saved baseline.
The old pending transaction is reconciled from this evidence without resubmission.
Packet-level navigation tests verify one Next with the live entity/pursuit/step,
both when the town notice precedes walking and when it interrupts a move.

Every verified non-legend attempt records its actual time/rewards and schedules
another seven days later. A verified Suomi legend is the sole final goal; it
disables repeats. Tests cover saved deadlines, restart paused, no early resubmit,
retained attempt history and existing legend before the deadline.

Clock then completed the corrected full flow automatically on the installed
build, from Mileth Inn through Suomi's notice and all Eeva Next pages. At
19:37:14 CDT he received Emerald Ring and 15,000 XP, level 3 to 5, without silk.
The runner recorded one attempt and entered Scheduled at 19:37:15. Manual input
at 19:37:21 paused the schedule after the attempt was verified; its deadline
remains October 16 at 19:37:14 CDT. Fresh own-legend observation showed no Suomi
mark. The actual packets/actions and separately labeled final state observation
are in `perfect-hairstyle-clock.json`. A live legend-award outcome and an actual
elapsed seven-day retry remain unobserved; simulations cover those branches.
The native smoke checks all four quest choices, automatic weekly policy and
the birth form.
# Dark interface validation — October 9, 2026

The midnight quest journal keeps the existing quest, proxy and control behavior. All 308 automated tests pass. The native smoke path verifies all four quest policies, character journal selection, developer visibility, library search/availability, birth validation and exact choices, and renders standard/compact workspaces plus settings, login, the client picker, calibration, and dropdowns. It rejects solid light 8×8 surfaces and clipped buttons outside scrollable workspaces. Screenshots are under `artifacts/*-smoke.png` and `artifacts/quest-*.png`.

The Windows x86 self-contained build is published separately under `release/midnight-win-x86`; current saved data is copied locally. Existing running game and DAQuester processes are not restarted by the build or smoke checks.

## Danaan variation validation — October 13, 2026

Lancelot's second opening was discovered live before implementation validation.
Its final Cap, 1,000,000 XP, own legend and terminal popup closure are observed
MCP evidence, not an end-to-end automatic-run result. See
[the variation handoff](research/danaan-lancelot-variation.md).

After the active-status change was separated from existing filtered quest
notices, all 1,100 core tests and all 209 app tests passed. The focused variation
set passed 62 tests. The app suite built the Windows x86 desktop project;
generated TRX files remain under ignored `artifacts/danaan-variation/test-results`.
Tests used synthetic sessions and did not operate game characters. Four other
saved queues retained their baseline hashes. No packaging, deployment, release,
commit or merge was performed. The running app and game session remain open.

Fresh automatic live validation and live review of variant progress remain
outstanding. The player requested wrap-up and no further MCP work; no additional
live actions were taken to reconcile the old read-only research checkpoint.

## Danaan Loures court validation — October 14, 2026

The player-approved `loures-court` child branch retains the original and
`loures-orders` journeys. Its new Ase/Courtney/Franchesco prefix uses retained
server pages and separate purchase/handoff receipts, then joins Logan. See
[the court handoff](research/danaan-loures-court.md) for observations and gaps.

The isolated Release solution build under ignored `artifacts/danaan-court`
succeeded with zero warnings and errors. All 40 focused core checks and seven
focused app checks passed, including recovery from the sanitized actual action
and resource excerpt, projection through replay schema 3, reconnect persistence,
wrong identity/generation/profile, missing or stale pages, incomplete closure,
and missing quote/debit/item receipts. Automatic-flow resource outcomes remain
explicitly synthetic. Both complete xUnit projects passed: 1,173 core tests and
244 app tests. Normal Windows test-process access was used for named-pipe tests.
`git diff --check` passed; source and built quest recipe hashes matched.

The new executable is ready for the player's next DAQuester restart at
`artifacts/danaan-court/bin/DAQuester.App/release_win-x86/DAQuester.exe`.
Existing player Data was not edited manually. On resume, the runner can recover
the complete recorded prefix to the Cousin through its journal gate, without
another Oren Brandy purchase, Courtney handoff or Logan conversation. Missing
proof retains the old pending checkpoint. No game commands were sent during
implementation validation, no Excalibur process was changed, and the separate
paid Fisk wait remains intact. This build has not yet had a fresh full automatic
live court run or a new final quest reward.

## Varuna gatekeeper validation — October 14, 2026

Native MCP completed Pixel's one offered final Next at Varuna and confirmed
actual closure with unchanged gold/XP and the already-consumed Gland. The
paragraph was already present in Lancelot observation 67 but omitted from both
Loures recipes. [The research handoff](research/danaan-varuna-gatekeeper.md)
separates these observed facts from simulated execution and replay recovery.

The isolated Release solution build under ignored `artifacts/danaan-varuna`
succeeded with zero warnings and errors. All 59 focused core and nine focused
app checks passed. Both complete xUnit projects passed: 1,192 core tests and
246 app tests. Tests cover the corrected pages, unknown replies, pending Next
without another handoff, full original-generation replay receipts, missing or
contradictory proof, later progress, reconnect persistence and baseline-aware
replay projection. Source and built recipe hashes matched; `git diff --check`
passed. No game commands were sent during implementation validation.

The executable is ready at
`artifacts/danaan-varuna/bin/DAQuester.App/release_win-x86/DAQuester.exe`.
On resume it can recover the recorded Varuna stage to Mold collection through
the normal journal gate. Actual live recovery in that new build remains pending
the player's restart. No player Data was edited manually, no Excalibur process
was changed, and no final Danaan reward was claimed or certified.

## Travel recovery validation — October 14, 2026

Read-only native MCP inspection confirmed Pixel's repeated refresh/transport
pauses and the Path Temple queue block for the exact `You are not a Monk.`
active status. The player identifies the Monk Nightmare response. The
[research handoff](research/danaan-travel-recovery.md) and sanitized capture
separate that observation from code analysis and synthetic validation.

The isolated Release solution build under ignored `artifacts/travel-recovery`
succeeded with zero warnings and errors. The broad focused set passed 131
checks. Seven final targeted checks passed after adding the delayed unexpected
destination guard: six landing cases and the existing melee timing recheck.
Both complete xUnit projects passed against the final build: 1,205 core tests
and 246 app tests, with no skipped tests. Normal Windows access was used for
named-pipe tests. Source and built recipe hashes matched; `git diff --check`
passed.

The first complete core run, overlapping compilation, passed 1,202 checks and
failed one existing melee test with a 300 ms cancellation timer. That test
passed individually and in the final complete run after compilation finished.
An initial asynchronous landing fixture also hit its simulation's 100 ms
deadline under load; it now delivers the landing explicitly after proving that
the runner is waiting with its submission retained. The delayed unexpected-map
test failed before the wait predicate was corrected and passed afterward.

Refresh retries are read-only F5, retain the original connection baseline and
keep movement blocked until actual position/world-map evidence. Captured Next
is sent once and waits through latency; transport retains its pending receipt
until actual landing. Cancellation, disconnect, unknown landings/popups and
actual entry requirements retain their guards. The exact Monk message on map
395 remains recorded while unrelated Danaan travel continues.

The executable is ready for the player's DAQuester restart at
`artifacts/travel-recovery/bin/DAQuester.App/release_win-x86/DAQuester.exe`.
No game commands were sent during implementation or tests, no player Data was
edited manually, and no Excalibur process was changed. Pixel's final automatic
reward and queue completion remain unverified until the new build is resumed.

## Reward, hub routing and dachaidh validation — October 14, 2026

[The research handoff](research/quest-reward-route-dachaidh.md) separates Pixel's
actual final XP/item/own-legend receipts and normal completion reconciliation
from the new automatic runner tests. The original-opening Varuna capture proves
Agent's complete Gland handoff and player final Next; recovery keeps Greim's
warning. No final Agent reward or fresh full automatic quest is certified.

Regressions first reproduced the late reward-message loss, retained active-status
recovery, missing world-hub path, merchant spell argument, upper Coliseum planner
exception, original-route Varuna successor and pre-inspected terminal-popup
bypass. The new exact page alternatives also preserve variant prefix equality.
The broad focused set passed 260 checks; 36 final branch/Varuna checks passed
after that last guard was added. Dachaidh cases cover Wizard and provisional
Priest preparation, missing prerequisites, unknown quotes, partial receipts,
replacement generations, parent receipt preservation and no repeated payment.

Concurrent artwork-cache changes appeared during the working-tree build. That
app run passed the established 246 checks and failed six new artwork-cache
tests. Those edits were preserved. An ignored source snapshot at
`artifacts/quest-fixes-source` contains the quest changes and the established
interface, excluding the unfinished concurrent artwork changes. Its quest source
and content hashes match the working-tree fixes. Final validation and delivery
use this isolated snapshot.

The isolated Release solution build succeeded with zero warnings and errors.
Both complete xUnit projects passed with no skips: 1,245 core tests and
246 app tests. All 349 Core/DarkAges source files
match the working-tree fixes, and the copied application has zero file-hash
mismatches against its isolated build. Recipe and route hashes match the source;
`git diff --check` passes. The executable is copied to
`artifacts/quest-fixes-final/DAQuester.exe` for the player's DAQuester restart.
No game commands were sent during these builds or tests, no player Data was
edited manually, and no Excalibur process was changed.

## 2026-10-14: queue supplies, mundane visibility, and Undine recovery

Known obtainable supplies are prepared before quest NPCs, with ready queued
consumables combined and current inventory, remaining handoffs, capacity, and
known payment funds respected. Independent acquisition checkpoints preserve
dialogue indexes and prevent repeated uncertain purchases or pickups. Mehadi
Gland sources use guide provenance and recorded travel costs; named inventory
receipts remain the proof. Mundane and travel-service approaches stop when the
intended NPC becomes visible, after safe doorway departure. Undine's known popup
and active status warning use guarded weapon removal with both receipt sides.

Test-first regressions reproduced late gathering, extra proximity walks, missing
status handling, incomplete removal proof, shared-consumable undercounting,
unknown-branch spending, inherited parent cooldowns, and future purchases spending
current payment funds. Captured Fisk dialogue and resource deltas remain checked;
its rebased early-purchase timing is explicitly synthetic. No fresh live runner
or Mehadi drop capture is claimed.

Final complete suites passed without skips: **1,265 core/protocol tests** and
**254 app tests**. The last focused purchase/travel set passed 25 checks. The
Release desktop build succeeded with zero errors and one cached `NU1900` warning
because NuGet's vulnerability-data endpoint was unavailable. The build resides
in `artifacts/quest-preparation-build/DAQuester.exe`; its recipe and route hashes
match the working tree. `git diff --check` passes. No interface layout or packaging
change was made, so no render review or release-packaging gate was required.

Existing working-tree edits were preserved. All sessions used by tests were
synthetic. No real characters were operated, player Data was not modified, and
Excalibur was not stopped, restarted, relaunched, or changed.

## Captured-run correction — 2026-10-14

The subsequent Glioca failure reproduced an off-screen cached NPC being treated
as visible. Shared approach, direct open and core arrival checks now require a
unique non-player NPC within ten tile steps on the actual map, after validated
doorway departure. Native synthetic movement checks cover both walking modes,
speech approaches and unanchored visits.

The Donnan failure matched Lancelot observation 140 exactly. Its meditative-master
referral is now a scoped alternative to the original jail-keeper referral in
all three journeys. Pending replies retain their original identity, revision
and generation checks. This build initially reopened Donnan's topic on a new
connection. The later assignment correction below supersedes that behavior;
the referral has already advanced server state and must continue to Kyros.

The [captured-run audit](research/danaan-captured-run-audit.md) accounts for 138
distinct NPC pursuit pages, 207 Danaan/Fisk transcript transitions and 50 Next
transitions across five raw Danaan captures. Its independent Cail continuation
requires the fruit and greaves handoffs, Cap/XP/own legend and terminal close.
All 20 focused audit/recovery checks passed, including negative receipt cases.

Both complete xUnit projects passed with no skips: **1,292 core/protocol tests**
and **254 app tests**. The broader Danaan/Fisk, preparation and travel set passed
202 focused checks before the final omission/reconnect coverage was added.

The corrected Release build is `artifacts/quest-preparation-fixes/DAQuester.exe`.
It was built separately because the previous output is running. Its recipe and
route hashes match the working tree. Build result: zero errors, one cached
`NU1900` warning for the unavailable vulnerability-data endpoint. No running
application, player Data or real character was changed by the build or tests.

## Donnan persisted-receipt boundary — 2026-10-14

The player's running corrected build still paused after reconnect. Read-only
inspection found pending revision 2575, submitted page 2576 and saved reply
2577. The recovery comparison was inverted relative to `BeginAsync`, which
deliberately stores `observedDialog.Revision - 1`. Earlier synthetic fixtures
had incorrectly fabricated an equal revision and therefore missed this bug.

The recovery now requires that exact writer boundary and a reply strictly
newer than the submitted page. A sanitized capture of the actual checkpoint
and tests using the real journal writer reproduced the failure before the
correction. All **64 focused recovery/audit checks passed** afterward. The
actual checkpoint simulation advances from task 26 to 27 with a fresh actor
and generation, without submitting an action from the old connection. No
player Data was edited and no real character was operated.

Both complete projects then passed with no skips: **1,295 core/protocol tests**
and **254 app tests**. The replacement build is
`artifacts/quest-receipt-fix/DAQuester.exe`; its recipe and route hashes match
the working tree. Release build succeeded with zero errors and the same single
cached NuGet vulnerability-data warning. The running application and Excalibur
were left untouched.

## Donnan assignment continuation — 2026-10-14

The next reported failure was a progression error in the reconnect recovery,
not missing fruit. Read-only inspection of the owned saved run confirmed
Meaveen's fruit acceptance, Donnan's completion confirmation and the exact Kyros
referral. Kyros had not been visited. The previous recovery reopened Donnan's
topic, which now checked the unfinished Kyros task and correctly returned
`You have not finished your task`.

The runner now persists the assignment independently of referral presentation
and continues it after reconnect without reopening Donnan. For the already
damaged checkpoint, automatic replay recovery requires the original owned root
selection, confirmation Next action and later exact referral, with matching
generation/entity/revision receipts. Missing proof or later Kyros/Erin progress
retains the pending checkpoint. Refusal popup cleanup is separate and submitted
once. The refusal and carried greaves alone never prove a completed task.

All **40 focused audit/recovery checks passed**, including complete captured Cail
continuations interrupted after assignment, before referral acknowledgement and
after popup closure. They still require the fruit and greaves handoffs once,
Erin's reward once, Cap/XP/fresh own legend and terminal closure. A standalone
x86 process also read the original 77 MB saved replay with the production
projection: 189 relevant snapshots and seven actions restored the assignment,
without inferring quest completion. Adding the newer saved replay also passed.

Both complete xUnit projects passed with no skips: **1,312 core/protocol tests**
and **259 app tests**. The Release build is
`artifacts/donnan-assignment-fix/DAQuester.exe`. Build result: zero errors and one
cached `NU1900` vulnerability-data endpoint warning. Recipe, route and recovery
fixture SHA-256 hashes match source; `git diff --check` passes.

All quest execution was synthetic. Saved player Data was read, not edited.
No real character was operated and no running application or Excalibur process
was stopped, restarted, relaunched or changed. Load the new DAQuester build to
use the fix; this is not a new live automatic quest completion.

## Bare-hand bashing correction — 2026-10-14

The player reported that characters can bash with their hands. The melee
basher incorrectly set its equip-attempt flag before finding an eligible owned
weapon, then stopped on the next tick with `No usable melee weapon is equipped
or in the bag, so the fight stopped.` No equip command had been sent.

The flag is now set only immediately before an actual equip submission. With
no eligible weapon, normal assails and skills continue. Owned usable weapons
remain preferred, and a submitted equip without confirmation still cannot be
resent. Ceannlaidir's equipped-weapon entry requirement is unchanged.

Test-first regressions reproduced the false stop for all six base classes and
for a bag containing only an ineligible melee weapon. The tests verify actual
assail commands with an empty weapon slot and no learned skills, continued
rotation, one owned-weapon equip and no retry of an uncertain equip. Existing
Donnan cleanup test snapshots were also corrected to supply required known XP
and a snapshot revision at least as new as the captured popup. All **27 final
focused combat/recovery checks passed**; the broader combat, travel and supply
checks passed apart from those fixture omissions before their correction.

Both complete xUnit projects then passed with no skips: **1,324 core/protocol
tests** and **259 app tests**. Release output:
`artifacts/unarmed-bashing-fix/DAQuester.exe`; zero build errors and the existing
single cached NuGet vulnerability-data warning. Its recipe, route and Donnan
recovery fixture hashes match source. `git diff --check` passes. All execution
was synthetic; no player Data or running application was changed.

## Blanchefleur Fiosachd branch — 2026-10-14

The captured sailor referral reproduced the missing later receipt. Test-first
checks failed for the missing branch, original-checkpoint recovery and Wizard
Hall's too-shallow visible-NPC approach. The new exact-reply branch preserves
the submitted prefix, reads the approved Fiosachd clue and returned menu loop,
then follows captured Keefe and Logan pages to Cousin. Recovery requires owned
original source actions and actual closures; missing choices, wrong identities,
changed balances and later Cousin/later-stage progress preserve the pending
submission. New-generation later payments also prevent rewind.

The full synthetic suites pass with no skips: **1,340 core/protocol tests** and
**262 app tests**. The core suite required execution outside the filesystem
sandbox because its private Windows named pipes were denied there. The final
run passed every test. The dialogue fixture supplies synthetic travel; native
fixtures separately check Wizard Hall departure and first-visible stopping.
Varuna and Donnan continuation checks include the new branch. Queue control
uses existing queue gates and a repeated start cannot toggle a running queue.

Release output: `artifacts/blanchefleur-fiosachd-fix/DAQuester.exe`. Clean Release
build and publish succeeded, with the existing cached `NU1900` endpoint warning.
Packaged recipe, routes and recovery-fixture SHA-256 hashes match source;
`git diff --check` passes. Live discovery is documented separately in
[the branch research](research/danaan-blanchefleur-fiosachd.md); synthetic tests
do not establish a new full automatic completion or an observed Band reward.

## Independent NPC reply continuation — 2026-10-14

The first resumed run reconciled Blanchefleur, paid Cousin once and handed one
Gland to Varuna. It then exposed the original spirits ending on the new opening.
This reproduced an incorrect assumption that the opening fixed every later NPC
reply. The player authorized finishing Varuna's final informational Next; its
actual closure and complete original handoff are retained in the new spirits
capture. No Gland delivery or payment was repeated.

Fresh informational replies now select a unique captured continuation for the
same NPC task, independently of the opening. They must preserve the map,
resource contract and every submitted page. The durable reply proof is checked
again after reload; stale/foreign/unknown replies, new choices, conflicting
handoffs and ambiguous future paths retain the pending submission. Recovery of
the actual stopped run continues at Greim's warning. Stage progress uses the
same resolved path, including that warning.

Final complete suites pass with no skips: **1,354 core/protocol tests** and
**263 app tests**. The progress regression first reported 26 instead of 27;
the corrected count passes. Release output is
`artifacts/danaan-npc-replies-fix/DAQuester.exe`. Build/publish succeeded; the
cached `NU1900` endpoint warning remains. All three application assembly hashes,
recipe, routes and the spirits fixture match source, and `git diff --check`
passes. Synthetic execution remained separate from the live segment.

## Dreamer actual return map — 2026-10-14

The resumed run finished Greim and every Dreamer page, then returned to Mileth
Threshold instead of a whitelisted inn. The player supplied the no-citizenship
threshold behavior and corrected departure to Up. The bounded up step was
accepted; the capture retains the original final Next, map header, actual
position and later world travel. Return now follows the submitted final prompt
and an actual different map, with coordinates awaited before movement. Normal
navigation handles the threshold exit. Other quest transports retain their
own destination checks.

Tests first reproduced rejection of both threshold and synthetic other-map
returns. Original-action replay recovery is tested across reconnect, including
delayed action headers, missing receipts, foreign actors and later handoffs.
The old recorded-redirect test was updated to accept the actual changed map;
unchanged-map and unrecorded-redirect cases still preserve the submission.
The app replay projection retains actual landing coordinates without bag data.

Final complete suites pass with no skips: **1,365 core/protocol tests** and
**264 app tests**. Release output: `artifacts/dreamer-return-fix/DAQuester.exe`.
Build/publish succeeded with the existing cached `NU1900` endpoint warning.
All three assembly hashes match the build; source recipe, routes and return
fixture are included. `git diff --check` passes. These checks do not establish
final live quest completion.

The later live continuation completed the resumed Cap journey automatically,
including Donnan's actual acceptance, Kyros's handoff, Erin's +1,000,000 XP
active message, Cap inventory receipt, fresh own legend and server-confirmed
terminal popup close. The sanitized final receipts are in
`danaan-blanchefleur-resumed-completion.json`; see
[the completion account](research/danaan-blanchefleur-fiosachd.md#resumed-automatic-completion).
The queue then accepted Fisk's paid bouquet wait, completed the queued Pet
Faerie visit and Rucesion Song return, and parked facing down at Tavern 413
(10,1). Fisk's wait remains running until its 2026-10-15 03:55:04 UTC check.
Earlier manual discovery/recovery means this is resumed completion, not a new
uninterrupted automatic run; Band and this run's final Fisk reward remain
unverified. No implementation changed after the complete suites above.

## Successful Faerie bond, Callough activation and scheduler clarity — 2026-10-14

The captured successful Faerie branch first failed its new automatic regression
at the missing Yes choice. The runner now binds that choice to the exact prior
250hp/250mp offer, writes a separate bond checkpoint, verifies Pet Faerie receipt,
and completes actual dialogue closure before return. Captured server-close and
remaining-popup timings pass, along with late delivery and uncertain Yes/close
recovery without another bond. Stale, foreign, changed and ambiguous offers are
rejected. The earlier player-selected live Yes remains distinct from these
synthetic automatic-choice checks; see [the bond evidence](research/shinewood-faerie-pet-bond.md).

Five new/updated regressions reproduced pre-acceptance branch preparation,
legacy preparation and missing Callough approach targets. Preparation now waits
for current-connection acceptance, including queued branches, while preserving
other early supplies. The normal collection helper enforces the same activation.
Both Callough visits supply his recorded reachable public-side approach; a
native synthetic fixture walks from a distant entry and stops at first actual
visibility. Existing reconnect, pending turn-in, cooldown, permanent-completion,
random-drought and patrol-extension coverage passes.

Scheduler keeps its runtime consent, ownership and per-entry resume gates.
The interface explains Save versus global enable, retains the page agreement
across those actions, shows Resume schedule on stopped rows, and distinguishes
successful feedback from errors. Native isolated preview screenshots were
reviewed at desktop and compact sizes, including the scrolled saved rows and
agreement/footer. No real character was operated by builds or synthetic checks.
[The investigation](research/stick-activation-and-scheduler.md) separates the
player's report, observed entry/timeout and saved scheduler state from code facts.

Final complete suites: **1,385 core/protocol tests** and **264 app tests**, all
passing with no skips. Focused quest/recovery coverage passed 227 tests before
the additional native approach case, which passed in the full suite. Windows
IPC fixtures ran with the same required local permissions as prior suites.
Build/publish succeeded to `artifacts/pet-stick-scheduler-fix/DAQuester.exe`;
the cached `NU1900` vulnerability-data endpoint warning remains. All three
application assembly hashes match the tested build, the bond evidence matches
source, and `git diff --check` passes. Running player sessions were not restarted
to load this build.

## DAWindower X integration — 2026-10-14

New DAQuester-owned clients load the pinned native x86 DAWindower X DLL while
their primary thread is suspended. Synthetic launcher regressions cover the
existing proxy, multi-instance and agreement patches, load-before-resume order,
failed/null/timed-out loading, cancellation and cleanup of only the newly created
process. Wrong-architecture, executable and missing-library inputs are rejected.
The actual bundled DLL also passes upstream's synthetic first-DirectDraw fixture
through the real Windows loader in ordinary and Unicode paths, without an extra
initializer. These checks do not establish a live game renderer or login result.

Release validation exposed three recorded maps missing from collision metadata.
The new coverage regression first failed on maps 3029, 3000 and 195. Their grids
were derived from installed files through the existing exporter, with all 2,664
tile bits audited and the established collision-table digest verified. Previous
grids and unavailable-map entries are unchanged; coverage is now 1,420 of 1,435
recorded maps. See [provenance](provenance.md) for the resulting asset digest.

The packaging guard also caught a stale Core assembly's embedded PDB path after
an incremental build. A forced symbol-free solution compilation removed it;
the complete suites then passed on those exact binaries: **1,396 core/protocol
tests** and **266 app tests**, with no failures or skips. The final reports are
`core-windower-clean.trx` and `app-windower-clean.trx` under
`artifacts/windower-tests`. No real character was operated during this work.

`Build-Release.ps1 -NoBuild` succeeds to
`artifacts/dawindower-x-release/release`, including its release verification.
An independent `Verify-Release.ps1` pass also succeeds against that output.
Installer, portable archive and update package retain the pinned DLL, MIT notice
and actual source/hash record. Test fixtures, symbols, checkout paths, player
Data and raw game assets are excluded. Application and native-library hashes
match the tested assemblies. Compression initially exhausted local disk space;
unused expanded generated packages were removed after process/path checks,
preserving release archives, source, research captures and Data. The cached
`NU1900` vulnerability-endpoint warning remains. Existing game windows and
Excalibur were not restarted or modified.

## X + button through DAQuester — 2026-10-14

The player selected a blank managed login window for X's + action. Each accepted
request now uses a fresh ownership slot and the normal proxy, supported-image
validation and suspended-X launch transaction, without credentials or character
selection. Named Launch retains its existing focus and saved-login behavior.
The per-app pipe authenticates the actual Windows caller PID against live process
handles registered before primary-thread resume, then checks ownership again on
the app dispatch thread. Failed or uncertain handoffs never launch a standalone
fallback; shutdown closes the endpoint and preserves existing game processes.

Three host tests first failed before the pipe existed. The before-resume
regression reported registered PID 0 instead of 42; the native + regression
timed out without a host call, and the fresh blank-launch regression failed on
its unimplemented path. Focused coverage now passes 11 launcher and seven app
cases, including the actual X caption double-click producing one request from
the synthetic client's Windows PID, failed callback without retry, malformed
requests, unregistered origins and shutdown with an incomplete caller.

Final full suites pass with no skips: **1,397 core/protocol tests** and **271 app
tests**. Reports are `plus-core-full.trx` and `plus-app-full.trx` under
`artifacts/windower-tests`. Build and native x86/package checks pass; the existing
cached `NU1900` endpoint warning remains. `Build-Release.ps1 -NoBuild` succeeds to
`artifacts/dawindower-x-plus/release`, and an independent `Verify-Release.ps1`
pass verifies both archives, source/patch/DLL fingerprints, licenses, checksums,
feed and private-data exclusions. All application/native hashes match the tested
assemblies. Synthetic fixtures remain outside the distribution.

Native caption review uses a separate isolated fixture harness, not a real game
client. The first preview failed its module-path comparison because the harness
used mixed path separators; canonicalizing that harness path resolved it without
changing production code. No player Data, queues, game files or Excalibur working
copies were modified. Existing clients need relaunch to receive the new endpoint.
The final caption-only capture was reviewed manually: dark chrome and controls
remain readable, with + disabled during the pending handoff. The separate fixture
completed one request and exited successfully. This is synthetic interface
evidence; no live game renderer or login outcome is certified.

## Release 1.5.0 qualification — 2026-10-15

The clean symbol-free Release solution build passes. Both complete xUnit
projects pass with no failures or skips: **1,487 Core tests** and **322 App
tests**. Reports are `core-complete.trx` and `app-complete.trx` under
`artifacts/release-1.5.0-checks/results`. The test copies use the normal fixture
directory depth and installed test runtimes, separate from the self-contained
application runtime. Initial host-launch failures were test-layout errors;
no product behavior was changed to resolve them.

`Build-Release.ps1` passes with a clean isolated application compilation to
`artifacts/release-1.5.0/release`. An independent `Verify-Release.ps1` pass checks
both archives, checksums, feeds, native provenance, licenses, walkability and
private-data exclusions. All three packaged application assembly hashes match
the tested solution build. `Test-UpdateCompatibility.ps1` verifies the pinned
public 1.0.7 updater against 1.5.0 with unchanged identity and a verified local
download, without installing or applying the update. The existing cached
`NU1900` vulnerability-feed warning appears in the solution build; the isolated
package compilation has zero warnings and errors.

The existing GitHub build failure was reproduced with Windows PowerShell's
hash function unavailable to command discovery. Loading the host's utility
module explicitly makes that same native-build regression pass, including
cached patch/library fingerprint checks and the synthetic fixture. Earlier
native interface review at 1420 × 900 and 900 × 560 remains applicable: release
preparation changes only version metadata, release documentation and this build
host fix. No real character, player schedule or Excalibur process was operated.

### Hosted x86 runtime follow-up

The 1.5.0 tag's clean GitHub build and all 1,487 Core tests passed. The App
suite could not start because the Windows runner had only an x86 .NET 9
runtime; it reported no App test outcomes. Publication was correctly skipped.
The 1.5.1 candidate makes the win-x86 App test host self-contained, removing
the dependency on a global x86 .NET 10 installation. Product source is unchanged
from the fully passing local 1.5.0 qualification above; only test runtime
provisioning, version and release documentation change. The failed 1.5.0 tag is
preserved and no release assets were published for it.

### Hosted journey watchdog follow-up

Both 1.5.1 GitHub workflows completed 1,486 Core tests and stopped the same
captured Fisk journey at its ten-second cancellation watchdog (`Paused by
player`). The 1.5.0 hosted Core suite and prior complete local suites passed
that identical journey. The 1.5.1 self-contained App suite started without a
global x86 runtime and passed 321 tests, with one native-caption handoff hitting
its five-second bound; all three native startup cases then passed in isolation.
The 1.5.1 packages pass build, independent release verification and isolated
1.0.7 updater compatibility without installation.

The 1.5.2 candidate changes only test watchdog bounds and release metadata.
The captured full journey retains its actual file journal and all dialogue,
resource and completion assertions, with a one-minute cancellation watchdog for
slow checkpoint flushes. The native fixture retains exact PID, one-call, receipt
and exit assertions with a thirty-second startup bound matching its own native
wait. Production action timeouts, ownership and reconciliation are unchanged.
The earlier unpublished candidate tags are retained.

Both focused regressions pass after the watchdog correction: the captured Fisk
journey and all three native startup cases. Reports are `fisk-focused.trx` and
`native-focused.trx` under `artifacts/release-1.5.2-checks/results`. Both test
projects compile without warnings or errors. Complete local and hosted suites
must still pass before the tagged workflow publishes its newly verified assets.
