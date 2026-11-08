using System.Collections.Generic;
using UnityEngine;

namespace Arc
{
    /// <summary>
    /// Cached materials. Base materials live in Resources/ArcMaterials (created by ArcBuild),
    /// which guarantees the shaders (URP) are included in the build.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> bases = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        const string LitShader = "Universal Render Pipeline/Lit";
        const string UnlitShader = "Universal Render Pipeline/Unlit";
        const string SpriteShader = "Sprites/Default";

        /// <summary>Lit flat color — use for 3D objects.</summary>
        public static Material Lit(Color c) => Get("Lit", LitShader, c);

        /// <summary>Unlit flat color — UI-like shapes, glowing things, backgrounds.</summary>
        public static Material Unlit(Color c) => Get("Unlit", UnlitShader, c);

        /// <summary>Sprite/vertex-color material — particles and 2D.</summary>
        public static Material Sprite(Color c) => Get("Sprite", SpriteShader, c);

        /// <summary>
        /// Glossy painted surface (car paint, plastic, metal). smoothness 0..1, metallic 0..1.
        /// </summary>
        public static Material Paint(Color c, float smoothness = 0.8f, float metallic = 0.3f)
        {
            string key = "Paint" + ColorUtility.ToHtmlStringRGBA(c) + smoothness.ToString("0.00") + metallic.ToString("0.00");
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Base("Lit", LitShader)) { color = c };
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            cache[key] = m;
            return m;
        }

        /// <summary>Dark reflective glass (windows, visors).</summary>
        public static Material Glass(Color tint) => Paint(tint, 0.95f, 0.6f);

        /// <summary>
        /// Real photo-scanned PBR texture from Poly Haven (must be listed in Game/kaynaklar.json).
        /// tiling = how many times the texture repeats across the surface (x, y).
        /// Falls back to a flat lit color if the texture is missing.
        /// </summary>
        public static Material Pbr(string textureId, Vector2 tiling, float smoothness = 0.2f, Color? tint = null)
        {
            Color t = tint ?? Color.white;
            string key = "Pbr" + textureId + tiling.x.ToString("0.0") + "x" + tiling.y.ToString("0.0") + smoothness.ToString("0.00") + ColorUtility.ToHtmlStringRGBA(t);
            if (cache.TryGetValue(key, out var m) && m != null) return m;

            var diff = Resources.Load<Texture2D>("PH/tex/" + textureId + "_diff");
            var nor = Resources.Load<Texture2D>("PH/tex/" + textureId + "_nor");
            m = new Material(Base("LitNormal", LitShader)) { color = t };
            if (diff != null)
            {
                m.SetTexture("_BaseMap", diff);
                m.SetTextureScale("_BaseMap", tiling);
            }
            else
            {
                Debug.LogWarning("[Arc] Doku bulunamadı: " + textureId + " (kaynaklar.json'a eklendi mi?)");
            }
            if (nor != null)
            {
                m.SetTexture("_BumpMap", nor);
                m.EnableKeyword("_NORMALMAP");
            }
            else
            {
                m.DisableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            cache[key] = m;
            return m;
        }

        /// <summary>PBR texture sized for a surface: one texture repeat every metersPerTile meters.</summary>
        public static Material PbrSized(string textureId, Vector2 surfaceMeters, float metersPerTile = 2f, float smoothness = 0.2f, Color? tint = null)
        {
            var tiling = new Vector2(
                Mathf.Max(0.25f, Mathf.Round(surfaceMeters.x / metersPerTile * 4f) / 4f),
                Mathf.Max(0.25f, Mathf.Round(surfaceMeters.y / metersPerTile * 4f) / 4f));
            return Pbr(textureId, tiling, smoothness, tint);
        }

        static Material Get(string kind, string shaderName, Color c)
        {
            string key = kind + ColorUtility.ToHtmlStringRGBA(c);
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Base(kind, shaderName)) { color = c };
            cache[key] = m;
            return m;
        }

        static Material Base(string kind, string shaderName)
        {
            if (bases.TryGetValue(kind, out var b) && b != null) return b;
            b = Resources.Load<Material>("ArcMaterials/" + kind);
            if (b == null)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) shader = Shader.Find(SpriteShader);
                b = new Material(shader);
            }
            bases[kind] = b;
            return b;
        }
    }
}
