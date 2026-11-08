using System;
using System.Diagnostics;
using PetaloPop.Core;
class P { static void Main() {
  foreach (var lvl in new[]{1,15,30}) {
    var cfg = LevelCatalog.Get(lvl); cfg.Moves = 100000; cfg.TargetScore = int.MaxValue/4;
    var s = new GameSession(cfg); var rng = new GameRng(5);
    for (int i=0;i<50;i++){ var a=MoveFinder.FindAllMoves(s.Board); var m=a[rng.Next(a.Count)]; s.TrySwap(m.From,m.To);} // calentar
    long mem0 = GC.GetAllocatedBytesForCurrentThread(); var sw=Stopwatch.StartNew(); double worst=0; int n=2000, steps=0;
    for (int i=0;i<n;i++){ var all=MoveFinder.FindAllMoves(s.Board); var m=all[rng.Next(all.Count)]; var t=Stopwatch.GetTimestamp(); var r=s.TrySwap(m.From,m.To); double ms=(Stopwatch.GetTimestamp()-t)*1000.0/Stopwatch.Frequency; if(ms>worst)worst=ms; steps+=r.Steps.Count; }
    sw.Stop(); long mem=GC.GetAllocatedBytesForCurrentThread()-mem0;
    Console.WriteLine($"Nivel {lvl,2} ({cfg.Width}x{cfg.Height}): {n} jugadas en {sw.ElapsedMilliseconds} ms (incluye buscar movimientos) | peor TrySwap {worst:F2} ms | {mem/n/1024} KB asignados por jugada | {steps/(double)n:F1} pasos/jugada");
  } } }
