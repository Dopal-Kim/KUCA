"""
컨셉아트 아이콘(Art/Concept/01_Buildings, 02_Landscapes)의 개성을 살리는 랜드마크 형태.
(이전 Assets/Editor/KeyArtLandmarks.cs 를 옮겨 와 다듬음)
"""
import math
import random

from .geo import Footprint
from .mesh import Frame, IDENT, Builder, outward_normal, shade

STONE = (0.94, 0.91, 0.83)
COLUMN = (0.98, 0.96, 0.90)
STEP = (0.87, 0.85, 0.79)
ROOF = (0.74, 0.76, 0.79)
DOME = (0.97, 0.97, 0.98)
DARK = (0.24, 0.30, 0.38)
SHUTTER = (0.55, 0.60, 0.67)   # 천문대 관측 창 닫힌 셔터 (M14)
SEAT = (0.30, 0.42, 0.60)      # 대운동장 스탠드 좌석 줄 (M14, 추정)
GLASS = (0.30, 0.42, 0.52)
SKY_GLASS = (0.66, 0.82, 0.93)   # 하늘이 비친 유리 (M10 도서관 천창)
GOLD = (0.86, 0.72, 0.40)
POLE = (0.62, 0.64, 0.66)
LAMP = (1.0, 0.98, 0.88)

# 사색의 광장(OSM place=square) 중심, 노천극장 옆 연못 중심 (월드 m)
PLAZA = (94.0, -352.0)
POND = (327.0, -594.0)

# 정면을 어느 쪽에서 찾을지: 건물 중심 → 목표 점 (없으면 사색의 광장)
FRONT_TARGET = {'way-455726113': lambda c: (c[0] - 300.0, c[1])}   # 체육대학관: 서쪽

LIBRARY, ARTS, PE, SEONSEUNG = 'way-474085534', 'way-474085536', 'way-455726113', 'way-585696506'
UJEONGWON, FOREIGN, ENGINEERING, ELECTRONICS = 'relation-8269760', 'way-585696507', 'way-455725718', 'way-474085540'
STUDENT_UNION = 'way-474085535'
TEAL_GLASS = (0.30, 0.66, 0.68)   # 실사 우정원 청록 유리 계단탑 · 캐노피
WISTERIA = (0.70, 0.58, 0.86)
VINE = (0.36, 0.56, 0.24)
DECK = (0.66, 0.48, 0.32)
OBSERVATORY, CERAMICS, THEATER, GATE = 'way-474521125', 'way-474521123', 'way-474531805', 'way-473963422'
GREENHOUSE = 'way-474521122'

# 실제 건물 층수 (나무위키 등 조사, 2026-10). OSM 에 층수가 없어 기본 높이가 틀린 건물을 바로잡는다.
REAL_LEVELS = {
    'relation-8269760': 7,   # 우정원: 지하1·지상7
    'way-474085540': 7,      # 전자정보대학관: 지하1·지상7 (ㅁ자)
    'way-474521122': 2,      # 원예생물공학온실: 층 자료 없음, 기본 18 m 는 6층 유리 상자처럼 보여 낮은 온실로 (M6, 실사 미확인)
    'way-474085537': 9,      # 멀티미디어교육관: 지하2·지상9 (공식 캠퍼스맵 층별 안내)
    'way-585696506': 5,      # 선승관: 지상 3층 대공간 체육관 (층고가 높아 5층 높이)
    'way-585696507': 5,      # 외국어대학관
    'way-474085534': 4,      # 중앙도서관 (연면적 17,498 m² / 바닥 5,500 m²)
    'way-455726113': 4,      # 체육대학관
    'way-474085536': 7,      # 예술디자인대학: B2·7F 본동 (앞 낮은 포르티코 전면동은 arts_palace)
    'way-455725718': 6,      # 공학관: 지하2·지상6 (공식 캠퍼스맵)
    'way-455728922': 2,      # 공학실험동: 지하1·지상2
    'way-474085539': 4,      # 국제대학 (국제·경영대학관): 지하1·지상4 (공식 캠퍼스맵)
    'way-474085542': 4,      # 국제학관: OSM 4층
    'way-474521123': 2,      # 도예관
    'way-474521125': 3,      # 천문대: 지하2·지상3 (공식 캠퍼스맵), 돔 아래 계단식 몸체
}
LEVEL_H = 3.3

CAMPUS_NAMES = {'학생회관', '원자로센터', '실습농장동', '실험연구동', '생명과학대학', '원예생물공학온실', '애지원', '공학실험동',
                '노천극장', '천문대', '선승관', '중앙도서관', '공학관', '멀티미디어교육관', '글로벌관', '우정원'}


def is_campus_seed(b):
    t = b.tags.get('building')
    return t in ('college', 'university', 'dormitory') or b.id in REAL_LEVELS or b.name in CAMPUS_NAMES \
        or '경희' in (b.name or '')

HIDDEN = {THEATER, GATE}          # 상자 건물 대신 이 형태만 보인다 (충돌체·정보는 Unity 에 남김)
NO_DETAILS = {THEATER, GATE, OBSERVATORY, GREENHOUSE}   # 공통 디테일(난간 등)을 붙이지 않을 건물


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

    b, f = fp(GREENHOUSE)
    if b:
        greenhouse_roof(builder_at(b), b, f)
        # M15: OSM 상 온실 남쪽 끝(동쪽 날개 · 둥근 돌출부)과 생명과학대학 북쪽 벽은 14 ~ 19 m 떨어진 따로 선 건물인데,
        #      35° 옆(N · W)에선 10 m 높이 온실 지붕이 틈을 가려 한 건물처럼 보였음 (REVIEW_v20) → 틈 가운데 나무 줄로 간격이 보이게
        #      (위치는 두 OSM 외곽에서 7 ~ 9 m 씩 떨어진 틈 가운데)
        from .nature import draw_tree
        rr = random.Random(474521122)
        for (px, pz), kind, sc in (((112.0, -95.5), 'round', 1.4), ((121.0, -97.5), 'cone', 1.3), ((130.0, -98.5), 'round', 1.4)):
            draw_tree(layer.at(px, pz), kind, px, pz, sc, rr.uniform(0, 360), rr)   # 수관 10 m: 온실 지붕(10 m) 너머로 보이게
            ground.occupy(px, pz, 4.0, ao=0.2, ao_radius=3.5)

    # 실사: 중앙도서관은 정면 거의 전체를 덮는 자이언트 오더 열주 (박공 없이 평평한 엔태블러처)
    # 정면 방향 (실사 · 항공 VR 확인, M6): 도서관 = 사색의 광장(동), 체육대학관 = 서쪽 긴 면 (대운동장 쪽 주차장 · 도로).
    # 예전엔 체육대학관도 광장 쪽(남쪽 짧은 면)에 붙어 실제 입구가 아닌 측면에 현관이 있었다.
    for bid, cols, pediment, frac, max_w in ((LIBRARY, 14, False, 0.78, 96.0), (PE, 14, False, 0.72, 86.0)):
        b, f = fp(bid)
        if b:
            face = f.face(FRONT_TARGET.get(bid, lambda c: PLAZA)(b.centroid))
            if entrances is not None and bid == PE:
                entrances.pop(PE, None)   # 작은 출입문 대신 이 열주 현관이 입구
            mb = builder_at(b)
            portico(mb, face, b.h, pediment, cols, grand=bid in (LIBRARY, PE), flags=(bid == LIBRARY),
                    width_frac=frac, max_width=max_w)
            if bid == PE:
                lions(mb, face, min(face['length'] * frac, max_w))   # 체육대학관: 대계단 양옆 웃는 사자상
                _rooftop_court(mb, b)    # 체육대학다운 지붕: 옥상 트랙 + 코트
            if bid == LIBRARY:
                atrium(mb, b, f)         # 중앙도서관: 가운데 아트리움 천창
            _occupy_face(ground, face, 18)

    b, f = fp(ARTS)
    if b and shells is not None:
        # 예술디자인대학: 궁전식 정면 (가운데 돌출 파빌리온 + 거대 기둥 + 박공 + 테라스 대계단)
        from .buildings import style_of
        st = style_of(b)
        arts_palace(builder_at(b), shells[st].at(*b.centroid), b, ground, front=(0.0, -1.0))
        if entrances is not None:
            entrances.pop(ARTS, None)

    b, f = fp(SEONSEUNG)
    if b:
        face = f.face(PLAZA)
        mb = builder_at(b)
        drum_roof(mb, b, f)              # 실사: 붉은 벽돌 원형 드럼 + 짙은 회색 낮은 둥근 지붕 + 금색 띠 (2017 개관 평화의전당형)
        drum_arcade(mb, b)               # 실사: 1층 반원 아치 회랑이 둘레를 한 바퀴
        clock_tower(mb, face, b.h, spire=SPIRE_GREEN)
        _occupy_face(ground, face, 12)

    b, f = fp(OBSERVATORY)
    if b:
        observatory(builder_at(b), b, f)

    # 도예관: 실사는 적갈색 외벽 2~3층 평지붕 (박공지붕 아님) → 공통 디테일 · 벽돌 스타일로

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
        stadium(layer, ground, buildings)

    # 사색의 광장: 오벨리스크 바깥 양옆 대칭 잔디 화단 + 둥근 회양목 (실사)
    for s in (-1, 1):
        px, pz = PLAZA[0] + tx * 30 + sx * 30 * s, PLAZA[1] + tz * 30 + sz * 30 * s
        lawn_bed(layer.at(px, pz), (px, pz), (tx, tz), 20.0, 10.0)
        ground.occupy(px, pz, 11, ao=0.0)

    if entrances is not None:
        secondary(by_id, entrances, builder_at, ground)


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


