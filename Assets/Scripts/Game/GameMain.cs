using Arc;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// EVE SERVİS — İstanbul'da motor kurye. Restorandan siparişi al, adrese zamanında teslim et.
    /// Phase 1: realistic city + bike + order loop. (Traffic, pedestrians and police come in phase 2.)
    /// </summary>
    public class GameMain : ArcGame
    {
        protected override string HowToPlay =>
            "Restorandan siparişi al, adrese zamanında götür!\nKlavye: W/S gaz-fren, A/D direksiyon  •  Telefon: ekrandaki düğmeler";

        protected override string EndTitle(bool won) => "VARDİYA BİTTİ";

        enum Step { ToRestaurant, ToCustomer }

        City city;
        Moto moto;
        Camera cam;
        Transform beacon, arrow;
        Material beaconPickup, beaconDrop;

        Step step;
        Place restaurant, customer;
        float orderTime, orderTimeLeft, shiftLeft, crashCooldown;
        int delivered, late;
        string toast;
        float toastTime;

        const float ShiftSeconds = 300f;
        const float ArriveRadius = 3.2f;

        // camera smoothing
        Vector3 camVel;
        float orbit;

        // touch controls (screen-space rects, bottom-left origin like Input.mousePosition)
        float tThrottle, tSteer;

        protected override void Setup()
        {
            cam = Camera.main;
            cam.orthographic = false;
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 700f;

            Look.Realistic("belfast_sunset_puresky", sunElevation: 28f, sunAzimuth: -35f, sunIntensity: 1.7f,
                sunColor: new Color(1f, 0.86f, 0.68f), exposure: 0.2f, fogDensity: 0.006f, fogColor: new Color(0.86f, 0.74f, 0.62f));

            city = new City(1453);
            moto = new Moto();

            beaconPickup = Mats.Unlit(new Color(1f, 0.78f, 0.15f));
            beaconDrop = Mats.Unlit(new Color(0.2f, 0.95f, 0.5f));
            beacon = Shapes.Group("Hedef", Vector3.zero);
            Shapes.Cylinder(new Vector3(0f, 6f, 0f), new Vector3(0.35f, 6f, 0.35f), Color.white, lit: false, parent: beacon);
            Shapes.Cylinder(new Vector3(0f, 0.04f, 0f), new Vector3(3.4f, 0.03f, 3.4f), Color.white, lit: false, parent: beacon);
            Shapes.Cube(new Vector3(0f, 2.2f, 0f), Vector3.one * 0.7f, Color.white, lit: false, parent: beacon);

            arrow = Shapes.Group("Ok", Vector3.zero);
            var shaft = Shapes.Cube(new Vector3(0f, 0f, -0.15f), new Vector3(0.18f, 0.06f, 0.6f), Color.white, lit: false, parent: arrow);
            var headL = Shapes.Cube(new Vector3(-0.12f, 0f, 0.2f), new Vector3(0.12f, 0.06f, 0.42f), Color.white, lit: false, parent: arrow);
            headL.transform.localRotation = Quaternion.Euler(0f, 40f, 0f);
            var headR = Shapes.Cube(new Vector3(0.12f, 0f, 0.2f), new Vector3(0.12f, 0.06f, 0.42f), Color.white, lit: false, parent: arrow);
            headR.transform.localRotation = Quaternion.Euler(0f, -40f, 0f);

            Sfx.Music("A3 C4 E4 - D4 C4 B3 - A3 E4 D4 C4 B3 - G3 -", 104f, 0.07f, Wave.Triangle);
        }

        protected override void ResetRound()
        {
            moto.Place(new Vector3(-2.5f, 0f, -150f), 0f);
            shiftLeft = ShiftSeconds;
            delivered = 0;
            late = 0;
            crashCooldown = 0f;
            toast = null;
            NewOrder();
            SnapCamera();
        }

        void NewOrder()
        {
            restaurant = city.Restaurants[Random.Range(0, city.Restaurants.Count)];
            customer = null;
            step = Step.ToRestaurant;
            SetBeacon(restaurant.DoorPos, beaconPickup);
        }

        void SetBeacon(Vector3 pos, Material m)
        {
            beacon.position = new Vector3(pos.x, 0.16f, pos.z);
            foreach (var r in beacon.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
            foreach (var r in arrow.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
        }

        protected override void Tick(float dt)
        {
            shiftLeft -= dt;
            if (shiftLeft <= 0f) { shiftLeft = 0f; GameOver(true); return; }

            ReadControls(out float throttle, out float steer);
            bool crash = moto.Drive(throttle, steer, dt, city);
            crashCooldown -= dt;
            if (crash && crashCooldown <= 0f)
            {
                crashCooldown = 1f;
                Sfx.Hit();
                Juice.Shake(0.35f, 0.3f);
                Score = Mathf.Max(0, Score - 5);
                ShowToast("Dikkat! -5");
            }

            // Order logic
            Vector3 target = step == Step.ToRestaurant ? restaurant.DoorPos : customer.DoorPos;
            if (step == Step.ToCustomer)
            {
                orderTimeLeft -= dt;
            }
            float dist = Flat(target - moto.Root.position).magnitude;
            if (dist < ArriveRadius && moto.Kmh < 20f)
            {
                if (step == Step.ToRestaurant)
                {
                    customer = city.Homes[Random.Range(0, city.Homes.Count)];
                    float route = Flat(customer.DoorPos - restaurant.DoorPos).magnitude;
                    orderTime = 20f + route / 7f;
                    orderTimeLeft = orderTime;
                    step = Step.ToCustomer;
                    SetBeacon(customer.DoorPos, beaconDrop);
                    Sfx.PowerUp();
                    Juice.Pop(moto.Root, 0.15f);
                    ShowToast("Sipariş alındı: " + restaurant.Name);
                }
                else
                {
                    int pts = 50 + Mathf.Max(0, Mathf.RoundToInt(orderTimeLeft * 2f));
                    if (orderTimeLeft < 0f) { pts = 20; late++; }
                    Score += pts;
                    delivered++;
                    Sfx.Coin();
                    Juice.Burst(customer.DoorPos + Vector3.up, Palette.Accent, 30, 5f, 0.25f);
                    Juice.FloatText("+" + pts, customer.DoorPos + Vector3.up * 2.5f, Palette.Accent);
                    ShowToast(orderTimeLeft < 0f ? "Geç kaldın ama teslim edildi +" + pts : "Teslim edildi! Bahşiş dahil +" + pts);
                    NewOrder();
                }
            }

            UpdateBeaconAndArrow(target);
            UpdateCamera(dt, false);
        }

        protected override void IdleTick(float dt)
        {
            if (cam == null || moto == null) return;
            UpdateBeaconAndArrow(beacon.position);
            UpdateCamera(dt, true);
        }

        void ReadControls(out float throttle, out float steer)
        {
            float kt = 0f, ks = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) kt += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Space)) kt -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ks -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ks += 1f;

            ReadTouch();
            throttle = Mathf.Clamp(kt + tThrottle + Inputs.SimMove.y, -1f, 1f);
            steer = Mathf.Clamp(ks + tSteer + Inputs.SimMove.x, -1f, 1f);
        }

        // ---- on-screen buttons (multi-touch) ----
        Rect BtnLeft => new Rect(Hud.U * 30f, Hud.U * 30f, Hud.U * 130f, Hud.U * 130f);
        Rect BtnRight => new Rect(Hud.U * 180f, Hud.U * 30f, Hud.U * 130f, Hud.U * 130f);
        Rect BtnGas => new Rect(Screen.width - Hud.U * 160f, Hud.U * 30f, Hud.U * 130f, Hud.U * 160f);
        Rect BtnBrake => new Rect(Screen.width - Hud.U * 310f, Hud.U * 30f, Hud.U * 130f, Hud.U * 110f);
        bool ShowTouch => Input.touchSupported || Application.isMobilePlatform;

        void ReadTouch()
        {
            tThrottle = 0f;
            tSteer = 0f;
            for (int i = 0; i < Input.touchCount; i++) Press(Input.GetTouch(i).position);
            if (Input.touchCount == 0 && Input.GetMouseButton(0) && ShowTouch) Press(Input.mousePosition);
        }

        void Press(Vector2 p)
        {
            if (BtnLeft.Contains(p)) tSteer -= 1f;
            if (BtnRight.Contains(p)) tSteer += 1f;
            if (BtnGas.Contains(p)) tThrottle += 1f;
            if (BtnBrake.Contains(p)) tThrottle -= 1f;
        }

        void UpdateBeaconAndArrow(Vector3 target)
        {
            beacon.Rotate(0f, 60f * Time.deltaTime, 0f);
            Vector3 head = moto.Root.position + Vector3.up * 2.3f;
            arrow.position = head;
            Vector3 dir = Flat(target - moto.Root.position);
            if (dir.sqrMagnitude > 0.01f) arrow.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            arrow.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 6f) * 0.08f);
        }

        void SnapCamera()
        {
            if (cam == null) return;
            cam.transform.position = moto.Root.position - moto.Forward * 6.5f + Vector3.up * 2.6f;
            cam.transform.LookAt(moto.Root.position + Vector3.up * 1.2f + moto.Forward * 3f);
        }

        void UpdateCamera(float dt, bool idle)
        {
            Vector3 focus = moto.Root.position + Vector3.up * 1.2f;
            Vector3 desired;
            if (idle)
            {
                orbit += dt * 12f;
                Vector3 off = Quaternion.Euler(0f, orbit, 0f) * new Vector3(0f, 0f, -7.5f);
                desired = focus + off + Vector3.up * 1.6f;
                cam.transform.position = Vector3.SmoothDamp(cam.transform.position, desired, ref camVel, 0.4f);
                cam.transform.LookAt(focus);
                return;
            }
            float back = 6.2f + moto.Kmh * 0.03f;
            desired = moto.Root.position - moto.Forward * back + Vector3.up * (2.4f + moto.Kmh * 0.008f);
            desired = KeepCameraOnStreet(moto.Root.position, desired);
            cam.transform.position = Vector3.SmoothDamp(cam.transform.position, desired, ref camVel, 0.18f);
            cam.transform.position = KeepCameraOnStreet(moto.Root.position, cam.transform.position);
            cam.transform.LookAt(focus + moto.Forward * 3.5f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f + moto.Kmh * 0.18f, dt * 3f);
        }

        /// <summary>Pulls the camera toward the bike (and up) until it is above a street, so it never enters a building.</summary>
        Vector3 KeepCameraOnStreet(Vector3 bike, Vector3 camPos)
        {
            if (city.IsDrivable(camPos)) return camPos;
            for (float t = 0.9f; t > 0.05f; t -= 0.1f)
            {
                Vector3 p = Vector3.Lerp(bike, camPos, t);
                if (city.IsDrivable(p)) return new Vector3(p.x, camPos.y + (1f - t) * 2.5f, p.z);
            }
            return new Vector3(bike.x, camPos.y + 2.5f, bike.z) - moto.Forward * 0.5f;
        }

        void ShowToast(string text)
        {
            toast = text;
            toastTime = 2.5f;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        protected override void DrawHud()
        {
            base.DrawHud();
            int m = Mathf.FloorToInt(shiftLeft / 60f), s = Mathf.FloorToInt(shiftLeft % 60f);
            Hud.Text("Vardiya: " + m + ":" + s.ToString("00") + "   Teslimat: " + delivered, 24, 112, 0.75f);

            // Order panel (top center)
            string line1, line2;
            Vector3 target = step == Step.ToRestaurant ? restaurant.DoorPos : customer.DoorPos;
            int dist = Mathf.RoundToInt(Flat(target - moto.Root.position).magnitude);
            if (step == Step.ToRestaurant)
            {
                line1 = "SİPARİŞİ AL: " + restaurant.Name;
                line2 = "Restorana " + dist + " m";
            }
            else
            {
                line1 = "TESLİM ET: " + customer.Name + " (" + customer.District + ")";
                line2 = dist + " m  •  Kalan süre: " + Mathf.CeilToInt(Mathf.Max(0f, orderTimeLeft)) + " sn";
            }
            Hud.Center(line1, -Screen.height * 0.5f / Hud.U + 60f, 0.8f, step == Step.ToRestaurant ? Palette.Accent : Palette.Good);
            Hud.Center(line2, -Screen.height * 0.5f / Hud.U + 100f, 0.7f);
            if (step == Step.ToCustomer)
            {
                float k = Mathf.Clamp01(orderTimeLeft / Mathf.Max(1f, orderTime));
                float barW = 360f;
                float x = (Screen.width / Hud.U - barW) * 0.5f;
                Hud.Bar(x, 128f, barW, 10f, k, k > 0.3f ? Palette.Good : Palette.Danger);
            }

            // Speedometer
            Hud.TextRight(Mathf.RoundToInt(moto.Kmh) + " km/sa", 30, Screen.height / Hud.U - (ShowTouch ? 230f : 80f), 1.3f);

            if (toast != null && toastTime > 0f)
            {
                toastTime -= Time.unscaledDeltaTime;
                Hud.Center(toast, 140f, 0.85f, Palette.Accent);
            }

            if (ShowTouch) DrawTouchButtons();
        }

        void DrawTouchButtons()
        {
            Btn(BtnLeft, "◀", tSteer < 0f);
            Btn(BtnRight, "▶", tSteer > 0f);
            Btn(BtnGas, "GAZ", tThrottle > 0f);
            Btn(BtnBrake, "FREN", tThrottle < 0f);
        }

        // Converts a bottom-left screen rect to GUI coordinates (top-left) and draws it.
        static void Btn(Rect r, string label, bool active)
        {
            var g = new Rect(r.x, Screen.height - r.y - r.height, r.width, r.height);
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, active ? 0.45f : 0.22f);
            GUI.DrawTexture(g, Texture2D.whiteTexture);
            GUI.color = old;
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(34 * Hud.U), fontStyle = FontStyle.Bold };
            st.normal.textColor = Color.white;
            GUI.Label(g, label, st);
        }
    }
}
