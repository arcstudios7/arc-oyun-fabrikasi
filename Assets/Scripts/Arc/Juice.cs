using System.Collections;
using UnityEngine;

namespace Arc
{
    /// <summary>"Game feel" helpers: camera shake, particle bursts, scale pops, flashes, floating text.</summary>
    public static class Juice
    {
        static float shakeTime, shakeDuration, shakeStrength;
        static Vector3 appliedOffset;
        static Transform shakenCam;

        /// <summary>Shake the main camera. strength in world units (0.2 small, 0.6 big).</summary>
        public static void Shake(float strength = 0.3f, float duration = 0.25f)
        {
            ArcRuntime.Ensure();
            shakeStrength = Mathf.Max(shakeStrength * (shakeTime > 0f ? 1f : 0f), strength);
            shakeDuration = Mathf.Max(duration, 0.01f);
            shakeTime = shakeDuration;
        }

        internal static void ApplyShake()
        {
            var cam = Camera.main;
            if (cam == null || shakeTime <= 0f) return;
            shakeTime -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(shakeTime / shakeDuration);
            appliedOffset = (Vector3)(Random.insideUnitCircle * shakeStrength * k);
            shakenCam = cam.transform;
            shakenCam.position += appliedOffset;
            if (shakeTime <= 0f) shakeStrength = 0f;
        }

        internal static void RemoveShake()
        {
            if (shakenCam != null) shakenCam.position -= appliedOffset;
            appliedOffset = Vector3.zero;
            shakenCam = null;
        }

        /// <summary>Explode particles at a point. Great for pickups, hits, deaths.</summary>
        public static void Burst(Vector3 position, Color color, int count = 24, float speed = 6f, float size = 0.25f, float lifetime = 0.6f, float gravity = 1f)
        {
            var go = new GameObject("Burst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = lifetime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Palette.Lighter(color, 0.5f));
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Mats.Sprite(Color.white);
            renderer.sortingOrder = 100;

            ps.Play();
            ps.Emit(count);
            Object.Destroy(go, lifetime + 0.5f);
        }

        /// <summary>Quick "boing" scale punch on an object (pickup, hit, button press).</summary>
        public static void Pop(Transform target, float amount = 0.3f, float duration = 0.18f)
        {
            if (target == null) return;
            ArcRuntime.Run(PopRoutine(target, amount, duration));
        }

        static IEnumerator PopRoutine(Transform t, float amount, float duration)
        {
            Vector3 baseScale = t.localScale;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
                t.localScale = baseScale * (1f + amount * k);
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        /// <summary>Freeze the game for a split second on big impacts ("hit stop").</summary>
        public static void HitStop(float seconds = 0.06f)
        {
            ArcRuntime.Run(HitStopRoutine(seconds));
        }

        static IEnumerator HitStopRoutine(float seconds)
        {
            float before = Time.timeScale;
            if (before <= 0f) yield break;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (Time.timeScale > 0f) Time.timeScale = before;
        }

        /// <summary>Text that floats up and fades (e.g. "+10"). Drawn by Hud automatically.</summary>
        public static void FloatText(string text, Vector3 worldPosition, Color color)
        {
            Hud.AddFloatingText(text, worldPosition, color);
        }

        /// <summary>Full-screen color flash (white on pickup, red on damage). Drawn by Hud automatically.</summary>
        public static void Flash(Color color, float duration = 0.15f)
        {
            Hud.StartFlash(color, duration);
        }
    }
}
