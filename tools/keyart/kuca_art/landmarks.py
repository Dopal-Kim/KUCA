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

# 실제 건물 층수 (나무위키 등 조사, 2026-10). OSM 에 층수가 없어 기본 높이가 틀린 건물을 바로잡는다.
REAL_LEVELS = {
    'relation-8269760': 7,   # 우정원: 지하1·지상7
    'way-474085540': 7,      # 전자정보대학관: 지하1·지상7 (ㅁ자)
    'way-474085537': 8,      # 멀티미디어교육관: 8층
    'way-585696506': 5,      # 선승관: 지상 3층 대공간 체육관 (층고가 높아 5층 높이)
    'way-585696507': 5,      # 외국어대학관
    'way-474085534': 4,      # 중앙도서관 (연면적 17,498 m² / 바닥 5,500 m²)
    'way-455726113': 4,      # 체육대학관
    'way-474085536': 4,      # 예술디자인대학
    'way-455725718': 4,      # 공학관
    'way-455728922': 2,      # 공학실험동: 지하1·지상2
    'way-474085539': 5,      # 국제대학 (국제·경영대학관)
    'way-474085542': 4,      # 국제학관: OSM 4층
    'way-474521123': 2,      # 도예관
    'way-474521125': 2,      # 천문대 (돔 아래 기단 건물)
}
LEVEL_H = 3.3

CAMPUS_NAMES = {'학생회관', '원자로센터', '실습농장동', '실험연구동', '생명과학대학', '원예생물공학온실', '애지원', '공학실험동',
                '노천극장', '천문대', '선승관', '중앙도서관', '공학관', '멀티미디어교육관', '글로벌관', '우정원'}


def is_campus_seed(b):
    t = b.tags.get('building')
    return t in ('college', 'university', 'dormitory') or b.id in REAL_LEVELS or b.name in CAMPUS_NAMES \
        or '경희' in (b.name or '')

HIDDEN = {THEATER, GATE}          # 상자 건물 대신 이 형태만 보인다 (충돌체·정보는 Unity 에 남김)
NO_DETAILS = {THEATER, GATE, OBSERVATORY, CERAMICS}   # 공통 디테일(난간 등)을 붙이지 않을 건물


def build(buildings, layer, ground, shells=None, entrances=None):
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

    for bid, cols, pediment in ((LIBRARY, 8, True), (PE, 8, False)):
        b, f = fp(bid)
        if b:
            face = f.face(PLAZA)
            mb = builder_at(b)
            portico(mb, face, b.h, pediment, cols, grand=bid in (LIBRARY, PE), flags=(bid == LIBRARY))
            if bid == PE:
                lions(mb, face)          # 체육대학관: 대계단 양옆 웃는 사자상
                _rooftop_court(mb, b)    # 체육대학다운 지붕: 옥상 트랙 + 코트
            if bid == LIBRARY:
                atrium(mb, b, f)         # 중앙도서관: 가운데 아트리움 천창
            _occupy_face(ground, face, 18)

    b, f = fp(ARTS)
    if b and shells is not None:
        # 예술디자인대학: 궁전식 정면 (가운데 돌출 파빌리온 + 거대 기둥 + 박공 + 테라스 대계단)
        from .buildings import style_of
        st = style_of(b)
        arts_palace(builder_at(b), shells[st].at(*b.centroid), b, ground)
        if entrances is not None:
            entrances.pop(ARTS, None)

    b, f = fp(SEONSEUNG)
    if b:
        face = f.face(PLAZA)
        mb = builder_at(b)
        arena_roof(mb, b, f)             # 실제: 대공간 종합체육관 → 배럴 볼트 지붕 (컨셉아트 아이콘도 둥근 지붕)
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
        gate(builder_at(b), f, ground)

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


def portico(mb, f, height, pediment, max_cols, grand=False, flags=False):
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
    if flags:
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
    # 관측 창: 돔 밑동에서 꼭대기 너머까지 이어진 어두운 띠 + 양옆 셔터 레일
    n = 12
    for k in range(n):
        a0, a1 = math.radians(4 + k * 92 / n), math.radians(4 + (k + 1) * 92 / n)
        for x0, x1, col, off in ((-0.8, 0.8, DARK, 0.06), (-1.2, -0.8, COLUMN, 0.1), (0.8, 1.2, COLUMN, 0.1)):
            rr = r + off
            p0 = (x0, math.sin(a0) * rr, math.cos(a0) * rr)
            p1 = (x1, math.sin(a0) * rr, math.cos(a0) * rr)
            p2 = (x1, math.sin(a1) * rr, math.cos(a1) * rr)
            p3 = (x0, math.sin(a1) * rr, math.cos(a1) * rr)
            mb.quad(slit, p0, p1, p2, p3, col, (0, 0, 0))
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


