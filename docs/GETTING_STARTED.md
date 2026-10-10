# Getting started

## Install

Download the installer or portable ZIP from
[Releases](https://github.com/BuildWithRaymond/DAQuester/releases/latest).
Use the installer for a Start menu shortcut, or extract the portable ZIP and open
its `DAQuester.exe`. Keep the complete portable folder together. .NET is included.

DAQuester supports the exact DATester 7.41 client hash in
[provenance](provenance.md). It checks the executable before launching and rejects
unsupported builds. No game binaries or assets are included.

## Set up and play

1. Open **Settings**. Select `Darkages.exe`, enter one character name per line,
   and save. Restart DAQuester after changing the client or roster.
2. Choose **Open Dark Ages**. Log in manually. Only clients opened through
   DAQuester connect through its proxy.
3. Select the character and quest. Read the requirements, then **Start quest**.
   Mundane Birth asks for its lasting choices before any movement.
4. **Pause** retains progress. **Resume** continues saved work. **Stop** cancels
   automation and retains the journal. Manual movement/NPC interaction pauses
   automation while gameplay continues.

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
The preference is saved immediately. Startup checks only report newer stable
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

Only the supported client/profile and four recipes are executable. Recorded live
runs validate specific cases; synthetic tests do not guarantee every server
condition. Gathering depends on actual drops and may continue during dry patrols.
Unexpected dialogs and uncertain transactions pause rather than repeat blindly.
Some banking, cooldown, detour and patrol cases still need live evidence; current
details are in [validation](validation.md) and [provenance](provenance.md).
