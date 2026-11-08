using System;
using System.Collections.Generic;
using System.Linq;
using PetaloPop.Core;

namespace CoreTests
{
    /// <summary>Fuente de fichas con guion para pruebas deterministas.</summary>
    sealed class Scripted : ITileSource
    {
        private readonly Queue<int> q;
        public Scripted(params int[] types) { q = new Queue<int>(types); }
        public int Next(int column) => q.Count > 0 ? q.Dequeue() : 9;
    }

    public static class Program
    {
        static int passed, failed;
        static readonly List<string> failures = new List<string>();

        public static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        public static void Run(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("  OK    " + name); }
            catch (Exception e) { failed++; failures.Add(name); Console.WriteLine("  FALLO " + name + " -> " + e.Message); }
        }

        static int Main()
        {
            Console.WriteLine("PetaloPop.Core - pruebas");
            Run("Generador: sin combinaciones iniciales y con movimiento (300 semillas, varios tamaños)", GeneratorTest);
            Run("Detector: línea horizontal de 3", () =>
            {
                var b = Board.FromRows("12121", "11121", "21212");
                var m = MatchDetector.FindMatches(b);
                Check(m.Count == 1 && m[0].Horizontal && m[0].Length == 3 && m[0].Type == 1, "esperaba 1 línea horizontal de 3");
            });
            Run("Detector: línea vertical de 4", () =>
            {
                var b = Board.FromRows("12311", "12132", "32122", "12313");
                var m = MatchDetector.FindMatches(b);
                var v = m.Where(x => !x.Horizontal).ToList();
                Check(v.Count == 1 && v[0].Length == 4 && v[0].Type == 2, "esperaba vertical de 4 tipo 2 (m=" + m.Count + ")");
            });
            Run("Detector: forma en T y L comparten celdas (unión sin duplicados)", () =>
            {
                var b = Board.FromRows("31423", "21324", "11145", "23254");
                var m = MatchDetector.FindMatches(b);
                var cells = MatchDetector.UnionOfCells(m);
                Check(m.Count == 2, "esperaba 2 líneas, hubo " + m.Count);
                Check(cells.Count == 5, "esperaba 5 celdas únicas (3+3-1), hubo " + cells.Count);
            });
            Run("Detector: sin falsos positivos con 2 iguales o huecos", () =>
            {
                var b = Board.FromRows("11.11", "22.22", "12121");
                Check(MatchDetector.FindMatches(b).Count == 0, "no debía haber líneas");
            });
            Run("Intercambio no adyacente: rechazado, sin cambios", NonAdjacent);
            Run("Intercambio adyacente sin línea: ida y vuelta, tablero y movimientos intactos", InvalidSwap);
            Run("Intercambio válido: elimina, aplica gravedad, puntúa y consume un movimiento", ValidSwapScripted);
            Run("Reacción en cadena: multiplicador x2 en la segunda cadena", CascadeScoring);
            Run("Invariantes tras 400 movimientos aleatorios (tablero lleno, sin líneas, hay movimiento)", RandomPlayInvariants);
            Run("Reproducir los pasos sobre una copia reproduce el tablero final (contrato de animación)", ReplayConsistency);
            Run("Determinismo: misma semilla, mismo resultado", Determinism);
            Run("Bloqueo: detecta tablero sin movimientos y Reshuffle lo arregla", DeadlockReshuffle);
            Run("Victoria al alcanzar la puntuación objetivo", WinCondition);
            Run("Derrota al agotar movimientos sin llegar al objetivo; luego se rechazan intercambios", LoseCondition);
            Run("Estrellas: umbrales 1 / 1.5x / 2x", Stars);
            SpecialTests.Register(Run);
            ObstacleTests.Register(Run);
            ProgressTests.Register(Run);

            Console.WriteLine($"\nResultado: {passed} correctas, {failed} fallidas");
            return failed == 0 ? 0 : 1;
        }

        static void GeneratorTest()
        {
            var sizes = new[] { (9, 9, 5), (9, 9, 6), (7, 8, 4), (5, 5, 3), (10, 12, 6) };
            for (int seed = 0; seed < 300; seed++)
            {
                var (w, h, t) = sizes[seed % sizes.Length];
                var b = BoardGenerator.Create(w, h, t, new GameRng(seed));
                Check(b.IsFull(), "tablero con huecos, semilla " + seed);
                Check(MatchDetector.FindMatches(b).Count == 0, "combinación inicial, semilla " + seed);
                Check(MoveFinder.HasMove(b), "sin movimientos, semilla " + seed);
                for (int x = 0; x < w; x++) for (int y = 0; y < h; y++)
                        Check(b[x, y] >= 1 && b[x, y] <= t, "tipo fuera de rango");
            }
        }

