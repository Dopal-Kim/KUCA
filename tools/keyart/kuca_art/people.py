"""
M29: 캠퍼스 학생 인형 (정지한 저폴리) — 지도에 플레이어 말고 사람이 없어 텅 빈 모형처럼 보였음 (MAP_REVIEW_v34 1번).
몸통(육각 기둥) + 머리 · 머리카락(구) + 다리 + 가방. 서 있기 · 앉기 · 잔디에 앉기 세 자세, 2 ~ 4명 무리.
자리: 벤치 · 노천극장 객석 · 대운동장 스탠드(앉기), 사색의 광장 · 건물 입구 옆 · 플레이어 시작 자리 둘레 · 트랙 가장자리 ·
체육대학관 옥상 운동장(서기), 시작 자리 잔디(잔디에 앉기). 보행로 · 차도 · 물 위는 피한다.
난수는 따로 (공용 rng 를 쓰면 캠퍼스 나무 · 가로등 배치가 통째로 바뀜 — M26), 땅 막기(occupy)도 안 함.
"""
import math
import random

from .mesh import Frame

# 앉을 자리 줄: [(x, y, z, 바라보는 yaw)] — 랜드마크 · 벤치가 그리면서 채운다
SEAT_ROWS = {'amphitheater': [], 'stadium': []}
BENCHES = []   # (x, z, yaw): nature.draw_bench 가 채운다
ROOF_COURTS = []   # (Frame, 반길이, 반폭): landmarks._rooftop_court 가 채운다 (체육대학관 옥상 운동장, 로컬 x 긴 축)

# M30: 키 1.7 m × 4.5 ≈ 7.6 m (M29 의 2.5 m 에서 3 배). 2.5 m 는 Unity 홈 시점(1080 × 1920, 0.11 m/px)에서 약 17 px,
#      넓게 본 화면에선 10 px 점이라 사람으로 안 읽혔음 (REVIEW_v35 1절). 플레이어는 화면에서 약 120 px(체감 18 m) ·
#      경희펫 약 10 m 라 그 반쯤(약 50 px)이 '같은 장난감 세계의 사람'으로 읽히는 하한 — 더 키우면(4 배 10 m) 1층(3.3 × 1.3 = 4.3 m)
#      의 2.3 배 · 현관 유리문(2.8 ~ 3.4 m)의 3 배가 넘어 건물이 인형집처럼 작아 보임. 7.6 m 는 머리가 2층 창 아래(1.75 층),
#      가로등(5 m)의 1.5 배 · 나무(8 ~ 10 m)보다 조금 작음.
SCALE = 4.5
K = SCALE / 1.5    # M29 자리 간격(무리 반지름 · 입구 비켜서기 · 무리끼리 거리)을 키에 맞춰 늘리는 배율
BODY_R = 0.35 * SCALE   # 몸 반지름 (어깨 폭 0.42 × SCALE 의 반 + 여유) — 이만큼 길 · 연석 · 나무에서 띄운다
SEAT_STEP = 3           # 객석 · 스탠드 좌석(0.75 m)을 몇 칸마다 앉힐지 (어깨 폭 1.9 m 가 겹치지 않게 2.25 m)
SHIRTS = [(0.88, 0.28, 0.25), (0.98, 0.78, 0.22), (0.20, 0.50, 0.80), (0.18, 0.66, 0.70), (0.28, 0.62, 0.46),
          (0.95, 0.55, 0.66), (0.56, 0.38, 0.70), (0.98, 0.56, 0.22)]
PANTS = [(0.20, 0.24, 0.34), (0.27, 0.27, 0.30), (0.40, 0.50, 0.66), (0.78, 0.72, 0.60)]
SKIN = [(0.96, 0.80, 0.66), (0.90, 0.72, 0.58)]
HAIR = [(0.12, 0.10, 0.09), (0.22, 0.16, 0.12), (0.36, 0.25, 0.16)]
BAGS = [(0.22, 0.24, 0.30), (0.72, 0.30, 0.25), (0.30, 0.44, 0.64), (0.85, 0.80, 0.70)]


def draw_person(mb, x, y, z, yaw, sit, rng):
    """(x, y, z): 서 있으면 발, 앉으면 엉덩이(앉는 면) 높이. 로컬 +Z 가 앞. sit='ground' 는 잔디에 다리 뻗고 앉기"""
    m = Frame.yaw((x, y, z), yaw, SCALE)
    shirt, pants = rng.choice(SHIRTS), rng.choice(PANTS)
    hip = 0.0 if sit else 0.78
    if sit == 'ground':
        mb.box(m, (0, 0.08, 0.36), (0.32, 0.16, 0.72), pants, bottom=False)     # 앞으로 뻗은 다리
    elif sit:
        mb.box(m, (0, 0.08, 0.2), (0.32, 0.16, 0.46), pants, bottom=False)       # 허벅지 (앞으로)
        mb.box(m, (0, -0.2, 0.4), (0.28, 0.44, 0.16), pants, bottom=False)      # 정강이 (아래로)
    else:
        mb.box(m, (0, 0.39, 0), (0.32, 0.78, 0.2), pants, bottom=False)         # 다리
    mb.prism(m, (0, hip, 0), 0.21, 0.62, 6, shirt, caps=True, r_top=0.17)      # 몸통
    mb.ico(m, (0, hip + 0.78, 0), (0.15, 0.16, 0.15), rng.choice(SKIN), var=0.04)   # 머리
    mb.ico(m, (0, hip + 0.86, -0.03), (0.16, 0.1, 0.16), rng.choice(HAIR), var=0.04)
    if rng.random() < 0.7:
        mb.box(m, (0, hip + 0.36, -0.25), (0.28, 0.34, 0.13), rng.choice(BAGS), bottom=False)   # 가방


