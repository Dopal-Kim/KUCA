"""
수집 동물 캐릭터 3D 모델 (Art/Concept/05_Characters 의 제미나이 3D 레퍼런스 시트를 보고 만든 말랑한 토이 형태).
둥근 덩어리(타원체·캡슐·원뿔)를 버텍스 색으로 칠해 붙인다. Unity·미리보기는 같은 위치·색의 정점을 합쳐
부드러운 법선으로 그린다 (소프트 비닐 피규어 느낌).

좌표: 캐릭터 발밑 받침 중심이 원점, +Y 위, +Z 가 얼굴 앞. 단위 m (지도에 그대로 서는 크기, 받침 포함 약 10 m).
"""
import math
import random

from .mesh import Builder, Frame, IDENT

# ---------- 색 (sRGB) ----------
WHITE = (0.99, 0.98, 0.96)
EYE = (0.10, 0.08, 0.09)
EYE_SHINE = (1.0, 1.0, 1.0)
BLUSH = (0.99, 0.66, 0.70)
MOUTH = (0.45, 0.20, 0.22)
TONGUE = (0.98, 0.52, 0.58)
GRASS = (0.48, 0.74, 0.28)
GRASS_DARK = (0.38, 0.62, 0.22)
STONE = (0.72, 0.72, 0.70)
CRIMSON = (0.66, 0.12, 0.20)
RIM = {'green': (0.45, 0.80, 0.33), 'blue': (0.32, 0.62, 0.96), 'gold': (1.0, 0.79, 0.20)}
ACC = {'green': (0.42, 0.74, 0.28), 'blue': (0.30, 0.60, 0.95), 'gold': (1.0, 0.80, 0.22)}

BASE_TOP = 1.05


# ---------- 기본 도형 ----------

def _norm(v):
    L = math.sqrt(sum(c * c for c in v)) or 1.0
    return tuple(c / L for c in v)


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def frame(o, yaw=0.0, pitch=0.0, roll=0.0):
    """원점 o, 요(Y)·피치(X)·롤(Z) 순서로 돌린 좌표계 (도)"""
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    cr, sr = math.cos(math.radians(roll)), math.sin(math.radians(roll))

    def R(v):
        x, y, z = v
        x, y = x * cr - y * sr, x * sr + y * cr          # roll (Z)
        y, z = y * cp - z * sp, y * sp + z * cp          # pitch (X)
        x, z = x * cy + z * sy, -x * sy + z * cy         # yaw (Y)
        return (x, y, z)
    return Frame(tuple(o), R((1, 0, 0)), R((0, 1, 0)), R((0, 0, 1)))


def along(p0, p1):
    """p0 → p1 을 로컬 +Y 로 하는 좌표계 (원점 p0)"""
    y = _norm((p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]))
    hint = (0, 0, 1) if abs(y[2]) < 0.9 else (1, 0, 0)
    x = _norm(_cross(y, hint))
    z = _cross(x, y)
    return Frame(tuple(p0), x, y, z)


def ellipsoid(mb, m, c, r, col, seg=20, rings=12):
    """타원체 (중심 c, 반지름 r = (rx, ry, rz)), 로컬 좌표계 m"""
    def P(i, j):
        th = -math.pi / 2 + math.pi * i / rings
        ph = 2 * math.pi * j / seg
        return (c[0] + r[0] * math.cos(th) * math.cos(ph), c[1] + r[1] * math.sin(th), c[2] + r[2] * math.cos(th) * math.sin(ph))
    for i in range(rings):
        for j in range(seg):
            a, b, d, e = P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)
            if i < rings - 1:
                mb.tri(m, a, b, d, col, c)
            if i > 0:
                mb.tri(m, a, d, e, col, c)


def sphere(mb, m, c, r, col, seg=16, rings=10):
    ellipsoid(mb, m, c, (r, r, r), col, seg, rings)


def capsule(mb, p0, p1, r, col, seg=12, rings=8):
    """둥근 막대 (팔·다리·더듬이): p0, p1 은 캐릭터 좌표"""
    L = math.dist(p0, p1)
    f = along(p0, p1)
    ellipsoid(mb, f, (0, L / 2, 0), (r, L / 2 + r, r), col, seg, rings)


def cone(mb, m, base, r, h, col, sides=14):
    mb.cone(m, base, r, h, sides, col)


def cylinder(mb, m, base, r, h, col, sides=24, r_top=None):
    mb.prism(m, base, r, h, sides, col, caps=True, r_top=r_top)


def on_sphere(c, R, yaw, pitch, out=0.0):
    """구 표면 위 점 (yaw: 앞(+Z)에서 오른쪽(+X)으로, pitch: 위로)"""
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    rr = R + out
    return (c[0] + rr * cp * sy, c[1] + rr * sp, c[2] + rr * cp * cy)


# ---------- 공통 부위 ----------

def base(mb, tier, rng):
    """키아트 아이콘형 둥근 받침: 등급 색 테두리 + 잔디 윗면 + 작은 꽃·돌"""
    rim = RIM[tier]
    mb.ao_strength = 0.0
    cylinder(mb, IDENT, (0, 0, 0), 4.5, 0.42, tuple(c * 0.86 for c in rim), sides=40)
    cylinder(mb, IDENT, (0, 0.42, 0), 4.55, 0.36, rim, sides=40)
    cylinder(mb, IDENT, (0, 0.78, 0), 4.3, 0.27, GRASS, sides=40)
    for k in range(7):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(2.6, 3.9)
        x, z = math.cos(a) * d, math.sin(a) * d
        if k < 5:
            sphere(mb, IDENT, (x, BASE_TOP + 0.08, z), 0.22, WHITE, 8, 5)
            sphere(mb, IDENT, (x, BASE_TOP + 0.2, z), 0.09, (1.0, 0.85, 0.30), 6, 4)
        else:
            ellipsoid(mb, IDENT, (x, BASE_TOP, z), (0.45, 0.28, 0.38), STONE, 10, 6)
    for k in range(10):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(2.0, 4.0)
        cone(mb, IDENT, (math.cos(a) * d, BASE_TOP - 0.05, math.sin(a) * d), 0.16, 0.45, GRASS_DARK, 5)


def _face_frame(head_c, R, yaw, pitch, out=0.0):
    """머리 표면 위 얼굴 부위 좌표계: 로컬 +Z = 바깥, +Y = 위 (세계 위쪽에 맞춤)"""
    p = on_sphere(head_c, R, yaw, pitch, out)
    return frame(p, yaw, -pitch, 0)


def eyes(mb, head_c, R, spread=26, pitch=-4, size=0.42, lid=None, look=(0, 0)):
    """둥근 검은 눈 + 흰 반짝임 두 개. lid = 눈꺼풀 색이면 위쪽 반을 덮은 멍한 눈"""
    for s in (-1, 1):
        f = _face_frame(head_c, R, s * spread, pitch, -size * 0.18)
        ellipsoid(mb, f, (0, 0, 0), (size * 0.86, size * 1.08, size * 0.42), EYE, 14, 8)
        ellipsoid(mb, f, (-size * 0.28 + look[0], size * 0.38 + look[1], size * 0.3), (size * 0.3, size * 0.3, size * 0.16), EYE_SHINE, 8, 5)
        ellipsoid(mb, f, (size * 0.3, -size * 0.42, size * 0.3), (size * 0.13, size * 0.13, size * 0.08), EYE_SHINE, 6, 4)
        if lid:
            ellipsoid(mb, f, (0, size * 0.52, size * 0.1), (size * 1.02, size * 0.62, size * 0.5), lid, 14, 8)


def cheeks(mb, head_c, R, spread=48, pitch=-22, size=0.42):
    for s in (-1, 1):
        f = _face_frame(head_c, R, s * spread, pitch, -0.08)
        ellipsoid(mb, f, (0, 0, 0), (size, size * 0.62, 0.1), BLUSH, 12, 6)


def mouth(mb, head_c, R, pitch=-24, w=0.32, open_=False):
    f = _face_frame(head_c, R, 0, pitch, -0.05)
    ellipsoid(mb, f, (0, 0, 0), (w, w * (0.75 if open_ else 0.32), 0.09), MOUTH, 10, 6)
    if open_:
        ellipsoid(mb, f, (0, -w * 0.25, 0.03), (w * 0.6, w * 0.38, 0.07), TONGUE, 8, 5)


def strap(mb, a, b, r, col):
    capsule(mb, a, b, r, col, 8, 4)


# ---------- 초록 (흔함) ----------

def pigeon(mb, rng):
    grey, grey_d = (0.63, 0.64, 0.71), (0.52, 0.53, 0.60)
    base(mb, 'green', rng)
    y0 = BASE_TOP
    for s in (-1, 1):   # 분홍 발
        capsule(mb, (s * 0.8, y0, 0.2), (s * 0.8, y0 + 0.9, 0.0), 0.22, (0.95, 0.55, 0.58))
        ellipsoid(mb, IDENT, (s * 0.8, y0 + 0.12, 0.5), (0.45, 0.14, 0.6), (0.95, 0.55, 0.58), 10, 5)
    body_c = (0, y0 + 2.6, 0)
    ellipsoid(mb, IDENT, body_c, (2.25, 2.0, 2.0), grey)
    ellipsoid(mb, IDENT, (0, y0 + 2.2, 0.7), (1.6, 1.5, 1.4), (0.78, 0.78, 0.84))             # 밝은 배
    ellipsoid(mb, IDENT, (0, y0 + 4.05, 0.1), (2.05, 0.75, 1.95), (0.42, 0.72, 0.52))         # 목 초록 윤기
    ellipsoid(mb, IDENT, (0, y0 + 3.55, 0.15), (2.15, 0.6, 2.0), (0.62, 0.48, 0.80))          # 목 보라 윤기
    head_c, R = (0, y0 + 5.7, 0.1), 2.15
    sphere(mb, IDENT, head_c, R, grey, 22, 14)
    eyes(mb, head_c, R, spread=30, pitch=2, size=0.48, lid=grey_d)                             # 멍하게 반쯤 감긴 눈
    cheeks(mb, head_c, R)
    beak = on_sphere(head_c, R, 0, -12, -0.1)
    ellipsoid(mb, IDENT, (beak[0], beak[1] + 0.25, beak[2] - 0.05), (0.42, 0.3, 0.3), WHITE, 10, 6)   # 콧등
    ellipsoid(mb, IDENT, (beak[0], beak[1] - 0.05, beak[2] + 0.35), (0.3, 0.22, 0.5), (0.42, 0.40, 0.45), 10, 6)
    for s in (-1, 1):   # 날개
        ellipsoid(mb, frame((s * 2.05, y0 + 2.7, -0.1), 0, 0, s * 12), (0, 0, 0), (0.55, 1.6, 1.35), grey_d)
        ellipsoid(mb, frame((s * 2.15, y0 + 1.7, -0.4), 0, 0, s * 18), (0, 0, 0), (0.35, 0.7, 0.9), (0.40, 0.41, 0.47))
    ellipsoid(mb, IDENT, (0, y0 + 1.6, -2.0), (1.1, 0.5, 0.9), grey_d)                         # 꼬리
    # 초록 빵부스러기 주머니 (오른쪽 날개 끝)
    pc = (2.55, y0 + 1.7, 0.7)
    ellipsoid(mb, IDENT, pc, (0.75, 0.8, 0.7), ACC['green'])
    cylinder(mb, IDENT, (pc[0], pc[1] + 0.6, pc[2]), 0.62, 0.25, (0.36, 0.64, 0.24), 14)
    for k in range(4):
        sphere(mb, IDENT, (pc[0] - 0.3 + 0.2 * k, pc[1] + 0.95, pc[2] + 0.1 * (k % 2)), 0.2, (0.86, 0.66, 0.38), 6, 4)


