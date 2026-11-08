using UnityEditor;

/// <summary>
/// Import settings for downloaded Poly Haven assets (Assets/Resources/PH):
/// *_nor.jpg → normal maps, HDRIs → cubemaps (sky + lighting), sensible sizes for web / phones.
/// </summary>
public class ArcAssetImport : AssetPostprocessor
{
    // Bump when the rules below change: forces Unity to re-import cached textures.
    public override uint GetVersion() => 2;

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Resources/PH/")) return;
        var ti = (TextureImporter)assetImporter;

        if (p.Contains("/PH/hdri/"))
        {
            ti.textureShape = TextureImporterShape.TextureCube;
            ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            ti.mipmapEnabled = true;
            ti.sRGBTexture = false;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Compressed;
            // WebGL / phones: shared-exponent HDR format = half the size of RGBA16F, still HDR.
            foreach (var platform in new[] { "WebGL", "Android" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = 2048;
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
}
