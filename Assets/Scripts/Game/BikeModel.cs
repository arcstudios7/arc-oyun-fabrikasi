using Arc;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The bike's look. Uses the real 3D model from Resources/Arac/motor (Sketchfab glTF/FBX, downloaded by the owner)
    /// if present: auto-scaled to 2 m length, auto-oriented, wheels found by name. Otherwise a detailed fallback.
    /// The delivery box is always added (red "Eve Servis" box).
    /// </summary>
    public static class BikeModel
    {
        public static void Build(Bike bike, Transform visual, out Transform frontWheel, out Transform rearWheel, out Transform steerPivot)
        {
            frontWheel = rearWheel = steerPivot = null;
            var prefab = Resources.Load<GameObject>("Arac/motor");
            if (prefab != null)
            {
                var m = Object.Instantiate(prefab, visual);
                m.name = "MotorModel";
                Courier.FixMaterials(m);
                FitModel(m.transform, 1.95f);
                FindWheels(m.transform, out frontWheel, out rearWheel);
                AddBox(visual, new Vector3(0f, 1.12f, -0.62f));
                return;
            }
            Fallback(visual, out frontWheel, out rearWheel, out steerPivot);
            AddBox(visual, new Vector3(0f, 1.12f, -0.62f));
        }

        /// <summary>Scales a model so its longest horizontal side = length, puts it on the ground, points it along +Z.</summary>
        static void FitModel(Transform m, float length)
        {
            m.localPosition = Vector3.zero;
            m.localRotation = Quaternion.identity;
            m.localScale = Vector3.one;
            var b = Props.BoundsOf(m.gameObject);
            bool alongX = b.size.x > b.size.z;
            if (alongX) m.localRotation = Quaternion.Euler(0f, 90f, 0f);
            float longest = Mathf.Max(b.size.x, b.size.z);
            if (longest > 0.01f) m.localScale = Vector3.one * (length / longest);
            b = Props.BoundsOf(m.gameObject);
            Vector3 centerLocal = m.parent.InverseTransformPoint(b.center);
            float bottomLocal = m.parent.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
            m.localPosition -= new Vector3(centerLocal.x, bottomLocal, centerLocal.z);
        }

        static void FindWheels(Transform root, out Transform front, out Transform rear)
        {
            front = rear = null;
            var wheels = new System.Collections.Generic.List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre") || n.Contains("teker")) wheels.Add(t);
            }
            foreach (var t in wheels)
            {
                string n = t.name.ToLowerInvariant();
                if (front == null && n.Contains("front")) front = t;
                else if (rear == null && (n.Contains("rear") || n.Contains("back"))) rear = t;
            }
            // Unnamed sides: decide by position along the bike (+Z = front).
            if ((front == null || rear == null) && wheels.Count >= 2)
            {
                wheels.Sort((a, b) => root.InverseTransformPoint(b.position).z.CompareTo(root.InverseTransformPoint(a.position).z));
                if (front == null) front = wheels[0];
                if (rear == null) rear = wheels[wheels.Count - 1];
            }
        }

        static void AddBox(Transform visual, Vector3 pos)
        {
            Material red = Mats.Paint(Palette.Hex("#C8102E"), 0.55f, 0.05f);
            Material yellow = Mats.Paint(Palette.Hex("#FFC300"), 0.5f, 0f);
            Material rack = Mats.Paint(Palette.Hex("#1C1C1E"), 0.4f, 0.6f);
            var box = Shapes.Group("TeslimatKutusu", pos, visual);
            Part(PrimitiveType.Cube, Vector3.zero, new Vector3(0.5f, 0.44f, 0.46f), red, box);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.51f, 0.08f, 0.47f), yellow, box);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.235f, 0f), new Vector3(0.52f, 0.03f, 0.48f), Mats.Paint(Palette.Hex("#A00D25"), 0.6f, 0f), box);
            Part(PrimitiveType.Cube, new Vector3(0f, -0.26f, 0.05f), new Vector3(0.36f, 0.04f, 0.4f), rack, box);
        }

        static GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Transform parent)
        {
            var go = Shapes.Make(type, pos, scale, Color.white, parent: parent);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>Scooter-style fallback built from many smooth parts (used until a real model is added).</summary>
        static void Fallback(Transform v, out Transform frontWheel, out Transform rearWheel, out Transform steerPivot)
        {
            Material rubber = Mats.Paint(Palette.Hex("#121212"), 0.25f, 0f);
            Material chrome = Mats.Paint(Palette.Hex("#D0D4D8"), 0.92f, 1f);
            Material body = Mats.Paint(Palette.Hex("#E9E6DF"), 0.85f, 0.15f);
            Material dark = Mats.Paint(Palette.Hex("#2A2C30"), 0.6f, 0.2f);
            Material seat = Mats.Paint(Palette.Hex("#151515"), 0.35f, 0f);

            rearWheel = Wheel(v, new Vector3(0f, 0.31f, -0.62f), rubber, chrome);
            steerPivot = Shapes.Group("Gidon", new Vector3(0f, 0.31f, 0.66f), v);
            frontWheel = Wheel(steerPivot, Vector3.zero, rubber, chrome);

            // floorboard + rear body (scooter)
            Part(PrimitiveType.Cube, new Vector3(0f, 0.36f, 0.08f), new Vector3(0.34f, 0.08f, 0.62f), dark, v);
            var rear = Part(PrimitiveType.Capsule, new Vector3(0f, 0.6f, -0.45f), new Vector3(0.42f, 0.45f, 0.42f), body, v);
            rear.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Capsule, new Vector3(0f, 0.86f, -0.28f), new Vector3(0.3f, 0.32f, 0.12f), seat, v).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // front shield + steering column
            var shield = Part(PrimitiveType.Capsule, new Vector3(0f, 0.72f, 0.42f), new Vector3(0.4f, 0.42f, 0.16f), body, v);
            shield.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            var column = Part(PrimitiveType.Cylinder, new Vector3(0f, 0.5f, -0.05f), new Vector3(0.06f, 0.5f, 0.06f), chrome, steerPivot);
            column.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            Part(PrimitiveType.Capsule, new Vector3(0f, 1.0f, -0.18f), new Vector3(0.06f, 0.36f, 0.06f), dark, steerPivot).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Sphere, new Vector3(0f, 0.95f, -0.12f), new Vector3(0.2f, 0.16f, 0.14f), body, steerPivot);
            Part(PrimitiveType.Sphere, new Vector3(0f, 0.95f, -0.05f), new Vector3(0.12f, 0.1f, 0.05f), Mats.Unlit(Palette.Hex("#FFF6DA")), steerPivot);
            // front fender
            Part(PrimitiveType.Capsule, new Vector3(0f, 0.36f, 0.04f), new Vector3(0.16f, 0.36f, 0.16f), body, steerPivot).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // tail light + exhaust
            Part(PrimitiveType.Cube, new Vector3(0f, 0.72f, -0.92f), new Vector3(0.18f, 0.06f, 0.04f), Mats.Unlit(Palette.Hex("#FF2020")), v);
            var ex = Part(PrimitiveType.Cylinder, new Vector3(0.2f, 0.3f, -0.55f), new Vector3(0.08f, 0.25f, 0.08f), chrome, v);
            ex.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
        }

        static Transform Wheel(Transform parent, Vector3 pos, Material tire, Material rim)
        {
            var w = Shapes.Group("Teker", pos, parent);
            var t = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.62f, 0.07f, 0.62f), tire, w);
            t.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var r = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.34f, 0.075f, 0.34f), rim, w);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int i = 0; i < 3; i++)
            {
                var sp = Part(PrimitiveType.Cube, Vector3.zero, new Vector3(0.05f, 0.42f, 0.04f), rim, w);
                sp.transform.localRotation = Quaternion.Euler(i * 60f, 0f, 0f);
            }
            return w;
        }
    }
}
