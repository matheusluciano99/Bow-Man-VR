using System.IO;
using System.Linq;
using System.Text;
using BowMan.Menu;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using WebXR;

public static class ItchWebBuild
{
    // Registers the browser XR provider alongside the existing desktop OpenXR provider.
    public static void Configure()
    {
        var group = BuildTargetGroup.WebGL;
        var settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        if (!settings.HasManagerSettingsForBuildTarget(group)) settings.CreateDefaultManagerSettingsForBuildTarget(group);
        var manager = settings.ManagerSettingsForBuildTarget(group);
        if (!manager.activeLoaders.Any(loader => loader is WebXRLoader) &&
            !XRPackageMetadataStore.AssignLoader(manager, typeof(WebXRLoader).FullName, group))
            throw new BuildFailedException("Unable to register the WebXR loader.");
        if (WebXRSettings.GetSettings() == null)
        {
            var browser = ScriptableObject.CreateInstance<WebXRSettings>();
            AssetDatabase.CreateAsset(browser, "Assets/XR/Settings/WebXR Settings.asset");
            EditorBuildSettings.AddConfigObject("WebXR.Settings", browser, true);
        }
        PlayerSettings.WebGL.template = "PROJECT:BowManVR";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        AssetDatabase.SaveAssets();
    }

    // Builds the four playable scenes and packages the relative HTML entry point for itch.io.
    [MenuItem("Bow Man VR/Build itch.io WebXR Game")]
    public static void Build()
    {
        Configure();
        const string output = "Builds/ItchIO/Web";
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            target = BuildTarget.WebGL,
            scenes = GameScenes.BuildOrder.Select(scene => $"Assets/_Game/Scenes/{scene}.unity").ToArray(),
            locationPathName = output
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"WebXR build finished with {report.summary.totalErrors} errors.");
        var notices = new StringBuilder(File.ReadAllText("Assets/StreamingAssets/BowMan-Credits.txt"));
        foreach (var id in new[] { "com.de-panther.webxr", "com.de-panther.webxr-interactions" })
        {
            var path = PackageInfo.FindForAssetPath("Packages/" + id + "/package.json").resolvedPath;
            notices.AppendLine().AppendLine(id).AppendLine(File.ReadAllText(Path.Combine(path, "Third Party Notices.md")));
            notices.AppendLine(File.ReadAllText(Path.Combine(path, "LICENSE.md")));
        }
        File.WriteAllText(Path.Combine(output, "StreamingAssets/BowMan-Credits.txt"), notices.ToString());
        const string zip = "Builds/ItchIO/BowManVR-WebXR.zip";
        var temp = zip + ".tmp";
        if (File.Exists(temp)) File.Delete(temp);
        System.IO.Compression.ZipFile.CreateFromDirectory(output, temp);
        if (File.Exists(zip)) File.Replace(temp, zip, null);
        else File.Move(temp, zip);
        Debug.Log($"WebXR package ready at {zip}");
    }
}
