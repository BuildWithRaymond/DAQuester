using DAQuester.Core;
namespace DAQuester.DarkAges.Navigation;
public class Grid(int width,int height,Func<int,int,bool> blocked) {
 public bool Blocked(Tile p) => p.X<0||p.Y<0||p.X>=width||p.Y>=height||blocked(p.X,p.Y);
 public IReadOnlyList<Tile> Path(Tile start,Tile goal,ISet<Tile>? obstacles=null,bool allowBlockedGoal=false) {
  bool GoalOverride(Tile p)=>allowBlockedGoal&&p==goal&&p.X>=0&&p.Y>=0&&p.X<width&&p.Y<height;
  if(Blocked(goal)&&!GoalOverride(goal)) throw new InvalidOperationException("Destination is blocked.");
  var open=new PriorityQueue<Tile,int>();var prev=new Dictionary<Tile,Tile>();var costs=new Dictionary<Tile,int>{{start,0}};
  open.Enqueue(start,0);
  while(open.TryDequeue(out var p,out _)) {
   if(p==goal){var result=new List<Tile>();while(p!=start){result.Add(p);p=prev[p];}result.Reverse();return result;}
   foreach(var q in Neighbors(p)) {
    if((Blocked(q)&&!GoalOverride(q))||obstacles?.Contains(q)==true) continue;
    int cost=costs[p]+1;
    if(costs.TryGetValue(q,out var old)&&old<=cost) continue;
    costs[q]=cost;prev[q]=p;open.Enqueue(q,cost+Math.Abs(goal.X-q.X)+Math.Abs(goal.Y-q.Y));
   }
  }
  throw new InvalidOperationException("No walkable route to destination.");
 }
 public static IEnumerable<Tile> Neighbors(Tile p) {yield return new(p.X,p.Y-1);yield return new(p.X+1,p.Y);yield return new(p.X,p.Y+1);yield return new(p.X-1,p.Y);}
}
