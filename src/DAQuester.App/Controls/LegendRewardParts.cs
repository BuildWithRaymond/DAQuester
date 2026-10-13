using System.Text.RegularExpressions;

namespace DAQuester.App.Controls;

internal sealed record LegendRewardPart(string Text, int? Icon = null);

internal static class LegendRewardParts
{
    public static LegendRewardPart[] Split(string text, string evidence)
    {
        var icons = Regex.Matches(evidence, @"legend(aisling|warrior|rogue|wizard|priest|monk|heart|shield)\.png")
            .Select(m => m.Groups[1].Value switch
            {
                "warrior" => 1, "rogue" => 2, "wizard" => 3, "priest" => 4,
                "monk" => 5, "heart" => 6, "shield" => 7, _ => 0
            }).ToArray();
        if (icons.Length == 0)
            return [new(text)];
        text = LegendFirst(text);
        var result = new List<LegendRewardPart>();
        int offset = 0, index = 0;
        foreach (Match quote in Regex.Matches(text, "\"[^\"]+\""))
        {
            if (quote.Index > offset)
                result.Add(new(text[offset..quote.Index]));
            result.Add(new(quote.Value, icons[Math.Min(index++, icons.Length - 1)]));
            offset = quote.Index + quote.Length;
        }
        if (offset < text.Length)
            result.Add(new(text[offset..]));
        return result.ToArray();
    }

    static string LegendFirst(string text)
    {
        var first = Regex.Match(text, "\"[^\"]+\"");
        if (!first.Success || first.Index == 0)
            return text;
        string prefix = text[..first.Index].Trim(), legend = first.Value;
        string tail = text[(first.Index + first.Length)..];
        if (prefix == "Chance:")
            return legend + " (chance)" + tail;
        const string chance = "And a Chance of:";
        if (prefix.EndsWith(chance, StringComparison.Ordinal))
            return legend + tail + " (chance) · " + prefix[..^chance.Length].Trim();
        const string priests = "Priests:";
        if (prefix.EndsWith(priests, StringComparison.Ordinal))
            return legend + tail + " (Priests only) · " + prefix[..^priests.Length].Trim();
        return legend + tail + " · " + prefix.TrimEnd(' ', '+');
    }
}
