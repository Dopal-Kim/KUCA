"""
나무·관목. 컨셉아트처럼 각진 원뿔형 침엽수, 둥근 활엽수, 분홍 벚나무, 작은 원뿔·공 모양 관목.
배치: OSM 숲·공원·잔디, 차도 가로수(벚꽃), 대운동장 둘레 벚꽃길, 건물 모서리 정원수, 지도 바깥 숲 띠.
"""
import math

import numpy as np

from .geo import MAP_H, MAP_W, dist_to_poly_edge
from .ground import ROAD_W
from .mesh import Frame, mix, offset_polygon, shade

TRUNK = (0.50, 0.36, 0.25)
EVERGREEN = [(0.28, 0.56, 0.19), (0.33, 0.62, 0.22), (0.24, 0.50, 0.17), (0.38, 0.66, 0.24)]
LEAFY = [(0.46, 0.71, 0.24), (0.52, 0.76, 0.29), (0.41, 0.66, 0.21)]
BLOSSOM = [(0.98, 0.76, 0.84), (0.96, 0.68, 0.79), (1.00, 0.84, 0.90), (0.99, 0.80, 0.86)]
SHRUB = [(0.34, 0.60, 0.20), (0.40, 0.66, 0.23), (0.30, 0.56, 0.19)]
FLOWER = [(0.98, 0.62, 0.76), (1.0, 0.94, 0.95), (0.99, 0.85, 0.36)]

# 종류: (차지 반지름 m, 바닥 그늘 반지름 m, 그늘 세기)
KINDS = {'cone_lo': (2.6, 2.8, 0.42), 'round_lo': (3.0, 3.2, 0.40), 'cone': (2.6, 2.8, 0.42), 'round': (3.0, 3.2, 0.40), 'cherry': (2.9, 3.0, 0.36),
         'small_cone': (1.3, 1.5, 0.35), 'shrub': (1.2, 1.5, 0.32)}


def draw_tree(mb, kind, x, z, s, rot, rng):
    pick = lambda c: c[rng.randrange(len(c))]
    m = Frame.yaw((x, 0.0, z), rot, s)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 2.2, 0.35
    jit = [rng.uniform(0.86, 1.12) for _ in range(7)]
    if kind == 'cone':
        base = pick(EVERGREEN)
        mb.prism(m, (0, 0, 0), 0.32, 1.6, 5, TRUNK)
        mb.ao_strength = 0.0
        mb.cone(m, (0, 1.1, 0), 3.0, 4.4, 7, shade(base, 0.92), rot=rng.uniform(0, 50), jitter=jit, bottom=False)
        mb.cone(m, (0, 3.4, 0), 2.35, 3.9, 7, base, rot=rng.uniform(0, 50), jitter=jit[::-1], bottom=False)
        mb.cone(m, (0, 5.5, 0), 1.65, 3.3, 7, shade(mix(base, (0.55, 0.78, 0.30), 0.25), 1.04), rot=rng.uniform(0, 50), bottom=False)
    elif kind == 'round':
        base = pick(LEAFY)
        mb.prism(m, (0, 0, 0), 0.38, 2.6, 5, TRUNK, r_top=0.28)
        mb.ao_strength = 0.0
        mb.ico(m, (0, 4.7, 0), (3.0, 2.6, 3.0), base)
        mb.ico(m, (1.3, 5.6, 0.5), (1.9, 1.7, 1.9), shade(mix(base, (0.62, 0.82, 0.32), 0.3), 1.03))
    elif kind == 'cherry':
        mb.prism(m, (0, 0, 0), 0.36, 2.3, 5, TRUNK, r_top=0.26)
        mb.prism(m, (0.2, 2.1, 0), 0.18, 1.2, 5, TRUNK)
        mb.ao_strength = 0.0
        mb.ico(m, (0, 4.4, 0), (2.7, 2.1, 2.7), pick(BLOSSOM))
        mb.ico(m, (1.5, 3.8, 0.6), (1.8, 1.5, 1.8), pick(BLOSSOM))
        mb.ico(m, (-1.3, 3.9, -0.8), (1.8, 1.4, 1.8), pick(BLOSSOM))
    elif kind == 'small_cone':
        base = pick(EVERGREEN)
        mb.prism(m, (0, 0, 0), 0.18, 0.6, 4, TRUNK)
        mb.ao_strength = 0.0
        mb.cone(m, (0, 0.4, 0), 1.25, 3.0, 6, shade(base, 1.02), rot=rng.uniform(0, 60), jitter=jit, bottom=False)
    elif kind == 'cone_lo':
        # 멀리 있는 숲·지도 바깥용: 줄기 없이 원뿔 두 단 (정점 36개)
        base = pick(EVERGREEN)
        mb.ao_strength = 0.3
        mb.cone(m, (0, 0.0, 0), 3.0, 5.6, 6, shade(base, 0.92), rot=rng.uniform(0, 60), jitter=jit, bottom=False)
        mb.ao_strength = 0.0
        mb.cone(m, (0, 3.6, 0), 2.1, 4.4, 6, base, rot=rng.uniform(0, 60), bottom=False)
    elif kind == 'round_lo':
        base = pick(LEAFY)
        mb.prism(m, (0, 0, 0), 0.38, 2.6, 4, TRUNK, r_top=0.28)
        mb.ao_strength = 0.0
        mb.ico(m, (0, 4.7, 0), (3.1, 2.7, 3.1), base)
    else:  # shrub
        base = pick(SHRUB)
        mb.ao_strength = 0.3
        mb.ao_height = 1.0
        mb.ico(m, (0, 0.7, 0), (1.25, 0.95, 1.25), base)
        if rng.random() < 0.22:   # 꽃 핀 관목
            fc = pick(FLOWER)
            for k in range(3):
                a = rng.uniform(0, 6.28)
                mb.ico(m, (math.cos(a) * 0.7, 1.35, math.sin(a) * 0.7), (0.28, 0.22, 0.28), fc, var=0.0)
    mb.ao_strength, mb.ao_height = 0.25, 2.0


