using DAQuester.Core;
using Xunit;
namespace DAQuester.Tests;

public class QuestCompletionDialogTests {
 [Theory][InlineData("npc")][InlineData("generation")][InlineData("text")][InlineData("choice")]
 public async Task Different_popup_after_reward_is_preserved_without_a_dialog_command(string difference){
  var session=new SimulatedSession();var receipt=new Dialog(1,1,77,1,"Eeva",PerfectHairstyleEvidence.Completion,true,731,20,0,[]);
  var popup=difference switch{"npc"=>receipt with{EntityId=88},"generation"=>receipt with{Generation=2},"text"=>receipt with{Text="Unfamiliar page"},_=>receipt with{Choices=[new("Do something",731,1)]}};
  session.Set(session.Snapshot with{Dialog=popup});
  var journal=new FileQuestJournal(Path.Combine(Path.GetTempPath(),"DAQuester-dialog-"+Guid.NewGuid()));
  var context=new QuestContext(session,journal,new(),new(){Character="Nuclear",Quest="perfect-hairstyle"});
  await Assert.ThrowsAsync<QuestBlockedException>(()=>QuestCompletionDialogs.DismissAsync(context,receipt,default));Assert.Same(popup,session.Snapshot.Dialog);Assert.Empty(session.Commands);
 }
}