def gate(mb, f, ground=None):
    """
    정문 (새천년기념탑, 네오르네상스문): 길을 가로지르는 깊은 석조 문.
    양 끝 큰 기둥덩어리 + 안쪽 기둥덩어리 2개로 가운데 넓은 차도 문 1 + 양옆 보행 문 2.
    덩어리마다 앞뒤 네 모서리에 굵은 기둥, 위로 두꺼운 엔태블러처, 양 끝·가운데 높은 아틱 (키아트 아이콘).
    """
    from .nature import LIGHTS
    (ax, az), length, _ = f.long_axis()
    W = max(length, 44.0)
    D = 13.0                       # 문 깊이 (앞뒤 기둥 사이)
    # 로컬 X = 문의 긴 방향, 로컬 Z = 길 방향
    m = Frame.look((f.center[0], 0.0, f.center[1]), (az, 0.0, -ax))
    base_h, ph = 1.2, 14.0
    top = base_h + ph
    end_w, side_w, inner_w = 7.5, 6.0, 5.6
    center_w = W - 2 * (end_w + side_w + inner_w)
    piers = []
    for s in (-1, 1):
        piers.append((s * (W / 2 - end_w / 2), end_w, True))
        piers.append((s * (center_w / 2 + inner_w / 2), inner_w, False))
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 3.0, 0.3
    for px, pw, is_end in piers:
        # 기단 2단
        mb.box(m, (px, 0.35, 0), (pw + 1.6, 0.7, D + 1.6), STEP)
        mb.box(m, (px, 0.95, 0), (pw + 0.8, 0.5, D + 0.8), shade(STEP, 1.03))
        # 기둥 사이로 보이는 안쪽 벽 덩어리 (조금 들어가 그늘진 돌)
        mb.box(m, (px, base_h + ph / 2, 0), (pw - 1.4, ph, D - 3.2), shade(STONE, 0.93))
        # 네 모서리 굵은 기둥 (+ 양 끝 덩어리는 바깥 옆면에 가운데 기둥 하나 더)
        cx_off = pw / 2 - 1.15
        for sx in (-1, 1):
            for sz in (-1, 1):
                column(mb, m, (px + sx * cx_off, base_h, sz * (D / 2 - 1.15)), 1.0, ph)
        if is_end:
            ox = px + (1 if px > 0 else -1) * (pw / 2 - 1.15)
            column(mb, m, (ox, base_h, 0), 1.0, ph)
            # 앞뒤 기둥 사이 아치 벽감
            for sz in (-1, 1):
                side = m.child((px, 0, sz * (D / 2 - 1.55)), 0 if sz > 0 else 180)
                mb.arch_panel(side, (0, base_h + 1.0, 0.02), 2.2, 6.0, shade(STONE, 0.78))
        else:
            # 밤에 문 안쪽을 밝히는 등 (안쪽 덩어리 앞뒤)
            for sz in (-1, 1):
                mb.emissive = True
                mb.box(m, (px, base_h + 5.2, sz * (D / 2 - 1.55)), (1.0, 1.4, 0.5), LAMP)
                mb.emissive = False
                mb.box(m, (px, base_h + 6.05, sz * (D / 2 - 1.55)), (1.3, 0.3, 0.7), GOLD)
        # 기둥 머리 위 받침 블록
        mb.box(m, (px, top + 0.3, 0), (pw + 0.4, 0.6, D + 0.4), STONE)
    # 엔태블러처: 아키트레이브 · 프리즈 · 코니스 (앞뒤로 같은 두께, 밑면은 그늘)
    mb.box(m, (0, top + 1.2, 0), (W + 0.4, 1.2, D + 0.4), shade(STONE, 1.02))
    mb.box(m, (0, top + 2.6, 0), (W + 0.2, 1.6, D + 0.2), STONE)
    for sz in (-1, 1):
        # 프리즈 안 오목한 띠, 기둥 위 마름돌
        mb.box(m, (0, top + 2.6, sz * (D / 2 + 0.12)), (W - 3.0, 0.9, 0.12), shade(STONE, 0.86), top=shade(STONE, 0.86))
        for px, pw, _ in piers:
            for sx in (-1, 1):
                mb.box(m, (px + sx * (pw / 2 - 1.15), top + 2.6, sz * (D / 2 + 0.18)), (1.6, 1.3, 0.2), COLUMN)
    mb.box(m, (0, top + 3.65, 0), (W + 1.8, 0.5, D + 1.8), COLUMN)
    mb.box(m, (0, top + 4.0, 0), (W + 1.2, 0.2, D + 1.2), shade(COLUMN, 0.97))
    y0 = top + 4.1
    # 아틱: 양 끝 높은 블록 + 가운데 높은 판 (사이는 낮은 난간)
    for s in (-1, 1):
        x = s * (W / 2 - end_w / 2)
        mb.box(m, (x, y0 + 2.6, 0), (end_w + 0.6, 5.2, D), STONE)
        mb.box(m, (x, y0 + 0.6, 0), (end_w + 1.0, 1.2, D + 0.4), shade(STONE, 1.03))
        mb.box(m, (x, y0 + 5.4, 0), (end_w + 1.4, 0.5, D + 0.8), COLUMN)
        mb.box(m, (x, y0 + 5.85, 0), (end_w - 1.0, 0.4, D - 2.0), shade(STEP, 1.05))
        for sz in (-1, 1):
            mb.box(m, (x, y0 + 2.8, sz * (D / 2 + 0.06)), (end_w - 2.2, 2.6, 0.12), shade(STONE, 0.88), top=shade(STONE, 0.88))
        # 난간 (끝 블록과 가운데 판 사이)
        x_in, x_out = s * (center_w / 2 + 3.0), s * (W / 2 - end_w - 0.3)
        mid, run = (x_in + x_out) / 2, abs(x_out - x_in)
        for sz in (-1, 1):
            balustrade(mb, m, (mid, y0, sz * (D / 2 - 0.4)), run, 1.1)
    cw = center_w + 6.0
    mb.box(m, (0, y0 + 2.0, 0), (cw, 4.0, D - 1.6), STONE)
    mb.box(m, (0, y0 + 4.2, 0), (cw + 0.8, 0.45, D - 0.8), COLUMN)
    mb.box(m, (0, y0 + 4.85, 0), (cw * 0.55, 0.9, D - 3.0), shade(STONE, 1.02))
    mb.box(m, (0, y0 + 5.4, 0), (cw * 0.55 + 0.6, 0.3, D - 2.4), COLUMN)
    for side_yaw in (0, 180):
        side = m.child((0, 0, 0), side_yaw)
        zf = (D - 1.6) / 2
        mb.box(side, (0, y0 + 2.0, zf + 0.06), (cw * 0.62, 2.0, 0.12), shade(STONE, 0.86), top=shade(STONE, 0.86))
        mb.box(side, (0, y0 + 2.0, zf + 0.14), (cw * 0.5, 0.18, 0.06), GOLD)   # 새김 글씨 줄
        mb.disc(side, (0, y0 + 2.0, zf + 0.22), 1.25, 0.1, 20, GOLD)
        mb.disc(side, (0, y0 + 2.0, zf + 0.27), 0.85, 0.1, 20, shade(GOLD, 1.12))
    # 바닥 불빛: 가운데 문·옆 문 아래
    for x in (0.0, -(center_w / 2 + inner_w + side_w / 2), center_w / 2 + inner_w + side_w / 2):
        p = m.p((x, 0, 0))
        LIGHTS.append((p[0], p[2], 12.0 if x == 0 else 7.0, 0.9))
    if ground is not None:
        # 문 전체 (기둥덩어리 + 문 사이 통로) 에 나무·벤치가 서지 않게, 덩어리 밑은 그늘
        for k in range(int(-D / 2) - 4, int(D / 2) + 5, 2):
            for dx in range(int(-W / 2) - 3, int(W / 2) + 4, 2):
                p = m.p((dx, 0, k))
                near = any(abs(dx - px) < pw / 2 + 1 and abs(k) < D / 2 + 1 for px, pw, _ in piers)
                ground.occupy(p[0], p[2], 1.6, ao=0.35 if near else 0.0, ao_radius=1.8)


def balustrade(mb, m, center, length, h=1.0, col=None, along_x=True):
    """난간: 아래 받침 + 동글한 난간동자 + 위 손잡이 (로컬 X 방향으로 길게)"""
    col = col or COLUMN
    x, y, z = center
    if length < 1.0:
        return
    if along_x:
        mb.box(m, (x, y + 0.12, z), (length, 0.24, 0.55), STONE)
        mb.box(m, (x, y + h - 0.1, z), (length + 0.1, 0.2, 0.6), col)
    else:
        mb.box(m, (x, y + 0.12, z), (0.55, 0.24, length), STONE)
        mb.box(m, (x, y + h - 0.1, z), (0.6, 0.2, length + 0.1), col)
    n = max(2, int(length / 0.7))
    for i in range(n):
        t = -length / 2 + length * (i + 0.5) / n
        px, pz = (x + t, z) if along_x else (x, z + t)
        mb.prism(m, (px, y + 0.24, pz), 0.17, h - 0.44, 5, col, r_top=0.12)
    for t in (-length / 2, length / 2):
        px, pz = (x + t, z) if along_x else (x, z + t)
        mb.box(m, (px, y + h / 2, pz), (0.6, h, 0.6), STONE)


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
                    mb.emissive = True
                    mb.box(m, (-1.4 + i * 1.4, 21.9 + j * 1.2, 0.6), (1.1, 0.9, 0.2), LAMP, top=LAMP)
                    mb.emissive = False
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


