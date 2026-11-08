using System.Collections.Generic;
using Arc;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Starter game "Yıldız Avcısı": move left/right, catch gold stars, dodge red blocks.
    /// Speed and spawn rate grow over time. Replace this file with any new game idea.
    /// </summary>
    public class GameMain : ArcGame
    {
        protected override string HowToPlay =>
            "Ekranın sol / sağ yarısına basılı tut (veya ← →)\nSarı yıldızları topla, kırmızı bloklardan kaç!";

        class Faller
        {
            public Transform t;
            public bool good;
            public Vector3 spin;
            public float radius;
        }

        Camera cam;
        Transform player;
        readonly List<Faller> fallers = new List<Faller>();
        float halfW, halfH, spawnTimer, speed, scoreTimer, tilt;

        const float PlayerY = -6.5f;
        const float PlayerSpeed = 11f;

        protected override void Setup()
        {
            cam = Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Bg;
            halfH = cam.orthographicSize;

            // Ground strip + background "stars" for depth.
            Shapes.Cube(new Vector3(0f, PlayerY - 1.4f, 1f), new Vector3(60f, 1.2f, 2f), Palette.Ground);
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(Random.Range(-12f, 12f), Random.Range(-8f, 9f), 5f);
                Shapes.Sphere(p, Random.Range(0.05f, 0.15f), new Color(1f, 1f, 1f, 1f), lit: false);
            }

            // Player: a little rounded ship made of 3 shapes.
            player = Shapes.Group("Player", new Vector3(0f, PlayerY, 0f));
            Shapes.Capsule(Vector3.zero, new Vector3(1.2f, 0.7f, 1.2f), Palette.Primary, parent: player).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Shapes.Sphere(new Vector3(0f, 0.35f, -0.2f), 0.55f, Palette.Lighter(Palette.Primary, 0.5f), parent: player);
            Shapes.Cube(new Vector3(0f, -0.45f, 0f), new Vector3(1.6f, 0.15f, 0.6f), Palette.Darker(Palette.Primary, 0.3f), parent: player);

            Sfx.Music("C4 E4 G4 E4 A4 G4 E4 D4 C4 E4 G4 C5 B4 G4 E4 -", 132f, 0.12f);
        }

        protected override void ResetRound()
        {
            foreach (var f in fallers) if (f.t != null) Destroy(f.t.gameObject);
            fallers.Clear();
            player.position = new Vector3(0f, PlayerY, 0f);
            player.rotation = Quaternion.identity;
            spawnTimer = 0.8f;
            speed = 5f;
            scoreTimer = 0f;
            tilt = 0f;
        }

        protected override void IdleTick(float dt)
        {
            // Gentle bobbing on menu so the screen feels alive.
            player.position = new Vector3(Mathf.Sin(Time.time * 1.5f) * 1.5f, PlayerY + Mathf.Sin(Time.time * 3f) * 0.1f, 0f);
        }

        protected override void Tick(float dt)
        {
            halfW = halfH * cam.aspect; // recomputed every frame: browser windows can be resized

            // Difficulty ramp.
            speed = 5f + PlayTime * 0.28f;

            // Movement with a little tilt for juice.
            float dir = Inputs.Horizontal;
            var p = player.position;
            p.x = Mathf.Clamp(p.x + dir * PlayerSpeed * dt, -halfW + 0.9f, halfW - 0.9f);
            player.position = p;
            tilt = Mathf.Lerp(tilt, -dir * 18f, dt * 10f);
            player.rotation = Quaternion.Euler(0f, 0f, tilt);

            // Survival points.
            scoreTimer += dt;
            if (scoreTimer >= 1f) { scoreTimer -= 1f; Score += 1; }

            // Spawning.
            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                spawnTimer = Mathf.Max(0.28f, 0.85f - PlayTime * 0.012f);
                Spawn();
            }

            // Move + collide.
            for (int i = fallers.Count - 1; i >= 0; i--)
            {
                var f = fallers[i];
                f.t.position += Vector3.down * speed * dt;
                f.t.Rotate(f.spin * dt);

                Vector2 d = f.t.position - player.position;
                if (d.magnitude < f.radius + 0.6f)
                {
                    if (f.good)
                    {
                        Score += 10;
                        Sfx.Coin();
                        Juice.Burst(f.t.position, Palette.Accent, 18, 5f, 0.25f);
                        Juice.FloatText("+10", f.t.position, Palette.Accent);
                        Juice.Pop(player, 0.25f);
                        Remove(i);
                    }
                    else
                    {
                        Sfx.Explosion();
                        Juice.Burst(player.position, Palette.Danger, 50, 9f, 0.35f, 0.9f);
                        Juice.Shake(0.6f, 0.35f);
                        Juice.Flash(Palette.Danger, 0.25f);
                        Juice.HitStop(0.08f);
                        GameOver();
                        return;
                    }
                }
                else if (f.t.position.y < -halfH - 2f)
                {
                    Remove(i);
                }
            }
        }

        void Spawn()
        {
            bool good = Random.value < 0.32f;
            float x = Random.Range(-halfW + 1f, halfW - 1f);
            var pos = new Vector3(x, halfH + 1.5f, 0f);
            GameObject go;
            float radius;
            if (good)
            {
                go = Shapes.Sphere(pos, 0.9f, Palette.Accent);
                radius = 0.45f;
            }
            else
            {
                float s = Random.Range(0.9f, 1.7f);
                go = Shapes.Cube(pos, Vector3.one * s, Palette.Danger);
                radius = s * 0.5f;
            }
            fallers.Add(new Faller
            {
                t = go.transform,
                good = good,
                radius = radius,
                spin = new Vector3(Random.Range(-180f, 180f), Random.Range(-180f, 180f), Random.Range(-90f, 90f))
            });
        }

        void Remove(int index)
        {
            if (fallers[index].t != null) Destroy(fallers[index].t.gameObject);
            fallers.RemoveAt(index);
        }
    }
}
