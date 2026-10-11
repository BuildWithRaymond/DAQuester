# Getting started

## Install

Download the installer or portable ZIP from
[Releases](https://github.com/BuildWithRaymond/DAQuester/releases/latest).
Use the installer for a Start menu shortcut, or extract the portable ZIP and open
its `DAQuester.exe`. Keep the complete portable folder together. .NET is included.

DAQuester supports the exact DATester 7.41 client hashes in
[provenance](provenance.md). It checks the executable before launching and rejects
unsupported builds. No game binaries or assets are included.

## Set up and play

1. Open **Settings**, select `Darkages.exe`, and save. Restart DAQuester after
   changing the client. In **Characters**, add rows for your character names
   and optional passwords, then **Save accounts**. Names update the journal
   immediately. Blank passwords keep existing saved passwords.
2. Use the **Launch** action beside a saved character. Its saved password signs
   into that character; without one, sign in manually. Only clients opened
   through DAQuester connect through its proxy. Existing owned clients are focused.
   Before saving any characters, the header's **Launch** opens a blank client
   without filling in or submitting a login.
3. Use the header's character switcher, search the library, and select automatic
   quests. Drag the queue into order, or use its arrows; Ctrl+Up/Down also moves
   a focused entry and Delete removes it. Guide-only entries open their details.
4. **Start queue** performs one verified attempt per entry. Birth and Emerald
   Dawn choices are confirmed before movement. A weekly Hairstyle attempt can
   finish without its Suomi legend; its cooldown and repeat intent remain saved.
   A pending or blocked attempt pauses the queue before the next entry.
5. **Pause run** retains progress and order. **Resume run** checks saved results.
   **Stop** cancels automation, keeps checkpoints, and unlocks queue edits.
   Manual movement/NPC interaction pauses automation while gameplay continues.
   The journey card shows the next queue entry. Open **Run history** for saved
   receipts, or **Quest controls** for individual actions and fast mode.

**Run history** combines verified queue outcomes with existing quest journals.
To return after a queue, connect the character, move to the desired location,
then use **Set home here** in Characters. Enable **Return home when finished**
in that character's queue. Failed travel leaves a resumable return checkpoint.
Saved-login form automation is tested synthetically; live login is not part of
build or UI validation.

Left navigation opens pages in the same window. Library filters, account edits
and quest progress stay in place when switching pages. For Spare a Stick,
**Run again** starts another attempt after cooldown; **Repeat** schedules further
attempts every eight hours until Callough confirms permanent completion.

DAQuester remembers each character's observed level and own legend marks,
separately for each server profile. An idle connected character's legend is read
after login and refreshed whenever the server sends a new own legend list.
Those facts survive disconnects and app restarts. Spare a Stick is selectable
only at known levels 1–11; a known level 99 is blocked. Recognized finished legend
goals show **Completed**, including Muisir, Birth and Emerald final outcomes.
The Hairstyle goal ends when its Suomi legend is recorded; a Stick legend alone
does not end repeat attempts. Unknown requirements still need a live check.
Green-tinted rows are Ready/Choices; blocked and unavailable rows are muted.

Optional **Fast mode · slows near players** saves separately for each character
and defaults off. Check it for Excalibur's fast pace when alone; another player
within 20 tiles automatically restores normal speed. It speeds up again when
that player leaves. The checkbox shows **fast pace** or **normal pace near
players**, so the slowdown is visible. Fast travel animates immediately and
queues a few steps while waiting for replies; it settles them before entering
doors or finishing travel. You can change it during a quest without restarting.

Travel automatically compares walkable routes, alternate doors, landing points
and recorded shortcuts. It chooses the shortest known walking route and adjusts
when observed players block an exit. No separate route setting is needed.

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
`dotnet publish` folder shows updates as unavailable. Older builds need one
manual installation of this release to gain the updater.

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

## Developer mode and connection issues

Developer mode reveals logs, connection profiles, recording and calibration.
Default local proxy ports are 2910–2912; other DA tools may use their own ports.
Resolve port conflicts through the connection profile, save, then restart
DAQuester. It does not change Excalibur's settings or processes.

For evidence export or local automation, see [Control and MCP](control.md).
Do not publish your Data folder, passwords, login packets or session secrets.

## Operational limits

Only the supported client/profile and six automatic recipes are executable. Recorded live
runs validate specific cases; synthetic tests do not guarantee every server
condition. Gathering depends on actual drops and may continue during dry patrols.
Unexpected dialogs and uncertain transactions pause rather than repeat blindly.
Some banking, cooldown, detour and patrol cases still need live evidence; current
details are in [validation](validation.md) and [provenance](provenance.md).
