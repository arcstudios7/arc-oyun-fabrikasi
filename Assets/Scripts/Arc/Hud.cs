using System.Collections.Generic;
using UnityEngine;

namespace Arc
{
    /// <summary>
    /// Screen UI with zero setup (Unity IMGUI). Call these ONLY from OnGUI / ArcGame.DrawHud().
    /// All sizes are in "reference units": 1 unit ≈ 1 pixel on a 720 px tall/wide screen, scaled automatically.
    /// </summary>
    public static class Hud
    {
        public static float U => Mathf.Max(0.5f, Mathf.Min(Screen.width, Screen.height) / 720f);

        static GUIStyle label, button, corner;
        static float builtFor = -1f;
        static Texture2D btnTex, btnHoverTex, cornerTex;

        public static Color ButtonColor = Palette.Primary;
        public static Color ButtonTextColor = Palette.Hex("#10132A");

        static void Ensure()
        {
            if (label != null && Mathf.Approximately(builtFor, U) && btnTex != null) return;
            builtFor = U;
            label = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(30 * U),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                richText = true
            };
            label.normal.textColor = Color.white;

            btnTex = Solid(ButtonColor);
            btnHoverTex = Solid(Palette.Lighter(ButtonColor, 0.2f));
            button = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(34 * U),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            button.normal.background = btnTex;
            button.hover.background = btnHoverTex;
            button.active.background = btnHoverTex;
            button.normal.textColor = ButtonTextColor;
            button.hover.textColor = ButtonTextColor;
            button.active.textColor = ButtonTextColor;

            cornerTex = Solid(new Color(1f, 1f, 1f, 0.15f));
            corner = new GUIStyle(button) { fontSize = Mathf.RoundToInt(30 * U) };
            corner.normal.background = cornerTex;
            corner.hover.background = cornerTex;
            corner.active.background = cornerTex;
            corner.normal.textColor = Color.white;
            corner.hover.textColor = Color.white;
            corner.active.textColor = Color.white;
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * color.a);
            GUI.Label(new Rect(r.x + 2f * U, r.y + 2f * U, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        /// <summary>Text anchored at top-left. size: 1 = normal, 2 = double.</summary>
        public static void Text(string text, float x, float y, float size = 1f, Color? color = null)
        {
            Ensure();
            var s = new GUIStyle(label) { fontSize = Mathf.RoundToInt(30 * U * size) };
            Shadowed(new Rect(x * U, y * U, Screen.width - x * U, 200 * U * size), text, s, color ?? Palette.Text);
        }

        /// <summary>Text anchored at top-right (x = distance from the right edge).</summary>
        public static void TextRight(string text, float x, float y, float size = 1f, Color? color = null)
        {
            Ensure();
            var s = new GUIStyle(label) { fontSize = Mathf.RoundToInt(30 * U * size), alignment = TextAnchor.UpperRight };
            Shadowed(new Rect(0f, y * U, Screen.width - x * U, 200 * U * size), text, s, color ?? Palette.Text);
        }

        /// <summary>Text centered horizontally; yOffset is from the screen center (negative = higher).</summary>
        public static void Center(string text, float yOffset = 0f, float size = 1f, Color? color = null)
        {
            Ensure();
            var s = new GUIStyle(label) { fontSize = Mathf.RoundToInt(30 * U * size), alignment = TextAnchor.MiddleCenter };
            float h = 140f * U * size;
            float margin = 30f * U;
            Shadowed(new Rect(margin, Screen.height * 0.5f + yOffset * U - h * 0.5f, Screen.width - margin * 2f, h), text, s, color ?? Palette.Text);
        }

        /// <summary>Big centered button; yOffset from screen center. Returns true when clicked/tapped.</summary>
        public static bool Button(string text, float yOffset = 0f, float width = 380f, float height = 96f)
        {
            Ensure();
            float w = Mathf.Min(width * U, Screen.width * 0.85f), h = height * U;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f + yOffset * U - h * 0.5f, w, h);
            bool clicked = GUI.Button(r, text, button);
            if (clicked) Sfx.Click();
            return clicked;
        }

        /// <summary>Small square button in the top-right corner (e.g. pause "II").</summary>
        public static bool CornerButton(string text, float size = 72f, float margin = 16f)
        {
            Ensure();
            float s = size * U, m = margin * U;
            return GUI.Button(new Rect(Screen.width - s - m, m, s, s), text, corner);
        }

        /// <summary>Darken (or tint) the whole screen.</summary>
        public static void Overlay(Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>Progress / health bar. fill 0..1. Position in reference units from top-left.</summary>
        public static void Bar(float x, float y, float width, float height, float fill, Color color)
        {
            var old = GUI.color;
            var r = new Rect(x * U, y * U, width * U, height * U);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ---------- floating text & flash (drawn automatically on top) ----------

        class Floater { public string text; public Vector3 pos; public Color color; public float t; }
        static readonly List<Floater> floaters = new List<Floater>();
        static Color flashColor;
        static float flashTime, flashDuration;

        internal static void AddFloatingText(string text, Vector3 worldPos, Color color)
        {
            ArcRuntime.Ensure();
            floaters.Add(new Floater { text = text, pos = worldPos, color = color, t = 0f });
        }

        internal static void StartFlash(Color color, float duration)
        {
            ArcRuntime.Ensure();
            flashColor = color;
            flashDuration = Mathf.Max(0.01f, duration);
            flashTime = flashDuration;
        }

        internal static void DrawOverlays()
        {
            if (Event.current.type != EventType.Repaint) return;
            var cam = Camera.main;
            if (floaters.Count > 0) Ensure();
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.t += Time.unscaledDeltaTime;
                if (f.t > 0.9f || cam == null) { floaters.RemoveAt(i); continue; }
                Vector3 sp = cam.WorldToScreenPoint(f.pos);
                if (sp.z < 0f) continue;
                float rise = f.t * 90f * U;
                var c = f.color;
                c.a = 1f - f.t / 0.9f;
                var s = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(34 * U) };
                Shadowed(new Rect(sp.x - 150 * U, Screen.height - sp.y - rise - 30 * U, 300 * U, 60 * U), f.text, s, c);
            }
            if (flashTime > 0f)
            {
                flashTime -= Time.unscaledDeltaTime;
                var c = flashColor;
                c.a *= Mathf.Clamp01(flashTime / flashDuration) * 0.6f;
                Overlay(c);
            }
        }
    }
}
