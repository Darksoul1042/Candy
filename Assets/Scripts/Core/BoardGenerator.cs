using System;
using System.Collections.Generic;

namespace PetaloPop.Core
{
    public static class BoardGenerator
    {
        public static Board Create(int width, int height, int typeCount, GameRng rng) =>
            Create(new LevelConfig { Width = width, Height = height, TypeCount = typeCount }, rng);

        /// <summary>Tablero aleatorio sin combinaciones iniciales y con al menos un movimiento posible, con la capa de obstáculos del nivel.</summary>
        public static Board Create(LevelConfig cfg, GameRng rng)
        {
            if (cfg.TypeCount < 3) throw new ArgumentException("Se necesitan al menos 3 tipos de ficha.");
            ValidateRows(cfg.TileRows, cfg, "TileRows");
            ValidateRows(cfg.JellyRows, cfg, "JellyRows");

            for (int attempt = 0; attempt < 1000; attempt++)
            {
                var b = new Board(cfg.Width, cfg.Height);
                for (int y = 0; y < cfg.Height; y++)
                    for (int x = 0; x < cfg.Width; x++)
                        b[x, y] = PickWithoutMatch(b, x, y, cfg.TypeCount, rng);
                ApplyLayout(b, cfg);
                if (MoveFinder.HasMove(b)) return b;
            }
            throw new InvalidOperationException("No se pudo generar un tablero válido.");
        }

        private static void ValidateRows(string[] rows, LevelConfig cfg, string name)
        {
            if (rows == null) return;
            if (rows.Length != cfg.Height) throw new ArgumentException(name + ": se esperaban " + cfg.Height + " filas.");
            foreach (var r in rows) if (r.Length != cfg.Width) throw new ArgumentException(name + ": se esperaban " + cfg.Width + " columnas.");
        }

        private static void ApplyLayout(Board b, LevelConfig cfg)
        {
            for (int row = 0; row < cfg.Height; row++)
                for (int x = 0; x < cfg.Width; x++)
                {
                    var p = new Position(x, cfg.Height - 1 - row);
                    if (cfg.TileRows != null)
                        switch (cfg.TileRows[row][x])
                        {
                            case 'f': b.SetHp(p, 1); break;
                            case 'F': b.SetHp(p, 2); break;
                            case 'c': b[p] = Board.Crate; b.SetHp(p, 1); break;
                            case 'C': b[p] = Board.Crate; b.SetHp(p, 2); break;
                            case 'K': b[p] = Board.Crate; b.SetHp(p, 3); break;
                            case 'S': b[p] = Board.Stone; break;
                        }
                    if (cfg.JellyRows != null && cfg.JellyRows[row][x] >= '1' && cfg.JellyRows[row][x] <= '9')
                        b.SetJelly(p, cfg.JellyRows[row][x] - '0');
                }
        }

        private static int PickWithoutMatch(Board b, int x, int y, int typeCount, GameRng rng)
        {
            var allowed = new List<int>(typeCount);
            for (int t = 1; t <= typeCount; t++)
            {
                bool hMatch = x >= 2 && b[x - 1, y] == t && b[x - 2, y] == t;
                bool vMatch = y >= 2 && b[x, y - 1] == t && b[x, y - 2] == t;
                if (!hMatch && !vMatch) allowed.Add(t);
            }
            return allowed[rng.Next(allowed.Count)];
        }

        /// <summary>
        /// Reordena las fichas móviles (no congeladas ni bloqueos; los especiales viajan con su ficha) hasta que no haya líneas
        /// y exista movimiento. Devuelve los desplazamientos, o null si no se encontró una disposición válida.
        /// </summary>
        public static List<TileMove> TryReshuffle(Board b, GameRng rng)
        {
            var pos = new List<Position>();
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                {
                    var p = new Position(x, y);
                    if (b.IsMovable(p)) pos.Add(p);
                }
            int n = pos.Count;
            if (n < 3) return null;
            var types = new int[n];
            var specs = new SpecialKind[n];
            for (int i = 0; i < n; i++) { types[i] = b[pos[i]]; specs[i] = b.SpecialAt(pos[i]); }

            for (int attempt = 0; attempt < 500; attempt++)
            {
                var perm = new int[n];
                for (int i = 0; i < n; i++) perm[i] = i;
                for (int i = n - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (perm[i], perm[j]) = (perm[j], perm[i]);
                }
                var trial = b.Clone();
                for (int i = 0; i < n; i++) { trial[pos[i]] = types[perm[i]]; trial.SetSpecial(pos[i], specs[perm[i]]); }
                if (MatchDetector.FindMatches(trial).Count > 0 || !MoveFinder.HasMove(trial)) continue;

                var moves = new List<TileMove>(n);
                for (int i = 0; i < n; i++)
                {
                    b[pos[i]] = types[perm[i]];
                    b.SetSpecial(pos[i], specs[perm[i]]);
                    moves.Add(new TileMove(pos[perm[i]], pos[i], types[perm[i]], specs[perm[i]]));
                }
                return moves;
            }
            return null;
        }
    }
}