class Planter:
    def __init__(self, layer, ground, rng):
        self.layer, self.g, self.rng = layer, ground, rng
        self.counts = {}

    def add(self, x, z, kind, scale=None, ignore_block=False):
        rad, ao_r, ao = KINDS[kind]
        s = scale if scale is not None else self.rng.uniform(0.85, 1.25)
        g = self.g
        if not ignore_block and not g.is_free(x, z, rad * 0.45 * s):
            return False
        if g.inside(x, z, 0):
            g.occupy(x, z, rad * 0.7 * s, ao=ao, ao_radius=ao_r * s)
        draw_tree(self.layer.at(x, z), kind, x, z, s, self.rng.uniform(0, 360), self.rng)
        self.counts[kind] = self.counts.get(kind, 0) + 1
        return True

    def scatter(self, spacing, where, prob, weights, kinds=('cone', 'round', 'cherry')):
        g, rng = self.g, self.rng
        for z in np.arange(-MAP_H / 2 + spacing / 2, MAP_H / 2, spacing):
            for x in np.arange(-MAP_W / 2 + spacing / 2, MAP_W / 2, spacing):
                px = x + rng.uniform(-0.45, 0.45) * spacing
                pz = z + rng.uniform(-0.45, 0.45) * spacing
                if where(px, pz) and rng.random() < prob:
                    self.add(px, pz, rng.choices(kinds, weights)[0])


