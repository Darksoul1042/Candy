#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using PetaloPop.Core;
using PetaloPop.Game;

namespace PetaloPop.EditorTools
{
    /// <summary>Crea los 30 ScriptableObject de nivel a partir del catálogo. Si ya existen, los conserva (no pisa tus ediciones).</summary>
    public static class LevelAssetGenerator
    {
        [MenuItem("PetaloPop/Generate Level Assets")]
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/Resources/Levels");
            int created = 0;
            for (int l = 1; l <= LevelCatalog.Count; l++)
            {
                string path = "Assets/Resources/Levels/Level_" + l.ToString("00") + ".asset";
                if (File.Exists(path)) continue;
                var asset = ScriptableObject.CreateInstance<LevelDefinition>();
                asset.FromConfig(l, LevelCatalog.Get(l));
                AssetDatabase.CreateAsset(asset, path);
                created++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Niveles creados: " + created);
        }
    }
}
#endif