def snail(mb, rng):
    cream, shell, shell_d = (0.97, 0.93, 0.82), (0.86, 0.64, 0.38), (0.68, 0.46, 0.25)
    base(mb, 'green', rng)
    y0 = BASE_TOP
    ellipsoid(mb, IDENT, (0, y0 + 0.45, 0.3), (1.7, 0.55, 2.2), cream)                         # 발(배) 판
    body_c = (0, y0 + 2.3, 0.3)
    ellipsoid(mb, IDENT, body_c, (1.8, 1.9, 1.6), cream)
    head_c, R = (0, y0 + 5.0, 0.45), 2.0
    sphere(mb, IDENT, head_c, R, cream, 22, 14)
    eyes(mb, head_c, R, spread=27, pitch=-6, size=0.46, lid=(0.90, 0.85, 0.74))
    cheeks(mb, head_c, R)
    mouth(mb, head_c, R, pitch=-28, w=0.28)
    for s in (-1, 1):   # 눈자루 (한쪽은 축 처짐)
        tip = (s * 1.05, head_c[1] + 2.35 - (0.55 if s > 0 else 0.0), head_c[2] + (0.75 if s > 0 else 0.3))
        capsule(mb, (s * 0.55, head_c[1] + 1.5, head_c[2] + 0.1), tip, 0.17, cream, 8, 5)
        sphere(mb, IDENT, tip, 0.3, (0.93, 0.86, 0.70), 10, 6)
    # 등 껍데기: 큰 소용돌이 원반 + 나선 띠
    sc = (0, y0 + 2.7, -1.7)
    ellipsoid(mb, IDENT, sc, (1.3, 2.05, 2.05), shell, 24, 14)
    for k in range(48):
        t = k / 47
        a = t * 4.0 * math.pi
        rad = 1.75 * (1 - t) + 0.1
        bulge = 1.3 * math.sqrt(max(0.0, 1 - (rad / 2.05) ** 2))
        for s in (-1, 1):
            sphere(mb, IDENT, (s * (bulge + 0.02), sc[1] + math.sin(a) * rad, sc[2] + math.cos(a) * rad), 0.16 + 0.06 * (1 - t), shell_d, 8, 5)
    for s in (-1, 1):   # 짧은 팔
        capsule(mb, (s * 1.5, y0 + 2.8, 0.6), (s * 2.0, y0 + 2.2, 1.2), 0.38, cream)
    # 초록 잎 우산 (오른손에 줄기, 머리 위로)
    capsule(mb, (2.05, y0 + 2.2, 1.25), (1.2, y0 + 8.3, 0.5), 0.13, (0.36, 0.58, 0.20), 8, 4)
    leaf = frame((0.9, y0 + 8.4, 0.4), 15, -8, 14)
    ellipsoid(mb, leaf, (0, 0, 0), (3.1, 0.32, 2.5), ACC['green'], 22, 10)
    ellipsoid(mb, leaf, (0, 0.18, 0), (2.9, 0.18, 0.12), (0.62, 0.86, 0.42), 10, 4)        # 잎맥


def ant(mb, rng):
    brown, brown_d = (0.70, 0.40, 0.24), (0.55, 0.30, 0.17)
    base(mb, 'green', rng)
    y0 = BASE_TOP
    for s in (-1, 1):   # 다리
        capsule(mb, (s * 0.7, y0 + 2.1, 0), (s * 0.85, y0 + 0.25, 0.15), 0.32, brown_d)
        ellipsoid(mb, IDENT, (s * 0.85, y0 + 0.2, 0.35), (0.42, 0.22, 0.55), brown_d, 10, 6)
    ellipsoid(mb, IDENT, (0, y0 + 2.6, 0), (1.25, 1.1, 1.0), brown)                           # 가슴
    ellipsoid(mb, frame((0, y0 + 2.2, -1.5), 0, 25, 0), (0, 0, 0), (1.35, 1.25, 1.55), brown_d)   # 배
    head_c, R = (0, y0 + 5.0, 0.3), 2.1
    sphere(mb, IDENT, head_c, R, brown, 22, 14)
    eyes(mb, head_c, R, spread=25, pitch=-2, size=0.6, look=(0.06, 0))
    cheeks(mb, head_c, R)
    mouth(mb, head_c, R, pitch=-30, w=0.26, open_=True)
    for s in (-1, 1):   # 팔 (잎을 받친다) + 작은 가운데 팔
        capsule(mb, (s * 1.1, y0 + 3.1, 0.2), (s * 1.9, y0 + 4.6, 0.6), 0.27, brown_d)
        capsule(mb, (s * 1.0, y0 + 2.4, 0.3), (s * 1.6, y0 + 1.8, 0.8), 0.2, brown_d)
    for s in (-1, 1):   # 꺾인 더듬이
        a = (s * 0.6, head_c[1] + 1.7, head_c[2] + 0.2)
        b = (s * 1.2, head_c[1] + 2.9, head_c[2] + 0.5)
        c = (s * 2.0, head_c[1] + 3.1, head_c[2] + 1.2)
        capsule(mb, a, b, 0.12, brown_d, 8, 4)
        capsule(mb, b, c, 0.12, brown_d, 8, 4)
        sphere(mb, IDENT, c, 0.24, brown_d, 8, 5)
    # 몸보다 큰 초록 잎을 머리에 이고 간다
    leaf = frame((0.2, y0 + 7.5, 0.4), 30, 10, -6)
    ellipsoid(mb, leaf, (0, 0, 0), (3.6, 0.35, 2.7), ACC['green'], 22, 10)
    ellipsoid(mb, leaf, (0, 0.2, 0), (3.3, 0.18, 0.12), (0.62, 0.86, 0.42), 10, 4)


def ladybug(mb, rng):
    red, black, face = (0.90, 0.18, 0.16), (0.16, 0.14, 0.16), (0.33, 0.27, 0.28)
    base(mb, 'green', rng)
    y0 = BASE_TOP
    for s in (-1, 1):
        capsule(mb, (s * 0.8, y0 + 1.4, 0.2), (s * 0.9, y0 + 0.25, 0.3), 0.32, black)
        ellipsoid(mb, IDENT, (s * 0.9, y0 + 0.2, 0.5), (0.42, 0.2, 0.55), black, 10, 6)
    shell_c = (0, y0 + 2.6, -0.1)
    ellipsoid(mb, IDENT, shell_c, (2.25, 2.0, 2.05), red, 24, 14)
    ellipsoid(mb, IDENT, (0, y0 + 2.4, 0.9), (1.5, 1.4, 1.2), black)                           # 앞 배
    for (yaw, pitch, s_) in ((60, 20, 0.5), (-60, 20, 0.5), (110, -5, 0.45), (-110, -5, 0.45), (150, 30, 0.55), (-150, 30, 0.55), (180, -10, 0.45)):
        p = on_sphere((0, 0, 0), 1.0, yaw, pitch)
        q = (shell_c[0] + p[0] * 2.25, shell_c[1] + p[1] * 2.0, shell_c[2] + p[2] * 2.05)
        f = along(shell_c, q)
        ellipsoid(mb, f, (0, math.dist(shell_c, q) - 0.05, 0), (s_, 0.12, s_), black, 12, 6)
    ellipsoid(mb, IDENT, (0, y0 + 4.5, -0.6), (0.08, 0.15, 1.7), black, 6, 4)                   # 날개 가운데 선
    head_c, R = (0, y0 + 5.15, 0.45), 2.05
    sphere(mb, IDENT, head_c, R, face, 22, 14)
    eyes(mb, head_c, R, spread=27, pitch=-4, size=0.55)
    cheeks(mb, head_c, R)
    mouth(mb, head_c, R, pitch=-28, w=0.3, open_=True)
    for s in (-1, 1):
        capsule(mb, (s * 0.7, head_c[1] + 1.6, head_c[2]), (s * 1.5, head_c[1] + 3.0, head_c[2] + 0.2), 0.12, black, 8, 4)
        sphere(mb, IDENT, (s * 1.5, head_c[1] + 3.05, head_c[2] + 0.2), 0.32, black, 10, 6)
        capsule(mb, (s * 1.6, y0 + 3.2, 0.4), (s * 2.2, y0 + 2.2, 0.8), 0.3, black)
    # 초록 미니 배낭 (오른쪽 어깨 뒤) + 크림슨 꽃 장식
    bp = frame((1.6, y0 + 3.0, -1.4), -35, 0, 0)
    ellipsoid(mb, bp, (0, 0, 0), (0.9, 1.0, 0.65), ACC['green'], 14, 8)
    ellipsoid(mb, bp, (0, -0.35, 0.55), (0.6, 0.45, 0.25), (0.36, 0.64, 0.24), 10, 6)
    strap(mb, (1.3, y0 + 3.9, -0.8), (1.5, y0 + 2.0, 0.9), 0.12, (0.36, 0.64, 0.24))
    for k in range(5):
        a = k * 2 * math.pi / 5
        sphere(mb, IDENT, (1.55 + math.cos(a) * 0.18, y0 + 3.45 + math.sin(a) * 0.18, 1.05), 0.14, CRIMSON, 6, 4)


# ---------- 파랑 (드묾) ----------

