using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

// Batch-mode project bootstrap: run with
//   Unity -batchmode -projectPath <proj> -executeMethod D1UProjectSetup.AddCorePackages
// (no -quit: the method exits the editor itself once the async request finishes).
public static class D1UProjectSetup
{
    static AddAndRemoveRequest request;

    public static void AddCorePackages()
    {
        request = Client.AddAndRemove(new[]
        {
            "com.unity.render-pipelines.universal",
            "com.unity.inputsystem",
        }, null);
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (!request.IsCompleted)
            return;
        EditorApplication.update -= Poll;
        if (request.Status == StatusCode.Success)
        {
            Console.WriteLine("D1U: package add OK");
            EditorApplication.Exit(0);
        }
        else
        {
            Console.WriteLine($"D1U: package add FAILED: {request.Error?.message}");
            EditorApplication.Exit(1);
        }
    }
}
