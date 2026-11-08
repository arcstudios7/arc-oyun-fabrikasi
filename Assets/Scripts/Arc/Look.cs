using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Arc
{
    /// <summary>
    /// "Realistic mode": real HDRI sky with image-based lighting and reflections, soft sun shadows,
    /// fog for depth and cinematic post-processing (ACES tonemapping, bloom, color grading, vignette).
    /// Call Look.Realistic(...) once in Setup(). The HDRI id must be in Game/kaynaklar.json.
    /// </summary>
    public static class Look
    {
        static Volume volume;
        static VolumeProfile profile;

        /// <summary>The scene's directional "sun" light.</summary>
        public static Light Sun { get; private set; }

        /// <param name="hdriId">Poly Haven HDRI id (sky + lighting). null = plain gradient sky.</param>
        /// <param name="sunElevation">Sun height in degrees (5 = sunset, 60 = noon).</param>
        /// <param name="sunAzimuth">Sun direction around the Y axis in degrees.</param>
        /// <param name="exposure">Overall brightness in stops (-1 darker, +1 brighter).</param>
        /// <param name="fogDensity">0 = no fog, 0.004 = light city haze, 0.02 = heavy fog.</param>
        public static void Realistic(string hdriId = null, float sunElevation = 35f, float sunAzimuth = -40f,
            float sunIntensity = 1.6f, Color? sunColor = null, float exposure = 0f, float fogDensity = 0.004f, Color? fogColor = null)
        {
            var cam = Camera.main;

            // --- Sun ---
            Sun = Object.FindFirstObjectByType<Light>();
            if (Sun == null || Sun.type != LightType.Directional)
            {
                var go = new GameObject("Sun");
                Sun = go.AddComponent<Light>();
                Sun.type = LightType.Directional;
            }
            Sun.color = sunColor ?? new Color(1f, 0.93f, 0.82f);
            Sun.intensity = sunIntensity;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.85f;
            Sun.transform.rotation = Quaternion.Euler(sunElevation, sunAzimuth, 0f);

            // --- Sky, ambient light and reflections ---
            Cubemap cube = string.IsNullOrEmpty(hdriId) ? null : Resources.Load<Cubemap>("PH/hdri/" + hdriId);
            var skyBase = Resources.Load<Material>("ArcMaterials/Sky");
            if (cube != null && skyBase != null)
            {
                var sky = new Material(skyBase);
                sky.SetTexture("_Tex", cube);
                sky.SetFloat("_Exposure", 1f);
                sky.SetFloat("_Rotation", 0f);
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1f;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = cube;
                RenderSettings.reflectionIntensity = 1f;
                DynamicGI.UpdateEnvironment();
                if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                if (!string.IsNullOrEmpty(hdriId)) Debug.LogWarning("[Arc] HDRI bulunamadı: " + hdriId);
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.8f);
                RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f);
                RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.2f);
            }

            // --- Fog (depth, hides the end of the world) ---
            RenderSettings.fog = fogDensity > 0f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor ?? new Color(0.78f, 0.74f, 0.70f);

            // --- Camera: post-processing + anti-aliasing ---
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                data.renderShadows = true;
                cam.allowHDR = true;
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 600f);
            }

            // --- Post-processing volume ---
            if (volume == null)
            {
                var go = new GameObject("ArcPostFX");
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.sharedProfile = profile;
            }

            var tone = Get<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = Get<Bloom>();
            bloom.intensity.Override(0.5f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.6f);

            var color = Get<ColorAdjustments>();
            color.postExposure.Override(exposure);
            color.contrast.Override(10f);
            color.saturation.Override(6f);

            var vignette = Get<Vignette>();
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);
        }

        /// <summary>Change exposure later (e.g. entering a dark tunnel).</summary>
        public static void SetExposure(float stops)
        {
            if (profile != null && profile.TryGet(out ColorAdjustments c)) c.postExposure.Override(stops);
        }

        static T Get<T>() where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            return profile.Add<T>(true);
        }
    }
}
