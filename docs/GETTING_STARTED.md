# Getting started

## Install

Download the installer or portable ZIP from
[Releases](https://github.com/BuildWithRaymond/DAQuester/releases/latest).
Use the installer for a Start menu shortcut, or extract the portable ZIP and open
its `DAQuester.exe`. Keep the complete portable folder together. .NET is included.

DAQuester supports the exact DATester 7.41 client hashes in
[provenance](provenance.md). It checks the executable before launching and rejects
unsupported builds. Raw game files are not included.

## Set up and play

1. Open **Settings**, select `Darkages.exe`, and save. Restart DAQuester after
   changing the client. In **Characters**, add rows for your character names
   and optional passwords, then **Save accounts** to apply name changes.
   Blank passwords keep existing saved passwords.
2. Use the **Launch** action beside a saved character. Its saved password signs
   into that character; without one, sign in manually. Only clients opened
   through DAQuester connect through its proxy. Existing owned clients are focused.
   These new clients use the bundled **DAWindower X** automatically. Keep its
   DLL with the application. X's **+** opens a fresh blank login through
   DAQuester's proxy. Sign in manually, then select that character in DAQuester
   for quests. Use a saved character's Launch action for saved login.
   Before saving any characters, the header's **Launch** opens a blank client
   without filling in or submitting a login.
3. Use the header's character switcher, search the library, and select automatic
   quests. Drag the queue into order, or use its arrows; Ctrl+Up/Down also moves
   a focused entry and Delete removes it. Select any quest and expand
   **Quest details** for its requirements, rewards, **Change choices** and **Open quest guide**.
   Use a checkbox or **Add to queue** in Quest details, even for a single quest.
4. **Run queue**, marked with a play icon, performs one verified attempt per entry.
   Birth and Emerald Dawn choices are confirmed before movement. A weekly Hairstyle attempt can
   finish without its Suomi legend; its cooldown and repeat intent remain saved.
   A pending or blocked attempt pauses the queue before the next entry.
5. **Pause queue** retains progress and order. **Resume queue** checks saved results.
   **Stop** cancels automation, keeps checkpoints, and unlocks queue edits.
   Moving, turning and clicking NPCs do not automatically pause automation.
   Use **Pause queue** or **Stop** when you want the run to pause or end.
   The journey card follows the active quest, including a paused queue's saved
   checkpoint. Browsing the library or planning a queue does not change it.
   Until a run starts, or after it stops or finishes, it says **No journey running**.
   The compact card shows progress on the left, with the queue and its run controls on the right. Fast mode is in Settings.
   Use the fold arrow for a compact bar; queue controls stay available there too.

**Run history** combines verified queue outcomes with existing quest journals.
Saved-login form automation is tested synthetically; live login is not part of
build or UI validation.

Left navigation opens pages in the same window. Library filters, account edits
and quest progress stay in place when switching pages. All planner runs use the
queue, including one quest. To run another eligible attempt, add the quest to the
queue again. Use the optional Scheduler for unattended repeat attempts.

**Scheduler**, in the left sidebar, is a separate, completely optional mode and is
off by default. Its page schedules any supported automatic quest at a chosen
local date/time or as soon as it is eligible. Select recurring mode explicitly
for Spare a Stick or Perfect Hairstyle. Quest cooldowns and requirements still
apply; guide-only quests cannot run automatically. Save any required quest
choices before arming the schedule.

**WARNING: A scheduled login can boot your existing character session.** This
also applies to saved helpers selected for flower quests. Scheduler uses saved
logins, performs verified eligible work, then closes only the game windows it
opened. It logs out between timed altar offerings and daily flower progress.
An existing game window known to DAQuester is left alone and the schedule needs
attention. Manual input does not pause scheduled work; Scheduler still closes
the windows it opened when work ends. Use **Pause all** or **Stop all quests**
to stop scheduled automation.

Saving a schedule does **not** enable Scheduler. Read and acknowledge the page's
warning, then explicitly choose **Turn on saved schedules**. A stopped entry
also needs **Resume schedule**; saving edits preserves its stopped state.
DAQuester must remain running. By default, closing its window hides it in the
system tray; schedules and active journeys continue. Click its tray icon to
reopen it, or right-click for status, Pause scheduling and Exit DAQuester.
Exit saves progress and stops scheduled work. In Settings, turn off **Keep
running in the tray** to make closing the window exit instead.

**Start DAQuester when I sign in to Windows** is a separate, optional Settings
choice, off by default. Save settings to apply it. Installed and portable builds
use their stable launcher; keep a portable folder in the same location. Startup
does not enable scheduling. Windows must remain signed in and the PC awake;
locked-session and disconnected remote-desktop launches are not yet qualified.
There is no Windows service or signed-out execution. Switching pages keeps
schedules running. Enabled schedules
resume after restarting DAQuester; overdue work runs once rather than replaying
all missed intervals. **Pause all** or the main **Stop all quests** keeps scheduling
off until explicitly enabled again.

If the server reports **already logged in**, that character's scheduled logins
are blocked until explicit review. There is **no automatic retry**, including
after restarting or editing a schedule. Failed or unconfirmed logins also require
review. The page lists blocked logins, including helper characters: acknowledge
the warning before **Review login block**, then re-enable the
affected schedule. Interrupted submissions retain their checkpoints and are
reconciled before another action; they are never blindly submitted again.

DAQuester remembers each character's observed level and own legend marks,
separately for each server profile. An idle connected character's legend is read
after login and refreshed whenever the server sends a new own legend list.
Those facts survive disconnects and app restarts. Spare a Stick is selectable
only at known levels 1–11; a known level 99 is blocked. Recognized finished legend
goals show **Completed**, including Muisir, Birth and Emerald final outcomes.
The Hairstyle goal ends when its Suomi legend is recorded; a Stick legend alone
does not end repeat attempts. Unknown requirements still need a live check.
Green rows are ready. Status labels and icons distinguish choices, cooldowns,
completed goals and guide-only entries. Hover over a row or its checkbox to see
why it can or cannot join the queue. You can arrange eligible quests while
disconnected; running them still requires a connection and fresh checks.

A newly verified outcome plays a blue spiral over its character. If that character
isn't visible, a brief notice shows who finished. Windows reduced-motion settings
replace animation with a written notice. Original artwork requires the supported
game installation and is not included in DAQuester.

If a saved Spare a Stick reward was interrupted, DAQuester checks matching recorded
evidence before continuing. A verified reward retains its original completion time
and cooldown. Missing evidence keeps the submission paused; it is not sent again.

Optional **Fast mode** in **Settings → Movement** saves separately for the selected character
and defaults on. Uncheck it for normal walking. With fast mode enabled, another
player within 20 tiles automatically restores normal speed. It speeds up again when
that player leaves. Your own connected characters do not cause this slowdown.
The checkbox shows **fast pace** or **normal pace near players**, so the slowdown
is visible. Fast travel animates immediately and
queues a few steps while waiting for replies; it settles them before entering
doors or finishing travel. You can change it during a quest without restarting.

Travel automatically compares walkable routes, alternate doors, landing points
and recorded shortcuts. It chooses the shortest known walking route and adjusts
when observed players block an exit. No separate route setting is needed.

The supported DATester profile includes walkability data for 1,417 of its 1,432
recorded maps. DAQuester uses those grids automatically when your client has no
cached maps or collision archive, including on another computer. You do not need
to copy a populated map cache. Fifteen recorded maps are listed as unavailable;
if neither client data nor bundled coverage is usable, travel pauses safely.
Raw map files, sprites and the game client are not part of the package.

Quest-specific rewards, repeat policies and evidence limits are in
[QUESTS](QUESTS.md). The library's **Guide only** entries require manual play.
They are not available for automatic execution.

## Optional updates

Settings provides **Check for updates when DAQuester opens**, off by default.
Use **Save settings** to remember this preference. Startup checks only report newer stable
releases. Use **Download update** to fetch one, then **Install and exit** to apply.
Regular exits do not implicitly authorize installation of downloaded packages.

Finish quests and close DAQuester-launched game clients first. DAQuester refuses
to install while game sessions or client processes remain active. It never
closes game processes for you. Start DAQuester again after installation.
Failed/offline checks leave the app usable and can be retried.

Updates are supported by the installer and Velopack portable package. A raw
`dotnet publish` folder shows updates as unavailable. Builds from before the
updater was introduced need one manual installation to gain automatic update
discovery; existing Velopack releases can update to this version directly.

## Existing data

New versions store settings, journals, encrypted logins and replays in:

```text
%LOCALAPPDATA%\DAQuester\Data
```

Close all DAQuester instances before migration. If this destination does not
exist, DAQuester imports an adjacent legacy `Data` folder using a complete staged
copy. It retains the old folder and does not merge into or overwrite an existing
destination. A failed copy leaves the destination uncommitted and can be retried.

For a new installation in a different directory, copy your old `Data` folder to
`%LOCALAPPDATA%\DAQuester\Data` **before first launch**. If the destination already
exists, first back it up and review which data to keep; automatic migration will
not replace it. Saved passwords remain bound to the original Windows account.
Keep a backup before switching releases or moving machines.

## Diagnostics and connection issues

Every diagnostic line is appended to `Data\diagnostics.log`; sanitized replays are
saved to `Data\replays` whenever a character disconnects and when DAQuester exits.
Default local proxy ports are 2910–2912; other DA tools may use their own ports.
Resolve port conflicts in `Data\profiles.json` (or the control bridge `settings`
method), then restart DAQuester. It does not change Excalibur's settings or processes.

For evidence export or local automation, see [Control and MCP](control.md).
Do not publish your Data folder, passwords, login packets or session secrets.

## Operational limits

Only the supported client/profile and six automatic recipes are executable. Recorded live
runs validate specific cases; synthetic tests do not guarantee every server
condition. Gathering depends on actual drops and may continue during dry patrols.
Unexpected dialogs and uncertain transactions pause rather than repeat blindly.
Some banking, cooldown, detour and patrol cases still need live evidence; current
details are in [validation](validation.md) and [provenance](provenance.md).

The **Scheduler** icon has a status dot: red means off, yellow means schedules
are paused or need attention, and green means active without errors. Hover for
the full state. A paused or blocked schedule keeps the dot yellow even when
other schedules are armed. Saved rows show the portrait, character, quest and
local run time together; their action menu provides edit, pause, review and remove.
The current journey follows the selected character's scheduled run from
login through logout, even if its manual queue was previously stopped.

The quest library starts with **Available** quests eligible for queue planning.
**Completed** shows finished goals that cannot repeat, **Guides** shows manual
guides only, and **All quests** shows everything. **Has legend marks** restricts
any category to quests offering legend rewards; uncheck to include both kinds.
On Characters, legend marks wrap into compact chips and scroll within the card.
