using System.Collections.Generic;
using Arc;
using UnityEngine;

namespace Game
{
    /// <summary>A place you can pick up from (restaurant) or deliver to (home). DoorPos is on the sidewalk / street.</summary>
    public class Place
    {
        public string Name;
        public string District;
        public Vector3 DoorPos;
        public bool Restaurant;
    }

    /// <summary>
    /// Builds a small fictional Istanbul neighbourhood: one main avenue (asphalt + sidewalks) and two
    /// cobblestone side streets, lined with pastel plaster / brick buildings, bay windows (cumba),
    /// restaurants with awnings and signs, street lamps, parked cars and road barriers.
    /// All sizes in meters. Main avenue runs along Z.
    /// </summary>
    public class City
    {
        public readonly List<Place> Restaurants = new List<Place>();
        public readonly List<Place> Homes = new List<Place>();
        readonly List<Rect> drivable = new List<Rect>(); // XZ rectangles where the bike may be

        /// <summary>Round obstacles on the street (x, z = center, y = radius): parked cars, lamps, tables...</summary>
        public readonly List<Vector3> Obstacles = new List<Vector3>();

        public const float RoadHalf = 5f;      // asphalt half width
        public const float WalkHalf = 8f;      // road + sidewalk half width
        public const float MainLength = 180f;  // avenue goes from -MainLength to +MainLength
        public const float AlleyHalf = 3.5f;
        public const float AlleyLength = 85f;
        public static readonly float[] AlleyZ = { -60f, 70f };
        const float AvenueDepth = 15f;

        Transform root;
        Font font;
        Material textMat;

        static readonly string[] Facades = { "painted_plaster_wall", "white_plaster_02", "red_brick_03", "rough_plasterbrick_05", "cracked_concrete_wall" };
        static readonly string[] Pastels = { "#F2D7B6", "#E8B4A0", "#F4E3A1", "#B7D3C9", "#E6C9D8", "#D9CBB3", "#F7F1E5", "#C9D6E8" };
        static readonly string[] RestaurantNames = { "Karagümrük Köftecisi", "Balat Pide", "Dürümcü Hasan", "Fatih Lahmacun", "Çorbacı Nuri", "Edirnekapı Kebap", "Haliç Balık Ekmek", "Fener Börekçisi" };
        static readonly string[] StreetNames = { "Kale Sk.", "Dervişali Sk.", "Kırkçeşme Sk.", "Hocaçakır Sk.", "Fevzi Paşa Cd.", "Kariye Sk.", "Avcıbey Sk." };
        static readonly string[] Districts = { "Karagümrük", "Fatih", "Balat", "Edirnekapı" };

        public City(int seed)
        {
            Random.InitState(seed);
            root = new GameObject("City").transform;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textBase = Resources.Load<Material>("ArcMaterials/Text");
            textMat = textBase != null ? new Material(textBase) { mainTexture = font.material.mainTexture } : font.material;

            BuildGround();
            BuildMainAvenue();
            for (int i = 0; i < AlleyZ.Length; i++) BuildAlley(AlleyZ[i], i);
            BuildBlocksAlongAvenue();
            for (int i = 0; i < AlleyZ.Length; i++) BuildBlocksAlongAlley(AlleyZ[i]);
            BuildEndCaps();
            BuildStreetProps();
        }

        // ---------------- queries ----------------

        /// <summary>True if a point (XZ) is on a street or sidewalk.</summary>
        public bool IsDrivable(Vector3 p)
        {
            var v = new Vector2(p.x, p.z);
            for (int i = 0; i < drivable.Count; i++) if (drivable[i].Contains(v)) return true;
            return false;
        }

        /// <summary>True if the point and a square of +-margin around it are all on the street (keeps the camera out of bay windows / awnings).</summary>
        public bool IsClear(Vector3 p, float margin)
        {
            return IsDrivable(p) && IsDrivable(p + new Vector3(margin, 0f, 0f)) && IsDrivable(p - new Vector3(margin, 0f, 0f))
                && IsDrivable(p + new Vector3(0f, 0f, margin)) && IsDrivable(p - new Vector3(0f, 0f, margin));
        }

