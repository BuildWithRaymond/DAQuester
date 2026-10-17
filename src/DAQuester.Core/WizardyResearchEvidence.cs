namespace DAQuester.Core;

public sealed record WizardyResearchOptions(string? Assistant = null)
{
    public void Validate(string character)
    {
        if (Assistant is not null && (Assistant.Length is < 2 or > 20 || !Assistant.All(char.IsAsciiLetter) || Assistant.Equals(character, StringComparison.OrdinalIgnoreCase)))
            throw new QuestBlockedException("Choose a different saved wizard as assistant, or research alone.");
    }
}

public sealed record ResearchAttemptReceipt(DateTimeOffset At, Dialog? Reply, Dictionary<string, int> Songs)
{
    public ServerNotice? Status { get; init; }
}
public sealed record ResearchDepositReceipt(DateTimeOffset At, string Item, int Quantity, Dialog Reply);
public sealed record WizardyResearchProgress
{
    public int Stage { get; init; }
    public Dialog? Source { get; init; }
    public Dialog? Cleanup { get; init; }
    public bool DepositCleanup { get; init; }
    public string? BankingItem { get; init; }
    public Dialog? SubmittedNext { get; init; }
    public string? DepositItem { get; init; }
    public byte DepositSlot { get; init; }
    public int DepositQuantity { get; init; }
    public ResearchAttemptReceipt[] Attempts { get; init; } = [];
    public ResearchDepositReceipt[] Deposits { get; init; } = [];
    public Dialog? Cooldown { get; init; }
    public DateTimeOffset? RestAt { get; init; }
}

public static class WizardyResearchEvidence
{
    public const string Id = "wizardy-research";
    public const int Map = 401;
    public const string Parchment = "Parchment Scroll";
    public const string Opening = "Do you need help before beginning your wizardy research? If you don't know about research you should seek help, and thereby make sure you bring your ingredients, reagents, or other materia.";
    public const string Begin = "No, I am ready to begin wizardy research";
    // The observed server opening includes the r; retain the original reported label.
    public const string ObservedBegin = "No, I am ready to begin wizardry research";
    public const string OutOfParchment = "You have no more available Parchment Scrolls";
    public const string SorceryResult = "You research the arcane art of Sorcery";
    public const string FailedExperiment = "The experiment is a failure";
    public const string Cooldown = "You have researched already. You cannot discover anything new until you have given your mind a long rest of four Temuairan days.";
    public static readonly Tile[] Triggers = [new(6, 9), new(5, 9), new(4, 9), new(3, 9)];
    public static bool IsSong(string name) => name.EndsWith(" Song", StringComparison.OrdinalIgnoreCase);
    public static Eligibility Eligibility(CharacterSnapshot s)
    {
        if (s.Connected && s.Name is not null && s.Gold == 0)
            return new(EligibilityKind.Blocked, "No gold carried. Parchment Scroll costs 500 gold from Dar; bring gold before starting research.");
        if (s.Connected && s.InventoryKnown && s.Gold is > 0 and < 500 && s.Count(Parchment) == 0)
            return new(EligibilityKind.Blocked, "Carry at least 500 gold to buy one Parchment Scroll from Dar before starting research.");
        if (!s.Connected || s.Name is null || !s.InventoryKnown || s.Class is null || s.Hp is null || s.Gold is null)
            return new(EligibilityKind.Unknown, "Connect this wizard for fresh class, inventory, health and gold.");
        if (!s.Class.Equals("Wizard", StringComparison.OrdinalIgnoreCase))
            return new(EligibilityKind.Blocked, "Wizardy research requires a wizard.");
        if (s.Hp == 0) return new(EligibilityKind.Unknown, "Waiting for fresh health after a zero-health reading.");
        return new(EligibilityKind.Eligible, "Specific · sorcery · one parchment at a time · 500 gold each");
    }
    public static bool AssistantEligible(CharacterSnapshot s) => s.Connected && s.Class == "Wizard" && s.Hp > 0 && s.LegendsKnown &&
        !s.Legends.Any(l => l.Contains("((Unregistered))", StringComparison.OrdinalIgnoreCase));
    public static Item? DepositCandidate(CharacterSnapshot s) => !s.InventoryKnown ? null : s.Inventory.Values.OrderBy(i => i.Slot)
        .FirstOrDefault(i => IsSong(i.Name) && s.Count(i.Name) >= 25 && i.Quantity > 0);
    public static bool ResearchPage(CharacterSnapshot s, Dialog d) => s.Map == Map && d.Generation == s.Generation && d.Revision > 0 && d.Revision <= s.Revision &&
        d.IsPursuit && d.EntityType == 4;
    public static Dictionary<string, int> NewSongs(CharacterSnapshot s, Transaction baseline) => s.Inventory.Values.Where(i => IsSong(i.Name))
        .Select(i => i.Name).Distinct(StringComparer.OrdinalIgnoreCase).Select(name => (Name: name, Gain: s.Count(name) - baseline.InventoryCounts.GetValueOrDefault(name)))
        .Where(x => x.Gain > 0).ToDictionary(x => x.Name, x => x.Gain, StringComparer.OrdinalIgnoreCase);

