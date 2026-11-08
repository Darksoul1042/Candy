using System;
using System.Collections.Generic;
using UnityEngine;
using PetaloPop.Core;

namespace PetaloPop.Game
{
    /// <summary>Datos de un nivel editables en el Inspector. Resources/Levels/Level_XX.asset (los crea el menú PetaloPop).</summary>
    [CreateAssetMenu(menuName = "PetaloPop/Level", fileName = "Level_00")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Serializable]
        public struct Objective { public ObjectiveKind kind; public int colorType; public int count; }

        public int number = 1;
        public int width = 9, height = 9, typeCount = 5, moves = 25, targetScore = 1500, seed = 1;
        [Tooltip("Fila superior primero. '.' normal, f/F congelada 1/2, c/C/K caja 1/2/3, S piedra")]
        public string[] tileRows;
        [Tooltip("Fila superior primero. '.' sin gelatina, '1'/'2' capas")]
        public string[] jellyRows;
        public List<Objective> objectives = new List<Objective>();

        public LevelConfig ToConfig()
        {
            var c = new LevelConfig
            {
                Width = width, Height = height, TypeCount = typeCount, Moves = moves, TargetScore = targetScore, Seed = seed,
                TileRows = tileRows != null && tileRows.Length > 0 ? tileRows : null,
                JellyRows = jellyRows != null && jellyRows.Length > 0 ? jellyRows : null,
            };
            foreach (var o in objectives) c.Objectives.Add(new ObjectiveSpec { Kind = o.kind, Type = o.colorType, Count = o.count });
            return c;
        }

        public void FromConfig(int levelNumber, LevelConfig c)
        {
            number = levelNumber; width = c.Width; height = c.Height; typeCount = c.TypeCount; moves = c.Moves; targetScore = c.TargetScore; seed = c.Seed;
            tileRows = c.TileRows; jellyRows = c.JellyRows;
            objectives = new List<Objective>();
            foreach (var o in c.Objectives) objectives.Add(new Objective { kind = o.Kind, colorType = o.Type, count = o.Count });
        }
    }

    public static class LevelProvider
    {
        /// <summary>Usa el asset Resources/Levels/Level_XX si existe; si no, el catálogo de código (misma definición).</summary>
        public static LevelConfig Get(int level)
        {
            var asset = Resources.Load<LevelDefinition>("Levels/Level_" + level.ToString("00"));
            return asset != null ? asset.ToConfig() : LevelCatalog.Get(level);
        }
    }
}
