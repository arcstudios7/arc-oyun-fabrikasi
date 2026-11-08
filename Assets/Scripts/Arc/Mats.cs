using System.Collections.Generic;
using UnityEngine;

namespace Arc
{
    /// <summary>
    /// Cached materials by color. Base materials live in Resources/ArcMaterials (created by ArcBuild),
    /// which guarantees the shaders are included in the build.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> bases = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        /// <summary>Lit (Standard shader) — use for 3D objects.</summary>
        public static Material Lit(Color c) => Get("Lit", "Standard", c);

        /// <summary>Unlit flat color — use for UI-like 3D shapes, backgrounds, glowing things.</summary>
        public static Material Unlit(Color c) => Get("Unlit", "Unlit/Color", c);

        /// <summary>Sprite/vertex-color material — use for particles and transparent shapes.</summary>
        public static Material Sprite(Color c) => Get("Sprite", "Sprites/Default", c);

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
                if (shader == null) shader = Shader.Find("Sprites/Default");
                b = new Material(shader);
            }
            bases[kind] = b;
            return b;
        }
    }
}
