"""
모든 건물에 키아트 아이콘의 공통 디테일을 덧붙인다.
- 기단(1층 바닥 띠), 코니스(지붕 아래 띠), 옥상 난간, 옥상 계단실·실외기
- 가장 가까운 길 쪽 면에 입구 (캐노피, 기둥, 유리문, 계단) + 길까지 포장
- 건물 둘레 산울타리 (입구, 길, 이웃 건물은 비움)
외벽 자체(창문 줄)는 Unity 의 KUCA/StylizedBuilding 셰이더가 그린다. 여기 색은 그 재질 색과 맞춘다.
"""
import json
import math
import random

from .geo import Footprint, dist_to_poly_edge, point_in_poly
from .mesh import Frame, IDENT, offset_polygon, outward_normal, shade

# 건물 id → 외벽 스타일 (Unity 재질 KeyArtBuilding_<스타일>)
STYLES = {
    'way-474085534': 'Classical',  # 중앙도서관
    'way-474085536': 'Classical',  # 예술디자인대학
    'way-474521123': 'Classical',  # 도예관
    'way-455726113': 'Classical',  # 체육대학관
    'way-585696506': 'Classical',  # 선승관
    'way-474521125': 'Classical',  # 천문대
    'way-455725718': 'Modern',     # 공학관
    'way-455728922': 'Modern',     # 공학실험동
    'way-474085540': 'Modern',     # 전자정보대학
    'way-474085537': 'Glass',      # 멀티미디어교육관
    'way-474085538': 'Glass',      # 글로벌관
    'relation-8269760': 'Brick',   # 우정원
}

# 스타일별 색: 벽·띠는 KeyArtLook.json (셰이더 재질과 같은 값), 기단은 따로
_BASE = {'Default': (0.80, 0.76, 0.69), 'Classical': (0.82, 0.77, 0.68), 'Modern': (0.74, 0.75, 0.75),
         'Glass': (0.72, 0.74, 0.76), 'Brick': (0.62, 0.56, 0.50)}
PALETTE = {}


def load_look(path):
    look = json.load(open(path, encoding='utf-8'))
    for name, st in look['styles'].items():
        PALETTE[name] = {'wall': tuple(st['wall']), 'trim': tuple(st['trim']), 'base': _BASE.get(name, _BASE['Default'])}
    return look


GLASS_DOOR = (0.24, 0.33, 0.42)
ROOF_UNIT = (0.80, 0.81, 0.83)
FAN = (0.30, 0.33, 0.37)
STEP = (0.86, 0.84, 0.79)
HEDGE = (0.30, 0.55, 0.19)


def style_of(b):
    return STYLES.get(b.id, 'Default')


def plan_entrances(buildings, ground, skip_ids):
    """건물마다 입구 (x, z, 바깥 법선, 변 번호). 길까지 포장도 ground 에 등록"""
    out = {}
    for b in buildings:
        if b.id in skip_ids or b.h < 6 or b.area < 120 or b.id in out:
            continue
        cx, cz = b.centroid
        target = ground.nearest_walk_point(cx, cz, max_dist=80)
        best, pick = -1e9, None
        n = len(b.poly)
        for i in range(n):
            a, c = b.poly[i], b.poly[(i + 1) % n]
            L = math.hypot(c[0] - a[0], c[1] - a[1])
            if L < 8:
                continue
            nx, nz = outward_normal(b.poly, i)
            mx, mz = (a[0] + c[0]) / 2, (a[1] + c[1]) / 2
            if target:
                dx, dz = target[0] - mx, target[1] - mz
                dl = math.hypot(dx, dz) or 1
                score = (nx * dx + nz * dz) / dl * 2 - dl / 40 + min(L, 30) / 30
            else:
                score = min(L, 60) / 60 - nz   # 길이 없으면 남쪽 긴 면
            if score > best:
                best, pick = score, (i, mx, mz, nx, nz)
        if pick is None:
            continue
        i, mx, mz, nx, nz = pick
        out[b.id] = {'x': mx, 'z': mz, 'n': (nx, nz), 'edge': i}
        end = ground.nearest_walk_point(mx + nx * 4, mz + nz * 4, max_dist=35)
        if end:
            ground.add_apron((mx + nx * 1.0, mz + nz * 1.0), end, 5.0)
        else:
            ground.add_apron((mx + nx * 1.0, mz + nz * 1.0), (mx + nx * 9, mz + nz * 9), 5.0)
    return out


