# DAQuester for Windows

DAQuester helps you plan and run quests for your Dark Ages characters.
See RELEASE_NOTES.md beside this file for changes in this version.

## First launch

1. Open DAQuester.exe. Keep all files in the portable folder together; the .NET
   runtime is included.
2. In Settings, select your supported DATester 7.41 Darkages.exe, then save.
   DAWindower X is included and loads automatically for clients opened with
   DAQuester. Keep `DAWindowerX.dll` beside the application executable.
   Restart DAQuester after changing the client path. The game is not included.
3. In Characters, add your character names and optional passwords, then choose
   Save accounts. Saved passwords are encrypted by Windows. Without a password,
   you can sign in manually.
4. Use Launch beside the character. Only clients launched through DAQuester
   connect to it; already-open game clients are not attached.
   X's + button opens another blank login through DAQuester. Sign in manually;
   the new client is tracked by DAQuester and can run quests after connecting.
5. Choose the character in the planner. Check eligible quests, arrange their order,
   and choose Run queue with the play icon. Some quests ask for choices first.

Use the fold arrow to make the journey and queue smaller. Hover over quest rows
or checkboxes to read their requirements and selection rules. Pause keeps progress;
Stop ends automation and allows queue edits. Unknown outcomes remain paused until
the game or matching recorded evidence confirms them.

## Your saved data and updates

Closing the window keeps DAQuester running in the system tray by default. Click
the tray icon to reopen it, or right-click for scheduler status, Pause scheduling
and Exit DAQuester. Exit saves progress and stops automation. Settings can turn
off tray mode and optionally start DAQuester when you sign in to Windows. Save
settings to apply these choices; startup never enables scheduling. Keep the PC
awake and Windows signed in. Portable startup requires keeping the folder in
place. Uninstall removes this installation's startup entry.

Settings, characters, encrypted passwords, progress, queues and history live in
%LOCALAPPDATA%\DAQuester\Data, outside this package. Keep a backup before moving
computers or recovering an older installation. Passwords belong to your Windows
account and cannot simply be moved to another account.

In Settings, click Update and restart once. DAQuester checks, downloads, waits for
active work and game clients to finish, saves progress, then installs and
relaunches. It never closes game clients to make an update possible. Startup
checks are optional, off by default, and check-only. Packages are unsigned; SHA256SUMS.txt in
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