def portico(mb, f, height, pediment, max_cols, grand=False, flags=False, width_frac=0.5, max_width=44.0):
    """정면 열주 현관: 기단, 기둥, 엔태블러처, (페디먼트), 앞 계단. 로컬 X = 면 방향, Z = 바깥"""
    m = Frame.look(f['center'], f['normal'])
    width = min(f['length'] * width_frac, max_width)
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


SPIRE_GREEN = (0.36, 0.56, 0.44)   # 선승관 시계탑 첨탑 (실사: 녹청 동판)
DOME_GRAY = (0.50, 0.56, 0.64)     # 선승관 지붕 (실사: 짙은 회색 금속판). M6 청회색 (0.44,0.48,0.54) 도 Unity 에선 진회색 → M7 한 단계 밝고 푸르게


def drum_roof(mb, b, f):
    """선승관: 외곽을 따라 금색 띠 + 처마, 그 위 짙은 회색 낮은 타원 돔 (외곽 크기에 맞춘 납작한 반구)"""
    (ax, az), L, Wd = f.long_axis()
    H = b.h
    mb.band(b.poly, H - 2.6, H - 0.9, 0.18, -0.01, GOLD)                    # 금색 프리즈
    mb.band(b.poly, H - 0.2, H + 0.5, 0.9, 1.4, DOME_GRAY, top_col=shade(DOME_GRAY, 1.08))   # 내민 처마
    k = 0.47
    dome = Frame((f.center[0], H + 0.4, f.center[1]), (ax * L * k, 0.0, az * L * k), (0.0, min(L, Wd) * 0.16, 0.0),
                 (-az * Wd * k, 0.0, ax * Wd * k))
    # M11: 칸마다 밝기를 번갈아 칠하던 체커(타일 반복처럼 보였음, REVIEW_v15)를 끄고 고리를 늘려 면 경계를 부드럽게 — 결은 리브가 맡는다
    # M13: 단색 진회색 덩어리가 무거웠음 (PC v16 · M12 다음 후보) → 밑동은 그대로, 꼭대기로 갈수록 하늘이 비친 듯 밝게
    mb.hemisphere(dome, (0.0, 0.0, 0.0), 1.0, 48, 12, DOME_GRAY, checker=False, top_shade=1.25)
    # 금속판 이음 리브 16줄 (결 따라 밝기 변화: 단색 덩어리로 안 보이게)
    rib = shade(DOME_GRAY, 1.22)

    def unit(v):
        n = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2) or 1.0
        return (v[0] / n, v[1] / n, v[2] / n), n

    for i in range(16):
        th = 2 * math.pi * i / 16
        for j in range(6):
            ph = (j + 0.5) / 6 * (math.pi / 2) * 0.92
            dph = (math.pi / 2) * 0.92 / 6
            cp, sp, ct, st = math.cos(ph), math.sin(ph), math.cos(th), math.sin(th)
            o = dome.p((cp * ct * 1.003, sp * 1.003, cp * st * 1.003))
            along, ln = unit(dome.dir((-sp * ct, cp, -sp * st)))   # 자오선 방향 (돔 프레임은 축마다 배율이 달라 월드에서 정규화)
            across, _ = unit(dome.dir((-st, 0.0, ct)))
            out = (along[1] * across[2] - along[2] * across[1], along[2] * across[0] - along[0] * across[2], along[0] * across[1] - along[1] * across[0])
            mb.box(Frame(o, across, along, out), (0.0, 0.0, 0.0), (0.45, ln * dph + 0.2, 0.3), rib)
    lantern = Frame((f.center[0], H + 0.4 + min(L, Wd) * 0.16, f.center[1]))
    mb.prism(lantern, (0, -0.3, 0), 3.2, 1.6, 16, GOLD, caps=True)


def drum_arcade(mb, b):
    """선승관: 외벽 둘레 1층에 2층 높이 반원 아치 (밝은 석재 테 + 어두운 안쪽)"""
    n = len(b.poly)
    for i in range(n):
        a, c = b.poly[i], b.poly[(i + 1) % n]
        dx, dz = c[0] - a[0], c[1] - a[1]
        seg = math.hypot(dx, dz)
        count = int(seg / 4.6)
        if count < 1:
            continue
        nx, nz = outward_normal(b.poly, i)
        mid = ((a[0] + c[0]) / 2, 0.0, (a[1] + c[1]) / 2)
        m = Frame.look(mid, (nx, 0.0, nz))
        w = seg / count
        for k in range(count):
            x = -seg / 2 + w * (k + 0.5)
            mb.arch_panel(m, (x, 0.0, 0.05), w * 0.78, 6.6, COLUMN)
            mb.arch_panel(m, (x, 0.0, 0.1), w * 0.6, 6.0, DARK)


def clock_tower(mb, f, height, spire=None):
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
    spire_h = 11.0 if spire else 7.0
    mb.cone(m, (0, tower_h + 0.8, 0), (w + 1.4) * 0.72, spire_h, 4, spire or ROOF, rot=45)
    mb.prism(m, (0, tower_h + 0.6 + spire_h, 0), 0.12, 2.2, 4, GOLD)
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
    # M12: 칸마다 밝기를 번갈아 칠하던 체커(선승관과 같은 문제, Unity v15 사방 모두 바둑판)를 끄고 칸을 늘려 매끈하게,
    #      결은 금속판 이음 리브 12줄이 맡는다 (관측 창 자리는 비움)
    # M13: 40 × 10 칸은 빛에 따라 작은 다각형 면이 보였음 (MAP_REVIEW_v18) → 64 × 16
    # M14: 지도 메시는 면 법선(각진 음영)이라 64 × 16 도 N · W 에서 작은 사각 칸이 보였음 (REVIEW_v19) → 96 × 24 (이웃 면 각도 5.6° → 3.75°)
    mb.hemisphere(IDENT, (cx, b.h + 3.7, cz), r, 96, 24, DOME, checker=False)
    rib = shade(DOME, 0.88)
    for i in range(12):
        th = 2 * math.pi * (i + 0.5) / 12
        if abs(math.cos(th)) < 0.2 and math.sin(th) < 0:   # 관측 창(-Z) 바로 옆
            continue
        ct, st = math.cos(th), math.sin(th)
        # M15: 곧은 상자 7토막은 이음매마다 끊겨 점선처럼 보였음 (REVIEW_v20) → 약 3.7° (돔 고리 간격) 마다 꺾이는 이어진 띠 하나 (윗면 + 양옆)
        def P(ph, w, rr):
            cp, sp = math.cos(ph), math.sin(ph)
            return (cx + cp * ct * rr - st * w, b.h + 3.7 + sp * rr, cz + cp * st * rr + ct * w)
        n = 22
        for j in range(n):
            a0, a1 = j / n * (math.pi / 2) * 0.9, (j + 1) / n * (math.pi / 2) * 0.9
            mid = P((a0 + a1) / 2, 0.0, r)
            mb.quad(IDENT, P(a0, -0.11, r + 0.1), P(a0, 0.11, r + 0.1), P(a1, 0.11, r + 0.1), P(a1, -0.11, r + 0.1), rib, mid)
            for w in (-0.11, 0.11):
                mb.quad(IDENT, P(a0, w, r - 0.03), P(a0, w, r + 0.1), P(a1, w, r + 0.1), P(a1, w, r - 0.03), shade(rib, 0.94),
                        P((a0 + a1) / 2, 0.0, r + 0.03))
    mb.prism(IDENT, (cx, b.h + 3.6, cz), r + 0.2, 0.3, 20, COLUMN, r_top=r + 0.2)
    # 관측 창: 남쪽 위로 세로 띠
    slit = Frame((cx, b.h + 3.7, cz)).child((0, 0, 0), 180)
    # 관측 창: 돔 밑동에서 꼭대기 너머까지 이어진 셔터 띠 + 양옆 셔터 레일
    n = 12
    for k in range(n):
        a0, a1 = math.radians(4 + k * 92 / n), math.radians(4 + (k + 1) * 92 / n)
        # M13: 위(T)에서 돔 가운데를 가르는 굵은 막대로 보였음 (MAP_REVIEW_v18) → 창 1.6 → 1.1 m, 레일 포함 2.4 → 1.6 m (약 2/3)
        # M14: 1.1 m 도 위(T)에서 굵은 막대 (REVIEW_v19) — 굵기보다 흰 돔 위 짙은 남색 대비가 컸음 → 닫힌 셔터 색(청회색 금속) + 0.8 m
        for x0, x1, col, off in ((-0.4, 0.4, SHUTTER, 0.06), (-0.6, -0.4, COLUMN, 0.1), (0.4, 0.6, COLUMN, 0.1)):
            rr = r + off
            p0 = (x0, math.sin(a0) * rr, math.cos(a0) * rr)
            p1 = (x1, math.sin(a0) * rr, math.cos(a0) * rr)
            p2 = (x1, math.sin(a1) * rr, math.cos(a1) * rr)
            p3 = (x0, math.sin(a1) * rr, math.cos(a1) * rr)
            mb.quad(slit, p0, p1, p2, p3, col, (0, 0, 0))
    # M16: 꼭대기 너머 슬릿 끝이 리브와 같은 굵기라 셔터로 안 읽혔음 (REVIEW_v21) → 레일보다 넓고 두툼한 짙은 셔터 캡 (윗면 + 옆 + 끝면)
    cap, cw, lo, hi = shade(SHUTTER, 0.8), 0.85, r + 0.02, r + 0.32
    angs = [math.radians(a) for a in (84, 88, 92, 96, 100)]

    def C(a, x, rr):
        return (x, math.sin(a) * rr, math.cos(a) * rr)
    for a0, a1 in zip(angs, angs[1:]):
        mb.quad(slit, C(a0, -cw, hi), C(a0, cw, hi), C(a1, cw, hi), C(a1, -cw, hi), cap, (0, 0, 0))
        for x in (-cw, cw):
            mb.quad(slit, C(a0, x, lo), C(a0, x, hi), C(a1, x, hi), C(a1, x, lo), shade(cap, 0.9), C((a0 + a1) / 2, 0.0, hi))
    for a, inn in ((angs[0], angs[1]), (angs[-1], angs[-2])):
        mb.quad(slit, C(a, -cw, lo), C(a, cw, lo), C(a, cw, hi), C(a, -cw, hi), shade(cap, 0.85), C(inn, 0.0, (lo + hi) / 2))
    # 기본 공통 디테일 대신 난간만
    mb.ao_floor = 0.0
    mb.band(b.poly, b.h - 0.05, b.h + 0.7, 0.12, 0.4, COLUMN)
    mb.band(b.poly, 0.0, 0.9, 0.35, 0.0, (0.82, 0.77, 0.68))


