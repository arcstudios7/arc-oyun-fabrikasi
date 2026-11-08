using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Turns this code-only project into a buildable Unity project:
/// creates the (empty) main scene, the base materials, applies player settings, then builds.
/// Used by GameCI in GitHub Actions (buildMethod: ArcBuild.BuildFromCommandLine) and by the ARC menu locally.
/// </summary>
public static class ArcBuild
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("ARC/Hazırla (sahne + materyaller + ayarlar)")]
    public static void Prepare()
    {
        EnsureMaterials();
        EnsureScene();
        ApplyPlayerSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[ARC] Proje hazır.");
    }

    [MenuItem("ARC/WebGL Build al")]
    public static void BuildWebGLMenu() => Build(BuildTarget.WebGL, "build/WebGL/Oyun");

    [MenuItem("ARC/Android APK al")]
    public static void BuildAndroidMenu() => Build(BuildTarget.Android, "build/Android/Oyun.apk");

    /// <summary>Entry point for CI. Reads GameCI's -customBuildPath / -customBuildTarget arguments.</summary>
    public static void BuildFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        string path = Arg(args, "-customBuildPath");
        string targetName = Arg(args, "-customBuildTarget") ?? Arg(args, "-buildTarget");

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        if (!string.IsNullOrEmpty(targetName) && Enum.TryParse(targetName, true, out BuildTarget parsed)) target = parsed;
        if (string.IsNullOrEmpty(path)) path = target == BuildTarget.Android ? "build/Android/Oyun.apk" : "build/" + target + "/Oyun";

        Debug.Log("[ARC] Build başlıyor: " + target + " → " + path);
        bool ok = false;
        try
        {
            ok = Build(target, path);
        }
        catch (Exception e)
        {
            Debug.LogError("[ARC] Build hatası: " + e);
        }
        EditorApplication.Exit(ok ? 0 : 101);
    }

    static string Arg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }

    static bool Build(BuildTarget target, string path)
    {
        Prepare();
        if (target == BuildTarget.Android) EditorUserBuildSettings.buildAppBundle = false;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = path,
            target = target,
            targetGroup = BuildPipeline.GetBuildTargetGroup(target),
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log("[ARC] Build sonucu: " + summary.result + " | süre: " + summary.totalTime + " | boyut: " + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB");
        if (summary.result != BuildResult.Succeeded)
        {
            foreach (var step in report.steps)
            foreach (var msg in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                Debug.LogError("[ARC] " + msg.content);
        }
        return summary.result == BuildResult.Succeeded;
    }

    // ---------- project preparation ----------

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }

    static void EnsureMaterials()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "ArcMaterials");
        MakeMaterial("Assets/Resources/ArcMaterials/Lit.mat", "Standard");
        MakeMaterial("Assets/Resources/ArcMaterials/Unlit.mat", "Unlit/Color");
        MakeMaterial("Assets/Resources/ArcMaterials/Sprite.mat", "Sprites/Default");
    }

    static void MakeMaterial(string path, string shaderName)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError("[ARC] Shader bulunamadı: " + shaderName);
            return;
        }
        AssetDatabase.CreateAsset(new Material(shader), path);
    }

    static void EnsureScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            EnsureFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = "ARC Studios";
        PlayerSettings.productName = Game.GameInfo.Title;
        PlayerSettings.bundleVersion = Game.GameInfo.Version;
        PlayerSettings.runInBackground = true;

        bool portrait = Game.GameInfo.Portrait;
        PlayerSettings.defaultInterfaceOrientation = portrait ? UIOrientation.Portrait : UIOrientation.LandscapeLeft;

        // Android: arm64 + IL2CPP is required by modern phones and Google Play.
        string id = "com.arcstudios." + Regex.Replace(Game.GameInfo.Slug.ToLowerInvariant(), "[^a-z0-9]", "");
        if (id == "com.arcstudios.") id += "oyun";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, id);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // WebGL: gzip keeps every file far below Cloudflare Pages' 25 MiB/file limit; decompressionFallback lets the
        // Unity loader unpack it in JavaScript, so it works on any static host without special server headers.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.defaultWebScreenWidth = portrait ? 540 : 960;
        PlayerSettings.defaultWebScreenHeight = portrait ? 960 : 540;

        // The Arc helpers use the classic Input Manager (0 = Input Manager, 1 = Input System, 2 = Both).
        var playerSettings = Resources.FindObjectsOfTypeAll<PlayerSettings>().FirstOrDefault();
        if (playerSettings != null)
        {
            var so = new SerializedObject(playerSettings);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue != 0)
            {
                prop.intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
