using Arc;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Delivery motorbike with real physics: Rigidbody + two raycast suspension wheels (stable on the bumpy
    /// photogrammetry streets), engine torque curve, front/rear brakes, tyre side grip, speed-sensitive steering,
    /// counter-steer lean (visual), wheelie-free pitch from the suspension, collisions with the real buildings.
    /// </summary>
    public class Bike : MonoBehaviour
    {
        // --- tuning (meters, kg, m/s) ---
        const float Mass = 190f;              // bike + rider
        const float WheelRadius = 0.31f;
        const float RestLength = 0.35f;
        const float Spring = 26000f;
        const float Damper = 2600f;
        const float EnginePower = 2600f;      // N at low speed
        const float TopSpeed = 22f;           // ~80 km/h
        const float BrakeForce = 3800f;
        const float ReverseSpeed = 2.5f;
        const float SideGrip = 9f;            // how strongly sideways sliding is cancelled
        const float Wheelbase = 1.35f;

        public Rigidbody Body { get; private set; }
        public Transform Seat { get; private set; }
        public Transform Visual { get; private set; }   // model, leaned in corners
        public float Speed => Vector3.Dot(Body.linearVelocity, transform.forward);
        public float Kmh => Mathf.Abs(Speed) * 3.6f;
        public bool Grounded { get; private set; }
        public bool Ridden;

        float throttle, brake, steerInput, steerAngle, lean;
        bool handbrake;
        Transform frontWheel, rearWheel, steerPivot;
        readonly float[] lastCompression = new float[2];
        // ray origins: at rest the ground sits at local y = 0 (0.65 = wheel radius + spring travel at static load)
        readonly Vector3[] wheelLocal = { new Vector3(0f, 0.65f, 0.68f), new Vector3(0f, 0.65f, -0.67f) };
        AudioSource engine;

        public static Bike Create(Vector3 pos, float yaw)
        {
            var go = new GameObject("Motor");
            go.layer = EveWorld.PlayerLayer;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var bike = go.AddComponent<Bike>();
            bike.Build();
            return bike;
        }

        void Build()
        {
            Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = Mass;
            Body.linearDamping = 0.05f;
            Body.angularDamping = 2.5f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.centerOfMass = new Vector3(0f, 0.45f, 0f);
            Body.constraints = RigidbodyConstraints.FreezeRotationZ; // never falls over; lean is visual

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = 0.32f;
            col.height = 1.9f;
            col.center = new Vector3(0f, 0.75f, 0f);
            var mat = new PhysicsMaterial("Moto") { dynamicFriction = 0.2f, staticFriction = 0.2f, bounciness = 0.05f };
            col.sharedMaterial = mat;

            Visual = Shapes.Group("Gorunum", Vector3.zero, transform);
            Seat = Shapes.Group("Sele", new Vector3(0f, 0.86f, -0.22f), Visual);
            BikeModel.Build(this, Visual, out frontWheel, out rearWheel, out steerPivot);
            SetLayerRecursive(gameObject, EveWorld.PlayerLayer);

            engine = gameObject.AddComponent<AudioSource>();
            engine.clip = EngineClip();
            engine.loop = true;
            engine.volume = 0f;
            engine.spatialBlend = 0.7f;
            engine.Play();
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        /// <summary>Set by the player each frame. throttle/brake 0..1, steer -1..1.</summary>
        public void SetInput(float throttle01, float brake01, float steer, bool hand)
        {
            throttle = throttle01;
            brake = brake01;
            steerInput = steer;
            handbrake = hand;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (!Ridden) { throttle = 0f; steerInput = 0f; brake = 0.3f; }

            // --- suspension ---
            int grounded = 0;
            Vector3 groundNormal = Vector3.zero;
            for (int i = 0; i < 2; i++)
            {
                Vector3 origin = transform.TransformPoint(wheelLocal[i]);
                float maxLen = RestLength + WheelRadius + 0.25f;
                if (Physics.Raycast(origin, -transform.up, out var hit, maxLen, EveWorld.GroundMask, QueryTriggerInteraction.Ignore))
                {
                    float compression = Mathf.Clamp01(1f - (hit.distance - WheelRadius) / RestLength);
                    float velocity = (compression - lastCompression[i]) / dt;
                    lastCompression[i] = compression;
                    float force = compression * Spring + velocity * Damper;
                    if (force > 0f) Body.AddForceAtPosition(transform.up * force, origin);
                    grounded++;
                    groundNormal += hit.normal;
                }
                else
                {
                    lastCompression[i] = 0f;
                }
            }
            Grounded = grounded > 0;

            Vector3 v = Body.linearVelocity;
            float fwdSpeed = Vector3.Dot(v, transform.forward);

            if (Grounded)
            {
                // --- engine & brakes ---
                float drive = 0f;
                if (throttle > 0.01f)
                {
                    float k = Mathf.Clamp01(1f - fwdSpeed / TopSpeed);
                    drive = EnginePower * throttle * (0.35f + 0.65f * k);
                    if (fwdSpeed < -0.5f) drive += BrakeForce * throttle; // braking out of reverse
                }
                if (brake > 0.01f)
                {
                    if (fwdSpeed > 0.5f) drive -= BrakeForce * brake;
                    else if (fwdSpeed > -ReverseSpeed) drive -= EnginePower * 0.35f * brake; // gentle reverse (pushing back)
                }
                if (handbrake) drive -= Mathf.Sign(fwdSpeed) * BrakeForce * 0.6f;
                // rolling resistance + air drag
                drive -= fwdSpeed * 18f + fwdSpeed * Mathf.Abs(fwdSpeed) * 0.9f;
                Body.AddForce(transform.forward * drive);

                // --- tyre side grip (no ice-skating) ---
                float side = Vector3.Dot(v, transform.right);
                float grip = handbrake ? SideGrip * 0.35f : SideGrip;
                Body.AddForce(-transform.right * side * Mass * grip); // grip = 1/s, cancels sideways slide

                // --- steering: bicycle model, max steer angle shrinks with speed ---
                float maxSteer = Mathf.Lerp(38f, 9f, Mathf.Clamp01(Mathf.Abs(fwdSpeed) / 20f));
                steerAngle = Mathf.MoveTowards(steerAngle, steerInput * maxSteer, 160f * dt);
                float yawRate = fwdSpeed / Wheelbase * Mathf.Tan(steerAngle * Mathf.Deg2Rad); // rad/s
                Vector3 av = Body.angularVelocity;
                float wantY = yawRate;
                av.y = Mathf.Lerp(av.y, wantY, 1f - Mathf.Exp(-10f * dt));
                Body.angularVelocity = av;

                // keep pitch aligned with the road
                if (grounded == 2)
                {
                    Vector3 n = groundNormal.normalized;
                    Vector3 fwdOnGround = Vector3.ProjectOnPlane(transform.forward, n).normalized;
                    Quaternion want = Quaternion.LookRotation(fwdOnGround, n);
                    Vector3 e = want.eulerAngles;
                    Quaternion pitchOnly = Quaternion.Euler(e.x, transform.eulerAngles.y, 0f);
                    Body.MoveRotation(Quaternion.Slerp(Body.rotation, pitchOnly, 1f - Mathf.Exp(-6f * dt)));
                }
            }
            else
            {
                // airborne: level out gently
                Quaternion level = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                Body.MoveRotation(Quaternion.Slerp(Body.rotation, level, 1f - Mathf.Exp(-1.5f * dt)));
            }

            // safety: if fallen through the world, put back on the street
            if (transform.position.y < -200f) Body.position += Vector3.up * 300f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float spd = Speed;
            // Lean into the turn (visual only), counter-lean at very low speed
            float lateralG = spd * spd / Wheelbase * Mathf.Tan(steerAngle * Mathf.Deg2Rad) / 9.81f;
            float wantLean = Mathf.Clamp(-Mathf.Atan(lateralG) * Mathf.Rad2Deg, -42f, 42f);
            if (!Grounded) wantLean = 0f;
            lean = Mathf.Lerp(lean, wantLean, 1f - Mathf.Exp(-6f * dt));
            Visual.localRotation = Quaternion.Euler(0f, 0f, lean);

            if (steerPivot != null) steerPivot.localRotation = Quaternion.Euler(0f, steerAngle, 0f);
            float spin = spd / WheelRadius * Mathf.Rad2Deg * dt;
            if (frontWheel != null) frontWheel.Rotate(spin, 0f, 0f, Space.Self);
            if (rearWheel != null) rearWheel.Rotate(spin, 0f, 0f, Space.Self);

            // engine sound: idle when ridden, pitch with speed + throttle
            float targetVol = Ridden ? 0.22f + throttle * 0.18f : 0f;
            engine.volume = Mathf.Lerp(engine.volume, targetVol * Sfx.Volume, dt * 4f);
            engine.pitch = Mathf.Lerp(engine.pitch, 0.7f + Mathf.Abs(spd) / TopSpeed * 1.3f + throttle * 0.15f, dt * 5f);
        }

        public void Horn() => Sfx.Tone("korna", 420f, 400f, 0.35f, Wave.Square, 0.5f, 0f);

        /// <summary>Put the bike back on its wheels on the street (used after crashes / getting stuck).</summary>
        public void Reset(Vector3 pos, float yaw)
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = pos + Vector3.up * 0.6f;
            Body.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.SetPositionAndRotation(Body.position, Body.rotation);
        }

        static AudioClip EngineClip()
        {
            // Two-stroke scooter "brrr": low saw + pulse, 0.5 s seamless loop.
            const int rate = 22050;
            int n = rate / 2;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = 62f;
                float saw = (t * f % 1f) * 2f - 1f;
                float pulse = (t * f * 2f % 1f) < 0.3f ? 0.6f : -0.2f;
                data[i] = (saw * 0.5f + pulse * 0.35f) * 0.6f;
            }
            var clip = AudioClip.Create("motor", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
