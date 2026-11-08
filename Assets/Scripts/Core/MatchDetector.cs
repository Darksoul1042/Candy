using System.Collections.Generic;

namespace PetaloPop.Core
{
    public sealed class Match
    {
        public int Type;
        public bool Horizontal;
        public List<Position> Cells = new List<Position>();
        public int Length => Cells.Count;
    }

    /// <summary>Detecta líneas de 3 o más fichas iguales (horizontales y verticales).</summary>
    public static class MatchDetector
    {
        public const int MinMatch = 3;

        public static List<Match> FindMatches(Board b)
        {
            var result = new List<Match>();
            for (int y = 0; y < b.Height; y++) ScanLine(b, result, true, y, b.Width);
            for (int x = 0; x < b.Width; x++) ScanLine(b, result, false, x, b.Height);
            return result;
        }

        private static void ScanLine(Board b, List<Match> result, bool horizontal, int fixedIdx, int length)
        {
            int runStart = 0;
            for (int i = 1; i <= length; i++)
            {
                int prev = Get(b, horizontal, fixedIdx, i - 1);
                int cur = i < length ? Get(b, horizontal, fixedIdx, i) : -1;
                if (cur == prev && prev > 0) continue;
                int runLen = i - runStart;
                if (prev > 0 && runLen >= MinMatch)
                {
                    var m = new Match { Type = prev, Horizontal = horizontal };
                    for (int k = runStart; k < i; k++)
                        m.Cells.Add(horizontal ? new Position(k, fixedIdx) : new Position(fixedIdx, k));
                    result.Add(m);
                }
                runStart = i;
            }
        }

        private static int Get(Board b, bool horizontal, int fixedIdx, int i) =>
            horizontal ? b[i, fixedIdx] : b[fixedIdx, i];

        public static HashSet<Position> UnionOfCells(IEnumerable<Match> matches)
        {
            var set = new HashSet<Position>();
            foreach (var m in matches) foreach (var c in m.Cells) set.Add(c);
            return set;
        }

        /// <summary>¿La ficha en p forma parte de una línea de 3+? (comprobación local, rápida)</summary>
        public static bool IsPartOfMatch(Board b, Position p)
        {
            int t = b[p];
            if (t <= 0) return false;
            int h = 1 + Count(b, p, -1, 0, t) + Count(b, p, 1, 0, t);
            if (h >= MinMatch) return true;
            int v = 1 + Count(b, p, 0, -1, t) + Count(b, p, 0, 1, t);
            return v >= MinMatch;
        }

        private static int Count(Board b, Position p, int dx, int dy, int t)
        {
            int n = 0, x = p.X + dx, y = p.Y + dy;
            while (b.InBounds(x, y) && b[x, y] == t) { n++; x += dx; y += dy; }
            return n;
        }
    }
}