GH_GLASS = (0.72, 0.87, 0.94)
GH_FRAME = (0.93, 0.95, 0.96)


def greenhouse_roof(mb, b, f):
    """원예생물공학온실: 하늘색 유리 박공 여러 줄 (벤로형 연동 온실). 건물 윤곽 안에 들어오는 칸에만 얹는다 (ㄱ자 모양도 맞게)"""
    from .geo import point_in_poly
    (ax, az), length, width = f.long_axis()
    m = Frame.look((f.center[0], b.h, f.center[1]), (ax, 0.0, az))   # 로컬 z = 긴 방향 (용마루 방향)
    span, seg = 4.0, 4.0
    nx, nz = max(1, int(width / span)), max(1, int(length / seg))
    for i in range(nx):
        x = -width / 2 + span * (i + 0.5)
        for k in range(nz):
            z = -length / 2 + seg * (k + 0.5)
            p = m.p((x, 0.0, z))
            if not point_in_poly(p[0], p[2], b.poly):
                continue
            mb.gable(m, (x, 0.0, z), span, seg + 0.02, 1.6, GH_GLASS, True, end_col=GH_GLASS)
            mb.box(m, (x, 1.62, z), (0.18, 0.12, seg + 0.02), GH_FRAME)   # 용마루 틀
            mb.box(m, (x - span / 2, 0.05, z), (0.16, 0.12, seg + 0.02), GH_FRAME)   # 골 틀


def pitched_roof(mb, b, f):
    """도예관 (키아트): 연회색 금속 박공지붕 (세로 이음 줄) + 지붕면에 납작한 천창 3개 + 박공 끝 환기창"""
    (ax, az), length, width = f.long_axis()
    m = Frame.look((f.center[0], b.h, f.center[1]), (ax, 0.0, az))
    L, Wd = length + 1.6, width + 1.6
    rh = max(3.0, min(Wd * 0.3, 6.5))
    metal = (0.76, 0.80, 0.85)
    mb.ao_floor = b.h
    mb.gable(m, (0, 0, 0), Wd, L, rh, metal, True, end_col=STONE)
    # 처마 띠
    mb.box(m, (0, -0.2, 0), (Wd + 0.2, 0.4, L + 0.2), COLUMN)
    # 세로 이음 줄 (처마 → 용마루) 과 용마루 덮개
    half = Wd / 2
    n_seam = max(4, int(L / 1.8))
    for sgn in (-1, 1):
        for i in range(n_seam + 1):
            z = -L / 2 + 0.4 + (L - 0.8) * i / n_seam
            p0 = (sgn * half, 0.02, z)
            p1 = (0.0, rh + 0.02, z)
            mb.quad(m, (p0[0], p0[1] + 0.04, z - 0.06), (p0[0], p0[1] + 0.04, z + 0.06), (p1[0], p1[1] + 0.04, z + 0.06),
                    (p1[0], p1[1] + 0.04, z - 0.06), shade(metal, 1.08), (0, -1.0, z))
    mb.box(m, (0, rh + 0.05, 0), (0.6, 0.18, L + 0.3), shade(metal, 0.92))

    def on_slope(sgn, t, z):          # t: 처마 0 → 용마루 1
        return (sgn * half * (1 - t), rh * t + 0.07, z)
    n = 3 if L > 18 else 2
    for i in range(n):
        z0 = -L / 2 + L * (i + 0.5) / n
        for sgn in (-1, 1):
            if sgn > 0 and i != n // 2:
                continue
            a0, a1 = on_slope(sgn, 0.38, z0 - 1.3), on_slope(sgn, 0.72, z0 - 1.3)
            b0, b1 = on_slope(sgn, 0.38, z0 + 1.3), on_slope(sgn, 0.72, z0 + 1.3)
            fa0, fa1 = on_slope(sgn, 0.34, z0 - 1.55), on_slope(sgn, 0.76, z0 - 1.55)
            fb0, fb1 = on_slope(sgn, 0.34, z0 + 1.55), on_slope(sgn, 0.76, z0 + 1.55)
            mb.quad(m, fa0, fb0, fb1, fa1, COLUMN, (0, -1.0, z0))
            lift = lambda p: (p[0], p[1] + 0.05, p[2])
            mb.quad(m, lift(a0), lift(b0), lift(b1), lift(a1), (0.42, 0.62, 0.80), (0, -1.0, z0))
    # 박공 끝 둥근 환기창
    for sgn in (-1, 1):
        mb.disc(m.child((0, 0, 0), 0 if sgn > 0 else 180), (0, rh * 0.45, L / 2 + 0.05), 0.7, 0.05, 12, (0.42, 0.62, 0.80))
    mb.ao_floor = 0.0
    mb.band(b.poly, 0.0, 0.9, 0.35, 0.0, (0.82, 0.77, 0.68))


