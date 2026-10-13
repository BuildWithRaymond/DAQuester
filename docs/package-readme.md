# DAQuester for Windows

DAQuester helps you plan and run quests for your Dark Ages characters.
See RELEASE_NOTES.md beside this file for changes in this version.

## First launch

1. Open DAQuester.exe. Keep all files in the portable folder together; the .NET
   runtime is included.
2. In Settings, select your supported DATester 7.41 Darkages.exe, then save.
   Restart DAQuester after changing the client path. The game is not included.
3. In Characters, add your character names and optional passwords, then choose
   Save accounts. Saved passwords are encrypted by Windows. Without a password,
   you can sign in manually.
4. Use Launch beside the character. Only clients launched through DAQuester
   connect to it; already-open game clients are not attached.
5. Choose the character in the planner. Check eligible quests, arrange their order,
   and choose Run queue with the play icon. Some quests ask for choices first.

Use the fold arrow to make the journey and queue smaller. Hover over quest rows
or checkboxes to read their requirements and selection rules. Pause keeps progress;
Stop ends automation and allows queue edits. Unknown outcomes remain paused until
the game or matching recorded evidence confirms them.

## Your saved data and updates

Settings, characters, encrypted passwords, progress, queues and history live in
%LOCALAPPDATA%\DAQuester\Data, outside this package. Keep a backup before moving
computers or recovering an older installation. Passwords belong to your Windows
account and cannot simply be moved to another account.

In Settings, choose Check for updates, then Download update. After finishing
quests and closing DAQuester-launched clients, choose Install and exit. Startup
checks are optional and off by default. Packages are unsigned; SHA256SUMS.txt in
the release downloads provides checksums.

## Guides and support

- [Setup and existing-data guide](https://github.com/BuildWithRaymond/DAQuester/blob/main/docs/GETTING_STARTED.md)
- [Quest requirements and limits](https://github.com/BuildWithRaymond/DAQuester/blob/main/docs/QUESTS.md)
- [Tested behavior and live-validation limits](https://github.com/BuildWithRaymond/DAQuester/blob/main/docs/validation.md)
- [Downloads](https://github.com/BuildWithRaymond/DAQuester/releases/latest)
- [Report a bug](https://github.com/BuildWithRaymond/DAQuester/issues/new/choose)

DAQuester is an independent community project, released under the MIT license.
Dark Ages belongs to its owners. Game files and artwork are not bundled. See
LICENSE and THIRD_PARTY_NOTICES.md for software credits and licenses.
