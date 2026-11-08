using UnityEngine;

namespace Arc
{
    /// <summary>
    /// Real 3D models from Poly Haven (CC0). The id must be listed under "models" in Game/kaynaklar.json.
    /// Example: Props.Spawn("street_lamp_01", new Vector3(4, 0, 10), 90f);
    /// </summary>
    public static class Props
    {
        /// <summary>True if the model was downloaded and can be spawned.</summary>
        public static bool Has(string modelId) => Load(modelId) != null;

        /// <summary>Spawn a model. yRotation in degrees. Returns null (and logs) if the model is missing.</summary>
        public static GameObject Spawn(string modelId, Vector3 position, float yRotation = 0f, float scale = 1f, Transform parent = null)
        {
            var prefab = Load(modelId);
            if (prefab == null)
            {
                Debug.LogWarning("[Arc] Model bulunamadı: " + modelId + " (kaynaklar.json'a eklendi mi?)");
                return null;
            }
            var go = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yRotation, 0f), parent);
            go.name = modelId;
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale *= scale;
            return go;
        }

        static GameObject Load(string modelId) => Resources.Load<GameObject>("PH/models/" + modelId + "/" + modelId);

        /// <summary>World-space bounds of an object including all children (useful to place models on the ground).</summary>
        public static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
