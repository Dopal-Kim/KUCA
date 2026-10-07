"""천문대 하늘다람쥐: 회갈색·흰 얼굴, 큰 둥근 귀, 아주 큰 검은 눈, 금별 남색 망토(크림 안감·붉은 보석 브로치),
놋쇠 망원경(가죽 감개) 어깨끈, 큰 복슬 꼬리, 받침 위 금별·돌."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .clock_rooster import Fast, hit, layer_paint, eye_on, beads
from ..mesh import Frame

TIER = 'gold'

FUR = (0.66, 0.58, 0.54)
FUR_DK = (0.52, 0.44, 0.41)
CREAM = (0.98, 0.95, 0.91)
EAR_IN = (1.0, 0.72, 0.70)
PAW = (1.0, 0.85, 0.84)
NAVY = (0.12, 0.17, 0.40)
LINING = (0.96, 0.89, 0.76)
LEATHER = (0.44, 0.26, 0.16)
EYE_DK = (0.24, 0.13, 0.08)
G = (Fg.GOLD, Fg.GOLD_HI)


def sparkle(fig, c, n, size, col=(1.0, 0.86, 0.3)):
    """표면 위 4각 반짝이 별 (또렷한 부품): 법선 n 쪽을 본다"""
    n = np.asarray(n, np.float64)
    n = n / np.linalg.norm(n)
    up = np.array((0, 1.0, 0)) if abs(n[1]) < 0.9 else np.array((1.0, 0, 0))
    x = np.cross(up, n)
    x /= np.linalg.norm(x)
    y = np.cross(n, x)
    f = Frame(tuple(c), tuple(x), tuple(y), tuple(n))
    Fg._ellipsoid(fig.extra, (0, 0, 0), (size * 0.22, size, 0.02), col, 8, 4, f)
    Fg._ellipsoid(fig.extra, (0, 0, 0), (size, size * 0.22, 0.02), col, 8, 4, f)


def _catmull(pts, n):
    """점들을 지나는 매끈한 곡선 n 점"""
    P = [np.asarray(p, np.float64) for p in pts]
    P = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]
    out = []
    segs = len(P) - 3
    for i in range(n):
        t = i / (n - 1) * segs
        k = min(int(t), segs - 1)
        u = t - k
        p0, p1, p2, p3 = P[k], P[k + 1], P[k + 2], P[k + 3]
        out.append(0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u ** 3))
    return out


def _ud_tri(P, a, b, c):
    """삼각형까지의 거리 (부호 없음, iq)"""
    ba, cb, ac = b - a, c - b, a - c
    pa, pb, pc = P - a, P - b, P - c
    nor = np.cross(ba, ac)
    sgn = (np.sign(pa @ np.cross(ba, nor)) + np.sign(pb @ np.cross(cb, nor)) + np.sign(pc @ np.cross(ac, nor)))

    def seg(e, p):
        h = np.clip((p @ e) / (e @ e), 0, 1)
        q = p - h[:, None] * e
        return np.einsum('ij,ij->i', q, q)
    edge = np.minimum(np.minimum(seg(ba, pa), seg(cb, pb)), seg(ac, pc))
    face = (pa @ nor) ** 2 / (nor @ nor)
    return np.sqrt(np.where(sgn < 2, edge, face))


def sheet(T, B, axis=(0.0, 0.0), th=0.06, nu=32, nv=4, bulge=0.2, folds=0.05):
    """윗선 T 와 아랫단 B 사이의 늘어진 천 (삼각형 판 두께 th). (바깥 반쪽, 안쪽 반쪽) SDF.
    axis: 몸 중심축 (x, z) — 법선을 바깥으로 맞추는 기준"""
    Tc, Bc = _catmull(T, nu), _catmull(B, nu)
    ax = np.array((axis[0], 0, axis[1]))
    grid = []
    for j in range(nv + 1):
        v = j / nv
        row = []
        for i in range(nu):
            p = Tc[i] * (1 - v) + Bc[i] * v
            out = p - ax
            out[1] = 0
            out /= max(np.linalg.norm(out), 1e-6)
            fold = folds * math.sin(i / (nu - 1) * math.pi * 14) * v
            row.append(p + out * (bulge * math.sin(math.pi * v) * 0.6 + fold))
        grid.append(row)
    tris = []
    for j in range(nv):
        for i in range(nu - 1):
            a, b, c, d = grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]
            tris += [(a, b, c), (a, c, d)]
    data = []
    for a, b, c in tris:
        n = np.cross(b - a, c - a)
        n /= max(np.linalg.norm(n), 1e-9)
        cen = (a + b + c) / 3
        o = cen - ax
        o[1] = 0
        if n @ o < 0:
            n = -n
        lo = np.minimum(np.minimum(a, b), c) - 0.25
        hi = np.maximum(np.maximum(a, b), c) + 0.25
        data.append((a, b, c, n, lo, hi))

    def make(side):
        def f(P):
            out = np.full(len(P), 0.25)
            for a, b, c, n, lo, hi in data:
                m = np.all((P >= lo) & (P <= hi), axis=1)
                if not m.any():
                    continue
                Q = P[m]
                d = _ud_tri(Q, a, b, c) - th
                dn = (Q - a) @ n
                d = np.maximum(d, -dn if side > 0 else dn)
                out[m] = np.minimum(out[m], d)
            return out
        return f
    return make(1), make(-1)


def normal(fig, p, e=0.01):
    p = np.asarray(p, np.float64)
    E = np.eye(3) * e
    g = np.array([fig.s.field((p + E[i])[None])[0] - fig.s.field((p - E[i])[None])[0] for i in range(3)])
    return g / np.linalg.norm(g)


def build(fig, rng):
    fig = Fast(fig)
    Fg.base(fig, rng, flowers=7)
    y0 = Fg.TOP

    # ---------- 발·다리 ----------
    for s in (-1, 1):
        x = s * 0.68
        fig.add(S.ellipsoid((x, y0 + 0.22, 0.4), (0.4, 0.24, 0.55)), CREAM, k=0.15)
        for j in range(3):
            a = math.radians(-28 + 28 * j)
            fig.add(S.sphere((x + math.sin(a) * 0.3, y0 + 0.15, 0.4 + math.cos(a) * 0.45), 0.13), CREAM, k=0.08)
        fig.add(S.capsule((x, y0 + 0.3, 0.15), (x * 1.05, y0 + 1.2, 0.0), 0.38, 0.5), FUR, k=0.25)

    # ---------- 몸통 (배는 크림) ----------
    fig.add(S.ellipsoid((0, y0 + 2.3, 0.0), (1.3, 1.45, 1.12)), FUR, k=0.45)
    fig.add(S.ellipsoid((0, y0 + 1.5, 0.05), (1.3, 0.75, 1.05)), FUR, k=0.45)

    # ---------- 꼬리 (바닥을 따라 뒤로, 끝이 위로 말림) ----------
    tail = [((0, y0 + 1.15, -0.9), 0.5), ((0, y0 + 0.9, -1.7), 0.68), ((0.05, y0 + 1.0, -2.5), 0.8),
            ((0.1, y0 + 1.65, -3.1), 0.85), ((0.1, y0 + 2.55, -3.2), 0.8), ((0.08, y0 + 3.25, -2.85), 0.6)]
    for (a, ra), (b, rb) in zip(tail, tail[1:]):
        m = (np.array(a) + np.array(b)) / 2
        t_ = np.array(b) - np.array(a)
        L = float(np.linalg.norm(t_))
        pitch = math.degrees(math.atan2(t_[2], t_[1]))
        fig.add(S.ellipsoid(tuple(m), ((ra + rb) * 0.62, L * 0.5 + (ra + rb) * 0.35, (ra + rb) * 0.45), R=S.rot(0, pitch, 0)), FUR, k=0.4)

    # ---------- 팔 (옆으로 쭉, 망토 끝을 잡음) ----------
    hands = []
    for s in (-1, 1):
        sh, wr = (s * 1.05, y0 + 3.05, 0.15), (s * 2.35, y0 + 3.0, 0.5)
        fig.add(S.capsule(sh, wr, 0.4, 0.3), FUR, k=0.25)
        hands.append((s, wr))
        fig.add(S.ellipsoid((s * 2.55, y0 + 3.08, 0.55), (0.24, 0.3, 0.26)), PAW, k=0.1, layer='paw')
        for j in range(4):
            a = math.radians(-35 + 23 * j)
            fig.add(S.capsule((s * 2.6, y0 + 3.1, 0.55), (s * 2.78, y0 + 3.15 + math.cos(a) * 0.3, 0.55 + math.sin(a) * 0.25), 0.09, 0.08), PAW, k=0.06, layer='paw')

    # ---------- 머리 ----------
    hc, HR = (0, y0 + 5.45, 0.1), 1.95
    fig.add(S.ellipsoid(hc, (HR * 1.04, HR * 0.95, HR)), FUR, k=0.5)
    fig.add(S.ellipsoid((0, y0 + 3.75, 0.1), (1.0, 0.55, 0.85)), FUR, k=0.5)
    for s in (-1, 1):   # 볼살
        fig.add(S.sphere((s * 1.0, hc[1] - 0.75, hc[2] + 0.95), 0.72), FUR, k=0.45)
    # 귀 (크고 둥근, 분홍 안쪽 오목)
    for s in (-1, 1):
        R = S.rot(-s * 12, -8, -s * 22)
        ec = (s * 1.35, hc[1] + 1.65, -0.1)
        ear = S.ellipsoid(ec, (0.72, 0.9, 0.34), R=R)
        inner = S.ellipsoid(tuple(np.array(ec) + R @ np.array((0, 0.05, 0.28))), (0.48, 0.66, 0.2), R=R)
        fig.add(S.subtract(ear, inner, k=0.08), FUR, k=0.3, layer='ear')
        fig.paint(S.ellipsoid(tuple(np.array(ec) + R @ np.array((0, 0.05, 0.2))), (0.52, 0.7, 0.22), R=R), EAR_IN, soft=0.06)
        fig.add(S.capsule(tuple(np.array(ec) + R @ np.array((0, -0.6, 0))), (s * 1.05, hc[1] + 0.9, -0.05), 0.35), FUR, k=0.3, layer='ear')

    # ---------- 망토: 손목 → 팔 위 → 목 뒤 → 반대 손목 (윗선) 과 아랫단 사이를 늘어뜨린 천 ----------
    yA = y0 + 3.05
    L1, L2, L3 = [], [], []
    for sd in (-1, 1):
        # 목둘레 (깃 아래) → 팔 윗선 (손목·어깨 뒤) → 아랫단
        L1.append([(sd * 0.72, y0 + 3.9, 0.8), (sd * 0.98, y0 + 3.95, 0.42), (sd * 1.08, y0 + 3.98, -0.02), (sd * 0.9, y0 + 4.0, -0.52), (sd * 0.45, y0 + 4.0, -0.88)])
        L2.append([(sd * 2.66, yA + 0.32, 0.56), (sd * 2.15, yA + 0.46, 0.44), (sd * 1.6, yA + 0.56, 0.28), (sd * 1.4, yA + 0.56, -0.5), (sd * 0.8, y0 + 3.5, -1.06)])
        L3.append([(sd * 2.66, y0 + 1.55, 0.56), (sd * 2.5, y0 + 1.9, 0.12), (sd * 2.3, y0 + 1.85, -0.4), (sd * 1.85, y0 + 1.75, -0.98), (sd * 1.0, y0 + 1.7, -1.36)])
    ring = L1[0] + [(0, y0 + 4.0, -0.95)] + L1[1][::-1]
    arm = L2[0] + [(0, y0 + 3.45, -1.15)] + L2[1][::-1]
    hem = L3[0] + [(0, y0 + 1.62, -1.48)] + L3[1][::-1]
    mo, mi = sheet(ring, arm, axis=(0, -0.1), th=0.065, nu=28, nv=3, bulge=0.12, folds=0.0)
    co, ci = sheet(arm, hem, axis=(0, -0.2), th=0.065, nu=32, nv=4, bulge=0.22)
    cape_navy = lambda P: np.minimum(mo(P), co(P))
    cape_lining = lambda P: np.minimum(mi(P), ci(P))
    fig.add(cape_navy, NAVY, k=0.0, layer='cape')
    fig.add(cape_lining, LINING, k=0.0, layer='lining')
    cape_out = cape_navy
    # 깃 (세운 남색 깃 + 금 테) + 붉은 보석 브로치
    fig.add(S.torus((0, y0 + 3.86, 0.12), 1.08, 0.2, Rm=S.rot(0, -16, 0)), NAVY, k=0.0, layer='collar')
    fig.add(S.torus((0, y0 + 4.04, 0.16), 1.11, 0.055, Rm=S.rot(0, -16, 0)), Fg.GOLD, k=0.0, layer='trim', metal=G)
    gem = (0, y0 + 3.64, 1.36)
    fig.add(S.sphere(gem, 0.2), Fg.CRIMSON, k=0.0, layer='gem')
    fig.add(S.torus(gem, 0.22, 0.06, Rm=S.rot(0, 80, 0)), Fg.GOLD, k=0.0, layer='trim', metal=G)

    # ---------- 망원경 (어깨끈, 왼쪽 허리) ----------
    t0 = np.array((-0.45, y0 + 2.7, 1.25))
    t1 = np.array((-1.25, y0 + 1.45, 1.15))
    u = (t1 - t0) / np.linalg.norm(t1 - t0)
    L = np.linalg.norm(t1 - t0)
    seg = [(0.0, 0.12, 0.25, Fg.GOLD, True), (0.12, 0.62, 0.22, LEATHER, False), (0.62, 0.72, 0.2, Fg.GOLD, True),
           (0.72, 0.9, 0.16, Fg.GOLD, True), (0.9, 0.97, 0.18, Fg.GOLD, True), (0.97, 1.08, 0.13, Fg.GOLD, True)]
    for a, b, r, col, met in seg:
        fig.add(S.capsule(tuple(t0 + u * a * L), tuple(t0 + u * b * L), r), col, k=0.0, layer='scope' if met else 'scopeleather',
                metal=G if met else None)
    fig.add(S.sphere(tuple(t0 - u * 0.02), 0.19), (0.25, 0.32, 0.45), k=0.0, layer='lens')
    strap = []
    for i in range(12):   # 오른쪽 어깨 → 망원경
        t = i / 11
        x = 1.0 - 1.45 * t
        y = y0 + 3.55 - 0.85 * t
        strap.append(hit(fig, (x, y, 4.5), (0, 0, -1)) + (0, 0, 0.0))
    strap.append(t0 + u * 0.3 + (0, 0, 0.1))
    for a, b in zip(strap, strap[1:]):
        fig.add(S.capsule(tuple(a), tuple(b), 0.065), LEATHER, k=0.02, layer='strap')
    # ---------- 얼굴 ----------
    # 흰 얼굴 가면 + 이마 줄무늬
    layer_paint(fig, 'body', S.ellipsoid((0, hc[1] - 0.25, hc[2] + 1.05), (1.95, 1.45, 1.3)), CREAM, 0.12, tol=0.08)
    layer_paint(fig, 'body', S.ellipsoid((0, hc[1] + 1.5, hc[2] + 0.9), (0.3, 1.05, 1.3)), FUR_DK, 0.2, tol=0.08)
    layer_paint(fig, 'body', S.ellipsoid((0, y0 + 2.2, 1.0), (0.95, 1.35, 0.5)), CREAM, 0.2, tol=0.08)
    layer_paint(fig, 'body', S.ellipsoid((0, y0 + 0.9, -2.0), (0.9, 0.5, 1.5)), CREAM, 0.3, tol=0.1)
    for s in (-1, 1):
        eye_on(fig, hc, s * 26, -4, size=0.6, tall=1.08, iris=EYE_DK, side=s)
    Fg.nose(fig, hc, HR + 0.02, pitch=-22, size=0.12)
    Fg.smile(fig, hc, HR + 0.03, pitch=-29, w=0.2)
    for s in (-1, 1):
        p = np.array(Fg._frame_on(hc, HR, s * 44, -25, 0).p((0, 0, 0)))
        layer_paint(fig, 'body', S.sphere(p, 0.5), Fg.BLUSH, 0.55, tol=0.1)

    # 망토 금별
    r_ = np.random.RandomState(7)
    for i in range(70):
        a = r_.uniform(-math.pi, math.pi)
        y = r_.uniform(y0 + 1.6, y0 + 3.85)
        o = np.array((math.sin(a) * 5, y, math.cos(a) * 5))
        q = hit(fig, o, (-math.sin(a), 0, -math.cos(a)))
        n = normal(fig, q)
        own = float(np.abs(cape_out(q[None])[0]))
        if own < 0.02:
            sparkle(fig, q + n * 0.012, n, r_.uniform(0.09, 0.16))

    # ---------- 받침: 금별 · 돌 ----------
    for (x, z, sz) in ((2.9, 1.8, 0.55), (-3.0, 1.6, 0.4), (3.2, -1.2, 0.35)):
        c = (x, y0 + sz * 0.95, z)
        R = S.rot(-math.degrees(math.atan2(x, z)) * 0.3, 0, 0)
        fig.add(S.ellipsoid(c, (sz * 0.28, sz, 0.12), R=R), Fg.GOLD, k=0.12, layer='star%d' % int(x * 10), metal=G)
        fig.add(S.ellipsoid(c, (sz, sz * 0.28, 0.12), R=R), Fg.GOLD, k=0.12, layer='star%d' % int(x * 10), metal=G)
    for p, r in (((-2.3, y0 + 0.05, 2.3), 0.3), ((2.2, y0 + 0.03, 2.6), 0.22), ((-3.2, y0, -0.8), 0.28), ((1.6, y0, -3.2), 0.25)):
        fig.add(S.ellipsoid(p, (r * 1.3, r * 0.7, r)), (0.6, 0.6, 0.62), k=0.0, layer='stone')
