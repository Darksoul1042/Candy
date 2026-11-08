using System.Collections.Generic;
using System.Linq;

namespace PetaloPop.Core
{
    /// <summary>
    /// Creación y activación de fichas especiales.
    /// 4 en línea horizontal -> LineV (limpia su columna); 4 en línea vertical -> LineH (limpia su fila);
    /// L/T -> Bomb (3x3); 5 en línea -> ColorBurst (limpia todas las fichas de su color).
    /// </summary>
    public static class SpecialTileSystem
    {
        public static bool IsComboSwap(Board b, Position a, Position c)
        {
            var ka = b.SpecialAt(a); var kc = b.SpecialAt(c);
            if (ka == SpecialKind.None && kc == SpecialKind.None) return false;
            if (ka == SpecialKind.ColorBurst || kc == SpecialKind.ColorBurst) return true;
            return ka != SpecialKind.None && kc != SpecialKind.None;
        }

        // ---- Desde combinaciones de líneas --------------------------------------------------
        public static ClearOutcome FromMatches(Board b, List<Match> matches, Position? swapA, Position? swapB)
        {
            var o = new ClearOutcome();
            var comps = new List<List<Match>>();
            foreach (var m in matches)
            {
                var touching = comps.Where(c => c.Any(x => x.Cells.Overlaps(m.Cells))).ToList();
                var merged = new List<Match> { m };
                foreach (var t in touching) { merged.AddRange(t.ToList()); comps.Remove(t); }
                comps.Add(merged);
            }

            var protect = new HashSet<Position>();
            foreach (var comp in comps)
            {
                var cells = new HashSet<Position>();
                foreach (var m in comp) foreach (var p in m.Cells) cells.Add(p);
                o.Cleared.UnionWith(cells);

                int maxRun = comp.Max(m => m.Length);
                bool hasH = comp.Any(m => m.Horizontal), hasV = comp.Any(m => !m.Horizontal);
                SpecialKind kind = SpecialKind.None;
                if (maxRun >= 5) kind = SpecialKind.ColorBurst;
                else if (hasH && hasV) kind = SpecialKind.Bomb;
                else if (maxRun == 4) kind = comp.First(m => m.Length == 4).Horizontal ? SpecialKind.LineV : SpecialKind.LineH;
                if (kind == SpecialKind.None) continue;

                var pos = PickSpawn(b, comp, cells, swapA, swapB, kind);
                if (pos == null) continue;
                o.Created.Add(new SpecialSpawn { Pos = pos.Value, Type = comp[0].Type, Kind = kind });
                protect.Add(pos.Value);
            }

            foreach (var p in protect) o.Cleared.Remove(p); // el especial nuevo ocupa su celda, no se elimina
            Expand(b, o, new HashSet<Position>(), protect);
            foreach (var p in protect) o.Cleared.Remove(p);
            return o;
        }

        private static Position? PickSpawn(Board b, List<Match> comp, HashSet<Position> cells, Position? a, Position? c, SpecialKind kind)
        {
            var candidates = new List<Position>();
            if (a.HasValue && cells.Contains(a.Value)) candidates.Add(a.Value);
            if (c.HasValue && cells.Contains(c.Value)) candidates.Add(c.Value);
            if (kind == SpecialKind.Bomb)
            {
                foreach (var h in comp.Where(m => m.Horizontal))
                    foreach (var v in comp.Where(m => !m.Horizontal))
                        foreach (var p in h.Cells) if (v.Cells.Contains(p)) candidates.Add(p);
            }
            var longest = comp.OrderByDescending(m => m.Length).First();
            candidates.Add(longest.Cells[longest.Length / 2]);
            candidates.AddRange(cells);
            foreach (var p in candidates)
                if (b.SpecialAt(p) == SpecialKind.None) return p;
            return null;
        }

