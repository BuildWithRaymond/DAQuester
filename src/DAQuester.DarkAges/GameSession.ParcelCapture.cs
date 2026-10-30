using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession : IQuestParcelCaptureSession
{
    const string ParcelCaptureProfile = "datester-7.41";
    const string ParcelCaptureMarker = "local-parcel-capture/v1";

    static Guid ParcelCaptureScope(string character, string profile)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(ParcelCaptureMarker + "|" + profile + "|" + character.ToUpperInvariant())).AsSpan(0, 16));

    static bool CaptureSettled(QuestParcelCheckpoint checkpoint)
        => !checkpoint.Uncertain && (checkpoint.Result?.Outcome is QuestParcelOutcome.Empty or QuestParcelOutcome.InventoryFull
            or QuestParcelOutcome.TooHeavy or QuestParcelOutcome.NotSubmitted
            || checkpoint.Intent.UnitId == "send-one" && checkpoint.Stage == "CaptureSent"
                && checkpoint.Result?.Outcome == QuestParcelOutcome.SourceAccepted
            || checkpoint.Intent.UnitId == "send-menu" && checkpoint.Observations.Length > 0);

    static bool CaptureFinancialPending(QuestParcelCheckpoint checkpoint)
        => checkpoint.Intent.CollectionId == ParcelCaptureMarker && checkpoint.Intent.UnitId is "receive-one" or "send-one"
            && (checkpoint.WriteIntent || checkpoint.Submitted) && !CaptureSettled(checkpoint);

    static bool SameCaptureBody(QuestParcelCaptureRequest request, QuestParcelCheckpoint saved)
        => saved.Intent.CollectionId == ParcelCaptureMarker && saved.Intent.AttemptId == request.OperationId.ToString("D")
            && saved.Intent.Character == request.Character && saved.Intent.ServerProfile == request.ServerProfile
            && (request.Action == "reconcile" || saved.Intent.UnitId == request.Action
                && saved.Before.Generation == request.ExpectedGeneration && saved.Before.Id == request.ExpectedCharacterId
                && saved.NpcId == request.ExpectedNpcId && saved.Before.Dialog?.Revision == request.ExpectedDialogRevision
                && (request.Action != "send-one" || saved.SelectedSlot is { } slot && request.ExpectedSlot == slot
                    && saved.Before.Inventory.TryGetValue(slot, out var item) && request.ExpectedItem == item.Name
                    && request.ExpectedQuantity == item.Quantity && request.ExpectedGold == saved.Before.Gold));

    static QuestParcelCheckpoint InspectCapture(QuestParcelCheckpoint checkpoint)
    {
        if (CaptureSettled(checkpoint) || !checkpoint.WriteIntent || !checkpoint.Submitted || checkpoint.Interference
            || checkpoint.Observations.Length == 0) return checkpoint;
        var before = checkpoint.Before;
        var latest = checkpoint.Observations[^1].State;
        bool identity = latest.Generation == before.Generation && latest.Character == before.Character && latest.Id == before.Id
            && latest.Map == before.Map && latest.Position == before.Position;
        if (checkpoint.Intent.UnitId == "send-one")
        {
            if (!identity || CapturePaidAcknowledgement(checkpoint) is not { } paid
                || checkpoint.SelectedSlot is not { } slot || !before.Inventory.TryGetValue(slot, out var selected)
                || selected.Quantity == 0 || selected.Name != checkpoint.Intent.Item
                || before.Gold is not { } goldBefore || latest.Gold is not { } goldAfter || goldAfter > goldBefore
                || paid.FeeGold != goldBefore - goldAfter) return checkpoint;
            bool freshGold = latest.GoldRevision > checkpoint.WriteRevision && latest.GoldRevision <= latest.Revision;
            // Earlier diagnostic journals omitted GoldRevision entirely. Their
            // positive, exact paid NPC receipt plus the matching owned gold and
            // whole-bag debit still proves a source send. Zero-cost sends and
            // any journal carrying modern gold metadata require fresh status.
            bool legacyPaid = before.GoldRevision == 0 && checkpoint.Observations.All(row => row.State.GoldRevision == 0)
                && paid.FeeGold > 0;
            if (!freshGold && !legacyPaid) return checkpoint;
            var expected = before.Inventory.ToBuilder();
            if (selected.Quantity == 1) expected.Remove(slot); else expected[slot] = selected with { Quantity = selected.Quantity - 1 };
            if (!BagSortPlanner.Matches(expected, latest.Inventory)) return checkpoint;
            return checkpoint with { Stage = "CaptureSent", Result = paid with { Item = selected.Name, Quantity = 1 }, Uncertain = false, Failure = null };
        }
        bool unchanged = identity && latest.Gold == before.Gold && BagSortPlanner.Matches(before.Inventory, latest.Inventory);
        if (!unchanged) return checkpoint;
        if (checkpoint.Intent.UnitId == "send-menu")
        {
            if (checkpoint.Observations.Any(observation => observation.State.Dialog is { } dialog
                && dialog.Generation == before.Generation && dialog.EntityId == checkpoint.NpcId && dialog.Name == checkpoint.NpcName
                && dialog.Revision > checkpoint.WriteRevision))
                return checkpoint with { Uncertain = false, Failure = null };
            return checkpoint;
        }
        if (checkpoint.Intent.UnitId != "receive-one") return checkpoint;
        foreach (var observation in checkpoint.Observations)
        {
            if (observation.ActiveStatus is not { Type: 3 } notice || notice.Generation != before.Generation
                || notice.Revision <= checkpoint.WriteRevision) continue;
            QuestParcelOutcome? outcome = notice.Text switch
            {
                "Nothing here for you, I'm afraid." => QuestParcelOutcome.Empty,
                "Your inventory is full." => QuestParcelOutcome.InventoryFull,
                var text when text.StartsWith("Whoa there, you don't have enough room ", StringComparison.Ordinal) => QuestParcelOutcome.TooHeavy,
                _ => null
            };
            if (outcome is { } terminal)
                return checkpoint with { Result = new(terminal, QuestParcelReplySource.ActiveStatus, notice.Revision),
                    Uncertain = false, Failure = null };
        }
        return checkpoint;
    }

    static QuestParcelReply? CapturePaidAcknowledgement(QuestParcelCheckpoint checkpoint)
    {
        if (checkpoint.Intent.CollectionId != ParcelCaptureMarker || checkpoint.Intent.ServerProfile != ParcelCaptureProfile
            || checkpoint.Intent.Direction != QuestParcelDirection.Send || checkpoint.Intent.UnitId != "send-one"
            || checkpoint.Intent.Quantity != 1 || checkpoint.Stage != "CaptureSendOne") return null;
        const string prefix = "You've given ", suffix = " coins. It will be done.";
        QuestParcelReply? paid = null;
        foreach (var observation in checkpoint.Observations)
        {
            var state = observation.State;
            if (state.Character != checkpoint.Before.Character || state.Id != checkpoint.Before.Id || state.Generation != checkpoint.Before.Generation
                || state.Map != checkpoint.Before.Map || state.Position != checkpoint.Before.Position
                || state.Dialog is not { EntityType: 1, IsPursuit: false } dialog || dialog.EntityId != checkpoint.NpcId || dialog.Name != checkpoint.NpcName
                || dialog.Generation != checkpoint.Before.Generation || dialog.Revision <= checkpoint.WriteRevision || dialog.Revision > state.Revision
                || dialog.Text.Length <= prefix.Length + suffix.Length
                || !dialog.Text.StartsWith(prefix, StringComparison.Ordinal) || !dialog.Text.EndsWith(suffix, StringComparison.Ordinal)) continue;
            string amount = dialog.Text[prefix.Length..^suffix.Length];
            if (amount.Length == 0 || amount.Any(value => value is < '0' or > '9')
                || !uint.TryParse(amount, NumberStyles.None, CultureInfo.InvariantCulture, out uint fee)) continue;
            if (paid is not null && paid.FeeGold != fee) return null;
            paid = new(QuestParcelOutcome.SourceAccepted, QuestParcelReplySource.NpcDialog, dialog.Revision, FeeGold: fee);
        }
        return paid;
    }

    public async Task<QuestParcelCaptureResult> CaptureParcelAsync(QuestParcelCaptureRequest request,
        IQuestParcelJournal journal, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId == Guid.Empty || request.Action is not ("receive-one" or "send-menu" or "send-one" or "reconcile")
            || request.ServerProfile != ParcelCaptureProfile || maps.ServerProfile != request.ServerProfile)
            throw new QuestBlockedException("Parcel capture needs its exact supported profile, stable operation identity and bounded action.");
        if (request.Action is not ("send-one" or "reconcile") && (request.ExpectedSlot is not null || request.ExpectedItem is not null
            || request.ExpectedQuantity is not null || request.ExpectedGold is not null))
            throw new QuestBlockedException("Item and gold capture authority is supported only for send-one.");
        var authority = new ParcelAuthority(Snapshot, connection.ManualOperationRevision, connection.ManualBagOperationRevision);
        void Own()
        {
            cancellation.ThrowIfCancellationRequested();
            var current = Snapshot;
            if (!current.Connected || !current.InventoryKnown || current.Name != request.Character
                || current.Generation != request.ExpectedGeneration || current.Id != request.ExpectedCharacterId
                || current.Id is null || current.Id == 0 || current.Exchange is not null
                || InputStamp != new QuestParcelInputStamp(authority.Manual, authority.BagInput))
                throw new QuestBlockedException("The parcel capture character, connection or manual input changed.");
        }
        Own();
        using var conflict = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, conflict.Token);
        void InterruptedCapture(string _) { try { conflict.Cancel(); } catch (ObjectDisposedException) { } }
        Interrupted += InterruptedCapture;
        bool locked = false;
        try
        {
            await actions.WaitAsync(linked.Token);
            locked = true;
            Own();
            var scope = ParcelCaptureScope(request.Character, request.ServerProfile);
            await using var lease = await journal.AcquireAsync(scope, linked.Token);
            var captures = await journal.LoadAsync(scope, linked.Token);
            Own();
            var saved = captures.SingleOrDefault(checkpoint => checkpoint.Intent.AttemptId == request.OperationId.ToString("D"));
            if (saved is not null)
            {
                if (!SameCaptureBody(request, saved))
                    throw new QuestBlockedException("The saved parcel capture operation cannot be reused with a different body.");
                // The original action no longer owns the character. Later bot
                // work does not change manual epochs, so no new snapshot can
                // safely extend this capture after its owned observation window.
                saved = InspectCapture(saved);
                await journal.SaveAsync(saved, linked.Token);
                return new(request.OperationId, saved.Intent.UnitId, true, saved);
            }
            if (request.Action == "reconcile")
                throw new QuestBlockedException("No saved parcel capture exists for that operation; reconciliation never submits a request.");
            if (captures.Any(CaptureFinancialPending))
                throw new QuestBlockedException("An unresolved financial capture owns this character. Reconcile its original operation before a new capture.");
            var before = Snapshot;
            Item? selected = null;
            if (request.Action == "send-one")
            {
                if (request.ExpectedSlot is not (>= 1 and <= 59) || string.IsNullOrWhiteSpace(request.ExpectedItem)
                    || request.ExpectedQuantity is not > 0 || request.ExpectedGold is null
                    || !before.Inventory.TryGetValue(request.ExpectedSlot.Value, out selected)
                    || selected.Name != request.ExpectedItem || selected.Quantity != request.ExpectedQuantity || before.Gold != request.ExpectedGold)
                    throw new QuestBlockedException("Send-one capture requires the exact observed item slot, name, stack quantity and gold.");
            }
            if (request.ExpectedNpcId is not { } npcId || request.ExpectedDialogRevision is not { } revision
                || before.Dialog is not { IsPursuit: false, EntityType: 1 } menu || menu.Generation != before.Generation
                || menu.EntityId != npcId || menu.Revision != revision || before.Map is not { } map || before.Position is not { } position)
                throw new QuestBlockedException("Parcel capture needs the exact current observed NPC menu and dialog revision.");
            var intent = new QuestParcelIntent(request.OperationId.ToString("D"), scope, request.Action,
                request.Action == "receive-one" ? QuestParcelDirection.Receive : QuestParcelDirection.Send,
                request.Character, request.ServerProfile, request.ExpectedGeneration, selected?.Name) { CollectionId = ParcelCaptureMarker };
            var route = new QuestParcelRoute(map, menu.Name, position);
            RequireParcelOwner(intent, authority, route, npcId, linked.Token);
            var checkpoint = new QuestParcelCheckpoint(intent, QuestParcelState.From(before), npcId, menu.Name)
            {
                Stage = request.Action switch { "receive-one" => "CaptureReceive", "send-one" => "CaptureSendOne", _ => "CaptureSendMenu" },
                SelectedSlot = selected?.Slot,
                InputStamp = InputStamp, Uncertain = false
            };
            using var pulse = new SemaphoreSlim(0);
            var observed = new ConcurrentQueue<QuestParcelObservation>();
            bool wireStarted = false, interference = false;
            void WakeCapture()
            {
                // An event invocation may already have copied its subscribers
                // when cleanup unsubscribes this completed capture.
                try { pulse.Release(); }
                catch (ObjectDisposedException) { }
            }
            void ObserveCapture(CharacterSnapshot state) { observed.Enqueue(QuestParcelObservation.From(state)); WakeCapture(); }
            void ManualCapture(byte opcode, byte[] payload, bool bot)
            {
                if (!bot && InputStamp != checkpoint.InputStamp) { interference = true; WakeCapture(); }
            }
            async Task Save(CancellationToken token)
            {
                var observations = checkpoint.Observations.ToList();
                while (observed.TryDequeue(out var observation)) observations.Add(observation);
                checkpoint = checkpoint with { Observations = observations.ToArray(), Interference = checkpoint.Interference || interference };
                await journal.SaveAsync(checkpoint, token);
            }
            void Validate()
            {
                RequireParcelOwner(intent, authority, route, npcId, linked.Token);
                var current = Snapshot;
                if (current.Revision != before.Revision || current.Dialog != menu || current.Gold != before.Gold
                    || !BagSortPlanner.Matches(current.Inventory, before.Inventory))
                    throw new QuestBlockedException("The bound parcel capture menu or resources changed before its request.");
            }
            Changed += ObserveCapture;
            connection.ClientObserved += ManualCapture;
            try
            {
                await Save(linked.Token);
                Validate();
                checkpoint = checkpoint with { WriteIntent = true, WriteRevision = before.Revision, Uncertain = true,
                    WrittenStages = [checkpoint.Stage] };
                await Save(linked.Token);
                Validate();
                ushort action = request.Action switch { "receive-one" => 0x64, "send-one" => 0x63, _ => 0x60 };
                await connection.SendAsync(ParcelPacket(npcId, action, selected?.Slot),
                    request.ExpectedGeneration, linked.Token, validateSend: Validate, beforeWrite: () => { Validate(); wireStarted = true; });
                checkpoint = checkpoint with { Submitted = true };
                await Save(linked.Token);
                var elapsed = Stopwatch.StartNew();
                while (true)
                {
                    RequireParcelOwner(intent, authority, route, npcId, linked.Token);
                    await Save(linked.Token);
                    checkpoint = InspectCapture(checkpoint);
                    if (CaptureSettled(checkpoint))
                    {
                        await Save(linked.Token);
                        return new(request.OperationId, request.Action, false, checkpoint);
                    }
                    bool resourceChange = checkpoint.Observations.Any(observation => observation.State.Gold != before.Gold
                        || !BagSortPlanner.Matches(before.Inventory, observation.State.Inventory));
                    bool rawReply = checkpoint.Observations.Any(observation => observation.ActiveStatus is { Type: 3 } notice
                        && notice.Generation == before.Generation && notice.Revision > checkpoint.WriteRevision
                        || observation.State.Dialog is { } dialog && dialog.Generation == before.Generation && dialog.EntityId == npcId
                            && dialog.Name == menu.Name && dialog.Revision > checkpoint.WriteRevision);
                    // A paid NPC reply can arrive before its gold/bag packets.
                    // Keep the owned bounded window open for those receipts.
                    bool awaitingPaidReceipts = CapturePaidAcknowledgement(checkpoint) is not null;
                    if (resourceChange && rawReply && !awaitingPaidReceipts || checkpoint.Observations.Length >= 512)
                    {
                        checkpoint = checkpoint with { Uncertain = true, Failure = "Raw parcel observations captured; no arrival or sender is qualified." };
                        await Save(linked.Token);
                        return new(request.OperationId, request.Action, false, checkpoint);
                    }
                    var remaining = TimeSpan.FromSeconds(5) - elapsed.Elapsed;
                    if (remaining <= TimeSpan.Zero || !await pulse.WaitAsync(remaining, linked.Token))
                    {
                        checkpoint = checkpoint with { Uncertain = true, Failure = "Capture receipt timed out; no request was repeated." };
                        await Save(CancellationToken.None);
                        return new(request.OperationId, request.Action, false, checkpoint);
                    }
                }
            }
            catch
            {
                if (checkpoint.Stage == "CaptureSent" && CaptureSettled(checkpoint))
                {
                    // Paid source proof is irreversible. Persist that frozen
                    // proof even if its final save acknowledgement was lost or
                    // cancelled; later events must not demote or extend it.
                    await journal.SaveAsync(checkpoint, CancellationToken.None);
                    return new(request.OperationId, request.Action, false, checkpoint);
                }
                checkpoint = checkpoint with
                {
                    WriteIntent = wireStarted, Submitted = wireStarted && checkpoint.Submitted, Uncertain = wireStarted,
                    Result = wireStarted ? null : new(QuestParcelOutcome.NotSubmitted, QuestParcelReplySource.ActiveStatus, Snapshot.Revision),
                    Failure = wireStarted ? "Capture interrupted after its request; reconcile observations without repeating it."
                        : "Capture stopped before its request; no packet was submitted."
                };
                await Save(CancellationToken.None);
                throw;
            }
            finally { Changed -= ObserveCapture; connection.ClientObserved -= ManualCapture; }
        }
        finally
        {
            Interrupted -= InterruptedCapture;
            if (locked) actions.Release();
        }
    }
}
