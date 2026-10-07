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
from .mesh import Frame, IDENT, extrude_poly, offset_polygon, outward_normal, rect_poly, shade

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
FLOOR_H = {}
_BASE = {'Outside': (0.82, 0.83, 0.84), 'Default': (0.80, 0.76, 0.69), 'Classical': (0.82, 0.77, 0.68), 'Modern': (0.74, 0.75, 0.75),
         'Glass': (0.72, 0.74, 0.76), 'Brick': (0.62, 0.56, 0.50)}
PALETTE = {}


def load_look(path):
    look = json.load(open(path, encoding='utf-8'))
    for name, st in look['styles'].items():
        FLOOR_H[name] = st['floorHeight']
        PALETTE[name] = {'wall': tuple(st['wall']), 'trim': tuple(st['trim']), 'base': _BASE.get(name, _BASE['Default'])}
    return look


GLASS_DOOR = (0.24, 0.33, 0.42)
ROOF_UNIT = (0.80, 0.81, 0.83)
FAN = (0.30, 0.33, 0.37)
STEP = (0.86, 0.84, 0.79)
HEDGE = (0.30, 0.55, 0.19)
FLOWERS = [(0.98, 0.62, 0.76), (1.0, 0.92, 0.94), (0.99, 0.84, 0.34), (0.40, 0.66, 0.24)]


OUTSIDE_IDS = set()   # 캠퍼스 경계 밖 건물 (build_art 이 채움): 차분한 단순형


def style_of(b):
    if b.id in OUTSIDE_IDS:
        return 'Outside'
    return STYLES.get(b.id, 'Default')


def build_shells(buildings, shells, skip_ids):
    """모든 건물 외벽 덩어리 (둥근 모서리 다각형 그대로 돌출). Unity 의 상자 건물 대신 보인다."""
    for b in buildings:
        if b.id in skip_ids or b.h <= b.min_h + 0.5:
            continue
        cx, cz = b.centroid
        sh = shells[style_of(b)].at(cx, cz)
        sh.ao_strength = 0.0
        extrude_poly(sh, b.poly, b.min_h, b.h)
        if b.min_h > 0.5:   # 떠 있는 건물(통로 등) 아랫면
            from .mesh import triangulate
            for ia, ib, ic in triangulate(b.poly):
                a, bb, c = b.poly[ia], b.poly[ib], b.poly[ic]
                sh.tri(IDENT, (a[0], b.min_h, a[1]), (bb[0], b.min_h, bb[1]), (c[0], b.min_h, c[1]), (1, 1, 1),
                       ((a[0] + bb[0] + c[0]) / 3, b.min_h + 1, (a[1] + bb[1] + c[1]) / 3))


def plan_entrances(buildings, ground, skip_ids):
    """건물마다 입구 (x, z, 바깥 법선, 변 번호). 길까지 포장도 ground 에 등록"""
    out = {}
    for b in buildings:
        if b.id in skip_ids or b.id in OUTSIDE_IDS or b.h < 6 or b.area < 120 or b.id in out:
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


HEIGHT_SCALE = 1.0     # build_art 이 KeyArtLook.json 의 buildingHeightScale 로 바꾼다
GLASS = (0.42, 0.62, 0.80)
SOLAR = (0.20, 0.28, 0.46)
GARDEN = (0.42, 0.66, 0.24)
SOIL = (0.55, 0.42, 0.30)


def floor_height(style):
    return FLOOR_H.get(style, 3.4) * HEIGHT_SCALE


def build_details(buildings, layer, shells, entrances, skip_ids, rng):
    """기단·코니스·난간, 지붕 단차(덩어리), 옥상 설비·천창·태양광·옥상정원, 입구"""
    for b in buildings:
        if b.id in skip_ids or b.h < 3.5 or b.min_h > 0.5:
            continue
        style = style_of(b)
        pal = PALETTE[style]
        cx, cz = b.centroid
        mb = layer.at(cx, cz)
        H = b.h
        if style == 'Outside':
            # 캠퍼스 밖: 둥근 난간 하나만 (조용한 배경)
            mb.ao_strength = 0.0
            mb.band(b.poly, H - 0.05, H + 0.7, 0.1, 0.35, pal['trim'], top_col=shade(pal['trim'], 1.03),
                    inner_col=shade(pal['wall'], 0.9))
            continue
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
        blocks = [] if small else _massing(mb, shells[style].at(cx, cz), b, style, pal, rng)
        if not small:
            _rooftop(mb, b, style, blocks, rng)
        ent = entrances.get(b.id)
        if ent:
            _entrance(mb, b, ent, pal)


