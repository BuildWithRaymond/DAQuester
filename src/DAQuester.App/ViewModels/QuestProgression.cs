using System.Text.RegularExpressions;

namespace DAQuester.App.ViewModels;

// Authored reading order, not server eligibility or an automatic queue plan.
internal static class QuestProgression
{
    static readonly string[] starter = ["awakening", "mundanebirth", "spareastick", "peasantsfirststeps",
        "terroralleygarden", "ciad", "perfecthairstyle", "muisirvillageentrance", "fromtheheart", "cryptclimb"];

    public static (int Phase, int Level, int Starter) Order(string id, string level)
    {
        int first = Array.IndexOf(starter, id);
        if (first >= 0)
            return (0, 0, first);
        int phase = level.Contains("AB", StringComparison.OrdinalIgnoreCase) ? 3
            : level.Contains("Master", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        var number = Regex.Match(level, @"\d+");
        return (phase, number.Success ? int.Parse(number.Value) : int.MaxValue, 0);
    }
}
