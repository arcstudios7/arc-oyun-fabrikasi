using Arc;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Delivery motorbike with rider. Arcade physics: throttle / brake / steering with speed-dependent turning,
    /// lean in corners, wheel spin, collisions with the street edge and obstacles.
    /// </summary>
    public class Moto
    {
        public Transform Root;      // position + heading
        Transform lean;             // visual lean (roll)
        Transform frontWheel, rearWheel, steerPivot;

        public float Speed;         // m/s, negative = reversing
        public const float MaxSpeed = 17f;   // ~61 km/h
        const float Accel = 6.5f, Brake = 14f, Drag = 1.2f, MaxReverse = 3f;
        float leanAngle, steerVisual;

        public float Kmh => Mathf.Abs(Speed) * 3.6f;
        public Vector3 Forward => Root.forward;

        public Moto()
        {
            Root = new GameObject("Motor").transform;
            lean = Shapes.Group("Yatis", Vector3.zero, Root);

            Material rubber = Mats.Paint(Palette.Hex("#141414"), 0.15f, 0f);
            Material chrome = Mats.Paint(Palette.Hex("#C9CDD2"), 0.9f, 1f);
            Material red = Mats.Paint(Palette.Hex("#C8102E"), 0.85f, 0.25f);
            Material black = Mats.Paint(Palette.Hex("#1C1C1E"), 0.5f, 0.1f);
            Material jacket = Mats.Paint(Palette.Hex("#D62828"), 0.25f, 0f);
            Material jeans = Mats.Paint(Palette.Hex("#2B3A55"), 0.2f, 0f);
            Material skin = Mats.Paint(Palette.Hex("#C58C68"), 0.3f, 0f);
            Material yellow = Mats.Paint(Palette.Hex("#FFC300"), 0.6f, 0f);

            // Wheels
            rearWheel = Wheel(new Vector3(0f, 0.31f, -0.62f), rubber, chrome);
            steerPivot = Shapes.Group("Gidon", new Vector3(0f, 0.31f, 0.66f), lean);
            frontWheel = Wheel(Vector3.zero, rubber, chrome, steerPivot);

            // Frame & body
            Part(PrimitiveType.Cube, new Vector3(0f, 0.55f, -0.05f), new Vector3(0.28f, 0.28f, 1.05f), black);
            var fairing = Part(PrimitiveType.Cube, new Vector3(0f, 0.78f, 0.42f), new Vector3(0.42f, 0.42f, 0.45f), red);
            fairing.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.72f, -0.2f), new Vector3(0.38f, 0.22f, 0.7f), red);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.86f, -0.32f), new Vector3(0.3f, 0.09f, 0.55f), black);  // seat
            Part(PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0.66f), new Vector3(0.16f, 0.12f, 0.08f), Mats.Unlit(Palette.Hex("#FFF4D6"))); // headlight
            Part(PrimitiveType.Cube, new Vector3(0f, 0.75f, -0.88f), new Vector3(0.2f, 0.06f, 0.05f), Mats.Unlit(Palette.Hex("#FF2A2A"))); // tail light
            var fork = Part(PrimitiveType.Cylinder, new Vector3(0f, 0.38f, 0f), new Vector3(0.05f, 0.4f, 0.05f), chrome, steerPivot);
            fork.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.75f, -0.08f), new Vector3(0.72f, 0.04f, 0.04f), black, steerPivot); // handlebar

            // Delivery box "EVE SERVİS"
            Part(PrimitiveType.Cube, new Vector3(0f, 1.18f, -0.68f), new Vector3(0.52f, 0.46f, 0.48f), red);
            Part(PrimitiveType.Cube, new Vector3(0f, 1.18f, -0.68f), new Vector3(0.53f, 0.09f, 0.49f), yellow);

            // Rider
            var torso = Part(PrimitiveType.Capsule, new Vector3(0f, 1.25f, -0.12f), new Vector3(0.42f, 0.36f, 0.3f), jacket);
            torso.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0.03f), new Vector3(0.3f, 0.32f, 0.33f), red);                    // helmet
            Part(PrimitiveType.Cube, new Vector3(0f, 1.72f, 0.17f), new Vector3(0.24f, 0.1f, 0.04f), Mats.Glass(Palette.Hex("#111820"))); // visor
            foreach (float s in new[] { -1f, 1f })
            {
                var arm = Part(PrimitiveType.Capsule, new Vector3(s * 0.25f, 1.28f, 0.22f), new Vector3(0.11f, 0.26f, 0.11f), jacket);
                arm.transform.localRotation = Quaternion.Euler(70f, 0f, s * -12f);
                Part(PrimitiveType.Sphere, new Vector3(s * 0.32f, 1.07f, 0.52f), Vector3.one * 0.09f, skin);   // hands (gloves would be black)
                var thigh = Part(PrimitiveType.Capsule, new Vector3(s * 0.19f, 0.92f, 0.02f), new Vector3(0.14f, 0.25f, 0.14f), jeans);
                thigh.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
                var shin = Part(PrimitiveType.Capsule, new Vector3(s * 0.22f, 0.62f, 0.2f), new Vector3(0.12f, 0.22f, 0.12f), jeans);
                shin.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                Part(PrimitiveType.Cube, new Vector3(s * 0.22f, 0.4f, 0.25f), new Vector3(0.12f, 0.1f, 0.24f), black); // shoes
            }
        }

        Transform Wheel(Vector3 pos, Material tire, Material rim, Transform parent = null)
        {
            var w = Shapes.Group("Teker", pos, parent != null ? parent : lean);
            var t = Shapes.Make(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.62f, 0.07f, 0.62f), Color.white, parent: w);
            t.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            t.GetComponent<Renderer>().sharedMaterial = tire;
            var r = Shapes.Make(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.36f, 0.075f, 0.36f), Color.white, parent: w);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            r.GetComponent<Renderer>().sharedMaterial = rim;
            // a spoke so the spin is visible
            var sp = Shapes.Make(PrimitiveType.Cube, Vector3.zero, new Vector3(0.16f, 0.5f, 0.05f), Color.white, parent: w);
            sp.GetComponent<Renderer>().sharedMaterial = tire;
            return w;
        }

        GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Transform parent = null)
        {
            var go = Shapes.Make(type, pos, scale, Color.white, parent: parent != null ? parent : lean);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        public void Place(Vector3 pos, float yaw)
        {
            Root.position = pos;
            Root.rotation = Quaternion.Euler(0f, yaw, 0f);
            Speed = 0f;
            leanAngle = 0f;
        }

        /// <summary>Drive one frame. throttle -1..1 (negative = brake / reverse), steer -1..1. Returns true on a crash.</summary>
        public bool Drive(float throttle, float steer, float dt, City city)
        {
            // Longitudinal
            if (throttle > 0.05f)
                Speed += (Speed < 0f ? Brake : Accel * (1f - Speed / MaxSpeed * 0.6f)) * throttle * dt;
            else if (throttle < -0.05f)
                Speed -= (Speed > 0.5f ? Brake : Accel * 0.5f) * -throttle * dt;
            else
                Speed = Mathf.MoveTowards(Speed, 0f, Drag * dt + Mathf.Abs(Speed) * 0.05f * dt);
            Speed = Mathf.Clamp(Speed, -MaxReverse, MaxSpeed);

            // Steering: turning radius grows with speed; no turning when standing still.
            float speedFactor = Mathf.Clamp01(Mathf.Abs(Speed) / 4f);
            float turnRate = Mathf.Lerp(95f, 48f, Mathf.Abs(Speed) / MaxSpeed) * speedFactor;
            Root.Rotate(0f, steer * turnRate * Mathf.Sign(Speed == 0f ? 1f : Speed) * dt, 0f);

            // Move + collisions
            bool crash = false;
            Vector3 next = Root.position + Root.forward * Speed * dt;
            if (!city.IsDrivable(next))
            {
                // slide along the edge if possible
                Vector3 slideX = new Vector3(next.x, next.y, Root.position.z);
                Vector3 slideZ = new Vector3(Root.position.x, next.y, next.z);
                if (city.IsDrivable(slideZ)) next = slideZ;
                else if (city.IsDrivable(slideX)) next = slideX;
                else next = Root.position;
                if (Mathf.Abs(Speed) > 7f) crash = true;
                Speed *= 0.6f;
            }
            if (city.HitsObstacle(next, 0.45f, out var push))
            {
                next += push;
                if (Mathf.Abs(Speed) > 6f) crash = true;
                Speed *= -0.25f;
            }
            Root.position = next;

            // Visuals: lean, steer, wheel spin
            float targetLean = -steer * 28f * Mathf.Clamp01(Mathf.Abs(Speed) / 8f);
            leanAngle = Mathf.Lerp(leanAngle, targetLean, dt * 6f);
            lean.localRotation = Quaternion.Euler(0f, 0f, leanAngle);
            steerVisual = Mathf.Lerp(steerVisual, steer * 25f * (1f - Mathf.Clamp01(Mathf.Abs(Speed) / MaxSpeed) * 0.7f), dt * 10f);
            steerPivot.localRotation = Quaternion.Euler(0f, steerVisual, 0f);
            float spin = Speed / 0.31f * Mathf.Rad2Deg * dt;
            frontWheel.Rotate(spin, 0f, 0f, Space.Self);
            rearWheel.Rotate(spin, 0f, 0f, Space.Self);
            return crash;
        }
    }
}
