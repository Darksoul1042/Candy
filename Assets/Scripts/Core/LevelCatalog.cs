using System.Collections.Generic;

namespace PetaloPop.Core
{
    /// <summary>
    /// 30 niveles originales, generados por reglas simples (no copian ningún nivel existente).
    /// Sirve de semilla para crear los ScriptableObject en el editor y para las pruebas de equilibrio.
    /// Tramos: 1-5 básicos, 6-10 recoger color, 11-15 gelatina, 16-20 congeladas, 21-25 cajas, 26-30 mezcla con piedras.
    /// </summary>
    public static class LevelCatalog
    {
        public const int Count = 30;

        // Puntos por movimiento medidos con un jugador voraz (primer tramo; ver Tests/Calib).
        private static readonly int[] PointsPerMove = { 700, 700, 420, 420, 520 };

        private static int Round100(double v) => (int)(System.Math.Round(v / 100.0) * 100);
        private static int Moves150(int moves) => Round100(moves * 150);

        public static LevelConfig Get(int level)
        {
            if (level < 1 || level > Count) throw new System.ArgumentOutOfRangeException(nameof(level));
            int i = level - 1, tier = i / 5, j = i % 5;
            int size = (tier == 0 && j < 2) ? 7 : (tier == 0 && j < 4) ? 8 : 9;
            var c = new LevelConfig
            {
                Width = size, Height = size,
                TypeCount = (tier == 0 && j < 2) ? 4 : (tier >= 4 ? 6 : 5),
                Seed = 7000 + level * 31,
            };
            var rng = new GameRng(c.Seed);

            switch (tier)
            {
                case 0:
                    c.Moves = 20 + j * 2;
                    c.TargetScore = Round100(c.Moves * PointsPerMove[j] * (0.35 + 0.08 * j)); // fracción de lo que logra un jugador voraz
                    break;
                case 1:
                    c.Moves = 24 + j * 2; c.TargetScore = Moves150(c.Moves);
                    c.Objectives.Add(new ObjectiveSpec { Kind = ObjectiveKind.CollectType, Type = j % c.TypeCount + 1, Count = 45 + j * 10 });
                    break;
                case 2:
                    c.Moves = 26 + j * 3; c.TargetScore = Moves150(c.Moves);
                    c.JellyRows = JellyPattern(c.Width, c.Height, j);
                    c.Objectives.Add(new ObjectiveSpec { Kind = ObjectiveKind.ClearJelly });
                    break;
                case 3:
                    c.Moves = 28 + j * 3; c.TargetScore = Moves150(c.Moves);
                    c.TileRows = Scatter(c.Width, c.Height, rng, 'f', 5 + j * 2, j >= 3 ? 'F' : 'f', j >= 3 ? 3 : 0);
                    c.Objectives.Add(new ObjectiveSpec { Kind = ObjectiveKind.BreakFrozen });
                    break;
                case 4:
                    c.Moves = 32 + j * 2; c.TargetScore = Round100(c.Moves * 120);
                    c.TileRows = TopCrates(c.Width, c.Height, rng, 5 + j, j >= 2 ? 'C' : 'c');
                    c.Objectives.Add(new ObjectiveSpec { Kind = ObjectiveKind.DestroyCrates });
                    break;
                default:
                    c.Moves = 50 + j * 3; c.TargetScore = Round100(c.Moves * 80);
                    c.JellyRows = JellyPattern(c.Width, c.Height, 1);
                    var rows = TopCrates(c.Width, c.Height, rng, 4, 'c');
                    rows = Overlay(rows, Scatter(c.Width, c.Height, rng, 'f', 6, 'F', j));
                    rows = PutStones(rows, 2);
                    c.TileRows = rows;
                    c.Objectives.Add(new ObjectiveSpec { Kind = ObjectiveKind.ClearJelly });
                    c.Objectives.Add(new ObjectiveSpec { Kind = ObjectiveKind.BreakFrozen });
                    c.Objectives.Add(new ObjectiveSpec { Kind = ObjectiveKind.DestroyCrates });
                    if (j >= 2) c.Objectives.Add(new ObjectiveSpec { Kind = ObjectiveKind.CollectType, Type = 1, Count = 30 + j * 4 });
                    break;
            }
            return c;
        }

        private static char[][] Empty(int w, int h)
        {
            var g = new char[h][];
            for (int r = 0; r < h; r++) { g[r] = new char[w]; for (int x = 0; x < w; x++) g[r][x] = '.'; }
            return g;
        }

        private static string[] ToRows(char[][] g)
        {
            var rows = new string[g.Length];
            for (int r = 0; r < g.Length; r++) rows[r] = new string(g[r]);
            return rows;
        }

        // Filas: índice 0 = superior. La gelatina se concentra abajo (las fichas caen hacia allí).
        private static string[] JellyPattern(int w, int h, int j)
        {
            var g = Empty(w, h);
            int rowsFromBottom = 2 + j % 3;
            for (int k = 0; k < rowsFromBottom; k++)
                for (int x = 0; x < w; x++)
                    g[h - 1 - k][x] = (j >= 3 && k == 0 && x > 1 && x < w - 2) ? '2' : '1';
            if (j == 2) for (int x = 0; x < w; x += 2) g[h - 1 - rowsFromBottom][x] = '1';
            return ToRows(g);
        }

        private static string[] Scatter(int w, int h, GameRng rng, char a, int countA, char b, int countB)
        {
            var g = Empty(w, h);
            Place(g, rng, a, countA, 2, h - 1);
            Place(g, rng, b, countB, 2, h - 1);
            return ToRows(g);
        }

        private static void Place(char[][] g, GameRng rng, char ch, int count, int minRow, int maxRow)
        {
            int w = g[0].Length, placed = 0, guard = 0;
            while (placed < count && guard++ < 1000)
            {
                int r = rng.Next(minRow, maxRow + 1), x = rng.Next(w);
                if (g[r][x] != '.') continue;
                g[r][x] = ch; placed++;
            }
        }

        private static string[] TopCrates(int w, int h, GameRng rng, int count, char kind)
        {
            var g = Empty(w, h);
            Place(g, rng, kind, count, 0, 1);
            return ToRows(g);
        }

        private static string[] Overlay(string[] baseRows, string[] top)
        {
            var o = new string[baseRows.Length];
            for (int r = 0; r < o.Length; r++)
            {
                var chars = baseRows[r].ToCharArray();
                for (int x = 0; x < chars.Length; x++) if (chars[x] == '.') chars[x] = top[r][x];
                o[r] = new string(chars);
            }
            return o;
        }

        private static string[] PutStones(string[] rows, int count)
        {
            var o = (string[])rows.Clone();
            int placed = 0;
            for (int x = 0; x < o[0].Length && placed < count; x += 3)
            {
                var chars = o[0].ToCharArray();
                if (chars[x] != '.') continue;
                chars[x] = 'S'; o[0] = new string(chars); placed++;
            }
            return o;
        }
    }
}
