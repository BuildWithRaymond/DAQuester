using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed record HelperRow(string Name, string Login, bool Eligible, string? PickLine);
public sealed record SickChildChoiceInfo(int Recorded, bool QuesterCanPick, uint? Gold, HelperRow[] Helpers);

public sealed partial class DesktopRuntime
{
    readonly Dictionary<string, string> helperStatus = new(StringComparer.OrdinalIgnoreCase);
    public string? HelperStatus(string name) => helperStatus.GetValueOrDefault(name);
    string? HelperBusy(string name) => QueueReserves(name) || IsRunning(name) || RunnerBusy(name) || controlActions.ContainsKey(name) || LaunchPending(name)
     ? "Busy with " + (RunningQuestId(name) is { } quest ? QuestName(quest) : "another action") : null;

    public SickChildChoiceInfo SickChildChoice(string quester, string questId)
    {
        var flower = SickChildEvidence.For(questId) ?? SickChildEvidence.Sevti;
        var job = Job(quester, questId);
        var now = DateTimeOffset.UtcNow;
        var picks = job.SickChildPicks.Concat(FlowerPicks()).ToArray();
        var rows = Settings.Characters.Where(n => !n.Equals(quester, StringComparison.OrdinalIgnoreCase)).Select(n =>
        {
            var current = Snapshot(n);
            bool online = current.Connected, saved = CredentialVault.Exists(n);
            string login = HelperBusy(n) ?? (online ? "Online" : saved ? "Saved login" : "No saved login");
            bool holds = SickChildEvidence.HoldsFlower(job, n) || online && current.InventoryKnown && current.Count(flower.Item) > 0;
            string? pick = holds ? SickChildEvidence.HoldingLine : SickChildEvidence.PickLine(picks, n, now);
            return new HelperRow(n, login, HelperBusy(n) is null && (online || saved) && (holds || pick is null), pick);
        }).ToArray();
        return new(job.SickChildTurnIns.Length, SickChildEvidence.CanPick(picks, quester, now), Snapshot(quester).Gold, rows);
    }

