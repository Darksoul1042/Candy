using System.Collections.Generic;

namespace PetaloPop.Core
{
    public enum BoosterKind { Hammer, FreeSwap, ObstacleRemover, ExtraMoves, SpecialMaker }

    /// <summary>Inventario de potenciadores. Sin compras integradas: las cantidades vienen de la partida guardada.</summary>
    public sealed class BoosterManager
    {
        private readonly Dictionary<BoosterKind, int> counts = new Dictionary<BoosterKind, int>();

        public int Count(BoosterKind k) { counts.TryGetValue(k, out int n); return n; }
        public void Add(BoosterKind k, int n = 1) { counts[k] = Count(k) + n; }
        public bool TryConsume(BoosterKind k)
        {
            if (Count(k) <= 0) return false;
            counts[k] = Count(k) - 1;
            return true;
        }

        public BoosterManager Clone()
        {
            var c = new BoosterManager();
            foreach (var kv in counts) c.counts[kv.Key] = kv.Value;
            return c;
        }
    }
}
