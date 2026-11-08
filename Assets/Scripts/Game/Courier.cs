using Arc;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The courier on foot: CharacterController movement relative to the camera (walk / run), smooth turning,
    /// gravity and slopes, and a realistic Mixamo character with Idle / Walk / Run / Ride animations
    /// (Resources/Kurye). Falls back to a simple figure until the character files are added.
    /// </summary>
    public class Courier : MonoBehaviour
    {
        const float WalkSpeed = 1.9f;
        const float RunSpeed = 5.6f;
        const float TurnSpeed = 720f;
        const float Gravity = -20f;

        public CharacterController Controller { get; private set; }
        public Transform Model { get; private set; }
        public float Speed { get; private set; }
        public bool Riding { get; private set; }

        Animator anim;
        RideIK ik;
        float verticalVel, animSpeed;
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int RidingHash = Animator.StringToHash("Riding");

        public static Courier Create(Vector3 pos, float yaw)
        {
            var go = new GameObject("Kurye");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var c = go.AddComponent<Courier>();
            c.Build();
            return c;
        }

        void Build()
        {
            Controller = gameObject.AddComponent<CharacterController>();
            Controller.height = 1.8f;
            Controller.radius = 0.3f;
            Controller.center = new Vector3(0f, 0.92f, 0f);
            Controller.slopeLimit = 50f;
            Controller.stepOffset = 0.45f;
            Controller.skinWidth = 0.04f;

            var prefab = Resources.Load<GameObject>("Kurye/Kurye");
            if (prefab != null)
            {
                Model = Instantiate(prefab, transform).transform;
                Model.localPosition = Vector3.zero;
                Model.localRotation = Quaternion.identity;
                Model.name = "KuryeModel";
                FixMaterials(Model.gameObject);
                foreach (var r in Model.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.sharedMaterials)
                        if (m != null)
                            Debug.Log("[Eve] Kurye malzemesi: " + r.name + " / " + m.name + " / " + m.shader.name + " / doku=" +
                                      (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : "YOK"));
                anim = Model.GetComponentInChildren<Animator>();
                if (anim == null) anim = Model.gameObject.AddComponent<Animator>();
                anim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Kurye/KuryeAnimator");
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ik = anim.gameObject.AddComponent<RideIK>();
                ik.Anim = anim;
                // Mixamo characters are ~1.8 m already; normalize just in case.
                var b = Props.BoundsOf(Model.gameObject);
                if (b.size.y > 0.5f && (b.size.y < 1.5f || b.size.y > 2.1f)) Model.localScale *= 1.78f / b.size.y;
            }
            else
            {
                Model = FallbackFigure(transform);
            }
            Bike.SetLayerRecursive(gameObject, EveWorld.PlayerLayer);
        }

        /// <summary>FBX materials come in as the built-in Standard shader (invisible / pink under URP): convert to URP Lit.</summary>
        public static void FixMaterials(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader.name.StartsWith("Universal Render Pipeline")) continue;
                    var tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    var nor = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                    var col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    var nm = new Material(Mats.Paint(col, 0.35f, 0f)) { name = m.name + " (URP)" };
                    if (tex != null) nm.SetTexture("_BaseMap", tex);
                    if (nor != null) { nm.SetTexture("_BumpMap", nor); nm.EnableKeyword("_NORMALMAP"); }
                    mats[i] = nm;
                }
                r.sharedMaterials = mats;
            }
        }

        static Transform FallbackFigure(Transform parent)
        {
            var root = Shapes.Group("KuryeModel", Vector3.zero, parent);
            Material jacket = Mats.Paint(Palette.Hex("#C8102E"), 0.3f, 0f);
            Material jeans = Mats.Paint(Palette.Hex("#2B3A55"), 0.25f, 0f);
            Material helmet = Mats.Paint(Palette.Hex("#E8E8E8"), 0.85f, 0.1f);
            Material skin = Mats.Paint(Palette.Hex("#C58C68"), 0.3f, 0f);
            Shapes.Capsule(new Vector3(0f, 1.25f, 0f), new Vector3(0.46f, 0.36f, 0.3f), Color.white, parent: root).GetComponent<Renderer>().sharedMaterial = jacket;
            Shapes.Sphere(new Vector3(0f, 1.72f, 0f), 0.3f, Color.white, parent: root).GetComponent<Renderer>().sharedMaterial = helmet;
            Shapes.Sphere(new Vector3(0f, 1.66f, 0.1f), 0.18f, Color.white, parent: root).GetComponent<Renderer>().sharedMaterial = skin;
            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Capsule(new Vector3(s * 0.12f, 0.48f, 0f), new Vector3(0.16f, 0.45f, 0.16f), Color.white, parent: root).GetComponent<Renderer>().sharedMaterial = jeans;
                Shapes.Capsule(new Vector3(s * 0.3f, 1.2f, 0f), new Vector3(0.12f, 0.32f, 0.12f), Color.white, parent: root).GetComponent<Renderer>().sharedMaterial = jacket;
            }
            return root;
        }

        /// <summary>Walk / run relative to the camera. move = stick / WASD (-1..1).</summary>
        public void Walk(Vector2 move, bool sprint, Transform cam, float dt)
        {
            if (Riding) return;
            Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 wish = fwd * move.y + right * move.x;
            float input = Mathf.Clamp01(wish.magnitude);
            float target = input * (sprint ? RunSpeed : WalkSpeed);
            Speed = Mathf.MoveTowards(Speed, target, (target > Speed ? 10f : 14f) * dt);

            if (input > 0.05f)
            {
                Quaternion want = Quaternion.LookRotation(wish.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, TurnSpeed * dt);
            }

            if (Controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
            verticalVel += Gravity * dt;
            Vector3 motion = transform.forward * Speed + Vector3.up * verticalVel;
            Controller.Move(motion * dt);
            if (transform.position.y < -200f) transform.position += Vector3.up * 300f;
            Animate(dt);
        }

        void Animate(float dt)
        {
            animSpeed = Mathf.Lerp(animSpeed, Riding ? 0f : Speed, 1f - Mathf.Exp(-12f * dt));
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.SetFloat(SpeedHash, animSpeed);
                anim.SetBool(RidingHash, Riding);
            }
            else if (!Riding)
            {
                // fallback: little walking bob
                float bob = Mathf.Sin(Time.time * (4f + animSpeed * 2f)) * 0.03f * Mathf.Clamp01(animSpeed);
                Model.localPosition = new Vector3(0f, bob, 0f);
            }
        }

        /// <summary>Sit on the bike seat (parented: moves and leans with the bike).</summary>
        public void Mount(Bike bike)
        {
            Riding = true;
            Speed = 0f;
            Controller.enabled = false;
            transform.SetParent(bike.Seat, false);
            transform.localPosition = new Vector3(0f, anim != null ? -0.48f : -0.86f, 0.08f); // sitting hips ~0.48 m above feet
            transform.localRotation = Quaternion.identity;
            if (ik != null)
            {
                ik.LeftHand = bike.GripL; ik.RightHand = bike.GripR;
                ik.LeftFoot = bike.FootL; ik.RightFoot = bike.FootR;
                ik.Active = true;
            }
            Animate(1f);
        }

        /// <summary>Step off to the left of the bike.</summary>
        public void Dismount(Bike bike)
        {
            Riding = false;
            if (ik != null) ik.Active = false;
            transform.SetParent(null, true);
            Vector3 side = bike.transform.position - bike.transform.right * 0.9f;
            EveWorld.StreetGround(side, bike.transform.position.y, out var ground);
            transform.SetPositionAndRotation(ground + Vector3.up * 0.05f, Quaternion.Euler(0f, bike.transform.eulerAngles.y, 0f));
            Controller.enabled = true;
            verticalVel = 0f;
            Animate(1f);
        }

        public void TickRiding(float dt) => Animate(dt);

        public void Teleport(Vector3 pos, float yaw)
        {
            bool was = Controller.enabled;
            Controller.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Controller.enabled = was;
        }
    }
}
