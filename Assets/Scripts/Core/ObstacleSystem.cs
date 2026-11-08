using System.Collections.Generic;
using System.Linq;

namespace PetaloPop.Core
{
    /// <summary>
    /// Aplica obstáculos sobre un conjunto de celdas a eliminar:
    /// - Piedra: no se empareja ni recibe golpes de vecinas; solo la destruyen efectos de fichas especiales o potenciadores.
    /// - Ficha congelada (vida > 0): cada golpe quita una capa y la ficha sobrevive; al llegar a 0 se libera.
    /// - Caja: recibe un golpe si la celda o una vecina ortogonal se elimina; al llegar a 0 desaparece.
    /// - Gelatina: pierde una capa cada vez que se elimina la ficha de su celda.
    /// </summary>
    public static class ObstacleSystem
    {
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };

        public static void Filter(Board b, ClearOutcome o)
        {
            var original = o.Cleared.ToList();
            var crateHit = new HashSet<Position>();
            foreach (var p in original)
                for (int i = 0; i < 4; i++)
                {
                    int nx = p.X + Dx[i], ny = p.Y + Dy[i];
                    if (b.InBounds(nx, ny) && b[nx, ny] == Board.Crate) crateHit.Add(new Position(nx, ny));
                }

            var remove = new HashSet<Position>();
            var destroyedCrates = new List<Position>();

            foreach (var p in original)
            {
                int t = b[p];
                bool force = o.ForceClear.Contains(p);
                if (t == Board.Stone)
                {
                    o.Stats.StonesRemoved++; // solo llega aquí por un efecto de especial o un potenciador (las líneas normales no incluyen piedras)
                }
                else if (t == Board.Crate)
                {
                    if (force) { o.Stats.CratesDestroyed++; crateHit.Remove(p); }
                    else { remove.Add(p); crateHit.Add(p); }
                }
                else if (t > 0 && b.Hp(p) > 0 && !force)
                {
                    int hp = b.Hp(p) - 1;
                    b.SetHp(p, hp);
                    o.Hits.Add(new Hit { Pos = p, Kind = ObstacleKind.Frozen, Remaining = hp });
                    if (hp == 0) o.Stats.FrozenBroken++;
                    remove.Add(p);
                }
            }

            foreach (var p in crateHit)
            {
                if (b[p] != Board.Crate) continue;
                int hp = b.Hp(p) - 1;
                b.SetHp(p, hp);
                o.Hits.Add(new Hit { Pos = p, Kind = ObstacleKind.Crate, Remaining = hp });
                if (hp <= 0) { destroyedCrates.Add(p); o.Stats.CratesDestroyed++; }
            }

            o.Cleared.ExceptWith(remove);
            foreach (var p in destroyedCrates) o.Cleared.Add(p);

            foreach (var p in o.Cleared.Concat(o.Created.Select(c => c.Pos)).ToList())
            {
                int j = b.Jelly(p);
                if (j <= 0) continue;
                b.SetJelly(p, j - 1);
                o.Hits.Add(new Hit { Pos = p, Kind = ObstacleKind.Jelly, Remaining = j - 1 });
                if (j - 1 == 0) o.Stats.JellyCleared++;
            }

            foreach (var p in o.Cleared) if (b[p] > 0) o.Stats.AddType(b[p]);
        }
    }
}
