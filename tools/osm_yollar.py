#!/usr/bin/env python3
"""Downloads the real street network (OpenStreetMap, ODbL) for a lat/lon box and converts it to
local meters around an origin, so the game can do GPS routing, place restaurants on real streets
and (later) drive AI traffic.

Usage: python3 tools/osm_yollar.py <config.json> <output.json>

config.json (Assets/Scripts/Game/harita.json):
{ "origin": {"lat": 41.0285, "lon": 28.9389}, "bbox": {"south": .., "west": .., "north": .., "east": ..} }

Output (Assets/Resources/Harita/yollar.json), flat arrays for Unity JsonUtility:
{ "nx": [...], "nz": [...],           # node positions in meters, +x = east, +z = north (Cesium for Unity convention)
  "ea": [...], "eb": [...], "ew": [...],  # edges: node a, node b, way index
  "ways":  [{"name": "Fevzi Paşa Cd.", "type": "secondary", "oneway": false}, ...],
  "places": [{"name": "...", "kind": "restaurant", "x": .., "z": ..}, ...] }

Map data © OpenStreetMap contributors (ODbL) — credited in the game.
"""
import json
import math
import os
import sys
import time
import urllib.parse
import urllib.request

UA = "ARC-Oyun-Fabrikasi/1.0 (+https://arcstudios.com.tr)"
ENDPOINTS = ["https://overpass-api.de/api/interpreter", "https://overpass.kumi.systems/api/interpreter",
             "https://overpass.private.coffee/api/interpreter"]
DRIVABLE = "motorway|trunk|primary|secondary|tertiary|unclassified|residential|living_street|service|motorway_link|trunk_link|primary_link|secondary_link|tertiary_link"


def overpass(query):
    data = urllib.parse.urlencode({"data": query}).encode()
    last = None
    for attempt in range(3):
        for url in ENDPOINTS:
            try:
                req = urllib.request.Request(url, data=data, headers={"User-Agent": UA})
                with urllib.request.urlopen(req, timeout=90) as r:
                    return json.loads(r.read())
            except Exception as e:  # noqa: BLE001
                last = e
                print(f"  overpass hata ({url}): {e}")
        time.sleep(5 + attempt * 10)
    raise RuntimeError(f"Overpass erişilemedi: {last}")


def main():
    cfg = json.load(open(sys.argv[1], encoding="utf-8"))
    out = sys.argv[2]
    lat0, lon0 = cfg["origin"]["lat"], cfg["origin"]["lon"]
    b = cfg["bbox"]
    box = f'{b["south"]},{b["west"]},{b["north"]},{b["east"]}'

    q = f"""[out:json][timeout:120];
(
  way["highway"~"^({DRIVABLE})$"]({box});
  node["amenity"~"^(restaurant|fast_food|cafe)$"]({box});
);
out body;
>;
out skel qt;"""
    data = overpass(q)

    # Local tangent plane (East, North) in meters around the origin.
    R = 6378137.0
    k_lat = math.pi / 180.0 * R
    k_lon = math.pi / 180.0 * R * math.cos(math.radians(lat0))

    def to_local(lat, lon):
        return round((lon - lon0) * k_lon, 2), round((lat - lat0) * k_lat, 2)

    osm_nodes = {}
    for el in data["elements"]:
        if el["type"] == "node" and "lat" in el:
            osm_nodes[el["id"]] = (el["lat"], el["lon"])

    node_index = {}
    nodes, edges, ways, places = [], [], [], []

    def idx(osm_id):
        if osm_id not in node_index:
            lat, lon = osm_nodes[osm_id]
            node_index[osm_id] = len(nodes)
            nodes.append(list(to_local(lat, lon)))
        return node_index[osm_id]

    for el in data["elements"]:
        if el["type"] != "way":
            continue
        tags = el.get("tags", {})
        hw = tags.get("highway")
        if not hw:
            continue
        if hw == "service" and tags.get("service") in ("parking_aisle", "driveway"):
            continue
        refs = [r for r in el.get("nodes", []) if r in osm_nodes]
        if len(refs) < 2:
            continue
        wi = len(ways)
        ways.append({
            "name": tags.get("name", ""),
            "type": hw,
            "oneway": tags.get("oneway") in ("yes", "1", "true") or hw in ("motorway",),
        })
        for a, c in zip(refs, refs[1:]):
            edges.append([idx(a), idx(c), wi])

    for el in data["elements"]:
        if el["type"] == "node" and el.get("tags", {}).get("amenity") in ("restaurant", "fast_food", "cafe"):
            x, z = to_local(el["lat"], el["lon"])
            places.append({"kind": el["tags"]["amenity"], "cuisine": el["tags"].get("cuisine", ""), "x": x, "z": z})

    os.makedirs(os.path.dirname(out), exist_ok=True)
    # Flat arrays so Unity's JsonUtility can read it directly.
    result = {
        "nx": [n[0] for n in nodes], "nz": [n[1] for n in nodes],
        "ea": [e[0] for e in edges], "eb": [e[1] for e in edges], "ew": [e[2] for e in edges],
        "ways": ways,
        "places": places,
    }
    with open(out, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, separators=(",", ":"))
    named = sorted({w["name"] for w in ways if w["name"]})
    print(f"Yollar: {len(nodes)} düğüm, {len(edges)} kenar, {len(ways)} yol ({len(named)} isimli), {len(places)} yemek yeri")
    print("Örnek sokaklar: " + ", ".join(named[:15]))


if __name__ == "__main__":
    main()
