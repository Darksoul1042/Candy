using UnityEngine;

namespace PetaloPop.Game
{
    /// <summary>Crea el juego automáticamente al iniciar cualquier escena (la primera versión no necesita escena preparada).</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Object.FindObjectOfType<GameManager>() != null) return;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            new GameObject("PetaloPop").AddComponent<GameManager>();
        }
    }
}