        static GameSession Crafted(Board b, ITileSource src, int moves = 10, int target = 100000) =>
            new GameSession(new LevelConfig { Width = b.Width, Height = b.Height, Moves = moves, TargetScore = target, TypeCount = 5 }, b, src);

        static void NonAdjacent()
        {
            var s = new GameSession(new LevelConfig { Seed = 1 });
            string before = s.Board.ToString();
            var r = s.TrySwap(new Position(0, 0), new Position(2, 0));
            Check(!r.Accepted && r.Steps.Count == 0, "debía rechazarse sin pasos");
            r = s.TrySwap(new Position(-1, 0), new Position(0, 0));
            Check(!r.Accepted && r.Steps.Count == 0, "fuera de límites debía rechazarse");
            Check(before == s.Board.ToString() && s.Moves.MovesLeft == s.Config.Moves, "estado alterado");
        }

        static void InvalidSwap()
        {
            var s = new GameSession(new LevelConfig { Seed = 7 });
            Position a = default, c = default; bool found = false;
            for (int x = 0; x < s.Board.Width - 1 && !found; x++)
                for (int y = 0; y < s.Board.Height && !found; y++)
                {
                    var p = new Position(x, y); var q = new Position(x + 1, y);
                    if (s.Board[p] != s.Board[q] && !MoveFinder.IsValidSwap(s.Board, p, q)) { a = p; c = q; found = true; }
                }
            Check(found, "no se halló un intercambio inválido");
            string before = s.Board.ToString();
            var r = s.TrySwap(a, c);
            Check(!r.Accepted, "debía no ser aceptado");
            Check(r.Steps.Count == 2 && r.Steps[0].Kind == StepKind.Swap && r.Steps[1].Kind == StepKind.SwapBack, "esperaba Swap+SwapBack");
            Check(before == s.Board.ToString(), "el tablero cambió");
            Check(s.Moves.MovesLeft == s.Config.Moves && s.Score == 0, "se consumió movimiento o se puntuó");
        }

        // Tablero 5x3 armado a mano (fila superior primero). Sin líneas iniciales.
        // Intercambiar (2,0)<->(2,1) deja la fila inferior en 1 1 1 4 3.
        static Board CraftedBoard() => Board.FromRows(
            "34343",
            "43134",
            "11243");

        static void ValidSwapScripted()
        {
            Check(MatchDetector.FindMatches(CraftedBoard()).Count == 0, "el tablero armado ya tiene líneas");
            var s = Crafted(CraftedBoard(), new Scripted(5, 6, 5)); // relleno que no forma líneas
            var r = s.TrySwap(new Position(2, 0), new Position(2, 1));
            Check(r.Accepted, "debía aceptarse");
            Check(s.Moves.MovesLeft == 9, "debía consumir 1 movimiento");
            var kinds = r.Steps.Select(x => x.Kind).ToArray();
            Check(kinds.SequenceEqual(new[] { StepKind.Swap, StepKind.Clear, StepKind.Fall, StepKind.Spawn }),
                "pasos inesperados: " + string.Join(",", kinds));
            var clear = r.Steps[1];
            Check(clear.Cleared.Count == 3, "esperaba 3 celdas, hubo " + clear.Cleared.Count);
            Check(clear.Points == 30 && s.Score == 30, "esperaba 30 puntos, hubo " + s.Score);
            Check(r.MaxCascade == 1, "no debía haber cadena");
            Check(s.Board.ToString() == "56543\n34334\n43243", "tablero final inesperado:\n" + s.Board);
            Check(s.Board.IsFull(), "tablero con huecos");
        }

        static void CascadeScoring()
        {
            // Relleno 7 7 7 forma una segunda línea (cadena x2); luego 5 6 5 cierra sin líneas.
            var s = Crafted(CraftedBoard(), new Scripted(7, 7, 7, 5, 6, 5));
            var r = s.TrySwap(new Position(2, 0), new Position(2, 1));
            Check(r.Accepted, "debía aceptarse");
            var kinds = r.Steps.Select(x => x.Kind).ToArray();
            Check(kinds.SequenceEqual(new[] { StepKind.Swap, StepKind.Clear, StepKind.Fall, StepKind.Spawn, StepKind.Clear, StepKind.Spawn }),
                "pasos inesperados: " + string.Join(",", kinds));
            var clears = r.Steps.Where(x => x.Kind == StepKind.Clear).ToList();
            Check(clears[0].Cascade == 1 && clears[0].Points == 30, "cadena 1 debía dar 30, dio " + clears[0].Points);
            Check(clears[1].Cascade == 2 && clears[1].Points == 60, "cadena 2 debía dar 60 (x2), dio " + clears[1].Points);
            Check(r.MaxCascade == 2 && r.PointsGained == 90 && s.Score == 90, "total esperado 90, fue " + s.Score);
        }

