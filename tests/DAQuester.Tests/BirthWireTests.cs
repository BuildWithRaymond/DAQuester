using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Protocol;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;
using Xunit;
namespace DAQuester.Tests;
public class BirthWireTests {
 sealed record Fixture(ReplayEvent[] Events,RecordedAction[] Actions);
 static Fixture Load()=>JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/evidence/datester-7.41/mundane-birth.json"))))!;
 [Fact] public void Actual_Riona_packets_decode_every_stage_and_preserve_live_menu_ids() {
  var f=Load();var tracker=new StateTracker(1834586039707651);tracker.Identity("Awesome",1179711);
  foreach(var e in f.Events){tracker.Observe(e.Opcode,Convert.FromBase64String(e.Payload));Assert.Equal(e.Snapshot.Dialog!.Text,tracker.Snapshot.Dialog!.Text);Assert.Equal(e.Snapshot.Dialog.Step,tracker.Snapshot.Dialog.Step);Assert.Equal(e.Snapshot.Dialog.Prompt,tracker.Snapshot.Dialog.Prompt);Assert.Equal(e.Snapshot.Dialog.Choices.ToArray(),tracker.Snapshot.Dialog.Choices.ToArray());}
  var menu=f.Events.Single(e=>e.Revision==701).Snapshot;
  var request=new ClientMerchantMessage();request.Deserialize(new NetworkPacketReader(DialogPackets.Choice(menu,menu.Dialog!,"Mundane Birth")));
  Assert.Equal(7025u,request.EntityId);Assert.Equal((ushort)1210,request.PursuitId);
 }
 [Theory][InlineData(12)][InlineData(25)][InlineData(40)]
 public void Actual_age_prompt_serializes_text_and_received_step(int age) {
  var prompt=Load().Events.Single(e=>e.Revision==726).Snapshot;var packet=DialogPackets.Continue(prompt,prompt.Dialog!,age.ToString());var m=new ClientPursuitMessage();m.Deserialize(new NetworkPacketReader(packet));
  Assert.Equal(7025u,m.EntityId);Assert.Equal((ushort)186,m.PursuitId);Assert.Equal((ushort)79,m.StepId);Assert.Equal(age.ToString(),Assert.Single(m.TextInputs));
  Assert.Throws<QuestBlockedException>(()=>DialogPackets.Continue(prompt with{Dialog=prompt.Dialog! with{Revision=727}},prompt.Dialog!,age.ToString()));
 }
}
