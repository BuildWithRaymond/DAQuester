using System.Globalization;
using System.Text.RegularExpressions;
namespace DAQuester.Core;

public record SickChildOptions(string Mode, string[] Helpers, int PriorTurnIns)
{
    public const string Daily = "daily", WithHelpers = "helpers";
    public void Validate()
    {
        if (Mode is not (Daily or WithHelpers))
            throw new QuestBlockedException("Choose how to get the flowers: one a day, or from your other characters.");
        if (PriorTurnIns is < 0 or > 4)
            throw new QuestBlockedException("Flowers already given to Paulin must be 0 to 4.");
        if (Helpers.Any(h => h.Length == 0 || !h.All(char.IsAsciiLetter)))
            throw new QuestBlockedException("Helper names use letters only.");
        if (Helpers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Helpers.Length)
            throw new QuestBlockedException("Each helper can be chosen once.");
        if (Mode == WithHelpers && Helpers.Length == 0)
            throw new QuestBlockedException("Choose at least one helper, or pick one flower a day.");
    }
}
public record SickChildTurnIn(DateTimeOffset At, long Generation, uint GoldBefore, uint GoldAfter, string Experience, string? Helper);
public record SickChildPick(string Character, DateTimeOffset At, bool Refused);

// Another character that picks a flower and trades it to the quester.
public interface IQuestHelper : IAsyncDisposable
{
    string Name
    {
        get;
    }
    IGameSession Session
    {
        get;
    }
    bool LoggedInForQuest
    {
        get;
    }
    CancellationToken Released
    {
        get;
    }
    Task LogOutAsync(CancellationToken cancellation);
}
// The helper's login didn't complete; the run may try it once more.
public sealed class HelperLoginException(string reason) : QuestBlockedException(reason);
public interface IQuestHelpers
{
    Task<IQuestHelper> AcquireAsync(string name, CancellationToken cancellation);
    void Report(string name, string status);
    // One pick a day per character across every flower, whichever quest made it (live 2026-10-12).
    IReadOnlyList<SickChildPick> SharedPicks
    {
        get;
    }
    void RecordPick(SickChildPick pick);
}
// Paulin takes Sevti (Cured the Sick Child) or Tulsi (Aided the Pact of Anaman); only these differ.
// Sevti goes to the child with the coins; Tulsi goes plainly to the physician, who is in on the pact.
public sealed record PaulinFlower(string Id, string CatalogId, string QuestName, string Legend, string OtherLegend, string Name, string Item, Tile Spot, string Description, string Give, uint Cost)
{
    public string Offer => "Offer " + Name + " Blossom";
    public Tile[] Sides => [new(Spot.X + 1, Spot.Y), new(Spot.X, Spot.Y - 1), new(Spot.X, Spot.Y + 1), new(Spot.X - 1, Spot.Y)];
}

public static class SickChildEvidence
{
    public const string Id = "cured-sick-child", CatalogId = "curedsickchild", AnamanId = "aided-pact-of-anaman", AnamanCatalogId = "aidedthepactofanaman";
    public const string Legend = "Cured a sick child in Loures", AnamanLegend = "Aided the Pact of Anaman";
    public const string Blossom = "White Blossom", Paulin = "Paulin";
    public const int Goal = 5, PaulinMap = 3070, FlowerMap = 3077;
    public const uint Cost = 5000;
    public static readonly Tile QuesterTile = new(4, 4), StagingTile = new(5, 6), FlowerApproach = new(3, 9), FlowerTile = new(2, 9);
    public static readonly TimeSpan PickInterval = TimeSpan.FromHours(24);
    // Exact texts observed live on 2026-10-11 (valadriel, jackharkness).
    public const string FlowerHere = "You see a flower here.", WhiteFlower = "You see a white flower, which smells like lemon.", PickChoice = "Pick the flower";
    public const string Picked = "You notice a small white flower that has been picked", NoBlossom = "You don't have a Sevti Blossom";
    public const string AskChild = "The sick child, how is she?", Offer = "Offer Sevti Blossom", GiveWithCoins = "Give Sevti Blossom and 5000 coins";
    public const string Collect = "I must collect this blossom", GaveIt = "I gave it to her", Smiles = "He smiles";
    public const string Fume = "A fume rises and you suddenly feel sleepy", Stunned = "Stunned", MoveAgain = "You can move again.";
    public const string Blind = "You canot see anything.", AllBlack = "All goes black", SeeAgain = "You can see again.";
    public const string TulsiDescription = "You see a flower here with seven green leaves and blue center.";
    public static readonly PaulinFlower Sevti = new(Id, CatalogId, "Cured the Sick Child", Legend, AnamanLegend, "Sevti", Blossom, FlowerTile, WhiteFlower, GiveWithCoins, Cost);
    public static readonly PaulinFlower Tulsi = new(AnamanId, AnamanCatalogId, "Aided the Pact of Anaman", AnamanLegend, Legend, "Tulsi", "Golden Blossom", new(11, 3), TulsiDescription, "Give Tulsi Blossom", 0);
    public static PaulinFlower? For(string quest) => quest == Id ? Sevti : quest == AnamanId ? Tulsi : null;
    static readonly Regex experience = new(@"^(\d+) experience!$", RegexOptions.CultureInvariant);
    // Sevti: "a small white flower"; Tulsi: "a green leaved plant" (both live 2026-10-12).
    public const string TulsiPicked = "You notice a green leaved plant that has been picked";
    static readonly Regex picked = new(@"^You notice a .+ that has been picked$", RegexOptions.CultureInvariant);
    static readonly Regex noFlower = new(@"^You don't have a \w+ Blossom$", RegexOptions.CultureInvariant);
    public static bool IsPicked(string? text) => text is not null && picked.IsMatch(text);
    public static bool IsNoFlower(string? text) => text is not null && noFlower.IsMatch(text);
    public static bool IsHazard(string? text) => text is Fume or Stunned or Blind or AllBlack;
    public static bool IsRecovery(string? text) => text is MoveAgain or SeeAgain;

