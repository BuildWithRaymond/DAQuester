using Xunit;
using System.Collections.Immutable;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
namespace DAQuester.Tests;
public class AutomaticEvidenceTests {
 static CharacterSnapshot S(long revision,int branches,uint xp=0,string? reply=null)=>FoundationTests.Ready() with{Revision=revision,Experience=xp,Inventory=branches==0?ImmutableDictionary<byte,Item>.Empty:ImmutableDictionary<byte,Item>.Empty.Add(1,new(1,"Branch",(uint)branches)),Dialog=reply is null?null:new(1,revision,99,1,"Callough",reply,true,7,0,0,[])};
 static ReplayEvent E(CharacterSnapshot s)=>new(DateTimeOffset.UtcNow,"Nuclear",s.Generation,s.Revision,0x0F,"",s);
 [Fact] public void Drop_marks_region_evidence_without_claiming_guaranteed_tile() {
  var result=QuestEvidenceAnalyzer.Analyze([E(S(1,0)),E(S(2,1))],[]);
  var drop=Assert.Single(result.Labels,l=>l.Kind=="branch-increase");Assert.Equal(new Tile(20,20),drop.Tile);Assert.Equal(1,drop.BranchDelta);Assert.Equal("observed-position",drop.Confidence);
 }
 [Fact] public void Completion_needs_actual_turnin_and_correlated_outcome() {
  var action=new RecordedAction(DateTimeOffset.UtcNow,"Nuclear",1,1,"choose","I have those branches, here",S(1,6).Dialog,6,10);
  var report=QuestEvidenceAnalyzer.Analyze([E(S(1,6,10)),E(S(2,0,10,"Well done!")),E(S(3,0,3510))],[action]);
  Assert.Contains(report.Labels,l=>l.Kind=="completion"&&l.Text=="Well done!");
  Assert.DoesNotContain(QuestEvidenceAnalyzer.Analyze([E(S(1,6,10)),E(S(2,6,3510,"Well done!"))],[action]).Labels,l=>l.Kind=="completion");
 }
 [Fact] public void Other_character_or_reconnect_cannot_supply_completion() {
  var a=new RecordedAction(DateTimeOffset.UtcNow,"Nuclear",1,1,"choose","I have those branches, here",null,6,10);
  var report=QuestEvidenceAnalyzer.Analyze([E(S(2,0,3510,"Done") with{Generation=2}),E(S(3,0,3510,"Done") with{Name="Clock"})],[a]);
  Assert.DoesNotContain(report.Labels,l=>l.Kind=="completion");
 }
 [Fact] public void Acceptance_label_requires_later_branch_or_completion_evidence() {
  var a=new RecordedAction(DateTimeOffset.UtcNow,"Nuclear",1,1,"choose","I'll gather them now",null,0,10);
  Assert.DoesNotContain(QuestEvidenceAnalyzer.Analyze([E(S(2,0,10,"Good luck"))],[a]).Labels,l=>l.Kind=="acceptance");
  Assert.Contains(QuestEvidenceAnalyzer.Analyze([E(S(2,0,10,"Good luck")),E(S(3,1,10))],[a]).Labels,l=>l.Kind=="acceptance"&&l.Text=="Good luck");
 }
}
