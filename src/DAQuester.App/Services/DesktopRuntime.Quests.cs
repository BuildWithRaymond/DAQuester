using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
 public string QuestName(string id) => recipes.TryGetValue(id, out var quest) ? quest.Name : id;
 public QuestAvailability Availability(string name, string id)
 {
  if (!recipes.TryGetValue(id, out var quest)) return new("Unavailable", "Automatic journey unavailable. Open the guide for manual play.", false);
  if (name.Length == 0) return new("Not checked", "Add a character to check requirements", false);
  var job = journal.Load(name, quest.Id); if (runners.TryGetValue(name, out var owner) && owner.Job.Quest == quest.Id) job = owner.Job;
  var remembered = Memory(name); var current = Snapshot(name);
  // A submitted action must reconcile its durable receipt even if a new attempt is now ineligible.
  if (job.Pending is not null) return new(current.Connected ? "Resume ready" : "Not checked", "Resume checks your last action before continuing", remembered.CompletedLegend(quest.Id, current) is null && remembered.SelectionRestriction(quest.Id, current) is null);
  if (remembered.CompletedLegend(quest.Id, current) is { } legend) return new("Completed", "Legend already recorded: " + legend, false);
  if (QuestRunPolicy.Finished(quest, job) && job.Pending is null) return new("Completed", job.CompletionText ?? "Saved goal completed", false);
  if (remembered.SelectionRestriction(quest.Id, current) is { } restriction) return new("Blocked", restriction.Reason, false);
  if (QuestRunPolicy.NextAttempt(quest, job) is { } due && due > DateTimeOffset.UtcNow) return new("Cooldown", $"Next attempt {due.ToLocalTime():MMM d, h:mm tt}", false);
  if (!current.Connected) return new("Not checked", "Connect this character to check requirements", true);
  var snapshot = current;
  if (quest.Id == "mundane-birth" && job.BirthOptions is null) return new("Choices", "Choose season, age, and town before starting", true);
  if (quest.Id == "emerald-dawn" && job.EmeraldOptions is null) return new("Choices", "Choose who receives the insignia before starting", true);
  Eligibility eligibility = quest.Id switch
  {
   "mundane-birth" => MundaneBirthEvidence.Eligibility(snapshot, job.BirthOptions),
   "perfect-hairstyle" => PerfectHairstyleEvidence.Eligibility(snapshot),
   "entered-muisir-village" => MuisirEvidence.Eligibility(snapshot),
   "emerald-dawn" => EmeraldDawnEvidence.Eligibility(snapshot),
   "shinewood-faerie" => ShinewoodFaerieEvidence.Eligibility(snapshot, job),
   _ => StickEligibility.Check(snapshot, job.CompletedAt, DateTimeOffset.UtcNow)
  };
  if (quest.Id == "spare-a-stick" && !Calibration.Ready) return new("Blocked", "Review quest calibration in Developer mode", false);
  return eligibility.Kind == EligibilityKind.Eligible ? new(job.Step is not ("preflight" or "complete") ? "Resume ready" : "Ready", eligibility.Reason, true) : new(eligibility.Kind == EligibilityKind.Unknown ? "Not checked" : "Blocked", PlayerReason(eligibility.Reason), eligibility.Kind == EligibilityKind.Unknown);
 }
 public bool RepeatEnabled(string name, string id) => recipes.TryGetValue(id, out var quest) && quest.Repeatable && repeatChoices.GetValueOrDefault((name.ToUpperInvariant(), id), journal.Load(name, id).Repeat);
 public async Task SetRepeatAsync(string name, string id, bool repeat)
 {
  RequireOpen();
  if (RunnerBusy(name)) throw new QuestBlockedException("A quest is already preparing for this character.");
  var quest = recipes[id]; if (QueueReserves(name) || IsRunning(name) || controlActions.ContainsKey(name)) throw new QuestBlockedException("Stop this character's active operation before changing repeat settings.");
  using var preparation = new CancellationTokenSource(); runnerPreparations.Add(name, preparation); Notify();
  try
  {
   var owner = runners.GetValueOrDefault(name);
   if (owner?.Job.Quest == id) { await owner.PauseAsync("Repeat setting changed"); if (runners.GetValueOrDefault(name) != owner) throw new QuestBlockedException("Another quest now owns this character."); }
   RequireOpen(); preparation.Token.ThrowIfCancellationRequested();
   var saved = owner?.Job.Quest == id ? owner.Job : journal.Load(name, id); bool enabled = quest.Repeatable && repeat && !QuestRunPolicy.Finished(quest, saved);
   await journal.SaveAsync(saved with { Repeat = enabled }); repeatChoices[(name.ToUpperInvariant(), id)] = enabled;
   if (owner?.Job.Quest == id && runners.GetValueOrDefault(name) == owner) runners.Remove(name);
   Notify();
  }
  finally { runnerPreparations.Remove(name); Notify(); }
 }
 public async Task StartQuestAsync(string name, string id, bool resume = false)
 {
  RequireOpen();
  if (Preview) throw new InvalidOperationException("Preview does not run game quests.");
  if (RunnerBusy(name)) throw new QuestBlockedException("A quest is already preparing for this character.");
  if (QueueReserves(name)) throw new QuestBlockedException("Stop the queue before starting a separate quest.");
  var selected = recipes[id]; if (!await ConfigureQuestAsync(name, selected, resume)) return;
  RequireOpen();
  if (QueueReserves(name)) throw new QuestBlockedException("A queue now owns this character. Stop it first.");
  var runner = await PrepareRunnerAsync(name, selected);
  await runner.RunAsync(RepeatEnabled(name, id), startNewAttempt: !resume);
 }
 public async Task PauseQuestAsync(string name, string reason)
 {
  if (QueueReserves(name)) { await PauseQueueAsync(name, reason.StartsWith("Stopped", StringComparison.Ordinal)); return; }
  if (runnerPreparations.TryGetValue(name, out var preparing)) preparing.Cancel();
  await WaitForRunnerPreparationsAsync(name);
  CancelControl(name); if (runners.TryGetValue(name, out var runner)) await runner.PauseAsync(reason); if (Preview) previewRunning.Remove(name); Notify();
 }
 static bool EmeraldChoicesLocked(QuestJob job) => job.Pending is not null || job.CompletedAt is not null || job.EmeraldMioasSeen || job.EmeraldBergBriefed || job.EmeraldDruidSeen || job.EmeraldRecovery is not null;
 async Task<bool> ConfigureQuestAsync(string name, QuestDefinition selected, bool resume)
 {
  if (RunnerBusy(name)) throw new QuestBlockedException("A quest is already preparing for this character.");
  if (!sessions.TryGetValue(name, out var session) || !session.Snapshot.Connected) throw new QuestBlockedException("Launch Dark Ages and log in as " + name + " first.");
  if (controlActions.ContainsKey(name)) throw new QuestBlockedException("A local control action owns this character. Stop it first.");
  long generation = session.Snapshot.Generation;
  var saved = runners.TryGetValue(name, out var existing) && existing.Job.Quest == selected.Id ? existing.Job : journal.Load(name, selected.Id);
  bool birth = selected.Id == "mundane-birth" && saved.Pending is null && saved.CompletedAt is null && (!resume || saved.BirthOptions is null);
  bool emerald = selected.Id == "emerald-dawn" && EmeraldDawnEvidence.FinalMark(session.Snapshot) is null && !EmeraldChoicesLocked(saved) && (!resume || saved.EmeraldOptions is null);
  bool walk = selected.Id == ShinewoodFaerieEvidence.Id && saved.Pending?.Kind.StartsWith("faerie-return", StringComparison.Ordinal) != true && ShinewoodFaerieEvidence.Escape(session.Snapshot) is null && (ShinewoodFaerieEvidence.Goals(session.Snapshot, saved).Length > 0 || ShinewoodFaerieEvidence.InForest(session.Snapshot.Map));
  QuestChoiceResult? choice = null;
  if (birth || emerald || walk)
  {
   string? error = null; if (walk) try { session.ValidateFaerieReturn(); } catch (Exception e) { error = e.Message; }
   if (ChooseQuestAsync is null) return false;
   choice = await ChooseQuestAsync(name, selected, saved, error); if (choice is null) return false;
   RequireOpen();
   // Choices can remain open across reconnects or a new local owner.
   if (sessions.GetValueOrDefault(name) != session || !session.Snapshot.Connected || session.Snapshot.Generation != generation) throw new QuestBlockedException("Character connection changed. Choose again after fresh state.");
   if (controlActions.ContainsKey(name)) throw new QuestBlockedException("Another action owns this character.");
   if (birth && choice.BirthOptions is null || emerald && choice.EmeraldOptions is null || walk && !choice.AllowUnsafeWalking) return false;
  }
  RequireOpen();
  if (RunnerBusy(name)) throw new QuestBlockedException("A quest is already preparing for this character.");
  var job = runners.TryGetValue(name, out var owner) && owner.Job.Quest == selected.Id ? owner.Job : journal.Load(name, selected.Id);
  bool repeatChanged = repeatChoices.TryGetValue((name.ToUpperInvariant(), selected.Id), out bool repeatChoice) && job.Repeat != (selected.Repeatable && repeatChoice);
  if (choice is null && !repeatChanged) return true;
  using var preparation = new CancellationTokenSource(); runnerPreparations.Add(name, preparation); Notify();
  try
  {
   if (owner is not null && owner.Job.Quest == selected.Id)
   {
    if (owner.IsRunning) throw new QuestBlockedException("This quest already runs for " + name + ". Pause before changing choices.");
    await owner.PauseAsync("Quest choices saved");
    if (runners.GetValueOrDefault(name) != owner) throw new QuestBlockedException("Another quest now owns this character.");
    job = owner.Job;
   }
   RequireOpen(); preparation.Token.ThrowIfCancellationRequested();
   if (sessions.GetValueOrDefault(name) != session || !session.Snapshot.Connected || session.Snapshot.Generation != generation) throw new QuestBlockedException("Character connection changed. Choose again after fresh state.");
   if (controlActions.ContainsKey(name)) throw new QuestBlockedException("Another action owns this character.");
   if (repeatChanged) job = job with { Repeat = selected.Repeatable && repeatChoice };
   if (choice?.BirthOptions is { } options) { options.Validate(); if (job.Pending is not null || job.CompletedAt is not null) throw new QuestBlockedException("Saved birth submission cannot change choices."); job = job with { BirthOptions = options, Repeat = false, NextRun = null }; }
   if (choice?.EmeraldOptions is { } emeraldOptions) { emeraldOptions.Validate(); if (EmeraldChoicesLocked(job) && job.EmeraldOptions != emeraldOptions) throw new QuestBlockedException("Saved Emerald Dawn choices cannot change after the journey begins."); job = job with { EmeraldOptions = emeraldOptions, Repeat = false, NextRun = null }; }
   if (choice?.AllowUnsafeWalking == true) { session.ValidateFaerieReturn(); job = job with { FaerieWalkAcknowledgedGeneration = session.Snapshot.Generation, FaerieWalkAcknowledgedAt = DateTimeOffset.UtcNow }; }
   await journal.SaveAsync(job);
   if (owner is not null && owner.Job.Quest == selected.Id && runners.GetValueOrDefault(name) == owner) runners.Remove(name);
   return true;
  }
  finally { runnerPreparations.Remove(name); Notify(); }
 }
 async Task<QuestRunner> PrepareRunnerAsync(string name, QuestDefinition selected, MundaneBirthOptions? options = null, EmeraldDawnOptions? emeraldOptions = null, bool allowUnsafeWalking = false)
 {
  RequireOpen();
  if (RunnerBusy(name)) throw new QuestBlockedException("A quest is already preparing for this character.");
  using var preparation = new CancellationTokenSource(); runnerPreparations.Add(name, preparation); Notify();
  try
  {
   if (controlActions.ContainsKey(name)) throw new QuestBlockedException("A local action owns this character. Stop it first.");
   if (!sessions.TryGetValue(name, out var session) || !session.Snapshot.Connected) throw new QuestBlockedException("Launch Dark Ages and log in as " + name + " first.");
   long generation = session.Snapshot.Generation;
   runners.TryGetValue(name, out var old);
   if (old?.Job.Quest == selected.Id && old.IsRunning) throw new QuestBlockedException("This quest already runs for " + name + ". Pause before changing choices.");
   var saved = old?.Job.Quest == selected.Id ? old.Job : journal.Load(name, selected.Id);
   if (selected.Id == ShinewoodFaerieEvidence.Id && saved.Pending?.Kind.StartsWith("faerie-return", StringComparison.Ordinal) != true && (ShinewoodFaerieEvidence.Goals(session.Snapshot, saved).Length > 0 || ShinewoodFaerieEvidence.InForest(session.Snapshot.Map)) && ShinewoodFaerieEvidence.Escape(session.Snapshot) is null)
   {
    bool acknowledged = saved.FaerieWalkAcknowledgedGeneration == session.Snapshot.Generation && (saved.CompletedAt is null || saved.FaerieWalkAcknowledgedAt > saved.CompletedAt);
    if (!allowUnsafeWalking && !acknowledged) ShinewoodFaerieEvidence.RequireEscape(session.Snapshot, saved with { FaerieWalkAcknowledgedGeneration = null });
    session.ValidateFaerieReturn(); if (allowUnsafeWalking) saved = saved with { FaerieWalkAcknowledgedGeneration = session.Snapshot.Generation, FaerieWalkAcknowledgedAt = DateTimeOffset.UtcNow };
   }
   if (selected.Id == "mundane-birth")
   {
    if (options is not null) { options.Validate(); if ((saved.Pending is not null || saved.CompletedAt is not null) && saved.BirthOptions != options) throw new QuestBlockedException("Saved birth submission cannot change choices. Inspect the legend before reconciliation."); }
    options ??= saved.BirthOptions; if (options is null) throw new QuestBlockedException("Birth choices required: season, age 12-40, and town."); options.Validate();
   }
   if (selected.Id == "emerald-dawn")
   {
    emeraldOptions?.Validate(); if (emeraldOptions is not null && EmeraldChoicesLocked(saved) && saved.EmeraldOptions != emeraldOptions) throw new QuestBlockedException("Saved Emerald Dawn choices cannot change after the journey begins. Resume to reconcile the saved outcome.");
    emeraldOptions ??= saved.EmeraldOptions; if (emeraldOptions is null && EmeraldDawnEvidence.FinalMark(session.Snapshot) is null) throw new QuestBlockedException("Emerald Dawn choice required: Emerald Knights, Necromancer Order, or sell to Skan."); emeraldOptions?.Validate();
   }
   if (old is not null && (old.Job.Quest != selected.Id || options is not null && old.Job.BirthOptions != options || emeraldOptions is not null && old.Job.EmeraldOptions != emeraldOptions || selected.Id == ShinewoodFaerieEvidence.Id && allowUnsafeWalking))
   {
    await old.PauseAsync("Another quest or birth choices selected; progress saved");
    if (runners.GetValueOrDefault(name) == old) runners.Remove(name);
    else if (runners.ContainsKey(name)) throw new QuestBlockedException("Another quest now owns this character.");
    old = null;
   }
   calibrationRuns.Remove(name); if (selected.Id == ShinewoodFaerieEvidence.Id && allowUnsafeWalking) await journal.SaveAsync(saved);
   RequireOpen();
   preparation.Token.ThrowIfCancellationRequested();
   if (sessions.GetValueOrDefault(name) != session || !session.Snapshot.Connected || session.Snapshot.Generation != generation) throw new QuestBlockedException("Character connection changed. Resume after fresh state.");
   if (old is not null) { if (runners.GetValueOrDefault(name) != old) throw new QuestBlockedException("Another quest now owns this character."); return old; }
   if (selected.Id == "mundane-birth" && options is not null) await journal.SaveAsync(saved with { BirthOptions = options, Repeat = false, NextRun = null });
   if (selected.Id == "emerald-dawn" && emeraldOptions is not null) await journal.SaveAsync(saved with { EmeraldOptions = emeraldOptions, Repeat = false, NextRun = null });
   RequireOpen();
   preparation.Token.ThrowIfCancellationRequested();
   if (sessions.GetValueOrDefault(name) != session || !session.Snapshot.Connected || session.Snapshot.Generation != generation) throw new QuestBlockedException("Character connection changed. Resume after fresh state.");
   if (runners.ContainsKey(name)) throw new QuestBlockedException("Another quest now owns this character.");
   var runner = new QuestRunner(session, journal, selected, Calibration); runners[name] = runner;
   runner.Changed += job => Ui(() => { Notify(); Log(name + ": " + QuestName(job.Quest) + " · " + job.Step + " · " + job.Status + " " + job.Reason); }); Notify(); return runner;
  }
  finally { runnerPreparations.Remove(name); Notify(); }
 }
 async Task WaitForRunnerPreparationsAsync(string? character) { while (runnerPreparations.Keys.Any(name => character is null || name.Equals(character, StringComparison.OrdinalIgnoreCase))) await Task.Delay(20); }
}
