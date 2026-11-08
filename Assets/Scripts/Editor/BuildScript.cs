#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PetaloPop.EditorTools
{
    /// <summary>Compilación de APK por línea de comandos:
    /// Unity -batchmode -quit -projectPath . -executeMethod PetaloPop.EditorTools.BuildScript.BuildAndroid</summary>
    public static class BuildScript
    {
        // Firma de publicación: se lee de variables de entorno (nunca se guarda en el proyecto). Sin ellas se usa la clave de depuración de Unity.
        private static void ApplyKeystoreFromEnvironment()
        {
            string path = System.Environment.GetEnvironmentVariable("PETALOPOP_KEYSTORE");
            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path)) throw new System.Exception("PETALOPOP_KEYSTORE apunta a un archivo que no existe: " + path);
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = path;
            PlayerSettings.Android.keystorePass = System.Environment.GetEnvironmentVariable("PETALOPOP_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = System.Environment.GetEnvironmentVariable("PETALOPOP_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = System.Environment.GetEnvironmentVariable("PETALOPOP_KEY_PASS");
        }

        [MenuItem("PetaloPop/Build Android APK")]
        public static void BuildAndroid()
        {
            PlayerSettings.companyName = "PetaloPop";
            PlayerSettings.productName = "Pétalo Pop";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.petalopop.game");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            ApplyKeystoreFromEnvironment();
            LevelAssetGenerator.Generate(); // asegura Resources/Levels/Level_XX.asset
            Directory.CreateDirectory("Build");
            Directory.CreateDirectory("Assets/Scenes");
            const string scenePath = "Assets/Scenes/Main.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath }, // escena vacía: el juego se crea por código (GameBootstrap)
                locationPathName = "Build/PetaloPop.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Resultado de compilación: " + report.summary.result + ", bytes: " + report.summary.totalSize);
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
#endif
