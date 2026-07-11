using System;
using System.Linq;
using D1U.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace D1U.EditorTools
{
    public static class SceneMenu
    {
        [MenuItem("D1U/Create Level Viewer Scene")]
        public static void CreateLevelViewerScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildSceneObjects(out _, out _);
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/D1ULevelViewer.unity");
            Debug.Log("D1U: saved Assets/Scenes/D1ULevelViewer.unity — press Play, hold RMB to look, WASD/QE to fly.");
        }

        static void BuildSceneObjects(out Camera camera, out LevelViewer viewer)
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UnityEngine.Color.black;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<FlyCamera>();

            var viewerGo = new GameObject("D1U Level Viewer");
            viewer = viewerGo.AddComponent<LevelViewer>();
            viewer.hogsDir = EditorPrefs.GetString("D1U.HogsDir", "");
            Selection.activeGameObject = viewerGo;
        }

        // Headless M2 acceptance:
        //   Unity -batchmode -projectPath <proj> -executeMethod D1U.EditorTools.SceneMenu.ValidateLevelBuild
        public static void ValidateLevelBuild()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildSceneObjects(out _, out var viewer);

                int failures = 0;
                foreach (var (key, levelNumber) in new[] { ("firststrike", 1), ("chaos", 1) })
                {
                    viewer.missionKey = key;
                    viewer.levelNumber = levelNumber;
                    viewer.Build();
                    var filters = viewer.GetComponentsInChildren<MeshFilter>();
                    int vertices = filters.Sum(f => f.sharedMesh != null ? f.sharedMesh.vertexCount : 0);
                    Debug.Log($"D1U VALIDATE: {key} L{levelNumber}: {filters.Length} meshes, {vertices} vertices");
                    if (filters.Length == 0 || vertices == 0)
                        failures++;
                }
                Debug.Log(failures == 0 ? "D1U VALIDATE OK" : "D1U VALIDATE FAILED");
                EditorApplication.Exit(failures == 0 ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("D1U VALIDATE EXCEPTION: " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