def stray_cat(mb, rng):
    orange, orange_d, cream = (0.98, 0.72, 0.38), (0.90, 0.56, 0.24), (0.99, 0.95, 0.88)
    base(mb, 'blue', rng)
    y0 = BASE_TOP
    for s in (-1, 1):
        capsule(mb, (s * 0.85, y0 + 1.6, 0.1), (s * 0.9, y0 + 0.35, 0.3), 0.55, orange)
        ellipsoid(mb, IDENT, (s * 0.9, y0 + 0.3, 0.55), (0.6, 0.32, 0.7), cream, 12, 6)
    ellipsoid(mb, IDENT, (0, y0 + 2.5, 0), (1.75, 1.9, 1.55), orange)
    ellipsoid(mb, IDENT, (0, y0 + 2.4, 0.75), (1.15, 1.45, 0.95), cream)                      # 흰 가슴
    capsule(mb, (0, y0 + 1.4, -1.4), (0.9, y0 + 3.6, -2.4), 0.42, orange)                     # 꼬리
    sphere(mb, IDENT, (0.9, y0 + 3.6, -2.4), 0.45, orange_d, 10, 6)
    head_c, R = (0, y0 + 5.4, 0.2), 2.3
    ellipsoid(mb, IDENT, head_c, (R * 1.08, R * 0.95, R), orange, 24, 14)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.75, head_c[2] + 1.55), (1.2, 0.85, 0.75), cream, 14, 8)   # 흰 주둥이
    for k in range(3):   # 이마 줄무늬
        p = on_sphere(head_c, R, (k - 1) * 16, 52, -0.08)
        ellipsoid(mb, along(head_c, p), (0, math.dist(head_c, p), 0), (0.16, 0.1, 0.5), orange_d, 8, 4)
    eyes(mb, head_c, R, spread=30, pitch=0, size=0.52)
    cheeks(mb, head_c, R, spread=50)
    sphere(mb, IDENT, on_sphere(head_c, R, 0, -12, 0.05), 0.22, (0.98, 0.55, 0.62), 8, 5)    # 코
    mouth(mb, head_c, R, pitch=-26, w=0.26)
    for s in (-1, 1):   # 귀 (왼쪽 귀 끝은 TNR 로 평평하게 잘림)
        ear = frame((s * 1.35, head_c[1] + 1.55, head_c[2] - 0.1), 0, 0, -s * 18)
        if s < 0:
            mb.prism(ear, (0, 0, 0), 0.9, 1.1, 12, orange, caps=True, r_top=0.32)
        else:
            cone(mb, ear, (0, 0, 0), 0.9, 1.6, orange, 12)
        cone(mb, frame((s * 1.36, head_c[1] + 1.6, head_c[2] + 0.12), 0, 0, -s * 18), (0, 0, 0), 0.5, 0.95 if s > 0 else 0.7, (0.98, 0.70, 0.72), 10)
    # 파랑 방울 목걸이
    mb.prism(IDENT, (0, y0 + 3.85, 0), 1.45, 0.35, 24, ACC['blue'], caps=True, r_top=1.5)
    sphere(mb, IDENT, (0, y0 + 3.6, 1.5), 0.32, (1.0, 0.82, 0.30), 10, 6)
    # 왼팔 내리고 오른팔에 파랑 생선 간식을 트로피처럼
    capsule(mb, (-1.5, y0 + 3.2, 0.2), (-1.9, y0 + 2.1, 0.7), 0.42, orange)
    capsule(mb, (1.5, y0 + 3.3, 0.2), (2.3, y0 + 4.6, 0.6), 0.42, orange)
    fish = frame((2.6, y0 + 5.5, 0.7), 0, 0, -20)
    ellipsoid(mb, fish, (0, 0, 0), (0.45, 0.95, 0.3), ACC['blue'], 12, 8)
    cone(mb, frame(fish.p((0, -0.8, 0)), 0, 180, -20), (0, 0, 0), 0.45, 0.6, ACC['blue'], 8)


def squirrel(mb, rng):
    brown, brown_d, cream = (0.50, 0.37, 0.28), (0.38, 0.27, 0.20), (0.96, 0.90, 0.80)
    base(mb, 'blue', rng)
    y0 = BASE_TOP
    # 큰 꼬리: 뒤로 휘어 올라가는 덩어리 4개
    for k, (y, z, r) in enumerate(((1.6, -1.9, 1.3), (3.2, -2.6, 1.6), (5.0, -2.5, 1.7), (6.4, -1.8, 1.4))):
        sphere(mb, IDENT, (0, y0 + y, z), r, brown if k % 2 == 0 else brown_d, 18, 10)
    for s in (-1, 1):
        capsule(mb, (s * 0.8, y0 + 1.5, 0.1), (s * 0.85, y0 + 0.3, 0.35), 0.48, brown)
        ellipsoid(mb, IDENT, (s * 0.85, y0 + 0.25, 0.6), (0.5, 0.25, 0.65), brown_d, 10, 6)
    ellipsoid(mb, IDENT, (0, y0 + 2.4, 0), (1.55, 1.85, 1.4), brown)
    ellipsoid(mb, IDENT, (0, y0 + 2.3, 0.7), (1.0, 1.4, 0.85), cream)
    head_c, R = (0, y0 + 5.2, 0.25), 2.15
    sphere(mb, IDENT, head_c, R, brown, 24, 14)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.75, head_c[2] + 1.45), (1.05, 0.8, 0.75), cream, 14, 8)
    eyes(mb, head_c, R, spread=29, pitch=0, size=0.55)
    cheeks(mb, head_c, R)
    sphere(mb, IDENT, on_sphere(head_c, R, 0, -12, 0.05), 0.22, (0.30, 0.20, 0.18), 8, 5)
    mouth(mb, head_c, R, pitch=-27, w=0.24)
    for s in (-1, 1):   # 귀 + 귀 끝 털뭉치
        ear = frame((s * 1.15, head_c[1] + 1.6, head_c[2] - 0.3), 0, 0, -s * 15)
        cone(mb, ear, (0, 0, 0), 0.7, 1.4, brown, 12)
        cone(mb, frame(ear.p((0, 1.2, 0)), 0, 0, -s * 30), (0, 0, 0), 0.32, 1.3, brown_d, 8)
    # 파랑 니트 모자 + 방울
    hat = frame((0.15, head_c[1] + 1.75, head_c[2] + 0.1), 0, -8, -6)
    ellipsoid(mb, hat, (0, 0, 0), (1.25, 0.8, 1.2), ACC['blue'], 18, 10)
    mb.prism(hat, (0, -0.25, 0), 1.3, 0.35, 20, (0.26, 0.52, 0.86), caps=True)
    sphere(mb, hat, (0, 0.85, 0), 0.38, (0.55, 0.78, 1.0), 10, 6)
    # 파랑 도토리 주머니 (어깨끈) + 손에 도토리
    strap(mb, (-1.2, y0 + 3.9, 0.6), (1.2, y0 + 1.9, 1.05), 0.12, (0.26, 0.52, 0.86))
    ellipsoid(mb, IDENT, (1.35, y0 + 1.7, 1.0), (0.7, 0.62, 0.42), ACC['blue'], 12, 8)
    sphere(mb, IDENT, (1.35, y0 + 1.85, 1.4), 0.13, CRIMSON, 6, 4)
    for s in (-1, 1):
        capsule(mb, (s * 1.3, y0 + 3.2, 0.3), (s * 0.5, y0 + 2.7, 1.5), 0.36, brown)
    ellipsoid(mb, IDENT, (0, y0 + 2.75, 1.75), (0.42, 0.5, 0.42), (0.72, 0.48, 0.24), 10, 6)  # 도토리
    ellipsoid(mb, IDENT, (0, y0 + 3.15, 1.75), (0.46, 0.2, 0.46), (0.50, 0.34, 0.18), 10, 5)


def crow(mb, rng):
    black, black_l, sheen = (0.15, 0.15, 0.20), (0.24, 0.24, 0.31), (0.30, 0.38, 0.62)
    base(mb, 'blue', rng)
    y0 = BASE_TOP
    for s in (-1, 1):
        capsule(mb, (s * 0.7, y0 + 1.4, 0.1), (s * 0.75, y0 + 0.2, 0.3), 0.2, (0.30, 0.30, 0.34))
        for k in (-1, 0, 1):
            capsule(mb, (s * 0.75, y0 + 0.18, 0.3), (s * 0.75 + k * 0.35, y0 + 0.15, 0.95), 0.12, (0.30, 0.30, 0.34), 6, 3)
    ellipsoid(mb, IDENT, (0, y0 + 2.6, 0), (1.9, 1.95, 1.7), black)
    ellipsoid(mb, IDENT, (0, y0 + 1.6, -1.8), (0.9, 0.35, 1.1), black_l)                        # 꼬리
    head_c, R = (0, y0 + 5.3, 0.2), 2.2
    sphere(mb, IDENT, head_c, R, black, 24, 14)
    ellipsoid(mb, IDENT, (0.2, head_c[1] + 1.95, head_c[2] - 0.2), (0.3, 0.6, 0.5), black_l, 8, 5)  # 삐친 머리깃
    eyes(mb, head_c, R, spread=28, pitch=2, size=0.55)
    cheeks(mb, head_c, R)
    bk = on_sphere(head_c, R, 0, -14, -0.2)
    cone(mb, frame(bk, 0, 90, 0), (0, 0, 0), 0.5, 1.0, (0.40, 0.40, 0.44), 10)                # 부리
    for s in (-1, 1):   # 날개 (오른쪽은 병뚜껑을 들고 위로)
        if s < 0:
            ellipsoid(mb, frame((-1.95, y0 + 2.6, 0), 0, 0, -10), (0, 0, 0), (0.55, 1.7, 1.3), black_l)
        else:
            ellipsoid(mb, frame((2.2, y0 + 3.9, 0.2), 0, 0, -40), (0, 0, 0), (0.55, 1.8, 1.2), black_l)
            cylinder(mb, frame((3.25, y0 + 5.4, 0.4), 0, 80, 0), (0, 0, 0), 0.5, 0.15, (0.78, 0.80, 0.84), 16)
    ellipsoid(mb, IDENT, (0, y0 + 3.2, 0.4), (1.5, 0.9, 1.3), sheen)                          # 푸른 윤기 (가슴)
    # 파랑 스카프 (목둘레 + 늘어진 끝) + 크림슨 꽃 핀
    mb.prism(IDENT, (0, y0 + 3.95, 0.05), 1.75, 0.55, 24, ACC['blue'], caps=True, r_top=1.8)
    capsule(mb, (-0.8, y0 + 4.0, 1.4), (-1.3, y0 + 2.6, 1.6), 0.32, ACC['blue'])
    for k in range(5):
        a = k * 2 * math.pi / 5
        sphere(mb, IDENT, (0.6 + math.cos(a) * 0.2, y0 + 4.2 + math.sin(a) * 0.2, 1.75), 0.15, CRIMSON, 6, 4)
    # 파랑 주머니 (병뚜껑 몇 개)
    ellipsoid(mb, IDENT, (-0.2, y0 + 1.9, 1.5), (0.8, 0.6, 0.35), ACC['blue'], 12, 8)
    for k in range(3):
        cylinder(mb, IDENT, (-0.6 + 0.4 * k, y0 + 2.35, 1.5), 0.2, 0.1, (0.82, 0.84, 0.88), 10)


