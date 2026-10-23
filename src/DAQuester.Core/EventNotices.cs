namespace DAQuester.Core;

public static class EventNotices
{
    // Captured DATester page. The player authorized closing it, not advancing
    // the event's offered Next or accepting unknown event choices.
    public const string RedMoon = "The full Red Moon shines brightly in the sky. You stare at it, hypnotized by its light. Your body starts to tremble. You breath heavily as you continue to stare at the moon. Your body begins to change...";
    public const string CiadGreeting = "Greetings there, adventurous Aisling.  So, you lookin' for an adventure are you?";

    public static bool Recognized(CharacterSnapshot s) => s.Connected && s.Name is not null
        && s.Dialog is { IsPursuit: true, EntityType: 4, Name: "", Type: 0, HasNext: true, Prompt: null, Choices.Length: 0, Items.Length: 0 } d
        && d.Generation == s.Generation && d.Revision > 0 && d.Revision <= s.Revision
        && (d is { Pursuit: 0, Step: 1, Text: RedMoon }
            || s.Map == 500 && d is { Pursuit: 434, Step: 54, Text: CiadGreeting });

    public static async Task DismissAsync(IGameSession session, CancellationToken ct)
    {
        var before = session.Snapshot;
        if (!Recognized(before))
            return;
        var d = before.Dialog!;
        await session.ExecuteAsync(new("close", Dialog: d, ExpectedGeneration: before.Generation,
            ExpectedDialogRevision: d.Revision, ExpectedPageText: d.Text), ct);
        var after = session.Snapshot;
        if (!after.Connected || after.Generation != before.Generation || after.Name != before.Name || after.Dialog is not null)
            throw new QuestBlockedException("Event notice closure is unconfirmed. Inspect fresh state before resuming; no event continuation was selected.");
    }
}
