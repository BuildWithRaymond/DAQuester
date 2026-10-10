namespace DAQuester.Core;
public class QuestRunner(IGameSession session,IQuestJournal journal,QuestDefinition recipe,Calibration calibration) {
 public QuestJob Job {get;private set;}=journal.Load(session.Snapshot.Name??"Unknown",recipe.Id);
 private readonly SemaphoreSlim owner=new(1,1);
 private CancellationTokenSource? active;
 private string pauseReason="Paused by player";
 public event Action<QuestJob>? Changed;
 public Dictionary<string,IQuestAction> Actions {get;} = recipe.Id switch {
  "spare-a-stick"=>new[]{"preflight","bank","accept","gather","turn-in","confirm"}.ToDictionary(x=>x,x=>(IQuestAction)new StickActions()),
  "mundane-birth"=>new[]{"birth-preflight","birth-dialog","birth-confirm"}.ToDictionary(x=>x,x=>(IQuestAction)new MundaneBirthActions()),
  "perfect-hairstyle"=>new[]{"hair-preflight","hair-dialog","hair-confirm"}.ToDictionary(x=>x,x=>(IQuestAction)new PerfectHairstyleActions()),
  "entered-muisir-village"=>new[]{"muisir-preflight","muisir-gather","muisir-dialog","muisir-confirm"}.ToDictionary(x=>x,x=>(IQuestAction)new MuisirActions()),
  _=>[]
 };
 public Dictionary<string,Func<QuestContext,bool?>> Conditions {get;}=[];
 public async Task RunAsync(bool repeat=false,CancellationToken cancellation=default) {
  if(!await owner.WaitAsync(0,cancellation))return;
  bool hairstyle=recipe.Id=="perfect-hairstyle";
  repeat=recipe.Repeatable&&(repeat||hairstyle);
  using var token=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
  active=token;pauseReason="Paused by player";
  void Interrupt(string reason){pauseReason=reason;token.Cancel();}
  void Observe(CharacterSnapshot s){if(!s.Connected)Interrupt("Disconnected. Resume after fresh state.");}
  session.Interrupted+=Interrupt;session.Changed+=Observe;
  var c=new QuestContext(session,journal,calibration,Job with{Repeat=repeat});
  async Task Save(QuestJob value){await c.SaveAsync(value);Job=c.Job;Changed?.Invoke(Job);}
  try {
   if(c.Job.PermanentCompletion is not null){await Save(c.Job with{Status="PermanentlyCompleted",Repeat=false,NextRun=null});return;}
   if(recipe.Id=="spare-a-stick")PermanentQuestCompletion.Check(session.Snapshot);
   var missing=recipe.Capabilities.Where(x=>!session.Capabilities.Contains(x)).ToArray();
   if(missing.Length>0)throw new QuestBlockedException("Unsupported capabilities: "+string.Join(", ",missing));
   if(c.Job.Version!=recipe.Version)throw new QuestBlockedException("Recipe version changed. Reconcile saved job before continuing.");
   if(hairstyle&&session.Snapshot.LegendsKnown&&PerfectHairstyleEvidence.HasLegend(session.Snapshot)) {
    await Save(c.Job with{Status="Completed",Step="complete",CompletedAt=c.Job.CompletedAt??DateTimeOffset.UtcNow,LegendReward=true,Repeat=false,NextRun=null,CompletionText=PerfectHairstyleEvidence.Legend,Reason="Suomi legend verified"});return;
   }
   if(c.Job.NextRun is {} next && next>DateTimeOffset.UtcNow) {
    await Save(c.Job with{Status="Scheduled",Reason=hairstyle?"Waiting for seven-day cooldown":"Waiting for eight-hour cooldown"});
    await Task.Delay(next-DateTimeOffset.UtcNow,token.Token);
   }
   if(c.Job.CompletedAt.HasValue&&c.Job.Step=="complete") {
    if(!repeat||hairstyle&&c.Job.LegendReward==true){await Save(c.Job with{Status="Completed",Repeat=hairstyle?false:repeat,NextRun=hairstyle?null:c.Job.NextRun,Reason=c.Job.CompletionText??"One completion verified"});return;}
    await Save(c.Job with{Step="preflight",Accepted=false});
   }
   var activation=recipe.Steps.FirstOrDefault(s=>s.Action=="accept"&&s.Arguments?.GetValueOrDefault("activationScope")=="connection");
   if(activation is not null&&c.Job.Accepted&&c.Job.AcceptedGeneration!=session.Snapshot.Generation) {
    if(c.Job.Pending is not null)throw new QuestBlockedException("Pending transaction requires reconciliation before reconnect activation.");
    await Save(c.Job with{Accepted=false,AcceptedGeneration=null,Step=activation.Id});
   }
   do {
    await Save(c.Job with{Status="Running",Reason=""});
    while(c.Job.Step!="complete") {
     token.Token.ThrowIfCancellationRequested();
     if(recipe.Id=="spare-a-stick")PermanentQuestCompletion.Check(session.Snapshot);
     var step=recipe.Steps.SingleOrDefault(x=>x.Id==c.Job.Step)??throw new QuestBlockedException("Unknown recipe step: "+c.Job.Step);
     if(step.Action is not ("confirm" or "birth-confirm"))foreach(var req in recipe.Requirements.Where(x=>x.Remediation is null)) {
      var check=RequirementEvaluator.Evaluate(session.Snapshot,req);
      if(check.Kind!=EligibilityKind.Eligible)throw new QuestBlockedException(check.Reason);
     }
     if(!Actions.TryGetValue(step.Action,out var action))throw new QuestBlockedException("Unsupported action: "+step.Action);
     await action.ExecuteAsync(c,step,token.Token);
     if(c.Job.Step=="complete")break;
     string? nextStep=step.Next;
     if(step.Transitions is not null)foreach(var branch in step.Transitions) {
      if(!Conditions.TryGetValue(branch.Key,out var test))throw new QuestBlockedException("Unsupported transition condition: "+branch.Key);
      var value=test(c);if(value is null)throw new QuestBlockedException("Transition evidence unknown: "+branch.Key);
      if(value.Value){nextStep=branch.Value;break;}
     }
     await Save(c.Job with{Step=nextStep??"complete"});
    }
    if(c.Job.CompletedAt is null&&!(hairstyle&&c.Job.LastAttemptAt is not null&&c.Job.NextRun is not null))throw new QuestBlockedException("Recipe ended without verified completion.");
    if(!hairstyle||c.Job.LegendReward==true)await Save(c.Job with{Status="Completed",Reason=c.Job.CompletionText??"Completion verified"});
    if(!repeat||!c.Job.Repeat)break;
    await Save(c.Job with{Status="Scheduled",Step=hairstyle?"preflight":c.Job.Step,Reason=hairstyle?"Waiting for seven-day cooldown; Suomi legend still needed":"Waiting for next run"});
    var delay=(c.Job.NextRun??DateTimeOffset.UtcNow)-DateTimeOffset.UtcNow;
    if(delay>TimeSpan.Zero)await Task.Delay(delay,token.Token);
    await Save(c.Job with{Step="preflight"});
   }while(!token.IsCancellationRequested);
  } catch(QuestPermanentlyCompletedException e) {await Save(PermanentQuestCompletion.Record(c.Job,e.Dialog,session.Snapshot));}
    catch(OperationCanceledException) {await Save(c.Job with{Status="Paused",Reason=pauseReason});}
    catch(Exception e) {await Save(c.Job with{Status="Paused",Reason=e.Message});}
  finally {session.Interrupted-=Interrupt;session.Changed-=Observe;active=null;owner.Release();}
 }
 public async Task PauseAsync(string reason="Paused by player") {
  pauseReason=reason;active?.Cancel();
  await owner.WaitAsync();owner.Release();
  if(active is null&&Job.Status!="Paused"&&Job.PermanentCompletion is null) {Job=Job with{Status="Paused",Reason=reason};await journal.SaveAsync(Job);Changed?.Invoke(Job);}
 }
}