def raccoon_dog(mb, rng):
    fur, fur_d, cream, mask = (0.68, 0.60, 0.52), (0.45, 0.38, 0.32), (0.96, 0.92, 0.84), (0.30, 0.24, 0.21)
    base(mb, 'blue', rng)
    y0 = BASE_TOP
    for s in (-1, 1):
        capsule(mb, (s * 0.85, y0 + 1.6, 0.1), (s * 0.9, y0 + 0.35, 0.3), 0.55, fur_d)
        ellipsoid(mb, IDENT, (s * 0.9, y0 + 0.3, 0.55), (0.6, 0.3, 0.7), fur_d, 12, 6)
    ellipsoid(mb, IDENT, (0, y0 + 2.5, 0), (1.85, 1.9, 1.6), fur)
    ellipsoid(mb, IDENT, (0, y0 + 2.3, 0.8), (1.2, 1.3, 0.85), cream)
    ellipsoid(mb, frame((0, y0 + 1.6, -1.8), 0, -30, 0), (0, 0, 0), (0.85, 1.2, 0.85), fur_d)   # 줄무늬 없는 짧은 꼬리
    head_c, R = (0, y0 + 5.3, 0.2), 2.3
    ellipsoid(mb, IDENT, head_c, (R * 1.1, R * 0.95, R), fur, 24, 14)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.7, head_c[2] + 1.55), (1.25, 0.85, 0.8), cream, 14, 8)
    for s in (-1, 1):   # 눈가 검은 무늬
        p = on_sphere(head_c, R, s * 30, -2, -0.15)
        ellipsoid(mb, along(head_c, p), (0, math.dist(head_c, p), 0), (0.95, 0.2, 0.75), mask, 12, 6)
    eyes(mb, head_c, R, spread=30, pitch=0, size=0.5)
    cheeks(mb, head_c, R, spread=52, pitch=-26)
    sphere(mb, IDENT, on_sphere(head_c, R, 0, -12, 0.12), 0.26, (0.22, 0.18, 0.17), 8, 5)
    mouth(mb, head_c, R, pitch=-28, w=0.3, open_=True)
    for s in (-1, 1):   # 둥근 귀 (가장자리 짙게)
        ear = frame((s * 1.6, head_c[1] + 1.6, head_c[2] - 0.2), 0, 0, -s * 20)
        ellipsoid(mb, ear, (0, 0, 0), (0.75, 0.7, 0.35), mask, 12, 8)
        ellipsoid(mb, ear, (0, -0.05, 0.18), (0.5, 0.48, 0.25), cream, 10, 6)
    # 파랑 배낭 + 양손에 파랑 과자 봉지
    ellipsoid(mb, IDENT, (0, y0 + 2.8, -1.55), (1.25, 1.35, 0.7), ACC['blue'], 14, 8)
    ellipsoid(mb, IDENT, (0, y0 + 2.3, -2.15), (0.8, 0.6, 0.3), (0.26, 0.52, 0.86), 10, 6)
    for s in (-1, 1):
        strap(mb, (s * 1.0, y0 + 3.9, -0.6), (s * 1.1, y0 + 2.0, 0.9), 0.13, (0.26, 0.52, 0.86))
        capsule(mb, (s * 1.5, y0 + 3.2, 0.2), (s * 0.55, y0 + 2.7, 1.45), 0.42, fur_d)
    mb.box(frame((0, y0 + 3.0, 1.7), 0, -10, 0), (0, 0, 0), (1.5, 1.6, 0.45), ACC['blue'])
    mb.box(frame((0, y0 + 3.85, 1.65), 0, -10, 0), (0, 0, 0), (1.6, 0.2, 0.5), (0.55, 0.78, 1.0))
    ellipsoid(mb, IDENT, (0, y0 + 3.05, 1.98), (0.35, 0.3, 0.05), (1.0, 0.86, 0.45), 10, 5)    # 봉지 그림
    for k in range(3):
        sphere(mb, IDENT, (-0.35 + 0.35 * k, y0 + 4.1, 1.6), 0.18, (0.98, 0.82, 0.45), 6, 4)


# ---------- 목록 ----------

CREATURES = {
    # id: (도감 이름, 등급, 만드는 함수)
    'pigeon': ('비둘기', 'green', pigeon),
    'snail': ('달팽이', 'green', snail),
    'ant': ('개미', 'green', ant),
    'ladybug': ('무당벌레', 'green', ladybug),
    'stray_cat': ('길고양이', 'blue', stray_cat),
    'squirrel': ('청설모', 'blue', squirrel),
    'crow': ('까마귀', 'blue', crow),
    'raccoon_dog': ('너구리', 'blue', raccoon_dog),
}


def build_all(seed=5):
    out = []
    for cid, (_, _, fn) in CREATURES.items():
        mb = Builder()
        mb.ao_floor, mb.ao_height, mb.ao_strength = BASE_TOP, 1.2, 0.18
        fn(mb, random.Random(seed + len(out)))
        out.append((f'Creature_{cid}', mb))
    return out


# ---------- 노랑 (가장 드묾, 랜드마크마다) ----------

GOLD = (1.0, 0.80, 0.24)
GOLD_L = (1.0, 0.92, 0.58)
SILVER = (0.80, 0.82, 0.86)


def gold_base(mb, rng):
    """금테 받침 + 윤기 띠 + 둘레에 떠 있는 작은 금빛 반짝이"""
    base(mb, 'gold', rng)
    mb.prism(IDENT, (0, 0.6, 0), 4.6, 0.08, 40, GOLD_L, caps=False)
    for a, y, r in ((40, 8.6, 0.32), (150, 6.2, 0.26), (260, 9.4, 0.22), (320, 4.8, 0.2)):
        x, z = math.cos(math.radians(a)) * 3.9, math.sin(math.radians(a)) * 3.9
        mb.cone(IDENT, (x, y, z), r, r * 2.2, 4, GOLD_L)
        mb.cone(frame((x, y, z), 0, 180, 0), (0, 0, 0), r, r * 2.2, 4, GOLD_L)


def ring(mb, m, c, R, r, col, n=28, axis='y'):
    """작은 구를 촘촘히 이어 만든 고리 (안경테·헬멧 테·헤드폰 띠·수영 튜브)"""
    n = max(n, int(2 * math.pi * R / (r * 0.9)))
    for k in range(n):
        a = 2 * math.pi * k / n
        if axis == 'y':
            p = (c[0] + math.cos(a) * R, c[1], c[2] + math.sin(a) * R)
        else:   # z 축 둘레 (얼굴 앞 고리)
            p = (c[0] + math.cos(a) * R, c[1] + math.sin(a) * R, c[2])
        sphere(mb, m, p, r, col, 8, 5)


def biped(mb, col, belly=None, foot=None, y0=BASE_TOP, body_r=(1.75, 1.9, 1.55), head_r=2.25, head_y=5.3, legs=True):
    """두 발로 선 2등신 몸통 + 머리. 머리 중심과 반지름을 돌려준다"""
    foot = foot or col
    if legs:
        for s in (-1, 1):
            capsule(mb, (s * 0.85, y0 + 1.6, 0.05), (s * 0.9, y0 + 0.35, 0.25), 0.52, foot)
            ellipsoid(mb, IDENT, (s * 0.9, y0 + 0.28, 0.5), (0.58, 0.3, 0.72), foot, 12, 6)
    ellipsoid(mb, IDENT, (0, y0 + 2.5, 0), body_r, col)
    if belly:
        ellipsoid(mb, IDENT, (0, y0 + 2.35, body_r[2] * 0.48), (body_r[0] * 0.66, body_r[1] * 0.72, body_r[2] * 0.6), belly)
    head_c = (0, y0 + head_y, 0.2)
    sphere(mb, IDENT, head_c, head_r, col, 24, 14)
    return head_c, head_r


def arms(mb, col, left_to, right_to, y0=BASE_TOP, r=0.42):
    capsule(mb, (-1.5, y0 + 3.3, 0.2), left_to, r, col)
    capsule(mb, (1.5, y0 + 3.3, 0.2), right_to, r, col)


def space_rabbit(mb, rng):
    suit, pink = (0.97, 0.97, 0.99), (0.98, 0.70, 0.74)
    gold_base(mb, rng)
    y0 = BASE_TOP
    # 로켓·달 돌·톱니
    mb.prism(IDENT, (-3.0, y0, 1.6), 0.38, 1.4, 12, WHITE, caps=True)
    mb.cone(IDENT, (-3.0, y0 + 1.4, 1.6), 0.38, 0.8, 12, CRIMSON)
    for k in range(3):
        mb.box(frame((-3.0, y0 + 0.35, 1.6), k * 120, 0, 0), (0.45, 0, 0), (0.5, 0.6, 0.08), CRIMSON)
    ellipsoid(mb, IDENT, (2.9, y0 + 0.3, 1.9), (0.7, 0.5, 0.6), (0.62, 0.62, 0.64), 12, 7)
    mb.prism(IDENT, (2.3, y0, -2.6), 0.6, 0.22, 10, GOLD, caps=True)
    head_c, R = biped(mb, suit, None, (0.95, 0.95, 0.97))
    for s in (-1, 1):   # 금색 손목·발목 고리
        mb.prism(IDENT, (s * 0.9, y0 + 0.75, 0.2), 0.62, 0.22, 14, GOLD, caps=True)
    mb.prism(IDENT, (0, y0 + 1.5, 0), 1.72, 0.3, 24, GOLD, caps=True)                       # 허리띠
    sphere(mb, IDENT, (0, y0 + 3.0, 1.5), 0.42, GOLD_L, 12, 6)                                # 가슴 패치
    sphere(mb, IDENT, (0.95, y0 + 3.4, 1.15), 0.14, CRIMSON, 6, 4)
    face = (0.99, 0.97, 0.95)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.1, head_c[2] + 0.6), (1.7, 1.55, 1.6), face, 18, 10)
    eyes(mb, head_c, R, spread=26, pitch=-6, size=0.5)
    cheeks(mb, head_c, R)
    mouth(mb, head_c, R, pitch=-26, w=0.26, open_=True)
    sphere(mb, IDENT, on_sphere(head_c, R, 0, -14, 0.05), 0.17, pink, 8, 5)
    ring(mb, IDENT, (0, head_c[1] - 0.05, head_c[2] + 1.75), 1.9, 0.24, (0.95, 0.95, 0.97), 30, axis='z')  # 헬멧 테
    ring(mb, IDENT, (0, head_c[1] - 0.05, head_c[2] + 1.9), 1.9, 0.08, GOLD, 30, axis='z')
    sphere(mb, IDENT, (0.95, head_c[1] + 1.0, head_c[2] + 1.95), 0.18, (0.92, 0.97, 1.0), 6, 4)  # 헬멧 유리 반사
    for s in (-1, 1):   # 귀 (헬멧 귀 혹)
        ear = frame((s * 0.75, head_c[1] + 1.8, head_c[2] - 0.2), 0, 0, -s * 8)
        ellipsoid(mb, ear, (0, 1.4, 0), (0.55, 1.6, 0.42), suit, 14, 8)
        ellipsoid(mb, ear, (0, 1.4, 0.3), (0.3, 1.25, 0.2), pink, 10, 6)
    # 제트팩 + 노란 분사구
    mb.box(IDENT, (0, y0 + 3.0, -1.7), (2.0, 2.2, 0.9), (0.92, 0.92, 0.95))
    for s in (-1, 1):
        mb.prism(IDENT, (s * 0.6, y0 + 1.5, -1.9), 0.35, 0.5, 12, GOLD, caps=True)
    arms(mb, suit, (-1.9, y0 + 2.2, 0.8), (2.2, y0 + 4.4, 0.8))
    w = frame((2.5, y0 + 5.0, 0.9), 0, 0, -25)                                                # 스패너
    mb.box(w, (0, 0, 0), (0.28, 1.6, 0.18), SILVER)
    ring(mb, w, (0, 0.95, 0), 0.32, 0.1, SILVER, 10, axis='z')


