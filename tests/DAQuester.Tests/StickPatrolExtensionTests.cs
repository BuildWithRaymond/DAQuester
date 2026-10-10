using System.Text.Json;
using DAQuester.Core;
using Xunit;
namespace DAQuester.Tests;
public class StickPatrolExtensionTests {
 [Fact] public async Task Mileth_patrol_rotates_to_world_map_woodlands_and_stops_at_six_observed_branches() {
  var session=new SimulatedSession();var cal=JsonSerializer.Deserialize<Calibration>("""
   {"Validated":true,"Patrol":[{"X":18,"Y":56},{"X":54,"Y":43},{"X":64,"Y":44}],
    "AdditionalPatrol":[{"Map":600,"Position":{"X":12,"Y":22}},{"Map":600,"Position":{"X":13,"Y":22}}],
    "Accepted":["accepted"],"Completed":["complete"],"EvidenceFiles":["simulation"]}
   """)!;
  session.OnCommand=command=>{
   Assert.True(session.Commands.Count<=4,"Patrol repeated Mileth without visiting East Woodlands.");
   Assert.Equal(new InventoryGoal("Branch",6),command.StopWhen);
   if(command.Map==600)session.Set(session.Snapshot with{Map=600,Position=command.Target,Inventory=session.Snapshot.Inventory.SetItem(1,new(1,"Branch",6))});
  };
  var journal=new FileQuestJournal(Path.Combine(Path.GetTempPath(),"DAQuester-extra-patrol-"+Guid.NewGuid()));
  var context=new QuestContext(session,journal,cal,new(){Character="Nuclear",Accepted=true,AcceptedGeneration=session.Snapshot.Generation,Step="gather"});
  await new StickActions().ExecuteAsync(context,new("gather","gather",null),default);
  Assert.Equal(new[]{500,500,500,600},session.Commands.Select(c=>c.Map!.Value));
  Assert.Equal(6,session.Snapshot.Count("Branch"));Assert.Null(context.Job.CompletedAt);
 }
}
