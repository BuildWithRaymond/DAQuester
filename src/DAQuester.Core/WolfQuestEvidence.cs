using System.Text.RegularExpressions;

namespace DAQuester.Core;

// Dialogue and reward details are retained in wolf-live-discovery.json. The level
// threshold remains guide metadata; the manual kill does not prove a full runner.
public static class WolfQuestEvidence
{
    public const string Id = "wolf-of-west-woodlands", CatalogId = "wolfofwestwoodlands";
    public const string Legend = "Hunted down a wolf of West Woodland", Potion = "Vanilla Potion";
    public static readonly string[] Potions = [Potion, "Fifleaf deum"];
    public const string Npc = "Throttler";
    public const string Opening = "Hello.  What can I do for you?", Woodland = "WoodLand";
    public const string DailyQuest = "West Woodlands Daily Kill Quest";
    public const string Favor = "Could ya do me a favor?", Accept = "Sure.", Decline = "Sorry.";
    public const string Objective = "((Find a Wolf in the path off West Woodlands 19-2))";
    public const string EntranceWarning = "You can feel it in the air.. the viciousness of the wolves nearby.  These must be the wolves Throttler was talking about. Do you want to go in?";
    public const string RewardClosing = "Come back some other time!  I may a have few things fer ya to do.";
    public const int MinimumLevel = 70, NpcMap = 454, EntranceMap = 463, WolfMap = 464;
    public const string LevelRequirement = "Requires level 70+ (guide threshold; server requirement unverified).";
    public const ushort WolfSprite = 2, PotionSprite = 62;
    public const uint Experience = 60000;
    public static readonly Tile NpcAnchor = new(21, 20), NpcApproach = new(19, 20), Entrance = new(48, 26);
    // The reactor also opens before the navigation target is reached.
    public static readonly Tile[] EntranceWarningPositions = [Entrance, new(47, 25), new(48, 25)];
    public static readonly string[] IntroductionPages =
    [
        "Praise be the skies!  Another person!  I thought I'd never see more of the likes of you again.",
        "I mean me whole life I lived in this village, yes I did.  But when a beast destroyed it, The other villagers left me behind.",
        "Ahh well pfft to them!  I managed to survive 'ere fer a long time without them. And I dinna like 'em anyways(I thought they all be smelly).",
        "Anyway me name's Throttler, g'day to you."
    ];
    public static readonly string[] AcceptedPages =
    [
        "Great! Nowa let's see here...",
        "Great!! Now lurking in these woods is a big beast. Thems giving me the creeps.",
        "Get rid of one for yer old buddy Throttler eh?  You can find 'em at the end of the path, ya can."
    ];
    public static readonly string[] RewardPages =
    [
        "Yes!  Thank you.  Now there be a dead wolf.",
        "Ya did yer Old buddy Throttler a big favor, I'll give ya present in return.",
        "Take this potion, I made it meself with the local flora."
    ];
    static readonly Regex xp = new(@"\A(?:You gain |You received )?60,?000 experience[!.]?\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string? Mark(CharacterSnapshot s) => s.Legends.FirstOrDefault(l => l.Equals(Legend, StringComparison.OrdinalIgnoreCase));
    public static bool FreshLegends(CharacterSnapshot s) => s.LegendsKnown && s.LegendsRevision > 0 && s.LegendsRevision <= s.Revision;
    public static Eligibility Eligibility(CharacterSnapshot s)
    {
        if (!s.Connected || s.Name is null || s.Id is null || s.Generation < 1 || s.Level is null || s.Hp is null)
            return new(EligibilityKind.Unknown, "Waiting for the connected character, level and health.");
        if (s.Level < MinimumLevel) return new(EligibilityKind.Blocked, LevelRequirement);
        if (s.Hp == 0) return new(EligibilityKind.Blocked, "Requires a living character.");
        if (!s.InventoryKnown || s.Experience is null || s.Gold is null || s.Weight is null || s.MaxWeight is null || !FreshLegends(s))
            return new(EligibilityKind.Unknown, "Waiting for inventory, experience, gold, weight and your own fresh legend.");
        if (Mark(s) is { } mark) return new(EligibilityKind.Blocked, "Already completed: " + mark + ".");
        if (s.FreeInventorySlots < 1 || s.Weight >= s.MaxWeight)
            return new(EligibilityKind.Blocked, "Free carrying capacity for Vanilla Potion.");
        return new(EligibilityKind.Eligible, "Ready for Throttler's observed woodland route; unexpected replies pause for review.");
    }

    static bool Choices(Dialog d, params string[] expected) => d.Choices.Length == expected.Length
        && expected.All(text => d.Choices.Count(c => c.Text == text) == 1);
    public static string? DialogueChoice(Dialog d, bool returning) => d.Prompt is not null ? null
        : d.Text == Opening && d.Type == 0 && !d.HasNext && Choices(d, Woodland, DailyQuest) ? Woodland
        : !returning && d.IsPursuit && d.Text == Favor && d.Type == 2 && d.HasNext && Choices(d, Accept, Decline) ? Accept
        : null;
    public static bool NextPage(Dialog d, bool returning, bool accepted) => d.IsPursuit && d.Type == 0 && d.HasNext
        && d.Prompt is null && d.Choices.Length == 0
        && (returning ? RewardPages.Contains(d.Text) : (accepted ? AcceptedPages : IntroductionPages).Contains(d.Text));
    public static bool RewardTerminal(Dialog d) => d.IsPursuit && d.Type == 0 && !d.HasNext
        && d.Prompt is null && d.Choices.Length == 0 && d.Text == RewardClosing;
    public static bool EntrancePage(CharacterSnapshot s, Dialog d) => s.Map == EntranceMap && s.Position is { } position
        && EntranceWarningPositions.Contains(position)
        && d.Generation == s.Generation && d.Revision > 0 && d.Revision <= s.Revision && d.IsPursuit
        && d.EntityType == 4 && d.Type == 2 && d.Prompt is null && d.HasNext && d.Text == EntranceWarning
        && Choices(d, "Yes", "No");
    public static bool ObjectiveMessage(ServerNotice n, Transaction acceptance) => n.Type == 3
        && n.Generation == acceptance.Generation && n.Revision > acceptance.Revision && n.Text == Objective;

    public static OwnMonsterDefeat? Defeat(CharacterSnapshot s, WolfQuestProgress progress) => s.Generation == progress.HuntGeneration
        ? s.OwnMonsterDefeats.FirstOrDefault(d => d.Generation == s.Generation && d.Revision > progress.HuntRevision
            && d.Revision <= s.Revision && d.Map == WolfMap && d.Sprite == WolfSprite) : null;

    public static bool ExperienceMessage(ServerNotice n, Transaction baseline) => n.Type == 3 && n.Generation == baseline.Generation
        && n.Revision > baseline.Revision && xp.IsMatch(n.Text);
    public static bool Reward(CharacterSnapshot s, WolfQuestProgress progress)
    {
        var t = progress.TurnIn;
        return t is not null && s.Connected && s.Generation == t.Generation && s.Revision > t.Revision && s.Map == NpcMap
            && t.InventoryBaselineKnown && s.InventoryKnown && Potions.Any(potion => s.Count(potion) > t.InventoryCounts.GetValueOrDefault(potion))
            && !t.Legends.Any(l => l.Equals(Legend, StringComparison.OrdinalIgnoreCase)) && FreshLegends(s)
            && s.LegendsRevision > progress.LegendBaselineRevision && s.LegendsRevision > t.Revision && Mark(s) is not null
            && (s.Experience is { } value && value >= (ulong)t.Experience + Experience
                || progress.RewardMessages.Any(n => n.Revision <= s.Revision && ExperienceMessage(n, t))
                || s.ActiveStatus is { } n && n.Revision <= s.Revision && ExperienceMessage(n, t));
    }
}