    public static string? Mark(CharacterSnapshot s, string legend) => s.LegendsKnown ? Mark(s.Legends, legend) : null;
    // The pact mark is written "- Aided the Pact of Anaman - Deoch 223, Spring" (live 2026-10-12).
    public static string? Mark(IEnumerable<string> legends, string legend) => legends.FirstOrDefault(l => EmeraldDawnEvidence.Matches(l.TrimStart('-', ' '), legend));
    // Only the legend mark locks the other quest; partial progress never does (player rule 2026-10-11).
    public static string? Lockout(string quest, IEnumerable<string> legends) => quest switch
    {
        Id or CatalogId => Mark(legends, AnamanLegend) is { } anaman ? "You already have \"" + anaman + "\". A character can earn only one of the two." : null,
        AnamanId or AnamanCatalogId => Mark(legends, Legend) is { } cured ? "You already have \"" + cured + "\". A character can earn only one of the two." : null,
        _ => null
    };
    public static bool IsNotice(ServerNotice notice, int? map) => notice.Type == 3 && (
     map == PaulinMap && (Experience(notice.Text) is not null || notice.Text is Smiles || IsNoFlower(notice.Text)) ||
     map == FlowerMap && IsPicked(notice.Text) || IsHazard(notice.Text) || IsRecovery(notice.Text));
    public static string? Experience(string? text) => text is not null && experience.IsMatch(text) ? text : null;
    public static int Given(QuestJob job) => (job.SickChildOptions?.PriorTurnIns ?? 0) + job.SickChildTurnIns.Length;
    public static int Remaining(QuestJob job) => Math.Max(0, Goal - Given(job));
    // The guide's one flower a day; the server's refusal always wins over this estimate.
    public static DateTimeOffset? NextPick(QuestJob job, string character) => NextPick(job.SickChildPicks, character);
    public static DateTimeOffset? NextPick(IEnumerable<SickChildPick> picks, string character)
    {
        var last = picks.Where(p => p.Character.Equals(character, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.At).LastOrDefault();
        return last is null ? null : last.At + PickInterval;
    }
    public static bool CanPick(QuestJob job, string character, DateTimeOffset now) => CanPick(job.SickChildPicks, character, now);
    public static bool CanPick(IEnumerable<SickChildPick> picks, string character, DateTimeOffset now) => NextPick(picks, character) is not { } next || next <= now;
    // A successful pick with none of that helper's flowers turned in since: they still hold it and only need to trade.
    public static bool HoldsFlower(QuestJob job, string character)
    {
        var pick = job.SickChildPicks.Where(p => !p.Refused && p.Character.Equals(character, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.At).LastOrDefault();
        return pick is not null && !job.SickChildTurnIns.Any(t => t.At >= pick.At && string.Equals(t.Helper, character, StringComparison.OrdinalIgnoreCase));
    }
    public const string HoldingLine = "Holding a flower to give";
    public static string? PickLine(QuestJob job, string character, DateTimeOffset now) => PickLine(job.SickChildPicks, character, now);
    public static string? PickLine(IEnumerable<SickChildPick> picks, string character, DateTimeOffset now)
    {
        var last = picks.Where(p => p.Character.Equals(character, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.At).LastOrDefault();
        if (last is null || last.At + PickInterval <= now)
            return null;
        var local = last.At.ToLocalTime();
        return last.Refused ? "Can't pick, refused at " + local.ToString("h:mm tt", CultureInfo.CurrentCulture) : "Can pick again at " + (last.At + PickInterval).ToLocalTime().ToString("h:mm tt ddd", CultureInfo.CurrentCulture);
    }
    public static bool IsFlowerPopup(Dialog d, PaulinFlower? flower = null) => d.IsPursuit && d.EntityType == 4 && d.Prompt is null && (d.Text == FlowerHere || d.Text == (flower ?? Sevti).Description);
    public static bool ChoicesLocked(QuestJob job) => job.Pending is not null || job.CompletedAt is not null;
    public static Eligibility Eligibility(CharacterSnapshot s, QuestJob job, PaulinFlower? flower = null)
    {
        var f = flower ?? Sevti;
        if (!s.Connected || s.Name is null || s.Id is null || s.Level is null || s.Hp is null)
            return new(EligibilityKind.Unknown, "Waiting for connected identity, level and health.");
        if (s.Level < 11 || s.Hp == 0)
            return new(EligibilityKind.Blocked, f.QuestName + " requires level 11+ and a living character.");
        if (!s.LegendsKnown)
            return new(EligibilityKind.Unknown, "Waiting for own legend.");
        if (Lockout(f.Id, s.Legends) is { } locked)
            return new(EligibilityKind.Blocked, locked);
        if (Mark(s, f.Legend) is not null)
            return new(EligibilityKind.Eligible, "Legend already recorded");
        if (!s.InventoryKnown || s.Gold is null)
            return new(EligibilityKind.Unknown, "Waiting for inventory and gold.");
        if (s.Gold < f.Cost)
            return new(EligibilityKind.Blocked, "Carry at least 5,000 gold; Paulin takes 5,000 for each flower.");
        if (s.Count(f.Item) == 0 && s.Inventory.Count >= 59)
            return new(EligibilityKind.Blocked, "Free an inventory slot for a " + f.Item + ".");
        return new(EligibilityKind.Eligible, "Level 11+; give Paulin " + Remaining(job) + " more " + f.Name + " Blossom" + (Remaining(job) == 1 ? "" : "s"));
    }
}
