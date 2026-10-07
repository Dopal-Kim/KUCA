"""
컨셉아트 아이콘(Art/Concept/01_Buildings, 02_Landscapes)의 개성을 살리는 랜드마크 형태.
(이전 Assets/Editor/KeyArtLandmarks.cs 를 옮겨 와 다듬음)
"""
import math

from .geo import Footprint
from .mesh import Frame, IDENT, Builder, shade

STONE = (0.94, 0.91, 0.83)
COLUMN = (0.98, 0.96, 0.90)
STEP = (0.87, 0.85, 0.79)
ROOF = (0.74, 0.76, 0.79)
DOME = (0.97, 0.97, 0.98)
DARK = (0.24, 0.30, 0.38)
GLASS = (0.30, 0.42, 0.52)
GOLD = (0.86, 0.72, 0.40)
POLE = (0.62, 0.64, 0.66)
LAMP = (1.0, 0.98, 0.88)
SEAT = (0.80, 0.82, 0.86)

# 사색의 광장(OSM place=square) 중심, 노천극장 옆 연못 중심 (월드 m)
PLAZA = (94.0, -352.0)
POND = (327.0, -594.0)

LIBRARY, ARTS, PE, SEONSEUNG = 'way-474085534', 'way-474085536', 'way-455726113', 'way-585696506'
OBSERVATORY, CERAMICS, THEATER, GATE = 'way-474521125', 'way-474521123', 'way-474531805', 'way-473963422'

HIDDEN = {THEATER, GATE}          # 상자 건물 대신 이 형태만 보인다 (충돌체·정보는 Unity 에 남김)
NO_DETAILS = {THEATER, GATE, OBSERVATORY, CERAMICS}   # 공통 디테일(난간 등)을 붙이지 않을 건물


def build(buildings, layer, ground):
    by_id = {}
    for b in buildings:
        by_id.setdefault(b.id, b)

    def fp(bid):
        b = by_id.get(bid)
        if b is None:
            print('landmarks: 건물을 찾지 못함', bid)
            return None, None
        return b, Footprint(b.poly, b.h)

    def builder_at(b):
        cx, cz = b.centroid
        mb = layer.at(cx, cz)
        mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 2.0, 0.25
        return mb

    for bid, cols, pediment in ((LIBRARY, 8, True), (ARTS, 6, True), (PE, 8, False)):
        b, f = fp(bid)
        if b:
            face = f.face(PLAZA)
            portico(builder_at(b), face, b.h, pediment, cols, grand=(bid == LIBRARY))
            _occupy_face(ground, face, 16)

    b, f = fp(SEONSEUNG)
    if b:
        face = f.face(PLAZA)
        mb = builder_at(b)
        arcade(mb, face, b.h)
        clock_tower(mb, face, b.h)
        _occupy_face(ground, face, 12)

    b, f = fp(OBSERVATORY)
    if b:
        observatory(builder_at(b), b, f)

    b, f = fp(CERAMICS)
    if b:
        pitched_roof(builder_at(b), b, f)

    b, f = fp(THEATER)
    if b:
        amphitheater(builder_at(b), f, f.face(POND), ground)

    b, f = fp(GATE)
    if b:
        gate(builder_at(b), f)

    # 사색의 광장 오벨리스크 2기: 광장 중심에서 중앙도서관 쪽 축 양옆
    lib = by_id.get(LIBRARY)
    lx, lz = lib.centroid if lib else (-40.0, -352.0)
    tx, tz = lx - PLAZA[0], lz - PLAZA[1]
    L = math.hypot(tx, tz) or 1
    tx, tz = tx / L, tz / L
    sx, sz = -tz, tx
    for s in (-1, 1):
        px, pz = PLAZA[0] + tx * 30 + sx * 13 * s, PLAZA[1] + tz * 30 + sz * 13 * s
        mb = layer.at(px, pz)
        mb.ao_floor, mb.ao_strength = 0.0, 0.25
        obelisk(mb, (px, 0.0, pz))
        ground.occupy(px, pz, 5, ao=0.18, ao_radius=5.0)

    if ground.stadium_box:
        stadium(layer, ground)


def _occupy_face(ground, face, depth):
    c, n, t = face['center'], face['normal'], face['tangent']
    w = min(face['length'] * 0.5, 44) / 2 + 3
    for k in range(int(depth)):
        for s in range(-int(w), int(w) + 1, 3):
            ground.occupy(c[0] + n[0] * k + t[0] * s, c[2] + n[2] * k + t[2] * s, 2.0)


# ---------- 형태 ----------

