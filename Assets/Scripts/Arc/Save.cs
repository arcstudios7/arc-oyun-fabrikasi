using UnityEngine;

namespace Arc
{
    /// <summary>Tiny save system on PlayerPrefs (works on web, Android and desktop).</summary>
    public static class Save
    {
        static string K(string key) => Game.GameInfo.Slug + "." + key;

        public static int Best(string key = "best") => PlayerPrefs.GetInt(K(key), 0);

        /// <summary>Stores score if it beats the record. Returns true on a new record.</summary>
        public static bool SubmitBest(int score, string key = "best")
        {
            if (score <= Best(key)) return false;
            PlayerPrefs.SetInt(K(key), score);
            PlayerPrefs.Save();
            return true;
        }

        public static int GetInt(string key, int fallback = 0) => PlayerPrefs.GetInt(K(key), fallback);
        public static void SetInt(string key, int value) { PlayerPrefs.SetInt(K(key), value); PlayerPrefs.Save(); }
        public static float GetFloat(string key, float fallback = 0f) => PlayerPrefs.GetFloat(K(key), fallback);
        public static void SetFloat(string key, float value) { PlayerPrefs.SetFloat(K(key), value); PlayerPrefs.Save(); }
        public static string GetString(string key, string fallback = "") => PlayerPrefs.GetString(K(key), fallback);
        public static void SetString(string key, string value) { PlayerPrefs.SetString(K(key), value); PlayerPrefs.Save(); }
    }
}
