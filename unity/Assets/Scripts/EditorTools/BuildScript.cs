using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace D1U.EditorTools
{
    /// <summary>
    /// Standalone player build. Menu: D1U/Build Windows Player. Headless:
    ///   Unity.exe -batchmode -projectPath unity
    ///            -executeMethod D1U.EditorTools.BuildScript.BuildWindows
    ///            -logFile build.log
    /// Output: unity/Builds/D1X-Unity/d1x-unity.exe. The game looks for
    /// DESCENT.HOG next to the exe, in an adjacent "hogs" folder, or in the
    /// repo's d1/hogs.
    /// </summary>
    public static class BuildScript
    {
        /// <summary>
        /// The game creates all its materials at runtime via Shader.Find, so no
        /// build asset references the shaders and Unity strips them from player
        /// builds (editor Play mode works, built player silently falls back to
        /// Sprites/Default — no depth write, walls overdraw each other). Pin
        /// them in GraphicsSettings' Always Included Shaders before building.
        /// </summary>
        static void EnsureAlwaysIncludedShaders()
        {
            var wanted = new[]
            {
                "Universal Render Pipeline/Particles/Unlit",
                "Sprites/Default",
            };
            var settings = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in wanted)
            {
                var shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning($"D1U build: shader '{name}' not found in project");
                    continue;
                }
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        present = true;
                        break;
                    }
                if (!present)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                    Debug.Log($"D1U build: pinned always-included shader '{name}'");
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("D1U/Build Windows Player")]
        public static void BuildWindows()
        {
            EnsureAlwaysIncludedShaders();
            PlayerSettings.companyName = "dxx-redux";
            PlayerSettings.productName = "D1X-Unity";
            PlayerSettings.runInBackground = true;
            // visible build stamp (menu footer + Player.log) — ends "which build
            // am I actually running?" confusion during test rounds
            PlayerSettings.bundleVersion = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "D1X-Unity"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/D1ULevelViewer.unity" },
                locationPathName = Path.Combine(outDir, "d1x-unity.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"D1U build: {summary.result}, {summary.totalErrors} error(s), " +
                      $"{summary.totalSize / (1024 * 1024)} MB -> {options.locationPathName}");

            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