def place_people(layer, ground, entrances, buildings, seed=29):
    g = ground
    rng = random.Random(seed)
    by_id = {b.id: b for b in buildings}
    groups = []   # 무리 중심 (서로 떨어지게)
    counts = {}

    def clear(x, z, r=BODY_R):
        """보행로 · 차도 · 물 · 건물 · 다른 물체(나무 · 가로등 · 벤치 · 울타리) 밖"""
        if not g.inside(x, z, 4) or not g.has(g.campus, x, z):
            return False
        for dx, dz in ((0, 0), (r, 0), (-r, 0), (0, r), (0, -r)):
            for mask in (g.road, g.curb, g.walk, g.water, g.buildings, g.occupied):
                if g.has(mask, x + dx, z + dz):
                    return False
        return True

    def apart(x, z, d=30.0 * K / 2):
        return all(math.hypot(x - a, z - c) > d for a, c in groups)

    def add(kind, x, y, z, yaw, sit):
        draw_person(layer.at(x, z), x, y, z, yaw, sit, rng)
        counts[kind] = counts.get(kind, 0) + 1

    def ring(cx, cz, n, r0=0.7, r1=0.95):
        a0 = rng.uniform(0, 360)
        pts = []
        for k in range(n):
            a = math.radians(a0 + k * 360 / n + rng.uniform(-20, 20))
            r = rng.uniform(r0, r1) * K
            pts.append((cx + math.sin(a) * r, cz + math.cos(a) * r))
        return pts

    def put_group(kind, cx, cz, pts, y=0.0, sit=False):
        for px, pz in pts:   # 무리 가운데를 보고 선다 (앉는다)
            add(kind, px, y, pz, math.degrees(math.atan2(cx - px, cz - pz)) + rng.uniform(-25, 25), sit)
        groups.append((cx, cz))

    def stand_group(kind, cx, cz, n, sit=False):
        pts = ring(cx, cz, n, *((1.3, 1.5) if sit else (0.7, 0.95)))   # 잔디에 앉으면 뻗은 다리(3.2 m)가 가운데서 안 겹치게
        if not all(clear(px, pz) for px, pz in pts):
            return False
        put_group(kind, cx, cz, pts, sit=sit)
        return True

    def near_group(kind, ax, az, n, sit=False, reach=16.0):
        """(ax, az) 에서 가까운 빈자리부터 (2 m 씩 넓혀 가며) 무리가 들어가는 첫 곳"""
        for r in range(0, int(reach) + 1, 2):
            for k in range(max(1, r * 3)):
                a = 2 * math.pi * k / max(1, r * 3)
                cx, cz = ax + math.sin(a) * r, az + math.cos(a) * r
                if apart(cx, cz, 20.0) and stand_group(kind, cx, cz, n, sit):
                    return True
        return False

    # 0) M30: 플레이어 시작 자리(지도 원점 (0, 0) = 체육대학관 서남 모서리, 열주 현관 남쪽 끝) 둘레 — 지도 홈 기본 화면
    #    (거리 420 m · 피치 42°, 세로 화면에서 위 카드와 아래 '이렇게 시작해요' 카드 사이 = 북쪽 약 0 ~ 130 m, 동서 약 ±55 m)
    #    에 사람이 하나도 안 보였음 (REVIEW_v35 1절). 열주 앞 길 건너 잔디(선 무리), 그 남쪽 잔디(잔디에 앉은 무리),
    #    북쪽 산책길 옆(선 무리). 체육대 쪽(동)은 건물이라 서쪽 잔디에만, 플레이어 몸(약 18 m)과 겹치지 않게 원점에서 16 m 넘게
    for ax, az, n, sit in ((-27.0, 46.0, 3, False), (-44.0, 14.0, 3, 'ground'), (-30.0, 100.0, 2, False)):
        near_group('start', ax, az, n, sit)

    # 1) 객석 · 스탠드: 같은 줄에 SEAT_STEP 칸 걸러 앉은 2 ~ 4명 (스탠드는 Unity 에서 안 보였음 — 키를 키우고 한 무리 더)
    for kind, n_groups in (('amphitheater', 3), ('stadium', 4)):
        rows = [r for r in SEAT_ROWS[kind] if len(r) >= 2 * SEAT_STEP + 1]
        rng.shuffle(rows)
        made = 0
        for row in rows:
            if made >= n_groups:
                break
            n = min(rng.randint(2, 4), (len(row) - 1) // SEAT_STEP + 1)
            i0 = rng.randrange(0, len(row) - (n - 1) * SEAT_STEP)
            if not apart(row[i0][0], row[i0][2], 12.0 * K / 2):
                continue
            for k in range(n):
                x, y, z, yaw = row[i0 + k * SEAT_STEP]
                add(kind, x, y, z, yaw, True)
            groups.append((row[i0][0], row[i0][2]))
            made += 1

    # 1-2) M30: 대운동장 트랙 가장자리 (스탠드 앞 흙 위, 긴 변에서 6 m 안쪽) — 양쪽 긴 변에 선 무리 하나씩
    if g.stadium_box:
        (x0, z0), (x1, z1) = g.stadium_box
        cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
        long_z = (z1 - z0) > (x1 - x0)
        for s, t in ((-1, 0.22), (1, -0.18)):
            if long_z:
                ax, az = (x0 + 6.0 if s < 0 else x1 - 6.0), cz + t * (z1 - z0)
            else:
                ax, az = cx + t * (x1 - x0), (z0 + 6.0 if s < 0 else z1 - 6.0)
            near_group('track', ax, az, rng.randint(2, 3), reach=10.0)

    # 1-3) M30: 체육대학관 옥상 운동장 — 한쪽 끝 트랙 안쪽에 선 셋, 반대쪽 골대 앞 코트에 선 둘 (지도 홈 화면 안)
    for m, hl, hw in ROOF_COURTS:
        for (lx, lz), n in (((hl - 4.0, hw * 0.35), 3), ((-(hl - 9.0), -hw * 0.3), 2)):
            c = m.p((lx, 0.0, lz))
            pts = [m.p((lx + math.sin(a) * 0.8 * K, 0.0, lz + math.cos(a) * 0.8 * K))
                   for a in (2 * math.pi * k / n + rng.uniform(-0.3, 0.3) for k in range(n))]
            put_group('roof', c[0], c[2], [(p[0], p[2]) for p in pts], y=m.o[1] + 0.34)

    # 2) 벤치: 캠퍼스 안 벤치 한가운데 한 명 (어깨 폭이 벤치 폭과 같아 둘은 겹침), 가끔 한 명이 벤치 끝 옆에 선다
    benches = [bb for bb in BENCHES if g.has(g.campus, bb[0], bb[1])]
    rng.shuffle(benches)
    made = 0
    for x, z, yaw in benches:
        if made >= 3:
            break
        if not apart(x, z, 40.0):
            continue
        m = Frame.yaw((x, 0.0, z), yaw)
        p = m.p((0.0, 0.0, 0.05))
        add('bench', p[0], 0.5, p[2], yaw, True)
        if rng.random() < 0.7:
            s = rng.choice((-1, 1))
            p = m.p((s * (0.95 + 0.6 * K), 0.0, 0.3 * K))
            if clear(p[0], p[2], BODY_R * 0.6):
                add('bench', p[0], 0.0, p[2], yaw + s * 70, False)   # 앉은 친구 쪽을 본다
        groups.append((x, z))
        made += 1

    # 3) 사색의 광장: 넓은 포장 위 선 무리 (보행로 줄 · 수조 · 화단 · 오벨리스크 밖)
    from .landmarks import PLAZA
    made, tries = 0, 0
    while made < 5 and tries < 800:
        tries += 1
        x, z = PLAZA[0] + rng.uniform(-60, 60), PLAZA[1] + rng.uniform(-45, 45)
        if g.has(g.square, x, z) and apart(x, z, 34.0) and stand_group('plaza', x, z, rng.randint(2, 4)):
            made += 1

    # 4) 건물 입구 옆: 현관 · 포장 가운데는 비우고 옆으로 비켜 선 무리 (M30: 키가 3 배라 현관 앞 · 옆 거리도 늘림)
    # 정면 돌출 동 · 캔틸레버 캐노피 건물(외국어대 · 국제경영대)은 현관 옆 자리가 넓은 계단 · 캐노피 밑이라 뺀다
    from .buildings import MASSING
    ents = sorted((bid, e) for bid, e in entrances.items()
                  if bid in by_id and g.has(g.campus, e['x'], e['z']) and 'bay' not in (MASSING.get(bid) or ()))
    rng.shuffle(ents)
    made = 0
    for bid, e in ents:
        if made >= 4:
            break
        nx, nz = e['n']
        tx, tz = -nz, nx
        for out, side in ((9.0, 11.0), (12.0, 12.5), (8.0, 14.0), (13.0, 10.0)):
            s = rng.choice((-1, 1))
            cx, cz = e['x'] + nx * out + tx * side * s, e['z'] + nz * out + tz * side * s
            if apart(cx, cz) and stand_group('entrance', cx, cz, rng.randint(2, 3)):
                made += 1
                break
    return counts
