using System.Collections.Generic;
using Arc;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// EVE SERVİS — gerçek İstanbul'da (Google fotogerçekçi 3D) motor kurye.
    /// Motorla restorana git, in, yürüyerek gir ve siparişi al; müşterinin kapısına götür. GPS gerçek sokakları izler.
    /// </summary>
    public class GameMain : ArcGame
    {
        protected override string HowToPlay =>
            "Gerçek Karagümrük sokaklarında kuryesin. Restorana gir, siparişi al, kapıya teslim et.\n" +
            "W A S D sür/yürü • Fare kamera • E bin/in/gir • Shift koş • Boşluk el freni • R sokağa dön • Oyun kolu desteklenir";

        protected override string EndTitle(bool won) => "VARDİYA BİTTİ";

        enum Step { ToRestaurant, ToCustomer }

        class Place
        {
            public string Name, Address;
            public Vector3 Door;      // on the sidewalk, snapped to the ground when close
            public int Node;
            public bool Snapped;
        }

        EveWorld world;
        Roads roads;
        Controls input;
        CamRig rig;
        Bike bike;
        Courier courier;
        Camera cam;

        readonly List<Place> restaurants = new List<Place>();
        Place restaurant, customer;
        Step step;
        Transform beacon;
        LineRenderer gps;
        Material pickupMat, dropMat;
        readonly System.Random rng = new System.Random(1453);

        bool ready;
        float readyTimer, shiftLeft, orderTime, orderLeft, gpsTimer, fade, toastTime;
        int delivered;
        string toast;
        Vector3 spawn;
        float streetY;           // last known street height near the player

        const float ShiftSeconds = 480f;
        const float DoorRadius = 1.8f;

        static readonly string[] RestaurantNames =
        {
            "Karagümrük Köftecisi", "Balat Pide Salonu", "Dürümcü Hasan Usta", "Fatih Lahmacun", "Çorbacı Nuri",
            "Edirnekapı Kebap", "Haliç Balık Ekmek", "Fener Börekçisi", "Kariye Mantı Evi", "Atikali Ciğercisi",
            "Çarşamba Tantuni", "Draman Pilavcısı", "Yavuz Selim Kokoreç", "Hırka-i Şerif Simit Fırını"
        };

        // ---------------- setup ----------------

        protected override void Setup()
        {
            cam = Camera.main;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 6000f;
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.LinuxPlayer || Application.isEditor)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
            }

            Look.Realistic("kloppenheim_06_puresky", sunElevation: 48f, sunAzimuth: 150f, sunIntensity: 2.2f,
                sunColor: new Color(1f, 0.96f, 0.9f), exposure: 0f, fogDensity: 0.0009f, fogColor: new Color(0.74f, 0.80f, 0.88f));

            world = new EveWorld();
            roads = new Roads();
            input = new Controls();
            rig = new CamRig(cam);
            ArcShot.ReadyCheck = () => ready;

            pickupMat = Mats.Unlit(new Color(1f, 0.75f, 0.1f));
            dropMat = Mats.Unlit(new Color(0.15f, 0.95f, 0.45f));
            BuildBeacon();
            BuildGps();
            BuildRestaurants();

            // Start on a real Karagümrük street near the origin
            int startNode = roads.Loaded ? roads.Nearest(Vector3.zero) : -1;
            spawn = startNode >= 0 ? roads.Pos(startNode) : Vector3.zero;
            float yaw = startNode >= 0 ? StreetHeading(startNode) : 0f;
            bike = Bike.Create(spawn + Vector3.up * 120f, yaw);
            bike.Body.isKinematic = true;   // wait until the city under us has loaded
            courier = Courier.Create(spawn + Vector3.up * 120f, yaw);
            courier.Mount(bike);
            bike.Ridden = true;

            Sfx.Music("D4 F4 A4 - G4 F4 E4 - D4 A4 G4 F4 E4 - C4 -", 96f, 0.05f, Wave.Triangle);
        }

        void BuildBeacon()
        {
            beacon = Shapes.Group("Hedef", Vector3.zero);
            Shapes.Cylinder(new Vector3(0f, 25f, 0f), new Vector3(0.6f, 25f, 0.6f), Color.white, lit: false, parent: beacon);
            Shapes.Cylinder(new Vector3(0f, 0.05f, 0f), new Vector3(DoorRadius * 2f, 0.04f, DoorRadius * 2f), Color.white, lit: false, parent: beacon);
            var icon = Shapes.Cube(new Vector3(0f, 2.4f, 0f), Vector3.one * 0.6f, Color.white, lit: false, parent: beacon);
            icon.name = "Ikon";
            Bike.SetLayerRecursive(beacon.gameObject, EveWorld.PlayerLayer);
        }

        void BuildGps()
        {
            var go = new GameObject("GPS");
            go.layer = EveWorld.PlayerLayer;
            gps = go.AddComponent<LineRenderer>();
            gps.sharedMaterial = Mats.Unlit(new Color(0.25f, 0.75f, 1f));
            gps.widthMultiplier = 0.9f;
            gps.numCornerVertices = 3;
            gps.numCapVertices = 2;
            gps.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat on the street
            gps.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gps.positionCount = 0;
        }

        void BuildRestaurants()
        {
            int n = 0;
            var used = new HashSet<int>();
            if (roads.Loaded)
            {
                foreach (var p in roads.Places)
                {
                    if (p.kind == "cafe") continue;
                    var poi = new Vector3(p.x, 0f, p.z);
                    int node = roads.Nearest(poi);
                    if (node < 0 || used.Contains(node)) continue;
                    if (Vector3.Distance(roads.Pos(node), poi) > 40f) continue;
                    used.Add(node);
                    Vector3 door = Vector3.MoveTowards(roads.Pos(node), poi, 3f);   // sidewalk in front of the shop
                    string street = roads.StreetName(node);
                    restaurants.Add(new Place
                    {
                        Name = RestaurantNames[n % RestaurantNames.Length],
                        Address = string.IsNullOrEmpty(street) ? "Karagümrük" : street,
                        Door = door, Node = node
                    });
                    n++;
                    if (restaurants.Count >= 40) break;
                }
            }
            // Not enough real food places nearby: use named streets.
            while (restaurants.Count < 8 && roads.Loaded)
            {
                int node = roads.RandomStreetNode(Vector3.zero, 50f, 900f, rng);
                restaurants.Add(new Place { Name = RestaurantNames[n++ % RestaurantNames.Length], Address = roads.StreetName(node), Door = roads.Pos(node), Node = node });
            }
            if (restaurants.Count == 0)
                restaurants.Add(new Place { Name = RestaurantNames[0], Address = "Karagümrük", Door = new Vector3(40f, 0f, 40f), Node = -1 });
        }

        float StreetHeading(int node)
        {
            if (node < 0 || roads.Neighbors[node].Count == 0) return 0f;
            Vector3 d = roads.Pos(roads.Neighbors[node][0]) - roads.Pos(node);
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // ---------------- round ----------------

        protected override void ResetRound()
        {
            shiftLeft = ShiftSeconds;
            delivered = 0;
            toast = null;
            if (!courier.Riding) { courier.Mount(bike); bike.Ridden = true; }
            if (ready) PlaceOnStreet(spawn, bike.transform.eulerAngles.y);
            NewOrder();
        }

        void NewOrder()
        {
            Vector3 from = bike != null ? bike.transform.position : Vector3.zero;
            // Prefer a restaurant 150-900 m away
            Place pick = null;
            for (int i = 0; i < 30 && pick == null; i++)
            {
                var r = restaurants[rng.Next(restaurants.Count)];
                float d = Flat(r.Door - from).magnitude;
                if (d > 120f && d < 900f) pick = r;
            }
            restaurant = pick ?? restaurants[rng.Next(restaurants.Count)];
            customer = null;
            step = Step.ToRestaurant;
            SetBeacon(restaurant, pickupMat);
            gpsTimer = 0f;
        }

        Place NewCustomer()
        {
            int node = roads.Loaded ? roads.RandomStreetNode(restaurant.Door, 200f, 1100f, rng) : -1;
            Vector3 p = node >= 0 ? roads.Pos(node) : restaurant.Door + new Vector3(150f, 0f, 120f);
            // Door: a few meters to the side of the street center (the sidewalk)
            if (node >= 0 && roads.Neighbors[node].Count > 0)
            {
                Vector3 along = (roads.Pos(roads.Neighbors[node][0]) - p).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, along) * (rng.Next(2) == 0 ? 1f : -1f);
                p += side * 3.5f;
            }
            string street = node >= 0 ? roads.StreetName(node) : "";
            if (string.IsNullOrEmpty(street)) street = "Sokak";
            return new Place { Name = street + " No: " + rng.Next(1, 90), Address = street, Door = p, Node = node };
        }

        void SetBeacon(Place p, Material m)
        {
            p.Snapped = false;
            beacon.position = new Vector3(p.Door.x, streetY, p.Door.z);
            foreach (var r in beacon.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
            gps.sharedMaterial = m == pickupMat ? Mats.Unlit(new Color(1f, 0.8f, 0.2f)) : Mats.Unlit(new Color(0.25f, 1f, 0.55f));
        }

        void PlaceOnStreet(Vector3 pos, float yaw)
        {
            EveWorld.Ground(pos, out var g);
            streetY = g.y;
            bike.Body.isKinematic = false;
            bike.Reset(g, yaw);
            if (!courier.Riding) courier.Teleport(g + bike.transform.right * -1f + Vector3.up * 0.1f, yaw);
            rig.Target = courier.Riding ? bike.transform : courier.transform;
            rig.VehicleMode = courier.Riding;
            rig.SnapBehind();
        }

        // ---------------- frame ----------------

        protected override void Update()
        {
            base.Update();
            if (bike == null) return;

            // World loading: keep everything frozen until the street under the spawn exists.
            if (!ready)
            {
                readyTimer += Time.unscaledDeltaTime;
                bool loadedEnough = !world.HasToken || world.Progress >= 97f || readyTimer > 45f;
                if (loadedEnough && readyTimer > 3f && EveWorld.Ground(spawn, out var g))
                {
                    ready = true;
                    spawn = g;
                    PlaceOnStreet(spawn, bike.transform.eulerAngles.y);
                }
                else if (readyTimer > 70f)
                {
                    ready = true;   // give up waiting: drop from above
                    PlaceOnStreet(spawn + Vector3.up * 40f, bike.transform.eulerAngles.y);
                }
            }

            bool playing = Current == State.Playing;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;

            if (fade > 0f) fade = Mathf.Max(0f, fade - Time.unscaledDeltaTime * 1.5f);
        }

        protected override void IdleTick(float dt)
        {
            if (rig == null) return;
            if (!ready || Current == State.Menu)
            {
                rig.Orbit(spawn + Vector3.up * 5f, 70f, 40f, dt);
                return;
            }
            rig.Tick(Vector2.zero, false, 0f, false, dt);
        }

        protected override void Tick(float dt)
        {
            if (!ready)
            {
                rig.Orbit(spawn + Vector3.up * 5f, 70f, 40f, dt);
                return;
            }

            shiftLeft -= dt;
            if (shiftLeft <= 0f) { GameOver(true); return; }

            Vector2 mv = input.Move;
            Vector3 playerPos = courier.Riding ? bike.transform.position : courier.transform.position;
            streetY = Mathf.Lerp(streetY, playerPos.y, dt * 2f);

            if (courier.Riding)
            {
                bike.SetInput(input.Throttle, input.Brake, mv.x, input.Handbrake);
                courier.TickRiding(dt);
                if (input.InteractPressed && bike.Kmh < 12f) GetOff();
                if (input.HornPressed) bike.Horn();
            }
            else
            {
                bike.SetInput(0f, 0f, 0f, true);
                courier.Walk(mv, input.Sprint, cam.transform, dt);
                if (input.InteractPressed)
                {
                    if (NearDoor()) EnterDoor();
                    else if (Vector3.Distance(courier.transform.position, bike.transform.position) < 2.6f) GetOn();
                }
            }

            if (input.ResetPressed) BackToStreet();

            // order timer + door height refinement
            if (step == Step.ToCustomer) orderLeft -= dt;
            var target = step == Step.ToRestaurant ? restaurant : customer;
            if (!target.Snapped && Flat(target.Door - playerPos).magnitude < 150f)
            {
                if (EveWorld.StreetGround(target.Door, streetY, out var g))
                {
                    target.Door = g;
                    target.Snapped = true;
                    beacon.position = g;
                }
            }
            beacon.Find("Ikon").Rotate(0f, 90f * dt, 0f, Space.World);

            gpsTimer -= dt;
            if (gpsTimer <= 0f) { gpsTimer = 1.2f; UpdateGps(playerPos, target); }

            rig.Target = courier.Riding ? bike.transform : courier.transform;
            rig.VehicleMode = courier.Riding;
            rig.Height = courier.Riding ? 1.5f : 1.55f;
            rig.Distance = courier.Riding ? 5.2f : 3.6f;
            rig.Tick(input.Look, input.LookIsMouse, courier.Riding ? Mathf.Abs(bike.Speed) : courier.Speed, input.RecenterPressed, dt);
        }

        void GetOn()
        {
            courier.Mount(bike);
            bike.Ridden = true;
            Sfx.Click();
            rig.SnapBehind();
        }

        void GetOff()
        {
            bike.Ridden = false;
            courier.Dismount(bike);
            Sfx.Click();
        }

        void BackToStreet()
        {
            Vector3 p = courier.Riding ? bike.transform.position : courier.transform.position;
            int node = roads.Loaded ? roads.Nearest(p) : -1;
            Vector3 dst = node >= 0 ? roads.Pos(node) : spawn;
            if (!courier.Riding) GetOn();
            PlaceOnStreet(dst, node >= 0 ? StreetHeading(node) : bike.transform.eulerAngles.y);
            ShowToast("Sokağa geri dönüldü");
        }

        bool NearDoor()
        {
            var t = step == Step.ToRestaurant ? restaurant : customer;
            return Flat(t.Door - courier.transform.position).magnitude < DoorRadius + 0.6f;
        }

        void EnterDoor()
        {
            fade = 1f;
            if (step == Step.ToRestaurant)
            {
                customer = NewCustomer();
                float route = Flat(customer.Door - restaurant.Door).magnitude;
                orderTime = 45f + route / 6f;
                orderLeft = orderTime;
                step = Step.ToCustomer;
                SetBeacon(customer, dropMat);
                gpsTimer = 0f;
                Sfx.PowerUp();
                ShowToast(restaurant.Name + ": siparişin hazır, müşteri bekliyor!");
            }
            else
            {
                int pts = 60 + Mathf.Max(0, Mathf.RoundToInt(orderLeft * 1.5f));
                if (orderLeft < 0f) pts = 25;
                Score += pts;
                delivered++;
                Sfx.Coin();
                Juice.FloatText("+" + pts, customer.Door + Vector3.up * 2.2f, Palette.Accent);
                ShowToast(orderLeft < 0f ? "Geç kaldın ama müşteri kabul etti +" + pts : "Teslim edildi! Bahşiş dahil +" + pts);
                NewOrder();
            }
        }

        void UpdateGps(Vector3 from, Place target)
        {
            if (!roads.Loaded) { gps.positionCount = 0; return; }
            int a = roads.Nearest(from), b = target.Node >= 0 ? target.Node : roads.Nearest(target.Door);
            var route = roads.Route(a, b);
            var pts = new List<Vector3>(route.Count + 2) { from };
            float y = streetY;
            foreach (int node in route)
            {
                Vector3 p = roads.Pos(node);
                if (EveWorld.StreetGround(p, y, out var g)) y = g.y;
                pts.Add(new Vector3(p.x, y + 0.35f, p.z));
            }
            pts.Add(new Vector3(target.Door.x, y + 0.35f, target.Door.z));
            pts[0] = new Vector3(from.x, from.y + 0.35f, from.z);
            gps.positionCount = pts.Count;
            gps.SetPositions(pts.ToArray());
        }

        void ShowToast(string text)
        {
            toast = text;
            toastTime = 3f;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ---------------- HUD ----------------

        protected override void DrawHud()
        {
            if (!ready)
            {
                DrawLoading();
                return;
            }
            base.DrawHud();
            int m = Mathf.FloorToInt(shiftLeft / 60f), s = Mathf.FloorToInt(shiftLeft % 60f);
            Hud.Text("Vardiya " + m + ":" + s.ToString("00") + "   Teslimat: " + delivered, 24, 112, 0.7f);

            var t = step == Step.ToRestaurant ? restaurant : customer;
            Vector3 p = courier.Riding ? bike.transform.position : courier.transform.position;
            int dist = Mathf.RoundToInt(Flat(t.Door - p).magnitude);
            float top = -Screen.height * 0.5f / Hud.U;
            if (step == Step.ToRestaurant)
            {
                Hud.Center("SİPARİŞİ AL: " + restaurant.Name, top + 50f, 0.85f, Palette.Accent);
                Hud.Center(restaurant.Address + "  •  " + dist + " m", top + 90f, 0.65f);
            }
            else
            {
                Hud.Center("TESLİM ET: " + customer.Name, top + 50f, 0.85f, Palette.Good);
                Hud.Center(dist + " m  •  Kalan süre " + Mathf.CeilToInt(Mathf.Max(0f, orderLeft)) + " sn", top + 90f, 0.65f);
                float k = Mathf.Clamp01(orderLeft / Mathf.Max(1f, orderTime));
                float w = 380f;
                Hud.Bar((Screen.width / Hud.U - w) * 0.5f, 118f, w, 8f, k, k > 0.3f ? Palette.Good : Palette.Danger);
            }

            // context prompt
            string prompt = null;
            if (courier.Riding)
            {
                if (dist < 25f) prompt = bike.Kmh < 12f ? "[E] Motordan in ve yürüyerek kapıya git" : "Yavaşla ve motordan in";
            }
            else
            {
                if (NearDoor()) prompt = step == Step.ToRestaurant ? "[E] Restorana gir" : "[E] Kapıyı çal ve teslim et";
                else if (Vector3.Distance(courier.transform.position, bike.transform.position) < 2.6f) prompt = "[E] Motora bin";
            }
            if (prompt != null) Hud.Center(prompt, 170f, 0.8f, Color.white);

            if (courier.Riding) Hud.TextRight(Mathf.RoundToInt(bike.Kmh) + " km/sa", 30, Screen.height / Hud.U - 90f, 1.4f);

            if (toast != null && toastTime > 0f)
            {
                toastTime -= Time.unscaledDeltaTime;
                Hud.Center(toast, 120f, 0.8f, Palette.Accent);
            }

            if (fade > 0f) Hud.Overlay(new Color(0f, 0f, 0f, Mathf.Clamp01(fade)));
            Hud.Text("Harita verisi © OpenStreetMap katkıcıları", 24, Screen.height / Hud.U - 40f, 0.45f, new Color(1f, 1f, 1f, 0.6f));
        }

        void DrawLoading()
        {
            Hud.Overlay(new Color(0f, 0f, 0f, 0.35f));
            if (!world.HasToken)
            {
                Hud.Center("Şehir anahtarı (CESIUM_ION_TOKEN) eklenmemiş", 0f, 0.9f, Palette.Danger);
                return;
            }
            Hud.Center("İstanbul yükleniyor... %" + Mathf.RoundToInt(world.Progress), 160f, 0.9f);
            Hud.Bar((Screen.width / Hud.U - 400f) * 0.5f, Screen.height / Hud.U * 0.5f + 200f, 400f, 10f, world.Progress / 100f, Palette.Accent);
        }

        protected override void OnGUI()
        {
            // Menu over a still-loading city: show progress under the menu too.
            base.OnGUI();
            if (Current == State.Menu && !ready && world != null && world.HasToken)
                Hud.Center("Şehir yükleniyor %" + Mathf.RoundToInt(world.Progress), 250f, 0.6f, Palette.Accent);
        }
    }
}