        /// <summary>Returns true and the push-out direction if a circle (XZ) at p with radius r hits an obstacle.</summary>
        public bool HitsObstacle(Vector3 p, float r, out Vector3 push)
        {
            push = Vector3.zero;
            for (int i = 0; i < Obstacles.Count; i++)
            {
                var o = Obstacles[i];
                float dx = p.x - o.x, dz = p.z - o.z;
                float min = r + o.y;
                float d2 = dx * dx + dz * dz;
                if (d2 < min * min)
                {
                    float d = Mathf.Sqrt(Mathf.Max(d2, 0.0001f));
                    push = new Vector3(dx / d, 0f, dz / d) * (min - d);
                    return true;
                }
            }
            return false;
        }

        void AddObstacle(Vector3 p, float radius) => Obstacles.Add(new Vector3(p.x, radius, p.z));

        // ---------------- ground & streets ----------------

        void BuildGround()
        {
            // Large concrete base under everything (visible between buildings / far away).
            var g = Shapes.Cube(new Vector3(0f, -0.06f, 0f), new Vector3(500f, 0.1f, 500f), Color.white, parent: root);
            g.GetComponent<Renderer>().sharedMaterial = Mats.Pbr("cracked_concrete_wall", new Vector2(120f, 120f), 0.1f, Palette.Hex("#8A8780"));
        }

        void BuildMainAvenue()
        {
            float len = MainLength * 2f;
            var road = Shapes.Cube(new Vector3(0f, -0.02f, 0f), new Vector3(RoadHalf * 2f, 0.04f, len), Color.white, parent: root);
            road.GetComponent<Renderer>().sharedMaterial = Mats.PbrSized("asphalt_02", new Vector2(RoadHalf * 2f, len), 5f, 0.25f);

            // Lane markings (dashed center line)
            for (float z = -MainLength + 2f; z < MainLength; z += 6f)
            {
                if (NearAlley(z, 1f)) continue;
                Shapes.Cube(new Vector3(0f, 0.005f, z), new Vector3(0.15f, 0.01f, 3f), Palette.Hex("#E9E4D4"), parent: root);
            }

            // Sidewalks with curbs
            foreach (float side in new[] { -1f, 1f })
            {
                float cx = side * (RoadHalf + (WalkHalf - RoadHalf) * 0.5f);
                var walk = Shapes.Cube(new Vector3(cx, 0.075f, 0f), new Vector3(WalkHalf - RoadHalf, 0.15f, len), Color.white, parent: root);
                walk.GetComponent<Renderer>().sharedMaterial = Mats.PbrSized("pavement_03", new Vector2(WalkHalf - RoadHalf, len), 2.5f, 0.15f);
                Shapes.Cube(new Vector3(side * (RoadHalf + 0.08f), 0.08f, 0f), new Vector3(0.18f, 0.17f, len), Palette.Hex("#BDB8AE"), parent: root);
            }
            drivable.Add(new Rect(-WalkHalf, -MainLength, WalkHalf * 2f, len));

            // Closed ends: concrete barriers
            foreach (float zEnd in new[] { -MainLength + 1f, MainLength - 1f })
                for (float x = -RoadHalf + 1f; x <= RoadHalf - 1f; x += 2.2f)
                    Barrier(new Vector3(x, 0f, zEnd), 0f);
        }

        void BuildAlley(float z, int index)
        {
            float len = AlleyLength * 2f;
            foreach (float side in new[] { -1f, 1f })
            {
                // cobblestone from the sidewalk edge outward
                float w = AlleyLength - WalkHalf;
                var c = Shapes.Cube(new Vector3(side * (WalkHalf + w * 0.5f), 0.0f, z), new Vector3(w, 0.06f, AlleyHalf * 2f), Color.white, parent: root);
                c.GetComponent<Renderer>().sharedMaterial = Mats.PbrSized("cobblestone_floor_04", new Vector2(w, AlleyHalf * 2f), 2.5f, 0.3f);
                // barrier at the far end
                for (float zz = z - AlleyHalf + 1f; zz <= z + AlleyHalf - 1f; zz += 2.2f)
                    Barrier(new Vector3(side * (AlleyLength - 1f), 0f, zz), 90f);
            }
            drivable.Add(new Rect(-AlleyLength, z - AlleyHalf, len, AlleyHalf * 2f));
        }

        static bool NearAlley(float z, float margin)
        {
            for (int i = 0; i < AlleyZ.Length; i++)
                if (Mathf.Abs(z - AlleyZ[i]) < AlleyHalf + margin) return true;
            return false;
        }

        // ---------------- buildings ----------------

