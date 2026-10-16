namespace DAQuester.Core;

// Recipe structure is separate from the character-specific progress saved in QuestJob.
public record QuestStep(
    string Id,
    string Action,
    string? Next,
    Dictionary<string, string>? Arguments = null,
    Dictionary<string, string>? Transitions = null);

public sealed record TaskPresentation
{
    public string Category { get; init; } = "Other";
    public string Location { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Requirements { get; init; } = "";
    public string HowItWorks { get; init; } = "";
}

public record QuestDefinition(
    string Id,
    int Version,
    string Name,
    string Source,
    string[] Capabilities,
    QuestStep[] Steps,
    double CooldownHours = 8)
{
    public bool IsTask { get; init; }
    public string[] QuestClasses { get; init; } = [];
    public TaskPresentation? TaskPresentation { get; init; }
    public ObservedQuestTask[] Journey { get; init; } = [];
    public Dictionary<string, ObservedJourneyVariant> JourneyVariants { get; init; } = [];
    public QuestRequirement[] Requirements
    {
        get;
        init;
    } = [];
    public string[] OutcomeConditions
    {
        get;
        init;
    } = [];
    public bool Repeatable
    {
        get;
        init;
    } = true;
}
