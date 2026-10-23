namespace DAQuester.Core;

public sealed partial class WizardyResearchActions
{
    static bool AbandonedSetup(QuestJob job, CharacterSnapshot snapshot)
    {
        if (job.Pending is not { Kind: "research-menu" or "research-next", TerminalSeen: false } pending ||
            job.Research is not { Source: { IsPursuit: true, EntityType: 4, Prompt: null } source } progress ||
            progress.Cleanup is not null || progress.SubmittedNext is not null || progress.ResultStatus is not null ||
            progress.Healing is not null || progress.PotionPurchase is not null || progress.RestAt is not null ||
            source.Generation != pending.Generation || source.EntityId != pending.EntityId || source.EntityType != pending.EntityType ||
            source.Revision <= pending.Revision || !snapshot.Connected ||
            !string.Equals(snapshot.Name, job.Character, StringComparison.OrdinalIgnoreCase))
            return false;

        bool known = pending.Kind == "research-next"
            ? progress.Stage is >= 0 and <= 3 && source.HasNext && source.Choices.Length == 0
            : progress.Stage switch
            {
                1 => ResearchChoice(source, WizardyResearchEvidence.Begin) is not null,
                2 => ResearchChoice(source, "Specific") is not null,
                3 => ResearchChoice(source, "sorcery") is not null,
                4 => job.ResearchOptions?.Assistant is not null && ResearchChoice(source, "Assistant") is not null,
                _ => false
            };
        if (!known) return false;

        bool replacedConnection = snapshot.Generation != pending.Generation;
        if (snapshot.Dialog is null)
            return replacedConnection || snapshot.Revision > source.Revision;
        // A fresh opening or explicit rest can replace an abandoned setup. An
        // unfamiliar popup remains untouched, including after reconnect.
        var current = snapshot.Dialog;
        return WizardyResearchEvidence.ResearchPage(snapshot, current) &&
            (replacedConnection || current.Revision > source.Revision) &&
            (ResearchChoice(current, WizardyResearchEvidence.Begin) is not null || current.Text == WizardyResearchEvidence.Cooldown);
    }

    static async Task<bool> RestartAbandonedSetup(QuestContext context)
    {
        if (!AbandonedSetup(context.Job, context.Session.Snapshot)) return false;
        // Setup choices and their Next pages never submit an experiment or
        // spend resources. Discard only their old dialog cursor, not receipts.
        await context.SaveAsync(context.Job with { Pending = null,
            Research = context.Job.Research! with { Stage = 0, Source = null },
            Reason = "Research setup closed or connection changed; restarting from fresh dialogue" });
        return true;
    }
}
