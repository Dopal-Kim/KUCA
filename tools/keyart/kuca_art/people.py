"""
M29: 캠퍼스 학생 인형 (정지한 저폴리) — 지도에 플레이어 말고 사람이 없어 텅 빈 모형처럼 보였음 (MAP_REVIEW_v34 1번).
몸통(육각 기둥) + 머리 · 머리카락(구) + 다리 + 가방. 서 있기 · 앉기 두 자세, 2 ~ 4명 무리.
자리: 벤치 · 노천극장 객석 · 대운동장 스탠드(앉기), 사색의 광장 · 건물 입구 옆(서기). 보행로 · 차도 · 물 위는 피한다.
난수는 따로 (공용 rng 를 쓰면 캠퍼스 나무 · 가로등 배치가 통째로 바뀜 — M26), 땅 막기(occupy)도 안 함.
"""
import math
import random

from .mesh import Frame

# 앉을 자리 줄: [(x, y, z, 바라보는 yaw)] — 랜드마크 · 벤치가 그리면서 채운다
SEAT_ROWS = {'amphitheater': [], 'stadium': []}
BENCHES = []   # (x, z, yaw): nature.draw_bench 가 채운다

# 키 1.7 m × 1.5 ≈ 2.5 m: 1.7 m 그대로는 지도 홈 거리(420 m · 웹 1600 × 1000)에서 4 px 점, × 1.3 도 6 px 로 사람으로 안 읽혔음
# (지도는 경희펫이 약 10 m · 가로등 5 m 인 미니어처라 이 정도는 어울림)
SCALE = 1.5
SHIRTS = [(0.88, 0.28, 0.25), (0.98, 0.78, 0.22), (0.20, 0.50, 0.80), (0.18, 0.66, 0.70), (0.28, 0.62, 0.46),
          (0.95, 0.55, 0.66), (0.56, 0.38, 0.70), (0.98, 0.56, 0.22)]
PANTS = [(0.20, 0.24, 0.34), (0.27, 0.27, 0.30), (0.40, 0.50, 0.66), (0.78, 0.72, 0.60)]
SKIN = [(0.96, 0.80, 0.66), (0.90, 0.72, 0.58)]
HAIR = [(0.12, 0.10, 0.09), (0.22, 0.16, 0.12), (0.36, 0.25, 0.16)]
BAGS = [(0.22, 0.24, 0.30), (0.72, 0.30, 0.25), (0.30, 0.44, 0.64), (0.85, 0.80, 0.70)]