    // One pick a day per character across both flower quests, whichever quester's run made it.
    List<SickChildPick>? flowerPicks;
    string FlowerPicksPath => Path.Combine(AppSettings.DataPath, "flower-picks.json");
    IReadOnlyList<SickChildPick> FlowerPicks()
    {
        if (flowerPicks is null)
        {
            try
            {
                flowerPicks = File.Exists(FlowerPicksPath) ? System.Text.Json.JsonSerializer.Deserialize<List<SickChildPick>>(File.ReadAllText(FlowerPicksPath)) ?? [] : [];
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { Log("Flower pick records could not be read: " + e.Message); flowerPicks = []; }
        }
        return flowerPicks;
    }
    void RecordFlowerPick(SickChildPick pick)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-3);
        flowerPicks = FlowerPicks().Where(p => p.At > cutoff).Append(pick).ToList();
        try
        {
            File.WriteAllText(FlowerPicksPath, System.Text.Json.JsonSerializer.Serialize(flowerPicks));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log("Flower pick could not be saved: " + e.Message); }
    }

    sealed class QuestHelperHub(DesktopRuntime runtime, string quester) : IQuestHelpers
    {
        public void Report(string name, string status) => runtime.Ui(() => { runtime.helperStatus[name] = status; runtime.Log(quester + " · helper " + name + ": " + status); runtime.Notify(); });
        public Task<IQuestHelper> AcquireAsync(string name, CancellationToken cancellation) => runtime.AcquireHelperAsync(quester, name, cancellation);
        public IReadOnlyList<SickChildPick> SharedPicks => runtime.FlowerPicks();
        public void RecordPick(SickChildPick pick) => runtime.Ui(() => runtime.RecordFlowerPick(pick));
    }

    async Task<IQuestHelper> AcquireHelperAsync(string quester, string name, CancellationToken cancellation)
    {
        RequireOpen();
        if (name.Equals(quester, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("The quester can't also be a helper.");
        if (!Settings.Characters.Contains(name, StringComparer.OrdinalIgnoreCase))
            throw new QuestBlockedException(name + " isn't a saved character.");
        if (HelperBusy(name) is { } busy)
            throw new QuestBlockedException(name + ": " + busy + ".");
        bool launched = false;
        if (!sessions.TryGetValue(name, out var session) || !session.Snapshot.Connected)
        {
            if (!CredentialVault.Exists(name))
                throw new QuestBlockedException(name + " has no saved login. Add it on the Characters page.");
            try
            {
                await LaunchAsync(name).WaitAsync(cancellation);
                launched = true;
                // Binding is posted to the UI queue just after authentication.
                for (int wait = 0; !(sessions.TryGetValue(name, out session) && session.Snapshot.Connected); wait++)
                {
                    if (wait >= 200)
                        throw new TimeoutException("logged in, but its connection wasn't ready.");
                    await Task.Delay(100, cancellation);
                }
            }
            catch (Exception e) when (e is InvalidOperationException or TimeoutException && !cancellation.IsCancellationRequested)
            {
                // A stuck login window would only be focused on retry; close it so the retry starts clean.
                CloseOwnedClient(name);
                throw new HelperLoginException(name + ": " + e.Message);
            }
        }
        RequireOpen();
        if (controlActions.ContainsKey(name) || QueueReserves(name) || IsRunning(name) || RunnerBusy(name))
            throw new QuestBlockedException(name + " became busy with another action.");
        var released = new CancellationTokenSource();
        controlActions[name] = released;
        Notify();
        void Interrupt(string reason) => released.Cancel();
        session!.Interrupted += Interrupt;
        var lease = new HelperLease(this, name, session, launched, released, () =>
        {
            session.Interrupted -= Interrupt;
            if (controlActions.GetValueOrDefault(name) == released)
                controlActions.Remove(name);
            released.Dispose();
            Notify();
        });
        try
        {
            if (!session.Snapshot.InventoryKnown)
                await session.WaitAsync(s => s.InventoryKnown, session.Snapshot.Revision - 1, TimeSpan.FromSeconds(20), cancellation);
            return lease;
        }
        catch { await lease.DisposeAsync(); throw; }
    }

    void CloseOwnedClient(string name)
    {
        if (!ownedClients.TryGetValue(LaunchKey(name), out var client))
            return;
        try
        {
            if (!client.HasExited)
            {
                client.Kill();
                Log(name + ": closed the game window after a failed login.");
            }
        }
        catch (InvalidOperationException) { Log(name + ": game window had already closed."); }
    }
    async Task LogOutHelperAsync(string name, IGameSession session, CancellationToken cancellation)
    {
        if (!ownedClients.TryGetValue(LaunchKey(name), out var client))
            throw new QuestBlockedException("DAQuester no longer owns " + name + "'s game window.");
        try
        {
            if (!client.HasExited)
            {
                client.CloseMainWindow();
                using var grace = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                grace.CancelAfter(3000);
                try
                {
                    await client.WaitForExitAsync(grace.Token);
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { client.Kill(); }
            }
        }
        catch (InvalidOperationException) { Log(name + ": game window had already closed."); }
        for (int wait = 0; session.Snapshot.Connected && wait < 100; wait++)
            await Task.Delay(100, cancellation);
        Log(name + ": logged out after helping");
    }

    sealed class HelperLease(DesktopRuntime runtime, string name, IGameSession session, bool launched, CancellationTokenSource released, Action release) : IQuestHelper
    {
        int disposed;
        public string Name => name;
        public IGameSession Session => session;
        public bool LoggedInForQuest => launched;
        public CancellationToken Released { get; } = released.Token;
        public Task LogOutAsync(CancellationToken cancellation) => launched ? runtime.LogOutHelperAsync(name, session, cancellation) : Task.CompletedTask;
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                runtime.Ui(release);
            return ValueTask.CompletedTask;
        }
    }
}
