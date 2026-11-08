using System;
using System.Collections.Generic;
using System.Linq;
using PetaloPop.Core;

namespace CoreTests
{
    static class ObstacleTests
    {
        public static void Register(Action<string, Action> run)
        {
            run("Gelatina: pierde capas al eliminar la ficha de su celda y avisa con Hit", Jelly);
            run("Congelada: sobrevive al primer golpe, no se puede intercambiar y cuenta para el objetivo", Frozen);
            run("Caja: recibe golpe por celda vecina, no se intercambia ni se empareja", Crate);
            run("Caja de 2 capas necesita dos golpes", CrateTwoLayers);
            run("Piedra: solo la destruyen especiales/potenciadores; no recibe golpes de vecinas ni de líneas normales", StoneSurvives);
            run("Objetivo CollectType se cumple y gana la partida", CollectObjective);
            run("Capa de nivel: TileRows/JellyRows se aplican y se validan", LayoutApplied);
            run("Reordenado con obstáculos: congeladas y bloqueos no se mueven", ReshuffleFixed);
            run("Juego aleatorio con obstáculos (150 movimientos): invariantes y reproducción de pasos", RandomWithObstacles);
            run("Potenciador Martillo: elimina ficha, caja o piedra, gasta inventario y rechaza sin existencias", Hammer);
            run("Potenciador Quitar obstáculo: libera ficha congelada y quita gelatina sin eliminar la ficha", Remover);
            run("Potenciador Movimientos extra y Crear especial", ExtraAndMaker);
            run("Potenciador Intercambio libre: no cuesta movimiento ni exige línea", FreeSwap);
            run("Clone de sesión es independiente del original", CloneIndependent);
        }

        static Board Row0Line()
        {
            var b = T.Quiet();
            b[1, 0] = 5; b[2, 0] = 5; b[3, 1] = 5;
            return b;
        }

        static MoveResult PlayRow0(GameSession s) => s.TrySwap(T.P(3, 1), T.P(3, 0));

        static void Jelly()
        {
            var b = Row0Line();
            b.SetJelly(T.P(1, 0), 2); b.SetJelly(T.P(2, 0), 1); b.SetJelly(T.P(5, 5), 1);
            var s = T.Make(b, obj: new List<ObjectiveSpec> { new ObjectiveSpec { Kind = ObjectiveKind.ClearJelly } });
            T.Check(s.Objectives.Items[0].Target == 3, "objetivo debía ser 3 celdas de gelatina");
            var r = PlayRow0(s);
            var c = T.FirstClear(r);
            T.Check(c.Hits.Count(h => h.Kind == ObstacleKind.Jelly) == 2, "esperaba 2 golpes de gelatina");
            T.Check(s.Board.Jelly(T.P(1, 0)) == 1 && s.Board.Jelly(T.P(2, 0)) == 0, "capas incorrectas");
            T.Check(s.Objectives.Items[0].Current == 1 && !s.Objectives.AllDone, "progreso del objetivo incorrecto");
        }

        static void Frozen()
        {
            var b = Row0Line();
            b.SetHp(T.P(1, 0), 1);
            var s = T.Make(b, target: 0, obj: new List<ObjectiveSpec> { new ObjectiveSpec { Kind = ObjectiveKind.BreakFrozen } });
            T.Check(!s.TrySwap(T.P(1, 0), T.P(0, 0)).Accepted && s.TrySwap(T.P(1, 0), T.P(0, 0)).Steps.Count == 0, "una ficha congelada no se puede intercambiar");
            var r = PlayRow0(s);
            var c = T.FirstClear(r);
            T.Check(!c.Cleared.Contains(T.P(1, 0)), "la ficha congelada no debía eliminarse");
            T.Check(c.Hits.Any(h => h.Kind == ObstacleKind.Frozen && h.Remaining == 0), "debía registrar el golpe");
            T.Check(s.Board[1, 0] == 5 && s.Board.Hp(T.P(1, 0)) == 0, "la ficha debía quedar libre en su sitio");
            T.Check(s.Objectives.AllDone && s.State == GameState.Won, "debía ganar al romper la única congelada, estado " + s.State);
        }

