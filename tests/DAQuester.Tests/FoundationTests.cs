using Xunit;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using System.Collections.Immutable;
namespace DAQuester.Tests;
public class FoundationTests {
 public static CharacterSnapshot Ready(int? level=1,uint gold=999) => new(){Name="Nuclear",Id=42,Connected=true,Generation=1,Level=level,Gold=gold,Hp=100,Weight=1,MaxWeight=100,InventoryKnown=true,Map=500,Position=new(20,20)};
 [Theory][InlineData(1,EligibilityKind.Eligible)][InlineData(11,EligibilityKind.Eligible)][InlineData(12,EligibilityKind.Blocked)][InlineData(null,EligibilityKind.Unknown)]
 public void Level_gate(int? level,EligibilityKind expected) => Assert.Equal(expected,StickEligibility.Check(Ready(level),null,DateTimeOffset.UtcNow).Kind);
 [Fact] public void Gold_excess_requires_banking() {Assert.Equal(EligibilityKind.Blocked,StickEligibility.Check(Ready(1,1000),null,DateTimeOffset.UtcNow).Kind);}
 [Fact] public void Unknown_inventory_waits() {Assert.Equal(EligibilityKind.Unknown,StickEligibility.Check(Ready() with{InventoryKnown=false},null,DateTimeOffset.UtcNow).Kind);}
 [Fact] public void Cooldown_blocks_until_eight_hours() {var now=DateTimeOffset.UtcNow;Assert.Equal(EligibilityKind.Blocked,StickEligibility.Check(Ready(),now.AddHours(-7),now).Kind);Assert.Equal(EligibilityKind.Eligible,StickEligibility.Check(Ready(),now.AddHours(-8),now).Kind);}
 [Fact] public void Stack_updates_are_absolute_and_case_insensitive() {var s=Ready() with{Inventory=ImmutableDictionary<byte,Item>.Empty.Add(1,new(1,"Branch",4)).Add(2,new(2,"branch",2))};Assert.Equal(6,s.Count("Branch"));}
 [Fact] public void Fragmented_and_coalesced_frames() {var b=new FrameBuffer();b.Append([0xAA,0]);Assert.Null(b.Read());b.Append([2,8,9,0xAA,0,1,7]);Assert.Equal(new byte[]{0xAA,0,2,8,9},b.Read());Assert.Equal(new byte[]{0xAA,0,1,7},b.Read());Assert.Null(b.Read());}
 [Fact] public void Walls_and_entities_force_detour() {var g=new Grid(5,5,(x,y)=>x==2&&y<4);var p=g.Path(new(0,0),new(4,0));Assert.Equal(new Tile(4,0),p.Last());Assert.Contains(new Tile(2,4),p);Assert.DoesNotContain(new Tile(2,0),p);}
 [Fact] public void Impossible_path_fails_clearly() {var g=new Grid(3,3,(x,y)=>x==1);Assert.Throws<InvalidOperationException>(()=>g.Path(new(0,0),new(2,0)));}
}