def build_details(buildings, layer, entrances, skip_ids, rng):
    """기단·코니스·난간·옥상 설비·입구"""
    for b in buildings:
        if b.id in skip_ids or b.h < 3.5 or b.min_h > 0.5:
            continue
        pal = PALETTE[style_of(b)]
        cx, cz = b.centroid
        mb = layer.at(cx, cz)
        H = b.h
        small = b.area < 80
        mb.ao_floor, mb.ao_strength = 0.0, 0.25
        # 기단: 바깥으로 0.35 m, 높이 0.9 m
        mb.band(b.poly, 0.0, 0.9, 0.35, 0.0, pal['base'], top_col=shade(pal['base'], 1.12))
        mb.ao_strength = 0.0
        if not small:
            # 코니스 (지붕 1 m 아래 내민 띠)
            mb.band(b.poly, H - 1.1, H - 0.55, 0.32, 0.0, pal['trim'])
        # 옥상 난간: 지붕 위 1.0 m, 안쪽 두께 0.45 m
        par_h = 0.6 if small else 1.0
        mb.band(b.poly, H - 0.05, H + par_h, 0.12, 0.45, pal['trim'], top_col=shade(pal['trim'], 1.04),
                inner_col=shade(pal['wall'], 0.86))
        if not small:
            _rooftop(mb, b, rng)
        ent = entrances.get(b.id)
        if ent:
            _entrance(mb, b, ent, pal)


def _rooftop(mb, b, rng):
    """계단실 1개 + 실외기 여러 대 (옥상 안쪽 3 m 이상 떨어진 곳)"""
    fp = Footprint(b.poly, b.h)
    H = b.h
    mb.ao_floor, mb.ao_height, mb.ao_strength = H, 1.2, 0.22
    inner = offset_polygon(b.poly, -2.5)

    def fits(x, z, r):
        return point_in_poly(x, z, b.poly) and dist_to_poly_edge(x, z, b.poly) > r + 1.5

    u, v = fp.u, fp.v
    yaw = math.degrees(math.atan2(u[0], u[1]))
    if fp.half_u > 7 and fp.half_v > 5:
        off = rng.uniform(-0.3, 0.3) * fp.half_u
        px, pz = fp.center[0] + u[0] * off, fp.center[1] + u[1] * off
        w, d = min(10.0, fp.half_u * 0.5), min(6.5, fp.half_v * 0.7)
        if fits(px, pz, max(w, d) / 2):
            m = Frame.yaw((px, H, pz), yaw)
            mb.box(m, (0, 1.7, 0), (d, 3.4, w), (0.95, 0.93, 0.88))
            mb.box(m, (0, 3.55, 0), (d + 0.4, 0.3, w + 0.4), (0.99, 0.98, 0.95))
    n_units = int(min(9, b.area / 260))
    tries = 0
    placed = []
    while len(placed) < n_units and tries < n_units * 12:
        tries += 1
        su, sv = rng.uniform(-1, 1) * fp.half_u, rng.uniform(-1, 1) * fp.half_v
        x = fp.center[0] + u[0] * su + v[0] * sv
        z = fp.center[1] + u[1] * su + v[1] * sv
        if not fits(x, z, 1.6) or any(math.hypot(x - p[0], z - p[1]) < 3.2 for p in placed):
            continue
        placed.append((x, z))
        m = Frame.yaw((x, H, z), yaw)
        mb.box(m, (0, 0.7, 0), (2.2, 1.4, 1.6), ROOF_UNIT)
        mb.box(m, (0, 1.42, 0), (1.1, 0.06, 1.1), FAN, top=FAN)
    mb.ao_floor, mb.ao_height = 0.0, 2.0


