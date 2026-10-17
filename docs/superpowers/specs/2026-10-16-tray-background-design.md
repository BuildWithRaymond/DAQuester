# DAQuester tray application

Approved direction: retain the native desktop application and its existing owned
runtime. Do not add a Windows service or claim support while Windows is signed out.

The application owns runtime startup, scheduler ticks and shutdown independently
of the main window. A tray icon remains available while the window is hidden.
Keep running in the tray defaults on; Windows sign-in startup defaults off and
does not enable schedules. Closing the window hides it when tray mode is enabled
and supported. Explicit Exit, update installation and OS shutdown save and stop
through the existing runtime shutdown path. Failed shutdown restores access.

The tray shows scheduler state, current work, next due time and attention status.
Its commands open the window, pause scheduling and exit. Pause does not silently
rearm anything. An ordinary second launch activates the existing window through
a bounded request on the existing current-user local control pipe. Background
startup does not raise an already running window or start another runtime.

Windows startup is an explicit current-user Run registration targeting the stable
packaged launcher with --background. Development and preview builds never change
real startup registration. Portable users must retain the package location.
Preferences are saved together with rollback if persistence fails.

Use Avalonia's tray icon/native-menu adapter and the shared dark resources. Hidden
startup must initialize listeners and timers without a Window.Opened event.
Explicit dialogs restore the main window before opening. Installation retains
existing safety gates. Preserve all character ownership, login blocks, pending
transactions, generation checks and timed continuations.

Verification covers hidden startup/ticks, close versus exit, concurrent exits,
startup/exit races, failed shutdown, activation, settings rollback,
and stable launcher selection. All fixtures use isolated data and fake startup
registration. Run focused checks and both full xUnit projects, review native UI
at normal/minimum sizes, and verify release packaging. Do not operate real game
characters or change Excalibur processes, player startup settings or schedules.
