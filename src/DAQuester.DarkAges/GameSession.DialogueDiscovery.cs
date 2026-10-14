using System.Collections.Immutable;
using DAQuester.Core;

namespace DAQuester.DarkAges;

public record DiscoveryState(long Generation, long Revision, bool Connected, int? Map, Tile? Position,
    uint? Gold, uint? Experience, int? Weight, bool InventoryKnown,
    ImmutableDictionary<byte, Item> Inventory, ImmutableDictionary<byte, string> Equipment)
{
    public static DiscoveryState From(CharacterSnapshot s) => new(s.Generation, s.Revision, s.Connected,
        s.Map, s.Position, s.Gold, s.Experience, s.Weight, s.InventoryKnown, s.Inventory, s.Equipment);
}
public record DialogueDiscovery(string StopReason, ImmutableArray<Dialog> Pages,
    DiscoveryState Before, DiscoveryState After, string? Error = null);

public sealed partial class GameSession
{
    public DialogueDiscovery? Discovery
    {
        get; private set;
    }
    long submittedDiscoveryRevision = -1;

    void RequireUnusedDiscoveryPage(Dialog? dialog)
    {
        // Repeated pursuit numbers are not stage identities. The current observed
        // revision can authorize one submission, including an offered Next.
        if (dialog is null)
            throw new QuestBlockedException("No current dialog to read.");
        if (submittedDiscoveryRevision == dialog.Revision)
            throw new QuestBlockedException("This discovery page was already submitted. Reconcile the result before continuing.");
    }

    void RecordUnfinishedDiscovery(GameCommand command, CharacterSnapshot before, bool interrupted)
    {
        var pages = new List<Dialog>();
        if (command.Kind is "choose" or "next" or "read-dialog" && before.Dialog is { } initial)
            pages.Add(initial);
        if (Snapshot.Dialog is { } current && (pages.Count == 0 || pages[^1].Revision != current.Revision))
            pages.Add(current);
        Discovery = new(interrupted ? "interrupted" : "reconcile", pages.ToImmutableArray(),
            DiscoveryState.From(before), DiscoveryState.From(Snapshot),
            "The initial action did not finish. Inspect current state before another submission.");
    }

    async Task ReadOfferedPages(GameCommand command, CharacterSnapshot before, CancellationToken ct)
    {
        // Batch only offered informational Next pages. Choices, amount prompts
        // and terminal popups remain explicit actions for their owning caller.
        var pages = new List<Dialog>();
        if (command.Kind is "choose" or "next" && before.Dialog is { } initial)
            pages.Add(initial);
        string reason = "closed";
        string? error = null;
        var first = Snapshot;
        uint? npc = command.Kind is "choose" or "next" ? before.Dialog?.EntityId : first.Dialog?.EntityId;
        int? map = command.Kind is "choose" or "next" or "read-dialog" ? before.Map : first.Map;
        try
        {
            if (command.Kind == "choose")
                await WaitAsync(s => s.Map != before.Map || s.Dialog is null || s.Dialog.Revision > before.Revision,
                    before.Revision, TimeSpan.FromSeconds(5), ct);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var current = Snapshot;
                if (!current.Connected || current.Generation != before.Generation || current.Map != map
                    || current.Dialog is { } changed && changed.EntityId != npc)
                {
                    reason = "state-changed";
                    break;
                }
                if (current.Dialog is not { } page)
                    break;
                if (pages.Count == 0 || pages[^1].Revision != page.Revision)
                    pages.Add(page);
                if (page.Choices.Length > 0)
                {
                    reason = "choice";
                    break;
                }
                if (page.Prompt is not null || !page.IsPursuit)
                {
                    reason = "prompt";
                    break;
                }
                if (!page.HasNext)
                {
                    reason = "terminal";
                    break;
                }
                if (pages.Count >= command.MaxPages)
                {
                    reason = "page-limit";
                    break;
                }
                RequireUnusedDiscoveryPage(page);
                // A Next can commit a delayed reward. Never send this page twice.
                submittedDiscoveryRevision = page.Revision;
                await connection.SendAsync(Protocol.DialogPackets.Continue(current, page), current.Generation, ct);
                await WaitAsync(s => s.Map != map || s.Dialog is null || s.Dialog.EntityId != npc
                    || s.Dialog.Revision > page.Revision, current.Revision, TimeSpan.FromSeconds(5), ct);
            }
        }
        catch (Exception e) when (e is TimeoutException or IOException or QuestBlockedException)
        {
            reason = "reconcile";
            error = e.Message;
        }
        finally
        {
            if (ct.IsCancellationRequested)
                reason = "interrupted";
            Discovery = new(reason, pages.ToImmutableArray(), DiscoveryState.From(before), DiscoveryState.From(Snapshot), error);
        }
    }
}