def library_owl(mb, rng):
    brown, cream, navy = (0.56, 0.39, 0.27), (0.97, 0.90, 0.78), (0.16, 0.20, 0.38)
    gold_base(mb, rng)
    y0 = BASE_TOP
    for k, c in enumerate(((0.62, 0.18, 0.18), (0.20, 0.36, 0.26), (0.18, 0.24, 0.45))):     # 쌓인 책
        mb.box(frame((-3.0, y0 + 0.22 + k * 0.42, 1.2), k * 12, 0, 0), (0, 0, 0), (1.5, 0.4, 1.0), c)
    head_c, R = biped(mb, brown, cream, (0.95, 0.70, 0.30), head_r=2.35)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.15, head_c[2] + 1.0), (1.85, 1.55, 1.45), cream, 18, 10)   # 얼굴판
    for s in (-1, 1):
        cone(mb, frame((s * 1.5, head_c[1] + 1.7, head_c[2]), 0, 0, -s * 22), (0, 0, 0), 0.55, 1.1, brown, 10)  # 귀깃
    eyes(mb, head_c, R, spread=24, pitch=-2, size=0.58)
    for s in (-1, 1):   # 금테 동그란 안경
        p = on_sphere(head_c, R, s * 24, -2, 0.1)
        ring(mb, frame(p, s * 24, 2, 0), (0, 0, 0), 0.82, 0.09, GOLD, 22, axis='z')
    sphere(mb, IDENT, on_sphere(head_c, R, 0, 2, 0.15), 0.12, GOLD, 6, 4)
    cone(mb, frame(on_sphere(head_c, R, 0, -16, -0.1), 0, 100, 0), (0, 0, 0), 0.3, 0.6, (0.95, 0.70, 0.30), 8)  # 부리
    cheeks(mb, head_c, R, spread=46, pitch=-24)
    ellipsoid(mb, IDENT, (0, y0 + 3.6, -0.35), (2.0, 1.1, 1.75), navy, 18, 10)                 # 학자 망토 (어깨)
    sphere(mb, IDENT, (0, y0 + 3.9, 1.45), 0.2, CRIMSON, 8, 5)
    for s in (-1, 1):   # 날개 (책을 든다)
        capsule(mb, (s * 1.6, y0 + 3.2, 0.1), (s * 0.7, y0 + 2.7, 1.55), 0.48, brown)
    book = frame((0, y0 + 2.8, 1.9), 0, -25, 0)
    for s in (-1, 1):
        mb.box(frame(book.p((s * 0.55, 0, 0)), 0, -25, s * 12), (0, 0, 0), (1.1, 1.3, 0.12), (0.97, 0.94, 0.86))
    mb.box(book, (0, 0, -0.1), (2.3, 1.4, 0.08), (0.55, 0.20, 0.18))


def plaza_duck(mb, rng):
    white, beak = (0.99, 0.98, 0.95), (1.0, 0.62, 0.24)
    gold_base(mb, rng)
    y0 = BASE_TOP
    cylinder(mb, IDENT, (2.4, y0 - 0.05, 1.6), 1.25, 0.1, (0.55, 0.80, 0.96), 20)               # 물웅덩이
    ellipsoid(mb, IDENT, (2.5, y0 + 0.1, 1.6), (0.6, 0.06, 0.55), (0.40, 0.68, 0.30), 10, 4)   # 연잎
    sphere(mb, IDENT, (2.3, y0 + 0.2, 1.7), 0.14, (0.98, 0.75, 0.85), 6, 4)
    head_c, R = biped(mb, white, None, beak, body_r=(1.9, 1.85, 1.7))
    eyes(mb, head_c, R, spread=26, pitch=-2, size=0.5, lid=white)                             # 꾸벅 조는 눈
    cheeks(mb, head_c, R)
    ellipsoid(mb, IDENT, on_sphere(head_c, R, 0, -16, 0.2), (0.75, 0.32, 0.6), beak, 12, 6)
    for k in range(3):
        sphere(mb, IDENT, (0.1 * k - 0.1, head_c[1] + R + 0.2 + 0.25 * k, head_c[2] - 0.2 * k), 0.3 - 0.07 * k, white, 8, 5)
    # 노란 비모자
    hat = frame((0, head_c[1] + 1.35, head_c[2] - 0.1), 0, -10, 0)
    cylinder(mb, hat, (0, 0, 0), 2.6, 0.18, GOLD, 28, r_top=2.4)
    ellipsoid(mb, hat, (0, 0.2, 0), (1.75, 1.2, 1.75), GOLD, 18, 10)
    sphere(mb, hat, (1.2, 0.8, 1.0), 0.3, CRIMSON, 8, 5)
    # 노란 튜브 (허리) + 오리 얼굴 무늬
    ring(mb, IDENT, (0, y0 + 2.2, 0), 2.05, 0.55, GOLD, 30)
    for s in (-1, 1):
        sphere(mb, IDENT, (s * 0.35, y0 + 2.55, 2.55), 0.12, EYE, 6, 4)
    arms(mb, white, (-2.3, y0 + 2.9, 0.4), (2.3, y0 + 2.9, 0.4), r=0.45)


def gate_magpie(mb, rng):
    black, white, teal = (0.13, 0.13, 0.17), (0.98, 0.98, 0.97), (0.20, 0.50, 0.62)
    gold_base(mb, rng)
    y0 = BASE_TOP
    mb.prism(IDENT, (2.9, y0, 1.4), 0.32, 1.6, 12, (0.94, 0.91, 0.84), caps=True)              # 작은 돌기둥
    mb.box(IDENT, (2.9, y0 + 1.7, 1.4), (0.9, 0.2, 0.9), (0.97, 0.95, 0.90))
    for s in (-1, 1):
        capsule(mb, (s * 0.7, y0 + 1.4, 0.1), (s * 0.75, y0 + 0.2, 0.3), 0.18, (0.25, 0.25, 0.28))
        ellipsoid(mb, IDENT, (s * 0.75, y0 + 0.15, 0.6), (0.3, 0.12, 0.55), (0.25, 0.25, 0.28), 8, 4)
    ellipsoid(mb, IDENT, (0, y0 + 2.6, 0), (1.85, 1.95, 1.65), black)
    ellipsoid(mb, IDENT, (0, y0 + 2.3, 0.8), (1.4, 1.45, 1.0), white)                        # 흰 배
    ellipsoid(mb, frame((0, y0 + 1.8, -2.4), 0, -40, 0), (0, 0, 0), (0.55, 0.25, 1.9), teal)  # 긴 꼬리
    head_c, R = (0, y0 + 5.3, 0.2), 2.2
    sphere(mb, IDENT, head_c, R, black, 24, 14)
    eyes(mb, head_c, R, spread=28, pitch=0, size=0.55)
    cheeks(mb, head_c, R)
    cone(mb, frame(on_sphere(head_c, R, 0, -14, -0.2), 0, 90, 0), (0, 0, 0), 0.45, 0.9, (0.30, 0.30, 0.34), 10)
    for s in (-1, 1):   # 어깨 흰 무늬 + 청록 날개 끝
        ellipsoid(mb, frame((s * 1.9, y0 + 2.8, -0.1), 0, 0, s * 10), (0, 0, 0), (0.55, 1.6, 1.25), black)
        ellipsoid(mb, frame((s * 2.05, y0 + 3.2, 0.3), 0, 0, s * 10), (0, 0, 0), (0.4, 0.6, 0.7), white, 10, 6)
        ellipsoid(mb, frame((s * 2.0, y0 + 1.6, -0.5), 0, 0, s * 14), (0, 0, 0), (0.45, 0.7, 0.9), teal, 10, 6)
    # 벨보이 모자 (크림슨 띠) + 편지 봉투
    cap = frame((0, head_c[1] + 1.95, head_c[2] - 0.1), 0, -8, 6)
    cylinder(mb, cap, (0, 0, 0), 1.05, 0.9, (0.70, 0.14, 0.20), 20)
    cylinder(mb, cap, (0, 0.05, 0), 1.08, 0.28, (0.15, 0.13, 0.15), 20)
    mb.box(cap, (0, 0.05, 1.0), (1.4, 0.1, 0.6), (0.15, 0.13, 0.15))
    sphere(mb, cap, (0, 0.92, 0), 0.16, GOLD, 6, 4)
    mb.box(frame((1.2, y0 + 3.4, 1.9), 0, -10, -10), (0, 0, 0), (1.5, 1.0, 0.1), WHITE)
    sphere(mb, IDENT, (1.2, y0 + 3.4, 1.98), 0.18, CRIMSON, 6, 4)


def lion_cub(mb, rng):
    fur, mane, white, red = (0.98, 0.78, 0.40), (0.88, 0.56, 0.24), (0.99, 0.98, 0.96), (0.86, 0.20, 0.22)
    gold_base(mb, rng)
    y0 = BASE_TOP
    for k in range(2):   # 돌계단 한 칸
        mb.box(IDENT, (-2.6, y0 + 0.25 + k * 0.4, -1.8 - k * 0.6), (2.2, 0.5, 1.2), (0.92, 0.90, 0.85))
    head_c, R = biped(mb, white, None, white)
    for s in (-1, 1):   # 트레이닝복 빨간 줄
        capsule(mb, (s * 1.0, y0 + 0.5, 0.6), (s * 1.0, y0 + 1.5, 0.6), 0.1, red, 6, 3)
        capsule(mb, (s * 1.62, y0 + 2.0, 0.4), (s * 1.62, y0 + 3.3, 0.3), 0.1, red, 6, 3)
    sphere(mb, IDENT, (0.3, y0 + 3.9, 1.55), 0.14, CRIMSON, 6, 4)
    for k in range(18):   # 갈기
        a = 2 * math.pi * k / 18
        sphere(mb, IDENT, (math.cos(a) * 2.1, head_c[1] + math.sin(a) * 2.1, head_c[2] - 0.5), 0.75, mane, 10, 6)
    sphere(mb, IDENT, (head_c[0], head_c[1], head_c[2] + 0.1), R * 0.98, fur, 24, 14)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.8, head_c[2] + 1.55), (1.0, 0.75, 0.7), (1.0, 0.95, 0.85), 14, 8)
    eyes(mb, head_c, R, spread=29, pitch=0, size=0.52)
    cheeks(mb, head_c, R)
    sphere(mb, IDENT, on_sphere(head_c, R, 0, -12, 0.1), 0.25, (0.55, 0.32, 0.24), 8, 5)
    for s in (-1, 1):
        ellipsoid(mb, frame((s * 1.55, head_c[1] + 1.55, head_c[2]), 0, 0, -s * 20), (0, 0, 0), (0.55, 0.5, 0.3), fur, 10, 6)
    mb.prism(IDENT, (0, head_c[1] + 1.15, head_c[2]), 1.98, 0.38, 28, white, caps=False, r_top=1.86)   # 땀밴드
    capsule(mb, (0, y0 + 1.6, -1.4), (0.6, y0 + 3.0, -2.2), 0.2, fur)                        # 꼬리 + 끝 털
    sphere(mb, IDENT, (0.6, y0 + 3.1, -2.2), 0.42, mane, 8, 5)
    arms(mb, white, (-1.9, y0 + 2.2, 0.8), (0.9, y0 + 4.8, 1.4))                              # 호루라기 부는 팔
    mb.prism(frame(on_sphere(head_c, R, 6, -28, 0.3), 0, 90, 0), (0, 0, 0), 0.22, 0.6, 10, red, caps=True)
    sphere(mb, IDENT, (-2.0, y0 + 4.6, 0.6), 0.45, fur, 8, 5)                                 # 치켜든 주먹 대신 아래팔