def column(mb, m, base, r, h, col=COLUMN, fluted=True):
    """받침·몸통·머리가 있는 고전 기둥"""
    x, y, z = base
    mb.box(m, (x, y + 0.25, z), (r * 2.6, 0.5, r * 2.6), STEP)
    mb.prism(m, (x, y + 0.5, z), r * 1.15, 0.3, 10, col)
    mb.prism(m, (x, y + 0.8, z), r, h - 1.6, 10 if fluted else 8, col, r_top=r * 0.9)
    mb.prism(m, (x, y + h - 0.8, z), r * 0.9, 0.35, 10, col, r_top=r * 1.25)
    mb.box(m, (x, y + h - 0.22, z), (r * 2.8, 0.45, r * 2.8), STONE)


def portico(mb, f, height, pediment, max_cols, grand=False):
    """정면 열주 현관: 기단, 기둥, 엔태블러처, (페디먼트), 앞 계단. 로컬 X = 면 방향, Z = 바깥"""
    m = Frame.look(f['center'], f['normal'])
    width = min(f['length'] * 0.5, 44.0)
    cols = max(4, min(max_cols, round(width / 5) + 1))
    depth, base_h = 7.0, 1.6 if grand else 1.2
    col_top = max(7.0, min(height - 2.2, 22.0 if grand else 17.0))

    mb.box(m, (0, base_h / 2, depth / 2 - 0.5), (width + 2, base_h, depth + 1), STEP)
    # 뒤 벽면의 어두운 현관 안쪽 (기둥 사이로 보이는 그늘진 벽과 문)
    mb.box(m, (0, (col_top + base_h) / 2, 0.15), (width - 2, col_top - base_h, 0.3), shade(STONE, 0.86))
    for k in range(3):
        x = (k - 1) * width / 4
        mb.arch_panel(m, (x, base_h, 0.32), 2.6, 5.0, DARK)
    for i in range(cols):
        x = -width / 2 + 1.4 + (width - 2.8) * (i / (cols - 1) if cols > 1 else 0.5)
        column(mb, m, (x, base_h, depth - 1.6), 0.8, col_top - base_h)
    # 엔태블러처: 아키트레이브 + 프리즈 + 코니스
    mb.box(m, (0, col_top + 0.5, depth / 2 - 0.5), (width + 1.0, 1.0, depth + 0.8), STONE)
    mb.box(m, (0, col_top + 1.4, depth / 2 - 0.5), (width + 1.4, 0.8, depth + 1.1), shade(STONE, 1.03))
    mb.box(m, (0, col_top + 2.0, depth / 2 - 0.5), (width + 2.0, 0.4, depth + 1.6), COLUMN)
    if pediment:
        mb.gable(m, (0, col_top + 2.2, depth / 2 - 0.3), width + 1.6, depth + 1.4, 5.0, STONE, True,
                 end_col=shade(STONE, 0.93))
        # 박공 테두리 (어두운 삼각 그늘)
        mb.gable(m, (0, col_top + 2.25, depth + 0.25), width - 2.5, 0.2, 3.9, shade(STONE, 0.82), True)
    # 앞 계단 (중앙도서관은 넓고 긴 대계단)
    steps = 7 if grand else 4
    for i in range(steps):
        h = base_h * (steps - i) / (steps + 1)
        mb.box(m, (0, h / 2, depth + 0.45 + 0.9 * i), (width + 4 + i * (1.2 if grand else 1.5), h, 0.9), STEP)
    if grand:
        # 계단 양옆 난간벽 (네모 기둥 머리)
        for s in (-1, 1):
            x = s * (width / 2 + 3.2)
            mb.box(m, (x, 1.0, depth + 3.2), (1.4, 2.0, 7.5), STONE)
            mb.box(m, (x, 2.2, depth + 6.4), (1.8, 0.6, 1.8), COLUMN)
        # 계단 앞 깃대 3기 (경희 진홍·흰색·하늘)
        for k, flag in zip((-1, 0, 1), ((0.68, 0.10, 0.18), (0.98, 0.98, 0.97), (0.36, 0.60, 0.86))):
            x, z = k * 7.0, depth + 7.5 + 7.0
            mb.box(m, (x, 0.3, z), (1.6, 0.6, 1.6), STEP)
            mb.prism(m, (x, 0.6, z), 0.14, 15.0, 6, (0.80, 0.81, 0.83), r_top=0.09)
            mb.prism(m, (x, 15.6, z), 0.22, 0.4, 6, GOLD, caps=True)
            mb.box(m, (x + 1.6, 13.6, z), (3.0, 1.9, 0.06), flag, top=flag)


