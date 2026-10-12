using System.Globalization;

namespace DAQuester.Core;

public record MundaneBirthOptions(string Season, int Age, string Town)
{
    public static readonly string[] Seasons = ["Spring", "Summer", "Fall", "Winter"];
    public static readonly string[] Towns = ["Abel", "Mileth", "Piet", "Rucesion", "Suomi", "Undine"];
    public void Validate()
    {
        if (!Seasons.Contains(Season, StringComparer.Ordinal) || Age is < 12 or > 40 || !Towns.Contains(Town, StringComparer.Ordinal))
            throw new QuestBlockedException("Birth choices required: season, whole-number age 12-40, and one of the six towns.");
    }
}

public static class MundaneBirthEvidence
{
    public const string CompletionText = "She winks, \"It's on your legend now\"";
    public static bool Matches(ServerNotice? notice) => notice is { Type: 1 or 2 or 3 or 4 or 6 } && notice.Text.Trim().TrimEnd('.').Equals(CompletionText, StringComparison.Ordinal);
    public static Eligibility Eligibility(CharacterSnapshot s, MundaneBirthOptions? options)
    {
        try
        {
            if (options is null)
                throw new QuestBlockedException("Birth choices required: choose season, age 12-40 and town before starting.");
            options.Validate();
        }
        catch (QuestBlockedException e)
        {
            return new(EligibilityKind.Blocked, e.Message);
        }

        if (!s.Connected || s.Name is null || s.Id is null)
            return new(EligibilityKind.Unknown, "Waiting for connected character identity.");
        if (s.Level is null || s.Hp is null)
            return new(EligibilityKind.Unknown, "Waiting for server level and health.");
        if (s.Level < 1 || s.Hp == 0)
            return new(EligibilityKind.Blocked, "Requires level 1+ and a living character.");
        if (s.Legends.Any(l => l.Contains("mundane birth", StringComparison.OrdinalIgnoreCase)))
            return new(EligibilityKind.Blocked, "Birth already recorded. Changing it can cost up to 100,000 XP; use manual play.");
        return new(EligibilityKind.Eligible, "Birth choices ready");
    }

    public static bool Proves(CharacterSnapshot s, Transaction t) => t.Kind == "birth-record" && t.EntityId is > 0 && s.Generation == t.Generation && s.Dialog is null && s.Notice is { } n && Matches(n) && n.Generation == t.Generation && n.Revision > t.Revision && n.Revision <= s.Revision && n.ObservedAt >= t.Started && n.ObservedAt - t.Started <= TimeSpan.FromSeconds(30);
}

public sealed class MundaneBirthActions : IQuestAction
{
    static readonly TimeSpan timeout = TimeSpan.FromSeconds(5);
    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        if (step.Action == "birth-confirm")
        {
            await Confirm(c, ct);
            return;
        }

        if (step.Action == "birth-dialog" && c.Job.Pending is not null)
        {
            if (c.Job.Pending.Kind != "birth-record")
                throw new QuestBlockedException("Unexpected pending birth transaction; reconciliation required.");
            return; // Reconcile the final submission even if its successful legend is already visible.
        }

