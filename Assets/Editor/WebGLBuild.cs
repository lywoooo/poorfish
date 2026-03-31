using System.IO;
using UnityEditor;
using UnityEngine;

public static class WebGLBuild
{
    public static void BuildDocs()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string buildPath = Path.Combine(projectRoot, "docs");

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new System.Exception("WebGL build failed.");
        }
    }
}
