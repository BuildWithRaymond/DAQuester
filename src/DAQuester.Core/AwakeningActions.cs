namespace DAQuester.Core;

// The tutorial, as recorded live on 2026-10-12 (capture-20261012-155114255.json): Raghnall's combat talk (Stick),
// Bartley's Interface and General Info tutorials ('nis' spell, Look skill), the exit at (49,29) (1,000 coins,
// 5,000 experience), then the wake-up in Mileth Inn Room 0, triggered on the way to (3,5) (Dirk, two rings,
// Tutorial Book, 1,000 coins, 5,000 experience).
public static class AwakeningEvidence
{
    public const string Id = "awakening", Name = "Awakening";
    public const int TutorialMap = 3029, RoomMap = 3000;
    public const string Raghnall = "Raghnall", Bartley = "Bartley", Stick = "Stick", Dirk = "Dirk", Nis = "nis";
    public const string Interface = "Tutorial: Interface", GeneralInfo = "Tutorial: General Info";
    public const int EntryMap = 133;
    public const string EntryNpc = "Torrance", EntryTopic = "(( Tutorial ))", EntryAnswer = "I am up to the challenge. Take me there.";
    public static readonly Tile ExitApproach = new(48, 29), Exit = new(49, 29);
    // The recorded walk from the landing tile (2,3); the wake-up popup arrived on the step to (3,5).
    public static readonly Tile[] WakePath = [new(3, 3), new(3, 4), new(3, 5)];
    public static readonly string[] WakeRewards = ["Dirk", "Small Emerald Ring", "Small Spinel Ring", "Tutorial Book"];
    // Questions inside the tutorial popups and the answer given in the recording.
    public static readonly (string Question, string Answer)[] Answers =
    [
        ("Do you understand these guidelines?", "Yes"),
        ("I will take you back to your home now", "Ok")
    ];

    public static bool Owned(CharacterSnapshot s, string item) => s.Count(item) > 0 || s.Equipment.Values.Any(v => v.Equals(item, StringComparison.OrdinalIgnoreCase));
    public static bool KnowsNis(CharacterSnapshot s) => s.Spells.Values.Any(v => v.Equals(Nis, StringComparison.OrdinalIgnoreCase));

    public static Eligibility Eligibility(CharacterSnapshot s)
    {
        if (!s.Connected || s.Name is null || s.Map is null)
            return new(EligibilityKind.Unknown, "Waiting for connected identity and map.");
        if (s.Map == TutorialMap)
            return new(EligibilityKind.Eligible, "In the tutorial");
        if (s.Map == RoomMap && !Owned(s, Dirk))
            return new(EligibilityKind.Eligible, "Waking up in Mileth Inn Room 0");
        if (s.Level is null)
            return new(EligibilityKind.Unknown, "Waiting for an observed level before tutorial entry.");
        if (s.Map != RoomMap && s.Level is >= 1 and <= 3)
            return new(EligibilityKind.Eligible, "Walk to Torrance in Mileth Armor Shop to enter the tutorial");
        return new(EligibilityKind.Blocked, "Awakening starts with a level 1–3 character, or resumes in the tutorial before the wake-up rewards.");
    }
}

