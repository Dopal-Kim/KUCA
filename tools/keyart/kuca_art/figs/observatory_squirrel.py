"""천문대 하늘다람쥐: 회갈색·흰 얼굴, 큰 둥근 귀, 아주 큰 검은 눈, 금별 남색 망토(크림 안감·붉은 보석 브로치),
놋쇠 망원경(가죽 감개) 어깨끈, 큰 복슬 꼬리, 받침 위 금별·돌."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .clock_rooster import Fast, hit, layer_paint, eye_on, beads, rmax
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

    torso = lambda P: S.smin(S.ellipsoid((0, y0 + 2.3, 0.0), (1.3, 1.45, 1.12))(P), S.ellipsoid((0, y0 + 1.5, 0.05), (1.3, 0.75, 1.05))(P), 0.45)

    # ---------- 꼬리 (바닥을 따라 뒤로, 끝이 위로 말림) ----------
    tail = [((0, y0 + 1.15, -0.9), 0.5), ((0, y0 + 0.9, -1.7), 0.68), ((0.05, y0 + 1.0, -2.5), 0.8),
            ((0.1, y0 + 1.65, -3.1), 0.85), ((0.1, y0 + 2.55, -3.2), 0.8), ((0.08, y0 + 3.25, -2.85), 0.6)]
    for (a, ra), (b, rb) in zip(tail, tail[1:]):
        m = (np.array(a) + np.array(b)) / 2
        t_ = np.array(b) - np.array(a)
        L = float(np.linalg.norm(t_))
        pitch = math.degrees(math.atan2(t_[2], t_[1]))
        fig.add(S.ellipsoid(tuple(m), ((ra + rb) * 0.62, L * 0.5 + (ra + rb) * 0.35, (ra + rb) * 0.45), R=S.rot(0, pitch, 0)), FUR, k=0.4)

    # ---------- 팔: 몸 옆으로 내림. 왼손(-x)은 엉덩이에서 망원경을 잡고, 오른손은 살짝 벌림 ----------
    arms = {}
    for s_, el, hd in ((-1, (-1.5, y0 + 2.45, 0.3), (-1.55, y0 + 1.85, 0.75)), (1, (1.52, y0 + 2.45, 0.2), (1.78, y0 + 1.9, 0.45))):
        sh = (s_ * 1.05, y0 + 3.15, 0.05)
        fig.add(S.sphere(sh, 0.44), FUR, k=0.3)
        fig.add(S.capsule(sh, el, 0.4, 0.35), FUR, k=0.25)
        fig.add(S.capsule(el, hd, 0.35, 0.3), FUR, k=0.2)
        fig.add(S.ellipsoid(hd, (0.3, 0.32, 0.3)), PAW, k=0.1, layer='paw')
        arms[s_] = (sh, el, hd)

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

    # ---------- 짧은 남색 케이프 (어깨만 덮음, 팔꿈치·등 중간까지), 금 테두리 ----------
    def shoulders(P):
        d = S.ellipsoid((0, y0 + 2.75, -0.05), (1.32, 1.15, 1.1))(P)
        d = S.smin(d, S.ellipsoid((0, y0 + 3.75, 0.05), (0.98, 0.5, 0.82))(P), 0.4)
        for s_ in (-1, 1):
            sh, el, _ = arms[s_]
            d = S.smin(d, S.capsule(sh, el, 0.46, 0.38)(P), 0.55)
        return d

    def region(P):
        """음수 = 케이프가 있는 곳. 아랫단(앞은 조금 높고 뒤는 등 중간) + 앞 트임"""
        x, y, z = P[:, 0], P[:, 1], P[:, 2]
        hem = y0 + 2.45 + 0.25 * np.clip(z / 1.2, 0, 1) + 0.05 * np.cos(np.arctan2(x, z) * 10)
        d = hem - y
        d = np.maximum(d, y - (y0 + 3.98))
        front = np.minimum(z - 0.35, (0.32 + 0.55 * (y0 + 3.95 - y)) - np.abs(x))   # 양수 = 비움 (앞 가운데)
        return np.maximum(d, front)
    off, th = 0.13, 0.06
    cape_out = lambda P: rmax(np.abs(shoulders(P) - off) - th, region(P), 0.04)
    fig.add(cape_out, NAVY, k=0.0, layer='cape')
    # 금 테두리: 케이프 가장자리를 따라 조금 두꺼운 띠
    edge = lambda P: rmax(np.abs(shoulders(P) - off) - th - 0.035, np.abs(region(P) + 0.06) - 0.06, 0.02)
    fig.add(edge, Fg.GOLD, k=0.0, layer='capetrim', metal=G)
    # 깃 (세운 남색 깃 + 금 테) + 붉은 보석 브로치
    fig.add(S.torus((0, y0 + 3.86, 0.12), 1.08, 0.2, Rm=S.rot(0, -16, 0)), NAVY, k=0.0, layer='collar')
    fig.add(S.torus((0, y0 + 4.04, 0.16), 1.11, 0.055, Rm=S.rot(0, -16, 0)), Fg.GOLD, k=0.0, layer='trim', metal=G)
    gem = (0, y0 + 3.72, 1.2)
    fig.add(S.sphere(gem, 0.2), Fg.CRIMSON, k=0.0, layer='gem')
    fig.add(S.torus(gem, 0.22, 0.06, Rm=S.rot(0, 80, 0)), Fg.GOLD, k=0.0, layer='trim', metal=G)

    # ---------- 망원경: 왼쪽 엉덩이에서 왼손이 잡음, 오른쪽 어깨에서 대각선 갈색 끈 ----------
    hdL = np.array(arms[-1][2])
    t0 = hdL + np.array((0.5, 0.45, 0.2))         # 접안부 (위, 배 쪽)
    t1 = hdL + np.array((-0.4, -0.75, 0.5))       # 대물렌즈 (아래, 넓은 쪽)
    u = (t1 - t0) / np.linalg.norm(t1 - t0)
    L = np.linalg.norm(t1 - t0)
    seg = [(0.0, 0.1, 0.15, Fg.GOLD, True), (0.1, 0.32, 0.19, Fg.GOLD, True), (0.32, 0.38, 0.23, Fg.GOLD, True),
           (0.38, 0.76, 0.25, LEATHER, False), (0.76, 0.84, 0.29, Fg.GOLD, True), (0.84, 1.0, 0.3, Fg.GOLD, True)]
    for a, b, r, col, met in seg:
        fig.add(S.capsule(tuple(t0 + u * a * L), tuple(t0 + u * b * L), r), col, k=0.0, layer='scope' if met else 'scopeleather',
                metal=G if met else None)
    fig.add(S.sphere(tuple(t1 + u * 0.06), 0.2), (0.25, 0.32, 0.45), k=0.0, layer='lens')
    for k in range(3):   # 손가락이 가죽 부분을 감쌈
        p = t0 + u * (0.5 + k * 0.1) * L + np.array((0.0, 0.1, 0.24))
        fig.add(S.sphere(tuple(p), 0.12), PAW, k=0.06, layer='paw')
    # 끈: 오른쪽 어깨 (케이프 위) → 몸 앞 대각선 → 망원경 고리
    anchor_lo = t0 + u * 0.36 * L
    front = []
    for i in range(14):
        t = i / 13
        x = 1.05 - 2.15 * t
        y = y0 + 3.62 - 1.45 * t
        front.append(hit(fig, (x, y, 4.5), (0, 0, -1)) + np.array((0, 0, 0.03)))
    front.append(anchor_lo + np.array((0.05, 0.05, 0.18)))
    back = []
    for i in range(12):
        t = i / 11
        x = 1.05 - 2.25 * t
        y = y0 + 3.62 - 1.4 * t
        back.append(hit(fig, (x, y, -4.5), (0, 0, 1), field=lambda P: np.minimum(cape_out(P), torso(P))) + np.array((0, 0, -0.03)))
    back.append(anchor_lo + np.array((-0.05, 0.0, -0.15)))
    over = [hit(fig, (1.05, y0 + 4.8, z), (0, -1, 0), field=cape_out) + np.array((0, 0.03, 0)) for z in np.linspace(0.75, -0.75, 7)]
    for line in (front, back, over):
        for a_, b_ in zip(line, line[1:]):
            fig.add(S.capsule(tuple(a_), tuple(b_), 0.07), LEATHER, k=0.02, layer='strap')
    fig.add(S.capsule(tuple(front[0]), tuple(over[0]), 0.07), LEATHER, k=0.02, layer='strap')
    fig.add(S.capsule(tuple(back[0]), tuple(over[-1]), 0.07), LEATHER, k=0.02, layer='strap')

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
    for i in range(60):
        a = r_.uniform(-math.pi, math.pi)
        y = r_.uniform(y0 + 2.6, y0 + 3.9)
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
