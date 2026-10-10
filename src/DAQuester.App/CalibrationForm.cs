using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
namespace DAQuester.App;
public sealed class CalibrationForm:ThemedForm {
 readonly IGameSession session;
 readonly ReplayRecorder recorder;
 readonly string path;
 readonly ListBox summary=new DarkListBox(){Dock=DockStyle.Fill};
 readonly HashSet<string> evidence=[];
 public Calibration Result {get;private set;}
 public CalibrationForm(IGameSession session,ReplayRecorder recorder,Calibration initial,string path) {
  this.session=session;this.recorder=recorder;this.path=path;Result=initial;
  Text="Developer tools · Spare a Stick calibration";Size=new(1120,720);StartPosition=FormStartPosition.CenterParent;
  var info=new Label{Text="Auto-calibrate records and labels quest evidence automatically. This panel reviews the saved profile; manual marks are optional overrides. Unknown replies pause execution. Refusal and cooldown samples can accumulate later.",Dock=DockStyle.Top,Height=60,Padding=new(10)};
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new(8)};
  void Button(string text,Action action){var b=Theme.Button(text);b.Width=182;b.Margin=new(3);b.Click+=(_,_)=>{try{action();RefreshSummary();}catch(Exception e){Theme.Error(this,e.Message,"Calibration");}};buttons.Controls.Add(b);}
  Button("Add patrol tile",()=>{
   var s=session.Snapshot;if(s.Map!=500||s.Position is null)throw new InvalidOperationException("Stand on a validated Mileth tree patrol tile.");
   Result=Result with{Patrol=Result.Patrol.Append(s.Position).Distinct().ToArray(),Validated=false};
  });
  Button("Mark accepted",()=>Mark("accept"));
  Button("Mark completed",()=>Mark("complete"));
  Button("Mark refusal",()=>Mark("refusal"));
  Button("Mark cooldown",()=>Mark("cooldown"));
  Button("Capture evidence",()=>{
   if(recorder.Count==0)throw new InvalidOperationException("No server events recorded.");
   var folder=Path.Combine(AppSettings.DataPath,"replays");Directory.CreateDirectory(folder);
   string file=Path.Combine(folder,"calibration-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".json");recorder.Export(file);
   evidence.Add(file);Result=Result with{EvidenceFiles=Result.EvidenceFiles.Concat(evidence).Distinct().ToArray(),Validated=false};
  });
  Button("Validate observed profile",()=>{
   if(!Result.Ready && !(Result.Patrol.Length>0&&Result.Accepted.Length>0&&Result.Completed.Length>0&&Result.EvidenceFiles.Any(File.Exists)))
    throw new InvalidOperationException("Record drop-validated patrol, acceptance, completion and export evidence first.");
   Result=Result with{Validated=true};
  });
  Button("Save calibration",()=>{File.WriteAllText(path,JsonSerializer.Serialize(Result,AppSettings.Json));DialogResult=DialogResult.OK;Close();});
  Button("Reset draft",()=>Result=new(){ServerProfile=initial.ServerProfile});
  Controls.Add(summary);Controls.Add(buttons);Controls.Add(info);Theme.Apply(this);summary.Font=new("Consolas",10);RefreshSummary();
 }
 void Mark(string kind) {
  var s=session.Snapshot;var d=s.Dialog;
  if(d is null||d.Generation!=s.Generation||d.Name!="Callough"||string.IsNullOrWhiteSpace(d.Text))throw new InvalidOperationException("Current Callough server dialog required.");
  string[] Add(string[] values)=>values.Append(d.Text).Distinct().ToArray();
  Result=kind switch {
   "accept"=>Result with{Accepted=Add(Result.Accepted),Validated=false},
   "complete"=>Result with{Completed=Add(Result.Completed),Validated=false,ValidatedCharacters=Result.ValidatedCharacters.Append(s.Name!).Distinct().ToArray()},
   "refusal"=>Result with{Refused=Add(Result.Refused),Validated=false},
   "cooldown"=>Result with{Cooldown=Add(Result.Cooldown),Validated=false},
   _=>Result
  };
 }
 void RefreshSummary() {
  summary.Items.Clear();summary.Items.Add("Profile: "+Result.ServerProfile+" · "+(Result.Ready?"VALIDATED":"draft"));
  summary.Items.Add("Patrol: "+string.Join(" → ",Result.Patrol.Select(x=>$"({x.X},{x.Y})")));
  foreach(var (label,values) in new[]{("Acceptance",Result.Accepted),("Completion",Result.Completed),("Refusal",Result.Refused),("Cooldown",Result.Cooldown),("Evidence",Result.EvidenceFiles)})
   foreach(var text in values)summary.Items.Add(label+": "+text);
  summary.Items.Add("Observed completion characters: "+string.Join(", ",Result.ValidatedCharacters));
 }
}