        void BuildBlocksAlongAvenue()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                float z = -MainLength + 4f;
                while (z < MainLength - 6f)
                {
                    float w = Random.Range(8f, 13f);
                    if (NearAlley(z, 0.5f) || NearAlley(z + w, 0.5f) || (z < AlleyZ[0] && z + w > AlleyZ[0]) || (z < AlleyZ[1] && z + w > AlleyZ[1]))
                    {
                        z += 1f;
                        continue;
                    }
                    // Building faces the avenue: front at x = side * WalkHalf, facing -side along X.
                    Vector3 front = new Vector3(side * WalkHalf, 0f, z + w * 0.5f);
                    float yaw = side > 0 ? -90f : 90f;
                    MakeBuilding(front, yaw, w, AvenueDepth, Random.Range(3, 7), "Fevzi Paşa Cd.");
                    z += w + Random.Range(0f, 0.4f);
                }
            }
        }

        void BuildBlocksAlongAlley(float alleyZ)
        {
            foreach (float zSide in new[] { -1f, 1f })
            {
                foreach (float xSide in new[] { -1f, 1f })
                {
                    float x = WalkHalf + AvenueDepth; // right behind the avenue buildings
                    string street = StreetNames[Random.Range(0, StreetNames.Length)];
                    while (x < AlleyLength - 6f)
                    {
                        float w = Random.Range(7f, 11f);
                        Vector3 front = new Vector3(xSide * (x + w * 0.5f), 0f, alleyZ + zSide * AlleyHalf);
                        float yaw = zSide > 0 ? 180f : 0f;
                        MakeBuilding(front, yaw, w, Random.Range(10f, 13f), Random.Range(2, 5), street);
                        x += w + Random.Range(0f, 0.3f);
                    }
                }
            }
        }

        /// <param name="front">Center of the facade at street level.</param>
        /// <param name="yaw">Rotation so that the building's local +Z points to the street.</param>
        void MakeBuilding(Vector3 front, float yaw, float width, float depth, int floors, string street)
        {
            const float FloorH = 3.1f;
            float h = floors * FloorH + 0.6f;
            var b = new GameObject("Bina").transform;
            b.SetParent(root, false);
            b.position = front;
            b.rotation = Quaternion.Euler(0f, yaw, 0f);

            string facade = Facades[Random.Range(0, Facades.Length)];
            bool brick = facade.Contains("brick") && !facade.Contains("plaster");
            Color tint = brick ? Color.white : Palette.Hex(Pastels[Random.Range(0, Pastels.Length)]);
            Material wall = Mats.PbrSized(facade, new Vector2(width, h), brick ? 2.2f : 3.5f, 0.12f, tint);

            // Body
            Box(b, new Vector3(0f, h * 0.5f, -depth * 0.5f), new Vector3(width, h, depth), wall);
            // Ground floor band (darker stone)
            Box(b, new Vector3(0f, 1.6f, 0.03f), new Vector3(width, 3.2f, 0.12f), Mats.PbrSized("rough_plasterbrick_05", new Vector2(width, 3.2f), 2f, 0.1f, Palette.Hex("#E3DCCF")));
            // Cornice + roof edge
            Box(b, new Vector3(0f, h - 0.15f, 0.15f), new Vector3(width + 0.3f, 0.35f, 0.5f), Mats.Lit(Palette.Darker(tint, 0.25f)));
            Box(b, new Vector3(0f, floors * FloorH * 0.5f + 1.5f, 0.06f), new Vector3(width, 0.18f, 0.25f), Mats.Lit(Palette.Lighter(tint, 0.3f)));

            // Windows on upper floors
            Material glass = Mats.Glass(Palette.Hex("#40525E"));
            Material frame = Mats.Lit(Palette.Hex("#EDE8DF"));
            int cols = Mathf.Max(1, Mathf.FloorToInt(width / 2.6f));
            float spacing = width / cols;
            bool cumba = floors >= 3 && Random.value < 0.45f;
            for (int f = 1; f < floors; f++)
            {
                float y = f * FloorH + 1.5f;
                for (int c = 0; c < cols; c++)
                {
                    float x = -width * 0.5f + spacing * (c + 0.5f);
                    if (cumba && f >= 1 && f <= 2 && Mathf.Abs(x) < width * 0.22f) continue;
                    Box(b, new Vector3(x, y, 0.02f), new Vector3(1.25f, 1.7f, 0.06f), frame);
                    Box(b, new Vector3(x, y, 0.06f), new Vector3(1.05f, 1.5f, 0.05f), glass);
                    Box(b, new Vector3(x, y - 0.9f, 0.14f), new Vector3(1.45f, 0.1f, 0.28f), frame);
                }
            }
            // Cumba (Ottoman bay window)
            if (cumba)
            {
                float cw = width * 0.44f;
                Material cMat = Mats.Lit(Palette.Darker(tint, 0.08f));
                Box(b, new Vector3(0f, FloorH * 2f + 0.2f, 0.65f), new Vector3(cw, FloorH * 2f - 0.4f, 1.3f), cMat);
                for (int f = 1; f <= 2; f++)
                {
                    Box(b, new Vector3(-cw * 0.22f, f * FloorH + 1.5f, 1.31f), new Vector3(0.9f, 1.4f, 0.04f), glass);
                    Box(b, new Vector3(cw * 0.22f, f * FloorH + 1.5f, 1.31f), new Vector3(0.9f, 1.4f, 0.04f), glass);
                }
                Box(b, new Vector3(0f, FloorH * 3f + 0.05f, 0.7f), new Vector3(cw + 0.2f, 0.2f, 1.45f), Mats.Lit(Palette.Darker(tint, 0.3f)));
            }

            // Ground floor: restaurant or apartment door
            bool restaurant = Restaurants.Count < RestaurantNames.Length && street.StartsWith("Fevzi") && Random.value < 0.3f;
            Vector3 doorLocal = new Vector3(0f, 0f, 1.6f);
            if (restaurant)
            {
                Box(b, new Vector3(0f, 1.45f, 0.1f), new Vector3(width * 0.8f, 2.3f, 0.05f), Mats.Glass(Palette.Hex("#3A2E22")));
                Color awning = Palette.Hex(Random.value < 0.5f ? "#B23A3A" : "#2F6B4F");
                var aw = Box(b, new Vector3(0f, 3.05f, 0.8f), new Vector3(width * 0.85f, 0.08f, 1.6f), Mats.Paint(awning, 0.3f, 0f));
                aw.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
                string name = RestaurantNames[Restaurants.Count];
                Sign(b, name, new Vector3(0f, 3.75f, 0.2f), Palette.Hex("#1E1A16"), Palette.Hex("#FFE7A3"), width * 0.85f);
                Restaurants.Add(new Place { Name = name, District = Districts[Random.Range(0, Districts.Length)], DoorPos = b.TransformPoint(doorLocal), Restaurant = true });
            }
            else
            {
                Box(b, new Vector3(0f, 1.2f, 0.08f), new Vector3(1.4f, 2.4f, 0.1f), Mats.Paint(Palette.Hex("#4A3426"), 0.35f, 0f));
                Box(b, new Vector3(0f, 2.55f, 0.09f), new Vector3(1.6f, 0.15f, 0.14f), frame);
                // shop shutter next to the door on wider buildings
                if (width > 9f) Box(b, new Vector3(width * 0.28f, 1.35f, 0.08f), new Vector3(width * 0.35f, 2.5f, 0.06f), Mats.PbrSized("corrugated_iron", new Vector2(width * 0.35f, 2.5f), 1.5f, 0.4f, Palette.Hex("#9AA3A8")));
                int no = Random.Range(1, 120);
                Sign(b, "No: " + no, new Vector3(0f, 2.85f, 0.18f), Palette.Hex("#1D4E89"), Color.white, 1.1f);
                Homes.Add(new Place { Name = street + " No: " + no, District = Districts[Random.Range(0, Districts.Length)], DoorPos = b.TransformPoint(doorLocal), Restaurant = false });
            }

            Shapes.Combine(b.gameObject);
        }

        GameObject Box(Transform parent, Vector3 localPos, Vector3 size, Material m)
        {
            var go = Shapes.Cube(localPos, size, Color.white, parent: parent);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>Flat sign with text (restaurant names, door numbers). width in meters.</summary>
        void Sign(Transform building, string text, Vector3 localPos, Color board, Color textColor, float width)
        {
            var holder = new GameObject("Tabela").transform;
            holder.SetParent(building, false);
            holder.localPosition = localPos;
            var bg = Shapes.Cube(Vector3.zero, new Vector3(width, 0.6f, 0.08f), Color.white, parent: holder);
            bg.GetComponent<Renderer>().sharedMaterial = Mats.Paint(board, 0.5f, 0f);

            var t = new GameObject("Yazi");
            t.transform.SetParent(holder, false);
            t.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TextMesh faces -Z; flip to face the street (+Z)
            var tm = t.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = textColor;
            t.GetComponent<MeshRenderer>().sharedMaterial = textMat;
            // shrink long names to fit the board
            float approxWidth = text.Length * 64 * 0.05f * 0.055f;
            if (approxWidth > width * 0.92f) tm.characterSize *= width * 0.92f / approxWidth;
            // Keep the sign out of mesh combining (it has a text child).
            holder.gameObject.name = "Tabela";
            var keep = new GameObject("_");
            keep.transform.SetParent(bg.transform, false);
        }

        /// <summary>Buildings that close the far ends of every street, so the city never looks unfinished.</summary>
        void BuildEndCaps()
        {
            // Avenue ends: a building row across the street, behind the barriers.
            foreach (float zSide in new[] { -1f, 1f })
            {
                float zFront = zSide * (MainLength + 0.5f);
                float yaw = zSide > 0 ? 180f : 0f;
                for (float x = -WalkHalf - 10f; x < WalkHalf + 10f; x += 10f)
                    MakeBuilding(new Vector3(x + 5f, 0f, zFront), yaw, 10f, 14f, Random.Range(4, 7), "Fevzi Paşa Cd.");
            }
            // Alley ends
            foreach (float az in AlleyZ)
                foreach (float xSide in new[] { -1f, 1f })
                {
                    float xFront = xSide * (AlleyLength + 0.5f);
                    float yaw = xSide > 0 ? -90f : 90f;
                    MakeBuilding(new Vector3(xFront, 0f, az), yaw, AlleyHalf * 2f + 8f, 12f, Random.Range(3, 5), StreetNames[Random.Range(0, StreetNames.Length)]);
                }
        }

        // ---------------- props ----------------

        void BuildStreetProps()
        {
            // Street lamps on both sidewalks
            for (float z = -MainLength + 10f; z < MainLength - 5f; z += 22f)
            {
                if (NearAlley(z, 2f)) continue;
                foreach (float side in new[] { -1f, 1f })
                {
                    var p = new Vector3(side * (RoadHalf + 0.7f), 0.15f, z + (side > 0 ? 11f : 0f));
                    AddObstacle(p, 0.35f);
                    if (Props.Spawn("street_lamp_01", p, side > 0 ? -90f : 90f, 1f, root) == null)
                    {
                        Shapes.Cylinder(p + Vector3.up * 2.5f, new Vector3(0.12f, 2.5f, 0.12f), Palette.Hex("#2B2F33"), parent: root);
                    }
                }
            }
            // Parked (covered) cars along the curb
            for (float z = -MainLength + 25f; z < MainLength - 20f; z += Random.Range(18f, 40f))
            {
                if (NearAlley(z, 6f)) continue;
                float side = Random.value < 0.5f ? -1f : 1f;
                var carPos = new Vector3(side * (RoadHalf - 1.3f), 0f, z);
                if (Props.Spawn("covered_car", carPos, Random.Range(-3f, 3f), 1f, root) == null)
                    Shapes.Cube(carPos + Vector3.up * 0.75f, new Vector3(1.8f, 1.5f, 4.3f), Palette.Hex("#6E7378"), parent: root);
                AddObstacle(carPos + Vector3.forward * 1.2f, 1.05f);
                AddObstacle(carPos - Vector3.forward * 1.2f, 1.05f);
            }
            // Fire hydrants and cafe tables near restaurants
            foreach (var r in Restaurants)
            {
                Vector3 outward = new Vector3(Mathf.Sign(r.DoorPos.x), 0f, 0f);
                var tp = r.DoorPos + Vector3.forward * 3.2f + outward * 0.6f + Vector3.up * 0.15f;
                if (Props.Spawn("outdoor_table_chair_set_01", tp, Random.Range(0f, 360f), 1f, root) != null) AddObstacle(tp, 0.9f);
            }
            for (float z = -MainLength + 40f; z < MainLength; z += 70f)
            {
                var hp = new Vector3(RoadHalf + 0.5f, 0.15f, z);
                Props.Spawn("fire_hydrant", hp, 0f, 1f, root);
                AddObstacle(hp, 0.3f);
            }
        }

        void Barrier(Vector3 pos, float yaw)
        {
            if (Props.Spawn("concrete_road_barrier", pos, yaw, 1f, root) == null)
            {
                var b = Shapes.Cube(pos + Vector3.up * 0.45f, new Vector3(2f, 0.9f, 0.5f), Palette.Hex("#C8C3B8"), parent: root);
                b.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
    }
}
