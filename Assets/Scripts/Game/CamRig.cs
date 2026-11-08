using UnityEngine;

namespace Game
{
    /// <summary>
    /// GTA-style third-person camera: free orbit with mouse / right stick, automatically swings back
    /// behind the bike when you ride without touching the camera, never clips into buildings
    /// (sphere cast against the real city geometry), speed-based field of view.
    /// </summary>
    public class CamRig
    {
        readonly Camera cam;
        public Transform Target;
        public float Distance = 4.2f;
        public float Height = 1.6f;      // look-at point above target pivot
        public bool VehicleMode;

        float yaw, pitch = 12f, currentDist, lastLookTime = -10f, fov = 62f;
        Vector3 smoothFocus;

        public CamRig(Camera camera)
        {
            cam = camera;
            currentDist = Distance;
        }

        public void SnapBehind()
        {
            if (Target == null) return;
            yaw = Target.eulerAngles.y;
            pitch = VehicleMode ? 10f : 14f;
            smoothFocus = Target.position + Vector3.up * Height;
            Apply(1f, 0f);
        }

        /// <param name="look">camera input (already scaled)</param>
        /// <param name="speed">target speed m/s (for auto-follow and FOV)</param>
        public void Tick(Vector2 look, bool lookIsMouse, float speed, bool recenter, float dt)
        {
            if (Target == null) return;
            if (look.sqrMagnitude > 0.0001f)
            {
                float k = lookIsMouse ? 1f : dt * 60f;
                yaw += look.x * k;
                pitch -= look.y * k;
                lastLookTime = Time.time;
            }
            pitch = Mathf.Clamp(pitch, -25f, 70f);

            // Auto swing behind the vehicle (or a walking player after a while)
            bool idleCam = Time.time - lastLookTime > (VehicleMode ? 1.2f : 3.5f);
            if (recenter || (idleCam && speed > 2f))
            {
                float target = Target.eulerAngles.y;
                float rate = recenter ? 12f : Mathf.Lerp(1.5f, 4f, Mathf.Clamp01(speed / 15f));
                yaw = Mathf.LerpAngle(yaw, target, 1f - Mathf.Exp(-rate * dt));
                pitch = Mathf.Lerp(pitch, VehicleMode ? 10f : 14f, 1f - Mathf.Exp(-2f * dt));
            }

            Apply(dt, speed);
        }

        void Apply(float dt, float speed)
        {
            Vector3 focusTarget = Target.position + Vector3.up * Height;
            smoothFocus = Vector3.Lerp(smoothFocus, focusTarget, 1f - Mathf.Exp(-(VehicleMode ? 14f : 18f) * dt));
            if ((smoothFocus - focusTarget).sqrMagnitude > 25f) smoothFocus = focusTarget; // teleport

            float wantDist = Distance + (VehicleMode ? speed * 0.06f : 0f);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 dir = rot * Vector3.back;

            // Collision: keep the camera in front of walls / under balconies
            float allowed = wantDist;
            if (Physics.SphereCast(smoothFocus, 0.3f, dir, out var hit, wantDist, EveWorld.GroundMask, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(0.6f, hit.distance - 0.1f);
            currentDist = allowed < currentDist ? allowed : Mathf.Lerp(currentDist, allowed, 1f - Mathf.Exp(-4f * dt));

            cam.transform.position = smoothFocus + dir * currentDist;
            cam.transform.rotation = Quaternion.LookRotation(smoothFocus - cam.transform.position, Vector3.up);

            float wantFov = 60f + (VehicleMode ? Mathf.Clamp(speed * 0.9f, 0f, 18f) : 0f);
            fov = Mathf.Lerp(fov, wantFov, 1f - Mathf.Exp(-3f * dt));
            cam.fieldOfView = fov;
        }

        /// <summary>Cinematic slow orbit (menu / loading screen).</summary>
        public void Orbit(Vector3 center, float radius, float height, float dt)
        {
            yaw += dt * 6f;
            Vector3 off = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -radius);
            cam.transform.position = center + off + Vector3.up * height;
            cam.transform.LookAt(center);
            cam.fieldOfView = 55f;
        }
    }
}
