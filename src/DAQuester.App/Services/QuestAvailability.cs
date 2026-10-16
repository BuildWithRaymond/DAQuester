namespace DAQuester.App.Services;

public sealed record QuestAvailability(string Status, string Reason, bool CanQueue, bool Warning = false)
{
    // A started altar run waits for its next offering itself.
    public bool Ready => Status is "Ready" or "Choices" or "Resume ready" or "Next offering" or "Bouquet waiting";
}
