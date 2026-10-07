"""천문대 하늘다람쥐: 회갈색·흰 얼굴, 큰 둥근 귀, 아주 큰 검은 눈, 금별 남색 망토(크림 안감·붉은 보석 브로치),
놋쇠 망원경(가죽 감개) 어깨끈, 큰 복슬 꼬리, 받침 위 금별·돌."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .clock_rooster import Fast, hit, layer_paint, beads, rmax, face_frame, surf_beads
from ..mesh import Frame

TIER = 'gold'

FUR = (0.66, 0.58, 0.54)
FUR_DK = (0.52, 0.44, 0.41)
CREAM = (0.98, 0.95, 0.91)
EAR_IN = (1.0, 0.72, 0.70)
PAW = (1.0, 0.85, 0.84)
NAVY = (0.12, 0.17, 0.40)
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


def normal(fig, p, e=0.01):
    p = np.asarray(p, np.float64)
    E = np.eye(3) * e
    g = np.array([fig.s.field((p + E[i])[None])[0] - fig.s.field((p - E[i])[None])[0] for i in range(3)])
    return g / np.linalg.norm(g)


def build(fig, rng):
    fig = Fast(fig)
    Fg.base(fig, rng, flowers=7)
    y0 = Fg.TOP

    # ---------- 발·다리 (작고 가는 몸) ----------
    for s in (-1, 1):
        x = s * 0.55
        fig.add(S.ellipsoid((x, y0 + 0.2, 0.36), (0.34, 0.2, 0.48)), CREAM, k=0.15)
        for j in range(3):
            a = math.radians(-28 + 28 * j)
            fig.add(S.sphere((x + math.sin(a) * 0.25, y0 + 0.13, 0.36 + math.cos(a) * 0.4), 0.11), CREAM, k=0.08)
        fig.add(S.capsule((x, y0 + 0.28, 0.12), (x * 1.05, y0 + 1.05, 0.0), 0.3, 0.4), FUR, k=0.25)

    # ---------- 몸통 (작고 날씬, 배는 크림) ----------
    T1 = ((0, y0 + 2.05, 0.0), (1.0, 1.12, 0.86))
    T2 = ((0, y0 + 1.38, 0.04), (1.0, 0.6, 0.82))
    fig.add(S.ellipsoid(*T1), FUR, k=0.4)
    fig.add(S.ellipsoid(*T2), FUR, k=0.4)

    torso = lambda P: S.smin(S.ellipsoid(*T1)(P), S.ellipsoid(*T2)(P), 0.4)

    # ---------- 꼬리 (크고 복슬, 바닥을 따라 뒤로, 끝이 위로 말림) ----------
    tail = [((0, y0 + 1.0, -0.7), 0.42), ((0, y0 + 0.82, -1.45), 0.6), ((0.05, y0 + 0.92, -2.2), 0.72),
            ((0.1, y0 + 1.5, -2.75), 0.78), ((0.1, y0 + 2.3, -2.85), 0.72), ((0.08, y0 + 2.95, -2.55), 0.54)]
    for (a, ra), (b, rb) in zip(tail, tail[1:]):
        m = (np.array(a) + np.array(b)) / 2
        t_ = np.array(b) - np.array(a)
        L = float(np.linalg.norm(t_))
        pitch = math.degrees(math.atan2(t_[2], t_[1]))
        fig.add(S.ellipsoid(tuple(m), ((ra + rb) * 0.62, L * 0.5 + (ra + rb) * 0.35, (ra + rb) * 0.45), R=S.rot(0, pitch, 0)), FUR, k=0.4)

    # ---------- 팔 (가늘게): 왼손(-x)은 엉덩이에서 망원경을 잡고, 오른손은 살짝 벌림 ----------
    arms = {}
    for s_, el, hd in ((-1, (-1.2, y0 + 2.12, 0.25), (-1.28, y0 + 1.55, 0.65)), (1, (1.22, y0 + 2.12, 0.15), (1.45, y0 + 1.6, 0.4))):
        sh = (s_ * 0.85, y0 + 2.72, 0.04)
        fig.add(S.sphere(sh, 0.34), FUR, k=0.25)
        fig.add(S.capsule(sh, el, 0.3, 0.27), FUR, k=0.2)
        fig.add(S.capsule(el, hd, 0.27, 0.24), FUR, k=0.18)
        fig.add(S.ellipsoid(hd, (0.24, 0.26, 0.24)), PAW, k=0.1, layer='paw')
        arms[s_] = (sh, el, hd)

    # ---------- 머리: 넓적한 두상 (아래 볼 쪽이 넓음, 볼 구 없음) ----------
    hc = np.array((0, y0 + 4.7, 0.1))
    fig.add(S.ellipsoid(tuple(hc), (2.08, 1.55, 1.68)), FUR, k=0.5)
    fig.add(S.ellipsoid(tuple(hc + (0, -0.48, 0.14)), (1.98, 1.08, 1.55)), FUR, k=0.55)
    fig.add(S.ellipsoid((0, y0 + 3.15, 0.08), (0.78, 0.42, 0.66)), FUR, k=0.45)
    # 귀 (크고 둥근, 분홍 안쪽 오목)
    for s in (-1, 1):
        R = S.rot(-s * 12, -8, -s * 24)
        ec = (s * 1.42, hc[1] + 1.5, -0.12)
        ear = S.ellipsoid(ec, (0.8, 1.0, 0.36), R=R)
        inner = S.ellipsoid(tuple(np.array(ec) + R @ np.array((0, 0.06, 0.3))), (0.54, 0.74, 0.22), R=R)
        fig.add(S.subtract(ear, inner, k=0.08), FUR, k=0.3, layer='ear')
        fig.paint(S.ellipsoid(tuple(np.array(ec) + R @ np.array((0, 0.06, 0.21))), (0.58, 0.78, 0.24), R=R), EAR_IN, soft=0.06)
        fig.add(S.capsule(tuple(np.array(ec) + R @ np.array((0, -0.65, 0))), (s * 1.1, hc[1] + 0.75, -0.08), 0.38), FUR, k=0.3, layer='ear')

    # ---------- 짧은 남색 케이프 (어깨만 덮음), 금 테두리 ----------
    def shoulders(P):
        d = S.ellipsoid((0, y0 + 2.3, -0.04), (1.04, 0.95, 0.88))(P)
        d = S.smin(d, S.ellipsoid((0, y0 + 3.08, 0.04), (0.8, 0.42, 0.68))(P), 0.35)
        for s_ in (-1, 1):
            sh, el, _ = arms[s_]
            d = S.smin(d, S.capsule(sh, el, 0.36, 0.32)(P), 0.45)
        return d

    def region(P):
        """음수 = 케이프가 있는 곳. 아랫단(앞은 조금 높고 뒤는 등 중간) + 앞 트임"""
        x, y, z = P[:, 0], P[:, 1], P[:, 2]
        hem = y0 + 2.02 + 0.2 * np.clip(z / 1.0, 0, 1) + 0.05 * np.cos(np.arctan2(x, z) * 10)
        d = hem - y
        d = np.maximum(d, y - (y0 + 3.3))
        front = np.minimum(z - 0.3, (0.26 + 0.5 * (y0 + 3.28 - y)) - np.abs(x))   # 양수 = 비움 (앞 가운데)
        return np.maximum(d, front)
    off, th = 0.12, 0.055
    cape_out = lambda P: rmax(np.abs(shoulders(P) - off) - th, region(P), 0.04)
    fig.add(cape_out, NAVY, k=0.0, layer='cape')
    edge = lambda P: rmax(np.abs(shoulders(P) - off) - th - 0.035, np.abs(region(P) + 0.06) - 0.06, 0.02)
    fig.add(edge, Fg.GOLD, k=0.0, layer='capetrim', metal=G)
    # 깃 (세운 남색 깃 + 금 테) + 붉은 보석 브로치
    fig.add(S.torus((0, y0 + 3.2, 0.1), 0.9, 0.18, Rm=S.rot(0, -16, 0)), NAVY, k=0.0, layer='collar')
    fig.add(S.torus((0, y0 + 3.36, 0.14), 0.93, 0.05, Rm=S.rot(0, -16, 0)), Fg.GOLD, k=0.0, layer='trim', metal=G)
    gem = (0, y0 + 3.06, 1.0)
    fig.add(S.sphere(gem, 0.18), Fg.CRIMSON, k=0.0, layer='gem')
    fig.add(S.torus(gem, 0.2, 0.055, Rm=S.rot(0, 80, 0)), Fg.GOLD, k=0.0, layer='trim', metal=G)

    # ---------- 망원경: 왼쪽 엉덩이에서 왼손이 잡음, 오른쪽 어깨에서 대각선 갈색 끈 ----------
    hdL = np.array(arms[-1][2])
    t0 = hdL + np.array((0.45, 0.4, 0.18))
    t1 = hdL + np.array((-0.35, -0.68, 0.45))
    u = (t1 - t0) / np.linalg.norm(t1 - t0)
    L = np.linalg.norm(t1 - t0)
    seg = [(0.0, 0.1, 0.13, Fg.GOLD, True), (0.1, 0.32, 0.17, Fg.GOLD, True), (0.32, 0.38, 0.21, Fg.GOLD, True),
           (0.38, 0.76, 0.22, LEATHER, False), (0.76, 0.84, 0.26, Fg.GOLD, True), (0.84, 1.0, 0.27, Fg.GOLD, True)]
    for a, b, r, col, met in seg:
        fig.add(S.capsule(tuple(t0 + u * a * L), tuple(t0 + u * b * L), r), col, k=0.0, layer='scope' if met else 'scopeleather',
                metal=G if met else None)
    fig.add(S.sphere(tuple(t1 + u * 0.05), 0.18), (0.25, 0.32, 0.45), k=0.0, layer='lens')
    for k in range(3):   # 손가락이 가죽 부분을 감쌈
        p = t0 + u * (0.5 + k * 0.1) * L + np.array((0.0, 0.1, 0.21))
        fig.add(S.sphere(tuple(p), 0.11), PAW, k=0.06, layer='paw')
    anchor_lo = t0 + u * 0.36 * L
    front = []
    for i in range(14):
        t = i / 13
        x = 0.88 - 1.55 * t
        y = y0 + 3.0 - 1.2 * t
        q = hit(fig, (x, y, 4.5), (0, 0, -1)) + np.array((0, 0, 0.03))
        if abs(q[2]) < 1.6:
            front.append(q)
    front.append(anchor_lo + np.array((0.05, 0.05, 0.16)))
    back = []
    for i in range(12):
        t = i / 11
        x = 0.88 - 1.55 * t
        y = y0 + 3.0 - 1.1 * t
        q = hit(fig, (x, y, -4.5), (0, 0, 1), field=lambda P: np.minimum(cape_out(P), torso(P))) + np.array((0, 0, -0.03))
        if abs(q[2]) < 1.6:
            back.append(q)
    side = [hit(fig, (-4.5, y0 + 1.82, z), (1, 0, 0), field=torso) + np.array((-0.03, 0, 0)) for z in np.linspace(-0.45, 0.2, 4)]
    back += side
    back.append(anchor_lo + np.array((-0.05, 0.0, -0.14)))
    over = [hit(fig, (0.88, y0 + 4.0, z), (0, -1, 0), field=cape_out) + np.array((0, 0.03, 0)) for z in np.linspace(0.65, -0.65, 7)]
    for line in (front, back, over):
        for a_, b_ in zip(line, line[1:]):
            fig.add(S.capsule(tuple(a_), tuple(b_), 0.065), LEATHER, k=0.02, layer='strap')
    fig.add(S.capsule(tuple(front[0]), tuple(over[0]), 0.065), LEATHER, k=0.02, layer='strap')
    fig.add(S.capsule(tuple(back[0]), tuple(over[-1]), 0.065), LEATHER, k=0.02, layer='strap')

    # ---------- 얼굴 ----------
    # 흰 얼굴 가면 + 이마 줄무늬
    layer_paint(fig, 'body', S.ellipsoid((0, hc[1] - 0.3, hc[2] + 1.05), (1.95, 1.4, 1.3)), CREAM, 0.12, tol=0.08)
    layer_paint(fig, 'body', S.ellipsoid((0, hc[1] + 1.35, hc[2] + 0.9), (0.28, 1.0, 1.3)), FUR_DK, 0.2, tol=0.08)
    layer_paint(fig, 'body', S.ellipsoid((0, y0 + 1.9, 0.85), (0.72, 1.05, 0.45)), CREAM, 0.2, tol=0.08)
    layer_paint(fig, 'body', S.ellipsoid((0, y0 + 0.85, -1.9), (0.8, 0.45, 1.4)), CREAM, 0.3, tol=0.1)
    # 야행성: 아주 큰 까만 눈 (big_dark), 살짝 아래·넓게
    for s in (-1, 1):
        f = face_frame(fig, hc, s * 27, -5, out=-0.085)
        Fg.eye_at(fig, f, style='big_dark', size=0.6, side=s)
    fn = face_frame(fig, hc, 0, -19)
    Fg._ellipsoid(fig.extra, tuple(np.asarray(fn.o) + np.asarray(fn.z) * 0.02), (0.13, 0.09, 0.07), Fg.PINK, 12, 6)
    for s in (-1, 1):
        surf_beads(fig, fn, [(s * (0.1 + math.cos(math.pi * k / 7) * 0.1), -0.17 - math.sin(math.pi * k / 7) * 0.085) for k in range(8)],
                   0.03, (0.30, 0.16, 0.16))
    for s in (-1, 1):
        q = np.asarray(face_frame(fig, hc, s * 47, -20).o)
        layer_paint(fig, 'body', S.sphere(q, 0.42), Fg.BLUSH, 0.5, tol=0.1)

    # 망토 금별
    r_ = np.random.RandomState(7)
    for i in range(60):
        a = r_.uniform(-math.pi, math.pi)
        y = r_.uniform(y0 + 2.1, y0 + 3.25)
        o = np.array((math.sin(a) * 5, y, math.cos(a) * 5))
        q = hit(fig, o, (-math.sin(a), 0, -math.cos(a)))
        n = normal(fig, q)
        own = float(np.abs(cape_out(q[None])[0]))
        if own < 0.02 and float(region(q[None])[0]) < -0.15:
            sparkle(fig, q + n * 0.012, n, r_.uniform(0.1, 0.15))

    # ---------- 받침: 금별 · 돌 ----------
    for (x, z, sz) in ((2.9, 1.8, 0.55), (-3.0, 1.6, 0.4), (3.2, -1.2, 0.35)):
        c = (x, y0 + sz * 0.95, z)
        R = S.rot(-math.degrees(math.atan2(x, z)) * 0.3, 0, 0)
        fig.add(S.ellipsoid(c, (sz * 0.28, sz, 0.12), R=R), Fg.GOLD, k=0.12, layer='star%d' % int(x * 10), metal=G)
        fig.add(S.ellipsoid(c, (sz, sz * 0.28, 0.12), R=R), Fg.GOLD, k=0.12, layer='star%d' % int(x * 10), metal=G)
    for p, r in (((-2.3, y0 + 0.05, 2.3), 0.3), ((2.2, y0 + 0.03, 2.6), 0.22), ((-3.2, y0, -0.8), 0.28), ((1.6, y0, -3.2), 0.25)):
        fig.add(S.ellipsoid(p, (r * 1.3, r * 0.7, r)), (0.6, 0.6, 0.62), k=0.0, layer='stone')
