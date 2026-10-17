using System.Diagnostics;
using DAQuester.Core;
using DAQuester.DarkAges;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    readonly QuestScheduler scheduler;
    string? schedulerCharacter;
    string? schedulerQuest;
    string? schedulerPhase;
    public string? SchedulerQuestId(string name) => string.Equals(schedulerCharacter, name, StringComparison.OrdinalIgnoreCase) ? schedulerQuest : null;
    public string? SchedulerRunPhase(string name) => SchedulerQuestId(name) is not null ? schedulerPhase ?? "Running" : null;

    void SetSchedulerPhase(string phase)
    {
        schedulerPhase = phase;
        Notify();
    }
    readonly HashSet<string> schedulerHelpers = new(StringComparer.OrdinalIgnoreCase);
    public bool SchedulerEnabled => scheduler.State.Enabled;
    public QuestSchedulerState SchedulerState => scheduler.State;
    string SchedulerConnection => Settings.RemoteAddress + ":" + string.Join(",", Settings.RemotePorts);
    public bool SchedulerReserves(string name) => name.Length > 0 && (string.Equals(schedulerCharacter, name, StringComparison.OrdinalIgnoreCase) || schedulerHelpers.Contains(name));
    public bool SchedulerLaunchInProgress(string name) => SchedulerReserves(name) && schedulerPhase is not null
        && SchedulerState.PendingLogins.Contains(name, StringComparer.OrdinalIgnoreCase)
        && !SchedulerState.LoginBlocks.Any(b => b.Character.Equals(name, StringComparison.OrdinalIgnoreCase));
    void RequireNoScheduler(string name)
    {
        if (SchedulerReserves(name))
            throw new QuestBlockedException("Scheduler owns this character. Pause its scheduled run and wait for progress to save first.");
    }

    public async Task SetSchedulerEnabledAsync(bool enabled, bool acknowledged)
    {
        RequireOpen();
        await scheduler.SetEnabledAsync(enabled, acknowledged);
        if (!enabled)
            await scheduler.WaitForIdleAsync();
    }

    public async Task TickSchedulerAsync()
    {
        if (Preview || !Ready || closing || shuttingDown)
            return;
        try { await scheduler.TickAsync(DateTimeOffset.UtcNow); }
        catch (Exception error) { Log("Scheduler stopped: " + error.Message); }
    }

    public DateTimeOffset SchedulerDue(string name, string id, DateTimeOffset requested)
    {
        var job = Job(name, id);
        var deadline = QuestRunPolicy.NextAttempt(recipes[id], job);
        if (id == AltarWorshipEvidence.Id && AltarWorshipEvidence.NextOffering(job) is { } offering)
            deadline = offering;
        if (SickChildEvidence.For(id) is not null && job.NextRun is { } flower)
            deadline = flower;
        if (id == FiskEvidence.Id && job.FiskWait is { Verified: true } bouquet)
            deadline = job.NextRun ?? bouquet.Due;
        return deadline is { } next && next > requested ? next : requested;
    }

    public bool SchedulerNeedsChoices(string name, string id)
    {
        var job = Job(name, id);
        return id == "mundane-birth" && job.BirthOptions is null
            || id == "emerald-dawn" && job.EmeraldOptions is null
            || id == WizardyResearchEvidence.Id && job.ResearchOptions is null
            || SickChildEvidence.For(id) is not null && job.SickChildOptions is null
            || id == AltarWorshipEvidence.Id && !job.AltarNoMarkAccepted && !AltarWorshipEvidence.IsPriest(CharacterClass(name))
            || id == NewCharacterPath.FirstStepsId && !job.FirstStepsBriefed
            || id == NewCharacterPath.ClimbId && !job.TerrorWindowAccepted;
    }

    public async Task SaveSchedulerChoicesAsync(string name, string id, QuestChoiceResult choice)
    {
        RequireOpen();
        RequireNoScheduler(name);
        if (QueueReserves(name) || IsRunning(name) || RunnerBusy(name) || controlActions.ContainsKey(name))
            throw new QuestBlockedException("Stop this character's active operation before saving quest choices.");
        var job = Job(name, id);
        if (job.Pending is not null || id == "mundane-birth" && job.CompletedAt is not null || id == "emerald-dawn" && EmeraldChoicesLocked(job))
            throw new QuestBlockedException("Saved submission owns its choices. Review it before changing them.");
        if (choice.BirthOptions is { } birth) { birth.Validate(); job = job with { BirthOptions = birth }; }
        if (choice.EmeraldOptions is { } emerald) { emerald.Validate(); job = job with { EmeraldOptions = emerald }; }
        if (choice.ResearchOptions is { } research)
        {
            if (id != WizardyResearchEvidence.Id) throw new QuestBlockedException("Research choices belong to the research task.");
            research.Validate(name);
            if (job.Research?.Stage > 0 || job.Research?.Cleanup is not null)
                throw new QuestBlockedException("Saved research progress owns its assistant choice; resume it first.");
            if (research.Assistant is { } helper && (!Settings.Characters.Contains(helper, StringComparer.OrdinalIgnoreCase) || !Preview && !Snapshot(helper).Connected && !CredentialVault.Exists(helper)))
                throw new QuestBlockedException("The research assistant needs a saved character and saved login.");
            job = job with { ResearchOptions = research };
        }
        if (choice.DanaanOptions is { } danaan)
        {
            danaan.Validate();
            if (DanaanEvidence.ChoicesLocked(job) && job.DanaanOptions != danaan)
                throw new QuestBlockedException("Saved Danaan progress owns its route and rite consent.");
            job = job with { DanaanOptions = danaan };
        }
        if (choice.SickChildOptions is { } sick)
        {
            sick.Validate();
            if (sick.Mode == SickChildOptions.WithHelpers && sick.Helpers.Any(h => !Settings.Characters.Contains(h, StringComparer.OrdinalIgnoreCase) || !Preview && !CredentialVault.Exists(h)))
                throw new QuestBlockedException("Every scheduled helper needs a saved character and saved login.");
            job = job with { SickChildOptions = sick };
        }
        if (choice.AltarNoMarkAccepted)
            job = job with { AltarNoMarkAccepted = true };
        if (choice.FirstStepsBriefed)
            job = job with { FirstStepsBriefed = true };
        if (choice.TerrorWindowAccepted)
            job = job with { TerrorWindowAccepted = true };
        await journal.SaveAsync(job);
        runners.Remove(name);
        Notify();
    }

    void ValidateSchedule(string name, string id, bool recurring)
    {
        RequireOpen();
        if (!Settings.Characters.Contains(name, StringComparer.OrdinalIgnoreCase) || !name.All(char.IsAsciiLetter))
            throw new QuestBlockedException("Choose a saved character from Characters.");
        if (!Preview && !CredentialVault.Exists(name))
            throw new QuestBlockedException("Save this character's login in Characters before scheduling it.");
        if (!recipes.TryGetValue(id, out var quest))
            throw new QuestBlockedException("This quest has a guide only. An automatic journey is required to schedule it.");
        if (recurring && !quest.Repeatable)
            throw new QuestBlockedException("Schedule this quest once; timed progress resumes automatically.");
        if (SchedulerNeedsChoices(name, id))
            throw new QuestBlockedException("Save the quest's choices before arming its schedule.");
        if (id == WizardyResearchEvidence.Id && Job(name, id).ResearchOptions?.Assistant is { } assistant && !Preview && !CredentialVault.Exists(assistant))
            throw new QuestBlockedException("Save the research assistant's login before scheduling it.");
        if (QuestRunPolicy.Finished(quest, Job(name, id)) || Memory(name).CompletedLegend(id, Snapshot(name)) is not null)
            throw new QuestBlockedException("This quest's goal is already completed.");
    }

    public async Task SaveScheduleAsync(string name, string id, DateTimeOffset requested, bool recurring, bool acknowledged, Guid? edit = null)
    {
        ValidateSchedule(name, id, recurring);
        var entry = new QuestSchedule { Character = name, Quest = id, Profile = Settings.ServerProfile, Connection = SchedulerConnection, RecipeVersion = recipes[id].Version, Due = SchedulerDue(name, id, requested), Recurring = recurring };
        if (edit is { } key)
            await scheduler.EditAsync(entry with { Id = key }, acknowledged);
        else
            await scheduler.AddAsync(entry, acknowledged);
    }

    public Task PauseScheduleAsync(Guid id) => scheduler.PauseAsync(id);
    public Task RemoveScheduleAsync(Guid id) => scheduler.RemoveAsync(id);
    public Task ReviewSchedulerLoginAsync(string name, bool acknowledged) => scheduler.ReviewLoginAsync(name, acknowledged);
    public Task RearmScheduleAsync(Guid id, bool acknowledged)
    {
        var entry = SchedulerState.Entries.Single(e => e.Id == id);
        ValidateSchedule(entry.Character, entry.Quest, entry.Recurring);
        if (entry.Profile != Settings.ServerProfile || entry.Connection != SchedulerConnection || entry.RecipeVersion != recipes[entry.Quest].Version)
            throw new QuestBlockedException("Profile or recipe changed. Review and edit this schedule first.");
        return scheduler.RearmAsync(id, acknowledged, SchedulerDue(entry.Character, entry.Quest, DateTimeOffset.UtcNow));
    }

    async Task<ScheduledQuestResult> ExecuteScheduledQuestAsync(QuestSchedule entry, CancellationToken cancellation)
    {
        if (Preview)
            throw new QuestBlockedException("Preview never launches game clients or runs quests.");
        RequireOpen();
        if (entry.Profile != Settings.ServerProfile || entry.Connection != SchedulerConnection || !recipes.TryGetValue(entry.Quest, out var quest) || entry.RecipeVersion != quest.Version)
            throw new QuestBlockedException("Profile or recipe changed. Review this schedule.");
        string name = entry.Character;
        if (QuestRunPolicy.Finished(quest, Job(name, entry.Quest)) || Memory(name).CompletedLegend(entry.Quest, Snapshot(name)) is not null)
            return new();
        ValidateSchedule(name, entry.Quest, entry.Recurring);
        if (Snapshot(name).Connected || OwnsLiveClient(name))
            throw new QuestBlockedException("This character's game window is already open. Scheduler will not take it over; close it and explicitly re-enable this schedule.");
        if (QueueReserves(name) || IsRunning(name) || RunnerBusy(name) || controlActions.ContainsKey(name) || LaunchPending(name) || calibrationTasks.TryGetValue(name, out var capture) && !capture.IsCompleted)
            throw new QuestBlockedException("This character is busy. Review its schedule after the active operation ends.");
        if (scheduler.LoginBlock(name) is { } block)
            throw new ScheduledLoginException(name, block);
        var due = SchedulerDue(name, entry.Quest, DateTimeOffset.UtcNow);
        if (due > DateTimeOffset.UtcNow)
            return new(due, entry.Resume);

        schedulerCharacter = name;
        schedulerQuest = entry.Quest;
        schedulerPhase = "Launching";
        Process? client = null;
        GameSession? session = null;
        using var cancelLaunch = cancellation.Register(() => Ui(() => CancelLaunches(name)));
        Notify();
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var launch = LaunchOwnedCharacterAsync(name, false, scheduled: true);
            launchTasks[name] = launch;
            await launch;
            client = ownedClients.GetValueOrDefault(name);
            for (int wait = 0; !(sessions.TryGetValue(name, out session) && session.Snapshot.Connected); wait++)
            {
                if (wait >= 200)
                    throw new QuestBlockedException("Login completed but fresh character state was not ready. Review before trying again.");
                await Task.Delay(100, cancellation);
            }
            SetSchedulerPhase("Preparing");
            var snapshot = session!.Snapshot;
            if (!snapshot.InventoryKnown || snapshot.Level is null || snapshot.Hp is null)
                await session.WaitAsync(s => s.InventoryKnown && s.Level is not null && s.Hp is not null, snapshot.Revision - 1, TimeSpan.FromSeconds(20), cancellation);
            if (!session.Snapshot.LegendsKnown)
                await session.ExecuteAsync(new("inspect-self"), cancellation);
            cancellation.ThrowIfCancellationRequested();
            var runner = await PrepareRunnerCoreAsync(name, quest, scheduled: true);
            var currentDue = SchedulerDue(name, entry.Quest, DateTimeOffset.UtcNow);
            if (currentDue > DateTimeOffset.UtcNow && runner.Job.Pending is null)
                return new(currentDue, entry.Resume);
            await scheduler.MarkQuestStartedAsync(entry.Id);
            SetSchedulerPhase("Running");
            await runner.RunAsync(false, cancellation, startNewAttempt: QuestRunPolicy.StartNewScheduledAttempt(quest, runner.Job, entry), singleAttempt: true, yieldScheduledWaits: true);
            cancellation.ThrowIfCancellationRequested();
            var job = runner.Job;
            if (entry.Quest == WizardyResearchEvidence.Id && job.Pending is null && job.Research?.Cleanup is null && job.Research?.RestAt is not null && job.NextRun is { } rest)
                return entry.Recurring ? new(rest, false) : new();
            if (job.Status == "Scheduled" && job.Pending is null && job.NextRun is { } next)
                return new(next, true);
            if (QueueAttemptOutcome.Evaluate(job) is null)
                throw new QuestBlockedException(QueueAttemptOutcome.BlockReason(job));
            if (!entry.Recurring || QuestRunPolicy.Finished(quest, job))
                return new();
            return new(QuestRunPolicy.NextAttempt(quest, job) ?? throw new QuestBlockedException("Next attempt has no verified deadline."));
        }
        finally
        {
            SetSchedulerPhase("Closing");
            client ??= ownedClients.GetValueOrDefault(name);
            try
            {
                if (client is not null)
                    await CloseSchedulerClientAsync(name, client, session);
            }
            finally
            {
                schedulerCharacter = null;
                schedulerQuest = null;
                schedulerPhase = null;
                Notify();
            }
        }
    }

    async Task CloseSchedulerClientAsync(string name, Process client, IGameSession? session)
    {
        if (!ReferenceEquals(ownedClients.GetValueOrDefault(name), client))
            throw new QuestBlockedException("Game window ownership changed. Scheduler left it open for review.");
        if (!client.HasExited)
        {
            client.CloseMainWindow();
            using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await client.WaitForExitAsync(grace.Token); }
            catch (OperationCanceledException)
            {
                if (!ReferenceEquals(ownedClients.GetValueOrDefault(name), client))
                    throw new QuestBlockedException("Game window ownership changed during logout. Review before running again.");
                client.Kill();
                await client.WaitForExitAsync();
            }
        }
        if (session is not null)
        {
            for (int wait = 0; session.Snapshot.Connected && wait < 100; wait++)
                await Task.Delay(100);
            if (session.Snapshot.Connected)
                throw new QuestBlockedException("Logout was not confirmed. Review before another scheduled login.");
        }
        AutoExport(name + " scheduled run ended");
        Log(name + ": scheduler closed its game window; progress saved.");
    }
}
