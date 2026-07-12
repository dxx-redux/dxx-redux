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
        [MenuItem("D1U/Build Windows Player")]
        public static void BuildWindows()
        {
            PlayerSettings.companyName = "dxx-redux";
            PlayerSettings.productName = "D1X-Unity";
            PlayerSettings.runInBackground = true;

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
