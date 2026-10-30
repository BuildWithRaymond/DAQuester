using System.Text.Json;
using System.Text.Json.Serialization;
using DAQuester.Core;
using DAQuester.DarkAges;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    readonly Dictionary<string, QuestSupplyIntegration> supplyIntegrations = new(StringComparer.OrdinalIgnoreCase);
    readonly List<AislingExchangeClient> supplyClients = [];
    readonly Lazy<AislingExchangePairingStore> supplyPairingStore = new(() => new(AppSettings.DataPath));
    sealed record SupplyActionOwner(GameSession Session, long Generation, uint? Id, CancellationTokenSource Cancellation);
    readonly Dictionary<string, SupplyActionOwner> supplyActionOwners = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, HashSet<object>> supplyReturnDispatches = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, HashSet<object>> supplyPlanMutations = new(StringComparer.OrdinalIgnoreCase);
    sealed class SupplyLease(Action release) : IDisposable
    {
        Action? releaseAction = release;
        public void Dispose() => Interlocked.Exchange(ref releaseAction, null)?.Invoke();
    }
    public Func<QuestSupplyOffer, CancellationToken, Task<bool?>>? ChooseSuppliesAsync { get; set; }

    sealed record SupplyConnection(string ApiBase);

    sealed class UnavailableSupplyProvider : IQuestSupplyProvider
    {
        public bool Enabled => false;
        public Task<QuestSupplyQuote?> QuoteAsync(QuestSupplyDemand demand, CancellationToken cancellation) => Task.FromResult<QuestSupplyQuote?>(null);
        public Task<QuestSupplyRequestStatus> SubmitAsync(QuestSupplySubmission submission, CancellationToken cancellation)
            => throw new QuestBlockedException("AislingExchange supplies are unavailable.");
        public Task<QuestSupplyRequestStatus?> FindAsync(Guid clientOperationId, CancellationToken cancellation)
            => throw new QuestBlockedException("AislingExchange supplies await service reconciliation; saved requests remain intact.");
    }

    sealed class SupplyConsent(DesktopRuntime runtime, bool scheduled) : IQuestSupplyConsent
    {
        public Task<bool?> AcceptAsync(QuestSupplyOffer offer, CancellationToken cancellation)
            => scheduled || runtime.ChooseSuppliesAsync is null ? Task.FromResult<bool?>(null)
                : runtime.ChooseSuppliesAsync(offer, cancellation);
    }

    QuestSupplyIntegration CreateSupplyIntegration(string name, IGameSession session, QuestJob job, bool scheduled)
    {
        var store = new FileAislingExchangeSupplyStore(Path.Combine(AppSettings.DataPath, "aisling-exchange", "journeys"));
        Guid? id = QueueReserves(name) ? Queue(name).SupplyJourneyId : job.SupplyJourneyId;
        IQuestSupplyProvider provider = new UnavailableSupplyProvider();
        string path = Path.Combine(AppSettings.DataPath, "aisling-exchange", "provider.json");
        if (!Preview && File.Exists(path))
        {
            try
            {
                if (new FileInfo(path).Length > 4096) throw new JsonException();
                var configuration = JsonSerializer.Deserialize<SupplyConnection>(File.ReadAllText(path), new JsonSerializerOptions
                {
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
                }) ?? throw new JsonException();
                var routes = AislingExchangeApiRoutes.FromApiBase(new Uri(configuration.ApiBase, UriKind.Absolute));
                var credentials = new AislingExchangeCredentialStore(AppSettings.DataPath);
                var client = new AislingExchangeClient(routes, credentials);
                var pairing = new AislingExchangePairingCoordinator(client, credentials, supplyPairingStore.Value);
                var pairingSession = new AislingExchangePairingSession(session, name, Settings.ServerProfile,
                    () => SupplyOwnsSession(name, session));
                async Task EnsurePaired(CancellationToken cancellation)
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    deadline.CancelAfter(TimeSpan.FromSeconds(30));
                    try
                    {
                        while (true)
                        {
                            var result = await pairing.EnsurePairedAsync(pairingSession, deadline.Token);
                            if (result.Paired) return;
                            if (result.State is "AwaitingChallengeReconciliation" or "AwaitingCompletionReconciliation" or "SessionChanged") break;
                            await Task.Delay(500, deadline.Token);
                        }
                    }
                    catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { }
                    throw new QuestBlockedException("AislingExchange connection is being confirmed. Resume after character pairing completes.");
                }
                provider = new AislingExchangeQuestSupplyProvider(client, name, Settings.ServerProfile,
                    () => Settings.AislingExchangeSupplies, EnsurePaired);
                supplyClients.Add(client);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or UriFormatException or AislingExchangeException)
            {
                // Never include file contents, endpoint strings or raw upstream failures.
                Log("AislingExchange service configuration is unavailable; normal acquisition remains available before any submission.");
            }
        }
        var integration = new QuestSupplyIntegration(provider, new SupplyConsent(this, scheduled), store,
            new QuestSupplyParcelOperations(new FileQuestParcelJournal(Path.Combine(AppSettings.DataPath,
                "aisling-exchange", "parcels")), UnqualifiedQuestParcelReceiptPolicy.Instance), id)
        {
            ReturnReserves = () => SupplyReturnReserves(name),
            ReturnDispatchLease = () => AcquireSupplyReturnDispatch(name, session)
        };
        supplyIntegrations[name] = integration;
        return integration;
    }

    async Task CompleteSupplyJourneyAsync(string name, CancellationToken cancellation)
    {
        if (!supplyIntegrations.TryGetValue(name, out var integration)) return;
        RequireOpen();
        if (Preview) throw new QuestBlockedException("Preview never sends or receives parcels.");
        if (!sessions.TryGetValue(name, out var session) || !session.Snapshot.Connected || !session.Snapshot.InventoryKnown ||
            session.Snapshot.Id is null || session.Snapshot.Generation <= 0 || session.Snapshot.Gold is null ||
            !string.Equals(session.Snapshot.Name, name, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("AislingExchange returns need a fresh own-character connection.");
        if (controlActions.ContainsKey(name) || IsRunning(name) || RunnerBusy(name) || QueueBusy(name) ||
            Queue(name).ActiveQuest is not null || LaunchPending(name) ||
            calibrationTasks.TryGetValue(name, out var capture) && !capture.IsCompleted)
            throw new QuestBlockedException("AislingExchange returns wait until this character's current action finishes.");
        using var owner = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var lease = new SupplyActionOwner(session, session.Snapshot.Generation, session.Snapshot.Id, owner);
        // Claim synchronously so Stop, exit, pairing and competing actions see
        // the same exact owner throughout checkpointing and parcel cleanup.
        controlActions.Add(name, owner);
        supplyActionOwners.Add(name, lease);
        void Interrupted(string _) { try { owner.Cancel(); } catch (ObjectDisposedException) { } }
        session.Interrupted += Interrupted;
        Notify();
        try
        {
            owner.Token.ThrowIfCancellationRequested();
            var reserves = SupplyReturnReserves(name);
            await integration.EndJourneyAsync(session.Snapshot, reserves.Items, owner.Token);
            owner.Token.ThrowIfCancellationRequested();
            if (!SupplyOwnsSession(name, session))
                throw new QuestBlockedException("AislingExchange return connection or ownership changed; saved returns remain intact.");
            reserves = SupplyReturnReserves(name);
            await integration.ProcessReturnsAsync(session, reserves.Items, reserves.Gold, owner.Token);
        }
        finally
        {
            session.Interrupted -= Interrupted;
            if (supplyActionOwners.GetValueOrDefault(name) == lease) supplyActionOwners.Remove(name);
            if (controlActions.GetValueOrDefault(name) == owner) controlActions.Remove(name);
            Notify();
        }
    }

    bool SupplyOwnsSession(string name, IGameSession session)
    {
        if (sessions.GetValueOrDefault(name) != session || !session.Snapshot.Connected ||
            !string.Equals(session.Snapshot.Name, name, StringComparison.OrdinalIgnoreCase)) return false;
        if (!controlActions.TryGetValue(name, out var owner))
            return IsRunning(name) || QueueRunning(name) || SchedulerReserves(name);
        return supplyActionOwners.TryGetValue(name, out var lease) && lease.Session == session &&
            lease.Cancellation == owner && !owner.IsCancellationRequested && lease.Generation == session.Snapshot.Generation &&
            lease.Id == session.Snapshot.Id;
    }

    IDisposable AcquireSupplyReturnDispatch(string name, IGameSession session)
    {
        RequireOpen();
        if (Preview || !SupplyOwnsSession(name, session))
            throw new QuestBlockedException("AislingExchange return dispatch needs this character's existing action owner.");
        if (queueSaving.Contains(name) || supplyPlanMutations.ContainsKey(name))
            throw new QuestBlockedException("AislingExchange returns wait for this character's plan changes to finish saving.");
        return AcquireSupplyToken(supplyReturnDispatches, name);
    }

    void RequireSupplyPlanEditable(string name)
    {
        if (supplyReturnDispatches.ContainsKey(name) || supplyActionOwners.TryGetValue(name, out var owner) &&
            controlActions.GetValueOrDefault(name) == owner.Cancellation)
            throw new QuestBlockedException("Wait for this character's AislingExchange parcel return to finish before changing its plans.");
    }

    IDisposable AcquireSupplyPlanMutation(string name)
    {
        RequireOpen();
        RequireSupplyPlanEditable(name);
        return AcquireSupplyToken(supplyPlanMutations, name);
    }

    static IDisposable AcquireSupplyToken(Dictionary<string, HashSet<object>> leases, string name)
    {
        var token = new object();
        if (!leases.TryGetValue(name, out var tokens)) leases[name] = tokens = [];
        tokens.Add(token);
        return new SupplyLease(() =>
        {
            if (leases.TryGetValue(name, out var current) && current == tokens && current.Remove(token) && current.Count == 0)
                leases.Remove(name);
        });
    }

    async Task MutateSupplyPlanAsync(string name, Func<Task> mutation)
    {
        using var lease = AcquireSupplyPlanMutation(name);
        await mutation();
    }

    QuestSupplyReturnReserves SupplyReturnReserves(string name)
    {
        var snapshot = Snapshot(name);
        var reservations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        long gold = 0;
        bool protectAll = false;
        var queued = Queue(name).Quests.ToHashSet(StringComparer.Ordinal);
        var schedules = SchedulerState.Entries.Where(entry => entry.Character.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            entry.Profile == Settings.ServerProfile && entry.Connection == SchedulerConnection && entry.Status != "Completed").ToArray();
        if (queued.Any(id => !recipes.ContainsKey(id)) || schedules.Any(entry => !recipes.TryGetValue(entry.Quest, out var recipe) ||
            entry.RecipeVersion != recipe.Version)) protectAll = true;
        foreach (var recipe in recipes.Values)
        {
            var current = runners.GetValueOrDefault(name);
            var saved = current?.Job.Quest == recipe.Id ? current.Job : journal.Load(name, recipe.Id);
            var planned = schedules.Where(entry => entry.Quest == recipe.Id).ToArray();
            bool unresolved = saved.Pending is not null || saved.Route?.Baseline is not null || saved.Route?.CleanupPending == true;
            bool continuation = queued.Contains(recipe.Id) || unresolved || saved.Preparation is not null ||
                saved.FiskWait is { Verified: true } && saved.Step != "complete" || saved.Status == "Scheduled" ||
                saved.Step is not ("preflight" or "complete") || current?.Job.Quest == recipe.Id && current.IsRunning || planned.Length > 0;
            if (!continuation || !unresolved && QuestRunPolicy.Finished(recipe, saved)) continue;
            if (unresolved) protectAll = true;
            bool complete = QueueAttemptOutcome.Evaluate(saved) is not null || QuestRunPolicy.AttemptCompleted(saved);
            bool future = queued.Contains(recipe.Id) || planned.Any(entry => entry.Recurring || !entry.Resume);
            if (complete && !future) continue;
            if (complete) saved = saved with { Step = "preflight", Route = null, Preparation = null, CompletedAt = null, Pending = null };
            try
            {
                var supplies = QuestItemPreparation.Requirements(recipe, saved, Calibration);
                var journey = DanaanTransport.Journey(recipe, saved);
                if (journey.Length == 0 && supplies.Length == 0) protectAll = true;
                int start = saved.Route?.Task ?? 0;
                var stages = journey.Skip(start).Concat(recipe.JourneyVariants.Values
                    .Where(variant => Array.FindIndex(journey, task => task.Id == variant.TaskId) >= start)
                    .SelectMany(variant => variant.Journey)).ToArray();
                var needed = supplies.ToDictionary(supply => supply.Task.Item!, supply => supply.Task.Quantity, StringComparer.OrdinalIgnoreCase);
                foreach (var inputs in stages.SelectMany(stage => stage.Inputs).GroupBy(input => input.Item, StringComparer.OrdinalIgnoreCase))
                    needed[inputs.Key] = Math.Max(needed.GetValueOrDefault(inputs.Key), checked(inputs.Sum(input => input.Count)));
                foreach (var item in needed)
                    reservations[item.Key] = checked(reservations.GetValueOrDefault(item.Key) + item.Value);
                long payments = stages.Where(stage => stage.Kind == "dialog").Sum(stage => (long)stage.Cost);
                long purchases = Math.Max(stages.Where(stage => stage.Kind == "buy").Sum(stage => (long)stage.Cost * stage.Quantity),
                    supplies.Sum(supply => (long)supply.Task.Cost * supply.Task.Quantity));
                gold = checked(gold + Math.Max(saved.Preparation?.GoldReserve ?? 0, payments + purchases));
            }
            catch (Exception error) when (error is QuestBlockedException or OverflowException or ArgumentException) { protectAll = true; }
        }
        if (protectAll)
        {
            foreach (var item in snapshot.Inventory.Values.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                reservations[item.Key] = Math.Max(reservations.GetValueOrDefault(item.Key), checked((int)item.Sum(value => (long)value.Quantity)));
            gold = Math.Max(gold, snapshot.Gold ?? uint.MaxValue);
        }
        return new(reservations, (uint)Math.Min(uint.MaxValue, gold));
    }

    async Task CompleteStandaloneSupplyJourneyAsync(string name, QuestRunner runner, CancellationToken cancellation = default)
    {
        if (QueueAttemptOutcome.Evaluate(runner.Job) is null || runners.GetValueOrDefault(name) != runner) return;
        try { await CompleteSupplyJourneyAsync(name, cancellation); }
        catch (OperationCanceledException) { throw; }
        catch { Log("AislingExchange returns await reconciliation; verified quest completion remains saved."); }
    }

    void DisposeSupplyClients()
    {
        foreach (var client in supplyClients) client.Dispose();
        supplyClients.Clear();
    }

    internal static bool SupplyGameActionPending(AislingExchangeSupplyJourney? journey)
        => journey is not null && (journey.Operations.Any(operation => !operation.Resolved && operation.ReceiptIntent is not null)
            || journey.ReturnOperations.Any(operation => operation.PendingSend is not null));

    bool SupplyGameActionPending(string name)
    {
        var ids = new[] { supplyIntegrations.GetValueOrDefault(name)?.JourneyId, runners.GetValueOrDefault(name)?.Job.SupplyJourneyId,
            Queue(name).SupplyJourneyId }.Where(id => id is not null).Select(id => id!.Value).Distinct();
        var store = new FileAislingExchangeSupplyStore(Path.Combine(AppSettings.DataPath, "aisling-exchange", "journeys"));
        try { return ids.Any(id => SupplyGameActionPending(store.Load(name, id))) ||
            store.PendingReturns(name, Settings.ServerProfile).Any(SupplyGameActionPending); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return true; }
    }

    async Task RunSupplyAwareControlQuestAsync(string name, QuestRunner runner, bool repeat)
    {
        await runner.RunAsync(repeat, startNewAttempt: true);
        await CompleteStandaloneSupplyJourneyAsync(name, runner);
    }
}
