"""체대 아기 사자 (금 전설): 황금빛 털 + 덩어리진 갈기, 흰 땀띠 (헤어밴드), 흰 트레이닝복 (빨간 두 줄),
금 호루라기 (빨간 끈), 흰 운동화, 끝이 복슬한 꼬리. 받침: 돌계단."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

FUR = (0.98, 0.74, 0.36)
FUR_LT = (0.99, 0.88, 0.66)
MANE = (0.90, 0.56, 0.20)
MANE_LT = (0.97, 0.69, 0.30)
MUZZLE = (1.0, 0.96, 0.90)
EAR_IN = (0.97, 0.66, 0.62)
NOSE = (0.86, 0.48, 0.48)
SUIT = (0.975, 0.965, 0.95)
SUIT_SH = (0.90, 0.89, 0.87)
STRIPE = (0.74, 0.13, 0.18)
BAND = (0.99, 0.985, 0.975)
STONE = (0.82, 0.80, 0.76)
STONE_DK = (0.68, 0.66, 0.62)
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


# ---------- 사자 ----------

def tube_stripe(a, b, r, refdir, ang, w, t0=0.0, t1=1.0):
    """원통(팔·다리) 표면의 세로 줄 (a→b 축, refdir 기준 각 ang 라디안, 폭 w) — paint 용 SDF"""
    a, b = np.asarray(a, np.float64), np.asarray(b, np.float64)
    L = np.linalg.norm(b - a)
    ax = (b - a) / L
    u = np.asarray(refdir, np.float64) - ax * (np.asarray(refdir) @ ax)
    u /= np.linalg.norm(u)
    v = np.cross(ax, u)

    def f(P):
        q = P - a
        t = q @ ax
        rad = q - t[:, None] * ax
        th = np.arctan2(rad @ v, rad @ u)
        d = np.abs(np.angle(np.exp(1j * (th - ang)))) * r - w
        return np.maximum(np.maximum(d, np.maximum(t0 * L - t, t - t1 * L)), np.abs(np.linalg.norm(rad, axis=1) - r) - 0.35)
    return f


def stripe_line(a, b, ra, rb, refdir, ang, w=0.065):
    """원통 구간 a→b (반지름 ra→rb) 표면을 따라가는 또렷한 세로 줄 (별도 레이어용 SDF)"""
    a, b = np.asarray(a, np.float64), np.asarray(b, np.float64)
    ax = (b - a) / np.linalg.norm(b - a)
    u = np.asarray(refdir, np.float64) - ax * (np.asarray(refdir, np.float64) @ ax)
    u /= np.linalg.norm(u)
    v = np.cross(ax, u)
    d = u * math.cos(ang) + v * math.sin(ang)
    return S.capsule(a + d * (ra - w * 0.35), b + d * (rb - w * 0.35), w)


def stairs(fig, c, yaw):
    """돌계단 3단 (뒤로 올라감) + 돌 이음 선"""
    c = np.asarray(c, np.float64)
    R = S.rot(yaw, 0, 0)
    blocks = []
    for i in range(3):
        hy = 0.22 * (i + 1)
        d = 0.95 - i * 0.32
        blocks.append(S.box(c + R @ np.array((0, hy, -i * 0.32)), (1.05, hy, d), round_=0.06, R=R))
    st = U(blocks)
    fig.add(st, STONE, k=0.0, layer='stairs')
    near = lambda P: np.abs(st(P)) - 0.06
    ex, ez = R @ np.array((1, 0, 0)), R @ np.array((0, 0, 1))
    for i in range(3):
        for x in (-0.35, 0.4):
            p0 = c + ex * (x + 0.12 * i)
            zc = 0.95 - i * 0.32 - (0.95 - i * 0.32) + (-i * 0.32)
            fig.paint(lambda P, p0=p0, i=i: np.maximum(np.maximum(np.abs((P - p0) @ ex) - 0.022, near(P)),
                                                       np.maximum(np.abs(P[:, 1] - c[1] - 0.22 * (i + 1)) - 0.2, ((P - c) @ ez) - (0.95 - i * 0.64) - 0.02)), STONE_DK, soft=0.02)
    for i in range(1, 3):
        fig.paint(lambda P, i=i: np.maximum(np.abs(P[:, 1] - c[1] - 0.44 * i) - 0.02, near(P)), STONE_DK, soft=0.02)
    fig.paint(lambda P: np.maximum(0.3 * np.sin(P[:, 0] * 7) * np.sin(P[:, 1] * 9) * np.sin(P[:, 2] * 8) + 0.1, near(P)), STONE_DK, soft=0.2)


def build(fig, rng):
    Fg.base(fig, rng, flowers=6)
    y0 = Fg.TOP
    sc, syaw = np.array((-2.75, y0 - 0.04, -0.5)), 35
    Rs = S.rot(syaw, 0, 0)
    clear_grass(fig, lambda C: np.all(np.abs((C - sc) @ Rs)[:, [0, 2]] < (1.1, 1.0), axis=1), ymax=y0 + 1.5)
    fig = Fast(fig)
    # ---- 운동화 + 양말 ----
    for s in (-1, 1):
        fc = np.array((s * 0.78, y0 + 0.3, 0.45))
        fig.add(S.ellipsoid(fc, (0.58, 0.34, 0.82)), SUIT, k=0.0, layer='shoe')
        fig.add(S.intersect(S.ellipsoid(fc + (0, -0.12, 0), (0.62, 0.3, 0.86)), S.box(fc + (0, -0.32, 0), (1, 0.1, 1))), (0.95, 0.94, 0.92), k=0.0, layer='sole')
        for k in range(2):
            fig.paint(tube_stripe(fc + (0, 0, -0.6), fc + (0, 0, 0.5), 0.58, (s, 0.2, 0), -0.25 + k * 0.32, 0.06), STRIPE, soft=0.02)
        fig.add(S.ellipsoid(fc + (0, 0.05, -0.55), (0.42, 0.25, 0.25)), STRIPE, k=0.05, layer='shoe')
        fig.add(S.capsule((s * 0.78, y0 + 0.55, 0.15), (s * 0.78, y0 + 0.82, 0.12), 0.46), FUR, k=0.05, layer='sock')
        fig.paint(S.box((s * 0.78, y0 + 0.62, 0.15), (0.6, 0.06, 0.6)), STRIPE, soft=0.02)
    # ---- 바지 (흰색, 바깥 빨간 두 줄) ----
    hip = S.ellipsoid((0, y0 + 1.95, 0.0), (1.42, 0.62, 1.12))
    legs = U([S.capsule((s * 0.72, y0 + 1.85, 0.05), (s * 0.78, y0 + 0.92, 0.15), 0.62, 0.55) for s in (-1, 1)], 0.15)
    fig.add(lambda P: S.smin(hip(P), legs(P), 0.3), SUIT, k=0.0, layer='pants')
    for s in (-1, 1):
        fig.add(S.torus((s * 0.78, y0 + 0.95, 0.15), 0.5, 0.1), SUIT_SH, k=0.0, layer='pants')
        for k in (-1, 1):
            fig.add(stripe_line((s * 0.78, y0 + 0.98, 0.15), (s * 0.73, y0 + 1.85, 0.05), 0.56, 0.62, (s, 0, 0), k * 0.2), STRIPE, k=0.0, layer='stripe')
    # ---- 꼬리 (오른쪽 뒤로, 끝 갈기 뭉치) ----
    tl = [(0.25, 1.9, -1.0), (0.85, 1.5, -1.55), (1.55, 1.35, -1.6), (2.0, 1.7, -1.2), (2.15, 2.15, -0.95)]
    tl = [np.array(p) + (0, y0, 0) for p in tl]
    fig.add(U([S.capsule(a, b, 0.2, 0.17) for a, b in zip(tl, tl[1:])], 0.1), FUR, k=0.0, layer='tail')
    fig.add(U([S.ellipsoid(tl[-1] + R @ np.array((0, 0.28, 0)), (0.2, 0.38, 0.2), R=R) for R in (S.rot(0, 0, 25), S.rot(0, 0, -20), S.rot(90, 25, 0), S.rot(90, -25, 0))], 0.12),
            MANE, k=0.05, layer='tail')
    # ---- 상의 (흰 트레이닝 재킷): 몸통 + 밑단 시보리 + 깃 + 지퍼 ----
    # 어깨 넓은 탄탄한 상체 (역삼각): 몸통 + 넓은 어깨 덩어리
    jt0 = S.ellipsoid((0, y0 + 2.85, 0.0), (1.56, 1.22, 1.32))
    jsh = S.ellipsoid((0, y0 + 3.38, -0.04), (1.9, 0.72, 1.2))
    jt = lambda P: S.smin(jt0(P), jsh(P), 0.5)
    fig.add(jt, SUIT, k=0.3, layer='jacket')
    fig.add((lambda P: np.maximum(np.abs(jt(P) + 0.0) - 0.12, np.abs(P[:, 1] - y0 - 1.98) - 0.16) - 0.04), SUIT_SH, k=0.0, layer='jacket2')
    fig.add(S.torus((0, y0 + 3.85, 0.05), 0.85, 0.2), SUIT, k=0.15, layer='jacket')
    fig.paint(lambda P: np.maximum(np.maximum(np.abs(P[:, 0]) - 0.055, -P[:, 2]), np.abs(P[:, 1] - y0 - 2.9) - 1.0), STRIPE, soft=0.02)
    fig.paint(lambda P: np.maximum(np.abs(S.torus((0, y0 + 3.85, 0.05), 0.85, 0.2)(P)) - 0.03, -(P[:, 1] - y0 - 3.98)), STRIPE, soft=0.03)
    for s in (-1, 1):
        fig.paint(lambda P, s=s: np.maximum(np.abs(np.linalg.norm(P[:, [0, 1]] - (s * 0.75, y0 + 2.45), axis=1) - 0.32) - 0.03,
                                            np.maximum(-P[:, 2], P[:, 1] - y0 - 2.45)), SUIT_SH, soft=0.03)
    # ---- 팔: 소매 (빨간 두 줄) + 시보리 + 황금 손 ----
    for s in (-1, 1):
        sh, el, wr = np.array((s * 1.68, y0 + 3.42, 0.0)), np.array((s * 2.18, y0 + 2.8, 0.15)), np.array((s * 2.48, y0 + 2.25, 0.35))
        fig.add(S.sphere(sh, 0.6), SUIT, k=0.3, layer='jacket')
        fig.add(S.capsule(sh, el, 0.56, 0.5), SUIT, k=0.25, layer='jacket')
        fig.add(S.capsule(el, wr, 0.5, 0.45), SUIT, k=0.2, layer='jacket')
        fig.add(S.torus(wr, 0.4, 0.13, Rm=axes(wr - el, (0, 0, 1)) @ np.eye(3)), SUIT_SH, k=0.0, layer='cuff')
        for k in (-1, 1):
            fig.add(U([stripe_line(sh + (0, 0.12, 0), el, 0.6, 0.5, (s, 1.0, 0), k * 0.24), stripe_line(el, wr - (wr - el) * 0.12, 0.5, 0.45, (s, 1.0, 0), k * 0.24)], 0.04),
                    STRIPE, k=0.0, layer='stripe')
        pw = wr + (wr - el) / np.linalg.norm(wr - el) * 0.42
        fig.add(S.sphere(pw, 0.4), FUR, k=0.15, layer='paw')
        for u in (-1, 0, 1):
            d = (wr - el) / np.linalg.norm(wr - el)
            side = np.cross(d, (0, 0, 1))
            fig.add(S.sphere(pw + d * 0.28 + side * u * 0.17 + np.array((0, 0, 0.1)), 0.15), FUR, k=0.1, layer='paw')
        fig.add(S.ellipsoid(pw + d * 0.22 + side * s * 0.0 + (s * 0.15, 0.25, 0.1), (0.13, 0.22, 0.13)), FUR, k=0.15, layer='paw')
    # ---- 머리 ----
    hc = np.array((0.0, y0 + 5.75, 0.25))
    # 넓고 둥근 머리: 아래쪽이 부드럽게 넓은 하나의 덩어리 (따로 붙인 볼 없음)
    head_up = S.ellipsoid(hc, (2.0, 1.72, 1.78))
    head_lo = S.ellipsoid(hc + np.array((0, -0.45, 0.1)), (2.12, 1.25, 1.66))
    head = lambda P: S.smin(head_up(P), head_lo(P), 0.5)
    fig.add(head, FUR, k=0.3, layer='head')
    mz = surf(head, hc, 0, -28, -0.3)
    muz = S.ellipsoid(mz, (0.74, 0.46, 0.42))
    fig.add(muz, MUZZLE, k=0.3, layer='head')
    fig.paint(S.ellipsoid(mz + (0, -0.15, 0.2), (1.1, 0.65, 0.6)), MUZZLE, soft=0.15)
    face = lambda P: S.smin(head(P), muz(P), 0.3)
    # 갈기: 얼굴 뒤 큰 덩어리 (얼굴 자리 파냄) + 바깥쪽 잎(불꽃) 모양 큰 뭉치들
    mc = hc + np.array((0, 0.35, -0.8))
    MR = np.array((2.45, 2.55, 1.95))
    mb = S.subtract(S.ellipsoid(mc, MR), S.ellipsoid(hc + (0, -0.25, 1.15), (1.95, 1.85, 1.5)), k=0.35)
    locks = []
    for i in range(10):
        el_ = -58 + i * 13.5
        n = int(round(17 * math.cos(math.radians(el_)))) + 4
        for j in range(n):
            az = 360 * (j + 0.5 * (i % 2)) / n
            d = _dir(az, el_)
            if d[2] > 0.3 and el_ < 35:
                continue
            p = mc + d * MR * 0.97
            tang = np.array((0, -1.0, 0)) + d * d[1]
            if np.linalg.norm(tang) < 0.25:
                tang = np.array((0, 0, -1.0)) + d * d[2]
            tn = tang / np.linalg.norm(tang)
            R = axes(tn * 0.8 + d * 0.42 if el_ < 20 else tn * 0.6 + d * 0.5, d)
            L = 0.6 + 0.14 * ((i * 7 + j * 3) % 3)
            if abs((p - (hc + np.array((0, 1.15, 0)))) @ (np.array((0, 1.0, 0.12)) / np.linalg.norm((0, 1.0, 0.12)))) < 0.5:
                continue
            # 잎 모양 털 뭉치: 아래로 겹쳐 내려오는 비늘처럼, 끝이 둥글게 가늘어짐 (복슬한 실루엣)
            b0 = p + R @ np.array((0, -0.2, -0.08))
            locks.append(S.capsule(b0, b0 + R @ np.array((0, L * 1.0, 0)), 0.4, 0.16))
    # 정수리: 앞(밴드 뒤)에서 뒤로 빗어 넘긴 긴 갈기 결 (돔을 따라 흐르는 굵은 → 가는 관)
    for u in (-1.05, -0.7, -0.35, 0.0, 0.35, 0.7, 1.05):
        pts = []
        for t in np.linspace(0, 1, 7):
            th = math.radians(62 + 88 * t + 6 * abs(u))
            d = np.array((u * (0.62 - 0.12 * t), math.sin(th), math.cos(th)))
            d /= np.linalg.norm(d)
            pts.append(mc + d * MR * (1.03 + 0.05 * math.sin(math.pi * t)))
        rr = [0.4 - 0.27 * t for t in np.linspace(0, 1, 7)]
        locks += [S.capsule(pts[i], pts[i + 1], rr[i], rr[i + 1]) for i in range(6)]
    lk = U(locks, 0.12)
    mane = lambda P: S.smin(mb(P), lk(P), 0.25)
    fig.add(mane, MANE, k=0.0, layer='mane')
    fig.paint(lambda P: np.maximum(S.ellipsoid(mc, MR + 0.42)(P), np.abs(mane(P)) - 0.04), MANE_LT, soft=0.3)
    # 앞머리 갈기 (밴드 위로 솟은 뭉치)
    fl = []
    for (yaw, pit, L, r0) in ((0, 48, 0.95, 0.36), (-22, 44, 0.6, 0.26), (22, 44, 0.6, 0.26)):
        p = surf(head, hc, yaw, pit, -0.15)
        R = axes(_dir(yaw, pit) * 0.35 + np.array((yaw * -0.01, 0.75, 0.55)), _dir(yaw, 10))
        fl.append(S.capsule(p, p + R @ np.array((0, L, 0)), r0, 0.1))
    fig.add(U(fl, 0.12), MANE_LT, k=0.0, layer='mane2')
    # 귀 (갈기 위로 나온 둥근 귀, 분홍 안쪽)
    for s in (-1, 1):
        ec = hc + np.array((s * 1.75, 2.15, -0.25))
        Re = S.rot(s * 12, 0, -s * 28)
        fig.add(S.subtract(S.ellipsoid(ec, (0.72, 0.7, 0.4), R=Re), S.ellipsoid(ec + Re @ np.array((0, -0.05, 0.3)), (0.44, 0.42, 0.22), R=Re), k=0.06),
                FUR, k=0.0, layer='ear')
        fig.paint(S.ellipsoid(ec + Re @ np.array((0, -0.06, 0.26)), (0.48, 0.46, 0.25), R=Re), EAR_IN, soft=0.05)
    # 땀띠 (흰 헤어밴드): 이마를 가로질러 갈기 둘레를 감는 띠
    allh = lambda P: np.minimum(face(P), mb(P) - 0.36 * np.clip(-(P[:, 2] - hc[2] + 0.4) / 1.2, 0, 1))
    bn = np.array((0, 1.0, 0.12))
    bn /= np.linalg.norm(bn)
    bcen = hc + np.array((0, 1.15, 0))
    band = lambda P: np.maximum(np.abs(allh(P) - 0.08) - 0.07, np.abs((P - bcen) @ bn) - 0.3) - 0.035
    fig.add(lambda P: band(P) - 0.01 * np.sin(P[:, 0] * 20) * np.sin(P[:, 1] * 22) * np.sin(P[:, 2] * 18), BAND, k=0.0, layer='band')
    fig.paint(lambda P: band(P) - 0.03, BAND, soft=0.02)
    # 얼굴: 눈·눈썹·코·입·볼
    # 자신감 있는 둥근 눈 + 살짝 올린 눈썹
    face_eyes(fig, hc, spread=23, pitch=-4, style='round', size=0.6, iris=(0.34, 0.19, 0.09), layer='head')
    for s in (-1, 1):
        a, m, b = surf(face, hc, s * 11, 13, -0.03), surf(face, hc, s * 20, 16.5, -0.03), surf(face, hc, s * 30, 15, -0.03)
        fig.add(U([S.capsule(a, m, 0.065, 0.075), S.capsule(m, b, 0.075, 0.05)]), (0.80, 0.52, 0.28), k=0.0, layer='brow')
        fig.paint(S.sphere(surf(face, hc, s * 37, -19), 0.4), Fg.BLUSH, soft=0.34)
    nf = sface(fig, hc, 0, -18, -0.04, layer='head')
    Fg._ellipsoid(fig.extra, (0, 0, 0), (0.2, 0.14, 0.12), NOSE, 14, 8, nf)
    mouth_w(fig, sface(fig, hc, 0, -31, -0.02, layer='head'), w=0.22, col=(0.45, 0.22, 0.18))
    # ---- 호루라기: 빨간 끈 (목 뒤 → 가슴) + 금 호루라기 ----
    neck_pts = [np.array((s * 0.9, y0 + 3.9, -0.1)) for s in (-1, 1)]
    wc = surf(jt, np.array((0, y0 + 2.95, 0)), 0, 0, 0.32)
    for s, a in zip((-1, 1), neck_pts):
        pts = [a, np.array((s * 0.85, y0 + 3.85, 0.65)), np.array((s * 0.45, y0 + 3.55, 1.12)), wc + (s * 0.08, 0.3, -0.05)]
        for p, q in zip(pts, pts[1:]):
            fig.add(S.capsule(p, q, 0.045), STRIPE, k=0.02, layer='cord')
    fig.add(S.torus(wc + (0, 0.25, -0.02), 0.1, 0.035, Rm=S.rot(0, 90, 0)), Fg.GOLD, k=0.0, layer='whistle', metal=GLD)
    fig.add(S.capsule(wc + (-0.12, 0, 0.0), wc + (0.12, 0, 0.0), 0.2), Fg.GOLD, k=0.0, layer='whistle', metal=GLD)
    fig.add(S.box(wc + (0.25, 0.08, 0.0), (0.2, 0.08, 0.11), round_=0.05), Fg.GOLD, k=0.06, layer='whistle', metal=GLD)
    fig.add(S.sphere(wc + (-0.02, 0.0, 0.17), 0.06), (0.35, 0.22, 0.05), k=0.0, layer='whistle_h')
    # ---- 받침 소품: 돌계단 ----
    stairs(fig, sc, syaw)
