using CesiumForUnity;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The real city: Google Photorealistic 3D Tiles of Istanbul streamed through Cesium for Unity,
    /// centered on Karagümrük. Unity axes at the origin: +X east, +Y up, +Z north (1 unit = 1 meter).
    /// </summary>
    public class EveWorld
    {
        public const double OriginLat = 41.0283;
        public const double OriginLon = 28.9392;
        public const double OriginHeight = 95.0;          // approx. ellipsoid height of street level here
        public const long GoogleTilesAssetId = 2275207;   // "Google Photorealistic 3D Tiles" on Cesium ion

        /// <summary>Layer used by the player, bike and markers so ground raycasts ignore them.</summary>
        public const int PlayerLayer = 2; // Unity's built-in "Ignore Raycast"
        public const int GroundMask = ~(1 << PlayerLayer);
        public const int FloorLayer = 4;  // built-in "Water": the invisible safety floor (SafetyFloor.cs)
        /// <summary>Only the real city geometry (no player, no safety floor): used to probe street height.</summary>
        public const int TilesMask = GroundMask & ~(1 << FloorLayer);

        public readonly CesiumGeoreference Geo;
        public readonly Cesium3DTileset Tiles;
        public readonly bool HasToken;

        const double R = 6378137.0;
        static readonly double KLat = System.Math.PI / 180.0 * R;
        static readonly double KLon = System.Math.PI / 180.0 * R * System.Math.Cos(OriginLat * System.Math.PI / 180.0);

        public EveWorld()
        {
            var geoGo = new GameObject("CesiumGeoreference");
            Geo = geoGo.AddComponent<CesiumGeoreference>();
            Geo.SetOriginLongitudeLatitudeHeight(OriginLon, OriginLat, OriginHeight);

            var token = Resources.Load<TextAsset>("Harita/cesium_token");
            HasToken = token != null && !string.IsNullOrWhiteSpace(token.text);
            if (!HasToken)
            {
                Debug.LogWarning("[Eve] Cesium token yok (CESIUM_ION_TOKEN secret'ı eklenmeli). Şehir yüklenmeyecek.");
                return;
            }

            var tGo = new GameObject("Istanbul (Google 3D)");
            tGo.transform.SetParent(geoGo.transform, false);
            Tiles = tGo.AddComponent<Cesium3DTileset>();
            Tiles.ionAccessToken = token.text.Trim();
            Tiles.ionAssetID = GoogleTilesAssetId;
            Tiles.maximumScreenSpaceError = 6f;        // lower = sharper (PC can afford it)
            Tiles.createPhysicsMeshes = true;          // we drive and walk on the real geometry
            Tiles.showCreditsOnScreen = true;          // Google / Cesium attribution is required
        }

        /// <summary>0..100 tile loading progress for the current view.</summary>
        public float Progress => Tiles != null ? Tiles.ComputeLoadProgress() : 100f;

        /// <summary>Latitude / longitude → local Unity position (y = 0; use Ground() to snap).</summary>
        public static Vector3 GeoToLocal(double lat, double lon)
        {
            return new Vector3((float)((lon - OriginLon) * KLon), 0f, (float)((lat - OriginLat) * KLat));
        }

        /// <summary>Finds the real street / ground height under a point (raycast from high above).</summary>
        public static bool Ground(Vector3 pos, out Vector3 hit, float fromHeight = 250f)
        {
            var start = new Vector3(pos.x, fromHeight, pos.z);
            if (Physics.Raycast(start, Vector3.down, out var h, fromHeight + 250f, TilesMask, QueryTriggerInteraction.Ignore))
            {
                hit = h.point;
                return true;
            }
            hit = new Vector3(pos.x, 0f, pos.z);
            return false;
        }

        /// <summary>
        /// Ground height for a point on a STREET: casts from a few meters above the expected street level
        /// so balconies and roofs above do not count. expectedY = last known street height nearby.
        /// </summary>
        public static bool StreetGround(Vector3 pos, float expectedY, out Vector3 hit)
        {
            var start = new Vector3(pos.x, expectedY + 6f, pos.z);
            if (Physics.Raycast(start, Vector3.down, out var h, 40f, TilesMask, QueryTriggerInteraction.Ignore))
            {
                hit = h.point;
                return true;
            }
            return Ground(pos, out hit);
        }
    }
}
