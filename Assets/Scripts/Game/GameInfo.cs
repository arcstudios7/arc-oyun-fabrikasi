namespace Game
{
    /// <summary>Identity of the current game. ArcBuild reads this for the app name, package id and orientation.</summary>
    public static class GameInfo
    {
        public const string Title = "Yıldız Avcısı";   // shown on menu + as the app name
        public const string Slug = "yildiz-avcisi";    // lowercase a-z, 0-9 and '-' only (package id + save keys)
        public const string Version = "1.0";
        public const bool Portrait = true;             // true = vertical phone game, false = horizontal
    }
}