def _entrance(mb, b, ent, pal):
    """캐노피 입구: 로컬 +Z 가 바깥"""
    x, z, (nx, nz) = ent['x'], ent['z'], ent['n']
    m = Frame.look((x, 0.0, z), (nx, 0.0, nz))
    trim = pal['trim']
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 1.5, 0.2
    w = 8.0 if b.area > 600 else 6.0
    # 문틀과 유리문 (벽에서 살짝 앞으로)
    mb.box(m, (0, 1.75, 0.18), (w * 0.62, 3.5, 0.36), trim)
    mb.box(m, (0, 1.55, 0.38), (w * 0.5, 3.1, 0.08), GLASS_DOOR, top=GLASS_DOOR)
    mb.box(m, (0, 1.55, 0.43), (0.12, 3.1, 0.04), trim)
    # 캐노피와 기둥
    mb.box(m, (0, 3.95, 1.9), (w, 0.5, 3.8), trim, top=shade(trim, 1.02))
    for s in (-1, 1):
        mb.prism(m, (s * (w / 2 - 0.5), 0.3, 3.4), 0.22, 3.4, 6, trim)
    # 계단 2단
    mb.box(m, (0, 0.15, 2.4), (w + 0.6, 0.3, 4.6), STEP)
    mb.box(m, (0, 0.38, 1.6), (w * 0.8, 0.16, 3.0), shade(STEP, 1.04))
    mb.ao_strength = 0.25


def build_hedges(buildings, layer, ground, entrances, skip_ids, rng):
    """건물 둘레 2.6 m 바깥에 산울타리 (모서리·입구·막힌 곳 비움)"""
    count = 0
    for b in buildings:
        if b.id in skip_ids or b.h < 5 or b.area < 150:
            continue
        ring = offset_polygon(b.poly, 2.6)
        ent = entrances.get(b.id)
        n = len(ring)
        for i in range(n):
            a, c = ring[i], ring[(i + 1) % n]
            L = math.hypot(c[0] - a[0], c[1] - a[1])
            if L < 6:
                continue
            ux, uz = (c[0] - a[0]) / L, (c[1] - a[1]) / L
            # 1 m 간격으로 비었는지 보고, 이어진 구간마다 산울타리 한 덩어리
            run = []
            s = 1.8
            while s <= L - 1.8:
                px, pz = a[0] + ux * s, a[1] + uz * s
                ok = ground.is_free(px, pz, 0.5, near_building=True) and ground.inside(px, pz)
                if ent and math.hypot(px - ent['x'], pz - ent['z']) < 7.5:
                    ok = False
                if ok:
                    run.append(s)
                if (not ok or s + 1.0 > L - 1.8) and len(run) >= 3:
                    s0, s1 = run[0], run[-1]
                    _hedge(layer, ground, (a[0] + ux * s0, a[1] + uz * s0), (a[0] + ux * s1, a[1] + uz * s1), rng)
                    count += 1
                if not ok:
                    run = []
                s += 1.0
    return count


def _hedge(layer, ground, p0, p1, rng):
    L = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
    mx, mz = (p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2
    yaw = math.degrees(math.atan2(p1[0] - p0[0], p1[1] - p0[1]))
    mb = layer.at(mx, mz)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 1.0, 0.3
    col = shade(HEDGE, rng.uniform(0.95, 1.06))
    m = Frame.yaw((mx, 0.0, mz), yaw)
    h = 1.0 + rng.uniform(-0.08, 0.08)
    mb.bevel_box(m, (0, h / 2, 0), (1.25, h, L), 0.28, col)
    mb.ao_strength = 0.25
    ground.occupy_line(p0, p1, 1.6, ao=0.45)
