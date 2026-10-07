"""한국 다람쥐 (blue): 복슬한 갈색 털 + 크림 배·주둥이, 짙은 귀 끝 털, 큰 S자 꼬리, 파란 털모자, 파란 도토리 가방(진홍 배지) 끈"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .pigeon import (axis_R, basis, surf_point, surf_frame, masked, pebble, ell, kawaii_eyes2, smile2, blush2)

TIER = 'blue'

FUR = (0.56, 0.39, 0.27)
FUR_D = (0.36, 0.24, 0.16)
FUR_L = (0.70, 0.52, 0.38)
CREAM = (0.98, 0.91, 0.79)
EAR_IN = (0.97, 0.74, 0.62)
KNIT = (0.30, 0.60, 0.96)
KNIT_HI = (0.62, 0.82, 1.0)
ACORN = (0.72, 0.42, 0.20)
ACORN_CAP = (0.55, 0.36, 0.20)


def fuzz(P, amp=0.05, f=7.0):
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    return amp * (np.sin(x * f + y * 0.7 * f) * np.sin(z * f - y * 0.5 * f) + 0.5 * np.sin((x + z) * f * 1.9 + y * 2.3))


def build(fig, rng):
    Fg.base(fig, rng, flowers=8)
    y0 = Fg.TOP
    # ---- 머리 ----
    hc = np.array((0.0, y0 + 5.85, 0.15))
    head0 = S.ellipsoid(hc, (2.02, 1.78, 1.85))
    cheeks = [S.ellipsoid((s * 1.15, y0 + 5.2, 0.8), (0.95, 0.75, 0.9)) for s in (-1, 1)]

    def head(P):
        return S.smin(S.smin(head0(P), cheeks[0](P), 0.6), cheeks[1](P), 0.6)
    # ---- 몸통 / 다리 / 팔 ----
    torso = S.ellipsoid((0, y0 + 2.6, 0.0), (1.3, 1.55, 1.15))
    belly = S.ellipsoid((0, y0 + 2.4, 0.3), (1.15, 1.25, 1.0))
    parts = []
    for s in (-1, 1):
        parts.append(S.ellipsoid((s * 0.78, y0 + 1.15, 0.0), (0.75, 0.85, 0.95)))                     # 허벅지
        parts.append(S.ellipsoid((s * 0.8, y0 + 0.28, 0.55), (0.42, 0.3, 0.78), R=S.rot(s * 10, 0, 0)))   # 발
    hands = {}
    for s in (-1, 1):
        sh = np.array((s * 1.1, y0 + 3.55, 0.15))
        el = np.array((s * 1.75, y0 + 3.0, 0.3))
        hd = np.array((s * 2.3, y0 + 2.55, 0.5))
        parts += [S.capsule(sh, el, 0.42, 0.34), S.capsule(el, hd, 0.34, 0.28), S.sphere(hd, 0.3)]
        hands[s] = hd

    def core(P):
        d = S.smin(torso(P), belly(P), 0.4)
        for g in parts:
            d = S.smin(d, g(P), 0.3)
        return S.smin(d, head(P), 0.5)
    # ---- 귀 + 귀 끝 털 ----
    ears = []
    for s in (-1, 1):
        b = np.array((s * 1.2, y0 + 7.2, -0.1))
        t = np.array((s * 1.72, y0 + 8.75, -0.15))
        Re = axis_R(t - b, (s * 0.1, 0, 1))
        cone = S.capsule(tuple(b), tuple(t), 0.72, 0.22)
        slab = S.box(tuple((b + t) / 2), (2, 2, 0.3), R=Re)
        ear = S.intersect(cone, slab)
        inner = S.capsule(tuple(b + (0, 0.25, 0.45)), tuple(t + (-s * 0.05, -0.3, 0.45)), 0.45, 0.06)
        ear = S.subtract(ear, S.intersect(inner, lambda P, b=b, Re=Re: -((P - b) @ Re[:, 2]) + 0.05), k=0.06)
        ears.append((s, ear, b, t, Re))

    def body_all(P):
        d = core(P)
        for s, e, b, t, Re in ears:
            d = S.smin(d, e(P), 0.25)
        return d
    fig.add(body_all, FUR, k=0.2)
    nb = lambda P: np.abs(core(P))
    # 귀 끝 털 (짙은 갈색, 뾰족하게 여러 가닥)
    for s, e, b, t, Re in ears:
        tb = t - (t - b) / np.linalg.norm(t - b) * 0.3
        up = np.array((s * 0.28, 1.0, -0.05)); up /= np.linalg.norm(up)
        fig.add(S.capsule(tuple(tb), tuple(tb + up * 1.2), 0.4, 0.05), FUR_D, k=0.2, layer='tuft')     # 붓 모양 중심
        for k, (dx, dz, L, r) in enumerate(((-0.42, 0.05, 0.85, 0.14), (0.42, -0.05, 0.9, 0.14), (-0.2, 0.12, 1.05, 0.15),
                                             (0.22, -0.12, 1.0, 0.15), (0.6, 0.0, 0.7, 0.12))):
            dvec = up + np.array((s * dx * 0.9, 0, dz)); dvec /= np.linalg.norm(dvec)
            fig.add(S.capsule(tuple(tb + up * 0.1), tuple(tb + up * 0.1 + dvec * L), r, 0.02), FUR_D, k=0.12, layer='tuft')
        fig.paint(masked(lambda P, b=b, t=t: S.capsule(tuple(b + (0, 0.35, 0)), tuple(t + (0, -0.3, 0)), 0.38, 0.08)(P) + ((P - b) @ Re[:, 2] < 0.0) * 1.0,
                         lambda P: np.abs(body_all(P)), 0.12), EAR_IN, soft=0.12)
        fig.paint(masked(lambda P, t=t: np.linalg.norm(P - t, axis=1) - 0.45, lambda P: np.abs(body_all(P)), 0.12), FUR_D, soft=0.3)

    # 볼 옆 복슬 털 뭉치 (바깥 아래로 삐죽)
    for s in (-1, 1):
        for k, (yw, pt, L) in enumerate(((72, -20, 0.42), (80, -8, 0.36), (62, -32, 0.38), (86, 6, 0.3))):
            p, n = surf_point(head, hc, s * yw, pt)
            dvec = n + np.array((s * 0.2, -0.6, 0.0)); dvec /= np.linalg.norm(dvec)
            fig.add(S.capsule(tuple(p - n * 0.25), tuple(p + dvec * L), 0.32, 0.06), FUR, k=0.25, layer='body')
    for k, (yw, pt) in enumerate(((-12, 82), (10, 84))):   # 정수리 털
        p, n = surf_point(head0, hc, yw, pt)
        fig.add(S.capsule(tuple(p - n * 0.1), tuple(p + n * 0.35 + np.array((yw * 0.01, 0, -0.1))), 0.15, 0.03), FUR, k=0.12, layer='body')
    # ---- 크림: 주둥이·볼 아래, 가슴·배, 눈 둘레 ----
    def muzzle(P):
        q = P - (hc + (0, -0.85, 1.25))
        return np.hypot(q[:, 0] / 1.6, q[:, 1] / 0.9) - 1.0 + np.clip(-q[:, 2], 0, None) * 0.6
    fig.paint(masked(muzzle, nb, 0.12), CREAM, soft=0.3)
    def belly_p(P):
        q = P - np.array((0, y0 + 2.4, 1.0))
        return np.hypot(q[:, 0] / 0.9, q[:, 1] / 1.55) - 1.0 + np.clip(0.4 - P[:, 2], 0, None) * 2
    fig.paint(masked(belly_p, nb, 0.12), CREAM, soft=0.3)
    # 등·정수리 조금 짙게
    fig.paint(masked(lambda P: (P[:, 2] + 0.6) * 1.5, nb, 0.12), (0.50, 0.34, 0.23), soft=1.2)

    # ---- 얼굴 ----
    kawaii_eyes2(fig, head, hc, spread=26, pitch=-4, size=0.6, iris=(0.34, 0.19, 0.09), tall=1.1)
    for s in (-1, 1):   # 살짝 찌푸린 눈썹 (안쪽이 올라감)
        p0, _ = surf_point(head, hc, s * 13, 21)
        p1, _ = surf_point(head, hc, s * 34, 16)
        fig.paint(masked(S.capsule(p0, p1, 0.1, 0.05), nb, 0.1), (0.30, 0.19, 0.12), soft=0.05)
    fn_, on_, Rn_ = surf_frame(head, hc, 0, -15, 0.02)
    ell(fig.extra, fn_, (0, 0, 0), (0.15, 0.1, 0.08), (0.96, 0.58, 0.58), 12, 6)
    smile2(fig, head, hc, pitch=-23, w=0.26)
    blush2(fig, head, hc, spread=44, pitch=-19, size=0.38, soft=0.55)

    # ---- 꼬리 (등 아래에서 뒤로 → 크게 솟아 끝이 뒤로 말림) ----
    tp = [((0.0, y0 + 1.2, -0.95), 0.55), ((0.05, y0 + 1.85, -1.9), 1.05), ((0.1, y0 + 3.0, -2.4), 1.5),
          ((0.1, y0 + 4.45, -2.5), 1.6), ((0.05, y0 + 5.6, -2.85), 1.35), ((0.0, y0 + 6.1, -3.45), 0.95), ((0.0, y0 + 5.55, -3.75), 0.55)]
    def tail(P):
        d = None
        for (a, ra), (b, rb) in zip(tp, tp[1:]):
            g = S.capsule(a, b, ra, rb)(P)
            d = g if d is None else S.smin(d, g, 0.4)
        return d + fuzz(P, 0.06, 2.6)
    fig.add(tail, FUR, k=0.3)
    fig.paint(masked(lambda P: -(np.abs(tail(P)) - 0.0) * 0 + (np.hypot(P[:, 0] - 0.4, (P[:, 2] + 2.6)) - 0.9), lambda P: np.abs(tail(P)), 0.15), FUR_L, soft=0.8)

    # ---- 파란 털모자 (머리 위, 골지 테 + 방울) ----
    top, tn = surf_point(head0, hc, 0, 75)
    Rh = axis_R(tn, (0, 0, 1))
    hcen = top + tn * 0.05
    dome = S.ellipsoid(tuple(hcen + tn * 0.15), (0.95, 0.8, 0.85), R=Rh)
    def hat(P):
        q = (P - hcen) @ Rh
        rib = 0.035 * np.cos(np.arctan2(q[:, 0], q[:, 2]) * 20)
        d = np.maximum(dome(P), -q[:, 1] - 0.05)
        brim = S.torus(tuple(hcen + tn * 0.1), 0.86, 0.22, Rm=Rh)(P) - rib
        knit = 0.025 * np.cos(np.arctan2(q[:, 0], q[:, 2]) * 16)
        return S.smin(d - knit, brim, 0.08)
    fig.add(hat, KNIT, k=0.0, layer='hat')
    pom = hcen + tn * 1.15
    fig.add(lambda P: S.sphere(tuple(pom), 0.4)(P) + fuzz(P, 0.012, 14.0), KNIT, k=0.08, layer='hat')
    fig.paint(masked(lambda P: ((P - hcen) @ Rh[:, 1]) - 0.3, lambda P: np.abs(hat(P)), 0.05), (0.24, 0.52, 0.88), soft=0.2)

    # ---- 도토리 가방 (-x 허리) + 대각선 끈 (+x 어깨 → -x 허리) ----
    shell_core = lambda P: S.smin(torso(P), belly(P), 0.4)
    pa = np.array((1.0, y0 + 4.05, 0.0)); pb = np.array((-1.3, y0 + 2.2, 0.0))
    dd = pb - pa; dd /= np.linalg.norm(dd)
    pn = np.cross(dd, (0, 0, 1)); pn /= np.linalg.norm(pn)
    for side in (0, 180):
        pts = []
        for t in np.linspace(-0.05, 1.0, 12):
            c = pa + (pb - pa) * t
            c = np.array((c[0], c[1], 0.0))
            p, n = surf_point(shell_core, c, side, 0)
            pts.append(p + n * 0.07)
        pts.append(np.array((-1.25, y0 + 2.62, 1.0)) if side == 0 else np.array((-1.55, y0 + 2.3, 0.5)))
        for a, b in zip(pts, pts[1:]):
            fig.add(S.capsule(tuple(a), tuple(b), 0.1), KNIT, k=0.05, layer='strap', metal=(KNIT, KNIT_HI))
    over = [surf_point(shell_core, np.array((pa[0] * 0.9, pa[1] - 0.15, z)), 0, 90)[0] for z in (-0.8, -0.4, 0.0, 0.4, 0.8)]
    for a, b in zip(over, over[1:]):
        fig.add(S.capsule(tuple(a + (0, 0.06, 0)), tuple(b + (0, 0.06, 0)), 0.1), KNIT, k=0.05, layer='strap', metal=(KNIT, KNIT_HI))
    bcn = np.array((-1.45, y0 + 2.0, 0.95))
    bag_body = S.ellipsoid(tuple(bcn + (0, -0.12, 0)), (0.62, 0.62, 0.5))
    fig.add(bag_body, KNIT, k=0.0, layer='bag', metal=(KNIT, KNIT_HI))
    cap = S.ellipsoid(tuple(bcn + (0, 0.25, 0)), (0.8, 0.45, 0.66))
    def cap_f(P):
        return np.maximum(cap(P), (bcn[1] + 0.05) - P[:, 1])
    fig.add(cap_f, (0.26, 0.54, 0.92), k=0.0, layer='bag2', metal=((0.26, 0.54, 0.92), KNIT_HI))
    def hatch_f(P):
        q = P - bcn
        a = np.abs(np.sin((q[:, 0] + q[:, 1]) * 9))
        b2 = np.abs(np.sin((q[:, 0] - q[:, 1]) * 9 + q[:, 2] * 4))
        return np.maximum(0.25 - np.minimum(a, b2), (np.abs(cap(P)) - 0.05) * 10)
    fig.paint(hatch_f, (0.18, 0.42, 0.80), soft=0.2)
    fig.add(S.capsule(tuple(bcn + (0, 0.6, 0)), tuple(bcn + (0.05, 0.85, 0.02)), 0.08, 0.06), KNIT, k=0.0, layer='bag3')
    # 고리: 끈 끝과 가방 연결
    fig.add(S.torus(tuple(bcn + (0.45, 0.55, -0.15)), 0.14, 0.05, Rm=S.rot(0, 0, 50)), KNIT, k=0.0, layer='bag3', metal=(KNIT, KNIT_HI))
    # 진홍 배지 (흰 도토리 문양)
    bp, bd = surf_point(bag_body, bcn + (0, -0.12, 0), 28, -4)
    bp = bp + bd * 0.03
    fig.add(S.cylinder(tuple(bp), 0.24, 0.05, round_=0.03, R=axis_R(bd)), Fg.CRIMSON, k=0.0, layer='badge')
    fig.add(S.torus(tuple(bp), 0.24, 0.035, Rm=axis_R(bd)), (0.95, 0.95, 0.97), k=0.0, layer='badge2', metal=((0.85, 0.85, 0.9), (1, 1, 1)))
    fig.paint(S.sphere(bp + bd * 0.06 - (0, 0.03, 0), 0.07), (1, 1, 1), soft=0.02)
    fig.paint(S.ellipsoid(bp + bd * 0.06 + (0, 0.05, 0), (0.1, 0.04, 0.1)), (1, 1, 1), soft=0.02)

    # ---- 받침: 도토리, 조약돌 ----
    ac = np.array((2.7, y0 + 0.42, 1.9))
    Ra = S.rot(30, 0, -60)
    fig.add(S.ellipsoid(tuple(ac), (0.38, 0.45, 0.38), R=Ra), ACORN, k=0.0, layer='acorn', metal=(ACORN, (0.95, 0.68, 0.42)))
    capc = ac + Ra[:, 1] * 0.3
    fig.add(lambda P: S.ellipsoid(tuple(capc), (0.42, 0.22, 0.42), R=Ra)(P) - 0.02 * np.cos(P[:, 0] * 30) * np.cos(P[:, 1] * 30), ACORN_CAP, k=0.0, layer='acorn2')
    fig.add(S.capsule(tuple(capc + Ra[:, 1] * 0.15), tuple(capc + Ra[:, 1] * 0.38 + Ra[:, 0] * 0.08), 0.05, 0.04), ACORN_CAP, k=0.0, layer='acorn2')
    for i, (x, z, r) in enumerate(((-3.0, 1.5, 0.32), (-2.4, 2.4, 0.2), (3.3, -0.9, 0.24), (-3.3, -1.2, 0.22), (1.2, 3.4, 0.18))):
        pebble(fig, x, z, r, seed=i)