# ---------- 정문 진입로와 사색의 광장 둘레 (키아트 메인 비주얼의 정돈된 축) ----------

GATE_POS = (-138.0, 368.0)
FORMAL = (0.22, 0.50, 0.18)
HEDGE_C = (0.30, 0.55, 0.19)


def formal_cone(mb, x, z, s=1.0):
    """정원식 원뿔 나무 (가로수 열·광장 둘레): 짧은 줄기 + 길쭉한 원뿔 하나"""
    m = Frame((x, 0.0, z), (s, 0, 0), (0, s, 0), (0, 0, s))
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 1.5, 0.3
    mb.prism(m, (0, 0, 0), 0.25, 0.9, 5, (0.50, 0.36, 0.25))
    mb.ao_strength = 0.0
    mb.cone(m, (0, 0.6, 0), 1.7, 6.6, 8, FORMAL, bottom=False)
    mb.ao_strength = 0.25


def low_hedge(mb, p0, p1, h=0.8, w=1.0, col=HEDGE_C):
    L = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
    if L < 1.5:
        return
    yaw = math.degrees(math.atan2(p1[0] - p0[0], p1[1] - p0[1]))
    m = Frame.yaw(((p0[0] + p1[0]) / 2, 0.0, (p0[1] + p1[1]) / 2), yaw)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 0.8, 0.3
    mb.bevel_box(m, (0, h / 2, 0), (w, h, L), 0.22, col)
    mb.ao_strength = 0.25


def _free_run(ground, pts, step=1.0, radius=0.4):
    """점 목록을 따라 비어 있는 구간들 [(시작점, 끝점)]"""
    runs, cur = [], []
    for p in pts:
        ok = ground.inside(p[0], p[1]) and ground.is_free(p[0], p[1], radius, near_building=True) \
            and not ground.has(ground.walk, p[0], p[1]) and not ground.has(ground.road, p[0], p[1])
        if ok:
            cur.append(p)
        elif len(cur) >= 3:
            runs.append((cur[0], cur[-1]))
            cur = []
        else:
            cur = []
    if len(cur) >= 3:
        runs.append((cur[0], cur[-1]))
    return runs


def gate_avenue(layer, ground, length=280.0):
    """정문을 지나는 큰길 양옆: 10 m 간격 원뿔 가로수 + 그 사이 낮은 생울타리"""
    from .ground import ROAD_W
    from .geo import dist_to_segment
    count = 0
    for hw, pts in ground.road_lines:
        # 캠퍼스 밖 덕영대로(primary/secondary)는 빼고, 정문을 지나는 캠퍼스 안쪽 길만
        if hw in ('primary', 'secondary') or len(pts) < 2:
            continue
        if min(dist_to_segment(GATE_POS[0], GATE_POS[1], a, b)[0] for a, b in zip(pts, pts[1:])) > 20:
            continue
        off = ROAD_W[hw] / 2 + 3.4
        for a, b in zip(pts, pts[1:]):
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            if L < 1:
                continue
            ux, uz = (b[0] - a[0]) / L, (b[1] - a[1]) / L
            nx, nz = -uz, ux
            t = 5.0
            while t < L:
                cx, cz = a[0] + ux * t, a[1] + uz * t
                if math.hypot(cx - GATE_POS[0], cz - GATE_POS[1]) < length and math.hypot(cx - GATE_POS[0], cz - GATE_POS[1]) > 22:
                    for s in (1, -1):
                        px, pz = cx + nx * off * s, cz + nz * off * s
                        if ground.inside(px, pz) and ground.is_free(px, pz, 1.0):
                            formal_cone(layer.at(px, pz), px, pz, 1.0)
                            ground.occupy(px, pz, 1.8, ao=0.4, ao_radius=1.9)
                            count += 1
                        # 나무 사이 생울타리 (길에서 1.6 m)
                        h0 = (cx + ux * 1.6 + nx * (off - 1.8) * s, cz + uz * 1.6 + nz * (off - 1.8) * s)
                        h1 = (cx + ux * 8.4 + nx * (off - 1.8) * s, cz + uz * 8.4 + nz * (off - 1.8) * s)
                        if all(ground.inside(*q) and ground.is_free(q[0], q[1], 0.3, near_building=True) for q in (h0, h1)):
                            low_hedge(layer.at(*h0), h0, h1)
                            ground.occupy_line(h0, h1, 1.2, ao=0.35)
                t += 10.0
    return count