        static void RandomPlayInvariants()
        {
            var rng = new GameRng(99);
            int played = 0;
            for (int game = 0; game < 8; game++)
            {
                var s = new GameSession(new LevelConfig { Seed = game + 1, Moves = 1000, TargetScore = int.MaxValue / 4 });
                for (int i = 0; i < 50; i++)
                {
                    Check(s.TryGetHint(out var a, out var c), "sin movimiento disponible (debía haber reordenado)");
                    int before = s.Score;
                    var r = s.TrySwap(a, c);
                    Check(r.Accepted, "la pista debía ser un movimiento válido");
                    Check(s.Board.IsFull(), "tablero con huecos");
                    Check(MatchDetector.FindMatches(s.Board).Count == 0, "quedaron líneas tras resolver");
                    Check(MoveFinder.HasMove(s.Board), "tablero bloqueado sin reordenar");
                    Check(s.Score > before, "un movimiento válido debe puntuar");
                    for (int x = 0; x < s.Board.Width; x++) for (int y = 0; y < s.Board.Height; y++)
                            Check(s.Board[x, y] >= 1 && s.Board[x, y] <= s.Config.TypeCount, "tipo inválido");
                    played++;
                }
            }
            Check(played == 400, "movimientos jugados: " + played);
        }

        public sealed class Grid { public int[,] Type, Hp, Jelly; public SpecialKind[,] Special; }

