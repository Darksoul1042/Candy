using System.Collections.Generic;

namespace PetaloPop.Core
{
    /// <summary>Valida intercambios y busca movimientos posibles (pistas / detección de bloqueo).</summary>
    public static class MoveFinder
    {
        /// <summary>
        /// Válido si ambas fichas se pueden mover, son adyacentes y (se forma una línea, o es un combo con especiales).
        /// </summary>
        public static bool IsValidSwap(Board b, Position a, Position c)
        {
            if (!b.InBounds(a) || !b.InBounds(c) || !a.IsAdjacentTo(c)) return false;
            if (!b.IsMovable(a) || !b.IsMovable(c)) return false;
            if (SpecialTileSystem.IsComboSwap(b, a, c)) return true;
            if (b[a] == b[c]) return false;
            b.Swap(a, c);
            bool ok = MatchDetector.IsPartOfMatch(b, a) || MatchDetector.IsPartOfMatch(b, c);
            b.Swap(a, c);
            return ok;
        }

        public static List<(Position From, Position To)> FindAllMoves(Board b)
        {
            var list = new List<(Position, Position)>();
            for (int x = 0; x < b.Width; x++)
                for (int y = 0; y < b.Height; y++)
                {
                    var p = new Position(x, y);
                    var right = new Position(x + 1, y);
                    var up = new Position(x, y + 1);
                    if (IsValidSwap(b, p, right)) list.Add((p, right));
                    if (IsValidSwap(b, p, up)) list.Add((p, up));
                }
            return list;
        }

        public static bool TryFindMove(Board b, out Position from, out Position to)
        {
            for (int x = 0; x < b.Width; x++)
                for (int y = 0; y < b.Height; y++)
                {
                    var p = new Position(x, y);
                    var right = new Position(x + 1, y);
                    var up = new Position(x, y + 1);
                    if (IsValidSwap(b, p, right)) { from = p; to = right; return true; }
                    if (IsValidSwap(b, p, up)) { from = p; to = up; return true; }
                }
            from = to = default;
            return false;
        }

        public static bool HasMove(Board b) => TryFindMove(b, out _, out _);
    }
}