def clock_rooster(mb, rng):
    white, red, russet, vest = (0.99, 0.98, 0.96), (0.90, 0.20, 0.18), (0.70, 0.30, 0.18), (0.97, 0.92, 0.80)
    gold_base(mb, rng)
    y0 = BASE_TOP
    cylinder(mb, IDENT, (-2.5, y0 - 0.05, 1.8), 1.0, 0.12, (0.98, 0.96, 0.90), 20)            # 시계판
    ring(mb, IDENT, (-2.5, y0 + 0.08, 1.8), 1.0, 0.08, GOLD, 20)
    mb.box(IDENT, (-2.5, y0 + 0.1, 2.1), (0.08, 0.04, 0.6), EYE)
    for s in (-1, 1):
        capsule(mb, (s * 0.7, y0 + 1.4, 0.1), (s * 0.75, y0 + 0.2, 0.3), 0.2, (1.0, 0.75, 0.35))
        ellipsoid(mb, IDENT, (s * 0.75, y0 + 0.15, 0.6), (0.35, 0.12, 0.55), (1.0, 0.75, 0.35), 8, 4)
    ellipsoid(mb, IDENT, (0, y0 + 2.6, 0), (1.85, 1.95, 1.7), white)
    for k in range(5):   # 화려한 꼬리깃
        ellipsoid(mb, frame((0, y0 + 3.0, -1.6), (k - 2) * 18, -35 - k * 4, 0), (0, 1.4, 0), (0.45, 1.6, 0.35), russet if k % 2 else (0.25, 0.30, 0.25), 12, 6)
    mb.prism(IDENT, (0, y0 + 1.9, 0), 1.9, 1.5, 24, vest, caps=False, r_top=1.75)            # 조끼
    sphere(mb, IDENT, (0, y0 + 3.2, 1.72), 0.16, CRIMSON, 6, 4)
    head_c, R = (0, y0 + 5.3, 0.2), 2.2
    sphere(mb, IDENT, head_c, R, white, 24, 14)
    for k in range(3):   # 볏
        sphere(mb, IDENT, (0, head_c[1] + R + 0.1 + (0.25 if k == 1 else 0), head_c[2] + 0.6 - k * 0.6), 0.55, red, 10, 6)
    ellipsoid(mb, IDENT, on_sphere(head_c, R, 0, -32, 0.2), (0.35, 0.6, 0.3), red, 10, 6)     # 턱볏
    cone(mb, frame(on_sphere(head_c, R, 0, -14, -0.2), 0, 90, 0), (0, 0, 0), 0.45, 0.8, (1.0, 0.75, 0.35), 10)
    eyes(mb, head_c, R, spread=28, pitch=4, size=0.55)
    cheeks(mb, head_c, R)
    for s in (-1, 1):
        ellipsoid(mb, frame((s * 1.95, y0 + 2.8, 0), 0, 0, s * 12), (0, 0, 0), (0.5, 1.5, 1.2), russet)
    w = frame((1.9, y0 + 3.6, 1.6), 0, -70, 0)                                                 # 금색 회중시계
    cylinder(mb, w, (0, 0, 0), 0.65, 0.18, GOLD, 20)
    cylinder(mb, w, (0, 0.18, 0), 0.52, 0.03, (0.99, 0.97, 0.90), 20)


def art_chameleon(mb, rng):
    pastels = [(0.62, 0.90, 0.78), (0.70, 0.82, 1.0), (0.92, 0.76, 1.0), (1.0, 0.80, 0.86), (1.0, 0.92, 0.66)]
    gold_base(mb, rng)
    y0 = BASE_TOP
    for k, c in enumerate(((0.95, 0.30, 0.30), (0.30, 0.60, 0.95), (0.98, 0.82, 0.25), (0.45, 0.80, 0.35), (0.70, 0.40, 0.90))):
        a = k * 1.25 + 0.4
        cylinder(mb, IDENT, (math.cos(a) * 3.2, y0 - 0.06, math.sin(a) * 3.2), 0.38, 0.1, c, 12)    # 물감 자국
    head_c, R = biped(mb, pastels[0], pastels[4], pastels[1])
    for k, c in enumerate(pastels):   # 무지개 그라데이션 띠
        ellipsoid(mb, IDENT, (0, y0 + 1.4 + k * 0.6, 0), (1.78, 0.35, 1.58), c, 18, 6)
    sphere(mb, IDENT, head_c, R, pastels[1], 24, 14)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.5, head_c[2] + 0.4), (2.05, 1.6, 1.9), pastels[0], 20, 10)
    for s in (-1, 1):   # 볼록 튀어나온 눈 (포탑형)
        p = on_sphere(head_c, R, s * 38, 6, -0.3)
        sphere(mb, IDENT, p, 0.85, pastels[3], 14, 8)
    eyes(mb, head_c, R + 0.5, spread=38, pitch=6, size=0.5)
    cheeks(mb, head_c, R, spread=50, pitch=-26)
    mouth(mb, head_c, R, pitch=-24, w=0.4, open_=True)
    tail = [(0, y0 + 1.3, -1.4), (0, y0 + 0.9, -2.4), (0, y0 + 1.4, -3.1), (0, y0 + 2.0, -2.8), (0, y0 + 1.9, -2.3)]
    for a, b in zip(tail, tail[1:]):
        capsule(mb, a, b, 0.32, pastels[2], 10, 5)
    beret = frame((0.3, head_c[1] + 2.0, head_c[2] - 0.1), 0, -6, -14)
    ellipsoid(mb, beret, (0, 0, 0), (1.6, 0.42, 1.6), (0.75, 0.14, 0.22), 18, 8)
    sphere(mb, beret, (0, 0.45, 0), 0.15, (0.75, 0.14, 0.22), 6, 4)
    arms(mb, pastels[1], (-1.9, y0 + 3.0, 1.0), (2.2, y0 + 4.3, 0.9))
    pal = frame((-2.2, y0 + 3.1, 1.3), 0, -70, 10)                                             # 팔레트
    cylinder(mb, pal, (0, 0, 0), 0.95, 0.1, (0.92, 0.80, 0.62), 18)
    for k, c in enumerate(((0.95, 0.30, 0.30), (0.30, 0.60, 0.95), (0.98, 0.82, 0.25), (0.45, 0.80, 0.35))):
        sphere(mb, pal, (math.cos(k * 1.4) * 0.55, 0.1, math.sin(k * 1.4) * 0.55), 0.18, c, 6, 4)
    capsule(mb, (2.3, y0 + 4.2, 0.9), (2.9, y0 + 6.0, 1.3), 0.1, (0.62, 0.42, 0.26), 6, 3)  # 붓
    sphere(mb, IDENT, (2.95, y0 + 6.15, 1.33), 0.2, (0.70, 0.40, 0.90), 6, 4)


def ceramics_mole(mb, rng):
    velvet, pink, apron = (0.38, 0.37, 0.40), (0.98, 0.66, 0.72), (0.93, 0.86, 0.72)
    gold_base(mb, rng)
    y0 = BASE_TOP
    mb.prism(IDENT, (2.8, y0, 1.5), 0.7, 0.9, 16, (0.55, 0.38, 0.24), caps=True)              # 물레
    mb.prism(IDENT, (2.8, y0 + 0.9, 1.5), 1.0, 0.14, 18, (0.70, 0.52, 0.34), caps=True)
    head_c, R = biped(mb, velvet, None, pink, head_r=2.2, head_y=5.0)
    mb.prism(IDENT, (0, y0 + 1.0, 0.15), 1.65, 2.6, 24, apron, caps=False, r_top=1.45)        # 앞치마
    sphere(mb, IDENT, (0.6, y0 + 2.4, 1.6), 0.12, CRIMSON, 6, 4)
    for k in range(4):
        sphere(mb, IDENT, (-0.8 + 0.5 * k, y0 + 1.6 + 0.3 * (k % 2), 1.62), 0.16, (0.70, 0.52, 0.36), 6, 4)   # 흙 자국
    for s in (-1, 1):   # 꼭 감은 실눈
        p = on_sphere(head_c, R, s * 26, 0, -0.05)
        ellipsoid(mb, frame(p, s * 26, 0, 0), (0, 0, 0), (0.4, 0.09, 0.08), EYE, 8, 4)
    cheeks(mb, head_c, R)
    nose = on_sphere(head_c, R, 0, -14, 0.25)                                                   # 분홍 별코
    for k in range(5):
        a = math.pi / 2 + k * 2 * math.pi / 5
        sphere(mb, IDENT, (nose[0] + math.cos(a) * 0.35, nose[1] + math.sin(a) * 0.35, nose[2]), 0.25, pink, 8, 5)
    sphere(mb, IDENT, nose, 0.28, pink, 8, 5)
    for s in (-1, 1):   # 큰 분홍 손 + 항아리
        capsule(mb, (s * 1.5, y0 + 3.0, 0.2), (s * 0.7, y0 + 3.4, 1.6), 0.42, velvet)
        ellipsoid(mb, IDENT, (s * 0.75, y0 + 3.45, 1.7), (0.55, 0.4, 0.45), pink, 10, 6)
    ellipsoid(mb, frame((0.1, y0 + 4.0, 2.0), 0, 0, 12), (0, 0, 0), (0.75, 0.8, 0.7), (0.80, 0.52, 0.34), 14, 8)   # 삐뚤어진 항아리
    cylinder(mb, frame((0.2, y0 + 4.7, 2.0), 0, 0, 12), (0, 0, 0), 0.45, 0.3, (0.74, 0.47, 0.30), 14)


