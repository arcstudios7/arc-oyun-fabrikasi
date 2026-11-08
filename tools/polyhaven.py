#!/usr/bin/env python3
"""Downloads the Poly Haven (CC0) assets a game declares in its kaynaklar.json into Assets/Resources/PH/.

Usage: python3 tools/polyhaven.py Assets/Scripts/Game/kaynaklar.json Assets/Resources/PH

kaynaklar.json:
{
  "hdri": "belfast_sunset_puresky",          # optional, sky + lighting
  "textures": ["asphalt_02", "red_brick_03"],  # PBR textures -> PH/tex/<id>_diff.jpg + _nor.jpg
  "models": ["street_lamp_01"],                # glTF models   -> PH/models/<id>/<id>.gltf
  "texture_res": "1k", "hdri_res": "2k", "model_res": "1k"
}
All Poly Haven assets are CC0 (public domain): free for commercial use, no attribution required.
"""
import json
import os
import sys
import time
import urllib.request

UA = {"User-Agent": "ARC-Oyun-Fabrikasi/1.0 (+https://arcstudios.com.tr)"}
API = "https://api.polyhaven.com"


def fetch(url, retries=3):
    for attempt in range(retries):
        try:
            req = urllib.request.Request(url, headers=UA)
            with urllib.request.urlopen(req, timeout=90) as r:
                return r.read()
        except Exception as e:  # noqa: BLE001
            if attempt == retries - 1:
                raise
            print(f"  tekrar deneniyor ({e})")
            time.sleep(2 + attempt * 3)


def save(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = fetch(url)
    with open(path, "wb") as f:
        f.write(data)
    print(f"  + {path} ({len(data) // 1024} KB)")


def pick(d, res):
    """Return d[res], falling back to the closest available resolution."""
    if res in d:
        return d[res]
    order = ["1k", "2k", "4k", "8k", "16k"]
    for r in order:
        if r in d:
            return d[r]
    raise KeyError(f"no resolution in {list(d)}")


def main():
    cfg_path, out = sys.argv[1], sys.argv[2]
    if not os.path.exists(cfg_path):
        print(f"{cfg_path} yok, indirilecek kaynak yok.")
        return
    cfg = json.load(open(cfg_path, encoding="utf-8"))
    tres, hres, mres = cfg.get("texture_res", "1k"), cfg.get("hdri_res", "2k"), cfg.get("model_res", "1k")
    failed = []

    for tid in cfg.get("textures", []):
        try:
            files = json.loads(fetch(f"{API}/files/{tid}"))
            save(pick(files["Diffuse"], tres)["jpg"]["url"], f"{out}/tex/{tid}_diff.jpg")
            save(pick(files["nor_gl"], tres)["jpg"]["url"], f"{out}/tex/{tid}_nor.jpg")
        except Exception as e:  # noqa: BLE001
            failed.append(f"doku {tid}: {e}")

    hdri = cfg.get("hdri")
    if hdri:
        try:
            files = json.loads(fetch(f"{API}/files/{hdri}"))
            save(pick(files["hdri"], hres)["hdr"]["url"], f"{out}/hdri/{hdri}.hdr")
        except Exception as e:  # noqa: BLE001
            failed.append(f"hdri {hdri}: {e}")

    for mid in cfg.get("models", []):
        try:
            files = json.loads(fetch(f"{API}/files/{mid}"))
            g = pick(files["gltf"], mres)["gltf"]
            base = f"{out}/models/{mid}"
            save(g["url"], f"{base}/{mid}.gltf")
            for rel, info in g.get("include", {}).items():
                save(info["url"], f"{base}/{rel}")
        except Exception as e:  # noqa: BLE001
            failed.append(f"model {mid}: {e}")

    if failed:
        print("UYARI: bazı kaynaklar indirilemedi:\n  " + "\n  ".join(failed))
    print("Kaynaklar hazır.")


if __name__ == "__main__":
    main()
