"""해 위치로 캠퍼스 그늘을 대충 계산한다: 건물 그림자 + OSM 숲 · 가로수 줄.

    python3 tools/route/shade.py <map.json> <출력 폴더>

출력: shade_<날짜>_<시>.png (그늘 = 반투명 남색, 지도 1 m = 1 px) 와 shade.json ({날짜: {시: {길 id: 그늘 비율}}}).
한계: 지형(동쪽 산) 그림자와 실제 가로수 위치는 모른다 (OSM 에 없음). 건물 높이는 OSM 층수 · 추정값.
"""
import json, math, os, sys
from datetime import date

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LAT, LON = 37.244, 127.080
DAYS = {"10-27": date(2026, 10, 27), "07-15": date(2026, 7, 15)}
HOURS = [9, 11, 13, 15, 17]
MAX_SHADOW = 150.0     # 해가 아주 낮을 때 그림자를 끝없이 늘이지 않게 (m)
TREE_ROW_HALF = 4.0    # 가로수 줄 그늘 반폭 (m)


def sun(day, hour):
    """(고도°, 방위° — 북에서 시계 방향). 한국 표준시 기준, 몇 분 오차는 무시."""
    n = day.timetuple().tm_yday
    b = 2 * math.pi * (n - 81) / 364
    eot = 9.87 * math.sin(2 * b) - 7.53 * math.cos(b) - 1.5 * math.sin(b)      # 균시차 (분)
    solar = hour + (4 * (LON - 135) + eot) / 60
    h = math.radians(15 * (solar - 12))
    d = math.radians(23.44 * math.sin(2 * math.pi * (284 + n) / 365))
    p = math.radians(LAT)
    el = math.asin(math.sin(p) * math.sin(d) + math.cos(p) * math.cos(d) * math.cos(h))
    az = math.degrees(math.atan2(math.sin(h), math.cos(h) * math.sin(p) - math.tan(d) * math.cos(p))) + 180
    return math.degrees(el), az % 360


def load(map_path):
    m = json.load(open(map_path))
    W, H = m["w"], m["h"]
    # 아트용 건물: 좌표는 지도 가운데가 0, z 는 북쪽이 + → 지도 m (x 동, y 남)
    bl = json.load(open(os.path.join(ROOT, "tools/keyart/preview/data/buildings.json")))
    buildings = [([(x + W / 2, H / 2 - z) for x, z in b["poly"]], b["h"]) for b in bl if b["h"] > 0]
    sys.path.insert(0, os.path.join(ROOT, "tools/keyart"))
    from kuca_art.geo import MIN_LON, MAX_LON, MIN_LAT, MAX_LAT
    xy = lambda g: ((g["lon"] - MIN_LON) / (MAX_LON - MIN_LON) * W, (MAX_LAT - g["lat"]) / (MAX_LAT - MIN_LAT) * H)
    osm = json.load(open(os.path.join(ROOT, "tools/keyart/osm_ground.json")))["elements"]
    canopy, rows = [], []
    for e in osm:
        t = e.get("tags", {})
        if e["type"] != "way" or "geometry" not in e:
            continue
        if t.get("natural") == "wood" or t.get("landuse") == "forest":
            canopy.append([xy(g) for g in e["geometry"]])
        elif t.get("natural") == "tree_row":
            rows.append([xy(g) for g in e["geometry"]])
    return m, buildings, canopy, rows


def mask(m, buildings, canopy, rows, el, az):
    W, H = round(m["w"]), round(m["h"])
    img = Image.new("L", (W, H), 0)
    dr = ImageDraw.Draw(img)
    for poly in canopy:
        dr.polygon(poly, fill=255)
    for line in rows:
        dr.line(line, fill=255, width=int(TREE_ROW_HALF * 2))
    if el <= 0:
        return np.ones((H, W), bool)
    k = min(1 / math.tan(math.radians(el)), MAX_SHADOW / 10)   # 높이 1 m 당 그림자 길이
    a = math.radians(az)
    ux, uy = -math.sin(a), math.cos(a)                          # 해 반대쪽 (지도 y 는 남쪽이 +)
    for poly, h in buildings:
        L = min(h * k, MAX_SHADOW)
        dx, dy = ux * L, uy * L
        moved = [(x + dx, y + dy) for x, y in poly]
        dr.polygon(poly, fill=255)
        dr.polygon(moved, fill=255)
        for i in range(len(poly)):   # 벽이 쓸고 지나간 면
            p, q = poly[i], poly[(i + 1) % len(poly)]
            dr.polygon([p, q, moved[(i + 1) % len(poly)], moved[i]], fill=255)
    return np.asarray(img) > 0


def seg_shade(m, sh):
    H, W = sh.shape
    out = {}
    for s in m["segs"]:
        p, hit, n = s["p"], 0, 0
        for i in range(0, len(p) - 2, 2):
            x0, y0, x1, y1 = p[i], p[i + 1], p[i + 2], p[i + 3]
            steps = max(1, int(math.dist((x0, y0), (x1, y1)) / 2))
            for j in range(steps + 1):
                x, y = int(x0 + (x1 - x0) * j / steps), int(y0 + (y1 - y0) * j / steps)
                if 0 <= x < W and 0 <= y < H:
                    n += 1
                    hit += sh[y, x]
        out[s["id"]] = round(hit / n, 2) if n else 0
    return out


def main(map_path, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    m, buildings, canopy, rows = load(map_path)
    result = {"sun": {}, "segs": {}}
    for name, day in DAYS.items():
        result["sun"][name], result["segs"][name] = {}, {}
        for hour in HOURS:
            el, az = sun(day, hour)
            sh = mask(m, buildings, canopy, rows, el, az)
            rgba = np.zeros(sh.shape + (4,), np.uint8)
            rgba[sh] = (20, 30, 70, 120)
            Image.fromarray(rgba).save(os.path.join(out_dir, f"shade_{name}_{hour}.png"), optimize=True)
            result["sun"][name][hour] = {"elevation": round(el, 1), "azimuth": round(az, 1)}
            result["segs"][name][hour] = seg_shade(m, sh)
    json.dump(result, open(os.path.join(out_dir, "shade.json"), "w"), ensure_ascii=False, separators=(",", ":"))
    return result


if __name__ == "__main__":
    # 해 위치 확인: 하지 정오 서울 고도 ≈ 76°, 동지 정오 ≈ 29°, 정오 방위 ≈ 180°
    el, az = sun(date(2026, 6, 21), 12.5)
    assert 74 < el < 77 and 170 < az < 190, (el, az)
    el, _ = sun(date(2026, 12, 21), 12.5)
    assert 27 < el < 31, el
    r = main(sys.argv[1], sys.argv[2])
    for d, hs in r["sun"].items():
        print(d, {h: (v["elevation"], v["azimuth"]) for h, v in hs.items()})
