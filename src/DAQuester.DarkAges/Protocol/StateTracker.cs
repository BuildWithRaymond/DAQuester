using DAQuester.Core;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;
using System.Collections.Immutable;
namespace DAQuester.DarkAges.Protocol;
public class StateTracker(long generation) {
 public CharacterSnapshot Snapshot {get;private set;}=new(){Generation=generation};
 public ushort Width {get;private set;}
 public ushort Height {get;private set;}
 public ServerFieldMapMessage? WorldMap {get;private set;}
 public long LastMovementRevision {get;private set;}
 public long LastPositionRevision {get;private set;}
 public event Action<CharacterSnapshot>? Changed;
 public event Action<byte,byte[]>? Observed;
 public void Identity(string name,uint id) => Publish(Snapshot with{Name=name,Id=id,Connected=true});
 public void Disconnect()=>Publish(Snapshot with{Connected=false,Dialog=null,InventoryKnown=false,LegendsKnown=false,Notice=null,GroundItems=ImmutableDictionary<uint,GroundItem>.Empty});
 void Publish(CharacterSnapshot s) {Snapshot=s with{Revision=Snapshot.Revision+1};Changed?.Invoke(Snapshot);}
 public void Observe(byte opcode,byte[] payload) {
  if(opcode is not (0x04 or 0x05 or 0x07 or 0x08 or 0x0A or 0x0B or 0x0C or 0x0E or 0x0F or 0x10 or 0x15 or 0x17 or 0x18 or 0x2C or 0x2D or 0x2E or 0x2F or 0x30 or 0x33 or 0x37 or 0x38 or 0x39 or 0x58))return;
  // Failed decodes stop automation upstream; raw gameplay still passes through.
  var m=ServerMessageFactory.Default.Create(new ServerPacket(opcode,payload));
  var s=Snapshot;long rev=s.Revision+1;
  switch(m) {
   case ServerUserAppearanceMessage p:s=s with{Id=p.UserId,Connected=s.Name is not null};break;
   case ServerStatusMessage p:
    s=s with{Level=p.Level??s.Level,Gold=p.Gold??s.Gold,Experience=p.TotalExperience??s.Experience,Hp=p.Health??s.Hp,Weight=p.Weight??s.Weight,MaxWeight=p.MaxWeight??s.MaxWeight};break;
   case ServerSystemMessage p:
    var notice=new ServerNotice(generation,rev,(byte)p.MessageType,p.Message,DateTimeOffset.UtcNow);
    s=s with{Notice=MundaneBirthEvidence.Matches(notice)||MuisirEvidence.IsKirilNotice(notice,s.Map)?notice:null};break;
   case ServerAddInventoryMessage p:
    s=s with{Inventory=s.Inventory.SetItem(p.Slot,new(p.Slot,p.Name,p.Quantity))};break;
   case ServerRemoveInventoryMessage p:s=s with{Inventory=s.Inventory.Remove(p.Slot)};break;
   case ServerAddEquipMessage p:s=s with{Equipment=s.Equipment.SetItem((byte)p.Slot,p.Name)};break;
   case ServerRemoveEquipMessage p:s=s with{Equipment=s.Equipment.Remove((byte)p.Slot)};break;
   case ServerAddSkillMessage p:s=s with{Skills=s.Skills.SetItem(p.Slot,p.Name)};break;
   case ServerRemoveSkillMessage p:s=s with{Skills=s.Skills.Remove(p.Slot)};break;
   case ServerAddSpellMessage p:s=s with{Spells=s.Spells.SetItem(p.Slot,p.Name)};break;
   case ServerRemoveSpellMessage p:s=s with{Spells=s.Spells.Remove(p.Slot)};break;
   case ServerSelfLookMessage p:s=s with{Legends=p.LegendMarks.Select(x=>x.Text).ToImmutableArray(),LegendsKnown=true,LegendsRevision=rev};break;
   case ServerUserPositionMessage p:LastPositionRevision=rev;s=s with{Position=new(p.X,p.Y)};break;
   case ServerMoveMessage p:
    LastMovementRevision=rev;
    var prev=new Tile(p.PreviousX,p.PreviousY);
    s=s with{Position=GridStep(prev,(byte)p.Direction)};break;
   case ServerMapSizeMessage p:
    Width=p.Width;Height=p.Height;WorldMap=null;
    s=s with{Map=p.MapId,Position=null,Dialog=null,Entities=ImmutableDictionary<uint,Entity>.Empty,GroundItems=ImmutableDictionary<uint,GroundItem>.Empty};break;
   case ServerDrawObjectsMessage p:
    foreach(var e in p.Entities) {
     if(e is ServerItemEntity item){s=s with{GroundItems=s.GroundItems.SetItem(e.Id,new(e.Id,item.Sprite,new(e.X,e.Y)))};continue;}
     var creature=e as ServerCreatureEntity;
     s=s with{Entities=s.Entities.SetItem(e.Id,new(e.Id,creature?.Name??"",new(e.X,e.Y),creature is not null&&creature.CreatureType!=CreatureType.Passable))};
    }break;
   case ServerDrawHumanObjectsMessage p:
    s=s with{Entities=p.EntityId==s.Id?s.Entities.Remove(p.EntityId):s.Entities.SetItem(p.EntityId,new(p.EntityId,p.Name??"",new(p.X,p.Y),true){IsPlayer=true})};break;
   case ServerRemoveObjectsMessage p:s=s with{Entities=s.Entities.Remove(p.EntityId),GroundItems=s.GroundItems.Remove(p.EntityId)};break;
   case ServerMoveObjectMessage p:
    if(s.Entities.TryGetValue(p.EntityId,out var entity))s=s with{Entities=s.Entities.SetItem(p.EntityId,entity with{Position=GridStep(new(p.OriginX,p.OriginY),(byte)p.Direction)})};break;
   case ServerUserReadyMessage:s=s with{InventoryKnown=true};break;
   case ServerScreenMenuMessage p:
    s=s with{Dialog=new(generation,rev,p.EntityId??0,(byte)p.EntityType,p.Name??"",p.Content??"",false,p.PursuitId??0,0,(byte)p.MenuType,
      p.MenuChoices.Select((x,i)=>new DialogChoice(x.Text,x.PursuitId,(byte)(i+1))).ToImmutableArray(),p.Prompt){ObservedAt=DateTimeOffset.UtcNow}};break;
   case ServerPursuitMessage p:
    s=s with{Dialog=p.DialogType==DialogType.CloseDialog?null:new(generation,rev,p.EntityId??0,(byte)p.EntityType,p.Name??"",p.Content??"",true,p.PursuitId??0,p.StepId??0,(byte)p.DialogType,
      p.MenuChoices.Select((x,i)=>new DialogChoice(x,p.PursuitId??0,(byte)(i+1))).ToImmutableArray(),p.InputPrompt,p.HasNextButton){ObservedAt=DateTimeOffset.UtcNow}};break;
   case ServerFieldMapMessage p:WorldMap=p;break;
  }
  Publish(s);Observed?.Invoke(opcode,payload);
 }
 public static Tile GridStep(Tile p,byte direction)=>direction switch{0=>p with{Y=p.Y-1},1=>p with{X=p.X+1},2=>p with{Y=p.Y+1},3=>p with{X=p.X-1},_=>p};
}
