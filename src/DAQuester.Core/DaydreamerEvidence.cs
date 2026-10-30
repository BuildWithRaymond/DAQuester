using System.Globalization;
using System.Text.RegularExpressions;

namespace DAQuester.Core;

public static class DaydreamerEvidence
{
    public const string Id = "avid-daydreamer", Name = "Avid Temuairian Daydreamer", Npc = "Celesta", Option = "Avid Daydreamer";
    public const int Map = 3052, Goal = 30;
    public static readonly Tile Approach = new(48, 18);

    static Regex Pattern(string value) => new(value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    static readonly Regex count = Pattern(@"^This is your\s+([0-9]+)(?:st|nd|rd|th)?\s+consecutive Login!\s*$");
    static readonly Regex expiration = Pattern(@"^Your expiration date is\s+([0-9]{1,2})-([0-9]{1,2})\.?$");
    static readonly Regex renewal = Pattern(@"^(?:This|Your) account is on auto[- ]renewal[.!]?$");
    static readonly Regex greeting = Pattern(@"thank\s+you\s+for\s+dropping\s+by[\s\S]*check\s+you\s+in");
    static readonly Regex denial = Pattern(@"\b(?:this\s+)?feature\s+is\s+(?:available\s+)?for\s+registered\s+users\s+only\b");
    static readonly Regex already = Pattern(@"\b(?:already\s+(?:(?:been|have been)\s+)?(?:checked\s*in|registered|logged\s*in)(?:\s+(?:for\s+)?today)?|(?:checked\s*in|logged\s*in)\s+already|(?:only|once)\s+(?:once\s+)?(?:per|a)\s+day|come\s+back\s+tomorrow)\b");

    public static int? Count(ServerNotice notice)
    {
        if (notice.Type != 3) return null;
        var match = count.Match(notice.Text);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : null;
    }

    public static bool IsRegistration(ServerNotice notice)
    {
        if (notice.Type is not (1 or 2 or 3 or 4 or 6)) return false;
        if (renewal.IsMatch(notice.Text)) return true;
        var match = expiration.Match(notice.Text);
        return match.Success && int.TryParse(match.Groups[1].Value, out int month) && month is >= 1 and <= 12 &&
            int.TryParse(match.Groups[2].Value, out int day) && day is >= 1 and <= 31;
    }

    public static bool HasCurrentRegistration(CharacterSnapshot snapshot)
    {
        if (!snapshot.Connected || snapshot.Name is null || snapshot.Id is null) return false;
        if (snapshot.Registration is { } direct && direct.Generation == snapshot.Generation &&
            direct.Revision > 0 && direct.Revision <= snapshot.Revision && IsRegistration(direct)) return true;
        return snapshot.LoginRegistration is { Source: { } source } binding && binding.Generation == snapshot.Generation &&
            binding.Revision > 0 && binding.Revision <= snapshot.Revision && binding.CharacterId == snapshot.Id &&
            string.Equals(binding.Character, snapshot.Name, StringComparison.OrdinalIgnoreCase) &&
            source.Generation > 0 && source.Generation != snapshot.Generation && source.Revision > 0 && IsRegistration(source);
    }

    public static bool IsStatus(ServerNotice notice) => notice.Type == 3 &&
        (Count(notice) is not null || IsAlreadyDone(notice.Text) || IsRegistrationDenial(notice.Text));
    public static bool IsAlreadyDone(string text) => already.IsMatch(text);
    public static bool IsRegistrationDenial(string text) => denial.IsMatch(text);
    public static bool IsGreeting(Dialog page) => page.IsPursuit && page.Type == 0 && page.Name.Equals(Npc, StringComparison.OrdinalIgnoreCase) &&
        page.HasNext && page.Step < ushort.MaxValue && page.Choices.Length == 0 && page.Prompt is null && greeting.IsMatch(page.Text);
    public static bool IsPage(CharacterSnapshot snapshot, Dialog page) => snapshot.Map == Map && page.Generation == snapshot.Generation &&
        page.Revision > 0 && page.Revision <= snapshot.Revision && page.EntityId != 0 && page.Type == 0 && page.Name.Equals(Npc, StringComparison.OrdinalIgnoreCase);

    public static bool GoalVerified(QuestJob job) => job.Daydreamer?.FirstThirtyReceipt is { } receipt && Count(receipt) >= Goal;
    public static bool AttemptVerified(QuestJob job)
    {
        if (job.Daydreamer?.LastAcknowledgement is not { } ack || job.CompletedAt != ack.At) return false;
        return ack.Status is { } status && ack.Dialog is null && status.ObservedAt == ack.At &&
            (Count(status) is not null || status.Type == 3 && IsAlreadyDone(status.Text)) ||
            ack.Dialog is { } dialog && ack.Status is null && dialog.ObservedAt == ack.At && dialog.Type == 0 &&
            dialog.Name.Equals(Npc, StringComparison.OrdinalIgnoreCase) && IsAlreadyDone(dialog.Text);
    }
    public static bool NeedsRecovery(QuestJob job) => job.Pending is not null || job.Daydreamer is { CleanupPending: true } or { Submission: not null };

    public static Eligibility Eligibility(CharacterSnapshot snapshot, QuestJob job)
    {
        if (!snapshot.Connected || snapshot.Name is null || snapshot.Id is null)
            return new(EligibilityKind.Unknown, "Waiting for a connected character.");
        if (!snapshot.Name.Equals(job.Character, StringComparison.OrdinalIgnoreCase))
            return new(EligibilityKind.Blocked, "Resume Daydreamer on the saved character.");
        if (NeedsRecovery(job)) return new(EligibilityKind.Eligible, "Resume to reconcile the saved Celesta interaction.");
        if (GoalVerified(job) && !job.Repeat) return new(EligibilityKind.Eligible, "30 consecutive check-ins verified.");
        if (snapshot.Hp is null) return new(EligibilityKind.Unknown, "Waiting for current health before visiting Celesta.");
        if (snapshot.Hp == 0) return new(EligibilityKind.Blocked, "Daydreamer requires a living character.");
        if (snapshot.Map is null || snapshot.Position is null) return new(EligibilityKind.Unknown, "Waiting for current map and position before visiting Celesta.");
        if (snapshot.Exchange is { Cancelled: false }) return new(EligibilityKind.Blocked, "Finish the active trade before visiting Celesta.");
        if (snapshot.Dialog is { } page && !IsPage(snapshot, page)) return new(EligibilityKind.Blocked, "A different popup is open; it was preserved.");
        if (!HasCurrentRegistration(snapshot))
            return new(EligibilityKind.Unknown, "Waiting for current login registration confirmation.");
        return new(EligibilityKind.Eligible, Progress(job));
    }

    public static string Progress(QuestJob job) => job.Daydreamer?.Streak is { } value
        ? value.ToString(CultureInfo.InvariantCulture) + " consecutive check-ins" + (GoalVerified(job) ? " · first 30 verified" : " of 30")
        : "30 consecutive daily check-ins with Celesta";

    public static DateTimeOffset NextEligible(DateTimeOffset acknowledgedAt, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var nextDay = TimeZoneInfo.ConvertTime(acknowledgedAt, zone).Date.AddDays(1);
        var midnight = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextDay, zone));
        var fullDay = acknowledgedAt.AddHours(24);
        return midnight > fullDay ? midnight : fullDay;
    }
}
