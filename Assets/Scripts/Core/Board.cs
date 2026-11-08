using System;
using System.Text;

namespace PetaloPop.Core
{
    public enum SpecialKind { None = 0, LineH = 1, LineV = 2, Bomb = 3, ColorBurst = 4 }

    /// <summary>
    /// Cuadrícula del juego. Tipo de celda: 0 = vacío, 1..N = ficha de color, -1 = caja, -2 = piedra.
    /// Capas adicionales: especial (por ficha), vida (hielo de una ficha o capas de una caja) y gelatina (bajo la celda).
    /// </summary>
    public sealed class Board
    {
        public const int Empty = 0;
        public const int Crate = -1;
        public const int Stone = -2;

        public int Width { get; }
        public int Height { get; }
        private readonly int[,] cells;
        private readonly SpecialKind[,] specials;
        private readonly int[,] hp;
        private readonly int[,] jelly;

        public Board(int width, int height)
        {
            if (width < 3 || height < 3) throw new ArgumentException("El tablero mínimo es 3x3.");
            Width = width;
            Height = height;
            cells = new int[width, height];
            specials = new SpecialKind[width, height];
            hp = new int[width, height];
            jelly = new int[width, height];
        }

        public int this[int x, int y] { get => cells[x, y]; set => cells[x, y] = value; }
        public int this[Position p] { get => cells[p.X, p.Y]; set => cells[p.X, p.Y] = value; }

        public SpecialKind SpecialAt(Position p) => specials[p.X, p.Y];
        public void SetSpecial(Position p, SpecialKind k) => specials[p.X, p.Y] = k;
        public int Hp(Position p) => hp[p.X, p.Y];
        public void SetHp(Position p, int v) => hp[p.X, p.Y] = v;
        public int Jelly(Position p) => jelly[p.X, p.Y];
        public void SetJelly(Position p, int v) => jelly[p.X, p.Y] = v;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(Position p) => InBounds(p.X, p.Y);

        /// <summary>Ficha de color que se puede intercambiar (no vacía, no bloqueo, no congelada).</summary>
        public bool IsMovable(Position p) => cells[p.X, p.Y] > 0 && hp[p.X, p.Y] == 0;

        public void Swap(Position a, Position b)
        {
            (cells[a.X, a.Y], cells[b.X, b.Y]) = (cells[b.X, b.Y], cells[a.X, a.Y]);
            (specials[a.X, a.Y], specials[b.X, b.Y]) = (specials[b.X, b.Y], specials[a.X, a.Y]);
            (hp[a.X, a.Y], hp[b.X, b.Y]) = (hp[b.X, b.Y], hp[a.X, a.Y]);
        }

        public void ClearCell(Position p)
        {
            cells[p.X, p.Y] = Empty;
            specials[p.X, p.Y] = SpecialKind.None;
            hp[p.X, p.Y] = 0;
        }

        public Board Clone()
        {
            var c = new Board(Width, Height);
            Array.Copy(cells, c.cells, cells.Length);
            Array.Copy(specials, c.specials, specials.Length);
            Array.Copy(hp, c.hp, hp.Length);
            Array.Copy(jelly, c.jelly, jelly.Length);
            return c;
        }

        public bool IsFull()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (cells[x, y] == Empty) return false;
            return true;
        }

        public int CountSpecials()
        {
            int n = 0;
            foreach (var s in specials) if (s != SpecialKind.None) n++;
            return n;
        }

        public int CountWhere(Func<Position, bool> pred)
        {
            int n = 0;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (pred(new Position(x, y))) n++;
            return n;
        }

        /// <summary>Crea un tablero desde texto (primera fila = fila superior). '.' = vacío, '1'..'9' = tipo.</summary>
        public static Board FromRows(params string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var b = new Board(w, h);
            for (int r = 0; r < h; r++)
            {
                if (rows[r].Length != w) throw new ArgumentException("Filas de distinta longitud.");
                for (int x = 0; x < w; x++)
                {
                    char ch = rows[r][x];
                    b[x, h - 1 - r] = ch == '.' ? Empty : ch - '0';
                }
            }
            return b;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    int t = cells[x, y];
                    sb.Append(t == Empty ? '.' : t == Crate ? 'c' : t == Stone ? 'S' : t > 9 ? '+' : (char)('0' + t));
                }
                if (y > 0) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