        var gate = MundaneBirthEvidence.Eligibility(c.Session.Snapshot, c.Job.BirthOptions);
        if (gate.Kind != EligibilityKind.Eligible)
            throw new QuestBlockedException(gate.Reason);
        if (step.Action == "birth-preflight")
            return;
        if (step.Action != "birth-dialog")
            throw new QuestBlockedException("Unsupported birth action: " + step.Action);
        var session = c.Session;
        var choices = c.Job.BirthOptions!;
        await c.SaveAsync(c.Job with
        {
            Reason = "Walking to Riona in Mileth Inn"
        });
        await session.ExecuteAsync(new("visit", 136, Text: "Riona"), ct);
        await session.ExecuteAsync(new("open", Text: "Riona"), ct);
        var menu = Current(c);
        if (menu.IsPursuit)
            throw new QuestBlockedException("Expected Riona's quest menu.");
        await Choose(c, "Mundane Birth", ct);
        CheckStage(c, 42);
        await Next(c, ct);
        CheckStage(c, 44);
        await Choose(c, "I have investigated my past", ct);
        CheckStage(c, 49);
        await Choose(c, "I am committed to this", ct);
        CheckStage(c, 60);
        await c.SaveAsync(c.Job with
        {
            Reason = "Choosing " + choices.Season
        });
        await Choose(c, choices.Season, ct);
        CheckStage(c, 78);
        var age = Current(c);
        if (age.Type != 4 || age.Prompt != "My mundane birth was")
            throw new QuestBlockedException("Unexpected Riona age prompt; no number submitted.");
        await c.SaveAsync(c.Job with
        {
            Reason = "Entering mundane age " + choices.Age
        });
        await session.ExecuteAsync(new("input", Text: choices.Age.ToString(CultureInfo.InvariantCulture), Dialog: age), ct);
        await Reply(c, age, ct);
        CheckStage(c, 128);
        ct.ThrowIfCancellationRequested();
        var town = Current(c);
        if (!town.Choices.Any(x => x.Text.Equals(choices.Town, StringComparison.Ordinal)))
            throw new QuestBlockedException("Requested birth town missing from Riona's choices.");
        await c.BeginAsync("birth-record", town.EntityId, "Recording birth in " + choices.Town);
        await session.ExecuteAsync(new("choose", Text: choices.Town, Dialog: town), ct);
    }

    static Dialog Current(QuestContext c)
    {
        var s = c.Session.Snapshot;
        var d = s.Dialog ?? throw new QuestBlockedException("Expected Riona dialog. Resume to restart the unfinished conversation.");
        if (!s.Connected || d.Generation != s.Generation || d.Revision > s.Revision || !d.Name.Equals("Riona", StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Unexpected or stale Riona dialog.");
        if (d.Choices.Any(x => x.Text == "I am changing my birth!") || d.IsPursuit && d.Pursuit == 186 && d.Step == 15)
            throw new QuestBlockedException("Birth already recorded. Changing it can cost up to 100,000 XP; use manual play.");
        return d;
    }

    static void CheckStage(QuestContext c, ushort step)
    {
        var d = Current(c);
        if (!d.IsPursuit || d.Pursuit != 186 || d.Step != step)
            throw new QuestBlockedException($"Unexpected Riona birth dialog: pursuit {d.Pursuit}, step {d.Step}; expected 186/{step}.");
    }

    static Task<CharacterSnapshot> Reply(QuestContext c, Dialog d, CancellationToken ct) => c.Session.WaitAsync(s => s.Generation == d.Generation && s.Dialog is { } n && n.Generation == d.Generation && n.EntityId == d.EntityId && n.Revision > d.Revision, d.Revision, timeout, ct);
    static async Task Choose(QuestContext c, string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var d = Current(c);
        if (!d.Choices.Any(x => x.Text.TrimEnd('.').Equals(text.TrimEnd('.'), StringComparison.OrdinalIgnoreCase)))
            throw new QuestBlockedException("Expected Riona option missing: " + text);
        await c.Session.ExecuteAsync(new("choose", Text: text, Dialog: d), ct);
        await Reply(c, d, ct);
        Current(c);
    }

    static async Task Next(QuestContext c, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var d = Current(c);
        if (!d.HasNext || d.Choices.Length != 0)
            throw new QuestBlockedException("Expected Riona introduction with Next.");
        await c.Session.ExecuteAsync(new("next", Dialog: d), ct);
        await Reply(c, d, ct);
    }

    static async Task Confirm(QuestContext c, CancellationToken ct)
    {
        var t = c.Job.Pending ?? throw new QuestBlockedException("No recorded birth transaction.");
        if (t.Kind != "birth-record" || t.Generation != c.Session.Snapshot.Generation)
            throw new QuestBlockedException("Birth submission requires reconciliation after reconnect. Inspect the legend; do not resend.");
        CharacterSnapshot done;
        try
        {
            done = await c.Session.WaitAsync(s => MundaneBirthEvidence.Proves(s, t), t.Revision, timeout, ct);
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("Birth completion message missing. Inspect the legend before reconciliation; final town choice will not be resent.");
        }

        var notice = done.Notice!;
        await c.SaveAsync(c.Job with
        {
            Pending = null,
            CompletedAt = notice.ObservedAt,
            BirthCompletion = notice,
            CompletionText = notice.Text,
            Reason = notice.Text,
            Repeat = false,
            NextRun = null,
            Accepted = false
        });
    }
}