def amphitheater(mb, f, to_pond, ground):
    c, n = to_pond['center'], to_pond['normal']
    stage = (c[0] - n[0] * min(14, to_pond['depth'] * 0.3), 0.0, c[2] - n[2] * min(14, to_pond['depth'] * 0.3))
    m = Frame.look(stage, n)
    max_r = min(f.half_u, f.half_v) * 1.6
    tiers = max(4, min(int((max_r - 12) / 3), 9))
    center, spread = -math.pi / 2, math.radians(80)
    # M10 실사: 회백 화강석 계단 객석 (예전 석재 · 잔디 단 번갈이는 초록 · 흰 줄무늬 잔디 계단처럼 보였음).
    # 한 단(3 m)을 낮은 두 칸으로 나누고 두 돌 색을 번갈아 → 촘촘한 돌계단, 통로 계단 3줄로 객석을 나눈다
    seat = (0.80, 0.78, 0.73)
    for i in range(tiers):
        r0 = 12 + i * 3.0
        for k in range(2):
            mb.arc_tier(m, (0, 0, 0), r0 + 1.5 * k, r0 + 1.5 * (k + 1), center - spread, center + spread,
                        0.6 * i + 0.3 * (k + 1), 24, seat if k == 0 else shade(seat, 0.92))
            for deg in (-45, 0, 45):   # 통로 계단 (어두운 줄)
                a, rm = center + math.radians(deg), r0 + 1.5 * k + 0.75
                am = m.child((math.cos(a) * rm, 0, math.sin(a) * rm), 90 - math.degrees(a))
                mb.box(am, (0, 0.6 * i + 0.3 * (k + 1) + 0.01, 0), (1.6, 0.04, 1.5), shade(STEP, 0.72), top=shade(STEP, 0.72))
    outer = 12 + tiers * 3.0
    # 뒤쪽 둘레 벽
    mb.arc_tier(m, (0, 0, 0), outer, outer + 1.0, center - spread, center + spread, 0.6 * tiers + 1.0, 24, COLUMN)
    # 무대, 배경 벽(코니스·기둥), 양옆 기둥
    mb.box(m, (0, 0.6, 2), (18, 1.2, 10), STEP)
    mb.box(m, (0, 0.62, 2), (16, 1.2, 8.6), shade(STEP, 1.05))
    mb.box(m, (0, 3.8, 7.6), (22, 7.6, 1.4), STONE)
    # M16: 아치 판이 +Z(연못 쪽)를 보고 벽 앞면(z 6.9) 안쪽에 묻혀 객석에서 민짜 벽으로 보였음 → 180° 돌린 틀로 객석 쪽을 보게
    back = m.child((0, 0, 0), 180)
    for k in (-2, -1, 0, 1, 2):
        mb.arch_panel(back, (k * 4.2, 1.2, -6.86), 2.4, 4.6, shade(STONE, 0.8))
    # M17: 뒷면(연못 쪽, z 8.3)이 민짜 흰 벽으로 보였음 (REVIEW_v22 N · E) → 아래 굽 + 한 톤 어두운 돌 띠 둘
    #      (0.14 m 줄눈은 지도 거리에서 점선처럼 깨졌음)
    # M18: 0.84 · 0.4 m 띠가 Unity N · E 에서 아주 옅게만 보였음 (REVIEW_v22_day) → 두 단계 진하게 (0.66) · 0.6 m, 굽도 0.72
    mb.box(m, (0, 0.45, 8.36), (22.1, 0.9, 0.16), shade(STONE, 0.72))
    for y in (2.6, 4.9):
        mb.box(m, (0, y, 8.37), (22.0, 0.6, 0.14), shade(STONE, 0.66))
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
    정문 (새천년기념탑, 네오르네상스문). 실사: 가운데 높은 반원 아치 1개 (두꺼운 사각 기둥 + 꼭대기 문장 장식),
    좌우로 낮은 2열 원기둥 회랑 + 엔태블러처, 양 끝 사각 파일런 위 조각상. 차로는 가운데 아치 밑으로 지난다.
    """
    from .nature import LIGHTS
    (ax, az), length, _ = f.long_axis()
    W = max(length, 44.0)
    # 로컬 X = 문의 긴 방향, 로컬 Z = 길 방향
    m = Frame.look((f.center[0], 0.0, f.center[1]), (az, 0.0, -ax))
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 3.0, 0.3
    # 1) 가운데 아치 (M6, 실사 비례): 넓은 반원 개구부(덩어리 폭의 약 2/3) + 날씬한 기둥, 덩어리 폭 ≥ 높이.
    #    예전 (통로 9 m · 기둥 5 m · 높은 아틱) 은 가운데가 좁고 높은 'I' 자처럼 보였다.
    # M11: 깊이 12 → 16 m — 옆(W · E)에서 가운데 덩어리 · 끝 기둥탑이 얇은 탑 두 개처럼 보였음 (REVIEW_v15). 정면 모양(X · Y)은 그대로
    D, pier, r, spring = 16.0, 4.5, 7.5, 8.0
    cw = 2 * r + 2 * pier
    block_top = spring + r + 4.0   # M7 실사: 아치 꼭대기가 덩어리(코니스까지)의 약 4분의 3 — 2.0 은 아치 위 벽이 얇아 아치가 커 보였음
    for s in (-1, 1):
        px = s * (r + pier / 2)
        mb.box(m, (px, 0.4, 0), (pier + 1.2, 0.8, D + 1.2), STEP)
        mb.box(m, (px, spring / 2, 0), (pier, spring, D), STONE)
        for sz in (-1, 1):   # 기둥 앞뒤로 붙은 높은 원기둥 2개씩 (엔태블러처까지)
            for cx in (-1, 1):
                column(mb, m, (px + cx * 1.3, 0.8, sz * (D / 2 + 0.5)), 0.55, block_top - 1.6)
            side = m.child((px, 0, sz * (D / 2)), 0 if sz > 0 else 180)
            mb.arch_panel(side, (0, 2.2, 0.03), 1.4, 3.6, shade(STONE, 0.8))   # 기둥 벽감
    n = 32   # 아치 위 벽(스팬드럴): 아치 곡선에서 블록 위까지 세로 띠로 채운다 (넓은 아치라 촘촘히: 12 는 계단처럼 보였음)
    for i in range(n):
        x0, x1 = -r + 2 * r * i / n, -r + 2 * r * (i + 1) / n
        xm = (x0 + x1) / 2
        y = spring + math.sqrt(max(r * r - xm * xm, 0.0))
        mb.box(m, (xm, (y + block_top) / 2, 0), (x1 - x0 + 0.02, block_top - y, D), STONE)
    for k in range(24):   # 아치 테 (밝은 돌): 곡선을 따라 돌린 조각 (축 정렬 상자는 계단처럼 보였음)
        a0, a1 = math.pi * k / 24, math.pi * (k + 1) / 24
        x0, y0 = -math.cos(a0) * (r + 0.35), spring + math.sin(a0) * (r + 0.35)
        x1, y1 = -math.cos(a1) * (r + 0.35), spring + math.sin(a1) * (r + 0.35)
        seg = math.hypot(x1 - x0, y1 - y0)
        tx, ty = (x1 - x0) / seg, (y1 - y0) / seg
        for sz in (-1, 1):
            o = m.p(((x0 + x1) / 2, (y0 + y1) / 2, sz * (D / 2 + 0.1)))
            mb.box(Frame(o, m.dir((tx, ty, 0.0)), m.dir((-ty, tx, 0.0)), m.dir((0.0, 0.0, 1.0))), (0, 0, 0), (seg + 0.08, 0.8, 0.3), COLUMN)
    for sz in (-1, 1):   # 쐐기돌
        mb.box(m, (0, spring + r + 0.2, sz * (D / 2 + 0.15)), (1.4, 1.9, 0.5), COLUMN)
    mb.box(m, (0, spring + 0.3, 0), (2 * r + 0.6, 0.5, D + 0.3), COLUMN)   # 아치 시작선 (임포스트)
    mb.box(m, (0, block_top + 0.4, 0), (cw + 1.4, 0.8, D + 1.4), COLUMN)   # 엔태블러처 · 코니스
    AT = 2.6   # 아틱 높이 (실사: 코니스 위 조각 띠가 뚜렷)
    mb.box(m, (0, block_top + 0.8 + AT / 2, 0), (cw - 1.0, AT, D - 0.6), STONE)   # 아틱
    mb.box(m, (0, block_top + 0.95 + AT, 0), (cw - 0.4, 0.3, D - 0.2), COLUMN)
    for side_yaw in (0, 180):   # 아틱 가운데 문장 (금색 원판)
        side = m.child((0, 0, 0), side_yaw)
        mb.disc(side, (0, block_top + 0.8 + AT / 2, (D - 0.6) / 2 + 0.06), 1.0, 0.1, 20, GOLD)
    top = block_top + 1.1 + AT
    # M12 (PC v16 ③): 위(T)에서 23 × 15 m 흰 평면 하나로 보였음 → 회색 금속 지붕 판(가장자리 흰 테만 남김) + 앞뒤 난간 + 조각 밑 두 단 받침
    #      (실제 윗면 마감은 확인 못 함 — 추정)
    mb.box(m, (0, top + 0.03, 0), (cw - 2.6, 0.06, D - 2.4), shade(ROOF, 0.96), top=shade(ROOF, 0.96))
    for sz in (-1, 1):
        balustrade(mb, m, (0, top, sz * (D / 2 - 0.9)), cw - 3.0, 0.8)
    mb.box(m, (0, top + 0.2, 0), (9.0, 0.4, 6.0), STONE)
    mb.box(m, (0, top + 0.45, 0), (7.0, 0.1, 4.4), COLUMN)
    mb.box(m, (0, top + 0.5, 0), (5.0, 1.0, 2.6), COLUMN)   # 꼭대기 조각 무리
    mb.ico(m, (0, top + 1.9, 0), (1.5, 1.6, 1.0), COLUMN, var=0.05)
    for s in (-1, 1):
        mb.ico(m, (s * 2.0, top + 1.4, 0), (0.7, 1.1, 0.7), COLUMN, var=0.05)
        mb.ico(m, (s * (cw / 2 - 0.6), top + 0.7, 0), (0.6, 1.0, 0.6), COLUMN, var=0.05)
    # 2) 좌우 낮은 회랑: 원기둥 2열 + 엔태블러처, 3) 양 끝 파일런 + 조각상
    pyl, Dc, col_h = 5.5, 9.0, 7.4   # 열주 위 = 가운데 덩어리의 약 0.55 (실사). M11: 회랑 깊이 7 → 9, 끝 기둥탑 Dc + 3
    x_in, x_out = cw / 2, W / 2 - pyl
    run = x_out - x_in
    ncol = max(3, int(run / 3.0))
    for s in (-1, 1):
        mid = s * (x_in + run / 2)
        mb.box(m, (mid, 0.3, 0), (run, 0.6, Dc + 1.0), STEP)
        for i in range(ncol + 1):
            x = s * (x_in + 1.0 + (run - 2.0) * i / ncol)
            for sz in (-1, 1):
                column(mb, m, (x, 0.6, sz * (Dc / 2 - 0.6)), 0.45, col_h)
        mb.box(m, (mid, 0.6 + col_h + 0.6, 0), (run, 1.2, Dc), STONE)
        mb.box(m, (mid, 0.6 + col_h + 1.35, 0), (run + 0.4, 0.3, Dc + 0.6), COLUMN)
        balustrade(mb, m, (mid, 0.6 + col_h + 1.5, 0), run - 1.0, 0.8)
        px = s * (W / 2 - pyl / 2)
        ph = 0.6 + col_h + 1.5   # 끝 기둥탑은 열주 높이까지 + 위 조각상 (예전 11 m 는 열주보다 높아 계단이 거꾸로였음)
        PD = Dc + 3.0
        mb.box(m, (px, 0.4, 0), (pyl + 1.0, 0.8, PD + 1.0), STEP)
        mb.box(m, (px, ph / 2, 0), (pyl, ph, PD), STONE)
        mb.box(m, (px, ph + 0.3, 0), (pyl + 0.6, 0.6, PD + 0.6), COLUMN)
        for sz in (-1, 1):
            mb.box(m, (px, ph * 0.55, sz * (PD / 2 + 0.06)), (pyl - 1.6, ph * 0.5, 0.12), shade(STONE, 0.86), top=shade(STONE, 0.86))   # 부조 판
        side = m.child((s * W / 2, 0, 0), 90 * s)   # 바깥 옆면 부조 판 (옆에서 민벽 대신 두께 있는 기둥탑으로)
        mb.box(side, (0, ph * 0.55, 0.06), (PD - 2.4, ph * 0.5, 0.12), shade(STONE, 0.86), top=shade(STONE, 0.86))
        mb.box(m, (px, ph + 1.1, 0), (2.0, 1.0, 2.0), STONE)
        mb.ico(m, (px, ph + 2.6, 0), (0.8, 1.4, 0.8), COLUMN, var=0.06)   # 조각상
    # 밤: 아치 밑 등
    mb.emissive = True
    for s in (-1, 1):
        mb.box(m, (s * (r - 0.3), spring - 2.0, 0), (0.5, 1.2, 1.0), LAMP)
    mb.emissive = False
    p = m.p((0, 0, 0))
    LIGHTS.append((p[0], p[2], 12.0, 0.9))
    if ground is not None:
        # 문 전체에 나무·벤치가 서지 않게, 기둥 · 파일런 밑은 그늘
        for k in range(int(-D / 2) - 4, int(D / 2) + 5, 2):
            for dx in range(int(-W / 2) - 3, int(W / 2) + 4, 2):
                p = m.p((dx, 0, k))
                near = (r - 1 < abs(dx) < r + pier + 1 or abs(dx) > W / 2 - pyl - 1) and abs(k) < D / 2 + 1
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


def stadium(layer, ground, buildings=()):
    """대운동장 네 모서리 조명탑 + 양쪽 긴 변 계단식 스탠드 (실사: 흙 운동장 둘레 스탠드)
    짧은 변(북 · 남)엔 스탠드 없음: OSM 에 그쪽 관람석 근거가 없고, 둘레 차도(way-423471655)가 8 ~ 15 m 바깥을 돈다 (M15)"""
    from .geo import dist_to_poly_edge, point_in_poly
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
    # 스탠드: 양쪽 긴 변 바깥. 서쪽(또는 남쪽)은 지붕 있는 본부석 포함
    if long_z:
        sides = (((x0 - 9, 0.0, cz), (1.0, 0.0, 0.0), True), ((x1 + 9, 0.0, cz), (-1.0, 0.0, 0.0), False))
        length = (z1 - z0) * 0.8
    else:
        sides = (((cx, 0.0, z0 - 9), (0.0, 0.0, 1.0), True), ((cx, 0.0, z1 + 9), (0.0, 0.0, -1.0), False))
        length = (x1 - x0) * 0.8
    for pos, face, main in sides:
        mb = layer.at(pos[0], pos[2])
        m = Frame.look(pos, face)
        # 계단 10단 (높이 5.5 m, 깊이 10 m): 6단은 멀리서 얇은 띠로만 보였음 (M4)
        # M14: 디딤 · 챌면이 거의 같은 흰색이라 계단 덩어리로만 보였음 (REVIEW_v19) → 챌면 한 톤 어둡게 + 단마다 좌석 줄
        #      (14 m 블록마다 2 m 통로). 좌석 색은 실사 확인 못 함 — 흙 · 잔디와 갈리는 차분한 청회색 (추정)
        #      계단 · 뒤 벽도 블록마다 끊는다: 150 m 상자 하나는 양 끝 정점만 지형 높이를 따라가 가운데 좌석을 덮었음
        near = [bb for bb in buildings if math.hypot(bb.centroid[0] - pos[0], bb.centroid[1] - pos[2]) < length]

        def clear(lx):
            # 스탠드 단면(계단 앞 ~ 뒤 기둥 띠)이 건물 외곽에서 6 m 밖인지
            for lz in (3.5, -2.0, -7.5):
                q = m.p((lx, 0.0, lz))
                if any(point_in_poly(q[0], q[2], bb.poly) or dist_to_poly_edge(q[0], q[2], bb.poly) < 6.0 for bb in near):
                    return False
            return True
        # M15: 서쪽 스탠드 북쪽 토막이 멀티미디어교육관 벽을 따라 2.5 ~ 5.5 m 로 붙어 닿아 보였음 (REVIEW_v20) → 양 끝 토막(가운데
        #      본부석 지붕 폭 밖)에서 건물 외곽 6 m 안에 드는 곳이 있으면, 그중 가장 안쪽 자리 1 m 앞에서 스탠드를 자른다.
        #      건물이 끝(끝에서 15 m 안) 옆에 있을 때만 — 가운데 옆 건물(지붕 밑 여자동)은 그대로, 그 너머 남쪽 스탠드도 남긴다.
        #      (끝 자체는 비스듬한 벽에서 5.5 m 라 끝에서부터 걸어 들어오면 못 찾음)
        lo, hi = -length / 2, length / 2
        for sgn in (1, -1):
            bad = [k for k in range(int(length * 0.2), int(length / 2) + 1) if not clear(sgn * k)]
            if bad and max(bad) >= length / 2 - 15:
                if sgn > 0:
                    hi = min(bad) - 1.0
                else:
                    lo = -min(bad) + 1.0
        if hi - lo < length - 0.5:
            print(f'stadium: 스탠드 끝 자름 {length:.0f} → {hi - lo:.0f} m')
        nb = max(1, round((hi - lo) / 14))
        bw = (hi - lo) / nb
        for k in range(nb):
            bx = lo + (k + 0.5) * bw
            for i in range(10):
                top = 0.55 * (i + 1)
                mb.box(m, (bx, top / 2, 3 - i * 1.0), (bw, top, 1.0), shade(STEP, 0.82), top=STEP)
                mb.box(m, (bx, top + 0.15, 3 - i * 1.0 - 0.05), (bw - 2.0, 0.3, 0.5), SEAT if i % 2 else shade(SEAT, 1.08))
            mb.box(m, (bx, 3.2, -6.9), (bw, 6.4, 0.8), shade(STEP, 0.92))   # 뒤 벽
            # M12: 100 m 넘는 민짜 뒤 벽 → 6 m 마다 기둥 띠 + 위 갓돌 (멀티미디어 · 여자동 쪽에서 큰 회색 판으로 보였음)
            mb.box(m, (bx, 6.5, -6.9), (bw + 0.02, 0.3, 1.1), shade(STEP, 1.04))
        n6 = max(1, int((hi - lo) / 6))
        for k in range(n6 + 1):
            mb.box(m, (lo + k * (hi - lo) / n6, 3.2, -7.35), (0.7, 6.4, 0.3), shade(STEP, 0.84))
        if main:
            # M12: 본부석 지붕이 뒤쪽 가는 기둥 3개(위에서 안 보임)뿐이라 스탠드 위에 떠 있는 흰 판처럼 보였음
            #      → 앞쪽 굵은 기둥 5개 + 뒤 기둥 5개(바닥부터), 지붕 앞 처마 띠 · 어두운 밑면 (실제 본부석 구조는 확인 못 함 — 추정)
            r0, r1 = max(-length * 0.2, lo + 0.6), min(length * 0.2, hi - 0.6)   # 자른 스탠드 밖으로 나가지 않게 (M15)
            w, rc = r1 - r0, (r0 + r1) / 2
            mb.box(m, (rc, 8.45, -1.6), (w + 1, 0.4, 10.0), (0.95, 0.95, 0.96))
            mb.box(m, (rc, 8.2, -1.6), (w + 0.6, 0.12, 9.6), (0.62, 0.64, 0.68))       # 밑면 그늘
            mb.box(m, (rc, 8.35, 3.45), (w + 1.2, 0.9, 0.35), shade(STEP, 1.02))       # 앞 처마 띠
            for s in (-1, -0.5, 0, 0.5, 1):
                mb.prism(m, (rc + s * w / 2, 0.55, 2.9), 0.32, 7.65, 8, POLE)
                mb.prism(m, (rc + s * w / 2, 0.0, -6.0), 0.32, 8.2, 8, POLE)
        for k in range(int(lo), int(hi) + 1, 3):
            p = m.p((k, 0, 0))
            ground.occupy(p[0], p[2], 4.0)


# ---------- 보조 건물 (사이클 M4, 실사 기준) ----------

def _entrance_frame(b, ent, out=0.0):
    """입구 변 위 로컬 틀 (X = 변 방향, Z = 바깥)과 입구 변 길이"""
    i = ent['edge']
    a, c = b.poly[i], b.poly[(i + 1) % len(b.poly)]
    nx, nz = ent['n']
    m = Frame.look((ent['x'] + nx * out, 0.0, ent['z'] + nz * out), (nx, 0.0, nz))
    return m, math.hypot(c[0] - a[0], c[1] - a[1])


def secondary(by_id, entrances, builder_at, ground):
    from .buildings import MASSING
    for bid, fn in ((UJEONGWON, ujeongwon_front), (FOREIGN, foreign_canopy), (ENGINEERING, engineering_arcade),
                    (ELECTRONICS, electronics_plaza), (STUDENT_UNION, union_canopy), (CERAMICS, ceramics_deck)):
        b, ent = by_id.get(bid), entrances.get(bid)
        if b is None or ent is None:
            print('landmarks: 보조 건물 입구 없음', bid)
            continue
        out = 2.4 if 'bay' in (MASSING.get(bid) or ()) else 0.0   # 정면 돌출 동 앞면에서 시작
        m, elen = _entrance_frame(b, ent, out)
        if bid == CERAMICS:
            ceramics_stair(builder_at(b), m, b, elen)
            # 도예관 입구 쪽은 옆 건물과 붙은 좁은 틈 → 데크는 숲길이 오는 서쪽 면에
            f = Footprint(b.poly, b.h).face((b.centroid[0] - 200.0, b.centroid[1]))
            m, elen = Frame.look(f['center'], f['normal']), f['length']
        fn(builder_at(b), m, b, elen, ground)


def ujeongwon_front(mb, m, b, elen, ground):
    """우정원: 정면 양 끝 청록 유리 계단탑(둥근 원통, 지붕 위로 한 층) + 1층 청록 캐노피 현관"""
    H = b.h
    # 실사: 계단탑은 정면 양 끝. 정면 띠(정면에서 안쪽 12 m 까지)에 있는 윤곽 꼭짓점을 정면 방향으로 투영해 양 끝을 찾는다
    # (안쪽으로 걸어가며 찾던 방식은 중정에 막혀 입구 변 끝에서 멈췄음)
    loc = [((px - m.o[0]) * m.x[0] + (pz - m.o[2]) * m.x[2], (px - m.o[0]) * m.z[0] + (pz - m.o[2]) * m.z[2]) for px, pz in b.poly]
    front = [lx for lx, lz in loc if -12.0 <= lz <= 1.5]
    ends = (min(front + [-elen / 2]), max(front + [elen / 2]))
    for s, edge in zip((-1, 1), ends):
        x = edge - s * 3.0
        mb.prism(m, (x, 0.0, 1.2), 3.6, H + 3.3, 14, TEAL_GLASS, caps=True)
        for k in range(1, int((H + 3.3) / 3.3)):
            mb.prism(m, (x, k * 3.3 - 0.15, 1.2), 3.75, 0.3, 14, COLUMN)   # 층 띠
        mb.prism(m, (x, H + 3.3, 1.2), 3.9, 0.5, 14, COLUMN, caps=True)  # 꼭대기 테
    w = min(elen * 0.45, 26.0)
    mb.box(m, (0, 4.6, 2.8), (w, 0.5, 5.6), TEAL_GLASS, top=shade(TEAL_GLASS, 1.15))
    for x in (-w / 2 + 0.6, -w / 6, w / 6, w / 2 - 0.6):
        mb.prism(m, (x, 0.0, 5.2), 0.22, 4.4, 8, COLUMN)
    # 가운데 연녹색 작은 박공 장식 입구
    mb.gable(m, (0, 4.85, 2.8), 7.0, 5.6, 1.8, (0.72, 0.86, 0.66), True, end_col=(0.80, 0.90, 0.74))


def foreign_canopy(mb, m, b, elen, ground):
    """외국어대학관: 가운데 입구 위로 크게 내민 캔틸레버 평 캐노피 (기둥 없음) + 넓은 계단"""
    w = min(max(elen * 0.6, 18.0), 26.0)
    mb.box(m, (0, 6.0, 4.6), (w, 0.9, 9.2), COLUMN, top=shade(COLUMN, 1.02))
    mb.box(m, (0, 5.5, 4.6), (w - 0.6, 0.1, 8.8), (0.98, 0.95, 0.82), top=(0.98, 0.95, 0.82))   # 밑면 조명 띠
    for k in range(4):
        mb.box(m, (0, 0.12 * (4 - k), 3.2 + k * 1.0), (w + 2 - k * 0.6, 0.24 * (4 - k), 1.0), STEP)


def engineering_arcade(mb, m, b, elen, ground):
    """공학관: 입구 변을 따라 1층 기둥 아케이드 (위 평 슬래브) + 가운데 반원 캐노피 (실사)"""
    L = elen - 6.0
    if L < 12:
        return
    mb.box(m, (0, 4.6, 1.7), (L, 0.6, 3.4), COLUMN, top=shade(COLUMN, 1.02))
    n = int(L / 4.5)
    for i in range(n + 1):
        x = -L / 2 + L * i / n
        if abs(x) < 7.0:
            continue   # 가운데 현관 · 계단 자리
        mb.box(m, (x, 2.15, 3.1), (0.7, 4.3, 0.7), COLUMN)
    mb.prism(m, (0, 4.5, 0.0), 6.5, 0.8, 18, COLUMN, caps=True)   # 벽에 반쯤 묻힌 원판 = 반원 캐노피
    mb.box(m, (0, 6.0, 0.6), (8.0, 1.6, 0.4), (0.24, 0.30, 0.38))  # 현판


def electronics_plaza(mb, m, b, elen, ground):
    """전자정보대학관: 앞 붉은 벽돌 포장 광장 (지면) + 가로등 줄"""
    from .nature import draw_lamp
    w, d = min(elen + 10, 44.0), 22.0
    corners = [m.p((sx * w / 2, 0, z)) for sx, z in ((-1, 1.0), (1, 1.0), (1, d), (-1, d))]
    ground.add_brick([(p[0], p[2]) for p in corners])
    for sx in (-1, 1):
        for z in (6.0, 16.0):
            p = m.p((sx * (w / 2 - 2), 0, z))
            draw_lamp(mb, p[0], p[2], math.degrees(math.atan2(m.z[0], m.z[2])))


def union_canopy(mb, m, b, elen, ground):
    """학생회관: 크게 내민 평 캐노피 + 넓은 대계단 (실사)"""
    w = min(max(elen * 0.5, 16.0), 24.0)
    mb.box(m, (0, 5.4, 3.8), (w, 0.8, 7.6), COLUMN, top=shade(COLUMN, 1.02))
    for x in (-w / 2 + 0.8, w / 2 - 0.8):
        mb.box(m, (x, 2.5, 6.8), (0.8, 5.0, 0.8), COLUMN)
    for k in range(6):
        mb.box(m, (0, 0.1 * (6 - k), 2.0 + k * 0.9), (w + 4 - k * 0.5, 0.2 * (6 - k), 0.9), STEP)
    # 실사: 정면 2층 위로 가로로 긴 청록 띠창
    for y in (7.4, 10.6):
        if y + 1.0 < b.h:
            mb.box(m, (0, y, 0.12), (elen * 0.86, 1.3, 0.24), TEAL_GLASS)
    # M11: M5 의 '뒤쪽 4층 더 높은 매스'(지붕 위 청록 띠창 탑)는 근거 없음 — OSM 은 다각형 하나(building=college, 높이 ·
    # 층수 · building:part 없음)라 이미 기본 높이 18 m(5층대) 몸체인데 그 위에 4층을 더 얹어 9층 탑이 됐다.
    # 실사의 '앞 2~3층 / 뒤 고층' 은 몸체 높이로 이미 닮았으니 탑을 없애고 지붕은 다른 건물과 같은 옥상 규칙(작은 계단실 · 실외기)에 맡긴다.


def ceramics_stair(mb, m, b, elen):
    """도예관: 입구 모서리의 전면 유리 계단실 (지붕 위로 반 층) + 옆 모자이크 타일 기둥 2개 (실사)"""
    x = elen / 2 - 2.4
    H = b.h
    mb.box(m, (x, (H + 1.6) / 2, 1.0), (4.4, H + 1.6, 2.6), (0.66, 0.82, 0.88), top=shade(COLUMN, 0.95))
    for k in range(1, int((H + 1.6) / 3.3) + 1):
        mb.box(m, (x, k * 3.3 - 0.1, 1.0), (4.6, 0.22, 2.8), COLUMN)   # 층 슬래브
    mb.box(m, (x, H + 1.75, 1.0), (4.8, 0.3, 3.0), COLUMN)
    tiles = ((0.20, 0.45, 0.70), (0.95, 0.85, 0.55), (0.85, 0.35, 0.30), (0.30, 0.65, 0.55), (0.98, 0.97, 0.92))
    for j, cx in enumerate((x - 5.0, x - 9.0)):
        for k in range(8):   # 0.5 m 조각을 쌓아 도자기 모자이크 느낌
            mb.box(m, (cx, 0.25 + k * 0.5, 0.7), (0.9, 0.5, 0.9), tiles[(k + j * 2) % len(tiles)])


def ceramics_deck(mb, m, b, elen, ground):
    """도예관: 앞 나무 데크 테라스 + 파라솔 테이블 (실사)"""
    dw, dd = 14.0, 8.0
    x0 = elen / 2 - dw / 2 if elen > dw + 6 else 0.0   # 입구 옆으로 비켜서
    mb.box(m, (x0, 0.25, 5.0 + dd / 2), (dw, 0.5, dd), DECK, top=shade(DECK, 1.08))
    for k in range(int(dw / 1.2)):
        mb.box(m, (x0 - dw / 2 + 0.6 + k * 1.2, 0.51, 5.0 + dd / 2), (0.08, 0.02, dd - 0.2), shade(DECK, 0.85))
    for i, (px, pz) in enumerate(((-3.5, 7.0), (3.5, 7.0), (0.0, 10.5))):
        x = x0 + px
        mb.prism(m, (x, 0.5, pz), 0.6, 0.75, 8, (0.95, 0.95, 0.93), caps=True)    # 둥근 탁자
        mb.prism(m, (x, 0.5, pz), 0.06, 2.6, 6, (0.40, 0.40, 0.42))               # 파라솔 대
        mb.cone(m, (x, 2.6, pz), 1.8, 0.7, 8, (0.98, 0.62, 0.40) if i % 2 == 0 else (0.98, 0.96, 0.90))
    for zz in range(0, int(dd) + 6, 2):
        for xx in range(int(-dw / 2) - 1, int(dw / 2) + 2, 2):
            p = m.p((x0 + xx, 0, 5.0 + zz))
            ground.occupy(p[0], p[2], 1.6)


def lawn_bed(mb, c, axis, length, width):
    """광장 잔디 화단: 낮은 석재 테두리 + 잔디 + 긴 변을 따라 둥근 회양목"""
    m = Frame.look((c[0], 0.0, c[1]), (axis[0], 0.0, axis[1]))   # 로컬 Z = 광장 축
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 0.6, 0.2
    mb.box(m, (0, 0.2, 0), (width + 0.8, 0.4, length + 0.8), STONE)
    mb.ao_strength = 0.0
    mb.box(m, (0, 0.42, 0), (width, 0.06, length), (0.42, 0.66, 0.28), top=(0.42, 0.66, 0.28))
    for sx in (-1, 1):
        for k in range(int(length / 3) + 1):
            mb.ico(m, (sx * (width / 2 - 1.0), 1.0, -length / 2 + 1.0 + k * (length - 2) / int(length / 3)),
                   (0.85, 0.7, 0.85), HEDGE_C, var=0.05)
    mb.cone(m, (0, 0.45, 0), 1.3, 4.2, 8, FORMAL, bottom=False)
    mb.ao_strength = 0.25


def pergola(mb, p0, p1):
    """등나무 퍼걸러 열주 (흰 원기둥 두 줄 + 들보 + 덮인 등나무, 군데군데 보라 꽃)"""
    L = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
    yaw = math.degrees(math.atan2(p1[0] - p0[0], p1[1] - p0[1]))
    m = Frame.yaw(((p0[0] + p1[0]) / 2, 0.0, (p0[1] + p1[1]) / 2), yaw)   # 로컬 Z = 길이 방향
    n = max(2, int(L / 4.0))
    mb.ao_floor, mb.ao_height, mb.ao_strength = 0.0, 1.0, 0.25
    for i in range(n + 1):
        z = -L / 2 + L * i / n
        for x in (-1.7, 1.7):
            mb.prism(m, (x, 0.0, z), 0.26, 3.3, 8, COLUMN)
    mb.ao_strength = 0.0
    for x in (-1.7, 1.7):
        mb.box(m, (x, 3.45, 0), (0.35, 0.35, L + 0.8), COLUMN)
    for k in range(int(L / 1.6) + 1):
        mb.box(m, (0, 3.75, -L / 2 + k * 1.6), (4.4, 0.25, 0.22), COLUMN)
    for k in range(int(L / 2.2)):
        z = -L / 2 + 1.1 + k * 2.2
        mb.ico(m, (((k * 37) % 5 - 2) * 0.4, 4.1, z), (1.6, 0.6, 1.4), VINE, var=0.08)
        if k % 2 == 0:
            for x in (-1.9, 1.9):
                mb.ico(m, (x, 3.2, z), (0.35, 0.7, 0.35), WISTERIA, var=0.05)
    mb.ao_strength = 0.25


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


def lions(mb, f, width):
    """대계단 양옆 받침 위 사자상 (돌 색, 앉은 모습을 덩어리로). width: 현관 폭"""
    m = Frame.look(f['center'], f['normal'])
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
    gw, gl, gh = Wd * 0.28, L * 0.3, 2.6
    mb.box(m, (0, 0.3, 0), (gw + 0.8, 0.6, gl + 0.8), COLUMN)
    # M10: 짙은 남색(GLASS)은 위 · 옆에서 지붕에 뚫린 구멍처럼 보였음 → 하늘이 비친 밝은 유리 + 흰 살 · 용마루
    mb.gable(m, (0, 0.6, 0), gw, gl, gh, SKY_GLASS, True, end_col=COLUMN)
    seg = math.hypot(gw / 2, gh)
    tx, ty = (gw / 2) / seg, gh / seg
    for k in range(-4, 5):
        for sx in (-1, 1):   # 경사면을 따라 내려오는 살 (지붕 위로 살짝 띄움)
            o = m.p((sx * gw / 4, 0.6 + gh / 2 + 0.06, k * gl / 9))
            mb.box(Frame(o, m.dir((-sx * tx, ty, 0.0)), m.dir((sx * ty, tx, 0.0)), m.dir((0.0, 0.0, -sx))), (0, 0, 0), (seg, 0.12, 0.28), COLUMN)   # z 는 오른손 좌표가 되게
    mb.box(m, (0, 0.6 + gh + 0.1, 0), (0.6, 0.3, gl + 0.4), COLUMN)   # 용마루
    mb.ao_floor = 0.0
    _library_roof(mb, b, f)


def _library_roof(mb, b, f):
    """M10: 박공 둘레 밖 넓은 평지붕이 민짜 베이지였음 → 안쪽 낮은 난간 띠(트림) + 날개마다 기계실 · 실외기 묶음"""
    from .buildings import ROOF_RESERVED
    from .geo import dist_to_poly_edge, point_in_poly
    from .mesh import offset_polygon, rect_poly
    H = b.h
    mb.ao_floor = H
    inner = offset_polygon(b.poly, -4.5)
    if len(inner) >= 3:
        mb.band(inner, H, H + 0.45, 0.18, 0.18, shade(COLUMN, 0.97), top_col=COLUMN)   # 낮은 안쪽 난간 (지붕 테두리 트림)
    (ax, az), L, Wd = f.long_axis()
    u, v = (ax, az), (-az, ax)
    yaw = math.degrees(math.atan2(ax, az))
    for su in (-1, 1):
        for sv in (-1, 1):
            px = f.center[0] + u[0] * su * L * 0.36 + v[0] * sv * Wd * 0.22
            pz = f.center[1] + u[1] * su * L * 0.36 + v[1] * sv * Wd * 0.22
            if not point_in_poly(px, pz, b.poly) or dist_to_poly_edge(px, pz, b.poly) < 7:
                continue
            m = Frame.yaw((px, H, pz), yaw)
            mb.box(m, (0, 1.1, 0), (5.0, 2.2, 7.0), (0.88, 0.88, 0.86), top=(0.93, 0.93, 0.91))   # 기계실
            mb.box(m, (0, 2.35, 0), (5.4, 0.3, 7.4), COLUMN)
            for k in (-1, 1):   # 실외기 + 팬
                mb.box(m, (k * 1.3, 2.9, 0), (1.8, 0.8, 1.8), (0.80, 0.81, 0.83))
                mb.prism(m, (k * 1.3, 3.3, 0), 0.7, 0.06, 12, (0.30, 0.33, 0.37), caps=True)
            ROOF_RESERVED.setdefault(b.id, []).append((rect_poly((px, pz), u, v, 5.0, 4.0), H + 3.4))
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
    """광장 다각형 안 동쪽에 직사각 수조 (오벨리스크는 서쪽 도서관 쪽). 반환: (중심, rx, rz)"""
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
    rx, rz = min(w * 0.2, 34.0), min(h * 0.2, 15.0)   # 실사: 축을 따라 긴 직사각 수조
    ground.add_lake((cx, cz), rx, rz)
    return (cx, cz), rx, rz


def lake_features(layer, ground, lake):
    """수조 둘레 낮은 석재 테, 가운데 3단 분수와 물줄기, 동쪽 끝 아치 7개 열주 (실사: 사색의 광장)"""
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
    ax = cx + rx + 10.0
    arch_colonnade(layer.at(ax, cz), (ax, 0.0, cz), 7, 6.5)
    ground.occupy(ax, cz, 8.0, ao=0.0)
    # 수조 긴 변 바깥 남북으로 등나무 퍼걸러 열주 (실사: 광장 가장자리)
    for s in (-1, 1):
        z = cz + s * (rz + 10.0)
        pergola(layer.at(cx, z), (cx - rx, z), (cx + rx, z))
        for x in range(int(cx - rx), int(cx + rx) + 1, 3):
            ground.occupy(x, z, 3.0)
    ground.occupy(cx, cz, max(rx, rz) + 2, ao=0.0)
    # 수조 긴 변을 따라 벤치 (수조를 바라보게)
    from .nature import draw_bench
    for sx in (-0.6, 0.0, 0.6):
        for sz in (-1, 1):
            x, z = cx + rx * sx, cz + sz * (rz + 4.0)
            draw_bench(layer.at(x, z), x, z, 180.0 if sz > 0 else 0.0)


def arch_colonnade(mb, pos, count, spacing):
    """따로 선 반원 아치 문이 남북으로 한 줄 (실사: 사색의 광장 동쪽 끝 아치 7개, 아치 사이는 비어 있다)"""
    m = Frame.look(pos, (1.0, 0.0, 0.0))   # 로컬 x = 남북 (아치가 늘어서는 방향), 로컬 z = 동쪽
    pier, h, r = 1.1, 4.2, spacing * 0.3
    L = spacing * count
    for i in range(count):
        xc = -L / 2 + spacing * (i + 0.5)
        for s in (-1, 1):
            x = xc + s * (r + pier / 2)
            mb.box(m, (x, 0.2, 0.0), (pier + 0.4, 0.4, 1.6), STONE)
            mb.box(m, (x, h / 2, 0.0), (pier, h, 1.2), COLUMN)
        R = r + pier / 2
        for k in range(8):   # 반원 아치 테 (짧은 상자 8개)
            a0, a1 = math.pi * k / 8, math.pi * (k + 1) / 8
            x0, y0 = xc - math.cos(a0) * R, h + math.sin(a0) * R
            x1, y1 = xc - math.cos(a1) * R, h + math.sin(a1) * R
            mb.box(m, ((x0 + x1) / 2, (y0 + y1) / 2, 0.0), (abs(x1 - x0) + pier * 0.9, abs(y1 - y0) + pier * 0.9, 1.2), COLUMN)


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
    mb.ico(m, (0, 5.3, 0), (1.8, 1.8, 1.8), (0.86, 0.88, 0.91), var=0.04)          # 은색 구 (실사)
    mb.prism(m, (0, 4.6, 0), 2.15, 0.25, 16, GOLD)                                 # 금색 고리
    for k in range(10):
        a = k * math.pi / 5
        mb.cone(m, (math.cos(a) * 5.2, 0.75, math.sin(a) * 5.2), 0.2, 1.8, 6, WATER_JET, bottom=False)
    # 실사: 로터리 둘레 장미 화단 고리 (낮은 회양목 테 + 빨강 · 분홍 장미)
    roses = ((0.86, 0.18, 0.30), (0.98, 0.62, 0.74), (0.95, 0.36, 0.45))
    mb.ao_strength = 0.2
    for k in range(28):
        a = 2 * math.pi * k / 28
        rx, rz = math.cos(a) * 9.6, math.sin(a) * 9.6
        mb.ico(m, (rx, 0.45, rz), (1.15, 0.55, 1.15), HEDGE_C, var=0.05)
        mb.ico(m, (rx, 0.95, rz), (0.42, 0.32, 0.42), roses[k % 3], var=0.0)
    mb.ao_strength = 0.0
    ground.occupy(x, z, 11.5, ao=0.25, ao_radius=7.5)
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
    """toward 방향(단위 벡터)을 바라보는 가장 긴 벽: (a, b, 바깥 법선, 길이).
    한 직선 벽이 여러 변으로 나뉘어 있으면(예술대 남쪽 면 4조각) 이어서 한 벽으로 본다 — 안 그러면 끝 조각에 현관이 붙는다"""
    from .mesh import outward_normal
    best = None
    n = len(poly)
    run = None   # 지금 잇고 있는 벽: [a, c, (nx, nz)]
    for i in range(n + 1):
        k = i % n
        a, c = poly[k], poly[(k + 1) % n]
        nx, nz = outward_normal(poly, k)
        ok = nx * toward[0] + nz * toward[1] >= 0.92
        if ok and run is not None and nx * run[2][0] + nz * run[2][1] > 0.995 and a == run[1]:
            run[1] = c
        else:
            if run is not None:
                L = math.hypot(run[1][0] - run[0][0], run[1][1] - run[0][1])
                if best is None or L > best[3]:
                    best = (run[0], run[1], run[2], L)
            run = [a, c, (nx, nz)] if ok and i < n else None
    return best


def arts_palace(mb, shell, b, ground, front=None):
    """
    예술디자인대학 (키아트 '궁전' 아이콘): 사색의 광장·산책길이 오는 서쪽 정면 돌출부를
    한 층 높은 가운데 파빌리온으로 키우고, 거대 기둥 6개 + 엔태블러처 + 박공 + 지붕 난간,
    1층 아치 현관 3개, 앞에는 낮은 테라스와 가운데 대계단 · 양옆 화단 생울타리.
    """
    from .buildings import PALETTE, floor_height, roof_tint, style_of
    from .mesh import extrude_poly, rect_poly, round_corners
    from .nature import LIGHTS
    pal = PALETTE[style_of(b)]
    # 실사 (항공 VR · 정면 사진, M6): 정면 포르티코는 남쪽 긴 면, 앞 차도 · 횡단보도 너머 대형 주차장. 예전엔 광장 쪽(서쪽 옆면)
    if front is None:
        dx, dz = PLAZA[0] - b.centroid[0], PLAZA[1] - b.centroid[1]
        L = math.hypot(dx, dz) or 1
        front = (dx / L, dz / L)
    edge = _front_edge(b.poly, front)
    if edge is None:
        return
    a, c, (nx, nz), elen = edge
    # 둥근 모서리로 깎인 길이만큼 되돌린 원래 변 길이
    width = min(elen + 4.8, 30.0)
    mx, mz = (a[0] + c[0]) / 2, (a[1] + c[1]) / 2
    proj, back = 4.0, 4.0
    H = b.h
    fh = floor_height(style_of(b))
    top = min(H, 2 * fh) + fh   # 실사: 7층 본동 앞의 낮은 포르티코 (2층 높이 기둥 + 박공 시계)
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
    _corner_towers(mb, b, front, pal, fh)
    _arts_wings(mb, b, pc, width, fh, ground, front)
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


def _arts_wings(mb, b, pc, width, fh, ground, front):
    """예술대 1층 열주 날개: 박공 현관 양옆으로 본동 정면을 따라 1층 높이 기둥 줄 + 위 평 슬래브 (실사)"""
    f = Footprint(b.poly, b.h).face((b.centroid[0] + front[0] * 300, b.centroid[1] + front[1] * 300))
    mf = Frame.look(f['center'], f['normal'])
    off = (pc[0] - f['center'][0]) * mf.x[0] + (pc[1] - f['center'][2]) * mf.x[2]   # 현관 가운데의 면 방향 위치
    h = fh * 1.15
    for s in (-1, 1):
        x0 = off + s * (width / 2 + 1.0)
        x1 = s * (f['length'] / 2 - 2.0)
        L = (x1 - x0) * s
        if L < 6.0:
            continue
        L = min(L, 22.0)
        cx = x0 + s * L / 2
        mb.box(mf, (cx, h + 0.35, 1.6), (L, 0.7, 3.4), COLUMN, top=shade(COLUMN, 1.02))
        n = max(2, int(L / 3.2))
        for i in range(n + 1):
            column(mb, mf, (x0 + s * L * i / n, 0.0, 2.9), 0.32, h, fluted=False)
        for i in range(0, int(L) + 1, 2):
            p = mf.p((x0 + s * i, 0, 2.6))
            ground.occupy(p[0], p[2], 1.4)


def _corner_towers(mb, b, front, pal, fh):
    """예술대 본동 정면 양 모서리 탑 (실사: 모서리가 탑처럼 솟고 아치창 · 코니스). 지붕 위로 1.5층"""
    from .geo import point_in_poly
    fp = Footprint(b.poly, b.h)
    H = b.h
    corners = [(fp.center[0] + fp.u[0] * su * fp.half_u + fp.v[0] * sv * fp.half_v,
                fp.center[1] + fp.u[1] * su * fp.half_u + fp.v[1] * sv * fp.half_v)
               for su in (-1, 1) for sv in (-1, 1)]
    corners.sort(key=lambda p: -((p[0] - fp.center[0]) * front[0] + (p[1] - fp.center[1]) * front[1]))
    yaw = math.degrees(math.atan2(fp.u[0], fp.u[1]))
    for px, pz in corners[:2]:
        # 모서리에서 중심 쪽으로 들어가며 탑(8 m) 이 건물 안에 들어가는 첫 자리
        dl = max(1.0, math.hypot(fp.center[0] - px, fp.center[1] - pz))
        for d in range(4, 52, 2):   # 윤곽이 패인 모서리도 더 안쪽까지 찾는다 (M4 에서 한쪽만 섰음)
            qx, qz = px + (fp.center[0] - px) * d / dl, pz + (fp.center[1] - pz) * d / dl
            if all(point_in_poly(qx + ox, qz + oz, b.poly) for ox in (-3, 3) for oz in (-3, 3)):
                break
        else:
            print('landmarks: 예술대 모서리 탑 자리 없음', round(px), round(pz))
            continue
        m = Frame.yaw((qx, 0.0, qz), yaw)
        top = H + fh * 2.6   # 양 끝 파빌리온(H + 1층)보다 한 층 반 더
        mb.box(m, (0, (H + top) / 2, 0), (8.0, top - H + 0.4, 8.0), pal['wall'], top=shade(pal['trim'], 0.97))
        mb.box(m, (0, top - 0.3, 0), (8.8, 0.6, 8.8), pal['trim'])
        mb.box(m, (0, top + 0.5, 0), (7.4, 1.0, 7.4), shade(pal['trim'], 0.98))
        for k in range(4):
            fm = Frame.yaw((qx, 0.0, qz), yaw + 90 * k)
            mb.arch_panel(fm, (0, H + 0.6, 4.02), 2.0, 3.0, (0.50, 0.60, 0.70))
        from .buildings import ROOF_RESERVED
        ROOF_RESERVED.setdefault(b.id, []).append(([m.p((sx * 5, 0, sz * 5))[::2] for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))], top))


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
