using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Real street network of Karagümrük / Fatih / Balat from OpenStreetMap (downloaded at build time
    /// by tools/osm_yollar.py). Gives GPS routes (A*), street names and real restaurant locations.
    /// </summary>
    public class Roads
    {
        [Serializable] public class Way { public string name; public string type; public bool oneway; }
        [Serializable] public class PlaceData { public string kind; public string cuisine; public float x; public float z; }
        [Serializable]
        class Data
        {
            public float[] nx; public float[] nz;
            public int[] ea; public int[] eb; public int[] ew;
            public Way[] ways; public PlaceData[] places;
        }

        public readonly Vector2[] Nodes;
        public readonly List<int>[] Neighbors;
        public readonly List<int>[] NeighborWay;
        public readonly Way[] Ways;
        public readonly PlaceData[] Places;
        public bool Loaded => Nodes.Length > 0;

        public Roads()
        {
            var ta = Resources.Load<TextAsset>("Harita/yollar");
            Data d = ta != null ? JsonUtility.FromJson<Data>(ta.text) : null;
            if (d == null || d.nx == null)
            {
                Debug.LogWarning("[Eve] Yol ağı bulunamadı (Harita/yollar).");
                Nodes = new Vector2[0];
                Neighbors = new List<int>[0];
                NeighborWay = new List<int>[0];
                Ways = new Way[0];
                Places = new PlaceData[0];
                return;
            }
            int n = d.nx.Length;
            Nodes = new Vector2[n];
            Neighbors = new List<int>[n];
            NeighborWay = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                Nodes[i] = new Vector2(d.nx[i], d.nz[i]);
                Neighbors[i] = new List<int>(3);
                NeighborWay[i] = new List<int>(3);
            }
            for (int e = 0; e < d.ea.Length; e++)
            {
                int a = d.ea[e], b = d.eb[e], w = d.ew[e];
                Neighbors[a].Add(b); NeighborWay[a].Add(w);
                Neighbors[b].Add(a); NeighborWay[b].Add(w);   // two-way for a motorbike courier (GPS ignores one-way for now)
            }
            Ways = d.ways ?? new Way[0];
            Places = d.places ?? new PlaceData[0];
            Debug.Log("[Eve] Yol ağı: " + n + " düğüm, " + d.ea.Length + " kenar, " + Places.Length + " yemek yeri");
        }

        public Vector3 Pos(int node) => new Vector3(Nodes[node].x, 0f, Nodes[node].y);

        /// <summary>Closest street node to a world position (XZ).</summary>
        public int Nearest(Vector3 p)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < Nodes.Length; i++)
            {
                float dx = Nodes[i].x - p.x, dz = Nodes[i].y - p.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < bd) { bd = d2; best = i; }
            }
            return best;
        }

        /// <summary>Name of a street touching this node ("" if unnamed).</summary>
        public string StreetName(int node)
        {
            if (node < 0) return "";
            foreach (int w in NeighborWay[node])
                if (!string.IsNullOrEmpty(Ways[w].name)) return Ways[w].name;
            return "";
        }

        /// <summary>A random node on a named, car-accessible street, at least minDist from 'from'.</summary>
        public int RandomStreetNode(Vector3 from, float minDist, float maxDist, System.Random rng)
        {
            for (int tries = 0; tries < 400; tries++)
            {
                int i = rng.Next(Nodes.Length);
                if (Neighbors[i].Count == 0 || string.IsNullOrEmpty(StreetName(i))) continue;
                float d = Vector2.Distance(Nodes[i], new Vector2(from.x, from.z));
                if (d >= minDist && d <= maxDist) return i;
            }
            return rng.Next(Nodes.Length);
        }

        /// <summary>Shortest path along streets (A*). Returns node indices from start to goal (empty if none).</summary>
        public List<int> Route(int start, int goal)
        {
            var path = new List<int>();
            if (start < 0 || goal < 0) return path;
            int n = Nodes.Length;
            var g = new float[n];
            var came = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.MaxValue; came[i] = -1; }
            var open = new SortedSet<(float, int)>();
            g[start] = 0f;
            open.Add((H(start, goal), start));
            int expanded = 0;
            while (open.Count > 0 && expanded < 20000)
            {
                var cur = open.Min;
                open.Remove(cur);
                int u = cur.Item2;
                if (closed[u]) continue;
                closed[u] = true;
                expanded++;
                if (u == goal) break;
                var nb = Neighbors[u];
                for (int k = 0; k < nb.Count; k++)
                {
                    int v = nb[k];
                    if (closed[v]) continue;
                    float ng = g[u] + Vector2.Distance(Nodes[u], Nodes[v]);
                    if (ng < g[v])
                    {
                        g[v] = ng;
                        came[v] = u;
                        open.Add((ng + H(v, goal), v));
                    }
                }
            }
            if (came[goal] < 0 && goal != start) return path;
            for (int v = goal; v >= 0; v = came[v])
            {
                path.Add(v);
                if (v == start) break;
            }
            path.Reverse();
            return path;
        }

        float H(int a, int b) => Vector2.Distance(Nodes[a], Nodes[b]);
    }
}
