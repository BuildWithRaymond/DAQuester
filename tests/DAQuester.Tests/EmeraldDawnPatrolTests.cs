using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;
namespace DAQuester.Tests;

public class EmeraldDawnPatrolTests {
 [Fact] public void Entrance_patrol_steps_on_all_reachable_tiles_without_doors() {
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content"));
  var json=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};
  using var profile=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"calibration","emerald-dawn-patrol.json")));
  var patrol=profile.RootElement.GetProperty("patrol").Deserialize<Tile[]>(json)!;
  var maps=new MapRepository("",Path.Combine(root,"routes.json"));var record=maps.Record(542);
  var doors=record.Portals.Select(p=>p.From).ToHashSet();
  var visited=new HashSet<Tile>();var position=new Tile(14,23);visited.Add(position);
  foreach(var waypoint in patrol) {
   Assert.True(position.X==waypoint.X||position.Y==waypoint.Y,"Patrol legs must be straight so shortest-path walking cannot skip sweep tiles.");
   while(position!=waypoint){position=new(position.X+Math.Sign(waypoint.X-position.X),position.Y+Math.Sign(waypoint.Y-position.Y));visited.Add(position);}
  }
  Assert.All(visited,tile=>{Assert.InRange(tile.X,0,record.Width-1);Assert.InRange(tile.Y,0,record.Height-1);Assert.DoesNotContain(tile,doors);});
  // Independent read-only collision survey: 519 reachable non-door tiles.
  Assert.Equal(519,visited.Count);Assert.Equal(519,profile.RootElement.GetProperty("reachableTiles").GetInt32());
  Assert.Contains(new Tile(10,11),visited); // Observed manual acquisition position; not a guaranteed drop tile.
  Assert.Equal(patrol[0],patrol[^1]); // Each repeated lap follows the same verified sweep.
  using var recipe=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"recipes","emerald-dawn.v1.json")));
  var gather=recipe.RootElement.GetProperty("Steps").EnumerateArray().Single(p=>p.GetProperty("Id").GetString()=="gather");
  Assert.Equal(patrol,JsonSerializer.Deserialize<Tile[]>(gather.GetProperty("Arguments").GetProperty("patrol").GetString()!,json));
 }
}