def draw_person(mb, x, y, z, yaw, sit, rng):
    """(x, y, z): 서 있으면 발, 앉으면 엉덩이(앉는 면) 높이. 로컬 +Z 가 앞"""
    m = Frame.yaw((x, y, z), yaw, SCALE)
    shirt, pants = rng.choice(SHIRTS), rng.choice(PANTS)
    hip = 0.0 if sit else 0.78
    if sit:
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
    groups = []   # 무리 중심 (서로 30 m 넘게)
    counts = {}

    def clear(x, z, r=1.2):
        """보행로 · 차도 · 물 · 건물 · 다른 물체(나무 · 가로등 · 벤치 · 울타리) 밖"""
        if not g.inside(x, z, 4) or not g.has(g.campus, x, z):
            return False
        for dx, dz in ((0, 0), (r, 0), (-r, 0), (0, r), (0, -r)):
            for mask in (g.road, g.curb, g.walk, g.water, g.buildings, g.occupied):
                if g.has(mask, x + dx, z + dz):
                    return False
        return True

    def apart(x, z, d=30.0):
        return all(math.hypot(x - a, z - c) > d for a, c in groups)

    def add(kind, x, y, z, yaw, sit):
        draw_person(layer.at(x, z), x, y, z, yaw, sit, rng)
        counts[kind] = counts.get(kind, 0) + 1

    def stand_group(kind, cx, cz, n):
        a0 = rng.uniform(0, 360)
        pts = []
        for k in range(n):
            a = math.radians(a0 + k * 360 / n + rng.uniform(-20, 20))
            r = rng.uniform(0.7, 0.95)
            pts.append((cx + math.sin(a) * r, cz + math.cos(a) * r))
        if not all(clear(px, pz, 0.6) for px, pz in pts):
            return False
        for px, pz in pts:   # 무리 가운데를 보고 선다
            add(kind, px, 0.0, pz, math.degrees(math.atan2(cx - px, cz - pz)) + rng.uniform(-25, 25), False)
        groups.append((cx, cz))
        return True

    # 1) 객석 · 스탠드: 같은 줄에 붙어 앉은 2 ~ 4명
    for kind, n_groups in (('amphitheater', 3), ('stadium', 3)):
        rows = [r for r in SEAT_ROWS[kind] if len(r) >= 6]
        rng.shuffle(rows)
        made = 0
        for row in rows:
            if made >= n_groups:
                break
            n = rng.randint(2, 4)
            i0 = rng.randrange(0, len(row) - n)
            if not apart(row[i0][0], row[i0][2], 12.0):
                continue
            for k in range(n):
                x, y, z, yaw = row[i0 + k]
                add(kind, x, y, z, yaw, True)
            groups.append((row[i0][0], row[i0][2]))
            made += 1

    # 2) 벤치: 캠퍼스 안 벤치에 한두 명 앉고, 가끔 한 명이 벤치 끝 옆에 선다
    benches = [bb for bb in BENCHES if g.has(g.campus, bb[0], bb[1])]
    rng.shuffle(benches)
    made = 0
    for x, z, yaw in benches:
        if made >= 4:
            break
        if not apart(x, z, 40.0):
            continue
        m = Frame.yaw((x, 0.0, z), yaw)
        n = rng.choice((1, 2, 2))
        for sx in ((-0.45, 0.45) if n == 2 else (rng.choice((-0.4, 0.4)),)):
            p = m.p((sx, 0.0, 0.05))
            add('bench', p[0], 0.5, p[2], yaw, True)
        if rng.random() < 0.6:
            s = rng.choice((-1, 1))
            p = m.p((s * 1.55, 0.0, 0.1))
            if not (g.has(g.walk, p[0], p[2]) or g.has(g.road, p[0], p[2])):
                add('bench', p[0], 0.0, p[2], yaw + s * 70, False)   # 앉은 친구 쪽을 본다
        groups.append((x, z))
        made += 1

    # 3) 사색의 광장: 넓은 포장 위 선 무리 (보행로 줄 · 수조 · 화단 · 오벨리스크 밖)
    from .landmarks import PLAZA
    made, tries = 0, 0
    while made < 6 and tries < 600:
        tries += 1
        x, z = PLAZA[0] + rng.uniform(-60, 60), PLAZA[1] + rng.uniform(-45, 45)
        if g.has(g.square, x, z) and apart(x, z, 25.0) and stand_group('plaza', x, z, rng.randint(2, 4)):
            made += 1

    # 4) 건물 입구 옆: 현관 · 포장 가운데는 비우고 옆으로 비켜 선 무리
    # 정면 돌출 동 · 캔틸레버 캐노피 건물(외국어대 · 국제경영대)은 현관 옆 자리가 넓은 계단 · 캐노피 밑이라 뺀다
    from .buildings import MASSING
    ents = sorted((bid, e) for bid, e in entrances.items()
                  if bid in by_id and g.has(g.campus, e['x'], e['z']) and 'bay' not in (MASSING.get(bid) or ()))
    rng.shuffle(ents)
    made = 0
    for bid, e in ents:
        if made >= 5:
            break
        nx, nz = e['n']
        tx, tz = -nz, nx
        for out, side in ((6.0, 7.0), (8.0, 8.5), (5.0, 9.5), (9.0, 6.5)):
            s = rng.choice((-1, 1))
            cx, cz = e['x'] + nx * out + tx * side * s, e['z'] + nz * out + tz * side * s
            if apart(cx, cz) and stand_group('entrance', cx, cz, rng.randint(2, 3)):
                made += 1
                break
    return counts
