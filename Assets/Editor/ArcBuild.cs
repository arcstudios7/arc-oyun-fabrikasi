using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        EnsureRenderPipeline();
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
        if (string.IsNullOrEmpty(path))
            path = target == BuildTarget.Android ? "build/Android/Oyun.apk"
                : target == BuildTarget.StandaloneLinux64 ? "build/StandaloneLinux64/Oyun.x86_64"
                : "build/" + target + "/Oyun";

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
        const string dir = "Assets/Resources/ArcMaterials/";
        MakeMaterial(dir + "Lit.mat", "Universal Render Pipeline/Lit");
        var litNormal = MakeMaterial(dir + "LitNormal.mat", "Universal Render Pipeline/Lit");
        if (litNormal != null)
        {
            // Keep the normal-map shader variant in the build (runtime PBR materials enable it).
            litNormal.EnableKeyword("_NORMALMAP");
            litNormal.SetTexture("_BumpMap", Texture2D.normalTexture);
            EditorUtility.SetDirty(litNormal);
        }
        MakeMaterial(dir + "Unlit.mat", "Universal Render Pipeline/Unlit");
        MakeMaterial(dir + "Sprite.mat", "Sprites/Default");
        MakeMaterial(dir + "Sky.mat", "Skybox/Cubemap");
    }

    static Material MakeMaterial(string path, string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError("[ARC] Shader bulunamadı: " + shaderName);
            return null;
        }
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            if (existing.shader != shader) { existing.shader = shader; EditorUtility.SetDirty(existing); }
            return existing;
        }
        var mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>Creates and activates the Universal Render Pipeline (URP) from code: modern lighting, soft shadows, post-processing.</summary>
    static void EnsureRenderPipeline()
    {
        EnsureFolder("Assets", "Settings");
        const string rdPath = "Assets/Settings/ArcRenderer.asset";
        const string rpPath = "Assets/Settings/ArcURP.asset";

        var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath);
        if (rd == null)
        {
            rd = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rd, rdPath);
        }
        if (rd.postProcessData == null)
        {
            rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (rd.postProcessData == null) Debug.LogWarning("[ARC] PostProcessData bulunamadı; efektler çalışmayabilir.");
            EditorUtility.SetDirty(rd);
        }

        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
        if (rp == null)
        {
            rp = UniversalRenderPipelineAsset.Create(rd);
            AssetDatabase.CreateAsset(rp, rpPath);
        }
        rp.supportsHDR = true;
        rp.msaaSampleCount = 1;          // FXAA on the camera instead (cheaper on phones / WebGL)
        rp.shadowDistance = 90f;
        rp.shadowCascadeCount = 2;
        rp.renderScale = 1f;
        var so = new SerializedObject(rp);
        SetBool(so, "m_SoftShadowsSupported", true);
        SetBool(so, "m_MainLightShadowsSupported", true);
        SetInt(so, "m_MainLightShadowmapResolution", 2048);
        SetBool(so, "m_SupportsTerrainHoles", false);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rp);

        GraphicsSettings.defaultRenderPipeline = rp;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = rp;
        }
        QualitySettings.SetQualityLevel(current, false);
        AssetDatabase.SaveAssets();
    }

    static void SetBool(SerializedObject so, string name, bool value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }

    static void SetInt(SerializedObject so, string name, int value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.intValue = value;
    }

    static void EnsureScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            EnsureFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        // Fog is enabled in the saved scene so its shader variants are kept in the build (games may turn it off at runtime).
        var opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.002f;
        EditorSceneManager.MarkSceneDirty(opened);
        EditorSceneManager.SaveScene(opened);
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
