using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Batch-mode URP bootstrap (run after the URP package is installed):
//   Unity -batchmode -projectPath <proj> -executeMethod D1UUrpSetup.CreateAndAssign
public static class D1UUrpSetup
{
    const string RendererPath = "Assets/Settings/D1U_Renderer.asset";
    const string PipelinePath = "Assets/Settings/D1U_URP.asset";

    public static void CreateAndAssign()
    {
        try
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();

            Console.WriteLine("D1U: URP setup OK");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Console.WriteLine("D1U: URP setup FAILED: " + e);
            EditorApplication.Exit(1);
        }
    }
}
