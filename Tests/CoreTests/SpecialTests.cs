using System;
using System.Collections.Generic;
using System.Linq;
using PetaloPop.Core;

namespace CoreTests
{
    /// <summary>Fichas nuevas con tipos únicos crecientes: nunca forman líneas ni cadenas.</summary>
    sealed class UniqueSource : ITileSource
    {
        private int next = 100;
        public int Next(int column) => next++;
    }

    static class T
    {
        public static void Check(bool c, string m) { if (!c) throw new Exception(m); }

        /// <summary>Tablero 7x7 sin ningún par adyacente igual (tipos 1..4); se plantan tipos 5+ encima.</summary>
        public static Board Quiet(int w = 7, int h = 7)
        {
            var b = new Board(w, h);
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) b[x, y] = (x + 2 * y) % 4 + 1;
            return b;
        }

        /// <summary>Planta un movimiento posible en la esquina superior izquierda para que el tablero no se reordene solo.</summary>
        public static Board WithMove(Board b)
        {
            int y = b.Height - 1;
            b[0, y] = 8; b[1, y] = 8; b[3, y] = 8; b[2, y - 1] = 8;
            return b;
        }

        public static GameSession Make(Board b, int moves = 20, int target = 1000000, List<ObjectiveSpec> obj = null, BoosterManager inv = null)
        {
            var cfg = new LevelConfig { Width = b.Width, Height = b.Height, Moves = moves, TargetScore = target, TypeCount = 5, Objectives = obj ?? new List<ObjectiveSpec>() };
            return new GameSession(cfg, b, new UniqueSource(), inv);
        }