public sealed class AwakeningActions : IQuestAction
{
    static readonly TimeSpan reply = TimeSpan.FromSeconds(5), popup = TimeSpan.FromSeconds(2);
    const int MaxPages = 120;

    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        var s = c.Session;
        if (s.Snapshot.Name is not { } name || !name.Equals(c.Job.Character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Character changed; resume on the saved character.");
        switch (step.Action)
        {
            case "awakening-preflight":
                var gate = AwakeningEvidence.Eligibility(s.Snapshot);
                if (gate.Kind != EligibilityKind.Eligible)
                    throw new QuestBlockedException(gate.Reason);
                return;
            case "awakening-run":
                await Run(c, name, ct);
                return;
            default:
                throw new QuestBlockedException("Unsupported action: " + step.Action);
        }
    }

    static async Task Run(QuestContext c, string name, CancellationToken ct)
    {
        var s = c.Session;
        await Enter(c, ct);
        if (s.Snapshot.Map == AwakeningEvidence.TutorialMap)
        {
            // The welcome popup shows on arrival; read it before walking.
            await ReadThrough(c, d => d.EntityType != 1, ct);
            if (!AwakeningEvidence.Owned(s.Snapshot, AwakeningEvidence.Stick))
                await Raghnall(c, ct);
            if (!AwakeningEvidence.KnowsNis(s.Snapshot))
                await Bartley(c, AwakeningEvidence.Interface, ct);
            if (!AwakeningEvidence.KnowsNis(s.Snapshot))
                throw new QuestBlockedException("Bartley's Interface tutorial ended without the 'nis' spell. Check the spell book, then resume.");
            await Bartley(c, AwakeningEvidence.GeneralInfo, ct);
            await Leave(c, ct);
        }

        if (s.Snapshot.Map != AwakeningEvidence.RoomMap)
            throw new QuestBlockedException("Expected to wake up in Mileth Inn Room 0 after leaving the tutorial.");
        await WakeUp(c, ct);
        var missing = AwakeningEvidence.WakeRewards.Where(item => !AwakeningEvidence.Owned(s.Snapshot, item)).ToArray();
        if (missing.Length > 0)
            throw new QuestBlockedException("The wake-up ended without " + string.Join(", ", missing) + ". Check the bag, then resume.");
        string text = name + " finished the tutorial at level " + s.Snapshot.Level + " with " + (s.Snapshot.Gold ?? 0).ToString("N0") + " gold, a Dirk and the 'nis' spell";
        await c.SaveAsync(c.Job with
        {
            Step = "complete",
            Pending = null,
            CompletedAt = DateTimeOffset.UtcNow,
            CompletionText = text,
            Repeat = false,
            NextRun = null,
            Reason = text
        });
    }

    static async Task Enter(QuestContext c, CancellationToken ct)
    {
        var s = c.Session;
        if (c.Job.Pending is { } pending)
        {
            if (pending.Kind != "awakening-entry")
                throw new QuestBlockedException("A saved action needs reconciliation before tutorial entry.");
            if (s.Snapshot.Map is AwakeningEvidence.TutorialMap or AwakeningEvidence.RoomMap && s.Snapshot.Position is not null)
                await c.SaveAsync(c.Job with { Pending = null, CompletionDialog = null });
            else
                throw new QuestBlockedException("Tutorial entry is unconfirmed. Resume after the tutorial map loads; do not submit another entry request.");
        }
        if (s.Snapshot.Map is AwakeningEvidence.TutorialMap or AwakeningEvidence.RoomMap)
            return;
        var eligibility = AwakeningEvidence.Eligibility(s.Snapshot);
        if (eligibility.Kind != EligibilityKind.Eligible)
            throw new QuestBlockedException(eligibility.Reason);
        await Say(c, "Walking to Torrance to enter the tutorial");
        await s.ExecuteAsync(new("visit", AwakeningEvidence.EntryMap, Text: AwakeningEvidence.EntryNpc), ct);
        await s.ExecuteAsync(new("open", Text: AwakeningEvidence.EntryNpc), ct);
        var menu = s.Snapshot.Dialog;
        if (menu is null || menu.IsPursuit || menu.Name != AwakeningEvidence.EntryNpc || !menu.Choices.Any(x => x.Text == AwakeningEvidence.EntryTopic))
            throw new QuestBlockedException("Torrance does not offer the tutorial to this character.");
        await s.ExecuteAsync(new("choose", Text: AwakeningEvidence.EntryTopic, Dialog: menu), ct);
        await Reply(c, menu, ct);
        for (int page = 0; page < MaxPages; page++)
        {
            if (s.Snapshot.Dialog is not { IsPursuit: true, EntityType: 1 } d || d.Name != AwakeningEvidence.EntryNpc)
                throw new QuestBlockedException("Torrance's tutorial introduction changed. Review his dialog before continuing.");
            if (d.Choices.Length > 0)
            {
                if (!d.Text.Contains("Are you up for the challenge?", StringComparison.OrdinalIgnoreCase)
                    || !d.Choices.Any(x => x.Text == AwakeningEvidence.EntryAnswer))
                    throw new QuestBlockedException("Unexpected tutorial-entry question: " + d.Text);
                long generation = s.Snapshot.Generation;
                await c.BeginAsync("awakening-entry", d.EntityId, "Entering the tutorial", d);
                await s.ExecuteAsync(new("choose", Text: AwakeningEvidence.EntryAnswer, Dialog: d), ct);
                try
                {
                    await s.WaitAsync(x => x.Generation == generation && string.Equals(x.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase) && x.Connected
                        && x.Map == AwakeningEvidence.TutorialMap && x.Position is not null,
                        d.Revision, reply, ct);
                }
                catch (TimeoutException)
                {
                    throw new QuestBlockedException("Tutorial entry was requested but arrival is unconfirmed. Resume after the tutorial map loads.");
                }
                await c.SaveAsync(c.Job with { Pending = null, CompletionDialog = null });
                return;
            }
            if (!d.HasNext)
                throw new QuestBlockedException("Torrance did not allow tutorial entry: " + d.Text);
            await s.ExecuteAsync(new("next", Dialog: d), ct);
            await Reply(c, d, ct);
        }
        throw new QuestBlockedException("Torrance's tutorial introduction did not reach its entry question.");
    }

    static async Task DiscoverNpc(QuestContext c, string name, CancellationToken ct)
    {
        var s = c.Session;
        long generation = s.Snapshot.Generation;
        for (int popupCount = 0; popupCount < MaxPages; popupCount++)
        {
            if (s.Snapshot.Generation != generation || !string.Equals(s.Snapshot.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase))
                throw new QuestBlockedException("Character connection changed while finding the tutorial NPC.");
            await ReadThrough(c, d => d.EntityType != 1, ct);
            try
            {
                await s.ExecuteAsync(new("discover-npc", AwakeningEvidence.TutorialMap, Text: name), ct);
                if (s.Snapshot.Generation != generation || !string.Equals(s.Snapshot.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase))
                    throw new QuestBlockedException("Character connection changed while finding the tutorial NPC.");
                return;
            }
            catch (QuestPopupException popup) when (popup.Map == AwakeningEvidence.TutorialMap
                && popup.Dialog.Generation == generation && s.Snapshot.Dialog == popup.Dialog && popup.Dialog.EntityType != 1)
            {
                // Discovery retains its target while the existing reader handles the reactor.
            }
        }
        throw new QuestBlockedException("Too many tutorial popups while finding " + name + ".");
    }

    static async Task Raghnall(QuestContext c, CancellationToken ct)
    {
        var s = c.Session;
        await Say(c, "Talking to Raghnall about combat");
        await DiscoverNpc(c, AwakeningEvidence.Raghnall, ct);
        await s.ExecuteAsync(new("visit", AwakeningEvidence.TutorialMap, Text: AwakeningEvidence.Raghnall), ct);
        await s.ExecuteAsync(new("open", Text: AwakeningEvidence.Raghnall), ct);
        await ReadThrough(c, d => d.Name.Equals(AwakeningEvidence.Raghnall, StringComparison.OrdinalIgnoreCase), ct);
        if (!AwakeningEvidence.Owned(s.Snapshot, AwakeningEvidence.Stick))
            throw new QuestBlockedException("Raghnall's talk ended without a Stick. Check the bag, then resume.");
    }

    static async Task Bartley(QuestContext c, string topic, CancellationToken ct)
    {
        var s = c.Session;
        await Say(c, "Bartley: " + topic);
        bool Ours(Dialog d) => d.Name.Equals(AwakeningEvidence.Bartley, StringComparison.OrdinalIgnoreCase);
        if (s.Snapshot.Dialog is not { IsPursuit: false } menu || !Ours(menu))
        {
            if (s.Snapshot.Dialog is { } other)
                await s.ExecuteAsync(new("close", Dialog: other), ct);
            await DiscoverNpc(c, AwakeningEvidence.Bartley, ct);
            await s.ExecuteAsync(new("visit", AwakeningEvidence.TutorialMap, Text: AwakeningEvidence.Bartley), ct);
            await s.ExecuteAsync(new("open", Text: AwakeningEvidence.Bartley), ct);
            menu = s.Snapshot.Dialog;
        }

        if (menu is null || menu.IsPursuit || !Ours(menu) || !menu.Choices.Any(x => x.Text.Equals(topic, StringComparison.OrdinalIgnoreCase)))
            throw new QuestBlockedException("Bartley's menu has no \"" + topic + "\" option.");
        await s.ExecuteAsync(new("choose", Text: topic, Dialog: menu), ct);
        await Reply(c, menu, ct);
        await ReadThrough(c, d => Ours(d) && d.IsPursuit, ct);
    }

    static async Task Leave(QuestContext c, CancellationToken ct)
    {
        var s = c.Session;
        await Say(c, "Leaving the tutorial");
        if (s.Snapshot.Dialog is { } open)
            await s.ExecuteAsync(new("close", Dialog: open), ct);
        await s.ExecuteAsync(new("navigate", AwakeningEvidence.TutorialMap, AwakeningEvidence.ExitApproach), ct);
        long before = s.Snapshot.Revision;
        await s.ExecuteAsync(new("step", Target: AwakeningEvidence.Exit), ct);
        try
        {
            await s.WaitAsync(x => x.Dialog is { EntityType: not 1 }, before, reply, ct);
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("The tutorial exit at (49,29) didn't show its message.");
        }

        await ReadThrough(c, d => d.EntityType != 1, ct);
        try
        {
            await s.WaitAsync(x => x.Map == AwakeningEvidence.RoomMap && x.Position is not null, before, reply, ct);
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("Answered the tutorial exit, but Mileth Inn Room 0 didn't load.");
        }
    }

    // The popup arrives during the recorded walk; each step is checked before the next.
    static async Task WakeUp(QuestContext c, CancellationToken ct)
    {
        var s = c.Session;
        await Say(c, "Waking up in Mileth Inn Room 0");
        if (AwakeningEvidence.Owned(s.Snapshot, AwakeningEvidence.Dirk) && s.Snapshot.Dialog is null)
            return;
        if (s.Snapshot.Dialog is null)
            foreach (var tile in AwakeningEvidence.WakePath)
            {
                if (s.Snapshot.Position == tile)
                    continue;
                long before = s.Snapshot.Revision;
                await s.ExecuteAsync(new("navigate", AwakeningEvidence.RoomMap, tile), ct);
                try
                {
                    await s.WaitAsync(x => x.Dialog is not null, before, popup, ct);
                    break;
                }
                catch (TimeoutException)
                {
                }
            }

        if (s.Snapshot.Dialog is null)
            throw new QuestBlockedException("The wake-up message didn't appear on the walk to (3,5) in Mileth Inn Room 0.");
        await ReadThrough(c, d => d.EntityType != 1, ct);
    }

    static Task Say(QuestContext c, string reason) => c.SaveAsync(c.Job with
    {
        Reason = reason
    });

    static Task<CharacterSnapshot> Reply(QuestContext c, Dialog d, CancellationToken ct) =>
        c.Session.WaitAsync(x => x.Dialog is null || x.Dialog.Revision > d.Revision || x.Map != c.Session.Snapshot.Map, d.Revision, reply, ct);

    // Reads one conversation to its end: Next on plain pages, the recorded answer on questions; anything else stops.
    static async Task ReadThrough(QuestContext c, Func<Dialog, bool> ours, CancellationToken ct)
    {
        var s = c.Session;
        (ushort Pursuit, ushort Step, string Text)? last = null;
        for (int page = 0; page < MaxPages; page++)
        {
            ct.ThrowIfCancellationRequested();
            if (s.Snapshot.Dialog is not { } d || !ours(d))
                return;
            // The same page sent again after Next is the end of the talk (Raghnall's last page did this).
            if (last == (d.Pursuit, d.Step, d.Text) && d.Choices.Length == 0)
            {
                await s.ExecuteAsync(new("close", Dialog: d), ct);
                return;
            }

            last = (d.Pursuit, d.Step, d.Text);
            if (d.Choices.Length > 0)
            {
                var answer = AwakeningEvidence.Answers.Where(a => d.Text.Contains(a.Question, StringComparison.OrdinalIgnoreCase)).Select(a => a.Answer).FirstOrDefault(a => d.Choices.Any(x => x.Text.Equals(a, StringComparison.OrdinalIgnoreCase)));
                if (answer is null)
                    throw new QuestBlockedException("Unexpected question from " + (d.Name.Length > 0 ? d.Name : "the tutorial") + ": \"" + d.Text + "\"");
                await s.ExecuteAsync(new("choose", Text: answer, Dialog: d), ct);
            }
            else if (d.HasNext)
                await s.ExecuteAsync(new("next", Dialog: d), ct);
            else
            {
                await s.ExecuteAsync(new("close", Dialog: d), ct);
                return;
            }

            try
            {
                await Reply(c, d, ct);
            }
            catch (TimeoutException)
            {
                // A last page that doesn't advance is closed rather than clicked again.
                if (s.Snapshot.Dialog is { } still && still.Revision == d.Revision)
                    await s.ExecuteAsync(new("close", Dialog: still), ct);
                return;
            }
        }

        throw new QuestBlockedException("The conversation ran longer than " + MaxPages + " pages.");
    }
}