    public static ResearchAttemptReceipt? StatusResult(QuestJob job, CharacterSnapshot s)
    {
        if (job.Quest != Id || job.Pending is not { Kind: "research-submit", InventoryBaselineKnown: true } t ||
            job.Research?.Source is not { } source || !s.Connected || !string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase) ||
            s.Generation != t.Generation || s.Dialog is not null || !ResearchPage(s, source) ||
            source.EntityId != t.EntityId || source.EntityType != t.EntityType || source.Revision <= t.Revision ||
            !s.InventoryKnown || s.Gold != t.Gold || t.InventoryCounts.GetValueOrDefault(Parchment) <= 0 ||
            s.ActiveStatus is not { Type: 3 } status || status.Generation != t.Generation ||
            status.Revision <= source.Revision || status.Revision > s.Revision || status.ObservedAt < t.Started)
            return null;
        if (status.Text == FailedExperiment)
        {
            // The observed explosion retains parchment and awards no Songs.
            // Wait for server closure; the failure status arrives before it.
            var names = t.InventoryCounts.Keys.Concat(s.Inventory.Values.Select(i => i.Name))
                .Where(IsSong).Distinct(StringComparer.OrdinalIgnoreCase);
            return s.Count(Parchment) == t.InventoryCounts[Parchment] &&
                names.All(name => s.Count(name) == t.InventoryCounts.GetValueOrDefault(name))
                ? new(status.ObservedAt, null, new()) { Status = status } : null;
        }
        // The Sorcery status precedes item deliveries. Require the empty bag
        // and server dialog closure as well; that status alone is not a result.
        if (status.Text is not (OutOfParchment or SorceryResult) || s.Count(Parchment) != 0) return null;
        var gains = NewSongs(s, t);
        return gains.Count == 0 ? null : new(status.ObservedAt, null, gains) { Status = status };
    }

    internal static QuestJob RecordAttempt(QuestJob job, ResearchAttemptReceipt receipt)
    {
        var rewards = new Dictionary<string, int>(job.Rewards);
        foreach (var gain in receipt.Songs)
            rewards["item:" + gain.Key] = rewards.GetValueOrDefault("item:" + gain.Key) + gain.Value;
        var progress = job.Research ?? new();
        return job with { Pending = null, Rewards = rewards,
            Research = progress with { Stage = 0, Cleanup = receipt.Reply, Attempts = [.. progress.Attempts, receipt] },
            Reason = receipt.Status?.Text == FailedExperiment ? "Research experiment failed; parchment retained for another attempt"
                : "Research scroll and parchment receipts verified" };
    }

    public static QuestJob ObserveStatus(QuestJob job, CharacterSnapshot snapshot) =>
        StatusResult(job, snapshot) is { } receipt ? RecordAttempt(job, receipt) : job;
}