def arcade(mb, f, height):
    """선승관: 정면 1층 아치 회랑"""
    m = Frame.look(f['center'], f['normal'])
    width = min(f['length'] * 0.55, 40.0)
    n = max(4, int(width / 5))
    h = min(7.5, height * 0.45)
    mb.box(m, (0, h / 2, 1.6), (width + 1.5, h, 3.2), STONE)
    for i in range(n):
        x = -width / 2 + width * (i + 0.5) / n
        mb.arch_panel(m, (x, 0.4, 3.22), width / n * 0.62, h - 1.6, DARK)
    mb.box(m, (0, h + 0.3, 1.7), (width + 2.0, 0.6, 3.6), COLUMN)
    for i in range(3):
        mb.box(m, (0, 0.15 + 0.15 * (2 - i), 3.6 + i * 0.8), (width * 0.5 + i * 1.5, 0.3 + 0.3 * (2 - i), 0.8), STEP)


def clock_tower(mb, f, height):
    c, n, t = f['center'], f['normal'], f['tangent']
    pos = (c[0] + t[0] * (f['length'] / 2 - 6) - n[0] * 4, 0.0, c[2] + t[2] * (f['length'] / 2 - 6) - n[2] * 4)
    m = Frame.look(pos, n)
    tower_h, w = height + 24.0, 9.0
    mb.box(m, (0, tower_h / 2, 0), (w, tower_h, w), STONE)
    # 모서리 기둥과 층 띠
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.box(m, (sx * (w / 2 - 0.3), tower_h / 2, sz * (w / 2 - 0.3)), (0.9, tower_h, 0.9), COLUMN)
    for y in (height, height + 9):
        mb.box(m, (0, y, 0), (w + 0.6, 0.6, w + 0.6), COLUMN)
    mb.box(m, (0, tower_h + 0.4, 0), (w + 1.4, 0.8, w + 1.4), STEP)
    mb.cone(m, (0, tower_h + 0.8, 0), (w + 1.4) * 0.72, 7.0, 4, ROOF, rot=45)
    mb.prism(m, (0, tower_h + 7.6, 0), 0.12, 2.2, 4, GOLD)
    clock_y = tower_h - 4.0
    for k in range(4):
        side = m.child((0, 0, 0), 90 * k)
        mb.disc(side, (0, clock_y, w / 2 + 0.15), 2.8, 0.15, 20, GOLD)
        mb.disc(side, (0, clock_y, w / 2 + 0.2), 2.4, 0.15, 20, (0.99, 0.98, 0.94))
        mb.box(side, (0, clock_y + 0.8, w / 2 + 0.3), (0.25, 1.7, 0.1), DARK)
        mb.box(side, (0.55, clock_y, w / 2 + 0.3), (1.2, 0.22, 0.1), DARK)
        mb.arch_panel(side, (0, height + 1.5, w / 2 + 0.05), 2.0, 5.0, DARK)


def observatory(mb, b, f):
    r = max(5.5, min(min(f.half_u, f.half_v) * 0.7, 11.0))
    cx, cz = f.center
    base = (cx, b.h, cz)
    mb.ao_floor = b.h
    mb.prism(IDENT, base, r + 2.2, 0.5, 20, STEP, caps=True)
    # 둥근 발코니 난간
    mb.prism(IDENT, (cx, b.h + 0.5, cz), r + 2.1, 0.9, 20, COLUMN, r_top=r + 2.1)
    mb.prism(IDENT, (cx, b.h + 0.5, cz), r, 3.2, 20, STONE)
    mb.hemisphere(IDENT, (cx, b.h + 3.7, cz), r, 20, 6, DOME)
    mb.prism(IDENT, (cx, b.h + 3.6, cz), r + 0.2, 0.3, 20, COLUMN, r_top=r + 0.2)
    # 관측 창: 남쪽 위로 세로 띠
    slit = Frame((cx, b.h + 3.7, cz)).child((0, 0, 0), 180)
    for k in range(5):
        a = math.radians(10 + k * 16)
        y, zz = math.sin(a) * (r + 0.05), math.cos(a) * (r + 0.05)
        mb.box(slit, (0, y, zz), (1.6, r * 0.3, 0.3), DARK)
    # 기본 공통 디테일 대신 난간만
    mb.ao_floor = 0.0
    mb.band(b.poly, b.h - 0.05, b.h + 0.7, 0.12, 0.4, COLUMN)
    mb.band(b.poly, 0.0, 0.9, 0.35, 0.0, (0.82, 0.77, 0.68))


