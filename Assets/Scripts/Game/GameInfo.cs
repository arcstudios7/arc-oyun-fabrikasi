namespace Game
{
    /// <summary>Identity of the current game. ArcBuild reads this for the app name, package id and orientation.</summary>
    public static class GameInfo
    {
        public const string Title = "Eve Servis";      // shown on menu + as the app name
        public const string Slug = "eve-servis";        // lowercase a-z, 0-9 and '-' only (package id + save keys + web address)
        public const string Version = "0.3";
        public const bool Portrait = false;             // true = vertical phone game, false = horizontal
    }
}
