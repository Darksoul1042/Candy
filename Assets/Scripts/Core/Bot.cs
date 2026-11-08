using System.Linq;

namespace PetaloPop.Core
{
    /// <summary>
    /// Jugador automático para comprobar que los niveles son ganables. Modo normal: elige el movimiento que más puntúa
    /// (simulando sin conocer las fichas futuras), con potenciadores opcionales; modo novato: movimientos válidos al azar.
    /// </summary>
    public static class Bot
    {
        public static BoosterManager StartingInventory()
        {
            var m = new BoosterManager();
            m.Add(BoosterKind.Hammer, 3); m.Add(BoosterKind.FreeSwap, 2); m.Add(BoosterKind.ObstacleRemover, 2);
            m.Add(BoosterKind.ExtraMoves, 2); m.Add(BoosterKind.SpecialMaker, 2);
            return m;
        }

        public static GameState Play(LevelConfig cfg, int botSeed, out int score, out int movesUsed, bool novice = false, BoosterManager boosters = null)
        {
            var s = new GameSession(cfg, null, null, boosters);
            var rng = new GameRng(botSeed);
            int used = 0, guard = 0;
            while (s.State == GameState.Playing && guard++ < 600)
            {
                if (boosters != null && TryBooster(s)) continue;
                var all = MoveFinder.FindAllMoves(s.Board);
                if (all.Count == 0) break;
                var pick = all[0];
                if (novice) pick = all[rng.Next(all.Count)];
                else
                {
                    int best = -1;
                    foreach (var m in all.OrderBy(_ => rng.Next(1000)).Take(40))
                    {
                        var c = s.CloneReseeded(rng.Next(1 << 30)); // simula sin conocer las fichas que caerán
                        var r = c.TrySwap(m.From, m.To);
                        int value = r.PointsGained + ObjectiveValue(s, c);
                        if (value > best) { best = value; pick = m; }
                    }
                }
                s.TrySwap(pick.From, pick.To);
                used++;
            }
            score = s.Score; movesUsed = used;
            return s.State;
        }

        // Usa potenciadores cuando quedan pocos movimientos y falta algún objetivo de obstáculos.
        private static bool TryBooster(GameSession s)
        {
            if (s.Objectives.AllDone && s.Score >= s.Config.TargetScore) return false;
            var b = s.Board;
            if (s.Moves.MovesLeft <= 2 && s.Boosters.Count(BoosterKind.ExtraMoves) > 0) return s.UseExtraMoves().Accepted;
            if (s.Moves.MovesLeft > 12) return false;
            foreach (var item in s.Objectives.Items.Where(i => !i.Done))
            {
                for (int x = 0; x < b.Width; x++)
                    for (int y = 0; y < b.Height; y++)
                    {
                        var p = new Position(x, y);
                        bool want = item.Spec.Kind == ObjectiveKind.BreakFrozen ? b[p] > 0 && b.Hp(p) > 0
                                  : item.Spec.Kind == ObjectiveKind.ClearJelly ? b.Jelly(p) > 0
                                  : item.Spec.Kind == ObjectiveKind.DestroyCrates && b[p] == Board.Crate;
                        if (!want) continue;
                        if (s.Boosters.Count(BoosterKind.ObstacleRemover) > 0 && s.UseObstacleRemover(p).Accepted) return true;
                        if (item.Spec.Kind != ObjectiveKind.BreakFrozen && s.Boosters.Count(BoosterKind.Hammer) > 0 && s.UseHammer(p).Accepted) return true;
                        return false;
                    }
            }
            return false;
        }

        private static int ObjectiveValue(GameSession before, GameSession after)
        {
            int v = 0;
            for (int i = 0; i < before.Objectives.Items.Count; i++)
                v += (after.Objectives.Items[i].Current - before.Objectives.Items[i].Current) * 30;
            return v;
        }
    }
}
