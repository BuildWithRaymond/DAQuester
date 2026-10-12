namespace DAQuester.Core;

public record QuestRequirement
{
    public string Kind { get;
        init; } = "";
    public int? Minimum
    {
        get;
        init;
    }
    public int? Maximum
    {
        get;
        init;
    }
    public string? Value
    {
        get;
        init;
    }
    public string? Remediation
    {
        get;
        init;
    }
    public string UnknownPolicy { get;
        init; } = "wait";
}

public static class RequirementEvaluator
{
    public static Eligibility Evaluate(CharacterSnapshot s, QuestRequirement r)
    {
        long? value = r.Kind switch
        {
            "level" => s.Level,
            "gold" => s.Gold,
            "inventory" => s.InventoryKnown && r.Value is not null ? s.Count(r.Value) : null,
            "inventory-weight" => s.Weight.HasValue && s.MaxWeight.HasValue ? s.MaxWeight - s.Weight : null,
            "equipment" => r.Value is not null && s.Equipment.Values.Any(x => x.Equals(r.Value, StringComparison.OrdinalIgnoreCase)) ? 1 : null,
            _ => null
        };
        if (value is null)
            return new(EligibilityKind.Unknown, "Waiting for requirement: " + r.Kind);
        if (r.Minimum is { } min && value < min || r.Maximum is { } max && value > max)
            return new(EligibilityKind.Blocked, "Requirement unmet: " + r.Kind);
        return new(EligibilityKind.Eligible, "Requirement met: " + r.Kind);
    }
}
