"""
컨셉아트 바닥 타일 + OSM 데이터로 캠퍼스 지면 텍스처와 나무 위치를 만든다.
좌표: CampusMap 과 같은 선형 매핑 (경도 127.0720~127.0880, 위도 37.2370~37.2510, 1418 x 1548 m)
"""
import json, math, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

MIN_LON, MAX_LON, MIN_LAT, MAX_LAT = 127.0720, 127.0880, 37.2370, 37.2510
MAP_W, MAP_H = 1418.0, 1548.0
H = 4096
W = round(H * MAP_W / MAP_H)
PPM = H / MAP_H  # pixels per meter
random.seed(7)
rng = np.random.default_rng(7)

out_tex, out_trees, osm_path, bld_path = sys.argv[1:5]

def to_px(lon, lat):
    return ((lon - MIN_LON) / (MAX_LON - MIN_LON) * W, (MAX_LAT - lat) / (MAX_LAT - MIN_LAT) * H)

def to_world(px, py):
    return ((px / W - 0.5) * MAP_W, (0.5 - py / H) * MAP_H)

# ---------- 타일 ----------
def seamless(img):
    a = np.asarray(img).astype(np.float32)
    h, w = a.shape[:2]
    b = np.roll(np.roll(a, h // 2, axis=0), w // 2, axis=1)
    yy, xx = np.mgrid[0:h, 0:w]
    wy = 1 - np.abs(yy / (h - 1) * 2 - 1); wx = 1 - np.abs(xx / (w - 1) * 2 - 1)
    m = np.clip(np.minimum(wy, wx) * 2.2, 0, 1)[..., None]
    return a * m + b * (1 - m)

def load_tile(name, meters, crop=None, blur=0):
    img = Image.open(f'tiles/{name}.png').convert('RGB')
    if blur:
        # 반복되면 줄무늬처럼 보이는 밝은 풀잎 점을 지운 뒤 살짝 흐린다
        img = img.filter(ImageFilter.MedianFilter(7)).filter(ImageFilter.GaussianBlur(blur))
    if crop:
        w, h = img.size
        img = img.crop((int(crop[0] * w), int(crop[1] * h), int(crop[2] * w), int(crop[3] * h)))
    t = seamless(img)
    size = max(8, int(round(meters * PPM)))
    t = np.asarray(Image.fromarray(t.astype(np.uint8)).resize((size, size), Image.LANCZOS)).astype(np.float32)
    reps = (math.ceil(H / size) + 1, math.ceil(W / size) + 1, 1)
    return np.tile(t, reps)[:H, :W]

print('tiles…', W, H)
grass = load_tile('grass', 36, blur=2.2)
paving = load_tile('paving', 14)
plaza = load_tile('plaza', 18)
water = load_tile('water', 24)
asphalt = load_tile('road', 9, crop=(0.10, 0.0, 0.40, 1.0))
curb_color = np.array([214, 204, 186], np.float32)

canvas = grass.copy()

def blend(mask_img, tex, soften=1.2):
    m = mask_img.filter(ImageFilter.GaussianBlur(soften)) if soften else mask_img
    m = (np.asarray(m).astype(np.float32) / 255.0)[..., None]
    global canvas
    canvas = canvas * (1 - m) + tex * m

def new_mask():
    return Image.new('L', (W, H), 0)

# ---------- OSM ----------
osm = json.load(open(osm_path))['elements']

def rings(e):
    if e['type'] == 'way' and e.get('geometry'):
        g = e['geometry']
        if len(g) >= 4 and g[0] == g[-1]:
            yield 'outer', [to_px(p['lon'], p['lat']) for p in g]
    elif e['type'] == 'relation':
        for m in e.get('members', []):
            if m.get('geometry') and m.get('role') in ('outer', 'inner'):
                yield m['role'], [to_px(p['lon'], p['lat']) for p in m['geometry']]

def area_mask(pred):
    m = new_mask(); d = ImageDraw.Draw(m)
    for e in osm:
        if pred(e.get('tags', {})):
            for role, pts in rings(e):
                if len(pts) >= 3:
                    d.polygon(pts, fill=0 if role == 'inner' else 255)
    return m

T = lambda k, *v: (lambda t: t.get(k) in v)
forest_m = area_mask(lambda t: t.get('natural') in ('wood',) or t.get('landuse') in ('forest',) or t.get('leisure') == 'golf_course')
park_m = area_mask(lambda t: t.get('leisure') in ('park',) or t.get('landuse') in ('grass', 'flowerbed', 'cemetery') or t.get('natural') == 'grassland')
pitch_m = area_mask(lambda t: t.get('leisure') in ('pitch', 'sports_centre'))
track_m = area_mask(lambda t: t.get('leisure') in ('track', 'stadium'))
square_m = area_mask(lambda t: t.get('place') == 'square')
paved_m = area_mask(lambda t: t.get('amenity') == 'parking' or t.get('area:highway') or (t.get('highway') == 'pedestrian' and t.get('area') == 'yes') or t.get('landuse') in ('construction',))
water_m = area_mask(lambda t: t.get('natural') == 'water' or t.get('landuse') in ('reservoir', 'basin'))

print('areas…')
# 숲 영역은 바닥에 숲 그림을 깔지 않고 잔디 그대로 둔다 (3D 나무만 보이게)
pitch_tex = np.clip(grass * np.array([0.92, 1.08, 0.9], np.float32) + 8, 0, 255)
# 육상 트랙(붉은색)과 그 안의 축구장(초록 + 흰 선)
track_tex = np.clip(paving * np.array([0.95, 0.52, 0.42], np.float32), 0, 255)
blend(track_m, track_tex, 1.0)
stadium_m = area_mask(lambda t: t.get('leisure') == 'stadium')
field_m = stadium_m.filter(ImageFilter.MinFilter(int(12 * PPM) | 1))
blend(field_m, pitch_tex, 1.0)
lines_m = new_mask(); ld = ImageDraw.Draw(lines_m)
fa = np.argwhere(np.asarray(field_m) > 0)
if len(fa):
    (y0, x0), (y1, x1) = fa.min(axis=0), fa.max(axis=0)
    cx, cy, lw = (x0 + x1) / 2, (y0 + y1) / 2, max(2, int(0.5 * PPM))
    ld.rectangle((x0 + 4 * PPM, y0 + 4 * PPM, x1 - 4 * PPM, y1 - 4 * PPM), outline=255, width=lw)
    if (y1 - y0) > (x1 - x0):
        ld.line((x0 + 4 * PPM, cy, x1 - 4 * PPM, cy), fill=255, width=lw)
    else:
        ld.line((cx, y0 + 4 * PPM, cx, y1 - 4 * PPM), fill=255, width=lw)
    r = 9 * PPM
    ld.ellipse((cx - r, cy - r, cx + r, cy + r), outline=255, width=lw)
blend(lines_m, np.broadcast_to(np.array([245, 245, 240], np.float32), canvas.shape), 0.6)
blend(pitch_m, pitch_tex, 1.0)
blend(square_m, plaza, 1.0)
blend(paved_m, plaza, 1.0)
blend(water_m, water, 1.5)

# ---------- 도로 ----------
ROAD_W = {'primary': 16, 'secondary': 14, 'tertiary': 11, 'residential': 8, 'unclassified': 8,
          'living_street': 6, 'service': 5}
WALK_W = {'footway': 3.2, 'path': 2.6, 'pedestrian': 6, 'steps': 3.5, 'track': 3.2, 'cycleway': 3}

def lines(kinds):
    for e in osm:
        t = e.get('tags', {})
        hw = t.get('highway')
        if e['type'] == 'way' and hw in kinds and e.get('geometry') and t.get('area') != 'yes':
            yield hw, [to_px(p['lon'], p['lat']) for p in e['geometry']]

def stroke_mask(kinds, extra=0.0):
    m = new_mask(); d = ImageDraw.Draw(m)
    for hw, pts in lines(kinds):
        w = max(2, int(round((kinds[hw] + extra) * PPM)))
        d.line(pts, fill=255, width=w, joint='curve')
        r = w / 2
        for x, y in (pts[0], pts[-1]):
            d.ellipse((x - r, y - r, x + r, y + r), fill=255)
    return m

print('roads…')
walk_m = stroke_mask(WALK_W)
blend(walk_m, paving, 0.8)
curb_m = stroke_mask(ROAD_W, extra=2.2)
blend(curb_m, np.broadcast_to(curb_color, canvas.shape), 0.8)
road_m = stroke_mask(ROAD_W)
blend(road_m, asphalt, 0.8)

# 아주 약한 비네팅으로 지도 가장자리가 지평선 안개와 섞이게
Image.fromarray(np.clip(canvas, 0, 255).astype(np.uint8)).save(out_tex, quality=88)
print('saved', out_tex, 'grass mean', grass.reshape(-1, 3).mean(axis=0).round(1))

# ---------- 나무 ----------
print('trees…')
blds = json.load(open(bld_path))['elements']
bld_m = new_mask(); d = ImageDraw.Draw(bld_m)
for e in blds:
    for role, pts in rings(e):
        if len(pts) >= 3:
            d.polygon(pts, fill=255)

def arr(m):
    return np.asarray(m) > 0

def grow(m, meters):
    k = int(meters * PPM) | 1
    return m.filter(ImageFilter.MaxFilter(k if k < 99 else 99))

blocked = arr(grow(bld_m, 4)) | arr(grow(stroke_mask(ROAD_W, 3), 1)) | arr(grow(walk_m, 1.5)) \
          | arr(grow(paved_m, 2)) | arr(grow(water_m, 2)) | arr(grow(pitch_m, 3)) | arr(grow(track_m, 3)) | arr(grow(square_m, 2))
forest_a, park_a = arr(forest_m), arr(park_m)

trees = []
def try_add(px, py, kind_weights):
    ix, iy = int(px), int(py)
    if ix < 8 or iy < 8 or ix >= W - 8 or iy >= H - 8 or blocked[iy, ix]:
        return
    x, z = to_world(px, py)
    kind = random.choices(['cone', 'round', 'cherry'], kind_weights)[0]
    s = random.uniform(0.8, 1.25)
    trees.append([round(x, 2), round(z, 2), kind, round(s, 2), random.randint(0, 359)])

def scatter(spacing_m, where, prob, weights):
    step = spacing_m * PPM
    for gy in np.arange(step / 2, H, step):
        for gx in np.arange(step / 2, W, step):
            px = gx + random.uniform(-0.45, 0.45) * step
            py = gy + random.uniform(-0.45, 0.45) * step
            ix, iy = int(min(max(px, 0), W - 1)), int(min(max(py, 0), H - 1))
            if where(ix, iy) and random.random() < prob:
                try_add(px, py, weights)

scatter(14, lambda x, y: forest_a[y, x], 0.75, [0.7, 0.25, 0.05])
scatter(15, lambda x, y: park_a[y, x] and not forest_a[y, x], 0.45, [0.3, 0.15, 0.55])
scatter(24, lambda x, y: not park_a[y, x] and not forest_a[y, x], 0.2, [0.45, 0.2, 0.35])

# 가로수: 차도 양옆으로 줄지어 (벚꽃 비율 높게)
for hw, pts in lines({k: v for k, v in ROAD_W.items() if k != 'service'}):
    off = (ROAD_W[hw] / 2 + 4) * PPM
    step = 20 * PPM
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        L = math.hypot(x1 - x0, y1 - y0)
        if L < 1:
            continue
        nx, ny = -(y1 - y0) / L, (x1 - x0) / L
        n = int(L // step)
        for i in range(n):
            t = (i + 0.5) / max(n, 1)
            cx, cy = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            for sgn in (1, -1):
                try_add(cx + nx * off * sgn, cy + ny * off * sgn, [0.2, 0.1, 0.7])

# 대운동장 둘레 벚꽃길: 트랙 바깥 6m 에 10m 간격
edge = np.argwhere(np.asarray(grow(track_m, 6)) > 0)
ring = np.asarray(grow(track_m, 6)).astype(bool) & ~np.asarray(grow(track_m, 5)).astype(bool)
pts = np.argwhere(ring)
random.shuffle(pts := [tuple(p) for p in pts])
taken = []
for (py, px) in pts:
    if all((px - qx) ** 2 + (py - qy) ** 2 > (10 * PPM) ** 2 for qx, qy in taken):
        taken.append((px, py))
        try_add(px, py, [0.05, 0.05, 0.9])

json.dump({'trees': trees}, open(out_trees, 'w'))
from collections import Counter
print('trees', len(trees), Counter(t[2] for t in trees))