def plaza_ring(layer, ground, osm):
    """사색의 광장 가장자리: 바깥 2.5 m 생울타리, 6 m 원뿔 나무 열(12 m), 안쪽 1.5 m 가로등(20 m), 모서리 화단"""
    from .geo import to_world
    from .mesh import offset_polygon, signed_area
    from .nature import draw_lamp, FLOWER
    ring = None
    for e in osm:
        if e.get('tags', {}).get('place') == 'square' and e['type'] == 'way' and e.get('geometry'):
            ring = [to_world(p['lon'], p['lat']) for p in e['geometry']][:-1]
    if not ring:
        return 0
    if signed_area(ring) > 0:
        ring.reverse()

    def walk(poly, step):
        out = []
        for i in range(len(poly)):
            a, b = poly[i], poly[(i + 1) % len(poly)]
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            n = max(1, int(L // step))
            for k in range(n):
                t = k / n
                out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
        return out

    n = 0
    hedge_line = offset_polygon(ring, 2.5)
    for a, b in _free_run(ground, walk(hedge_line, 1.0)):
        low_hedge(layer.at(*a), a, b, h=0.9, w=1.1)
        ground.occupy_line(a, b, 1.4, ao=0.35)
        n += 1
    for (x, z) in walk(offset_polygon(ring, 6.0), 12.0):
        if ground.inside(x, z) and ground.is_free(x, z, 1.2):
            formal_cone(layer.at(x, z), x, z, 1.1)
            ground.occupy(x, z, 1.8, ao=0.4, ao_radius=2.0)
            n += 1
    cx = sum(p[0] for p in ring) / len(ring)
    cz = sum(p[1] for p in ring) / len(ring)
    for (x, z) in walk(offset_polygon(ring, -1.5), 20.0):
        if not ground.has(ground.occupied, x, z) and not ground.has(ground.water, x, z):
            draw_lamp(layer.at(x, z), x, z, math.degrees(math.atan2(cx - x, cz - z)))
            ground.occupy(x, z, 0.8, ao=0.25, ao_radius=0.6)
            n += 1
    # 모서리 화단: 원형 석재 화분 + 꽃 (꺾임이 큰 모서리만)
    inner = offset_polygon(ring, -4.0)
    corners = []
    for i, (x, z) in enumerate(inner):
        a, b = inner[i - 1], inner[(i + 1) % len(inner)]
        d0 = math.atan2(z - a[1], x - a[0])
        d1 = math.atan2(b[1] - z, b[0] - x)
        corners.append((abs((d1 - d0 + math.pi) % (2 * math.pi) - math.pi), x, z))
    for turn, x, z in sorted(corners, reverse=True)[:4]:
        if ground.has(ground.occupied, x, z) or ground.has(ground.water, x, z):
            continue
        mb = layer.at(x, z)
        mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 0.6, 0.25
        mb.prism(IDENT, (x, 0, z), 2.4, 0.6, 12, (0.90, 0.87, 0.81), caps=True)
        mb.ao_strength = 0.0
        for k in range(6):
            a = k * math.pi / 3
            mb.ico(IDENT, (x + math.cos(a) * 1.3, 0.85, z + math.sin(a) * 1.3), (0.55, 0.4, 0.55), FLOWER[k % len(FLOWER)])
        mb.ico(IDENT, (x, 1.1, z), (0.9, 0.7, 0.9), (0.36, 0.62, 0.22))
        ground.occupy(x, z, 3.0, ao=0.3, ao_radius=2.6)
        n += 1
    return n


def lions(mb, f):
    """대계단 양옆 받침 위 사자상 (돌 색, 앉은 모습을 덩어리로)"""
    m = Frame.look(f['center'], f['normal'])
    width = min(f['length'] * 0.5, 44.0)
    for sx in (-1, 1):
        x, z = sx * (width / 2 + 5.0), 7.0 + 9.5
        mb.box(m, (x, 0.9, z), (2.6, 1.8, 3.6), STEP)
        mb.box(m, (x, 2.05, z), (2.9, 0.3, 3.9), COLUMN)
        lion = (0.90, 0.86, 0.76)
        mb.ico(m, (x, 3.0, z - 0.4), (0.9, 0.85, 1.3), lion)      # 몸
        mb.ico(m, (x, 4.1, z + 0.6), (0.8, 0.8, 0.75), lion)      # 머리·갈기
        mb.box(m, (x, 2.6, z + 0.9), (1.0, 0.7, 0.5), lion)       # 앞발


def atrium(mb, b, f):
    """가운데 블록 위 유리 박공 천창 (실제 중앙도서관 1층 중앙 로비가 트인 아트리움)"""
    from .buildings import floor_height
    top = b.h + floor_height('Classical') + 0.8
    (ax, az), L, Wd = f.long_axis()
    m = Frame.look((f.center[0], top, f.center[1]), (ax, 0.0, az))
    mb.ao_floor = top
    mb.box(m, (0, 0.3, 0), (Wd * 0.28 + 0.8, 0.6, L * 0.3 + 0.8), COLUMN)
    mb.gable(m, (0, 0.6, 0), Wd * 0.28, L * 0.3, 2.6, GLASS, True, end_col=COLUMN)
    mb.ao_floor = 0.0


def arena_roof(mb, b, f):
    """선승관: 체육관 몸체 위 반원통 지붕 + 지붕 꼭대기 천창 띠"""
    (ax, az), L, Wd = f.long_axis()
    m = Frame.look((f.center[0], b.h + 0.6, f.center[1]), (ax, 0.0, az))
    span, length = Wd * 0.72, L * 0.7
    mb.ao_floor = b.h
    mb.box(m, (0, -0.2, 0), (span + 1.2, 0.8, length + 1.2), COLUMN)
    from .mesh import vault
    vault(mb, m, length, span, span * 0.28, 12, ROOF, end_col=STONE)
    # 볼트 꼭대기 천창과 갈비뼈 띠
    mb.box(m, (0, span * 0.28 + 0.15, 0), (2.4, 0.4, length - 2), GLASS, top=GLASS)
    for k in range(-3, 4):
        z = k * length / 7
        for j in range(12):
            a0, a1 = math.pi * j / 12, math.pi * (j + 1) / 12
            x0, y0 = -span / 2 * math.cos(a0), span * 0.28 * math.sin(a0)
            x1, y1 = -span / 2 * math.cos(a1), span * 0.28 * math.sin(a1)
            mb.box(m, ((x0 + x1) / 2, (y0 + y1) / 2 + 0.08, z), (abs(x1 - x0) + 0.2, abs(y1 - y0) + 0.25, 0.45), COLUMN, bottom=False)
    mb.ao_floor = 0.0


# ---------- 사색의 광장 호수 ----------

WATER_JET = (0.86, 0.95, 1.0)
LILY = (0.36, 0.62, 0.26)


def plan_lake(ground, osm):
    """광장 다각형 안 동쪽에 둥근 호수 (오벨리스크는 서쪽 도서관 쪽). 반환: (중심, rx, rz)"""
    from .geo import to_world
    ring = None
    for e in osm:
        if e.get('tags', {}).get('place') == 'square' and e['type'] == 'way' and e.get('geometry'):
            ring = [to_world(p['lon'], p['lat']) for p in e['geometry']][:-1]
    if not ring:
        return None
    xs, zs = [p[0] for p in ring], [p[1] for p in ring]
    w, h = max(xs) - min(xs), max(zs) - min(zs)
    cx = (min(xs) + max(xs)) / 2 + w * 0.14
    cz = (min(zs) + max(zs)) / 2
    rx, rz = min(w * 0.24, 42.0), min(h * 0.3, 26.0)
    ground.add_lake((cx, cz), rx, rz)
    return (cx, cz), rx, rz


def lake_features(layer, ground, lake):
    """호수 둘레 낮은 석재 테, 가운데 3단 분수와 물줄기, 연잎"""
    import random as _r
    from .mesh import offset_polygon, signed_area
    (cx, cz), rx, rz = lake
    poly = list(ground.lake_poly)
    if signed_area(poly) > 0:
        poly.reverse()
    mb = layer.at(cx, cz)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 0.5, 0.2
    mb.band(poly, 0.0, 0.55, 1.3, 0.0, (0.92, 0.89, 0.83), top_col=(0.97, 0.95, 0.90))
    mb.ao_strength = 0.0
    # 분수: 받침 3단 + 물줄기 원뿔
    base = (0.94, 0.92, 0.87)
    mb.prism(IDENT, (cx, 0.0, cz), 5.0, 0.7, 16, base, caps=True)
    mb.prism(IDENT, (cx, 0.7, cz), 4.4, 0.08, 16, (0.55, 0.80, 0.92), caps=True)
    mb.prism(IDENT, (cx, 0.7, cz), 0.8, 2.4, 10, base)
    mb.prism(IDENT, (cx, 3.1, cz), 2.6, 0.45, 14, base, caps=True)
    mb.prism(IDENT, (cx, 3.55, cz), 0.45, 1.6, 8, base)
    mb.prism(IDENT, (cx, 5.15, cz), 1.3, 0.35, 12, base, caps=True)
    mb.cone(IDENT, (cx, 5.5, cz), 0.5, 3.4, 8, WATER_JET, bottom=False)
    for k in range(8):
        a = k * math.pi / 4
        x, z = cx + math.cos(a) * 3.6, cz + math.sin(a) * 3.6
        mb.cone(IDENT, (x, 0.75, z), 0.22, 1.6, 6, WATER_JET, bottom=False)
    # 연잎과 분홍 연꽃
    rr = _r.Random(5)
    for k in range(22):
        a = rr.uniform(0, 2 * math.pi)
        t = rr.uniform(0.45, 0.85)
        x, z = cx + math.cos(a) * rx * t, cz + math.sin(a) * rz * t
        mb.prism(IDENT, (x, 0.05, z), rr.uniform(0.7, 1.2), 0.06, 8, LILY, caps=True)
        if rr.random() < 0.35:
            mb.ico(IDENT, (x, 0.3, z), (0.3, 0.22, 0.3), (0.98, 0.70, 0.82), var=0.0)
    ground.occupy(cx, cz, max(rx, rz) + 2, ao=0.0)
    # 호수 둘레 벤치 6개 (호수를 바라보게)
    from .nature import draw_bench
    for k in range(6):
        a = k * math.pi / 3 + 0.3
        x, z = cx + math.cos(a) * (rx + 4.5), cz + math.sin(a) * (rz + 4.5)
        draw_bench(layer.at(x, z), x, z, math.degrees(math.atan2(cx - x, cz - z)))


# ---------- 캠퍼스 경계 담장 ----------

WALL = (0.95, 0.92, 0.85)
WALL_CAP = (0.99, 0.97, 0.92)


def campus_wall(layer, ground, rng):
    """캠퍼스 경계선을 따라 크림 석재 담장(1.3 m) + 12 m 기둥, 길과 만나는 곳은 문처럼 비우고 기둥에 등.
    담장 안쪽 5 m 에 벚나무·원뿔 나무를 번갈아 (경계 안이 특별한 공간으로 보이게)"""
    from .ground import ROAD_W
    from .nature import draw_tree
    from .geo import MAP_W, MAP_H
    walls = posts = trees = 0

    def blocked(x, z):
        return (not ground.inside(x, z, 20)) or ground.has(ground.road, x, z) or ground.has(ground.curb, x, z) \
            or ground.has(ground.walk, x, z) or ground.has(ground.water, x, z) or ground.has(ground.buildings, x, z) \
            or ground.has(ground.parking, x, z) or ground.has(ground.paved, x, z)

    for line in ground.campus_contours():
        if len(line) < 4:
            continue
        # 2 m 간격으로 다시 찍기
        pts = [line[0]]
        for p in line[1:]:
            q = pts[-1]
            L = math.hypot(p[0] - q[0], p[1] - q[1])
            n = int(L // 2.0)
            for k in range(1, n + 1):
                pts.append((q[0] + (p[0] - q[0]) * k / max(n, 1), q[1] + (p[1] - q[1]) * k / max(n, 1)))
        free = [not blocked(*p) for p in pts]
        run = []
        for i, p in enumerate(pts + [None]):
            ok = p is not None and free[i]
            if ok:
                run.append(p)
                continue
            if len(run) >= 3:
                # 열린 끝(길·문)에는 등 기둥
                for end in (run[0], run[-1]):
                    _post(layer.at(*end), end, lamp=True)
                    posts += 1
                for a, b in zip(*_merge_straight(run)):
                    _wall_seg(layer.at(*a), a, b)
                    walls += 1
                for k in range(6, len(run) - 3, 6):
                    _post(layer.at(*run[k]), run[k], lamp=False)
                    ground.occupy(run[k][0], run[k][1], 1.2)
                    posts += 1
                for a, b in zip(run, run[1:]):
                    ground.occupy_line(a, b, 1.6, ao=0.3)
            run = []
        # 안쪽 가로수 띠
        for i in range(4, len(pts) - 4, 8):
            (x0, z0), (x1, z1) = pts[i - 1], pts[i + 1]
            L = math.hypot(x1 - x0, z1 - z0) or 1
            nx, nz = -(z1 - z0) / L, (x1 - x0) / L
            for sgn in (1, -1):
                px, pz = pts[i][0] + nx * 5.5 * sgn, pts[i][1] + nz * 5.5 * sgn
                if ground.has(ground.campus, px, pz) and ground.inside(px, pz) and ground.is_free(px, pz, 1.2):
                    kind = 'cherry' if (i // 8) % 2 == 0 else 'small_cone'
                    draw_tree(layer.at(px, pz), kind, px, pz, rng.uniform(1.0, 1.2), rng.uniform(0, 360), rng)
                    ground.occupy(px, pz, 2.2, ao=0.35, ao_radius=2.6)
                    trees += 1
    return walls, posts, trees


def _wall_seg(mb, a, b):
    L = math.hypot(b[0] - a[0], b[1] - a[1])
    if L < 0.2:
        return
    yaw = math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))
    m = Frame.yaw(((a[0] + b[0]) / 2, 0.0, (a[1] + b[1]) / 2), yaw)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 0.8, 0.22
    mb.box(m, (0, 0.25, 0), (1.3, 0.5, L + 0.05), (0.86, 0.82, 0.74), bottom=False)
    mb.box(m, (0, 1.0, 0), (1.0, 1.9, L + 0.05), WALL, bottom=False)
    mb.ao_strength = 0.0
    mb.bevel_box(m, (0, 2.08, 0), (1.35, 0.32, L + 0.1), 0.14, WALL_CAP)
    mb.ao_strength = 0.25


def _post(mb, p, lamp):
    m = Frame((p[0], 0.0, p[1]))
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 1.0, 0.22
    mb.box(m, (0, 1.4, 0), (2.0, 2.8, 2.0), WALL, bottom=False)
    mb.ao_strength = 0.0
    mb.bevel_box(m, (0, 2.95, 0), (2.4, 0.35, 2.4), 0.16, WALL_CAP)
    if lamp:
        from .nature import LIGHTS
        LIGHTS.append((p[0], p[1], 6.0, 0.8))
        mb.prism(m, (0, 3.1, 0), 0.3, 0.6, 6, (0.32, 0.34, 0.38))
        mb.emissive = True
        mb.box(m, (0, 4.0, 0), (0.8, 0.9, 0.8), (1.0, 0.96, 0.82), top=(1.0, 0.96, 0.82))
        mb.emissive = False
        mb.box(m, (0, 4.52, 0), (1.05, 0.16, 1.05), (0.32, 0.34, 0.38))
    else:
        mb.ico(m, (0, 3.6, 0), (0.6, 0.6, 0.6), WALL_CAP, var=0.04)
    mb.ao_strength = 0.25


def _merge_straight(run, tol=0.35, max_len=40.0):
    """점 열을 거의 곧은 구간끼리 묶어 (시작점들, 끝점들). 담장 상자 수를 줄인다."""
    starts, ends = [], []
    i = 0
    while i < len(run) - 1:
        j = i + 1
        while j + 1 < len(run):
            a, b = run[i], run[j + 1]
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            if L > max_len:
                break
            # 중간 점들이 직선에서 tol 이내인지
            ok = True
            for k in range(i + 1, j + 1):
                p = run[k]
                d = abs((b[0] - a[0]) * (a[1] - p[1]) - (a[0] - p[0]) * (b[1] - a[1])) / (L or 1)
                if d > tol:
                    ok = False
                    break
            if not ok:
                break
            j += 1
        starts.append(run[i])
        ends.append(run[j])
        i = j
    return starts, ends


# ---------- 정문 안쪽 분수 조형물 (실제: 정문을 들어서면 오른쪽 '네오르네상스' 분수) ----------

def gate_fountain(layer, ground):
    """정문 안쪽(남쪽으로 들어서며 오른쪽 = 서쪽)에 둥근 분수 + 받침 위 지구본 조형물"""
    gx, gz = GATE_POS
    best = None
    for dx in range(-60, -10, 4):
        for dz in range(-70, -15, 4):
            x, z = gx + dx, gz + dz
            if ground.has(ground.campus, x, z) and ground.is_free(x, z, 9.0):
                d = math.hypot(dx + 30, dz + 35)
                if best is None or d < best[0]:
                    best = (d, x, z)
    if not best:
        return False
    _, x, z = best
    mb = layer.at(x, z)
    m = Frame((x, 0.0, z))
    stone, cap = (0.93, 0.90, 0.84), (0.99, 0.97, 0.92)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 0.6, 0.22
    mb.prism(m, (0, 0, 0), 7.0, 0.7, 20, stone, caps=False)
    mb.ao_strength = 0.0
    mb.prism(m, (0, 0.7, 0), 7.3, 0.18, 20, cap, caps=True)
    mb.prism(m, (0, 0.55, 0), 6.4, 0.2, 20, (0.50, 0.80, 0.94), caps=True)       # 물
    mb.prism(m, (0, 0.0, 0), 1.6, 3.2, 8, stone)                                   # 받침
    mb.prism(m, (0, 3.2, 0), 2.0, 0.35, 8, cap, caps=True)
    mb.ico(m, (0, 5.3, 0), (1.8, 1.8, 1.8), (0.62, 0.78, 0.92), var=0.06)          # 지구본
    mb.prism(m, (0, 4.6, 0), 2.15, 0.25, 16, GOLD)                                 # 금색 고리
    for k in range(10):
        a = k * math.pi / 5
        mb.cone(m, (math.cos(a) * 5.2, 0.75, math.sin(a) * 5.2), 0.2, 1.8, 6, WATER_JET, bottom=False)
    ground.occupy(x, z, 9.0, ao=0.25, ao_radius=7.5)
    return True


# ---------- 쉼터: 넓은 잔디의 육각 정자 ----------

PAVILION_ROOFS = [(0.70, 0.86, 0.78), (0.98, 0.78, 0.82), (0.72, 0.84, 0.96), (0.98, 0.88, 0.70)]


def pavilions(layer, ground, rng, max_count=10, spacing=130.0):
    """캠퍼스 안 넓은 잔디(숲 아님) 중 길에서 25 m 안, 주변 9 m 가 빈 곳에 정자 + 벤치 + 꽃"""
    from .geo import MAP_H, MAP_W
    from .nature import draw_bench, FLOWER
    from scipy import ndimage
    g = ground
    lawn = g.campus & g.park & ~g.forest
    d_walk = ndimage.distance_transform_edt(~(g.walk | g.road)) / g.ppm
    cands = []
    step = 9.0
    z = -MAP_H / 2 + 20
    while z < MAP_H / 2 - 20:
        x = -MAP_W / 2 + 20
        while x < MAP_W / 2 - 20:
            ix, iy = g._ix(x, z)
            if 0 <= ix < g.W and 0 <= iy < g.H and lawn[iy, ix] and 6 < d_walk[iy, ix] < 25 and g.is_free(x, z, 9.0):
                cands.append((d_walk[iy, ix], x, z))
            x += step
        z += step
    rng.shuffle(cands)
    placed = []
    for _, x, z in cands:
        if len(placed) >= max_count:
            break
        if any(math.hypot(x - px, z - pz) < spacing for px, pz in placed):
            continue
        placed.append((x, z))
        mb = layer.at(x, z)
        m = Frame.yaw((x, 0.0, z), rng.uniform(0, 60))
        roof = PAVILION_ROOFS[len(placed) % len(PAVILION_ROOFS)]
        mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 0.6, 0.25
        mb.prism(m, (0, 0, 0), 4.6, 0.45, 6, (0.90, 0.87, 0.80), caps=True, rot=30)
        mb.ao_strength = 0.0
        for k in range(6):
            a = math.radians(30 + 60 * k)
            mb.prism(m, (math.cos(a) * 3.7, 0.45, math.sin(a) * 3.7), 0.22, 3.0, 6, (0.98, 0.96, 0.92))
        mb.prism(m, (0, 3.45, 0), 4.5, 0.35, 6, (0.98, 0.96, 0.92), caps=True, rot=30)
        mb.cone(m, (0, 3.8, 0), 5.4, 2.8, 6, roof, rot=30)
        mb.prism(m, (0, 6.5, 0), 0.18, 0.7, 6, GOLD, caps=True)
        mb.ico(m, (0, 7.3, 0), (0.3, 0.3, 0.3), GOLD, var=0.0)
        for sx in (-1, 1):
            p = m.p((sx * 1.6, 0.45, 0))
            draw_bench(mb, p[0], p[2], math.degrees(math.atan2(-sx, 0)))
        for k in range(8):
            a = k * math.pi / 4 + 0.2
            p = m.p((math.cos(a) * 6.0, 0, math.sin(a) * 6.0))
            mb.ico(IDENT, (p[0], 0.4, p[2]), (0.6, 0.45, 0.6), FLOWER[k % len(FLOWER)] if k % 2 else (0.36, 0.62, 0.22))
        ground.occupy(x, z, 8.0, ao=0.3, ao_radius=5.5)
    return len(placed)


def _front_edge(poly, toward):
    """toward 방향(단위 벡터)을 바라보는 가장 긴 변: (a, b, 바깥 법선, 길이)"""
    from .mesh import outward_normal
    best = None
    n = len(poly)
    for i in range(n):
        a, c = poly[i], poly[(i + 1) % n]
        L = math.hypot(c[0] - a[0], c[1] - a[1])
        nx, nz = outward_normal(poly, i)
        if nx * toward[0] + nz * toward[1] < 0.92:
            continue
        if best is None or L > best[3]:
            best = (a, c, (nx, nz), L)
    return best


def arts_palace(mb, shell, b, ground):
    """
    예술디자인대학 (키아트 '궁전' 아이콘): 사색의 광장·산책길이 오는 서쪽 정면 돌출부를
    한 층 높은 가운데 파빌리온으로 키우고, 거대 기둥 6개 + 엔태블러처 + 박공 + 지붕 난간,
    1층 아치 현관 3개, 앞에는 낮은 테라스와 가운데 대계단 · 양옆 화단 생울타리.
    """
    from .buildings import PALETTE, floor_height, roof_tint, style_of
    from .mesh import extrude_poly, rect_poly, round_corners
    from .nature import LIGHTS
    pal = PALETTE[style_of(b)]
    dx, dz = PLAZA[0] - b.centroid[0], PLAZA[1] - b.centroid[1]
    L = math.hypot(dx, dz) or 1
    edge = _front_edge(b.poly, (dx / L, dz / L))
    if edge is None:
        return
    a, c, (nx, nz), elen = edge
    # 둥근 모서리로 깎인 길이만큼 되돌린 원래 변 길이
    width = min(elen + 4.8, 30.0)
    mx, mz = (a[0] + c[0]) / 2, (a[1] + c[1]) / 2
    proj, back = 4.0, 4.0
    H = b.h
    fh = floor_height(style_of(b))
    top = H + fh
    # 1) 파빌리온 덩어리 (외벽 셰이더: 창이 같이 그려진다)
    tx, tz = -nz, nx
    pc = (mx + nx * (proj - back) / 2, mz + nz * (proj - back) / 2)
    rect = round_corners(rect_poly(pc, (nx, nz), (tx, tz), (proj + back) / 2, width / 2), radius=1.4)
    shell.ao_strength = 0.0
    extrude_poly(shell, rect, 0.0, top, roof_col=roof_tint(b))
    # 로컬 X = 면 방향, Z = 바깥 (면 = 파빌리온 앞면)
    m = Frame.look((mx + nx * proj, 0.0, mz + nz * proj), (nx, 0.0, nz))
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 2.0, 0.25
    mb.band(rect, 0.0, 1.0, 0.4, 0.0, pal['base'], top_col=shade(pal['base'], 1.12))
    mb.ao_strength = 0.0
    mb.band(rect, top - 1.3, top - 0.6, 0.4, 0.0, pal['trim'])
    # 2) 거대 기둥 6개 (벽에서 0.9 m 앞, 기단 위에서 처마 밑까지)
    n_cols = 6
    span = width - 3.0
    col_h = top - 1.3 - 1.0
    for i in range(n_cols):
        x = -span / 2 + span * i / (n_cols - 1)
        column(mb, m, (x, 1.0, 0.95), 0.75, col_h)
    # 엔태블러처 띠 (기둥 머리 위)
    mb.box(m, (0, top - 1.0, 0.9), (width + 0.6, 1.6, 2.2), STONE)
    mb.box(m, (0, top - 0.1, 0.9), (width + 1.4, 0.35, 2.8), COLUMN)
    # 3) 박공 (앞을 향한 삼각, 지붕 위까지 덮음) + 박공 안 원형 장식
    mb.ao_floor = top
    mb.gable(m, (0, top + 0.05, -0.85), width + 1.2, 6.9, 4.2, shade(pal['trim'], 0.97), True,
             end_col=shade(STONE, 0.95))
    mb.gable(m, (0, top + 0.4, 2.66), width - 3.0, 0.1, 3.0, shade(STONE, 0.8), True)
    mb.disc(m, (0, top + 1.5, 2.76), 0.9, 0.1, 16, GOLD)
    mb.ao_floor = 0.0
    # 4) 1층 아치 현관 3개 (기둥 사이, 밤에 불 켜짐) + 위 아치창 띠
    gap = span / (n_cols - 1)
    mb.emissive = True
    for k in (-1, 0, 1):
        mb.arch_panel(m, (k * gap, 1.0, 0.06), gap * 0.55, 4.4, (0.95, 0.80, 0.52))
    mb.emissive = False
    for k in (-1, 0, 1):
        mb.arch_panel(m, (k * gap, 1.0, 0.03), gap * 0.55 + 0.6, 4.7, pal['trim'])
    LIGHTS.append((mx + nx * (proj + 4), mz + nz * (proj + 4), 12.0, 1.0))
    # 5) 파빌리온 지붕 양옆 난간 + 모서리 화분
    for s in (-1, 1):
        mb.prism(m, (s * (width / 2 + 0.2), top + 0.1, 1.0), 0.45, 0.5, 8, STONE, caps=True)
        mb.ico(m, (s * (width / 2 + 0.2), top + 1.0, 1.0), (0.45, 0.55, 0.45), COLUMN, var=0.04)
    # 6) 테라스 (높이 1.2 m, 깊이 6 m) + 가운데 대계단 + 양옆 생울타리 화단
    tw, td, th = width + 12.0, 6.0, 1.2
    mb.ao_strength = 0.25
    mb.box(m, (0, th / 2, td / 2), (tw, th, td), pal['base'], top=shade(STEP, 1.05))
    mb.ao_strength = 0.0
    mb.box(m, (0, th + 0.03, td / 2 + 0.3), (tw - 1.6, 0.06, td - 1.2), shade(STEP, 1.08), top=shade(STEP, 1.08))
    sw = width * 0.45
    steps = 5
    for i in range(steps):
        h = th * (steps - i) / (steps + 1)
        mb.box(m, (0, h / 2, td + 0.45 + 0.85 * i), (sw, h, 0.85), STEP)
    for s in (-1, 1):
        # 계단 옆 볼 (낮은 벽) + 구슬 장식
        x = s * (sw / 2 + 0.5)
        mb.box(m, (x, th / 2 + 0.2, td + 2.2), (1.0, th + 0.4, 4.6), STONE)
        mb.prism(m, (x, th + 0.4, td + 4.2), 0.42, 0.4, 8, COLUMN, caps=True)
        mb.ico(m, (x, th + 1.15, td + 4.2), (0.42, 0.42, 0.42), COLUMN, var=0.03)
        # 테라스 앞 난간
        bx0, bx1 = s * (sw / 2 + 1.0), s * (tw / 2 - 0.3)
        balustrade(mb, m, ((bx0 + bx1) / 2, th, td - 0.35), abs(bx1 - bx0), 0.95)
        # 테라스 아래 생울타리와 원뿔 나무
        hx = s * (sw / 2 + 1.2 + (tw / 2 - sw / 2 - 1.2) / 2)
        p0, p1 = m.p((hx - (tw / 2 - sw / 2 - 2.0) / 2, 0, td + 1.2)), m.p((hx + (tw / 2 - sw / 2 - 2.0) / 2, 0, td + 1.2))
        low_hedge(mb, (p0[0], p0[2]), (p1[0], p1[2]), h=0.9, w=1.2)
        for k in range(3):
            q = m.p((s * (sw / 2 + 2.8 + k * 3.2), 0, td + 1.2))
            mb.ico(IDENT, (q[0], 1.1, q[2]), (0.5, 0.4, 0.5), (0.98, 0.70, 0.80) if k % 2 == 0 else (1.0, 0.92, 0.94))
        cp = m.p((s * (tw / 2 + 1.6), 0, td * 0.5))
        formal_cone(mb, cp[0], cp[2], 1.0)
        cp = m.p((s * (tw / 2 + 1.6), 0, td * 0.5 - 5.5))
        formal_cone(mb, cp[0], cp[2], 0.9)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 2.0, 0.25
    _atelier_roof(mb, b, pal)
    # 나무·벤치가 테라스·계단에 서지 않게, 계단 앞은 포장 (apron)
    for zz in range(-1, int(td + 6), 2):
        for xx in range(int(-tw / 2) - 2, int(tw / 2) + 3, 2):
            p = m.p((xx, 0, zz))
            ground.occupy(p[0], p[2], 1.6)
    s0 = m.p((0, 0, td + 4.5))
    end = ground.nearest_walk_point(s0[0], s0[2], max_dist=30)
    if end:
        ground.add_apron((s0[0], s0[2]), end, sw)


def _atelier_roof(mb, b, pal):
    """예술대학다운 지붕: 가운데 넓은 평지붕에 북향 톱날 천창 (화실·작업실의 북쪽 빛). 위에서 보면 줄무늬 결."""
    from .buildings import ROOF_RESERVED, _rect_inside
    from .geo import Footprint
    from .mesh import rect_poly
    fp = Footprint(b.poly, b.h)
    if fp.half_u >= fp.half_v:
        u, v, hu, hv = fp.u, fp.v, fp.half_u, fp.half_v
    else:
        u, v, hu, hv = fp.v, fp.u, fp.half_v, fp.half_u
    # v 를 북쪽(+Z) 으로
    if v[1] < 0:
        v = (-v[0], -v[1])
    half_u, half_v = hu * 0.42, hv * 0.5
    for _ in range(6):
        rect = rect_poly(fp.center, u, v, half_u, half_v)
        if _rect_inside(b.poly, rect, 2.5):
            break
        half_u, half_v = half_u * 0.9, half_v * 0.9
    else:
        return
    H = b.h
    # 로컬 X = u, Z = v (북)
    m = Frame((fp.center[0], H, fp.center[1]), (u[0], 0.0, u[1]), (0.0, 1.0, 0.0), (v[0], 0.0, v[1]))
    mb.ao_floor, mb.ao_height, mb.ao_strength = H, 1.5, 0.3
    mb.box(m, (0, 0.2, 0), (half_u * 2 + 1.0, 0.4, half_v * 2 + 1.0), shade(STEP, 1.02))
    mb.ao_strength = 0.0
    pitch, th = 5.0, 2.4
    rows = max(2, int(half_v * 2 / pitch))
    roof_c = shade(pal['trim'], 0.96)
    x0, x1 = -half_u, half_u
    for k in range(rows):
        z0 = -half_v + k * (half_v * 2 / rows)
        z1 = z0 + half_v * 2 / rows - 0.3
        y0 = 0.4
        inside = (0, y0 + 0.3, z1 - 0.6)
        # 남쪽으로 기운 지붕면, 북쪽 세로 유리면 (밤에는 불 켜진 작업실)
        mb.quad(m, (x0, y0, z0), (x1, y0, z0), (x1, y0 + th, z1), (x0, y0 + th, z1), roof_c, inside)
        mb.emissive = True
        mb.quad(m, (x0, y0, z1), (x1, y0, z1), (x1, y0 + th, z1), (x0, y0 + th, z1), (0.55, 0.72, 0.86), inside)
        mb.emissive = False
        for xx in (x0, x1):
            mb.tri(m, (xx, y0, z0), (xx, y0, z1), (xx, y0 + th, z1), shade(roof_c, 0.9), inside)
        # 유리면 창살 + 마루 띠
        n_m = max(2, int((x1 - x0) / 3.0))
        for i in range(n_m + 1):
            x = x0 + (x1 - x0) * i / n_m
            mb.box(m, (x, y0 + th / 2, z1 + 0.05), (0.18, th, 0.1), COLUMN)
        mb.box(m, (0, y0 + th + 0.08, z1), (x1 - x0 + 0.4, 0.16, 0.4), COLUMN)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 2.0, 0.25
    ROOF_RESERVED.setdefault(b.id, []).append((rect_poly(fp.center, u, v, half_u + 1.0, half_v + 1.0), H + 3.0))


def _rooftop_court(mb, b):
    """체육대학관 옥상: 붉은 트랙이 두른 초록 코트 + 흰 선 + 낮은 펜스 (위에서 보면 작은 운동장)"""
    from .buildings import ROOF_RESERVED, _rect_inside
    from .geo import Footprint
    from .mesh import rect_poly
    fp = Footprint(b.poly, b.h)
    if fp.half_u >= fp.half_v:
        u, v, hu, hv = fp.u, fp.v, fp.half_u, fp.half_v
    else:
        u, v, hu, hv = fp.v, fp.u, fp.half_v, fp.half_u
    hl, hw = min(hu * 0.45, 26.0), min(hv * 0.62, 16.0)
    for _ in range(6):
        if _rect_inside(b.poly, rect_poly(fp.center, u, v, hl + 1, hw + 1), 2.0):
            break
        hl, hw = hl * 0.9, hw * 0.9
    else:
        return
    H = b.h
    m = Frame((fp.center[0], H, fp.center[1]), (u[0], 0.0, u[1]), (0.0, 1.0, 0.0), (v[0], 0.0, v[1]))
    track, court, line = (0.84, 0.45, 0.36), (0.38, 0.66, 0.46), (0.98, 0.98, 0.96)
    mb.ao_floor, mb.ao_height, mb.ao_strength = H, 1.0, 0.25
    mb.box(m, (0, 0.1, 0), (hl * 2 + 1.2, 0.2, hw * 2 + 1.2), shade(STEP, 1.02))
    mb.ao_strength = 0.0
    mb.box(m, (0, 0.24, 0), (hl * 2, 0.08, hw * 2), track, top=track)
    # 트랙 둥근 끝 (양 끝 반원 대신 팔각 근사)
    iw, il = hw - 3.0, hl - 3.0
    mb.box(m, (0, 0.3, 0), (il * 2, 0.06, iw * 2), court, top=court)
    for k in range(2):
        r = 3.0 - 1.4 * k
        y = 0.33
        for sx in (-1, 1):
            mb.box(m, (0, y, sx * (hw - r)), (hl * 2 - 2.0, 0.02, 0.12), line, top=line)
        for sx in (-1, 1):
            mb.box(m, (sx * (hl - r), y, 0), (0.12, 0.02, hw * 2 - 2.0), line, top=line)
    # 코트 선: 가운데선, 원, 양쪽 골 영역
    mb.box(m, (0, 0.36, 0), (0.14, 0.02, iw * 2 - 1.0), line, top=line)
    for i in range(16):
        a = i * math.pi / 8
        mb.box(m, (math.cos(a) * 2.8, 0.36, math.sin(a) * 2.8), (0.5, 0.02, 0.5), line, top=line)
    for sx in (-1, 1):
        mb.box(m, (sx * (il - 3.0), 0.36, 0), (0.14, 0.02, iw * 1.0), line, top=line)
        for sz in (-1, 1):
            mb.box(m, (sx * (il - 1.5), 0.36, sz * iw * 0.5), (3.0, 0.02, 0.14), line, top=line)
        # 골대
        mb.box(m, (sx * (il - 0.3), 1.3, 0), (0.15, 2.0, 0.15), POLE)
        mb.box(m, (sx * (il - 0.6), 2.4, 0), (0.1, 0.9, 1.6), line)
    # 펜스 (낮은 기둥 + 위 띠)
    for sx in (-1, 1):
        mb.box(m, (0, 1.4, sx * (hw + 0.5)), (hl * 2 + 1.0, 0.12, 0.08), POLE)
        mb.box(m, (sx * (hl + 0.5), 1.4, 0), (0.08, 0.12, hw * 2 + 1.0), POLE)
    n = int(hl * 2 / 4)
    for i in range(n + 1):
        x = -hl - 0.5 + (hl * 2 + 1.0) * i / n
        for sz in (-1, 1):
            mb.box(m, (x, 0.75, sz * (hw + 0.5)), (0.12, 1.4, 0.12), POLE)
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 2.0, 0.25
    ROOF_RESERVED.setdefault(b.id, []).append((rect_poly(fp.center, u, v, hl + 1.5, hw + 1.5), H + 2.0))