        static void Crate()
        {
            var b = Row0Line();
            b[2, 1] = Board.Crate; b.SetHp(T.P(2, 1), 1);
            var s = T.Make(b, obj: new List<ObjectiveSpec> { new ObjectiveSpec { Kind = ObjectiveKind.DestroyCrates } });
            T.Check(s.Objectives.Items[0].Target == 1, "objetivo 1 caja");
            T.Check(s.TrySwap(T.P(2, 1), T.P(2, 2)).Steps.Count == 0, "una caja no se puede intercambiar");
            var c = T.FirstClear(PlayRow0(s));
            T.Check(c.Cleared.Contains(T.P(2, 1)), "la caja vecina debía destruirse");
            T.Check(s.Objectives.AllDone, "objetivo de cajas incompleto");
        }

        static void CrateTwoLayers()
        {
            var b = Row0Line();
            b[2, 1] = Board.Crate; b.SetHp(T.P(2, 1), 2);
            var s = T.Make(b);
            var c = T.FirstClear(PlayRow0(s));
            T.Check(!c.Cleared.Contains(T.P(2, 1)), "con 2 capas no debía caer aún");
            T.Check(c.Hits.Any(h => h.Kind == ObstacleKind.Crate && h.Remaining == 1), "debía quedar 1 capa");
            T.Check(s.Board.Hp(T.P(2, 0)) == 1 && s.Board[2, 0] == Board.Crate, "la caja cae con su vida (a (2,0))");
        }

        static void StoneSurvives()
        {
            var b = PlantedStone();
            var s = T.Make(b);
            var c = T.FirstClear(PlayRow0(s));
            T.Check(c.Cleared.Count == 7 && c.Cleared.Contains(T.P(5, 0)), "la piedra en la fila del LineH debía destruirse (7 celdas), hubo " + c.Cleared.Count);
            T.Check(s.Board.CountWhere(p => s.Board[p] == Board.Stone) == 1 && s.Board[2, 0] == Board.Stone, "la piedra vecina debía sobrevivir (y caer a (2,0))");
            var plain = T.Make(StoneNextToPlainMatch());
            var c2 = T.FirstClear(PlayRow0(plain));
            T.Check(c2.Cleared.Count == 3 && plain.Board.CountWhere(p => plain.Board[p] == Board.Stone) == 1, "una línea normal junto a una piedra no la toca");
        }

        static Board PlantedStone()
        {
            var b = Row0Line();
            b.SetSpecial(T.P(3, 1), SpecialKind.LineH);
            b[5, 0] = Board.Stone;  // en la fila que se limpia
            b[2, 1] = Board.Stone;  // vecina de una celda eliminada: no recibe golpes
            return b;
        }

        static Board StoneNextToPlainMatch()
        {
            var b = Row0Line();
            b[2, 1] = Board.Stone;
            return b;
        }

        static void CollectObjective()
        {
            var b = Row0Line();
            var s = T.Make(b, target: 0, obj: new List<ObjectiveSpec> { new ObjectiveSpec { Kind = ObjectiveKind.CollectType, Type = 5, Count = 3 } });
            PlayRow0(s);
            T.Check(s.Objectives.Items[0].Current == 3 && s.State == GameState.Won, $"progreso {s.Objectives.Items[0].Current}, estado {s.State}");
        }

        static LevelConfig MixedLevel(int seed) => new LevelConfig
        {
            Width = 9, Height = 9, TypeCount = 5, Moves = 500, TargetScore = int.MaxValue / 4, Seed = seed,
            TileRows = new[]
            {
                "S.C...K.S",
                ".c.....c.",
                ".........",
                "...f.F...",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
            },
            JellyRows = new[]
            {
                ".........", ".........", ".........", ".........", ".........",
                "..1...1..", ".2.....2.", "111111111", "222222222",
            },
            Objectives = new List<ObjectiveSpec> { new ObjectiveSpec { Kind = ObjectiveKind.ClearJelly } }
        };

