namespace PetaloPop.Core
{
    /// <summary>Origen de fichas nuevas. Permite pruebas deterministas.</summary>
    public interface ITileSource
    {
        int Next(int column);
    }

    public sealed class RandomTileSource : ITileSource
    {
        private readonly GameRng rng;
        private readonly int typeCount;
        public RandomTileSource(GameRng rng, int typeCount) { this.rng = rng; this.typeCount = typeCount; }
        public int Next(int column) => rng.Next(1, typeCount + 1);
    }
}
