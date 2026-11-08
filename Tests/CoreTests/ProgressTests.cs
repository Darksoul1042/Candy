using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PetaloPop.Core;

namespace CoreTests
{
    static class ProgressTests
    {
        public static void Register(Action<string, Action> run)
        {
            run("Catálogo: 30 niveles se generan, con objetivos y obstáculos coherentes", Catalog);
            run("Equilibrio: niveles ganables (con potenciadores todos; sin ellos los 25 primeros), 12 intentos por bot", Balance);
            run("Guardado: ida y vuelta conserva nivel, desbloqueos, estrellas, puntuaciones, potenciadores y sonido", RoundTrip);
            run("Guardado: datos dañados o vacíos no rompen; valores fuera de rango se acotan", Corrupt);
            run("Progreso: ganar desbloquea el siguiente, perder no; estrellas solo mejoran", Progress);
            run("Guardado en archivo real: escribir, reabrir y recuperar", FileRoundTrip);
        }

        static void Catalog()
        {
            for (int l = 1; l <= LevelCatalog.Count; l++)
            {
                var cfg = LevelCatalog.Get(l);
                var s = new GameSession(cfg);
                T.Check(s.Board.IsFull() && MatchDetector.FindMatches(s.Board).Count == 0 && MoveFinder.HasMove(s.Board), "tablero inicial inválido, nivel " + l);
                foreach (var o in s.Objectives.Items) T.Check(o.Target > 0, $"objetivo sin meta en nivel {l}");
                if (l >= 11 && l <= 15) T.Check(s.Board.CountWhere(p => s.Board.Jelly(p) > 0) > 0, "nivel " + l + " sin gelatina");
                if (l >= 16 && l <= 20) T.Check(s.Board.CountWhere(p => s.Board.Hp(p) > 0 && s.Board[p] > 0) > 0, "nivel " + l + " sin congeladas");
                if (l >= 21) T.Check(s.Board.CountWhere(p => s.Board[p] == Board.Crate) > 0, "nivel " + l + " sin cajas");
                if (l >= 26) T.Check(s.Board.CountWhere(p => s.Board[p] == Board.Stone) > 0, "nivel " + l + " sin piedras");
            }
            bool threw = false;
            try { LevelCatalog.Get(31); } catch (ArgumentOutOfRangeException) { threw = true; }
            T.Check(threw, "el nivel 31 no existe");
        }

        static void Balance()
        {
            var lines = new List<string>();
            for (int l = 1; l <= LevelCatalog.Count; l++)
            {
                int plain = 0, boosted = 0, novice = 0;
                for (int t = 0; t < 12; t++)
                {
                    if (Bot.Play(LevelCatalog.Get(l), 100 + t, out _, out _) == GameState.Won) plain++;
                    if (Bot.Play(LevelCatalog.Get(l), 100 + t, out _, out _, false, Bot.StartingInventory()) == GameState.Won) boosted++;
                    if (Bot.Play(LevelCatalog.Get(l), 100 + t, out _, out _, true) == GameState.Won) novice++;
                }
                lines.Add($"        nivel {l,2}: voraz {plain,2}/12 | voraz+potenciadores {boosted,2}/12 | novato al azar {novice,2}/12  (movs {LevelCatalog.Get(l).Moves}, meta {LevelCatalog.Get(l).TargetScore})");
                T.Check(boosted >= 1, "el bot con potenciadores no gana el nivel " + l + "\n" + string.Join("\n", lines));
                if (l <= 25) T.Check(plain >= 1, "el bot sin potenciadores no gana el nivel " + l + "\n" + string.Join("\n", lines));
            }
            foreach (var ln in lines) Console.WriteLine(ln);
        }