def _rect_inside(poly, rect, margin):
    from .geo import point_in_poly as pip
    cx = sum(p[0] for p in rect) / 4
    cz = sum(p[1] for p in rect) / 4
    pts = rect + [(cx, cz)] + [((rect[i][0] + rect[(i + 1) % 4][0]) / 2, (rect[i][1] + rect[(i + 1) % 4][1]) / 2) for i in range(4)]
    return all(pip(x, z, poly) and dist_to_poly_edge(x, z, poly) >= margin for x, z in pts[:4]) and \
        all(pip(x, z, poly) for x, z in pts)


# 실제 건물 구조에 맞춘 덩어리 (기본 규칙 대신)
#   center: 가운데 몸체 한 층 위 (공학관: 몸체 + 양팔)
#   ends:   긴 축 양 끝을 반 층 위 (외국어대학관: 원통 두 개 + 막대 동, 반 층 엇갈림)
#   none:   덩어리 없음 (랜드마크가 따로 처리)
MASSING = {
    'way-455725718': 'center',
    'way-585696507': 'ends',
    'way-585696506': 'none',      # 선승관: 배럴 볼트 체육관 지붕 (landmarks)
    'way-474085534': 'center',    # 중앙도서관: 가운데 아트리움 블록
}


def _massing(mb, shell, b, style, pal, rng):
    """
    넓은 평지붕을 나눈다: 긴 건물은 양 끝 파빌리온을, 넓은 건물은 가운데 블록을 한 층 높인다
    (컨셉아트 아이콘의 단차 있는 덩어리). 외벽은 같은 셰이더 재질 (shells 레이어).
    """
    if b.area < 700 or b.h < 8:
        return []
    fp = Footprint(b.poly, b.h)
    fh = floor_height(style)
    H = b.h
    (ax, az), L, Wd = fp.long_axis()
    if fp.half_u >= fp.half_v:
        u, v, hu, hv = fp.u, fp.v, fp.half_u, fp.half_v
    else:
        u, v, hu, hv = fp.v, fp.u, fp.half_v, fp.half_u
    shell.ao_strength = 0.0
    blocks = []
    cands = []
    mode = MASSING.get(b.id)
    if mode == 'none':
        return []
    if mode == 'ends':
        # 긴 축 양 끝 18% 를 잘라 반 층 높인다 (실제 외곽선 그대로)
        from .mesh import clip_half_plane
        for sgn in (-1, 1):
            axis = (u[0] * sgn, u[1] * sgn)
            part = clip_half_plane(b.poly, fp.center, axis, hu * 0.64)
            if len(part) >= 3:
                top = H + fh * 0.5
                extrude_poly(shell, part, H - 0.1, top)
                mb.band(part, top - 0.05, top + 0.8, 0.1, 0.4, pal['trim'], top_col=shade(pal['trim'], 1.04),
                        inner_col=shade(pal['wall'], 0.86))
                blocks.append((part, top))
        return blocks
    if mode == 'center':
        cands.append((rect_poly(fp.center, u, v, hu * 0.3, hv * 0.6), 1))
    elif L > 2.0 * Wd and L > 45:
        pl = min(L * 0.13, 11.0)
        for s in (-1, 1):
            c = (fp.center[0] + u[0] * s * (hu - pl - 0.6), fp.center[1] + u[1] * s * (hu - pl - 0.6))
            cands.append((rect_poly(c, u, v, pl, hv - 0.6), 1))
    if mode is None and (not cands or b.area > 2500):
        cands.append((rect_poly(fp.center, u, v, hu * 0.42, hv * 0.55), 2 if b.area > 4000 and H < 40 else 1))
    for rect, floors in cands:
        if not _rect_inside(b.poly, rect, 0.5):
            continue
        top = H + fh * floors
        extrude_poly(shell, rect, H - 0.1, top)
        mb.ao_strength = 0.0
        mb.band(rect, top - 1.0, top - 0.55, 0.28, 0.0, pal['trim'])
        mb.band(rect, top - 0.05, top + 0.8, 0.1, 0.4, pal['trim'], top_col=shade(pal['trim'], 1.04),
                inner_col=shade(pal['wall'], 0.86))
        blocks.append((rect, top))
    return blocks


