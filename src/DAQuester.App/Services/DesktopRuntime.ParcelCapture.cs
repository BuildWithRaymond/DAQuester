using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Control;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    public async Task<QuestParcelCaptureResult> CaptureParcelAsync(QuestParcelCaptureRequest request, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireOpen();
        if (Preview)
            throw new InvalidOperationException("Preview cannot execute parcel capture commands.");
        cancellation.ThrowIfCancellationRequested();
        string name = request.Character;
        if (request.OperationId == Guid.Empty || request.Action is not ("receive-one" or "send-menu" or "send-one" or "reconcile"))
            throw new QuestBlockedException("A stable parcel capture identity and supported action are required.");
        if (request.ServerProfile != "datester-7.41" || Settings.ServerProfile != request.ServerProfile)
            throw new QuestBlockedException("Parcel capture requires the current datester-7.41 connection profile.");
        if (string.IsNullOrWhiteSpace(name) || !Settings.Characters.Contains(name, StringComparer.OrdinalIgnoreCase))
            throw new QuestBlockedException("Parcel capture requires a saved character.");
        if (SchedulerReserves(name) || QueueReserves(name) || QueueBusy(name)
            || IsRunning(name) || RunnerBusy(name) || controlActions.ContainsKey(name)
            || LaunchPending(name) || calibrationTasks.TryGetValue(name, out var calibration) && !calibration.IsCompleted)
            throw new QuestBlockedException("Another action owns this character. Parcel capture requires an idle character.");
        if (!sessions.TryGetValue(name, out var session))
            throw new QuestBlockedException("A connected character is required for parcel capture.");
        var before = session.Snapshot;
        if (!before.Connected || !before.InventoryKnown || before.Id is null
            || !string.Equals(before.Name, name, StringComparison.OrdinalIgnoreCase)
            || request.ExpectedGeneration <= 0 || request.ExpectedCharacterId == 0
            || before.Generation != request.ExpectedGeneration || before.Id != request.ExpectedCharacterId
            || before.Exchange is not null)
            throw new QuestBlockedException("The parcel capture character, connection or inventory is not current and confirmed.");
        if (request.Action != "reconcile" &&
            (request.ExpectedNpcId is not > 0 || request.ExpectedDialogRevision is not > 0
             || before.Dialog is not { IsPursuit: false, EntityType: 1 } dialog
             || dialog.Generation != before.Generation || dialog.Revision <= 0 || dialog.Revision > before.Revision
             || dialog.EntityId != request.ExpectedNpcId || dialog.Revision != request.ExpectedDialogRevision))
            throw new QuestBlockedException("Parcel capture requires the exact fresh nonpursuit NPC menu.");
        if (request.Action == "send-one")
        {
            if (request.ExpectedSlot is not (>= 1 and <= 59) || string.IsNullOrWhiteSpace(request.ExpectedItem)
                || request.ExpectedQuantity is not > 0 || request.ExpectedGold is null
                || !before.Inventory.TryGetValue(request.ExpectedSlot.Value, out var item)
                || item.Name != request.ExpectedItem || item.Quantity != request.ExpectedQuantity || before.Gold != request.ExpectedGold)
                throw new QuestBlockedException("Send-one capture requires the exact observed item slot, name, stack quantity and gold.");
        }
        else if (request.Action != "reconcile" && (request.ExpectedSlot is not null || request.ExpectedItem is not null
            || request.ExpectedQuantity is not null || request.ExpectedGold is not null))
            throw new QuestBlockedException("Item and gold capture authority is supported only for send-one.");

        // The capture owns the entire native action and its durable observation
        // checkpoint. Existing Stop/shutdown cancellation drains this same lease.
        using var owner = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        controlActions[name] = owner;
        void Interrupt(string _)
        {
            try { owner.Cancel(); }
            catch (ObjectDisposedException) { }
        }
        session.Interrupted += Interrupt;
        try
        {
            Notify();
            var captures = new FileQuestParcelJournal(Path.Combine(AppSettings.DataPath, "parcel-captures"));
            return await ((IQuestParcelCaptureSession)session).CaptureParcelAsync(request, captures, owner.Token);
        }
        finally
        {
            session.Interrupted -= Interrupt;
            if (controlActions.GetValueOrDefault(name) == owner)
                controlActions.Remove(name);
            Notify();
        }
    }

    static QuestParcelCaptureRequest ParseParcelCapture(ControlRequest request)
    {
        try
        {
            if (request.Arguments is not { ValueKind: JsonValueKind.Object } arguments || string.IsNullOrWhiteSpace(request.Character))
                throw new InvalidDataException("Parcel capture requires a character and bounded arguments.");
            string[] allowed = ["character", "operationId", "action", "serverProfile", "expectedGeneration", "expectedCharacterId", "expectedNpcId", "expectedDialogRevision",
                "expectedSlot", "expectedItem", "expectedQuantity", "expectedGold"];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in arguments.EnumerateObject())
                if (!allowed.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
                    throw new InvalidDataException("Unknown or duplicate parcel capture argument.");
            if (arguments.TryGetProperty("character", out var character)
                && !string.Equals(character.GetString(), request.Character, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The parcel capture character does not match the control request.");
            if (!Guid.TryParseExact(arguments.GetProperty("operationId").GetString(), "D", out var operation))
                throw new InvalidDataException("Parcel capture requires a stable UUID operationId.");
            return new(operation, arguments.GetProperty("action").GetString() ?? "", request.Character,
                arguments.GetProperty("serverProfile").GetString() ?? "", arguments.GetProperty("expectedGeneration").GetInt64(),
                arguments.GetProperty("expectedCharacterId").GetUInt32(),
                arguments.TryGetProperty("expectedNpcId", out var npc) ? npc.GetUInt32() : null,
                arguments.TryGetProperty("expectedDialogRevision", out var revision) ? revision.GetInt64() : null)
            {
                ExpectedSlot = arguments.TryGetProperty("expectedSlot", out var slot) ? slot.GetByte() : null,
                ExpectedItem = arguments.TryGetProperty("expectedItem", out var item) ? item.GetString() : null,
                ExpectedQuantity = arguments.TryGetProperty("expectedQuantity", out var quantity) ? quantity.GetUInt32() : null,
                ExpectedGold = arguments.TryGetProperty("expectedGold", out var gold) ? gold.GetUInt32() : null
            };
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("Parcel capture arguments are missing or malformed.", error);
        }
    }
}