        static void RoundTrip()
        {
            var mem = new MemoryStorage();
            var a = new SaveSystem(mem);
            a.RecordResult(1, true, 1500, 2, a.CreateInventory());
            a.RecordResult(2, true, 900, 3, a.CreateInventory());
            var inv = a.CreateInventory(); inv.TryConsume(BoosterKind.Hammer);
            a.RecordResult(3, false, 400, 0, inv);
            a.SetSound(false, true);
            var b = new SaveSystem(mem); // "cerrar y volver a abrir"
            T.Check(b.Data.HighestUnlocked == 3 && b.Data.CurrentLevel == 3, $"desbloqueo {b.Data.HighestUnlocked}/{b.Data.CurrentLevel}");
            T.Check(b.StarsOf(1) == 2 && b.StarsOf(2) == 3 && b.StarsOf(3) == 0, "estrellas");
            T.Check(b.BestOf(1) == 1500 && b.BestOf(3) == 400, "mejores puntuaciones (incluso perdiendo)");
            T.Check(b.Data.Boosters[BoosterKind.Hammer] == 2 && b.Data.Boosters[BoosterKind.ExtraMoves] == 2, "potenciadores");
            T.Check(!b.Data.SoundOn && b.Data.MusicOn, "ajustes de sonido");
            T.Check(b.IsUnlocked(3) && !b.IsUnlocked(4), "IsUnlocked");
        }

        static void Corrupt()
        {
            foreach (var text in new[] { "", "   ", "basura sin formato", "current=abc\nunlocked=\n=5\nstars.x=2", "current=-9\nunlocked=999\nstars.1=77\nbest.1=-5\nbooster.Hammer=-3\nbooster.Inexistente=4" })
            {
                var d = SaveSerializer.Deserialize(text);
                T.Check(d.CurrentLevel >= 1 && d.CurrentLevel <= 30 && d.HighestUnlocked >= 1 && d.HighestUnlocked <= 30, "niveles fuera de rango con: " + text);
                T.Check(d.Stars.Values.All(v => v >= 0 && v <= 3) && d.BestScore.Values.All(v => v >= 0) && d.Boosters.Values.All(v => v >= 0), "valores sin acotar con: " + text);
            }
            var mem = new MemoryStorage { Text = "current=7\nunlocked=5" };
            var s = new SaveSystem(mem);
            T.Check(s.Data.HighestUnlocked >= s.Data.CurrentLevel, "no puede estar el nivel actual bloqueado");
        }

        static void Progress()
        {
            var s = new SaveSystem(new MemoryStorage());
            s.RecordResult(1, false, 300, 0, null);
            T.Check(!s.IsUnlocked(2), "perder no debe desbloquear");
            s.RecordResult(1, true, 1000, 3, null);
            T.Check(s.IsUnlocked(2) && s.StarsOf(1) == 3, "ganar desbloquea el siguiente");
            s.RecordResult(1, true, 800, 1, null);
            T.Check(s.StarsOf(1) == 3 && s.BestOf(1) == 1000, "las estrellas y la puntuación solo mejoran");
            s.RecordResult(30, true, 9999, 3, null);
            T.Check(s.Data.HighestUnlocked == 30 && s.Data.CurrentLevel == 30, "el último nivel no pasa de 30");
        }

        static void FileRoundTrip()
        {
            string path = Path.Combine(Path.GetTempPath(), "petalopop_test_" + Guid.NewGuid().ToString("N"), "save.txt");
            try
            {
                var a = new SaveSystem(new FileStorage(path));
                a.RecordResult(1, true, 1234, 2, a.CreateInventory());
                var b = new SaveSystem(new FileStorage(path));
                T.Check(b.StarsOf(1) == 2 && b.BestOf(1) == 1234 && b.IsUnlocked(2), "no se recuperó desde archivo");
                File.WriteAllText(path, "\u0000\u0001 corrupto");
                var c = new SaveSystem(new FileStorage(path));
                T.Check(c.Data.HighestUnlocked == 1, "un archivo corrupto debía dar partida nueva sin fallar");
            }
            finally { try { Directory.Delete(Path.GetDirectoryName(path), true); } catch (Exception) { } }
        }
    }
}