        public static Step FirstClear(MoveResult r) => r.Steps.First(s => s.Kind == StepKind.Clear);
        public static Position P(int x, int y) => new Position(x, y);
    }

    static class SpecialTests
    {
        public static void Register(Action<string, Action> run)
        {
            run("Especial: 4 en horizontal crea LineV en la celda movida y puntúa 4 fichas", Line4);
            run("Especial: 5 en línea crea ColorBurst", Line5);
            run("Especial: forma en T crea Bomb", TShape);
            run("Activación: LineH al eliminarse limpia toda su fila", LineHActivation);
            run("Activación: Bomb al eliminarse limpia 3x3", BombActivation);
            run("Combo: ColorBurst + ficha normal elimina todo ese color", BurstNormal);
            run("Combo: ColorBurst + ColorBurst elimina todo el tablero", BurstBurst);
            run("Combo: LineH + LineV elimina fila y columna en cruz", LineCross);
            run("Combo: Bomb + Bomb elimina 5x5; LineH + Bomb elimina cruz gruesa", BombCombos);
            run("Combo: ColorBurst + Bomb convierte todas las de ese color en bombas", BurstBomb);
            run("Cadena de especiales: una activación dispara otras (15 celdas)", SpecialChain);
            run("Un especial con ficha normal sin línea no es un movimiento válido", SpecialNormalInvalid);
            run("Juego aleatorio con TODOS los movimientos (incluye combos): invariantes y cobertura de especiales", RandomWithSpecials);
        }

        static void PlantLine(Board b, int type, params (int x, int y)[] cells) { foreach (var c in cells) b[c.x, c.y] = type; }

        static void Line4()
        {
            var b = T.Quiet();
            PlantLine(b, 5, (1, 0), (2, 0), (4, 0), (3, 1));
            var s = T.Make(b);
            var r = s.TrySwap(T.P(3, 1), T.P(3, 0));
            T.Check(r.Accepted, "debía aceptarse");
            var c = T.FirstClear(r);
            T.Check(c.Created.Count == 1 && c.Created[0].Kind == SpecialKind.LineV && c.Created[0].Pos == T.P(3, 0), "esperaba LineV en (3,0)");
            T.Check(c.Cleared.Count == 3, "3 celdas eliminadas (la 4ª se convierte), hubo " + c.Cleared.Count);
            T.Check(c.Points == 40, "40 puntos (4 fichas), hubo " + c.Points);
            T.Check(s.Board.SpecialAt(T.P(3, 0)) == SpecialKind.LineV, "el especial debía quedar en el tablero");
        }

        static void Line5()
        {
            var b = T.Quiet();
            PlantLine(b, 5, (0, 0), (1, 0), (3, 0), (4, 0), (2, 1));
            var s = T.Make(b);
            var r = s.TrySwap(T.P(2, 1), T.P(2, 0));
            var c = T.FirstClear(r);
            T.Check(c.Created.Count == 1 && c.Created[0].Kind == SpecialKind.ColorBurst && c.Created[0].Pos == T.P(2, 0), "esperaba ColorBurst en (2,0)");
            T.Check(c.Cleared.Count == 4 && c.Points == 50, "4 celdas y 50 puntos");
        }

        static void TShape()
        {
            var b = T.Quiet();
            PlantLine(b, 5, (1, 2), (3, 2), (2, 1), (2, 0), (2, 3));
            var s = T.Make(b);
            var r = s.TrySwap(T.P(2, 3), T.P(2, 2));
            var c = T.FirstClear(r);
            T.Check(c.Created.Count == 1 && c.Created[0].Kind == SpecialKind.Bomb && c.Created[0].Pos == T.P(2, 2), "esperaba Bomb en (2,2)");
            T.Check(c.Cleared.Count == 4 && c.Points == 50, "4 celdas y 50 puntos, hubo " + c.Cleared.Count + "/" + c.Points);
            T.Check(s.Board.CountSpecials() == 1, "debía quedar exactamente 1 especial");
        }

        static Board PlantedRow0Match(SpecialKind special)
        {
            var b = T.Quiet();
            PlantLine(b, 5, (1, 0), (2, 0), (3, 1));
            b.SetSpecial(T.P(3, 1), special);
            return b;
        }

        static void LineHActivation()
        {
            var s = T.Make(PlantedRow0Match(SpecialKind.LineH));
            var r = s.TrySwap(T.P(3, 1), T.P(3, 0));
            var c = T.FirstClear(r);
            T.Check(c.Cleared.Count == 7 && c.Cleared.All(p => p.Y == 0), "debía limpiar la fila 0 completa, hubo " + c.Cleared.Count);
            T.Check(c.Activated.Any(a => a.Kind == SpecialKind.LineH), "no se registró la activación");
        }

        static void BombActivation()
        {
            var s = T.Make(PlantedRow0Match(SpecialKind.Bomb));
            var r = s.TrySwap(T.P(3, 1), T.P(3, 0));
            var c = T.FirstClear(r);
            T.Check(c.Cleared.Count == 7, "esperaba 7 celdas (línea + 3x3 recortado), hubo " + c.Cleared.Count);
        }

        static void BurstNormal()
        {
            var b = T.Quiet();
            b[3, 3] = 6; b.SetSpecial(T.P(3, 3), SpecialKind.ColorBurst);
            int t = b[3, 4];
            int n = b.CountWhere(p => b[p] == t);
            var s = T.Make(b);
            var r = s.TrySwap(T.P(3, 3), T.P(3, 4));
            T.Check(r.Accepted, "el combo debía aceptarse");
            var c = T.FirstClear(r);
            T.Check(c.Cleared.Count == n + 1, $"esperaba {n + 1} celdas, hubo {c.Cleared.Count}");
        }

        static void BurstBurst()
        {
            var b = T.Quiet();
            b[3, 3] = 6; b.SetSpecial(T.P(3, 3), SpecialKind.ColorBurst);
            b[3, 4] = 7; b.SetSpecial(T.P(3, 4), SpecialKind.ColorBurst);
            var s = T.Make(b);
            var r = s.TrySwap(T.P(3, 3), T.P(3, 4));
            T.Check(T.FirstClear(r).Cleared.Count == 49, "debía limpiar las 49 celdas");
        }

        static Board TwoSpecials(SpecialKind ka, SpecialKind kc)
        {
            var b = T.Quiet();
            b.SetSpecial(T.P(3, 3), ka);
            b.SetSpecial(T.P(3, 4), kc);
            return b;
        }

        static void LineCross()
        {
            var s = T.Make(TwoSpecials(SpecialKind.LineH, SpecialKind.LineV));
            var r = s.TrySwap(T.P(3, 3), T.P(3, 4));
            T.Check(r.Accepted, "debía aceptarse");
            var c = T.FirstClear(r);
            T.Check(c.Cleared.Count == 13, "cruz de 7+7-1 = 13, hubo " + c.Cleared.Count);
            T.Check(c.Cleared.Contains(T.P(0, 4)) && c.Cleared.Contains(T.P(3, 0)), "faltan extremos de la cruz");
        }

        static void BombCombos()
        {
            var s = T.Make(TwoSpecials(SpecialKind.Bomb, SpecialKind.Bomb));
            var c = T.FirstClear(s.TrySwap(T.P(3, 3), T.P(3, 4)));
            T.Check(c.Cleared.Count == 25, "5x5 = 25, hubo " + c.Cleared.Count);
            s = T.Make(TwoSpecials(SpecialKind.LineH, SpecialKind.Bomb));
            c = T.FirstClear(s.TrySwap(T.P(3, 3), T.P(3, 4)));
            T.Check(c.Cleared.Count == 33, "cruz gruesa = 33, hubo " + c.Cleared.Count);
        }

        static void BurstBomb()
        {
            var b = T.Quiet();
            b[3, 3] = 6; b.SetSpecial(T.P(3, 3), SpecialKind.ColorBurst);
            b.SetSpecial(T.P(3, 4), SpecialKind.Bomb);
            int t = b[3, 4];
            int n = b.CountWhere(p => b[p] == t);
            var s = T.Make(b);
            var c = T.FirstClear(s.TrySwap(T.P(3, 3), T.P(3, 4)));
            int bombs = c.Activated.Count(a => a.Kind == SpecialKind.Bomb);
            T.Check(bombs == n, $"esperaba {n} bombas activadas, hubo {bombs}");
            T.Check(c.Cleared.Count > n * 3, "las bombas debían limpiar mucho más que su color: " + c.Cleared.Count);
        }

        static void SpecialChain()
        {
            var b = PlantedRow0Match(SpecialKind.LineH);
            b.SetSpecial(T.P(6, 0), SpecialKind.Bomb);
            b.SetSpecial(T.P(0, 0), SpecialKind.LineV);
            var s = T.Make(b);
            var c = T.FirstClear(s.TrySwap(T.P(3, 1), T.P(3, 0)));
            T.Check(c.Cleared.Count == 15, "esperaba 15 celdas, hubo " + c.Cleared.Count);
            var kinds = c.Activated.Select(a => a.Kind).ToList();
            T.Check(kinds.Contains(SpecialKind.LineH) && kinds.Contains(SpecialKind.Bomb) && kinds.Contains(SpecialKind.LineV), "faltan activaciones en cadena");
        }

        static void SpecialNormalInvalid()
        {
            var b = T.Quiet();
            b.SetSpecial(T.P(3, 3), SpecialKind.LineH);
            var s = T.Make(b);
            T.Check(!MoveFinder.IsValidSwap(s.Board, T.P(3, 3), T.P(4, 3)), "especial + normal sin línea debía ser inválido");
            var r = s.TrySwap(T.P(3, 3), T.P(4, 3));
            T.Check(!r.Accepted && s.Moves.MovesLeft == 20, "no debía consumir movimiento");
        }

        static void RandomWithSpecials()
        {
            int totalSpecialsSeen = 0, combos = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var rng = new GameRng(seed * 7);
                var s = new GameSession(new LevelConfig { Seed = seed, Moves = 1000, TargetScore = int.MaxValue / 4, TypeCount = 5 });
                for (int i = 0; i < 40; i++)
                {
                    var moves = MoveFinder.FindAllMoves(s.Board);
                    T.Check(moves.Count > 0, "sin movimientos disponibles");
                    var m = moves[rng.Next(moves.Count)];
                    bool combo = SpecialTileSystem.IsComboSwap(s.Board, m.From, m.To);
                    var start = s.Board.Clone();
                    var r = s.TrySwap(m.From, m.To);
                    T.Check(r.Accepted, "movimiento válido rechazado");
                    if (combo) combos++;
                    totalSpecialsSeen += r.Steps.Where(st => st.Kind == StepKind.Clear).Sum(st => st.Created.Count);
                    T.Check(s.Board.IsFull(), "tablero con huecos");
                    T.Check(MatchDetector.FindMatches(s.Board).Count == 0, "quedaron líneas");
                    T.Check(MoveFinder.HasMove(s.Board), "tablero bloqueado");
                    var g = Program.Replay(start, r);
                    for (int x = 0; x < s.Board.Width; x++) for (int y = 0; y < s.Board.Height; y++)
                        {
                            T.Check(g.Type[x, y] == s.Board[x, y], $"tipo difiere en reproducción {x},{y} (semilla {seed}, mov {i})");
                            T.Check(g.Special[x, y] == s.Board.SpecialAt(new Position(x, y)), $"especial difiere en reproducción {x},{y} (semilla {seed}, mov {i})");
                        }
                }
            }
            T.Check(totalSpecialsSeen >= 20, "poca cobertura: especiales creados = " + totalSpecialsSeen);
            T.Check(combos >= 1, "ningún combo jugado en la simulación");
            Console.WriteLine($"        (cobertura: {totalSpecialsSeen} especiales creados, {combos} combos jugados)");
        }
    }
}
