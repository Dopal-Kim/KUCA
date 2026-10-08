"""너구리 (파랑 희귀): 회갈색 털, 눈가 짙은 갈색 가면 + 흰 볼·주둥이, 둥근 귀, 복슬한 꼬리 (고리 무늬 없음),
파란 배낭 (빨간 단추) + 곰돌이 과자 봉지를 든 오른손. 받침: 파란 테 + 데이지."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'blue'

FUR = (0.66, 0.52, 0.42)
FUR_LT = (0.72, 0.63, 0.55)
FUR_DK = (0.34, 0.22, 0.16)
CREAM = (0.98, 0.94, 0.88)
EAR_IN = (0.99, 0.82, 0.76)
NOSE = (0.22, 0.14, 0.12)
BLUE = (0.34, 0.63, 0.96)
BLUE_DK = (0.27, 0.53, 0.88)
RED = (0.86, 0.16, 0.18)
COOKIE = (0.93, 0.78, 0.48)
CRIMSON = (0.72, 0.10, 0.14)
SILVER = (0.80, 0.82, 0.86)
SIL = (SILVER, (1.0, 1.0, 1.0))
GLD = (Fg.GOLD, Fg.GOLD_HI)


# ---------- 공통 도우미 ----------

_GRID = []


def bounded(f, margin=1.0):
    """빠르게: 성긴 격자로 부품의 경계 상자를 찾아, 상자에서 margin 안쪽 점만 진짜 SDF 를 계산 (밖은 상자까지 거리 = 하한)"""
    if not _GRID:
        xs, ys = np.arange(-4.8, 4.81, 0.2), np.arange(-0.05, 12.61, 0.2)
        X, Y, Z = np.meshgrid(xs, ys, xs, indexing='ij')
        _GRID.append(np.stack([X.ravel(), Y.ravel(), Z.ravel()], axis=1))
    G = _GRID[0]
    m = f(G) < 0.32
    if not m.any():
        return f
    lo, hi = G[m].min(axis=0) - 0.32, G[m].max(axis=0) + 0.32
    c, h = (lo + hi) / 2, (hi - lo) / 2

    def g(P):
        db = np.linalg.norm(np.maximum(np.abs(P - c) - h, 0.0), axis=1)
        sel = db < margin
        if sel.any():
            db[sel] = f(P[sel])
        return db
    return g


def U(fs, k=0.0):
    """여러 SDF 를 하나로 (k>0 이면 부드럽게)"""
    fs = list(fs)
    if len(fs) > 2:
        fs = [bounded(g, 0.6) for g in fs]

    def f(P):
        d = fs[0](P)
        for g in fs[1:]:
            d = S.smin(d, g(P), k) if k > 0 else np.minimum(d, g(P))
        return d
    return f


class Fast:
    """Figure 대리: add 하는 모든 부품을 bounded 로 감싼다"""

    def __init__(self, fig):
        self._fig = fig

    def add(self, f, col, k=0.2, layer='body', metal=None):
        self._fig.add(bounded(f), col, k, layer, metal)

    def __getattr__(self, n):
        return getattr(self._fig, n)


def _dir(yaw, pitch):
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    return np.array((cp * sy, sp, cp * cy))


def hit(f, c, d, rmax=6.0, n=3000):
    """c 에서 d 방향으로 나가며 f 가 처음 0 을 넘는 거리 (표면 찾기)"""
    d = np.asarray(d, np.float64)
    d = d / np.linalg.norm(d)
    ts = np.linspace(0.0, rmax, n)
    v = f(np.asarray(c, np.float64)[None, :] + ts[:, None] * d[None, :])
    i = int(np.argmax(v > 0))
    return ts[i]


def surf(f, c, yaw, pitch, out=0.0):
    d = _dir(yaw, pitch)
    return np.asarray(c, np.float64) + d * (hit(f, c, d) + out)


def sface(fig, c, yaw, pitch, out=0.0, layer='body', up=(0, 1, 0)):
    """머리 중심 c 에서 (yaw, pitch) 방향의 실제 조형 표면 Frame (바깥에서 안으로 광선, Fg.surface_frame)"""
    d = _dir(yaw, pitch)
    return Fg.surface_frame(fig, np.asarray(c, np.float64) + d * 8.0, -d, up=up, out=out, layer=layer)


def face_eyes(fig, c, spread, pitch, style, size, iris=Fg.IRIS, tilt=0.0, lid=None, layer='body', sink=0.14):
    """성격별 눈 한 쌍 (Fg.eye_at) 을 실제 표면에 붙인다. sink = 표면 아래로 묻는 비율 (size 배)"""
    for s in (-1, 1):
        f = sface(fig, c, s * spread, pitch, -size * sink, layer)
        Fg.eye_at(fig, f, style=style, size=size, iris=iris, side=s, tilt=tilt, lid=lid)


def mouth_w(fig, f, w=0.24, col=(0.30, 0.16, 0.16), r=0.05):
    """표면 Frame f 위 'w' 입 (작은 곡선 두 개, 표면에 얕게 박음)"""
    for s in (-1, 1):
        for k in range(9):
            a = math.pi * k / 8
            Fg._ellipsoid(fig.extra, (s * w * 0.5 + math.cos(a) * w * 0.5, -math.sin(a) * w * 0.42, 0.0), (r, r, r * 0.7), col, 6, 4, f)


def frame_at(f, c, yaw, pitch, out=0.0):
    """머리 표면 위 (yaw, pitch) 지점의 Frame (Z = 바깥)"""
    t = hit(f, c, _dir(yaw, pitch))
    return Fg._frame_on(c, t, yaw, pitch, out)


def axes(ydir, zdir):
    """로컬 Y = ydir, Z ≈ zdir 인 회전 행렬 (열 = 로컬 축)"""
    y = np.asarray(ydir, np.float64)
    y = y / np.linalg.norm(y)
    z = np.asarray(zdir, np.float64)
    z = z - y * (z @ y)
    z = z / np.linalg.norm(z)
    x = np.cross(y, z)
    return np.stack([x, y, z], axis=1)


def ribbon(fig, pts, nrm, width, thick, col, layer, k=0.06, metal=None):
    """점 열을 따라 납작한 띠 (목도리 끝, 끈). nrm = 띠 면의 바깥 방향"""
    pts = [np.asarray(p, np.float64) for p in pts]
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        L = np.linalg.norm(b - a)
        n = nrm[i] if isinstance(nrm, list) else nrm
        R = axes(b - a, n)
        w = width[i] if isinstance(width, list) else width
        fig.add(S.box((a + b) / 2, (w, L / 2 + thick * 0.6, thick), round_=thick * 0.95, R=R), col, k=k, layer=layer, metal=metal)


def feather(pivot, yaw, pitch, roll, dist, size):
    """pivot 에서 아래(-Y)로 dist 떨어진 깃털 하나 (yaw·pitch·roll 로 방향), size = (폭, 길이, 두께)"""
    R = S.rot(yaw, pitch, roll)
    c = np.asarray(pivot, np.float64) + R @ np.array((0.0, -dist, 0.0))
    return S.ellipsoid(c, size, R=R)


def eyes(fig, f, c, spread, pitch, size, iris=Fg.IRIS, tall=1.1, sclera=True, out=0.0, iris_hi=None):
    """반짝이는 큰 눈 (표면에 붙임): 흰자 테두리 + 홍채 + 동공 + 반짝임 2개"""
    b = fig.extra
    E = Fg._ellipsoid
    ih = iris_hi or tuple(min(1.0, x * 1.6 + 0.05) for x in iris)
    for s in (-1, 1):
        fr = frame_at(f, c, s * spread, pitch, -size * 0.2 + out)
        if sclera:
            E(b, (s * size * 0.13, -size * 0.07, -size * 0.02), (size * 0.92, size * tall * 0.92, size * 0.33), (0.99, 0.98, 0.97), 22, 10, fr)
        E(b, (0, 0, size * 0.04), (size * 0.86, size * tall * 0.86, size * 0.34), Fg.PUPIL, 22, 10, fr)
        E(b, (0, -size * 0.12, size * 0.09), (size * 0.76, size * tall * 0.74, size * 0.32), iris, 20, 10, fr)
        E(b, (0, -size * 0.38, size * 0.12), (size * 0.5, size * 0.34, size * 0.28), ih, 16, 8, fr)
        E(b, (0, size * 0.05, size * 0.19), (size * 0.42, size * 0.48, size * 0.28), Fg.PUPIL, 16, 8, fr)
        E(b, (-size * 0.3 * s, size * 0.34, size * 0.33), (size * 0.3, size * 0.3, size * 0.12), Fg.SHINE, 12, 6, fr)
        E(b, (size * 0.3 * s, -size * 0.42, size * 0.31), (size * 0.12, size * 0.12, size * 0.06), Fg.SHINE, 8, 4, fr)


def gear(fig, gc, gr, layer='gear'):
    teeth = []
    for k in range(8):
        R = S.rot(k * 45 + 10, 0, 0)
        teeth.append(S.box(tuple(np.array(gc) + R @ np.array((gr * 1.12, 0, 0))), (gr * 0.22, 0.11, gr * 0.2), round_=0.03, R=R))
    fig.add(S.subtract(U([S.cylinder(gc, gr, 0.12, round_=0.04)] + teeth), S.cylinder(gc, gr * 0.38, 0.3)), SILVER, k=0.0, layer=layer, metal=SIL)


def moon_rock(fig, c, r, layer='rock'):
    c = np.asarray(c, np.float64)
    base = lambda P: S.ellipsoid(c, (r, r * 0.82, r))(P) + 0.025 * np.sin(P[:, 0] * 9) * np.sin(P[:, 2] * 8)
    craters = []
    for (yaw, pit, rr) in ((-20, 25, 0.24), (40, 15, 0.2), (5, 55, 0.18), (-60, 5, 0.17), (80, 40, 0.15), (150, 30, 0.2)):
        d = _dir(yaw, pit)
        craters.append((c + d * np.array((r, r * 0.82, r)) * 1.04, rr * r / 0.8))

    def f(P):
        d = base(P)
        for cc, rr in craters:
            d = np.maximum(d, -(S.sphere(cc, rr)(P)))
        return d
    fig.add(f, (0.68, 0.68, 0.70), k=0.0, layer=layer)
    for cc, rr in craters:
        fig.paint(S.sphere(cc, rr * 1.05), (0.55, 0.55, 0.58), soft=0.05)


def bottle_cap(fig, c, R, r=0.2, col=Fg.GOLD, metal=GLD, layer='cap'):
    """병뚜껑: 납작한 원판 + 톱니 테 (R = 회전 행렬, 로컬 Y = 뚜껑 축)"""
    c = np.asarray(c, np.float64)
    fs = [S.cylinder(c, r, 0.035, round_=0.02, R=R)]
    for k in range(14):
        a = 2 * math.pi * k / 14
        fs.append(S.sphere(c + R @ np.array((math.cos(a) * r, -0.03, math.sin(a) * r)), 0.045))
    fig.add(U(fs, 0.03), col, k=0.0, layer=layer, metal=metal)


def flower_brooch(fig, c, n, size=0.17):
    """빨간 꽃 브로치 (꽃잎 5장 + 금색 가운데). n = 바깥 방향"""
    n = np.asarray(n, np.float64)
    n = n / np.linalg.norm(n)
    R = axes((0, 1, 0) - n * n[1], n)
    c = np.asarray(c, np.float64)
    for k in range(5):
        a = math.pi / 2 + 2 * math.pi * k / 5
        p = c + R @ np.array((math.cos(a) * size * 0.75, math.sin(a) * size * 0.75, 0.0))
        fig.add(S.ellipsoid(p, (size * 0.55, size * 0.55, size * 0.25), R=R), CRIMSON, k=0.04, layer='brooch')
    fig.add(S.sphere(c + n * size * 0.15, size * 0.38), Fg.GOLD, k=0.0, layer='brooch_c', metal=GLD)


def fringe(fig, end, tang, n, gap, L, col, layer='fringe'):
    """띠 끝 술 장식 (tang: 띠 끝의 로컬 축, Y = 띠 진행 방향)"""
    fs = []
    for i in range(n):
        u = (i - (n - 1) / 2) * gap
        a = end + tang @ np.array((u, 0.0, 0))
        fs.append(S.capsule(a, a + tang @ np.array((u * 0.3, L, 0.0)), 0.045, 0.04))
    fig.add(U(fs), col, k=0.0, layer=layer)


def tube_loop(fig, core, c, nrm, out, r, col, layer, n=28, k=0.15, start=0.0):
    """core 표면을 따라 c 를 지나는 (nrm 에 수직인) 고리 관 (목도리 등)"""
    nrm = np.asarray(nrm, np.float64)
    nrm = nrm / np.linalg.norm(nrm)
    u = np.cross(nrm, (0, 0, 1.0))
    u /= np.linalg.norm(u)
    v = np.cross(nrm, u)
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n + start
        d = u * math.cos(a) + v * math.sin(a)
        pts.append(np.asarray(c) + d * (hit(core, c, d) + out))
    fig.add(U([S.capsule(pts[i], pts[(i + 1) % n], r) for i in range(n)], k), col, k=k, layer=layer)
    return pts


# ---------- 너구리 ----------

def snack_bag(fig, c, R):
    """파란 과자 봉지 (톱니 모양으로 눌린 위·아래 끝) + 곰돌이 과자 그림"""
    c = np.asarray(c, np.float64)
    pillow = lambda P: S.ellipsoid(c, (0.62, 0.95, 0.22), R=R)(P)
    fig.add(S.intersect(S.box(c, (0.58, 0.72, 0.3), round_=0.12, R=R), pillow), BLUE, k=0.0, layer='snack')
    for sy in (-1, 1):
        cc = c + R @ np.array((0, sy * 0.76, 0))
        teeth = [S.box(cc, (0.6, 0.09, 0.035), round_=0.03, R=R)]
        for i in range(9):
            teeth.append(S.sphere(cc + R @ np.array((-0.52 + i * 0.13, sy * 0.08, 0)), 0.055))
        fig.add(U(teeth, 0.04), BLUE_DK, k=0.0, layer='snack_e')
    # 곰돌이 과자: 얼굴 + 귀 2 + 눈·코 (봉지 앞면에 붙임)
    f = c + R @ np.array((0, -0.02, 0.17))
    nz = R[:, 2]
    fig.add(U([S.ellipsoid(f, (0.33, 0.28, 0.06), R=R)] + [S.sphere(f + R @ np.array((s * 0.25, 0.24, -0.01)), 0.12) for s in (-1, 1)], 0.05),
            COOKIE, k=0.0, layer='cookie')
    for s in (-1, 1):
        fig.paint(S.sphere(f + R @ np.array((s * 0.25, 0.24, 0.06)), 0.055), (0.86, 0.62, 0.34), soft=0.03)
        fig.add(S.sphere(f + R @ np.array((s * 0.12, 0.04, 0.05)), 0.04), NOSE, k=0.0, layer='cookie_d')
    fig.add(S.ellipsoid(f + R @ np.array((0, -0.07, 0.055)), (0.06, 0.04, 0.03), R=R), NOSE, k=0.0, layer='cookie_d')


def build(fig, rng):
    Fg.base(fig, rng, flowers=8)
    fig = Fast(fig)
    y0 = Fg.TOP
    hc = np.array((0.0, y0 + 5.45, 0.1))
    # 넓적한 머리: 가로로 넓고 아래가 부드럽게 퍼진 하나의 덩어리 (볼 구 없음)
    head_up = S.ellipsoid(hc, (2.3, 1.82, 1.92))
    head_lo = S.ellipsoid(hc + np.array((0, -0.48, 0.1)), (2.48, 1.3, 1.72))
    head = lambda P: S.smin(head_up(P), head_lo(P), 0.6)
    # 동글동글 통통한 몸 (짧은 다리)
    torso = S.ellipsoid((0, y0 + 2.2, 0.0), (1.78, 1.68, 1.62))
    belly = S.ellipsoid((0, y0 + 1.92, 0.3), (1.66, 1.45, 1.5))
    trunk = lambda P: S.smin(torso(P), belly(P), 0.4)
    face = head
    core = lambda P: S.smin(face(P), trunk(P), 0.38)
    fig.add(core, FUR, k=0.3)
    # 볼 털 뭉치 (옆으로 삐죽)
    tufts = []
    for s in (-1, 1):
        for (dy, dz, L) in ((0.35, 0.3, 0.5), (-0.05, 0.6, 0.55), (-0.45, 0.85, 0.45)):
            a = np.array((s * 2.2, hc[1] - 0.62 + dy, dz))
            tufts.append(S.capsule(a, a + np.array((s * L, -0.25, 0.05)), 0.26, 0.07))
    fig.add(U(tufts, 0.1), FUR, k=0.25)
    # 주둥이
    mz = surf(face, hc, 0, -26, -0.35)
    fig.add(S.ellipsoid(mz, (0.62, 0.42, 0.45)), CREAM, k=0.35)
    # 정수리 털 몇 가닥
    tp = hc + np.array((0, 1.75, 0.15))
    fig.add(U([S.capsule(tp - np.array((0, 0.2, 0)), tp + np.array((dx, 0.45, dz)), 0.2, 0.08) for dx, dz in ((-0.32, 0.1), (0.0, -0.1), (0.32, 0.12))], 0.1),
            FUR_DK, k=0.15)
    # 귀: 둥글고 짙은 갈색 + 연분홍 안쪽 (오목)
    for s in (-1, 1):
        ec = np.array((s * 1.6, hc[1] + 1.45, -0.25))
        Re = S.rot(s * -12, -8, -s * 22)
        ear = S.ellipsoid(ec, (0.82, 0.85, 0.38), R=Re)
        inner = S.ellipsoid(ec + Re @ np.array((0, -0.05, 0.3)), (0.52, 0.56, 0.22), R=Re)
        fig.add(S.subtract(ear, inner, k=0.08), FUR_DK, k=0.3, layer='ear')
        fig.paint(S.ellipsoid(ec + Re @ np.array((0, -0.08, 0.25)), (0.56, 0.6, 0.3), R=Re), EAR_IN, soft=0.06)
    # 얼굴 무늬: 흰 아래 얼굴 + 눈 위 흰 눈썹 띠 → 짙은 눈가 가면 → 이마 점
    fig.paint(S.ellipsoid(hc + np.array((0, -1.15, 1.35)), (2.3, 1.0, 1.3)), CREAM, soft=0.1)
    fig.paint(S.ellipsoid(surf(face, hc, 0, 4, -0.2), (0.5, 0.9, 0.6)), CREAM, soft=0.1)
    for s in (-1, 1):
        fig.paint(S.ellipsoid(surf(face, hc, s * 22, 20), (0.75, 0.42, 0.6), R=S.rot(0, 0, -s * 14)), CREAM, soft=0.08)
        fig.paint(S.ellipsoid(surf(face, hc, s * 25.5, -6.5, -0.1), (0.8, 0.64, 0.7), R=S.rot(s * 25, 0, s * 22)), FUR_DK, soft=0.05)
    fig.paint(S.sphere(surf(face, hc, 0, 22), 0.13), FUR_DK, soft=0.04)
    # 가슴 흰 털 (V 자 + 삐죽 끝)
    bib = lambda P: S.smin(S.ellipsoid((0, y0 + 2.75, 1.3), (0.8, 0.6, 0.7))(P), S.ellipsoid((0, y0 + 2.2, 1.35), (0.42, 0.62, 0.7))(P), 0.2)
    fig.paint(lambda P: np.maximum(bib(P), np.abs(core(P)) - 0.06), CREAM, soft=0.05)
    # 눈·코·입·볼
    # 작고 동그란 눈 (짙은 가면 안에서 반짝)
    face_eyes(fig, hc, spread=25, pitch=-5, style='round', size=0.47, iris=(0.24, 0.14, 0.10))
    nf = sface(fig, hc, 0, -17, -0.05)
    Fg._ellipsoid(fig.extra, (0, 0, 0), (0.22, 0.15, 0.13), NOSE, 14, 8, nf)
    Fg._ellipsoid(fig.extra, (-0.06, 0.06, 0.1), (0.07, 0.04, 0.04), (0.6, 0.5, 0.48), 8, 4, nf)
    mouth_w(fig, sface(fig, hc, 0, -27, -0.02), w=0.22, col=(0.32, 0.2, 0.17))
    for s in (-1, 1):
        fig.paint(S.sphere(surf(face, hc, s * 41, -25), 0.3), Fg.BLUSH, soft=0.28)
    # 다리·발 (짙은 갈색)
    for s in (-1, 1):
        fig.add(S.capsule((s * 0.8, y0 + 1.2, 0.15), (s * 0.82, y0 + 0.45, 0.35), 0.52, 0.48), FUR_DK, k=0.3, layer='body')
        fig.add(S.ellipsoid((s * 0.82, y0 + 0.3, 0.55), (0.55, 0.32, 0.68)), FUR_DK, k=0.25, layer='body')
    # 팔: 오른팔(-X) 과자 봉지 들고, 왼팔(+X) 옆에 내림. 아래팔 짙은 색
    sh_r, el_r, pw_r = np.array((-1.5, y0 + 3.05, 0.25)), np.array((-1.95, y0 + 2.3, 0.65)), np.array((-1.82, y0 + 2.6, 1.42))
    sh_l, el_l, pw_l = np.array((1.5, y0 + 3.05, 0.2)), np.array((1.98, y0 + 2.3, 0.4)), np.array((2.02, y0 + 1.75, 0.75))
    for sh, el, pw in ((sh_r, el_r, pw_r), (sh_l, el_l, pw_l)):
        fig.add(S.capsule(sh, el, 0.5, 0.43), FUR, k=0.16, layer='body')
        fig.add(S.capsule(el, pw, 0.43, 0.42), FUR_DK, k=0.15, layer='body')
        fig.add(S.sphere(pw, 0.46), FUR_DK, k=0.15, layer='body')
        fig.paint(S.capsule(el, pw, 0.55), FUR_DK, soft=0.2)
    # 꼬리: 크고 복슬한 갈색, 끝이 짙음 (고리 무늬 없음)
    tail = [((-0.05, 1.15, -1.2), 0.6), ((-0.15, 0.98, -1.95), 0.82), ((-0.25, 1.2, -2.6), 0.78), ((-0.3, 1.62, -2.95), 0.58), ((-0.3, 1.95, -3.0), 0.36)]
    tf = U([S.capsule(np.array(a) + (0, y0, 0), np.array(b) + (0, y0, 0), ra, rb) for (a, ra), (b, rb) in zip(tail, tail[1:])], 0.3)
    fig.add(tf, FUR, k=0.3, layer='body')
    fig.paint(S.sphere(np.array((-0.3, y0 + 2.0, -3.05)), 0.7), FUR_DK, soft=0.3)
    # 배낭 (등): 몸통 + 앞주머니 + 빨간 단추 + 손잡이 + 옆주머니 + 어깨끈 (앞가슴에 빨간 단추)
    pc = np.array((0, y0 + 2.95, -2.1))
    fig.add(S.box(pc, (1.3, 1.22, 0.6), round_=0.5), BLUE, k=0.0, layer='pack')
    pk = pc + np.array((0, -0.35, -0.56))
    fig.add(S.box(pk, (0.8, 0.45, 0.2), round_=0.16), BLUE, k=0.0, layer='pocket')
    fig.add(S.capsule(pk + np.array((-0.62, 0.32, -0.19)), pk + np.array((0.62, 0.32, -0.19)), 0.03), (0.86, 0.88, 0.92), k=0.0, layer='zip')
    fig.add(S.sphere(pk + np.array((0.42, 0.05, -0.2)), 0.14), RED, k=0.0, layer='button')
    fig.add(S.intersect(S.torus(pc + np.array((0, 1.12, 0)), 0.38, 0.09, Rm=S.rot(0, 90, 0)), S.box(pc + np.array((0, 1.62, 0)), (1, 0.5, 1))), BLUE_DK, k=0.0, layer='handle')
    for s in (-1, 1):
        fig.add(S.box(pc + np.array((s * 1.3, -0.4, 0.0)), (0.18, 0.45, 0.4), round_=0.15), BLUE_DK, k=0.0, layer='pocket')
    for s in (-1, 1):
        x0 = s * 0.78
        strap = (lambda x0, s: lambda P: np.maximum(np.maximum(np.abs(trunk(P) - 0.06) - 0.06, np.abs(P[:, 0] - x0 - s * 0.25 * (y0 + 3.6 - P[:, 1])) - 0.17),
                                                    np.maximum(P[:, 1] - (y0 + 3.75), (y0 + 1.75) - P[:, 1])))(x0, s)
        fig.add(strap, BLUE, k=0.0, layer='strap')
        bp = surf(trunk, np.array((0, y0 + 2.85, 0)), s * 30, 0, 0.1)
        fig.add(S.sphere(bp, 0.14), RED, k=0.0, layer='button')
    # 과자 봉지: 오른손(-X)에 쥠
    snack_bag(fig, (-1.3, y0 + 2.8, 1.72), S.rot(-14, -6, 6))
