namespace PetaloPop.Core
{
    /// <summary>Generador pseudoaleatorio determinista y clonable (splitmix64). Misma semilla = misma partida en cualquier plataforma.</summary>
    public sealed class GameRng
    {
        private ulong state;

        public GameRng(int seed) { state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x1234567UL; NextU32(); }
        private GameRng() { }

        public GameRng Clone() => new GameRng { state = state };

        public uint NextU32()
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (uint)((z ^ (z >> 31)) >> 32);
        }

        /// <summary>Entero en [0, max).</summary>
        public int Next(int max) => (int)(NextU32() % (uint)max);

        /// <summary>Entero en [min, max).</summary>
        public int Next(int min, int max) => min + Next(max - min);
    }
}
