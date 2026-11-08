using System.Collections.Generic;
using UnityEngine;

namespace Arc
{
    public enum Shape2D { Square, Circle, Triangle, Diamond, Star, Ring }

    /// <summary>Build visible things from code: 3D primitives and generated 2D sprites. No asset files needed.</summary>
    public static class Shapes
    {
        // ---------- 3D ----------

        /// <summary>Create a primitive. collider=false removes the collider (cheaper, use distance checks instead).</summary>
        public static GameObject Make(PrimitiveType type, Vector3 position, Vector3 scale, Color color,
            bool lit = true, bool collider = false, Transform parent = null, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = lit ? Mats.Lit(color) : Mats.Unlit(color);
            if (!collider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
            return go;
        }

        public static GameObject Cube(Vector3 position, Vector3 scale, Color color, bool lit = true, bool collider = false, Transform parent = null)
            => Make(PrimitiveType.Cube, position, scale, color, lit, collider, parent);

        public static GameObject Sphere(Vector3 position, float diameter, Color color, bool lit = true, bool collider = false, Transform parent = null)
            => Make(PrimitiveType.Sphere, position, Vector3.one * diameter, color, lit, collider, parent);

        public static GameObject Cylinder(Vector3 position, Vector3 scale, Color color, bool lit = true, bool collider = false, Transform parent = null)
            => Make(PrimitiveType.Cylinder, position, scale, color, lit, collider, parent);

        public static GameObject Capsule(Vector3 position, Vector3 scale, Color color, bool lit = true, bool collider = false, Transform parent = null)
            => Make(PrimitiveType.Capsule, position, scale, color, lit, collider, parent);

        /// <summary>Empty parent object to group shapes into one character / prop.</summary>
        public static Transform Group(string name, Vector3 position, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        /// <summary>Recolor every renderer under an object.</summary>
        public static void Tint(GameObject go, Color color, bool lit = true)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = lit ? Mats.Lit(color) : Mats.Unlit(color);
        }

        // ---------- 2D ----------

        static readonly Dictionary<Shape2D, Sprite> sprites = new Dictionary<Shape2D, Sprite>();
        const int Res = 128;

        /// <summary>Create a 2D sprite object. size is in world units. sortingOrder: higher draws on top.</summary>
        public static SpriteRenderer Sprite(Shape2D shape, Vector2 position, Vector2 size, Color color,
            int sortingOrder = 0, Transform parent = null, string name = null)
        {
            var go = new GameObject(name ?? shape.ToString());
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite(shape);
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>A 1x1 world-unit sprite of the given shape (white, tint with SpriteRenderer.color).</summary>
        public static Sprite GetSprite(Shape2D shape)
        {
            if (sprites.TryGetValue(shape, out var s) && s != null) return s;
            var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color32[Res * Res];
            for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
            {
                float u = (x + 0.5f) / Res * 2f - 1f; // -1..1
                float v = (y + 0.5f) / Res * 2f - 1f;
                float a = Mathf.Clamp01(Coverage(shape, u, v) * Res * 0.5f + 0.5f); // soft anti-aliased edge
                px[y * Res + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, Res, Res), new Vector2(0.5f, 0.5f), Res);
            s.name = shape.ToString();
            sprites[shape] = s;
            return s;
        }

        // Signed "inside" distance: > 0 inside, < 0 outside (in -1..1 space).
        static float Coverage(Shape2D shape, float u, float v)
        {
            switch (shape)
            {
                case Shape2D.Circle:
                    return 1f - Mathf.Sqrt(u * u + v * v);
                case Shape2D.Ring:
                {
                    float d = Mathf.Sqrt(u * u + v * v);
                    return 0.18f - Mathf.Abs(d - 0.8f);
                }
                case Shape2D.Diamond:
                    return 1f - (Mathf.Abs(u) + Mathf.Abs(v));
                case Shape2D.Triangle:
                {
                    // Upward triangle: base at v=-1, tip at v=1.
                    float edge = (1f - v) * 0.5f; // half width allowed at this height
                    return Mathf.Min(edge - Mathf.Abs(u), v + 1f) * 0.9f;
                }
                case Shape2D.Star:
                {
                    float ang = Mathf.Atan2(v, u) + Mathf.PI / 2f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float k = Mathf.Cos(5f * ang) * 0.5f + 0.5f; // 5 points
                    float radius = Mathf.Lerp(0.45f, 1f, k);
                    return radius - r;
                }
                default: // Square
                    return 1f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
            }
        }
    }
}