def observatory_squirrel(mb, rng):
    fur, belly, navy = (0.62, 0.56, 0.52), (0.99, 0.97, 0.94), (0.16, 0.20, 0.42)
    gold_base(mb, rng)
    y0 = BASE_TOP
    head_c, R = biped(mb, fur, belly, fur, head_r=2.3)
    ellipsoid(mb, frame((0, y0 + 1.5, -1.8), 0, -60, 0), (0, 0, 0), (1.3, 0.4, 1.6), fur)     # 납작 꼬리
    eyes(mb, head_c, R, spread=27, pitch=0, size=0.68)                                        # 아주 큰 눈
    cheeks(mb, head_c, R)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.8, head_c[2] + 1.5), (1.0, 0.7, 0.7), belly, 12, 6)
    sphere(mb, IDENT, on_sphere(head_c, R, 0, -10, 0.12), 0.2, (0.95, 0.60, 0.68), 6, 4)
    for s in (-1, 1):
        ellipsoid(mb, frame((s * 1.5, head_c[1] + 1.6, head_c[2] - 0.2), 0, 0, -s * 22), (0, 0, 0), (0.6, 0.8, 0.3), fur, 10, 6)
    # 별무늬 망토 (활공막처럼 양옆으로 펼침)
    for s in (-1, 1):
        cape = frame((s * 1.8, y0 + 2.8, -0.3), s * 20, 0, s * 25)
        ellipsoid(mb, cape, (0, 0, 0), (1.5, 1.9, 0.25), navy, 16, 8)
        for k in range(4):
            sphere(mb, cape, ((k % 2 - 0.5) * 1.2, (k // 2 - 0.5) * 1.6, 0.25), 0.13, GOLD, 6, 4)
    ellipsoid(mb, IDENT, (0, y0 + 3.9, -0.2), (1.75, 0.5, 1.5), navy, 16, 6)
    sphere(mb, IDENT, (0, y0 + 3.95, 1.3), 0.18, CRIMSON, 6, 4)
    arms(mb, fur, (0.6, y0 + 4.2, 1.6), (1.7, y0 + 4.6, 1.5))
    tel = along((0.4, y0 + 4.3, 1.8), (2.6, y0 + 6.6, 3.0))                                     # 놋쇠 망원경
    cylinder(mb, tel, (0, 0, 0), 0.28, 1.6, (0.82, 0.62, 0.30), 14)
    cylinder(mb, tel, (0, 1.6, 0), 0.36, 1.1, (0.82, 0.62, 0.30), 14)
    ring(mb, tel, (0, 1.6, 0), 0.38, 0.08, GOLD_L, 14)


def amphitheater_frog(mb, rng):
    green, belly = (0.45, 0.78, 0.30), (0.98, 0.95, 0.72)
    gold_base(mb, rng)
    y0 = BASE_TOP
    cylinder(mb, IDENT, (0, y0 - 0.06, 0), 3.0, 0.12, (0.38, 0.66, 0.26), 24)                # 연잎 무대
    head_c, R = biped(mb, green, belly, green, body_r=(1.9, 1.85, 1.65), head_r=2.35, head_y=5.0)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.4, head_c[2] + 0.5), (2.25, 1.6, 1.9), belly, 18, 10)
    for s in (-1, 1):   # 머리 위 큰 눈 언덕
        sphere(mb, IDENT, (s * 1.05, head_c[1] + 1.75, head_c[2] + 0.4), 0.95, green, 16, 10)
        p = (s * 1.05, head_c[1] + 1.8, head_c[2] + 1.15)
        ellipsoid(mb, IDENT, p, (0.55, 0.6, 0.25), EYE, 12, 6)
        sphere(mb, IDENT, (p[0] - 0.18, p[1] + 0.22, p[2] + 0.2), 0.16, EYE_SHINE, 6, 4)
    cheeks(mb, head_c, R, spread=52, pitch=-14)
    mouth(mb, head_c, R, pitch=-12, w=0.75, open_=True)
    bow = (0, y0 + 3.75, 1.45)                                                                  # 나비넥타이
    for s in (-1, 1):
        cone(mb, frame(bow, 0, 0, s * 90), (0, 0, 0), 0.38, 0.7, CRIMSON, 8)
    sphere(mb, IDENT, bow, 0.2, CRIMSON, 6, 4)
    arms(mb, green, (0.8, y0 + 4.0, 1.7), (2.6, y0 + 4.4, 0.8))
    mb.prism(IDENT, (-2.4, y0, 1.5), 0.08, 3.6, 8, SILVER, caps=True)                         # 스탠드 마이크
    mb.prism(IDENT, (-2.4, y0, 1.5), 0.55, 0.12, 12, SILVER, caps=True)
    capsule(mb, (-2.4, y0 + 3.6, 1.5), (-1.1, y0 + 4.1, 1.8), 0.07, SILVER, 6, 3)
    ellipsoid(mb, IDENT, (0.7, y0 + 4.3, 1.9), (0.32, 0.45, 0.32), (0.30, 0.30, 0.32), 10, 6)


def stadium_jindo(mb, rng):
    white, red = (0.99, 0.98, 0.95), (0.86, 0.20, 0.22)
    gold_base(mb, rng)
    y0 = BASE_TOP
    mb.box(IDENT, (0, y0 - 0.02, 2.6), (6.0, 0.08, 1.4), (0.84, 0.40, 0.32))                  # 붉은 트랙 조각
    for k in (-1, 1):
        mb.box(IDENT, (0, y0 + 0.03, 2.6 + k * 0.45), (6.0, 0.03, 0.08), WHITE)
    head_c, R = biped(mb, white, None, white)
    for s in (-1, 1):   # 빨간 운동화
        ellipsoid(mb, IDENT, (s * 0.9, y0 + 0.3, 0.55), (0.66, 0.36, 0.82), red, 12, 6)
        ellipsoid(mb, IDENT, (s * 0.9, y0 + 0.08, 0.55), (0.7, 0.1, 0.86), WHITE, 12, 4)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.8, head_c[2] + 1.55), (1.15, 0.85, 0.85), white, 14, 8)
    eyes(mb, head_c, R, spread=29, pitch=0, size=0.52)
    cheeks(mb, head_c, R)
    sphere(mb, IDENT, on_sphere(head_c, R, 0, -12, 0.22), 0.3, EYE, 8, 5)
    mouth(mb, head_c, R, pitch=-28, w=0.36, open_=True)
    for s in (-1, 1):   # 진돗개: 짧고 뾰족하게 선 삼각 귀 (토끼 귀 아님)
        ear = frame((s * 1.25, head_c[1] + 1.7, head_c[2] - 0.2), 0, 0, -s * 14)
        cone(mb, ear, (0, 0, 0), 0.75, 1.35, white, 4)
        cone(mb, frame((s * 1.27, head_c[1] + 1.72, head_c[2] + 0.02), 0, 0, -s * 14), (0, 0, 0), 0.42, 0.9, (0.98, 0.76, 0.78), 4)
    mb.prism(IDENT, (0, head_c[1] + 0.9, head_c[2]), R, 0.45, 28, red, caps=False, r_top=R * 0.96)   # 머리띠
    tail = [(0, y0 + 1.8, -1.5), (0, y0 + 3.0, -2.2), (0, y0 + 3.6, -1.6)]                      # 말린 꼬리
    for a, b in zip(tail, tail[1:]):
        capsule(mb, a, b, 0.42, white, 10, 5)
    arms(mb, white, (-1.9, y0 + 2.5, -0.6), (2.0, y0 + 3.3, 1.3))
    mb.prism(along((1.8, y0 + 2.6, 1.0), (2.4, y0 + 4.2, 1.8)), (0, 0, 0), 0.22, 1.8, 10, red, caps=True)   # 바통


def language_parrot(mb, rng):
    green, face, blue, beak = (0.40, 0.76, 0.30), (0.98, 0.42, 0.24), (0.25, 0.50, 0.92), (0.98, 0.84, 0.50)
    gold_base(mb, rng)
    y0 = BASE_TOP
    ellipsoid(mb, IDENT, (-2.8, y0 + 0.45, 1.5), (0.9, 0.55, 0.5), (0.90, 0.88, 0.84), 12, 6)   # 말풍선 돌
    cone(mb, frame((-2.4, y0 + 0.1, 1.6), 0, 0, 40), (0, 0, 0), 0.25, 0.5, (0.90, 0.88, 0.84), 6)
    head_c, R = biped(mb, green, None, (0.55, 0.55, 0.58), body_r=(1.8, 1.95, 1.65))
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.2, head_c[2] + 0.8), (1.9, 1.7, 1.5), face, 18, 10)
    eyes(mb, head_c, R, spread=27, pitch=2, size=0.52)
    cheeks(mb, head_c, R)
    ellipsoid(mb, frame(on_sphere(head_c, R, 0, -14, 0.1), 0, 30, 0), (0, 0, 0), (0.42, 0.6, 0.45), beak, 10, 6)
    for s in (-1, 1):   # 파란 날개 끝 + 꼬리
        ellipsoid(mb, frame((s * 1.95, y0 + 2.8, -0.1), 0, 0, s * 12), (0, 0, 0), (0.55, 1.6, 1.25), green)
        ellipsoid(mb, frame((s * 2.05, y0 + 1.7, -0.4), 0, 0, s * 16), (0, 0, 0), (0.45, 0.8, 0.9), blue, 10, 6)
        ring_c = (s * (R + 0.05), head_c[1] + 0.2, head_c[2])
        ellipsoid(mb, IDENT, ring_c, (0.35, 0.75, 0.75), (0.86, 0.18, 0.22), 12, 8)            # 헤드폰
    mb.prism(frame((0, head_c[1], head_c[2]), 0, 0, 0), (0, R - 0.15, 0), 0.3, 0.3, 8, (0.86, 0.18, 0.22), caps=True)
    ring(mb, IDENT, (0, head_c[1] + 0.15, head_c[2]), R + 0.12, 0.13, (0.86, 0.18, 0.22), 26, axis='z')
    ellipsoid(mb, IDENT, (0, y0 + 1.5, -2.2), (0.6, 0.3, 1.2), blue, 10, 6)
    mb.prism(IDENT, (2.8, y0, 1.4), 0.08, 1.4, 8, (0.62, 0.42, 0.26), caps=True)              # 지구본
    sphere(mb, IDENT, (2.8, y0 + 2.2, 1.4), 0.85, (0.35, 0.62, 0.92), 16, 10)
    for k in range(4):
        sphere(mb, IDENT, on_sphere((2.8, y0 + 2.2, 1.4), 0.85, k * 85 + 20, (k % 2) * 30 - 10, -0.25), 0.38, (0.45, 0.78, 0.35), 8, 5)
    capsule(mb, (1.5, y0 + 3.3, 0.2), (2.3, y0 + 2.6, 1.2), 0.42, green)
    capsule(mb, (-1.5, y0 + 3.3, 0.2), (-2.2, y0 + 4.4, 0.5), 0.42, green)


def dorm_hamster(mb, rng):
    gold_f, white, pj, pj_s = (0.98, 0.80, 0.50), (0.99, 0.97, 0.94), (0.99, 0.84, 0.86), (0.95, 0.62, 0.66)
    gold_base(mb, rng)
    y0 = BASE_TOP
    mb.box(frame((-2.7, y0 + 0.2, 1.5), 20, 0, 0), (0, 0, 0), (1.8, 0.4, 1.2), pj_s)          # 접힌 이불
    mb.box(frame((-2.7, y0 + 0.45, 1.5), 20, 0, 0), (0, 0, 0), (1.7, 0.12, 1.1), WHITE)
    head_c, R = biped(mb, pj, None, white, body_r=(1.85, 1.9, 1.6), head_r=2.35)
    for k in range(5):   # 잠옷 세로 줄무늬
        a = (k - 2) * 26
        p = on_sphere((0, y0 + 2.5, 0), 1.0, a, 0)
        capsule(mb, (p[0] * 1.85, y0 + 1.1, p[2] * 1.6), (p[0] * 1.85, y0 + 3.9, p[2] * 1.6), 0.12, pj_s, 6, 3)
    sphere(mb, IDENT, head_c, R, gold_f, 24, 14)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.5, head_c[2] + 0.7), (2.1, 1.5, 1.7), white, 18, 10)
    for s in (-1, 1):   # 빵빵한 볼
        sphere(mb, IDENT, (s * 1.45, head_c[1] - 0.75, head_c[2] + 0.9), 1.05, white, 14, 8)
    eyes(mb, head_c, R, spread=26, pitch=4, size=0.48, lid=gold_f)
    cheeks(mb, head_c, R, spread=46, pitch=-20)
    mouth(mb, head_c, R, pitch=-26, w=0.4, open_=True)                                        # 하품
    for s in (-1, 1):
        ellipsoid(mb, frame((s * 1.55, head_c[1] + 1.6, head_c[2] - 0.2), 0, 0, -s * 25), (0, 0, 0), (0.5, 0.5, 0.25), gold_f, 10, 6)
    cap = frame((0.3, head_c[1] + 1.6, head_c[2] - 0.2), 0, -15, -25)                           # 수면 모자
    cone(mb, cap, (0, 0, 0), 1.8, 2.8, pj, 18)
    mb.prism(cap, (0, -0.15, 0), 1.85, 0.5, 18, WHITE, caps=True)
    sphere(mb, cap, (0, 2.9, 0), 0.42, CRIMSON, 10, 6)
    arms(mb, pj, (-0.7, y0 + 3.0, 1.7), (0.8, y0 + 3.0, 1.7))
    ellipsoid(mb, IDENT, (0, y0 + 2.6, 2.0), (1.5, 0.9, 0.55), WHITE, 14, 8)                  # 베개


