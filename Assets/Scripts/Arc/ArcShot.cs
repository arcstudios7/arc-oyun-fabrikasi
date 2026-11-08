using System.Collections;
using UnityEngine;

namespace Arc
{
    /// <summary>
    /// Automatic screenshots for CI ("ekran görüntüsü" build): the player is started with
    /// "-arcshot &lt;folder&gt;", takes a menu shot, starts the game, drives forward with some steering,
    /// takes timed shots and quits. Lets Claude see the game without a human.
    /// </summary>
    public class ArcShot : MonoBehaviour
    {
        public static bool Active { get; private set; }
        string dir;

        internal static void TryStart()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-arcshot") continue;
                Active = true;
                var go = new GameObject("ArcShot");
                DontDestroyOnLoad(go);
                go.AddComponent<ArcShot>().dir = args[i + 1];
                return;
            }
        }

        IEnumerator Start()
        {
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("01_menu");

            var game = FindFirstObjectByType<ArcGame>();
            if (game != null) game.StartGame();

            float[] times = { 2f, 3f, 3f, 4f, 4f };
            float[] steer = { 0f, 0.6f, -0.5f, 0f, 0.8f };
            for (int i = 0; i < times.Length; i++)
            {
                Inputs.SimMove = new Vector2(steer[i], 1f);
                yield return new WaitForSecondsRealtime(times[i]);
                yield return Shot((i + 2).ToString("00") + "_oyun");
            }
            Inputs.SimMove = Vector2.zero;
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[ArcShot] bitti, fps≈" + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0"));
            Application.Quit();
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = System.IO.Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[ArcShot] " + path);
            yield return null;
        }
    }
}
