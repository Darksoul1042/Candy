using System.Collections.Generic;

namespace PetaloPop.Core
{
    public readonly struct TileMove
    {
        public readonly Position From;
        public readonly Position To;
        public readonly int Type;
        public readonly SpecialKind Special;
        public readonly int Hp;
        public TileMove(Position from, Position to, int type, SpecialKind special = SpecialKind.None, int hp = 0)
        { From = from; To = to; Type = type; Special = special; Hp = hp; }
    }

    /// <summary>Gravedad (todo cae hacia Y=0, incluidas cajas y piedras) y relleno desde arriba.</summary>
    public static class GravitySystem
    {
        public static List<TileMove> ApplyGravity(Board b)
        {
            var moves = new List<TileMove>();
            for (int x = 0; x < b.Width; x++)
            {
                int write = 0;
                for (int y = 0; y < b.Height; y++)
                {
                    var from = new Position(x, y);
                    int t = b[from];
                    if (t == Board.Empty) continue;
                    if (y != write)
                    {
                        var to = new Position(x, write);
                        var sp = b.SpecialAt(from);
                        int hp = b.Hp(from);
                        b.ClearCell(from);
                        b[to] = t;
                        b.SetSpecial(to, sp);
                        b.SetHp(to, hp);
                        moves.Add(new TileMove(from, to, t, sp, hp));
                    }
                    write++;
                }
            }
            return moves;
        }

        /// <summary>Rellena huecos (tras la gravedad están arriba). From.Y >= Height: nacen sobre el tablero.</summary>
        public static List<TileMove> Refill(Board b, ITileSource source)
        {
            var spawns = new List<TileMove>();
            for (int x = 0; x < b.Width; x++)
            {
                int k = 0;
                for (int y = 0; y < b.Height; y++)
                {
                    if (b[x, y] != Board.Empty) continue;
                    int t = source.Next(x);
                    b[x, y] = t;
                    spawns.Add(new TileMove(new Position(x, b.Height + k), new Position(x, y), t));
                    k++;
                }
            }
            return spawns;
        }
    }
}
