"""정문 까치 (금 전설): 검은 머리·등 + 흰 볼·배, 푸른-청록 무지개빛 날개·긴 꼬리, 흰 어깨 깃,
벨보이 모자 (검정 + 진홍 띠 + 금 테·단추), 편지 봉투 (금 봉랍) 를 든 날개, 갈색 가죽 크로스백. 받침: 작은 돌기둥."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

INK = (0.17, 0.17, 0.20)
WHITE = (0.98, 0.97, 0.96)
NAVY = (0.11, 0.19, 0.54)
TEAL = (0.03, 0.50, 0.52)
GREEN = (0.10, 0.56, 0.38)
BEAK = (0.30, 0.30, 0.34)
LEG = (0.20, 0.20, 0.24)
CAP = (0.13, 0.13, 0.15)
CRIMSON = (0.70, 0.10, 0.15)
PAPER = (0.98, 0.94, 0.84)
PAPER_DK = (0.88, 0.82, 0.70)
LEATHER = (0.50, 0.31, 0.18)
LEATHER_DK = (0.38, 0.22, 0.12)
STONE = (0.76, 0.76, 0.75)
STONE_DK = (0.62, 0.62, 0.62)
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


def clear_grass(fig, inside, ymax=None):
    """받침 잔디 잎·꽃(fig.extra) 중 inside(중심점) 인 삼각형을 지운다 (웅덩이·돌계단 밑)"""
    b = fig.extra
    if not b.pos:
        return
    P = np.asarray(b.pos, np.float64).reshape(-1, 3, 3)
    cen = P.mean(axis=1)
    m = inside(cen) & (cen[:, 1] < (Fg.TOP + 0.9 if ymax is None else ymax))
    keep = ~m
    b.pos = P[keep].ravel().tolist()
    b.col = np.asarray(b.col, np.float64).reshape(-1, 9)[keep].ravel().tolist()
    if b.alpha and len(b.alpha) == len(P) * 3:
        b.alpha = np.asarray(b.alpha, np.float64).reshape(-1, 3)[keep].ravel().tolist()


# ---------- 까치 ----------

def envelope(fig, c, R):
    """편지 봉투: 납작한 종이 + 덮개 V 선 + 금 봉랍. 로컬 X 폭, Y 높이, Z 앞"""
    c = np.asarray(c, np.float64)
    fig.add(S.box(c, (0.72, 0.48, 0.045), round_=0.03, R=R), PAPER, k=0.0, layer='letter')
    for s in (-1, 1):
        a, b = c + R @ np.array((s * 0.7, 0.45, 0.05)), c + R @ np.array((0, -0.02, 0.05))
        fig.add(S.capsule(a, b, 0.018), PAPER_DK, k=0.0, layer='letter_l')
    fig.add(S.cylinder(c + R @ np.array((0, 0.0, 0.07)), 0.16, 0.035, round_=0.025, R=R @ S.rot(0, 90, 0)), Fg.GOLD, k=0.0, layer='seal', metal=GLD)
    fig.add(S.torus(c + R @ np.array((0, 0.0, 0.1)), 0.1, 0.022, Rm=R @ S.rot(0, 90, 0)), Fg.GOLD, k=0.0, layer='seal', metal=GLD)


def column(fig, c):
    """작은 돌기둥: 받침돌 + 마디 진 기둥 + 머리돌 + 돌 공"""
    c = np.asarray(c, np.float64)
    fig.add(S.box(c + (0, 0.16, 0), (0.62, 0.16, 0.62), round_=0.07), STONE, k=0.0, layer='col')
    fig.add(S.cylinder(c + (0, 0.36, 0), 0.52, 0.06, round_=0.04), STONE, k=0.0, layer='col')
    fig.add(S.cylinder(c + (0, 1.15, 0), 0.43, 0.78, round_=0.05), STONE, k=0.0, layer='col')
    for yy in (0.72, 1.12, 1.52):
        fig.paint(lambda P, yy=yy: np.maximum(np.abs(P[:, 1] - c[1] - yy) - 0.025, np.linalg.norm(P[:, [0, 2]] - c[[0, 2]], axis=1) - 0.5), STONE_DK, soft=0.02)
    shaft = S.cylinder(c + (0, 1.15, 0), 0.43, 0.78, round_=0.05)
    for k in range(4):
        a = math.pi * k / 2 + 0.4
        n = np.array((math.sin(a), 0, -math.cos(a)))
        t = np.array((math.cos(a), 0, math.sin(a)))
        fig.paint(lambda P, n=n, t=t: np.maximum(np.maximum(np.abs((P - c) @ n) - 0.02, np.abs(shaft(P)) - 0.05),
                                                 np.maximum(np.abs(((P[:, 1] - c[1] - 0.52) % 0.8) - 0.4) - 0.2, -((P - c) @ t))), STONE_DK, soft=0.02)
    fig.add(S.cylinder(c + (0, 1.97, 0), 0.5, 0.06, round_=0.04), STONE, k=0.0, layer='col')
    fig.add(S.box(c + (0, 2.1, 0), (0.48, 0.09, 0.48), round_=0.05), STONE, k=0.0, layer='col')
    fig.add(S.sphere(c + (0, 2.55, 0), 0.4), (0.80, 0.80, 0.79), k=0.08, layer='col')
    fig.paint(lambda P: S.sphere(c + (0.1, 2.0, 0.3), 1.0)(P) + 0.25 * np.sin(P[:, 0] * 11) * np.sin(P[:, 1] * 9) * np.sin(P[:, 2] * 10) + 0.6, STONE_DK, soft=0.4)
    leaves = []
    for k in range(7):
        a = 2 * math.pi * k / 7 + 0.3
        R = S.rot(math.degrees(a), -35 - 15 * (k % 2), 0)
        p = c + np.array((math.sin(a) * 0.7, 0.25, math.cos(a) * 0.7))
        leaves.append(S.ellipsoid(p + R @ np.array((0, 0.22, 0)), (0.12, 0.3, 0.04), R=R))
    fig.add(U(leaves), (0.38, 0.66, 0.26), k=0.0, layer='leaf')


def build(fig, rng):
    Fg.base(fig, rng, flowers=6)
    y0 = Fg.TOP
    cc = np.array((2.85, y0 - 0.02, 0.85))
    clear_grass(fig, lambda C: np.linalg.norm(C[:, [0, 2]] - cc[[0, 2]], axis=1) < 0.7)
    fig = Fast(fig)
    hc = np.array((0.0, y0 + 5.55, 0.28))
    # 매끈한 둥근 머리 (앞으로 살짝 숙임), 아래쪽이 아주 살짝 넓다 (볼 구 없음)
    head_up = S.ellipsoid(hc, (1.72, 1.7, 1.72), R=S.rot(0, 10, 0))
    head_lo = S.ellipsoid(hc + np.array((0, -0.48, 0.1)), (1.8, 1.2, 1.62))
    head = lambda P: S.smin(head_up(P), head_lo(P), 0.5)
    # 날씬하지만 배는 둥근 몸: 아래 둥근 배 → 위로 갈수록 좁아지는 가슴
    belly = S.ellipsoid((0, y0 + 2.15, 0.15), (1.82, 1.62, 1.66))
    chest = S.ellipsoid((0, y0 + 3.8, 0.12), (1.62, 1.4, 1.48))
    trunk = lambda P: S.smin(belly(P), chest(P), 0.7)
    core = lambda P: S.smin(head(P), trunk(P), 0.65)
    fig.add(core, INK, k=0.3)
    # 흰 무늬: 볼 (눈 아래·옆) + 가슴·배
    for s in (-1, 1):
        fig.paint(lambda P, e=S.ellipsoid(surf(head, hc, s * 38, -14, -0.2), (1.0, 0.95, 0.9), R=S.rot(s * 38, 0, s * 20)): np.maximum(e(P), np.abs(core(P)) - 0.05), WHITE, soft=0.06)
    fig.paint(lambda P, e=S.ellipsoid((0, y0 + 2.0, 0.95), (1.5, 1.5, 1.2)): np.maximum(e(P), np.abs(core(P)) - 0.05), WHITE, soft=0.08)
    # 부리 (짙은 회색, 앞으로 뾰족)
    bf = sface(fig, hc, 0, -9)
    bp, fwd, up = np.array(bf.p((0, 0, -0.12))), np.array(bf.dir((0, 0, 1))), np.array(bf.dir((0, 1, 0)))
    fig.add(S.capsule(bp, bp + fwd * 1.05 - up * 0.1, 0.33, 0.04), BEAK, k=0.12, layer='beak')
    fig.add(S.capsule(bp - up * 0.16, bp + fwd * 0.6 - up * 0.24, 0.2, 0.04), (0.24, 0.24, 0.28), k=0.08, layer='beak')
    # 똘똘한 아몬드 눈 (눈꼬리 살짝 올라감)
    face_eyes(fig, hc, spread=25, pitch=-2, style='almond', size=0.62, iris=(0.34, 0.19, 0.10), tilt=11)
    for s in (-1, 1):
        fig.paint(S.sphere(surf(head, hc, s * 44, -18), 0.38), Fg.BLUSH, soft=0.32)
    # 다리·발
    for s in (-1, 1):
        x = s * 0.7
        ank = np.array((x, y0 + 0.24, 0.45))
        fig.add(S.capsule((x, y0 + 1.1, 0.2), ank, 0.22, 0.14), LEG, k=0.06, layer='feet')
        toes = [S.capsule(ank - (0, 0.1, 0), ank - (0, 0.1, 0) + _dir(yw + s * 8, -6) * 0.68, 0.13, 0.08) for yw in (-32, 0, 32)]
        toes.append(S.capsule(ank - (0, 0.1, 0), ank + np.array((0, -0.15, -0.5)), 0.11, 0.07))
        fig.add(U(toes, 0.08), LEG, k=0.06, layer='feet')
    # 긴 꼬리 (뒤 아래로 넓게, 남색 → 청록 끝)
    tp = np.array((0, y0 + 2.05, -1.42))
    tail = U([feather(tp, 0, 36, r, 1.5, (0.36, 1.45, 0.12)) for r in (-15, -7.5, 0, 7.5, 15)], 0.05)
    fig.add(tail, NAVY, k=0.1, layer='tail')
    fig.paint(lambda P: np.maximum(S.sphere(tp + S.rot(0, 36, 0) @ np.array((0, -2.9, 0)), 1.5)(P), np.abs(tail(P)) - 0.1), TEAL, soft=0.6)
    # 날개: 몸 옆면에 붙어 내려오는 납작한 날개 (남색) + 긴 첫째 깃 (청록→초록 끝) + 위 흰 어깨 깃
    for s in (-1, 1):
        sp = surf(trunk, np.array((0, y0 + 2.9, 0)), s * 84, 22)
        outn = np.array((s * 1.0, 0.1, 0.12))
        if s < 0:
            Rw = axes(np.array((-0.5, -1.0, -0.3)), np.array((-1.0, -0.35, 0.25)))
            bcn = sp + np.array((-0.08, 0, 0)) + Rw @ np.array((0, 0.9, 0))
            blade = S.ellipsoid(bcn, (0.64, 1.12, 0.22), R=Rw)
            shoulder = S.ellipsoid(sp + (-0.1, -0.2, 0), (0.55, 0.6, 0.5))
            blade = (lambda b, sh_: lambda P: S.smin(b(P), sh_(P), 0.3))(blade, shoulder)
            prim = U([S.ellipsoid(bcn + Rw @ np.array((u * 0.42, 1.25 + 0.12 * abs(u), -0.04)), (0.2, 0.78, 0.1), R=Rw @ S.rot(0, 0, u * 10))
                      for u in (-0.9, -0.3, 0.3, 0.9)], 0.04)
            tipc = bcn + Rw @ np.array((0, 2.0, 0))
        else:
            el = np.array((2.25, y0 + 2.4, 0.45))
            hand = np.array((1.5, y0 + 2.8, 1.72))
            Ru = axes(el - sp, outn)
            Rf = axes(hand - el, (0.5, -0.7, 0.3))
            blade = U([S.ellipsoid((sp + el) / 2 + outn * 0.1, (0.6, 0.85, 0.3), R=Ru), S.ellipsoid((el + hand) / 2, (0.48, 0.8, 0.3), R=Rf)], 0.3)
            prim = U([S.ellipsoid(hand + Rf @ np.array((u * 0.28, 0.35, 0)), (0.17, 0.5, 0.1), R=Rf) for u in (-1, 0, 1)], 0.05)
            tipc = hand + Rf @ np.array((0, 0.5, 0))
        fig.add(blade, NAVY, k=0.12, layer='wing')
        fig.add(prim, TEAL, k=0.08, layer='wing')
        wing = (lambda b, p: lambda P: np.minimum(b(P), p(P)))(blade, prim)
        fig.paint((lambda tipc, wing: lambda P: np.maximum(S.sphere(tipc, 0.8)(P), np.abs(wing(P)) - 0.03))(tipc, wing), GREEN, soft=0.5)
        fig.paint((lambda sp, wing: lambda P: np.maximum(S.sphere(sp + np.array((s * 0.35, -0.3, 0.3)), 0.55)(P), np.abs(wing(P)) - 0.03))(sp, wing), WHITE, soft=0.06)
    envelope(fig, (1.05, y0 + 3.15, 2.0), S.rot(-18, -10, 12))
    # 벨보이 모자: 검은 원통 + 진홍 띠 + 금 테 2줄 + 앞 금 단추 + 작은 챙
    Rc = S.rot(0, -6, -8)
    cp = hc + np.array((0.2, 1.67, -0.12))
    fig.add(S.cylinder(cp + Rc @ np.array((0, 0.32, 0)), 1.28, 0.36, round_=0.2, R=Rc), CAP, k=0.0, layer='cap')
    fig.add(S.cylinder(cp + Rc @ np.array((0, -0.12, 0)), 1.2, 0.2, round_=0.06, R=Rc), CRIMSON, k=0.0, layer='band')
    for yy in (0.1, -0.33):
        fig.add(S.torus(cp + Rc @ np.array((0, yy, 0)), 1.21, 0.05, Rm=Rc), Fg.GOLD, k=0.0, layer='trim', metal=GLD)
    fig.add(S.cylinder(cp + Rc @ np.array((0, -0.11, 1.2)), 0.17, 0.05, round_=0.04, R=Rc @ S.rot(0, 90, 0)), Fg.GOLD, k=0.0, layer='trim', metal=GLD)
    visor = lambda P: np.maximum(S.cylinder(cp + Rc @ np.array((0, -0.38, 0.25)), 1.3, 0.05, round_=0.04, R=Rc @ S.rot(0, 14, 0))(P), -((P - cp) @ Rc[:, 2]) + 0.6)
    fig.add(visor, CAP, k=0.0, layer='cap')
    # 갈색 가죽 크로스백 (왼 어깨 → 오른 엉덩이) + 덮개 + 금 버클
    mid = np.array((0, y0 + 2.65, 0))
    sn = np.array((1.5, 2.0, 0.0))
    sn /= np.linalg.norm(sn)
    strap = lambda P: np.maximum(np.maximum(np.abs(trunk(P) - 0.07) - 0.05, np.abs((P - mid) @ sn) - 0.11), P[:, 1] - (y0 + 3.85))
    fig.add(strap, LEATHER, k=0.0, layer='strap')
    bc = surf(trunk, np.array((0, y0 + 1.7, 0)), 62, -8, 0.3)
    bR = S.rot(62, -6, 0)
    fig.add(S.box(bc, (0.48, 0.5, 0.28), round_=0.18, R=bR), LEATHER, k=0.0, layer='bag')
    fl = bc + bR @ np.array((0, 0.17, 0.27))
    fig.add(S.intersect(S.box(fl, (0.5, 0.36, 0.05), round_=0.04, R=bR), S.ellipsoid(fl + bR @ np.array((0, 0.3, 0)), (0.6, 0.72, 0.5), R=bR)),
            LEATHER_DK, k=0.0, layer='flap')
    fig.add(S.box(fl + bR @ np.array((0, -0.22, 0.05)), (0.08, 0.07, 0.03), round_=0.02, R=bR), Fg.GOLD, k=0.0, layer='trim', metal=GLD)
    # 받침 소품: 돌기둥
    column(fig, cc)
