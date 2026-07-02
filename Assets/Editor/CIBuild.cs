using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command line build entry point for CI.
/// Invoked as: -batchmode -quit -buildTarget &lt;target&gt; [-standaloneBuildSubtarget Server]
///             -executeMethod CIBuild.Build -buildPath &lt;output folder&gt; [-buildName &lt;name&gt;]
/// The build target and server subtarget are consumed by the editor itself;
/// this method just assembles the player build for whatever is active.
/// </summary>
public static class CIBuild
{
    public static void Build()
    {
        string buildPath = GetArgument("-buildPath") ?? "build";
        string buildName = GetArgument("-buildName") ?? Application.productName;

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("CIBuild: no enabled scenes in Build Settings.");
            EditorApplication.Exit(1);
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(buildPath, buildName + ExecutableExtension(target)),
            target = target,
            options = BuildOptions.None,
        };

        Debug.Log($"CIBuild: building {target} ({EditorUserBuildSettings.standaloneBuildSubtarget}) to '{options.locationPathName}' with {scenes.Length} scene(s).");

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"CIBuild: succeeded, size {report.summary.totalSize / (1024 * 1024)} MB.");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"CIBuild: {report.summary.result}, {report.summary.totalErrors} error(s).");
            EditorApplication.Exit(1);
        }
    }

    private static string ExecutableExtension(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return ".exe";
            case BuildTarget.StandaloneOSX:
                return ".app";
            default:
                return string.Empty;
        }
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }
}