        static void LayoutApplied()
        {
            var s = new GameSession(MixedLevel(3));
            var b = s.Board;
            T.Check(b[0, 8] == Board.Stone && b[8, 8] == Board.Stone, "piedras en la fila superior");
            T.Check(b[2, 8] == Board.Crate && b.Hp(T.P(2, 8)) == 2 && b[6, 8] == Board.Crate && b.Hp(T.P(6, 8)) == 3, "cajas con sus capas");
            T.Check(b[3, 5] > 0 && b.Hp(T.P(3, 5)) == 1 && b.Hp(T.P(5, 5)) == 2, "congeladas con sus capas");
            T.Check(b.Jelly(T.P(0, 0)) == 2 && b.Jelly(T.P(0, 1)) == 1 && b.Jelly(T.P(1, 2)) == 2, "gelatina por filas");
            T.Check(MatchDetector.FindMatches(b).Count == 0, "no debía haber líneas iniciales");
            bool threw = false;
            try { new GameSession(new LevelConfig { TileRows = new[] { "...." } }); } catch (ArgumentException) { threw = true; }
            T.Check(threw, "una capa con dimensiones erróneas debía lanzar ArgumentException");
        }

        static void ReshuffleFixed()
        {
            var b = T.Quiet(8, 8);
            b[1, 1] = Board.Crate; b.SetHp(T.P(1, 1), 1);
            b[2, 2] = Board.Stone;
            b.SetHp(T.P(3, 3), 1);
            b.SetSpecial(T.P(5, 5), SpecialKind.Bomb);
            var before = b.Clone();
            var moves = BoardGenerator.TryReshuffle(b, new GameRng(11));
            T.Check(moves != null, "debía poder reordenarse");
            T.Check(b[1, 1] == Board.Crate && b[2, 2] == Board.Stone && b.Hp(T.P(3, 3)) == 1 && b[3, 3] == before[3, 3], "los fijos se movieron");
            T.Check(moves.All(m => before.IsMovable(m.From) && before.IsMovable(m.To)), "se movió una ficha no móvil");
            T.Check(b.CountSpecials() == 1, "el especial debía conservarse");
            T.Check(MatchDetector.FindMatches(b).Count == 0 && MoveFinder.HasMove(b), "tablero reordenado inválido");
        }

        static void RandomWithObstacles()
        {
            int moved = 0, jellyHits = 0, crateHits = 0, frozenHits = 0;
            for (int seed = 1; seed <= 6; seed++)
            {
                var rng = new GameRng(seed);
                var s = new GameSession(MixedLevel(seed));
                for (int i = 0; i < 25; i++)
                {
                    var all = MoveFinder.FindAllMoves(s.Board);
                    if (all.Count == 0) break;
                    var m = all[rng.Next(all.Count)];
                    var start = s.Board.Clone();
                    var r = s.TrySwap(m.From, m.To);
                    T.Check(r.Accepted, "movimiento válido rechazado");
                    moved++;
                    foreach (var st in r.Steps) foreach (var h in st.Hits)
                        { if (h.Kind == ObstacleKind.Jelly) jellyHits++; else if (h.Kind == ObstacleKind.Crate) crateHits++; else if (h.Kind == ObstacleKind.Frozen) frozenHits++; }
                    T.Check(s.Board.IsFull(), "tablero con huecos");
                    T.Check(MatchDetector.FindMatches(s.Board).Count == 0, "quedaron líneas");
                    var g = Program.Replay(start, r);
                    for (int x = 0; x < 9; x++) for (int y = 0; y < 9; y++)
                        {
                            var p = new Position(x, y);
                            T.Check(g.Type[x, y] == s.Board[p], $"tipo difiere {x},{y} (semilla {seed}, mov {i})");
                            T.Check(g.Hp[x, y] == s.Board.Hp(p), $"vida difiere {x},{y} (semilla {seed}, mov {i})");
                            T.Check(g.Jelly[x, y] == s.Board.Jelly(p), $"gelatina difiere {x},{y} (semilla {seed}, mov {i})");
                            T.Check(g.Special[x, y] == s.Board.SpecialAt(p), $"especial difiere {x},{y} (semilla {seed}, mov {i})");
                        }
                }
            }
            T.Check(jellyHits > 10, "poca cobertura de gelatina: " + jellyHits);
            Console.WriteLine($"        (cobertura: {moved} movimientos, golpes gelatina {jellyHits}, cajas {crateHits}, congeladas {frozenHits})");
        }

        static BoosterManager Inv(BoosterKind k, int n = 1) { var m = new BoosterManager(); m.Add(k, n); return m; }