def pitched_roof(mb, b, f):
    """도예관: 박공지붕 + 지붕창 (dormer)"""
    (ax, az), length, width = f.long_axis()
    m = Frame.look((f.center[0], b.h, f.center[1]), (ax, 0.0, az))
    L, Wd = length + 1.6, width + 1.6
    rh = max(3.0, min(Wd * 0.3, 6.5))
    mb.ao_floor = b.h
    mb.gable(m, (0, 0, 0), Wd, L, rh, ROOF, True, end_col=STONE)
    # 처마 띠
    mb.box(m, (0, -0.2, 0), (Wd + 0.2, 0.4, L + 0.2), COLUMN)
    n = max(2, int(L / 9))
    for i in range(n):
        z = -L / 2 + L * (i + 0.5) / n
        dm = m.child((-Wd * 0.22, rh * 0.42, z), 0)
        mb.box(dm, (0, 0.6, 0), (1.6, 1.4, 2.2), STONE)
        mb.box(dm, (-0.82, 0.6, 0), (0.05, 0.9, 1.4), GLASS, top=GLASS)
        mb.gable(dm, (0, 1.3, 0), 1.8, 2.4, 0.8, ROOF, False)
    mb.ao_floor = 0.0
    mb.band(b.poly, 0.0, 0.9, 0.35, 0.0, (0.82, 0.77, 0.68))


def amphitheater(mb, f, to_pond, ground):
    c, n = to_pond['center'], to_pond['normal']
    stage = (c[0] - n[0] * min(14, to_pond['depth'] * 0.3), 0.0, c[2] - n[2] * min(14, to_pond['depth'] * 0.3))
    m = Frame.look(stage, n)
    max_r = min(f.half_u, f.half_v) * 1.6
    tiers = max(4, min(int((max_r - 12) / 3), 9))
    center, spread = -math.pi / 2, math.radians(80)
    for i in range(tiers):
        r0 = 12 + i * 3.0
        col = STONE if i % 2 == 0 else (0.52, 0.74, 0.28)   # 석재 단과 잔디 단을 번갈아 (컨셉아트)
        mb.arc_tier(m, (0, 0, 0), r0, r0 + 3, center - spread, center + spread, 0.6 * (i + 1), 24, col)
        # 가운데 통로 계단 (어두운 줄)
        mb.box(m, (0, 0.6 * (i + 1) + 0.01, -(r0 + 1.5)), (1.6, 0.04, 3.0), shade(STEP, 0.8), top=shade(STEP, 0.8))
    outer = 12 + tiers * 3.0
    # 뒤쪽 둘레 벽
    mb.arc_tier(m, (0, 0, 0), outer, outer + 1.0, center - spread, center + spread, 0.6 * tiers + 1.0, 24, COLUMN)
    # 무대, 배경 벽(코니스·기둥), 양옆 기둥
    mb.box(m, (0, 0.6, 2), (18, 1.2, 10), STEP)
    mb.box(m, (0, 0.62, 2), (16, 1.2, 8.6), shade(STEP, 1.05))
    mb.box(m, (0, 3.8, 7.6), (22, 7.6, 1.4), STONE)
    for k in (-2, -1, 0, 1, 2):
        mb.arch_panel(m, (k * 4.2, 1.2, 6.88), 2.4, 4.6, shade(STONE, 0.8))
    mb.box(m, (0, 7.9, 7.6), (23.5, 0.8, 2.2), STEP)
    mb.box(m, (0, 8.8, 7.6), (10, 1.2, 1.6), STONE)
    for s in (-1, 1):
        column(mb, m, (s * 10.2, 1.2, 6.2), 0.7, 6.0)
    # 둘레 벚나무와 꽃 관목 (컨셉아트: 분홍 꽃에 둘러싸인 극장)
    import random as _r
    from .nature import draw_tree
    rr = _r.Random(11)
    for k in range(13):
        aa = center - spread - 0.15 + (2 * spread + 0.3) * k / 12
        for rad, kind in ((outer + 5.0, 'cherry'), (outer + 2.2, 'shrub')):
            p = m.p((math.cos(aa) * rad, 0, math.sin(aa) * rad))
            if ground.is_free(p[0], p[2], 1.0):
                draw_tree(mb, kind, p[0], p[2], rr.uniform(0.9, 1.15), rr.uniform(0, 360), rr)
                ground.occupy(p[0], p[2], 2.0, ao=0.35, ao_radius=2.6 if kind == 'cherry' else 1.4)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 2.0, 0.25
    # 나무가 들어오지 않게
    for k in range(0, int(outer) + 2, 3):
        for a in range(-80, 81, 10):
            aa = center + math.radians(a)
            p = m.p((math.cos(aa) * k, 0, math.sin(aa) * k))
            ground.occupy(p[0], p[2], 2.0)
    for z in range(-2, 10, 2):
        for x in range(-12, 13, 3):
            p = m.p((x, 0, z))
            ground.occupy(p[0], p[2], 2.0)


