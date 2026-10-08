"""도서관 부엉이 (금 전설): 갈색 몸 + 크림색 얼굴 원반·배 (비늘 깃 무늬), 깃털 귀뿔, 큰 호박색 눈,
둥근 금테 안경, 금 테두리 남색 학자 망토 + 빨간 보석 브로치. 펼친 책을 들고, 받침엔 책 3권 더미."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

FUR = (0.67, 0.44, 0.30)
FUR_DK = (0.47, 0.31, 0.22)
FUR_LT = (0.80, 0.64, 0.52)
CREAM = (0.98, 0.94, 0.86)
SCALE = (0.86, 0.76, 0.64)
BEAKC = (0.99, 0.70, 0.16)
FOOT = (0.99, 0.64, 0.14)
NAVY = (0.13, 0.19, 0.40)
GEM = (0.78, 0.08, 0.14)
PAGE = (0.98, 0.95, 0.86)
COVER = (0.58, 0.20, 0.13)
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


# ---------- 부엉이 ----------

def book_closed(fig, c, R, size, col, layer):
    """닫힌 책: 표지(색) + 세 면에 보이는 종이 + 등에 금색 띠 2줄. 로컬: X 폭, Y 두께, Z 깊이 (등 = +Z)"""
    c = np.asarray(c, np.float64)
    w, t, d = size
    fig.add(S.box(c, (w, t, d), round_=0.06, R=R), col, k=0.0, layer=layer)
    fig.add(S.box(c + R @ np.array((-0.05, 0, -0.06)), (w - 0.02, t - 0.05, d - 0.02), round_=0.02, R=R), PAGE, k=0.0, layer=layer + 'p')
    for u in (-0.62, 0.62):
        fig.add(S.box(c + R @ np.array((u * w, 0, d - 0.005)), (0.035, t + 0.012, 0.03), round_=0.012, R=R), Fg.GOLD, k=0.0, layer='trim', metal=GLD)


def open_book(fig, c, R):
    """펼친 책: V 자로 벌어진 빨간 표지 + 볼록한 종이 두 묶음 + 금 모서리. 로컬 Y = 종이 면 바깥, X = 폭, Z = 높이"""
    c = np.asarray(c, np.float64)
    for s in (-1, 1):
        Rs = R @ S.rot(0, 0, -s * 10)
        cc = c + Rs @ np.array((s * 0.62, 0, 0))
        fig.add(S.box(cc, (0.64, 0.045, 0.62), round_=0.035, R=Rs), COVER, k=0.03, layer='book')
        pg = lambda P, cc=cc, Rs=Rs: S.intersect(S.box(cc + Rs @ np.array((-s * 0.02, 0.1, 0)), (0.58, 0.1, 0.56), round_=0.02, R=Rs),
                                                  S.ellipsoid(cc + Rs @ np.array((s * 0.1, -0.35, 0)), (0.75, 0.6, 2.0), R=Rs))(P)
        fig.add(pg, PAGE, k=0.0, layer='pages')
        for v in (-1, 1):
            fig.add(S.box(cc + Rs @ np.array((s * 0.56, 0.0, v * 0.54)), (0.1, 0.06, 0.1), round_=0.03, R=Rs), Fg.GOLD, k=0.0, layer='trim', metal=GLD)
    fig.add(S.capsule(c + R @ np.array((0, -0.05, -0.62)), c + R @ np.array((0, -0.05, 0.62)), 0.07), COVER, k=0.03, layer='book')


def build(fig, rng):
    Fg.base(fig, rng, flowers=7)
    fig = Fast(fig)
    y0 = Fg.TOP
    hc = np.array((0.0, y0 + 5.3, 0.1))
    # 넓적한 머리: 위가 납작하고 옆으로 넓으며, 목 없이 어깨로 그대로 이어진다
    head_up = S.ellipsoid(hc, (2.42, 1.78, 2.02))
    head_lo = S.ellipsoid(hc + np.array((0, -0.5, 0.06)), (2.5, 1.3, 1.88))
    head = lambda P: S.smin(head_up(P), head_lo(P), 0.55)
    # 서양배형 몸: 아래가 넓고 위로 갈수록 좁아져 머리와 한 덩어리
    torso = S.ellipsoid((0, y0 + 2.1, 0.05), (2.12, 1.95, 1.82))
    belly = S.ellipsoid((0, y0 + 1.92, 0.3), (1.8, 1.55, 1.6))
    chest = S.ellipsoid((0, y0 + 3.55, 0.05), (1.85, 1.25, 1.6))
    trunk = lambda P: S.smin(S.smin(torso(P), belly(P), 0.4), chest(P), 0.8)
    core = lambda P: S.smin(head(P), trunk(P), 1.1)
    fig.add(core, FUR, k=0.3)
    on = lambda g: (lambda P: np.maximum(g(P), np.abs(core(P)) - 0.06))
    # 배: 크림색 + 비늘 깃 무늬 (U 자 줄 3줄)
    fig.paint(on(S.ellipsoid((0, y0 + 2.05, 1.0), (1.42, 1.55, 1.3))), CREAM, soft=0.12)
    for row, (yy, xs) in enumerate(((2.75, (-0.75, -0.25, 0.25, 0.75)), (2.2, (-1.0, -0.5, 0.0, 0.5, 1.0)), (1.65, (-0.75, -0.25, 0.25, 0.75)), (1.1, (-0.5, 0.0, 0.5)))):
        for x in xs:
            p = surf(trunk, np.array((0, y0 + yy, 0)), math.degrees(math.atan2(x, 1.5)), 0)
            sc = lambda P, p=p: np.maximum(np.abs(np.linalg.norm(P - p, axis=1) - 0.27) - 0.035, P[:, 1] - p[1] + 0.02)
            fig.paint(on(sc), SCALE, soft=0.05)
    # 얼굴 원반: 짙은 테두리 → 크림 원 2개 + 아래 → 이마 갈색 V
    # 얼굴판: 눈 둘레 크림 원 두 개 + 부리 아래 크림 (또렷한 경계)
    for s in (-1, 1):
        fig.paint(on(S.sphere(surf(head, hc, s * 24, -3, -0.3), 1.38)), CREAM, soft=0.04)
    fig.paint(on(S.sphere(surf(head, hc, 0, -26, -0.3), 0.95)), CREAM, soft=0.04)
    fig.paint(on(S.ellipsoid(surf(head, hc, 0, 26, 0), (0.38, 0.8, 0.6))), FUR, soft=0.12)
    # 귀뿔 (깃털 3장씩, 위·바깥으로)
    for s in (-1, 1):
        piv = hc + np.array((s * 1.5, 1.32, -0.2))
        fs = []
        for (roll, L, w) in ((2, 0.85, 0.33), (-26, 1.1, 0.38), (-54, 0.85, 0.32)):
            R = S.rot(s * -15, -10, s * roll)
            fs.append(S.ellipsoid(piv + R @ np.array((0, L * 0.55, 0)), (w, L * 0.6, 0.18), R=R))
        fig.add(U(fs, 0.1), FUR, k=0.35)
        fig.paint(S.sphere(piv + S.rot(0, 0, s * -40) @ np.array((0, 1.25, 0)), 0.55), FUR_DK, soft=0.25)
    # 눈 (호박색) · 부리 · 볼
    # 크고 동그란 눈, 주황 홍채 (안경 안)
    face_eyes(fig, hc, spread=24, pitch=-1, style='round', size=0.72, iris=(0.66, 0.32, 0.07), sink=0.16)
    bf = frame_at(head, hc, 0, -13)
    bp, fwd, up = np.array(bf.p((0, 0, -0.08))), np.array(bf.dir((0, 0, 1))), np.array(bf.dir((0, 1, 0)))
    fig.add(S.capsule(bp + up * 0.1, bp + fwd * 0.55 - up * 0.5, 0.32, 0.06), BEAKC, k=0.1, layer='beak')
    fig.add(S.ellipsoid(bp + up * 0.08, (0.36, 0.3, 0.24)), BEAKC, k=0.1, layer='beak')
    for s in (-1, 1):
        fig.paint(on(S.sphere(surf(head, hc, s * 42, -20), 0.36)), Fg.BLUSH, soft=0.3)
    # 둥근 금테 안경: 테 2 + 코다리 + 다리 (머리 옆을 따라 뒤로)
    rimR = 0.88
    centers = []
    for s in (-1, 1):
        fr = frame_at(head, hc, s * 24, -2, 0.18)
        cc = np.array(fr.p((0, 0, 0)))
        centers.append(cc)
        nz = np.array(fr.dir((0, 0, 1)))
        Rm = axes(nz, (0, 1, 0))     # 로컬 Y = 바깥 (torus 축)
        fig.add(S.torus(cc, rimR, 0.07, Rm=Rm), Fg.GOLD, k=0.0, layer='glasses', metal=GLD)
        a = cc + np.array(fr.dir((s * rimR, 0.05, 0)))
        pts = [a] + [surf(head, hc, s * yw, 0, 0.1) for yw in (62, 75, 88, 100)]
        for p, q in zip(pts, pts[1:]):
            fig.add(S.capsule(p, q, 0.055), Fg.GOLD, k=0.02, layer='glasses', metal=GLD)
    l, r = centers
    br = [l + (r - l) * t + np.array((0, 0.18 * math.sin(math.pi * t) + 0.05, 0.12 * math.sin(math.pi * t))) for t in np.linspace(0.33, 0.67, 6)]
    for p, q in zip(br, br[1:]):
        fig.add(S.capsule(p, q, 0.055), Fg.GOLD, k=0.02, layer='glasses', metal=GLD)
    # 날개 (망토 아래로 나와 앞으로 모아 책을 받침): 어깨 덩어리 + 깃 + 밝은 깃끝
    bc = np.array((0.15, y0 + 2.15, 2.22))
    for s in (-1, 1):
        sh = np.array((s * 1.75, y0 + 2.75, 0.1))
        tip = np.array((s * 1.35, y0 + 1.75, 1.95))
        mid = (sh + tip) / 2 + np.array((s * 0.45, 0, 0))
        fig.add(S.capsule(sh, mid, 0.5, 0.52), FUR, k=0.25, layer='wing')
        fig.add(S.capsule(mid, tip, 0.55, 0.36), FUR, k=0.25, layer='wing')
        fs = []
        for i in range(4):
            a = mid + np.array((s * 0.25, -0.35 - i * 0.12, -0.3 + i * 0.32))
            fs.append(S.ellipsoid(a + np.array((0, -0.35, 0)), (0.26, 0.55, 0.18), R=S.rot(s * 70, 0, s * 15)))
        fig.add(U(fs, 0.06), FUR_LT, k=0.08, layer='wing')
    # 펼친 책 (배 앞, 보는 사람 쪽으로 기울임)
    open_book(fig, bc, S.rot(0, 42, 0))
    # 망토: 어깨를 덮는 남색 케이프 + 아래 가장자리 금 테 + 앞 여밈 보석
    bell = S.capsule((0, y0 + 4.0, -0.05), (0, y0 + 2.2, -0.05), 1.45, 2.35)
    cb = lambda P: S.smin(core(P), bell(P), 0.3)
    # 망토 밑단: 앞은 가슴 높이, 옆을 지나 등 쪽은 등 가운데까지 내려오는 둥근 U 자 (작은 망토)
    cth = lambda P: np.cos(np.arctan2(P[:, 0], P[:, 2] - 0.05))
    edge = lambda P: y0 + 2.3 + 0.45 * np.clip(cth(P), 0, None) - 1.3 * np.clip(-cth(P), 0, None) ** 1.1
    front_open = lambda P: np.maximum(np.abs(P[:, 0]) - 0.04 - 0.25 * np.clip(y0 + 3.45 - P[:, 1], 0, None), -P[:, 2])
    drop = lambda P: np.clip((y0 + 4.0 - P[:, 1]) / 2.4, 0, 1)
    flare = lambda P: 0.08 + 0.42 * drop(P) ** 1.1 + 0.07 * drop(P) * np.cos(np.arctan2(P[:, 0], P[:, 2]) * 7)   # 아래로 퍼지며 세로 주름
    cape = lambda P: np.maximum(np.maximum(np.abs(cb(P) - flare(P)) - 0.08, edge(P) - P[:, 1]), np.maximum(P[:, 1] - (y0 + 4.02), -front_open(P)))
    fig.add(cape, NAVY, k=0.0, layer='cape')
    trim = lambda P: np.maximum(np.maximum(np.abs(cb(P) - flare(P) - 0.01) - 0.11, np.abs(P[:, 1] - edge(P) - 0.03) - 0.06), -front_open(P))
    fig.add(trim, Fg.GOLD, k=0.0, layer='trim', metal=GLD)
    cl = surf(cb, np.array((0, y0 + 3.4, 0)), 0, 0, 0.3)
    fig.add(S.torus(cl, 0.2, 0.06, Rm=S.rot(0, 80, 0)), Fg.GOLD, k=0.0, layer='trim', metal=GLD)
    fig.add(S.ellipsoid(cl + np.array((0, 0, 0.04)), (0.19, 0.19, 0.13)), GEM, k=0.0, layer='gem')
    fig.paint(S.sphere(cl + np.array((-0.07, 0.08, 0.15)), 0.05), (1.0, 0.75, 0.75), soft=0.04)
    # 발 (주황, 발가락 3개씩)
    for s in (-1, 1):
        a = np.array((s * 0.75, y0 + 0.3, 0.75))
        fig.add(U([S.capsule(a, a + _dir(yw, -6) * 0.55, 0.2, 0.16) for yw in (-28 + s * 6, s * 6, 28 + s * 6)] + [S.sphere(a + np.array((0, 0.1, -0.2)), 0.3)], 0.12),
                FOOT, k=0.0, layer='feet')
    # 꼬리 깃 (뒤, 망토 아래)
    tp = np.array((0, y0 + 1.5, -1.3))
    fig.add(U([feather(tp, 0, 40, roll, 0.7, (0.3, 0.6, 0.12)) for roll in (-30, -10, 10, 30)], 0.05), FUR_DK, k=0.08, layer='tail')
    # 받침 소품: 책 3권 더미 (아래부터 초록·남색·빨강)
    bx, bz = -2.75, 1.6
    yy = y0 + 0.0
    for i, (col, yaw) in enumerate((((0.20, 0.42, 0.27), 30), ((0.20, 0.27, 0.46), 38), ((0.68, 0.16, 0.14), 24))):
        t = 0.2
        book_closed(fig, (bx, yy + t, bz), S.rot(yaw, 0, 0), (0.85 - i * 0.04, t, 0.62), col, f'stack{i}')
        yy += 2 * t + 0.01
