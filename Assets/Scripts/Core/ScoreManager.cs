namespace PetaloPop.Core
{
    public sealed class ScoreManager
    {
        public const int PointsPerTile = 10;
        public const int MaxMultiplier = 5;
        private readonly int targetScore;

        public int Score { get; private set; }
        public ScoreManager(int targetScore) { this.targetScore = targetScore; }

        /// <summary>Puntos de una eliminación; el multiplicador crece con cada reacción en cadena hasta un tope de x5.</summary>
        public static int Calculate(int tilesCleared, int cascadeLevel) =>
            tilesCleared * PointsPerTile * System.Math.Min(cascadeLevel, MaxMultiplier);

        public void Add(int points) => Score += points;

        public ScoreManager Clone() => new ScoreManager(targetScore) { Score = Score };

        /// <summary>0 a 3 estrellas: objetivo, 1.5x objetivo, 2x objetivo.</summary>
        public int Stars =>
            Score >= targetScore * 2 ? 3 :
            Score >= targetScore * 3 / 2 ? 2 :
            Score >= targetScore ? 1 : 0;
    }
}
