using UnityEditor;

/// <summary>
/// Import settings for downloaded Poly Haven assets (Assets/Resources/PH):
/// *_nor.jpg → normal maps, HDRIs → cubemaps (sky + lighting), sensible sizes for web / phones.
/// </summary>
public class ArcAssetImport : AssetPostprocessor
{
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
            return;
        }

        if (p.Contains("/PH/tex/"))
        {
            if (p.EndsWith("_nor.jpg")) ti.textureType = TextureImporterType.NormalMap;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