def electronics_hedgehog(mb, rng):
    spine, face, red = (0.56, 0.38, 0.24), (0.99, 0.94, 0.86), (0.86, 0.18, 0.22)
    gold_base(mb, rng)
    y0 = BASE_TOP
    mb.box(IDENT, (2.8, y0 + 0.1, 1.6), (0.8, 0.2, 0.8), (0.18, 0.20, 0.22))                  # 마이크로칩
    for k in range(4):
        mb.box(IDENT, (2.4 + k * 0.27, y0 + 0.05, 2.08), (0.08, 0.06, 0.2), SILVER)
    head_c, R = biped(mb, face, face, face)
    # 가시: 머리·등을 덮는 원뿔, 끝마다 따뜻한 LED (밤에 빛남)
    sc = (0, y0 + 4.2, -0.6)
    for i in range(7):
        for j in range(12):
            yaw = -150 + j * 300 / 11 + (i % 2) * 12
            pitch = -40 + i * 18
            if abs(((yaw + 180) % 360) - 180) < 52 and pitch < 40:
                continue          # 얼굴 앞은 비움
            p = on_sphere(sc, 2.5, 180 + yaw, pitch)
            f = along(sc, p)
            d = math.dist(sc, p)
            cone(mb, f, (0, d - 0.3, 0), 0.5, 1.4, spine, 6)
            mb.emissive = True
            sphere(mb, f, (0, d + 1.15, 0), 0.16, (1.0, 0.86, 0.45), 6, 4)
            mb.emissive = False
    eyes(mb, head_c, R, spread=27, pitch=-4, size=0.52)
    cheeks(mb, head_c, R)
    sphere(mb, IDENT, on_sphere(head_c, R, 0, -12, 0.2), 0.24, EYE, 8, 5)
    for s in (-1, 1):   # 헤드셋 (크림슨 귀 쿠션)
        ellipsoid(mb, IDENT, (s * (R + 0.05), head_c[1] + 0.1, head_c[2]), (0.32, 0.7, 0.7), red, 12, 8)
    ring(mb, IDENT, (0, head_c[1] + 0.1, head_c[2]), R + 0.12, 0.12, (0.94, 0.94, 0.96), 26, axis='z')
    capsule(mb, (-R - 0.1, head_c[1] - 0.2, head_c[2] + 0.4), (-0.6, head_c[1] - 1.3, head_c[2] + 2.0), 0.08, (0.30, 0.30, 0.32), 6, 3)
    sphere(mb, IDENT, (-0.6, head_c[1] - 1.3, head_c[2] + 2.0), 0.18, (0.30, 0.30, 0.32), 6, 4)
    arms(mb, face, (0.5, y0 + 3.6, 1.7), (1.8, y0 + 3.8, 1.4))
    board = frame((1.2, y0 + 4.1, 1.9), 0, -60, -10)                                           # 회로기판
    mb.box(board, (0, 0, 0), (1.5, 0.12, 1.1), (0.25, 0.58, 0.32))
    for k in range(3):
        mb.box(board, (-0.4 + 0.4 * k, 0.1, 0.1), (0.25, 0.1, 0.3), (0.15, 0.15, 0.18))


def business_fox(mb, rng):
    orange, white, vest = (0.98, 0.56, 0.24), (0.99, 0.97, 0.94), (0.42, 0.44, 0.48)
    gold_base(mb, rng)
    y0 = BASE_TOP
    for k in range(3):
        mb.box(frame((-2.8, y0 + 0.08 + k * 0.1, 1.5), k * 9, 0, 0), (0, 0, 0), (1.2, 0.08, 1.5), WHITE)   # 서류 더미
    head_c, R = biped(mb, orange, white, (0.30, 0.22, 0.20))
    mb.prism(IDENT, (0, y0 + 1.6, 0.05), 1.82, 1.9, 24, vest, caps=False, r_top=1.65)         # 조끼
    capsule(mb, (0, y0 + 3.6, 1.62), (0, y0 + 2.2, 1.75), 0.2, CRIMSON, 8, 4)                 # 넥타이
    for k in range(2):
        sphere(mb, IDENT, (0.6, y0 + 2.2 + k * 0.6, 1.65), 0.1, GOLD, 6, 4)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.6, head_c[2] + 1.0), (1.6, 1.1, 1.3), white, 16, 8)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.75, head_c[2] + 1.9), (0.6, 0.45, 0.6), white, 10, 6)
    eyes(mb, head_c, R, spread=29, pitch=2, size=0.5)
    cheeks(mb, head_c, R)
    sphere(mb, IDENT, on_sphere(head_c, R, 0, -16, 0.6), 0.22, EYE, 8, 5)
    for s in (-1, 1):   # 큰 세모 귀
        ear = frame((s * 1.35, head_c[1] + 1.55, head_c[2] - 0.2), 0, 0, -s * 16)
        cone(mb, ear, (0, 0, 0), 0.95, 1.9, orange, 4)
        cone(mb, frame((s * 1.37, head_c[1] + 1.6, head_c[2] + 0.1), 0, 0, -s * 16), (0, 0, 0), 0.5, 1.2, (0.30, 0.22, 0.20), 4)
    for k, (a, b) in enumerate((((0, y0 + 1.6, -1.4), (0.6, y0 + 2.8, -2.6)), ((0.6, y0 + 2.8, -2.6), (0.4, y0 + 4.2, -2.6)))):
        capsule(mb, a, b, 0.75 - 0.1 * k, orange, 12, 6)                                        # 큰 꼬리
    sphere(mb, IDENT, (0.4, y0 + 4.5, -2.6), 0.6, white, 10, 6)
    capsule(mb, (1.5, y0 + 3.3, 0.2), (2.4, y0 + 4.4, 1.4), 0.42, orange)                      # 앞을 가리키는 손
    capsule(mb, (-1.5, y0 + 3.3, 0.2), (-2.0, y0 + 2.1, 0.6), 0.42, orange)
    mb.box(IDENT, (-2.2, y0 + 1.3, 0.7), (0.4, 1.2, 1.5), (0.52, 0.32, 0.20))                # 서류가방
    mb.box(IDENT, (-2.2, y0 + 2.05, 0.7), (0.15, 0.25, 0.6), (0.30, 0.20, 0.14))


def multimedia_meerkat(mb, rng):
    sand, sand_d, patch = (0.90, 0.78, 0.60), (0.72, 0.58, 0.42), (0.36, 0.28, 0.24)
    gold_base(mb, rng)
    y0 = BASE_TOP
    for k in range(3):   # 작은 삼각대
        a = k * 2 * math.pi / 3
        capsule(mb, (2.9, y0 + 1.6, 1.5), (2.9 + math.cos(a) * 0.6, y0 + 0.05, 1.5 + math.sin(a) * 0.6), 0.06, (0.25, 0.25, 0.28), 6, 3)
    mb.box(IDENT, (2.9, y0 + 1.75, 1.5), (0.4, 0.3, 0.3), (0.25, 0.25, 0.28))
    head_c, R = biped(mb, sand, (0.98, 0.92, 0.80), sand_d, body_r=(1.55, 2.05, 1.4), head_r=2.15, head_y=5.6)
    for s in (-1, 1):   # 눈가 짙은 무늬
        p = on_sphere(head_c, R, s * 28, 0, -0.15)
        ellipsoid(mb, frame(p, s * 28, 0, 0), (0, 0, 0), (0.75, 0.62, 0.2), patch, 12, 6)
    eyes(mb, head_c, R, spread=28, pitch=0, size=0.5)
    cheeks(mb, head_c, R)
    ellipsoid(mb, IDENT, (0, head_c[1] - 0.7, head_c[2] + 1.45), (0.9, 0.7, 0.75), (0.98, 0.92, 0.80), 12, 6)
    sphere(mb, IDENT, on_sphere(head_c, R, 0, -14, 0.35), 0.2, EYE, 8, 5)
    for s in (-1, 1):
        ellipsoid(mb, frame((s * 1.65, head_c[1] + 0.6, head_c[2] - 0.2), 0, 0, s * 20), (0, 0, 0), (0.3, 0.4, 0.25), patch, 8, 5)
    capsule(mb, (0, y0 + 1.2, -1.2), (0.3, y0 + 0.3, -2.8), 0.3, sand_d, 10, 5)               # 가는 꼬리
    beanie = frame((0, head_c[1] + 1.05, head_c[2] - 0.05), 0, -6, 0)                          # 크림슨 줄 비니
    ellipsoid(mb, beanie, (0, 0.2, 0), (2.05, 1.35, 2.0), (0.96, 0.93, 0.88), 18, 10)
    mb.prism(beanie, (0, -0.2, 0), 2.1, 0.5, 26, (0.96, 0.93, 0.88), caps=False)
    mb.prism(beanie, (0, 0.45, 0), 2.0, 0.32, 26, CRIMSON, caps=False, r_top=1.9)
    arms(mb, sand, (-0.8, y0 + 3.9, 1.6), (0.8, y0 + 3.9, 1.6))
    mb.box(IDENT, (0, y0 + 4.0, 1.85), (1.6, 1.0, 0.6), (0.20, 0.20, 0.22))                  # 카메라
    mb.box(IDENT, (0, y0 + 4.55, 1.85), (1.0, 0.25, 0.4), SILVER)
    mb.prism(frame((0, y0 + 4.0, 2.1), 0, 90, 0), (0, 0, 0), 0.35, 0.45, 14, (0.30, 0.30, 0.34), caps=True)


CREATURES.update({
    'space_rabbit': ('공학관 토끼', 'gold', space_rabbit),
    'library_owl': ('중앙도서관 올빼미', 'gold', library_owl),
    'plaza_duck': ('사색의 광장 오리', 'gold', plaza_duck),
    'gate_magpie': ('정문 까치', 'gold', gate_magpie),
    'lion_cub': ('체육대학관 아기 사자', 'gold', lion_cub),
    'clock_rooster': ('선승관 수탉', 'gold', clock_rooster),
    'art_chameleon': ('예술·디자인대학 카멜레온', 'gold', art_chameleon),
    'ceramics_mole': ('도예관 두더지', 'gold', ceramics_mole),
    'observatory_squirrel': ('천문대 날다람쥐', 'gold', observatory_squirrel),
    'amphitheater_frog': ('평화노천극장 개구리', 'gold', amphitheater_frog),
    'stadium_jindo': ('대운동장 진돗개', 'gold', stadium_jindo),
    'language_parrot': ('외국어대학관 앵무새', 'gold', language_parrot),
    'dorm_hamster': ('우정원 햄스터', 'gold', dorm_hamster),
    'electronics_hedgehog': ('전자정보대학관 고슴도치', 'gold', electronics_hedgehog),
    'business_fox': ('국제·경영대학관 여우', 'gold', business_fox),
    'multimedia_meerkat': ('멀티미디어·글로벌관 미어캣', 'gold', multimedia_meerkat),
})
