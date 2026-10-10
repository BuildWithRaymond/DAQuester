using DAQuester.Core;
namespace DAQuester.DarkAges.Navigation;
public static class NpcApproach {
 public static Tile Find(Grid grid,Tile start,Tile npc,ISet<Tile> obstacles,int range=3) {
  var distance=new Dictionary<Tile,int>{{start,0}};var queue=new Queue<Tile>();queue.Enqueue(start);
  while(queue.TryDequeue(out var tile))foreach(var next in Grid.Neighbors(tile)) {
   if(grid.Blocked(next)||obstacles.Contains(next)||distance.ContainsKey(next))continue;
   distance[next]=distance[tile]+1;queue.Enqueue(next);
  }
  return distance.Keys.Where(t=>t!=npc&&Math.Abs(t.X-npc.X)+Math.Abs(t.Y-npc.Y)<=range).OrderBy(t=>distance[t]).ThenBy(t=>Math.Abs(t.X-npc.X)+Math.Abs(t.Y-npc.Y)).FirstOrDefault()??throw new QuestBlockedException("NPC has no reachable interaction tile within "+range+" tiles.");
 }
}
