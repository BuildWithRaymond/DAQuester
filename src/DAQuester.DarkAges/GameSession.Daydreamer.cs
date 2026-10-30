using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession : IDaydreamerSession
{
    Action DaydreamerSendGuard(Dialog page, bool greeting, CancellationToken cancellation)
    {
        var owner = Snapshot;
        long manual = connection.ManualOperationRevision;
        void RequirePage()
        {
            cancellation.ThrowIfCancellationRequested();
            var current = Snapshot;
            if (!current.Connected || current.Id is null || current.Name is null || current.Generation != owner.Generation || current.Id != owner.Id
                || !string.Equals(current.Name, owner.Name, StringComparison.OrdinalIgnoreCase)
                || !DaydreamerEvidence.HasCurrentRegistration(current)
                || current.Dialog != page || !DaydreamerEvidence.IsPage(current, page) || greeting && !DaydreamerEvidence.IsGreeting(page)
                || !current.Entities.TryGetValue(page.EntityId, out var npc) || npc.IsPlayer
                || !npc.Name.Equals(DaydreamerEvidence.Npc, StringComparison.OrdinalIgnoreCase)
                || current.Position is not { } position || Math.Abs(position.X - DaydreamerEvidence.Approach.X) + Math.Abs(position.Y - DaydreamerEvidence.Approach.Y) > 2
                || current.Exchange is { Cancelled: false } || current.Exchange != owner.Exchange || connection.ManualOperationRevision != manual)
                throw new QuestBlockedException("Celesta's check-in changed or another action intervened. Reconcile the saved submission before continuing.");
        }
        return RequirePage;
    }

    public async Task<DaydreamerSubmission> SubmitDaydreamerAsync(Dialog page, CancellationToken cancellation)
    {
        var requirePage = DaydreamerSendGuard(page, greeting: true, cancellation);
        await actions.WaitAsync(cancellation);
        try
        {
            requirePage();
            await SettleMovement(cancellation);
            requirePage();
            var packet = Protocol.DialogPackets.Continue(Snapshot, page);
            DaydreamerSubmission? submitted = null;
            await connection.SendAsync(packet, page.Generation, cancellation, validateSend: requirePage, beforeWrite: () =>
            {
                requirePage();
                submitted = new(page, Snapshot.Revision, DateTimeOffset.UtcNow);
            });
            // The receipt is returned only after the serialized network write completed. A failed write stays uncertain.
            return submitted ?? throw new InvalidOperationException("The check-in was not submitted.");
        }
        finally { actions.Release(); }
    }
}
