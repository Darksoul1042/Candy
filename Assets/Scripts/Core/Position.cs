using System;

namespace PetaloPop.Core
{
    /// <summary>Celda del tablero. X = columna (0 = izquierda), Y = fila (0 = abajo).</summary>
    public readonly struct Position : IEquatable<Position>
    {
        public readonly int X;
        public readonly int Y;

        public Position(int x, int y) { X = x; Y = y; }

        public bool IsAdjacentTo(Position o) => Math.Abs(X - o.X) + Math.Abs(Y - o.Y) == 1;

        public bool Equals(Position o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Position p && Equals(p);
        public override int GetHashCode() => X * 397 ^ Y;
        public static bool operator ==(Position a, Position b) => a.Equals(b);
        public static bool operator !=(Position a, Position b) => !a.Equals(b);
        public override string ToString() => $"({X},{Y})";
    }
}