        static void Hammer()
        {
            var b = T.Quiet();
            b[4, 4] = Board.Crate; b.SetHp(T.P(4, 4), 3);
            b[1, 1] = Board.Stone;
            var s = T.Make(b, inv: Inv(BoosterKind.Hammer, 3));
            var r = s.UseHammer(T.P(4, 4));
            T.Check(r.Accepted && T.FirstClear(r).Cleared.Contains(T.P(4, 4)), "el martillo debía destruir la caja de 3 capas");
            T.Check(s.Moves.MovesLeft == 20, "un potenciador no cuesta movimiento");
            r = s.UseHammer(T.P(1, 1));
            T.Check(r.Accepted && T.FirstClear(r).Cleared.Contains(T.P(1, 1)), "el martillo debía quitar la piedra");
            r = s.UseHammer(T.P(0, 0));
            T.Check(r.Accepted && s.Boosters.Count(BoosterKind.Hammer) == 0, "tercer uso y existencias a 0");
            r = s.UseHammer(T.P(2, 2));
            T.Check(!r.Accepted && r.Steps.Count == 0, "sin existencias debía rechazarse");
        }

        static void Remover()
        {
            var b = T.WithMove(T.Quiet());
            b.SetHp(T.P(2, 2), 2); b.SetJelly(T.P(3, 3), 2);
            var s = T.Make(b, inv: Inv(BoosterKind.ObstacleRemover, 2), obj: new List<ObjectiveSpec>
            { new ObjectiveSpec { Kind = ObjectiveKind.BreakFrozen }, new ObjectiveSpec { Kind = ObjectiveKind.ClearJelly } });
            int tile = s.Board[2, 2];
            var r = s.UseObstacleRemover(T.P(2, 2));
            T.Check(r.Accepted && s.Board.Hp(T.P(2, 2)) == 0 && s.Board[2, 2] == tile, "la ficha debía liberarse sin eliminarse");
            r = s.UseObstacleRemover(T.P(3, 3));
            T.Check(r.Accepted && s.Board.Jelly(T.P(3, 3)) == 0, "la gelatina debía quitarse");
            T.Check(s.Objectives.AllDone, "los objetivos debían contar el obstáculo quitado");
            T.Check(!s.UseObstacleRemover(T.P(0, 0)).Accepted && s.Boosters.Count(BoosterKind.ObstacleRemover) == 0, "no se gasta si no hay obstáculo");
        }

        static void ExtraAndMaker()
        {
            var inv = Inv(BoosterKind.ExtraMoves); inv.Add(BoosterKind.SpecialMaker, 2);
            var s = T.Make(T.WithMove(T.Quiet()), inv: inv);
            T.Check(s.UseExtraMoves().Accepted && s.Moves.MovesLeft == 25, "debía dar +5 movimientos");
            var r = s.UseSpecialMaker(T.P(3, 3), SpecialKind.LineH);
            T.Check(r.Accepted && s.Board.SpecialAt(T.P(3, 3)) == SpecialKind.LineH && r.Steps[0].Created.Count == 1, "debía crear LineH");
            T.Check(!s.UseSpecialMaker(T.P(3, 3), SpecialKind.Bomb).Accepted && s.Boosters.Count(BoosterKind.SpecialMaker) == 1, "no sobre una ficha que ya es especial");
        }

        static void FreeSwap()
        {
            var s = T.Make(T.WithMove(T.Quiet()), inv: Inv(BoosterKind.FreeSwap));
            int a = s.Board[0, 0], b2 = s.Board[1, 0];
            var r = s.UseFreeSwap(T.P(0, 0), T.P(1, 0));
            T.Check(r.Accepted && s.Moves.MovesLeft == 20, "no debía costar movimiento");
            T.Check(s.Board[0, 0] == b2 && s.Board[1, 0] == a, "las fichas debían intercambiarse sin exigir línea");
        }

        static void CloneIndependent()
        {
            var s = new GameSession(new LevelConfig { Seed = 4, Moves = 50, TargetScore = 100000 });
            var c = s.Clone();
            s.TryGetHint(out var a, out var b);
            s.TrySwap(a, b);
            T.Check(c.Score == 0 && c.Moves.MovesLeft == 50, "el clon no debía cambiar");
            c.TrySwap(a, b);
            T.Check(c.Board.ToString() == s.Board.ToString() && c.Score == s.Score, "el clon debía dar el mismo resultado (determinista)");
        }
    }
}