def gate(mb, f):
    """정문: 길을 가로지르는 석조 열주 문 (새천년기념탑, 네오르네상스문)"""
    (ax, az), length, thick = f.long_axis()
    thick = max(thick, 5.0)
    # 로컬 X = 문의 긴 방향
    m = Frame.look((f.center[0], 0.0, f.center[1]), (az, 0.0, -ax))
    pillars, ph = 6, 16.0
    for i in range(pillars):
        x = -length / 2 + 1.5 + (length - 3) * i / (pillars - 1)
        mb.box(m, (x, 0.6, 0), (3.6, 1.2, thick + 0.8), STEP)
        for sz in (-1, 1):
            column(mb, m, (x, 1.2, sz * thick / 4), 0.9, ph - 1.2)
    mb.box(m, (0, ph + 1.3, 0), (length + 1.5, 2.6, thick + 1.0), STONE)
    mb.box(m, (0, ph + 2.75, 0), (length + 2.2, 0.4, thick + 1.6), COLUMN)
    mb.box(m, (0, ph + 4.2, 0), (length * 0.42, 2.6, thick), STONE)
    mb.box(m, (0, ph + 5.65, 0), (length * 0.46, 0.5, thick + 0.6), STEP)
    # 가운데 문장 (금색 원판, 양면)
    for s in (0, 180):
        side = m.child((0, 0, 0), s)
        mb.disc(side, (0, ph + 4.2, thick / 2 + 0.1), 1.0, 0.1, 16, GOLD)


def obelisk(mb, pos):
    m = Frame(pos)
    mb.box(m, (0, 0.6, 0), (9, 1.2, 9), STEP)
    mb.box(m, (0, 1.8, 0), (7, 1.2, 7), shade(STEP, 1.04))
    mb.box(m, (0, 3.0, 0), (5.2, 1.2, 5.2), STONE)
    mb.frustum4(m, (0, 3.6, 0), 2.1, 1.3, 24, STONE)
    mb.cone(m, (0, 27.6, 0), 1.3 * 1.414, 3.6, 4, COLUMN, rot=45)


def stadium(layer, ground):
    """대운동장 네 모서리 조명탑 + 긴 변 한쪽 관중석"""
    (x0, z0), (x1, z1) = ground.stadium_box
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    long_z = (z1 - z0) > (x1 - x0)
    for sx in (-1, 1):
        for sz in (-1, 1):
            px, pz = cx + sx * ((x1 - x0) / 2 + 4), cz + sz * ((z1 - z0) / 2 + 4)
            mb = layer.at(px, pz)
            mb.ao_strength = 0.2
            m = Frame.look((px, 0, pz), (cx - px, 0, cz - pz))
            mb.prism(m, (0, 0, 0), 0.9, 1.0, 8, STEP)
            mb.prism(m, (0, 1.0, 0), 0.45, 21, 6, POLE, r_top=0.3)
            mb.box(m, (0, 22.5, 0.3), (4.4, 2.6, 0.5), POLE)
            for i in range(3):
                for j in range(2):
                    mb.box(m, (-1.4 + i * 1.4, 21.9 + j * 1.2, 0.6), (1.1, 0.9, 0.2), LAMP, top=LAMP)
            ground.occupy(px, pz, 2.5, ao=0.3, ao_radius=1.8)
    # 관중석: 서쪽(또는 남쪽) 긴 변 바깥
    if long_z:
        pos, face = (x0 - 9, 0.0, cz), (1.0, 0.0, 0.0)
        length = (z1 - z0) * 0.55
    else:
        pos, face = (cx, 0.0, z0 - 9), (0.0, 0.0, 1.0)
        length = (x1 - x0) * 0.55
    mb = layer.at(pos[0], pos[2])
    m = Frame.look(pos, face)
    for i in range(6):
        mb.box(m, (0, 0.45 * (i + 1) / 2, 3 - i * 1.0), (length, 0.45 * (i + 1), 1.0), SEAT if i % 2 else shade(SEAT, 1.05))
    mb.box(m, (0, 3.6, -2.6), (length + 1, 0.4, 3.6), (0.95, 0.95, 0.96))
    for s in (-1, 0, 1):
        mb.prism(m, (s * length / 2.2, 0, -3.8), 0.18, 3.5, 6, POLE)
    for k in range(-int(length / 2), int(length / 2) + 1, 3):
        p = m.p((k, 0, 0))
        ground.occupy(p[0], p[2], 4.0)
