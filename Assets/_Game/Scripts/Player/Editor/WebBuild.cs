using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Repeatable browser export. Only SC_Farm is included in the build.
public static class WebBuild
{
    [MenuItem("Rushed Crops/Build Web")]
    public static void Build()
    {
        string output = Path.GetFullPath("Builds/Web");
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-webOutput") output = Path.GetFullPath(args[i + 1]);
        }

        PlayerSettings.WebGL.template = "PROJECT:RushedCrops";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        // GitHub Pages cannot set Content-Encoding headers. The Unity loader
        // decompresses .unityweb files on hosts that do not provide them.
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.threadsSupport = false;
        PlayerSettings.WebGL.initialMemorySize = 128;
        PlayerSettings.WebGL.maximumMemorySize = 1024;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.showDiagnostics = false;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        AssetDatabase.SaveAssets();

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_Game/Scenes/SC_Farm.unity" },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });

        string result = $"Web build: {report.summary.result}, {report.summary.totalSize} bytes, {report.summary.totalErrors} errors.";
        Debug.Log(result);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception(result);
        }
        File.WriteAllText(Path.Combine(output, ".nojekyll"), "");
    }
}
