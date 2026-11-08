using UnityEngine;

namespace Arc
{
    /// <summary>
    /// One input API that works on keyboard, mouse AND touch screens (phone / browser).
    /// Uses the classic Input Manager, which this project is configured for.
    /// </summary>
    public static class Inputs
    {
        static Vector2 downPos;
        static float downTime;
        static Vector2Int swipe;
        static bool tapped;

        /// <summary>-1..1. Arrow keys / A-D, or holding the left/right half of the screen.</summary>
        public static float Horizontal
        {
            get
            {
                float k = 0f;
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) k -= 1f;
                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) k += 1f;
                if (k != 0f) return k;
                if (Input.GetMouseButton(0)) return Input.mousePosition.x < Screen.width * 0.5f ? -1f : 1f;
                return 0f;
            }
        }

        /// <summary>-1..1. Arrow keys / W-S, or holding the top/bottom half of the screen.</summary>
        public static float Vertical
        {
            get
            {
                float k = 0f;
                if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) k -= 1f;
                if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) k += 1f;
                if (k != 0f) return k;
                if (Input.GetMouseButton(0)) return Input.mousePosition.y < Screen.height * 0.5f ? -1f : 1f;
                return 0f;
            }
        }

        /// <summary>Both axes together (keyboard or screen quadrant).</summary>
        public static Vector2 Move => new Vector2(Horizontal, Vertical);

        /// <summary>True on the frame the screen/mouse is pressed, or Space/Enter/Up is pressed. Use for jump/shoot/flap.</summary>
        public static bool Pressed =>
            Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);

        /// <summary>True while the screen/mouse or Space is held.</summary>
        public static bool Held => Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);

        /// <summary>True on the frame the press is released.</summary>
        public static bool Released => Input.GetMouseButtonUp(0) || Input.GetKeyUp(KeyCode.Space);

        /// <summary>Quick tap (press + release under 0.25 s without moving). Valid for one frame.</summary>
        public static bool Tap => tapped;

        /// <summary>Swipe direction this frame: (0,1) up, (0,-1) down, (-1,0) left, (1,0) right, (0,0) none. Arrow keys count too.</summary>
        public static Vector2Int Swipe
        {
            get
            {
                if (swipe != Vector2Int.zero) return swipe;
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return Vector2Int.left;
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return Vector2Int.right;
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return Vector2Int.up;
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return Vector2Int.down;
                return Vector2Int.zero;
            }
        }

        /// <summary>Pointer position in screen pixels.</summary>
        public static Vector2 PointerScreen => Input.mousePosition;

        /// <summary>Pointer position on the z = planeZ plane in world space (works for ortho and perspective cameras).</summary>
        public static Vector3 PointerWorld(float planeZ = 0f)
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));
            return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : Vector3.zero;
        }

        /// <summary>Pause / back button (Escape on PC, Back on Android).</summary>
        public static bool Back => Input.GetKeyDown(KeyCode.Escape);

        internal static void Tick()
        {
            swipe = Vector2Int.zero;
            tapped = false;
            if (Input.GetMouseButtonDown(0))
            {
                downPos = Input.mousePosition;
                downTime = Time.unscaledTime;
            }
            if (Input.GetMouseButtonUp(0))
            {
                Vector2 delta = (Vector2)Input.mousePosition - downPos;
                float min = Mathf.Max(40f, Screen.height * 0.06f);
                if (delta.magnitude >= min)
                {
                    swipe = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                        ? (delta.x > 0 ? Vector2Int.right : Vector2Int.left)
                        : (delta.y > 0 ? Vector2Int.up : Vector2Int.down);
                }
                else if (Time.unscaledTime - downTime < 0.25f)
                {
                    tapped = true;
                }
            }
        }
    }
}