        // Aplica los pasos a una copia (tipos, especiales, vida y gelatina) y devuelve el resultado.
        public static Grid Replay(Board start, MoveResult r)
        {
            int w = start.Width, h = start.Height;
            var g = new Grid { Type = new int[w, h], Hp = new int[w, h], Jelly = new int[w, h], Special = new SpecialKind[w, h] };
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++)
                {
                    var p = new Position(x, y);
                    g.Type[x, y] = start[p]; g.Hp[x, y] = start.Hp(p); g.Jelly[x, y] = start.Jelly(p); g.Special[x, y] = start.SpecialAt(p);
                }
            void SwapCells(Position a, Position b)
            {
                (g.Type[a.X, a.Y], g.Type[b.X, b.Y]) = (g.Type[b.X, b.Y], g.Type[a.X, a.Y]);
                (g.Special[a.X, a.Y], g.Special[b.X, b.Y]) = (g.Special[b.X, b.Y], g.Special[a.X, a.Y]);
                (g.Hp[a.X, a.Y], g.Hp[b.X, b.Y]) = (g.Hp[b.X, b.Y], g.Hp[a.X, a.Y]);
            }
            foreach (var st in r.Steps)
            {
                switch (st.Kind)
                {
                    case StepKind.Swap:
                    case StepKind.SwapBack: SwapCells(st.A, st.B); break;
                    case StepKind.Clear:
                        foreach (var c in st.Cleared) { g.Type[c.X, c.Y] = 0; g.Special[c.X, c.Y] = SpecialKind.None; g.Hp[c.X, c.Y] = 0; }
                        foreach (var c in st.Created) g.Special[c.Pos.X, c.Pos.Y] = c.Kind;
                        foreach (var hit in st.Hits)
                        {
                            if (hit.Kind == ObstacleKind.Jelly) g.Jelly[hit.Pos.X, hit.Pos.Y] = hit.Remaining;
                            else g.Hp[hit.Pos.X, hit.Pos.Y] = hit.Remaining;
                        }
                        break;
                    case StepKind.Fall:
                    case StepKind.Shuffle:
                        {
                            var ty = (int[,])g.Type.Clone(); var sp = (SpecialKind[,])g.Special.Clone(); var hp = (int[,])g.Hp.Clone();
                            foreach (var m in st.Moves)
                            {
                                Check(ty[m.From.X, m.From.Y] == m.Type, "tipo de origen no coincide en " + st.Kind);
                                Check(sp[m.From.X, m.From.Y] == m.Special && hp[m.From.X, m.From.Y] == m.Hp, "especial/vida de origen no coinciden en " + st.Kind);
                            }
                            foreach (var m in st.Moves) { g.Type[m.From.X, m.From.Y] = 0; g.Special[m.From.X, m.From.Y] = SpecialKind.None; g.Hp[m.From.X, m.From.Y] = 0; }
                            foreach (var m in st.Moves) { g.Type[m.To.X, m.To.Y] = m.Type; g.Special[m.To.X, m.To.Y] = m.Special; g.Hp[m.To.X, m.To.Y] = m.Hp; }
                            break;
                        }
                    case StepKind.Spawn:
                        foreach (var m in st.Moves)
                        {
                            Check(m.From.Y >= h, "las fichas nuevas deben nacer sobre el tablero");
                            Check(g.Type[m.To.X, m.To.Y] == 0, "se rellenó una celda ocupada");
                            g.Type[m.To.X, m.To.Y] = m.Type;
                        }
                        break;
                }
            }
            return g;
        }

        static void ReplayConsistency()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                var s = new GameSession(new LevelConfig { Seed = seed, Moves = 1000, TargetScore = int.MaxValue / 4 });
                for (int i = 0; i < 15; i++)
                {
                    var start = s.Board.Clone();
                    s.TryGetHint(out var a, out var c);
                    var r = s.TrySwap(a, c);
                    var g = Replay(start, r);
                    for (int x = 0; x < s.Board.Width; x++) for (int y = 0; y < s.Board.Height; y++)
                            Check(g.Type[x, y] == s.Board[x, y], $"reproducción difiere en {x},{y} (semilla {seed}, mov {i})");
                }
            }
        }

        static void Determinism()
        {
            string Play(int seed)
            {
                var s = new GameSession(new LevelConfig { Seed = seed, Moves = 1000, TargetScore = int.MaxValue / 4 });
                for (int i = 0; i < 20; i++) { s.TryGetHint(out var a, out var c); s.TrySwap(a, c); }
                return s.Board + "|" + s.Score;
            }
            Check(Play(5) == Play(5), "dos partidas con la misma semilla difieren");
            Check(Play(5) != Play(6), "semillas distintas dieron lo mismo (improbable)");
        }

        static void DeadlockReshuffle()
        {
            var dead = Board.FromRows(
                "123123",
                "231231",
                "312312",
                "123123",
                "231231",
                "312312");
            Check(MatchDetector.FindMatches(dead).Count == 0, "el tablero de prueba tiene líneas");
            Check(!MoveFinder.HasMove(dead), "debía estar bloqueado");
            var moves = BoardGenerator.TryReshuffle(dead, new GameRng(3));
            Check(moves.Count == 36, "debía mover las 36 fichas");
            Check(MatchDetector.FindMatches(dead).Count == 0, "reordenado con líneas");
            Check(MoveFinder.HasMove(dead), "reordenado sin movimientos");
            var counts = dead.ToString().Where(char.IsDigit).GroupBy(ch => ch).ToDictionary(g => g.Key, g => g.Count());
            Check(counts['1'] == 12 && counts['2'] == 12 && counts['3'] == 12, "cambió la cantidad de fichas por tipo");
        }

        static void WinCondition()
        {
            var s = new GameSession(new LevelConfig { Seed = 2, Moves = 100, TargetScore = 30 });
            int guard = 0;
            while (s.State == GameState.Playing && guard++ < 50) { s.TryGetHint(out var a, out var c); s.TrySwap(a, c); }
            Check(s.State == GameState.Won && s.Score >= 30, "debía ganar, estado " + s.State);
        }

        static void LoseCondition()
        {
            var s = new GameSession(new LevelConfig { Seed = 2, Moves = 2, TargetScore = 1000000 });
            for (int i = 0; i < 2; i++) { s.TryGetHint(out var a, out var c); s.TrySwap(a, c); }
            Check(s.State == GameState.Lost, "debía perder, estado " + s.State);
            Check(s.Moves.MovesLeft == 0, "movimientos restantes distintos de 0");
            s.TryGetHint(out var a2, out var c2);
            var r = s.TrySwap(a2, c2);
            Check(!r.Accepted && r.Steps.Count == 0, "tras perder no debe aceptar intercambios");
        }

        static void Stars()
        {
            var sc = new ScoreManager(1000);
            Check(sc.Stars == 0, "0 estrellas"); sc.Add(1000); Check(sc.Stars == 1, "1 estrella");
            sc.Add(500); Check(sc.Stars == 2, "2 estrellas"); sc.Add(500); Check(sc.Stars == 3, "3 estrellas");
        }
    }
}
