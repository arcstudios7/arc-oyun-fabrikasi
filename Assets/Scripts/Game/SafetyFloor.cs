using UnityEngine;

namespace Game
{
    /// <summary>
    /// Streamed city tiles swap their meshes (and colliders) when the detail level changes. For a moment there may be
    /// nothing under the wheels. This invisible slab follows whatever the player controls and sits 8 cm below the last
    /// real street height, so the bike / courier lands on it instead of falling through the world.
    /// </summary>
    public class SafetyFloor : MonoBehaviour
    {
        public Transform Target;
        float lastY;
        bool known;
        float lostTime;

        public static SafetyFloor Create()
        {
            var go = new GameObject("Safety Floor");
            go.layer = EveWorld.FloorLayer;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(10f, 0.2f, 10f);
            return go.AddComponent<SafetyFloor>();
        }

        /// <summary>Call after teleporting the player.</summary>
        public void Forget() { known = false; lostTime = 0f; }

        void FixedUpdate()
        {
            if (Target == null) return;
            Vector3 p = Target.position;

            // Probe only the real tiles (not this floor) from a little above the player.
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 12f, EveWorld.TilesMask, QueryTriggerInteraction.Ignore))
            {
                lastY = hit.point.y;
                known = true;
                lostTime = 0f;
            }
            else
            {
                lostTime += Time.fixedDeltaTime;
            }

            if (!known) { transform.position = new Vector3(p.x, -10000f, p.z); return; }

            // Player sank well below the last street (fell through before the floor caught it): lift back up.
            if (p.y < lastY - 2.5f)
            {
                var rb = Target.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.position = new Vector3(p.x, lastY + 0.6f, p.z);
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                }
                else
                {
                    var cc = Target.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    Target.position = new Vector3(p.x, lastY + 0.1f, p.z);
                    if (cc != null) cc.enabled = true;
                }
            }

            // Slab 8 cm under the street (top face), centered under the player.
            transform.position = new Vector3(p.x, lastY - 0.08f - 0.1f, p.z);
            transform.rotation = Quaternion.Euler(0f, Target.eulerAngles.y, 0f);
        }
    }
}
