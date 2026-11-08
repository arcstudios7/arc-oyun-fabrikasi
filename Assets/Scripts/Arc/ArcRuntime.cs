using System.Collections;
using UnityEngine;

namespace Arc
{
    /// <summary>
    /// Boots every game: makes sure a camera + light exist, then adds Game.GameMain.
    /// No scene editing is ever needed — the whole game is built from code.
    /// </summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;

            ArcRuntime.Ensure();
            EnsureCamera();
            EnsureLight();

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            if (Object.FindFirstObjectByType<ArcGame>() == null)
            {
                var go = new GameObject("Game");
                go.AddComponent<Game.GameMain>();
            }
        }

        static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Bg;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        static void EnsureLight()
        {
            if (Object.FindFirstObjectByType<Light>() != null) return;
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    /// <summary>
    /// Hidden helper object: runs coroutines for static helpers, ticks input and removes camera shake.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ArcRuntime : MonoBehaviour
    {
        static ArcRuntime instance;

        public static ArcRuntime Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("ArcRuntime");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ArcRuntime>();
            go.AddComponent<ArcLate>();
            return instance;
        }

        /// <summary>Start a coroutine from anywhere (static code included).</summary>
        public static Coroutine Run(IEnumerator routine) => Ensure().StartCoroutine(routine);

        void Update()
        {
            Juice.RemoveShake();
            Inputs.Tick();
        }
    }

    /// <summary>Runs after all game LateUpdates so the shake offset sits on top of the game's own camera code.</summary>
    [DefaultExecutionOrder(10000)]
    public class ArcLate : MonoBehaviour
    {
        void LateUpdate() => Juice.ApplyShake();

        void OnGUI()
        {
            GUI.depth = -100; // draw floating texts / flashes above the game's HUD
            Hud.DrawOverlays();
        }
    }
}