def plant_all(layer, ground, buildings, rng, skip_ids):
    p = Planter(layer, ground, rng)
    g = ground
    forest = lambda x, z: g.has(g.forest, x, z)
    park = lambda x, z: g.has(g.park, x, z) and not g.has(g.forest, x, z)
    other = lambda x, z: g.inside(x, z) and not g.has(g.park, x, z) and not g.has(g.forest, x, z)

    # 1) 차도 가로수: 양옆 18 m 간격, 벚꽃 위주
    for hw, pts in g.road_lines:
        if hw == 'service':
            continue
        off = ROAD_W[hw] / 2 + 3.8
        for a, b in zip(pts, pts[1:]):
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            if L < 1:
                continue
            nx, nz = -(b[1] - a[1]) / L, (b[0] - a[0]) / L
            n = int(L // 14)
            for i in range(n):
                t = (i + 0.5) / max(n, 1)
                cx, cz = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
                for sgn in (1, -1):
                    kind = rng.choices(['cherry', 'round', 'cone'], [0.75, 0.15, 0.1])[0]
                    p.add(cx + nx * off * sgn, cz + nz * off * sgn, kind, scale=rng.uniform(1.05, 1.3) if kind == 'cherry' else None)

    # 2) 대운동장 둘레 벚꽃길: 트랙 바깥 6 m, 9 m 간격
    if g.track.any():
        from scipy import ndimage
        d = ndimage.distance_transform_edt(~g.track) / g.ppm
        ring = np.argwhere((d > 5.5) & (d < 6.5))
        rng.shuffle(ring_list := [tuple(q) for q in ring[::7]])
        taken = []
        for (py, px) in ring_list:
            x, z = g.r.world(px, py)
            if all((x - qx) ** 2 + (z - qz) ** 2 > 81 for qx, qz in taken):
                if p.add(x, z, 'cherry', scale=rng.uniform(0.95, 1.2)):
                    taken.append((x, z))

    # 3) 건물 모서리 정원수 (컨셉아트 아이콘의 원뿔 나무 무리)
    from .buildings import OUTSIDE_IDS
    for b in buildings:
        if b.id in skip_ids or b.id in OUTSIDE_IDS or b.h < 5 or b.area < 150:
            continue
        ring = offset_polygon(b.poly, 5.5)
        for (x, z) in ring:
            if rng.random() < 0.7:
                for k in range(rng.randint(1, 3)):
                    a = rng.uniform(0, 6.28)
                    p.add(x + math.cos(a) * 2.2 * k, z + math.sin(a) * 2.2 * k,
                          rng.choices(['small_cone', 'cone', 'shrub'], [0.55, 0.25, 0.2])[0], scale=rng.uniform(0.8, 1.1))

    # 4) 숲, 공원, 그 밖의 잔디
    deep = _deep_forest(g)
    p.scatter(11, lambda x, z: forest(x, z) and not g.has(deep, x, z), 0.85, [0.65, 0.3, 0.05])
    p.scatter(11, lambda x, z: g.has(deep, x, z), 0.85, [0.7, 0.3], kinds=('cone_lo', 'round_lo'))
    # 숲 가장자리 층: 바깥 0~5 m 에 관목·작은 원뿔을 촘촘히
    edge = _forest_edge(g)
    p.scatter(4.5, lambda x, z: g.has(edge, x, z), 0.55, [0.55, 0.45], kinds=('shrub', 'small_cone'))
    p.scatter(13, park, 0.5, [0.35, 0.25, 0.4])
    p.scatter(20, other, 0.28, [0.45, 0.25, 0.3])

    # 5) 관목: 길가와 공원에 작은 덤불
    p.scatter(6, lambda x, z: (park(x, z) or other(x, z)) and _near(g, x, z), 0.5, [0.7, 0.3], kinds=('shrub', 'small_cone'))

    # 6) 지도 바깥 숲 띠 (가장자리에서 잔디만 보이지 않게)
    band = 120
    for z in np.arange(-MAP_H / 2 - band, MAP_H / 2 + band, 15):
        for x in np.arange(-MAP_W / 2 - band, MAP_W / 2 + band, 15):
            if abs(x) < MAP_W / 2 + 4 and abs(z) < MAP_H / 2 + 4:
                continue
            if rng.random() < 0.8:
                px, pz = x + rng.uniform(-5, 5), z + rng.uniform(-5, 5)
                p.add(px, pz, rng.choices(['cone_lo', 'round_lo'], [0.7, 0.3])[0], ignore_block=True)
    return p.counts


def _deep_forest(g):
    """숲 안쪽: 길·건물·숲 가장자리에서 25 m 이상 (카메라에서 덩어리로만 보이는 곳)"""
    from scipy import ndimage
    d_edge = ndimage.distance_transform_edt(g.forest) / g.ppm
    d_env = ndimage.distance_transform_edt(~(g.blocked_env | g.buildings)) / g.ppm
    return (d_edge > 25) & (d_env > 25)


def _forest_edge(g):
    from scipy import ndimage
    d_out = ndimage.distance_transform_edt(~g.forest) / g.ppm
    return (d_out > 0.5) & (d_out < 5.0)


def _near(g, x, z):
    """길·포장 옆 4 m 안 (덤불 띠)"""
    ix, iy = g._ix(x, z)
    k = int(4 * g.ppm)
    if not (k <= ix < g.W - k and k <= iy < g.H - k):
        return False
    win = g.blocked_env[iy - k:iy + k + 1:3, ix - k:ix + k + 1:3]
    return bool(win.any())


# ---------- 소품: 가로등, 벤치 ----------

LAMP_POLE = (0.32, 0.34, 0.38)
LAMP_HEAD = (1.0, 0.97, 0.86)
WOOD = (0.66, 0.47, 0.30)
METAL = (0.36, 0.38, 0.42)


def draw_lamp(mb, x, z, face_yaw):
    m = Frame.yaw((x, 0.0, z), face_yaw)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 0.8, 0.2
    mb.prism(m, (0, 0, 0), 0.28, 0.45, 6, LAMP_POLE)
    mb.ao_strength = 0.0
    mb.prism(m, (0, 0.45, 0), 0.13, 4.6, 6, LAMP_POLE, r_top=0.1)
    mb.box(m, (0, 5.0, 0.35), (0.14, 0.12, 0.8), LAMP_POLE)
    mb.box(m, (0, 4.75, 0.75), (0.55, 0.45, 0.55), LAMP_HEAD, top=LAMP_HEAD)
    mb.box(m, (0, 5.05, 0.75), (0.75, 0.14, 0.75), LAMP_POLE)
    mb.ao_strength = 0.25


def draw_bench(mb, x, z, face_yaw):
    """face_yaw: 앉은 사람이 바라보는 방향 (로컬 +Z)"""
    m = Frame.yaw((x, 0.0, z), face_yaw)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 0.6, 0.25
    for s in (-0.75, 0.75):
        mb.box(m, (s, 0.22, 0), (0.12, 0.44, 0.55), METAL)
        mb.box(m, (s, 0.62, -0.26), (0.1, 0.5, 0.08), METAL)
    mb.ao_strength = 0.0
    mb.box(m, (0, 0.47, 0.02), (1.9, 0.08, 0.58), WOOD)
    mb.box(m, (0, 0.78, -0.28), (1.9, 0.32, 0.07), shade(WOOD, 0.95))
    mb.ao_strength = 0.25


def place_props(layer, ground, rng):
    """보행로 옆 가로등(24 m)·벤치(55 m), 차도 옆 가로등(32 m). 길·물·다른 물체와 겹치지 않게"""
    from .ground import WALK_W
    g = ground
    counts = {'lamp': 0, 'bench': 0}

    def spot_ok(x, z, r):
        if not g.inside(x, z, 4):
            return False
        for mask in (g.road, g.curb, g.walk, g.water, g.buildings, g.track, g.pitch, g.square, g.parking, g.paved):
            if g.has(mask, x, z):
                return False
        return True

    def along(lines, widths, step, extra, kind, phase):
        for hw, pts in lines:
            if hw in ('steps', 'track', 'service'):
                continue
            off = widths[hw] / 2 + extra
            dist_next = step * phase
            side = 1
            for a, b in zip(pts, pts[1:]):
                L = math.hypot(b[0] - a[0], b[1] - a[1])
                if L < 0.5:
                    continue
                ux, uz = (b[0] - a[0]) / L, (b[1] - a[1]) / L
                nx, nz = -uz, ux
                t = dist_next
                while t < L:
                    px, pz = a[0] + ux * t + nx * off * side, a[1] + uz * t + nz * off * side
                    if spot_ok(px, pz, 0.8) and not g.has(g.occupied, px, pz):
                        # 길 쪽을 바라보게
                        yaw = math.degrees(math.atan2(-nx * side, -nz * side))
                        if kind == 'lamp':
                            draw_lamp(layer.at(px, pz), px, pz, yaw)
                            g.occupy(px, pz, 0.8, ao=0.25, ao_radius=0.6)
                        else:
                            draw_bench(layer.at(px, pz), px, pz, yaw)
                            g.occupy(px, pz, 1.4, ao=0.3, ao_radius=1.1)
                        counts[kind] += 1
                        side = -side
                    t += step
                dist_next = t - L

    along(g.walk_lines, WALK_W, 24.0, 0.9, 'lamp', 0.3)
    along(g.walk_lines, WALK_W, 55.0, 1.3, 'bench', 0.7)
    along(g.road_lines, ROAD_W, 32.0, 2.6, 'lamp', 0.5)
    return counts
