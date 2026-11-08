using System.Collections.Generic;

namespace PetaloPop.Core
{
    /// <summary>
    /// Parámetros de un nivel (se rellena desde un ScriptableObject en Unity).
    /// TileRows (fila superior primero): '.' normal, 'f'/'F' ficha congelada 1/2 capas, 'c'/'C'/'K' caja 1/2/3 capas, 'S' piedra.
    /// JellyRows: '.' sin gelatina, '1'/'2' capas de gelatina.
    /// </summary>
    public sealed class LevelConfig
    {
        public int Width = 9;
        public int Height = 9;
        public int TypeCount = 5;
        public int Moves = 25;
        public int TargetScore = 1500;
        public int Seed = 12345;
        public string[] TileRows;
        public string[] JellyRows;
        public List<ObjectiveSpec> Objectives = new List<ObjectiveSpec>();
    }
}
