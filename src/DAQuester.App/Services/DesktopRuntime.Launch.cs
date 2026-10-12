using System.Diagnostics;
using System.Net;
using DAQuester.Core;
using DAQuester.DarkAges.Launcher;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    readonly List<Process> launchedClients = [];
    // Login typing needs window focus, so overlapping launches take turns at the login form.
    readonly SemaphoreSlim loginGate = new(1, 1);
    public string LaunchState(string name) => launchStates.GetValueOrDefault(name, Snapshot(name).Connected ? "Connected" : "Offline");
    public bool LaunchPending(string name) => pendingLaunches.ContainsKey(LaunchKey(name));
    (string Path, DateTime Written, string? Error) clientCheck;
    static string LaunchKey(string name) => name.Length > 0 ? name : "(manual)";
    // Hashing the client on every presentation refresh is expensive. Launch still validates it again.
    string? ClientBlocker()
    {
        string path = Settings.ClientPath;
        if (path.Length == 0 || !File.Exists(path))
            return "Dark Ages application not found. Set its path in Settings.";
        var written = File.GetLastWriteTimeUtc(path);
        if (clientCheck.Path != path || clientCheck.Written != written)
        {
            string? error = null;
            try
            {
                ClientLauncher.Validate(path);
            }
            catch (InvalidOperationException)
            {
                error = "This Dark Ages application isn't a supported 7.41 client. Choose it in Settings.";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = "The Dark Ages application couldn't be read. Check its path in Settings.";
            }

            clientCheck = (path, written, error);
        }

        return clientCheck.Error;
    }

    bool OwnsLiveClient(string name)
    {
        if (!ownedClients.TryGetValue(LaunchKey(name), out var client))
            return false;
        try
        {
            return !client.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public string? LaunchBlocker(string name)
    {
        if (Preview)
            return null;
        if (closing || shuttingDown)
            return "DAQuester is closing. Wait for progress to finish saving.";
        if (name.Length == 0 && Settings.Characters.Length > 0)
            return "Select a saved character before launching.";
        if (name.Length > 0 && !name.All(char.IsAsciiLetter))
            return "Enter a character name using letters only.";
        if (!Ready)
            return "The game connection isn't ready. Check the connection profile in profiles.json, then restart DAQuester.";
        if (LaunchPending(name))
            return launchStates.GetValueOrDefault(name, "Launching Dark Ages…");
        if (OwnsLiveClient(name))
            return null;
        if (name.Length > 0 && Snapshot(name).Connected)
            return name + " is already connected. Use its game window.";
        return ClientBlocker();
    }

    public string? LaunchHint(string name)
    {
        if (Preview)
            return "Preview cannot launch game clients.";
        if (!OwnsLiveClient(name))
            return null;
        return name.Length > 0 ? name + "'s game window is already open. Launch focuses it." : "Dark Ages is already open. Launch focuses its game window.";
    }

    readonly Dictionary<string, Process> ownedClients = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, CancellationTokenSource> pendingLaunches = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> launchStates = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Task<int>> launchTasks = new(StringComparer.OrdinalIgnoreCase);
    public Task<int> LaunchAsync(string name, bool manualOnly = false)
    {
        if (launchTasks.TryGetValue(name, out var active) && !active.IsCompleted)
            return active;
        var task = LaunchOwnedCharacterAsync(name, manualOnly);
        launchTasks[name] = task;
        return task;
    }

    async Task<int> LaunchOwnedCharacterAsync(string name, bool manualOnly)
    {
        RequireOpen();
        if (Preview)
            throw new InvalidOperationException("Preview cannot launch game clients.");
        if (name.Length > 0 && !name.All(char.IsAsciiLetter))
            throw new InvalidOperationException("Enter a character name using letters only.");
        if (!Ready)
            throw new InvalidOperationException("The game connection isn't ready. Check the connection profile in profiles.json, then restart DAQuester.");
        string key = LaunchKey(name);
        if (pendingLaunches.ContainsKey(key))
            throw new InvalidOperationException("This character's client is already launching.");
        if (ownedClients.TryGetValue(key, out var existing) && !existing.HasExited)
        {
            LoginWindowInput.Focus(existing.Id);
            return existing.Id;
        }

        if (name.Length > 0 && sessions.TryGetValue(name, out var current) && current.Snapshot.Connected)
            throw new InvalidOperationException("This character is already connected. Use its existing game window.");
        using var cancellation = new CancellationTokenSource();
        pendingLaunches[key] = cancellation;
        SavedLoginTicket? ticket = null;
        CancellationTokenRegistration ticketCancellation = default;
        void Progress(string text)
        {
            launchStates[name] = text;
            Log(name + ": " + text);
            Notify();
        }

        var authenticated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            bool saved = !manualOnly && name.Length > 0 && CredentialVault.Exists(name);
            if (saved)
            {
                ticket = new(name, () => CredentialVault.Load(name));
                ticketCancellation = cancellation.Token.Register(ticket.Cancel);
            }

            int port = await host.StartOwnedAsync(new(IPAddress.Parse(Settings.RemoteAddress), Settings.RemotePorts[0]), ticket, c => c.Authenticated += connection => authenticated.TrySetResult(connection.Tracker.Snapshot.Name ?? ""), cancellation.Token);
            Progress("Launching Dark Ages…");
            int pid = await Task.Run(() =>
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return ClientLauncher.Launch(Settings.ClientPath, port);
            }, cancellation.Token);
            var process = Process.GetProcessById(pid);
            launchedClients.Add(process);
            ownedClients[key] = process;
            if (!saved)
            {
                Progress("Client open · sign in manually");
                return pid;
            }

            Progress("Waiting for login screen…");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                process.Refresh();
                if (process.HasExited)
                    throw new InvalidOperationException("Game client closed before login.");
                if (process.MainWindowHandle != IntPtr.Zero)
                    break;
                await Task.Delay(50, timeout.Token);
            }

            if (loginGate.CurrentCount == 0)
                Progress("Waiting for another login to finish…");
            await loginGate.WaitAsync(cancellation.Token);
            try
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(90));
                using var form = new NativeLoginForm(pid);
                await LoginFormAutomation.Run(form, name, ticket!.Ticket, Progress, timeout.Token);
            }
            finally
            {
                loginGate.Release();
            }

            Progress("Login submitted · waiting for character…");
            string actual = await authenticated.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellation.Token);
            if (!actual.Equals(name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The client connected as another character. Check its login before running quests.");
            Progress("Connected · ready for quests");
            return pid;
        }
        catch (OperationCanceledException)
        {
            Progress(cancellation.IsCancellationRequested ? "Launch cancelled · sign in manually" : "Login timed out · sign in manually");
            throw new InvalidOperationException(launchStates[name]);
        }
        catch (TimeoutException)
        {
            Progress("Login not confirmed · check the game window");
            throw new InvalidOperationException(launchStates[name]);
        }
        catch
        {
            Progress("Launch unavailable · check the game window");
            throw;
        }
        finally
        {
            ticket?.Cancel();
            ticketCancellation.Dispose();
            pendingLaunches.Remove(key);
            Notify();
        }
    }

    void CancelLaunches(string? name = null)
    {
        foreach (var pending in pendingLaunches.Where(p => name is null || p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).ToArray())
            pending.Cancel();
    }

    async Task AwaitLaunchesAsync(string? name = null)
    {
        try
        {
            await Task.WhenAll(launchTasks.Where(p => name is null || p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).Where(t => !t.IsCompleted).ToArray());
        }
        catch (Exception)
        {
            Log("Pending character launch stopped.");
        }
    }
}