        // ---- Combos al intercambiar dos especiales ------------------------------------------
        /// <summary>Se llama con el tablero ya intercambiado. Devuelve null si no es un combo.</summary>
        public static ClearOutcome FromSwapCombo(Board b, Position a, Position c)
        {
            if (!IsComboSwap(b, a, c)) return null;
            var o = new ClearOutcome();
            var pre = new HashSet<Position>();
            var ka = b.SpecialAt(a); var kc = b.SpecialAt(c);

            if (ka == SpecialKind.ColorBurst && kc == SpecialKind.ColorBurst)
            {
                for (int x = 0; x < b.Width; x++)
                    for (int y = 0; y < b.Height; y++)
                        if (b[x, y] != Board.Empty) o.Cleared.Add(new Position(x, y));
                o.Activated.Add(new ActivatedSpecial { Pos = a, Kind = ka });
                o.Activated.Add(new ActivatedSpecial { Pos = c, Kind = kc });
                pre.Add(a); pre.Add(c);
            }
            else if (ka == SpecialKind.ColorBurst || kc == SpecialKind.ColorBurst)
            {
                var burst = ka == SpecialKind.ColorBurst ? a : c;
                var other = ka == SpecialKind.ColorBurst ? c : a;
                var ko = b.SpecialAt(other);
                int t = b[other];
                o.Cleared.Add(burst);
                o.Activated.Add(new ActivatedSpecial { Pos = burst, Kind = SpecialKind.ColorBurst });
                pre.Add(burst);
                for (int x = 0; x < b.Width; x++)
                    for (int y = 0; y < b.Height; y++)
                    {
                        var p = new Position(x, y);
                        if (b[p] != t) continue;
                        o.Cleared.Add(p);
                        if (ko != SpecialKind.None) b.SetSpecial(p, ko); // se convierten y se activan todas
                    }
            }
            else
            {
                bool lineA = ka == SpecialKind.LineH || ka == SpecialKind.LineV;
                bool lineC = kc == SpecialKind.LineH || kc == SpecialKind.LineV;
                if (lineA && lineC) { AddRow(b, o.Cleared, c.Y, 0); AddCol(b, o.Cleared, c.X, 0); }
                else if (ka == SpecialKind.Bomb && kc == SpecialKind.Bomb) AddArea(b, o.Cleared, c, 2);
                else { for (int d = -1; d <= 1; d++) { AddRow(b, o.Cleared, c.Y + d, 0); AddCol(b, o.Cleared, c.X + d, 0); } }
                o.Cleared.Add(a); o.Cleared.Add(c);
                o.Activated.Add(new ActivatedSpecial { Pos = a, Kind = ka });
                o.Activated.Add(new ActivatedSpecial { Pos = c, Kind = kc });
                pre.Add(a); pre.Add(c);
            }
            Expand(b, o, pre, new HashSet<Position>());
            return o;
        }

        /// <summary>Activa en cadena todos los especiales que estén en el conjunto a eliminar.</summary>
        public static void Expand(Board b, ClearOutcome o, HashSet<Position> preActivated, HashSet<Position> protect)
        {
            var queue = new Queue<Position>(o.Cleared);
            var done = new HashSet<Position>(preActivated);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                var k = b.SpecialAt(p);
                if (k == SpecialKind.None || done.Contains(p) || protect.Contains(p)) continue;
                if (b[p] > 0 && b.Hp(p) > 0 && !o.ForceClear.Contains(p)) continue; // congelado: aún no se activa
                done.Add(p);
                o.Activated.Add(new ActivatedSpecial { Pos = p, Kind = k });
                foreach (var e in Effect(b, p, k))
                    if (o.Cleared.Add(e)) queue.Enqueue(e);
            }
        }

        public static IEnumerable<Position> Effect(Board b, Position p, SpecialKind k)
        {
            var set = new HashSet<Position>();
            switch (k)
            {
                case SpecialKind.LineH: AddRow(b, set, p.Y, 0); break;
                case SpecialKind.LineV: AddCol(b, set, p.X, 0); break;
                case SpecialKind.Bomb: AddArea(b, set, p, 1); break;
                case SpecialKind.ColorBurst:
                    int t = b[p];
                    for (int x = 0; x < b.Width; x++)
                        for (int y = 0; y < b.Height; y++)
                            if (b[x, y] == t) set.Add(new Position(x, y));
                    break;
            }
            return set;
        }

        private static void AddRow(Board b, HashSet<Position> set, int y, int _)
        {
            if (y < 0 || y >= b.Height) return;
            for (int x = 0; x < b.Width; x++) if (b[x, y] != Board.Empty) set.Add(new Position(x, y));
        }

        private static void AddCol(Board b, HashSet<Position> set, int x, int _)
        {
            if (x < 0 || x >= b.Width) return;
            for (int y = 0; y < b.Height; y++) if (b[x, y] != Board.Empty) set.Add(new Position(x, y));
        }

        private static void AddArea(Board b, HashSet<Position> set, Position c, int radius)
        {
            for (int x = c.X - radius; x <= c.X + radius; x++)
                for (int y = c.Y - radius; y <= c.Y + radius; y++)
                    if (b.InBounds(x, y) && b[x, y] != Board.Empty) set.Add(new Position(x, y));
        }
    }

    internal static class SetExt
    {
        public static bool Overlaps(this List<Position> list, List<Position> other)
        {
            foreach (var p in list) if (other.Contains(p)) return true;
            return false;
        }
    }
}
