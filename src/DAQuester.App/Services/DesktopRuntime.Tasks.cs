using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed record ActivityRunChoice(WizardyResearchOptions? ResearchOptions, bool Schedule, bool BootRiskAccepted);

public sealed partial class DesktopRuntime
{
    public Func<string, QuestDefinition, QuestJob, Task<ActivityRunChoice?>>? ChooseActivityRunAsync { get; set; }
    public string? TaskBlocker(string name) => name.Length == 0 ? "Add a character to start a task."
        : SchedulerReserves(name) ? "Scheduler owns this character."
        : QueueReserves(name) ? "Pause or stop this character's quest queue first."
        : IsRunning(name) || RunnerBusy(name) || controlActions.ContainsKey(name) ? "This character already has active work."
        : !Snapshot(name).Connected ? "Launch Dark Ages and connect this character first." : null;

    public HelperRow[] ResearchAssistants(string character) => Settings.Characters.Where(n => !n.Equals(character, StringComparison.OrdinalIgnoreCase)).Select(name =>
    {
        var s = Snapshot(name);
        var busy = HelperBusy(name);
        bool login = Preview || s.Connected || CredentialVault.Exists(name);
        bool wizard = CharacterClass(name) is null or "Wizard";
        bool unregistered = s.LegendsKnown && s.Legends.Any(l => l.Contains("((Unregistered))", StringComparison.OrdinalIgnoreCase));
        string detail = busy ?? (!login ? "Save a login in Characters" : !wizard ? "Wizard required" : unregistered ? "Registered wizard required"
            : s.Connected ? "Online · registration checked before assisting" : "Saved login · eligibility checked on arrival");
        return new HelperRow(name, detail, busy is null && login && wizard && !unregistered, null);
    }).ToArray();

    async Task<bool> ConfigureActivityRunAsync(string name, QuestDefinition quest, QuestJob saved, bool resume, bool change)
    {
        bool research = quest.Id == WizardyResearchEvidence.Id;
        if (saved.Pending is not null || research && (saved.Research?.Stage > 0 || saved.Research?.Cleanup is not null)) return true;
        if (research && !change && WizardyResearchEvidence.Eligibility(Snapshot(name)) is { Kind: EligibilityKind.Blocked } blocked)
            throw new QuestBlockedException(blocked.Reason);
        if (!research && (resume || change || !quest.Repeatable || quest.CooldownHours <= 0) || research && resume && !change && saved.ResearchOptions is not null) return true;
        if (ChooseActivityRunAsync is null) return !research || saved.ResearchOptions is not null;
        bool Blocked() => SchedulerReserves(name) || IsRunning(name) || RunnerBusy(name) || controlActions.ContainsKey(name) || !Snapshot(name).Connected;
        if (Blocked()) throw new QuestBlockedException("This character is busy; pause its current work before setup.");
        var before = Snapshot(name);
        var choice = await ChooseActivityRunAsync(name, quest, saved);
        if (choice is null) return false;
        if (Snapshot(name).Generation != before.Generation || Blocked() || Job(name, quest.Id).Pending is not null)
            throw new QuestBlockedException("Character ownership or connection changed while setup was open; choose again after fresh state.");
        if (research)
        {
            if (choice.ResearchOptions is null) return false;
            await SaveSchedulerChoicesAsync(name, quest.Id, new(ResearchOptions: choice.ResearchOptions));
        }
        if (choice.Schedule)
        {
            if (!choice.BootRiskAccepted) throw new QuestBlockedException("Accept the scheduled login risk before saving automatic runs.");
            var old = SchedulerState.Entries.FirstOrDefault(e => e.Character.Equals(name, StringComparison.OrdinalIgnoreCase) && e.Quest == quest.Id &&
                e.Profile == Settings.ServerProfile && e.Status != "Completed");
            await SaveScheduleAsync(name, quest.Id, DateTimeOffset.UtcNow.AddHours(quest.CooldownHours), true, true, old?.Id);
        }
        return true;
    }
}