def _rooftop(mb, b, style, blocks, rng):
    """계단실, 실외기, 천창 줄, 태양광 패널, 옥상정원 (난간 안쪽, 블록과 겹치지 않게)"""
    fp = Footprint(b.poly, b.h)
    H = b.h
    mb.ao_floor, mb.ao_height, mb.ao_strength = H, 1.2, 0.22

    def free(x, z, r):
        if not (point_in_poly(x, z, b.poly) and dist_to_poly_edge(x, z, b.poly) > r + 1.5):
            return False
        for rect, _ in blocks:
            if point_in_poly(x, z, rect) or dist_to_poly_edge(x, z, rect) < r + 1.0:
                return False
        return True

    u, v = fp.u, fp.v
    yaw = math.degrees(math.atan2(u[0], u[1]))

    def at(su, sv):
        return fp.center[0] + u[0] * su + v[0] * sv, fp.center[1] + u[1] * su + v[1] * sv

    # 계단실
    if fp.half_u > 7 and fp.half_v > 5:
        off = rng.uniform(-0.6, 0.6) * fp.half_u
        px, pz = at(off, rng.uniform(-0.3, 0.3) * fp.half_v)
        w, d = min(9.0, fp.half_u * 0.5), min(6.0, fp.half_v * 0.7)
        if free(px, pz, max(w, d) / 2):
            m = Frame.yaw((px, H, pz), yaw)
            mb.box(m, (0, 1.7, 0), (d, 3.4, w), (0.95, 0.93, 0.88))
            mb.box(m, (0, 3.55, 0), (d + 0.4, 0.3, w + 0.4), (0.99, 0.98, 0.95))
            mb.box(m, (d / 2 + 0.03, 1.1, w * 0.25), (0.06, 2.2, 1.2), GLASS_DOOR, top=GLASS_DOOR)

    # 큰 지붕: 하나를 골라 천창 / 태양광 / 옥상정원
    big = b.area > 900 and fp.half_u > 12 and fp.half_v > 9
    feature = None
    if big:
        feature = rng.choices(['skylight', 'solar', 'garden', None], [0.4, 0.3, 0.2, 0.1] if style != 'Classical' else [0.2, 0.0, 0.5, 0.3])[0]
    if feature == 'skylight':
        rows = max(1, min(4, int(fp.half_v / 5)))
        length = min(fp.half_u * 0.9, 16.0)
        for k in range(rows):
            sv = (k - (rows - 1) / 2) * 4.5
            for sgn in (-1, 1):
                x, z = at(sgn * (length / 2 + 2.5), sv)
                if free(x, z, 1.5) and free(*at(sgn * 2.5, sv), 1.5) and free(*at(sgn * (length + 2.5), sv), 1.5):
                    m = Frame.yaw((x, H, z), yaw)
                    mb.box(m, (0, 0.3, 0), (2.2, 0.6, length + 0.4), (0.92, 0.92, 0.90))
                    mb.gable(m, (0, 0.6, 0), 1.9, length, 0.8, GLASS, True, end_col=(0.92, 0.92, 0.90))
    elif feature == 'solar':
        # 남쪽(-Z)을 향해 기운 패널 줄
        nx = max(1, min(5, int(fp.half_u / 4)))
        nz = max(1, min(4, int(fp.half_v / 4)))
        for i in range(nx):
            for j in range(nz):
                x, z = at((i - (nx - 1) / 2) * 5.5, (j - (nz - 1) / 2) * 4.0)
                if free(x, z, 2.2):
                    m = Frame.yaw((x, H, z), 180)
                    _solar_panel(mb, m)
    elif feature == 'garden':
        gw, gd = min(fp.half_u * 0.6, 14.0), min(fp.half_v * 0.6, 9.0)
        x, z = at(rng.uniform(-0.2, 0.2) * fp.half_u, 0)
        if all(free(*at(su, sv), 0.5) for su in (-gw, gw) for sv in (-gd, gd)) and free(x, z, 1.0):
            m = Frame.yaw((x, H, z), yaw)
            mb.box(m, (0, 0.25, 0), (gd * 2 + 0.6, 0.5, gw * 2 + 0.6), (0.86, 0.84, 0.80))
            mb.box(m, (0, 0.45, 0), (gd * 2, 0.2, gw * 2), GARDEN, top=GARDEN)
            for k in range(int(gw * gd / 12)):
                lx, lz = rng.uniform(-gd + 1, gd - 1), rng.uniform(-gw + 1, gw - 1)
                col = rng.choice([(0.34, 0.60, 0.20), (0.40, 0.66, 0.23), (0.98, 0.72, 0.82)])
                mb.ico(m, (lx, 0.95, lz), (0.9, 0.6, 0.9), col)

    # 실외기
    n_units = int(min(8, b.area / 300))
    tries, placed = 0, []
    while len(placed) < n_units and tries < n_units * 12:
        tries += 1
        x, z = at(rng.uniform(-1, 1) * fp.half_u, rng.uniform(-1, 1) * fp.half_v)
        if not free(x, z, 1.6) or any(math.hypot(x - p[0], z - p[1]) < 3.2 for p in placed):
            continue
        placed.append((x, z))
        m = Frame.yaw((x, H, z), yaw)
        mb.box(m, (0, 0.7, 0), (2.2, 1.4, 1.6), ROOF_UNIT)
        mb.box(m, (0, 1.42, 0), (1.1, 0.06, 1.1), FAN, top=FAN)
    mb.ao_floor, mb.ao_height = 0.0, 2.0


