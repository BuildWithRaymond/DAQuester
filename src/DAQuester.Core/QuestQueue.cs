using System.Text.Json.Serialization;

namespace DAQuester.Core;

public record QueueOutcome(string Quest, string Outcome, DateTimeOffset At, string Summary);
public record QuestQueueState
{
    public string Character { get;
        init; } = "";
    public string[] Quests { get;
        init; } = [];
    public string State { get;
        init; } = "Idle";
    public string Reason { get;
        init; } = "";
    public string? ActiveQuest
    {
        get;
        init;
    }
    public DateTimeOffset? ActiveStartedAt
    {
        get;
        init;
    }
    public bool ActiveResume
    {
        get;
        init;
    }
    public QueueOutcome[] History { get;
        init; } = [];

    [JsonIgnore]
    public bool Locked => State == "Running" || State == "Paused" && ActiveQuest is not null;

    public QuestQueueState Reorder(string quest, int index)
    {
        if (Locked)
            throw new InvalidOperationException("Stop the queue before changing its order.");
        var ordered = UniqueQuests().ToList();
        if (!ordered.Remove(quest))
            return this with
            {
                Quests = ordered.ToArray()
            };
        ordered.Insert(Math.Clamp(index, 0, ordered.Count), quest);
        return this with
        {
            Quests = ordered.ToArray()
        };
    }

    internal string[] UniqueQuests() => Quests.Where(q => !string.IsNullOrWhiteSpace(q)).Distinct(StringComparer.Ordinal).ToArray();
    internal QuestQueueState Normalize() => this with
    {
        Quests = UniqueQuests(),
        History = History.TakeLast(100).ToArray()
    };
}
