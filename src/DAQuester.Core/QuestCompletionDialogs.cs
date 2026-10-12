namespace DAQuester.Core;
// Dismiss only the conversation bound to an observed terminal receipt. Closing
// a popup never supplies reward evidence or authorizes another submission.
public static class QuestCompletionDialogs
{
    public static async Task DismissAsync(QuestContext context, Dialog? receipt, CancellationToken ct)
    {
        if (receipt is null)
            return;
        for (int page = 0; page < 4; page++)
        {
            ct.ThrowIfCancellationRequested();
            var s = context.Session.Snapshot;
            if (s.Dialog is not { } d)
                return;
            if (!s.Connected || !string.Equals(s.Name, context.Job.Character, StringComparison.OrdinalIgnoreCase) || s.Generation != receipt.Generation || d.Generation != receipt.Generation || d.EntityId != receipt.EntityId || d.EntityType != receipt.EntityType || d.Revision < receipt.Revision || d.Revision > s.Revision || !d.Name.Equals(receipt.Name, StringComparison.OrdinalIgnoreCase) || d.Text != receipt.Text || !d.IsPursuit || d.Type != 0 || d.Choices.Length > 0 || d.Prompt is not null)
                throw new QuestBlockedException("A different popup is open after the quest reward. It is preserved; review it before continuing the queue.");
            await context.Session.ExecuteAsync(new(d.HasNext ? "next" : "close", Dialog: d), ct);
            if (!d.HasNext)
                return; // The session awaits the server's close acknowledgment.
        }

        throw new QuestBlockedException("Quest reward popup exceeded its page limit. Review the preserved conversation before resuming.");
    }
}
