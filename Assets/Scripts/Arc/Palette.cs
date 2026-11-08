using UnityEngine;

namespace Arc
{
    /// <summary>One coherent color set so every game looks designed. Override per game if wanted.</summary>
    public static class Palette
    {
        public static Color Bg = Hex("#1B1F3B");       // deep night blue
        public static Color Ground = Hex("#2E3466");
        public static Color Primary = Hex("#4FD1C5");  // player / main actor (teal)
        public static Color Accent = Hex("#FFD166");   // collectibles, highlights (gold)
        public static Color Danger = Hex("#EF476F");   // enemies, hazards (pink-red)
        public static Color Good = Hex("#06D6A0");     // bonus, success (green)
        public static Color Purple = Hex("#9B5DE5");
        public static Color Text = Hex("#F8F9FA");

        /// <summary>"#RRGGBB" or "#RRGGBBAA" → Color. Returns magenta if the string is invalid.</summary>
        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        /// <summary>Same color, a bit brighter (amount 0..1).</summary>
        public static Color Lighter(Color c, float amount = 0.25f) => Color.Lerp(c, Color.white, amount);

        /// <summary>Same color, a bit darker (amount 0..1).</summary>
        public static Color Darker(Color c, float amount = 0.25f) => Color.Lerp(c, Color.black, amount);
    }
}
