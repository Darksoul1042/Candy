using System.Collections.Generic;

namespace PetaloPop.Core
{
    public enum StepKind { Swap, SwapBack, Clear, Fall, Spawn, Shuffle }
    public enum GameState { Playing, Won, Lost }
    public enum ObstacleKind { Jelly, Frozen, Crate, Stone }

    public sealed class SpecialSpawn { public Position Pos; public int Type; public SpecialKind Kind; }
    public struct ActivatedSpecial { public Position Pos; public SpecialKind Kind; }
    public sealed class Hit { public Position Pos; public ObstacleKind Kind; public int Remaining; }

    /// <summary>Contadores de lo ocurrido en una ronda de eliminación (alimentan los objetivos).</summary>
    public sealed class RoundStats
    {
        public Dictionary<int, int> ClearedByType = new Dictionary<int, int>();
        public int JellyCleared, FrozenBroken, CratesDestroyed, StonesRemoved;

        public void AddType(int type)
        {
            ClearedByType.TryGetValue(type, out int n);
            ClearedByType[type] = n + 1;
        }
    }

    /// <summary>Resultado de una ronda: qué se elimina, qué especiales nacen o se activan y qué obstáculos reciben golpes.</summary>
    public sealed class ClearOutcome
    {
        public HashSet<Position> Cleared = new HashSet<Position>();
        public HashSet<Position> ForceClear = new HashSet<Position>();
        public List<SpecialSpawn> Created = new List<SpecialSpawn>();
        public List<ActivatedSpecial> Activated = new List<ActivatedSpecial>();
        public List<Hit> Hits = new List<Hit>();
        public RoundStats Stats = new RoundStats();
    }

    /// <summary>Un paso de la resolución de un movimiento. La vista los reproduce en orden para animar.</summary>
    public sealed class Step
    {
        public StepKind Kind;
        public Position A, B;                                      // Swap / SwapBack
        public List<Position> Cleared = new List<Position>();      // Clear
        public List<SpecialSpawn> Created = new List<SpecialSpawn>();
        public List<ActivatedSpecial> Activated = new List<ActivatedSpecial>();
        public List<Hit> Hits = new List<Hit>();
        public List<TileMove> Moves = new List<TileMove>();        // Fall / Spawn / Shuffle
        public int Cascade;                                        // Clear: nivel de cadena (1 = directo)
        public int Points;                                         // Clear: puntos ganados
    }

    public sealed class MoveResult
    {
        /// <summary>true si la acción se aplicó (consumió movimiento o potenciador).</summary>
        public bool Accepted;
        public List<Step> Steps = new List<Step>();
        public int PointsGained;
        public int MaxCascade;
        public GameState State;
    }
}
