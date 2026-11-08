using System;
using System.Linq;
using PetaloPop.Core;
class P {
  static void Main(string[] a) {
    int[] lv = a.Length>0 ? a.Select(int.Parse).ToArray() : Enumerable.Range(1,30).ToArray();
    foreach (var l in lv) {
      var cfg = LevelCatalog.Get(l);
      double sumPpm=0; int n=12; int wins=0; string obj="";
      for (int t=0;t<n;t++){
        // pierde siempre: objetivo imposible para medir puntos/mov con todos los movimientos
        var c = LevelCatalog.Get(l); c.TargetScore=int.MaxValue/4;
        var s = new GameSession(c); var rng=new GameRng(100+t); int used=0;
        while (s.State==GameState.Playing && used<500){
          var all=MoveFinder.FindAllMoves(s.Board); if(all.Count==0)break;
          int best=-1; var pick=all[0];
          foreach(var m in all.OrderBy(_=>rng.Next(1000)).Take(40)){ var cl=s.CloneReseeded(rng.Next(1<<30)); var r=cl.TrySwap(m.From,m.To); int v=r.PointsGained; for(int i=0;i<s.Objectives.Items.Count;i++) v+=(cl.Objectives.Items[i].Current-s.Objectives.Items[i].Current)*30; if(v>best){best=v;pick=m;} }
          s.TrySwap(pick.From,pick.To); used++;
        }
        sumPpm += (double)s.Score/Math.Max(1,used);
        if (t==0) obj = string.Join(" ", s.Objectives.Items.Select(o=>$"{o.Spec.Kind}:{o.Current}/{o.Target}"));
      }
      Console.WriteLine($"L{l,2} moves={cfg.Moves,2} target={cfg.TargetScore,5} pts/mov≈{sumPpm/n,6:F0} total≈{sumPpm/n*cfg.Moves,6:F0}  obj(seed0)={obj}");
    }
  }
}
