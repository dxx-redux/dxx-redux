using System.IO;
using D1U.Convert;
using UnityEditor;
using UnityEngine;

namespace D1U.EditorTools
{
    public static class DxuMenu
    {
        [MenuItem("D1U/Build Base DXU")]
        public static void BuildBaseDxu()
        {
            var hogsDir = EditorPrefs.GetString("D1U.HogsDir", "");
            if (!File.Exists(Path.Combine(hogsDir, "DESCENT.HOG")))
            {
                EditorUtility.DisplayDialog("D1U",
                    "Set a valid hogs directory in D1U > Asset Browser first.", "OK");
                return;
            }
            var path = DxuCache.EnsureBase(hogsDir, null, Debug.Log);
            var sizeMb = new FileInfo(path).Length / (1024 * 1024.0);
            EditorUtility.DisplayDialog("D1U",
                $"base.dxu ready ({sizeMb:F1} MB)\n{path}", "OK");
        }
    }
}
