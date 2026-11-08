using UnityEditor;

/// <summary>
/// Import settings for downloaded Poly Haven assets (Assets/Resources/PH):
/// *_nor.jpg → normal maps, HDRIs → cubemaps (sky + lighting), sensible sizes for web / phones.
/// </summary>
public class ArcAssetImport : AssetPostprocessor
{
    // Bump when the rules below change: forces Unity to re-import cached textures.
    public override uint GetVersion() => 5;

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        var ti = (TextureImporter)assetImporter;

        // Courier textures extracted from Kurye.fbx (ArcBuild.ExtractCourierTextures).
        if (p.Contains("/Art/Kurye/"))
        {
            string n = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            if (n.Contains("normal")) ti.textureType = TextureImporterType.NormalMap;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 1024;   // a person on screen never needs more
            ti.textureCompression = TextureImporterCompression.Compressed;
            return;
        }

        if (!p.Contains("/Resources/PH/")) return;

        if (p.Contains("/PH/hdri/"))
        {
            ti.textureShape = TextureImporterShape.TextureCube;
            ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            ti.mipmapEnabled = true;
            ti.sRGBTexture = false;
            ti.maxTextureSize = 512;   // for cubemaps this is the size of EACH of the 6 faces
            ti.textureCompression = TextureImporterCompression.Compressed;
            // WebGL / phones: shared-exponent HDR format = half the size of RGBA16F, still HDR.
            foreach (var platform in new[] { "WebGL", "Android" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = 512;
                ps.format = TextureImporterFormat.RGB9E5;
                ti.SetPlatformTextureSettings(ps);
            }
            return;
        }

        bool normal = p.Contains("_nor");
        if (p.Contains("/PH/tex/"))
        {
            if (normal) ti.textureType = TextureImporterType.NormalMap;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            ti.maxTextureSize = normal ? 512 : 1024;   // normal detail is invisible beyond a few meters
            ti.textureCompression = TextureImporterCompression.Compressed;
            return;
        }

        if (p.Contains("/PH/models/"))
        {
            // Props are seen from a distance: 512 px is plenty and keeps the web build small.
            if (normal) ti.textureType = TextureImporterType.NormalMap;
            ti.maxTextureSize = 512;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }
    }

    // ---------- Mixamo courier (Resources/Kurye) ----------

    // Character: Assets/Resources/Kurye/Kurye.fbx (shipped). Animations: Assets/Anim/Kurye/*.fbx (outside Resources, so
    // only the animation clips the animator references go into the build, not the extra mesh copies inside them).
    static bool IsKurye(string path)
    {
        string p = path.Replace('\\', '/');
        return p.Contains("/Resources/Kurye/") || p.Contains("/Anim/Kurye/");
    }

    void OnPreprocessModel()
    {
        if (!IsKurye(assetPath)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        bool character = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant() == "kurye";
        mi.materialImportMode = character ? ModelImporterMaterialImportMode.ImportViaMaterialDescription : ModelImporterMaterialImportMode.None;
        if (character) mi.materialSearch = ModelImporterMaterialSearch.Everywhere;   // finds the extracted textures in Assets/Art/Kurye
        mi.importAnimation = !character;
    }

    void OnPreprocessAnimation()
    {
        if (!IsKurye(assetPath)) return;
        var mi = (ModelImporter)assetImporter;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = true;
            c.lockRootRotation = true;
            c.keepOriginalOrientation = true;
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = true;
            c.lockRootPositionXZ = true;
        }
        mi.clipAnimations = clips;
    }
}