def _solar_panel(mb, m):
    """기운 태양광 패널 (로컬 +Z 쪽이 낮음)"""
    w, d, lo, hi = 4.6, 2.4, 0.5, 1.6
    a, b, c, e = (-w / 2, lo, d / 2), (w / 2, lo, d / 2), (w / 2, hi, -d / 2), (-w / 2, hi, -d / 2)
    under = (0, 0.0, 0)
    mb.quad(m, a, b, c, e, SOLAR, under)
    mb.quad(m, (-w / 2, 0, -d / 2), (w / 2, 0, -d / 2), c, e, (0.70, 0.71, 0.73), (0, 0.5, 0.5))
    for s in (-1, 1):
        mb.tri(m, (s * w / 2, 0, d / 2), (s * w / 2, lo, d / 2), (s * w / 2, hi, -d / 2), (0.75, 0.76, 0.78), (0, 0.5, 0))
        mb.tri(m, (s * w / 2, 0, d / 2), (s * w / 2, hi, -d / 2), (s * w / 2, 0, -d / 2), (0.75, 0.76, 0.78), (0, 0.5, 0))
    # 셀 줄 (밝은 선)
    for k in (-1, 0, 1):
        x = k * w / 4
        mb.quad(m, (x - 0.04, lo + 0.02, d / 2), (x + 0.04, lo + 0.02, d / 2), (x + 0.04, hi + 0.02, -d / 2), (x - 0.04, hi + 0.02, -d / 2),
                (0.55, 0.62, 0.78), under)


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
    # 계단 양옆 화단 (석재 화분 + 꽃)
    for s in (-1, 1):
        x0 = s * (w / 2 + 1.9)
        mb.ao_strength = 0.2
        mb.box(m, (x0, 0.35, 2.2), (2.4, 0.7, 3.4), (0.88, 0.85, 0.79))
        mb.ao_strength = 0.0
        mb.box(m, (x0, 0.72, 2.2), (2.1, 0.06, 3.1), (0.45, 0.33, 0.24), top=(0.45, 0.33, 0.24))
        for k in range(3):
            fx = x0 + ((k * 37) % 5 - 2) * 0.38
            fz = 2.2 + ((k * 53) % 7 - 3) * 0.42
            mb.ico(m, (fx, 0.95, fz), (0.42, 0.32, 0.42), FLOWERS[(k + (s > 0)) % len(FLOWERS)])
    mb.ao_strength = 0.25


def build_hedges(buildings, layer, ground, entrances, skip_ids, rng):
    """건물 둘레 2.6 m 바깥에 산울타리 (모서리·입구·막힌 곳 비움)"""
    count = 0
    for b in buildings:
        if b.id in skip_ids or b.id in OUTSIDE_IDS or b.h < 5 or b.area < 150:
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
