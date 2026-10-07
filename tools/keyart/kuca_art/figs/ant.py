"""개미 (green): 둥근 주황갈색 큰 머리, 흰자 큰 눈, 꺾인 더듬이, 잎 모자, 마디진 몸 + 진홍 가슴 점"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .pigeon import (axis_R, basis, surf_point, surf_frame, masked, pebble, ell, smile2, blush2, tuft)
from .snail import leaf_fn, leaf_paints

TIER = 'green'

HEAD = (0.86, 0.53, 0.30)
BODY = (0.74, 0.40, 0.20)
BODY_D = (0.50, 0.25, 0.11)
LIMB = (0.68, 0.36, 0.18)
LEAF = (0.42, 0.76, 0.16)
LEAF_HI = (0.55, 0.84, 0.28)


def googly_eye(fig, fn, hc, s, yaw, pitch, size, iris=(0.36, 0.20, 0.10), look=(0.0, 0.0), sink=0.12):
    f, o, R = surf_frame(fn, hc, s * yaw, pitch, -size * sink)
    b = fig.extra
    lx, ly = look[0] * s, look[1]
    ell(b, f, (0, 0, 0), (size, size * 1.06, size * 0.36), (0.99, 0.98, 0.97), 24, 12)
    ell(b, f, (lx * size, ly * size, size * 0.2), (size * 0.66, size * 0.7, size * 0.18), iris, 22, 11)
    ell(b, f, (lx * size, ly * size - size * 0.3, size * 0.26), (size * 0.44, size * 0.3, size * 0.14),
        tuple(min(1, c * 1.6) for c in iris), 16, 8)
    ell(b, f, (lx * size, ly * size + size * 0.04, size * 0.3), (size * 0.36, size * 0.4, size * 0.12), Fg.PUPIL, 18, 9)
    ell(b, f, (lx * size - size * 0.2 * s, ly * size + size * 0.25, size * 0.38), (size * 0.18, size * 0.18, size * 0.06), Fg.SHINE, 12, 6)
    ell(b, f, (lx * size + size * 0.24 * s, ly * size - size * 0.28, size * 0.36), (size * 0.08, size * 0.08, size * 0.04), Fg.SHINE, 8, 4)
    return f, o, R


def build(fig, rng):
    Fg.base(fig, rng, flowers=6)
    y0 = Fg.TOP
    # ---- 머리 ----
    hc = np.array((0.0, y0 + 5.35, 0.1))
    head = S.ellipsoid(hc, (2.0, 1.78, 1.82))
    fig.add(head, HEAD, k=0.2, layer='head')
    near_head = lambda P: np.abs(head(P))
    fig.paint(masked(lambda P: (y0 + 6.3 - P[:, 1]) * 2, near_head, 0.1), (0.76, 0.43, 0.22), soft=1.0)   # 정수리 조금 짙게
    googly_eye(fig, head, hc, -1, 25, -7, 0.68, look=(0.1, -0.05))
    googly_eye(fig, head, hc, 1, 25, -7, 0.68, look=(0.1, -0.05))
    # 눈썹 점 (짧은 짙은 점 두 개씩)
    for s in (-1, 1):
        for a, p in ((18, 24), (30, 22)):
            pp, _ = surf_point(head, hc, s * a, p)
            fig.paint(S.sphere(pp, 0.08), (0.62, 0.34, 0.16), soft=0.06)
    # 'o' 입 + 콧구멍 점 + 볼터치
    fm, om, Rm = surf_frame(head, hc, 0, -24, -0.02)
    ell(fig.extra, fm, (0, 0, 0), (0.13, 0.17, 0.06), (0.30, 0.10, 0.10), 14, 8)
    ell(fig.extra, fm, (0, -0.06, 0.02), (0.08, 0.07, 0.05), (0.85, 0.40, 0.42), 10, 6)
    for s in (-1, 1):
        pp, _ = surf_point(head, hc, s * 3, -12)
        fig.paint(S.sphere(pp, 0.04), (0.45, 0.22, 0.10), soft=0.03)
    blush2(fig, head, hc, spread=40, pitch=-20, size=0.45, soft=0.55)
    # ---- 더듬이 (위로 → 바깥으로 꺾여 끝이 둥글다) ----
    for s in (-1, 1):
        a, an = surf_point(head, hc, s * 17, 36)
        b = np.array((s * 0.8, y0 + 6.75, 2.45))
        c = np.array((s * 1.05, y0 + 7.85, 2.55))
        c2 = np.array((s * 1.45, y0 + 8.4, 2.35))
        d = np.array((s * 1.9, y0 + 8.3, 2.1))
        for p, q in ((a - an * 0.2, b), (b, c), (c, c2), (c2, d)):
            fig.add(S.capsule(tuple(p), tuple(q), 0.13, 0.12), LIMB, k=0.12, layer='ant')
        fig.add(S.sphere(tuple(d), 0.17), LIMB, k=0.1, layer='ant')
    # ---- 잎 모자 (머리 위에 덮여 뒤로 늘어지고, 앞 오른쪽 끝이 처짐) ----
    O = np.array((-0.15, y0 + 7.7, -0.25))
    dvec = np.array((1.0, 0.0, -0.12)); dvec /= np.linalg.norm(dvec)
    R = np.stack([np.cross((0, 1, 0), dvec), np.array((0, 1.0, 0)), dvec], axis=1)
    R = R @ S.rot(0, 0, 0)
    u0, u1 = -2.9, 3.3
    lf, local, height, width, to_world = leaf_fn(O, R, u0, u1, 2.85, 0.36, 0.09, 0.14, th=0.09, rib=0.06, tip_pow=0.8, k_cup_pos=0.42)
    fig.add(lf, LEAF, k=0.0, layer='leaf')
    leaf_paints(fig, lf, local, height, width, u0, u1, under=(0.64, 0.86, 0.38), vein=LEAF_HI, spacing=0.7)
    pb, nb = to_world(u0 + 0.05, 0.0)
    fig.add(S.capsule(tuple(pb), tuple(pb + np.array((0.0, -0.35, -0.2))), 0.1, 0.08), (0.46, 0.74, 0.22), k=0.05, layer='leaf')
    # ---- 몸: 가슴(진홍 점) + 마디진 배 (앞 두 볼록 + 뒤 배) ----
    thor = S.ellipsoid((0, y0 + 2.85, 0.05), (0.62, 0.62, 0.55))
    neck = S.capsule((0, y0 + 3.2, 0.05), (0, y0 + 3.8, 0.1), 0.32)
    gaster = S.ellipsoid((0, y0 + 1.75, -0.55), (1.0, 0.95, 1.05))
    thighs = [S.ellipsoid((s * 0.5, y0 + 1.55, 0.25), (0.62, 0.72, 0.66)) for s in (-1, 1)]
    def body(P):
        d = S.smin(thor(P), neck(P), 0.2)
        g = S.smin(gaster(P), S.smin(thighs[0](P), thighs[1](P), 0.15), 0.3)
        # 마디 홈 (가로 띠)
        yy = P[:, 1] - (y0 + 1.6)
        groove = 0.045 * np.exp(-((yy - 0.38) / 0.06) ** 2) + 0.045 * np.exp(-((yy + 0.22) / 0.06) ** 2)
        g = g + groove
        return S.smin(d, g, 0.2)
    fig.add(body, BODY, k=0.2, layer='ant')
    near_b = lambda P: np.abs(body(P))
    for yy in (0.38, -0.22):
        fig.paint(masked(lambda P, yy=yy: np.abs(P[:, 1] - (y0 + 1.6 + yy)) - 0.05, near_b, 0.05), BODY_D, soft=0.08)
    cp, cn = surf_point(thor, (0, y0 + 2.85, 0.05), 0, 0)
    fig.add(S.sphere(tuple(cp + cn * 0.02), 0.16), (0.78, 0.08, 0.10), k=0.0, layer='dot')
    # ---- 팔 (가는 팔, 바깥 아래로, 손가락 3) ----
    for s in (-1, 1):
        sh = np.array((s * 0.5, y0 + 3.0, 0.05))
        el = np.array((s * 1.25, y0 + 2.72, 0.15))
        wr = np.array((s * 1.75, y0 + 2.42, 0.35))
        fig.add(S.capsule(tuple(sh), tuple(el), 0.21, 0.18), LIMB, k=0.1, layer='ant')
        fig.add(S.capsule(tuple(el), tuple(wr), 0.18, 0.16), LIMB, k=0.1, layer='ant')
        fig.add(S.sphere(tuple(wr + (s * 0.08, -0.05, 0.03)), 0.2), LIMB, k=0.1, layer='ant')
        for a in (-35, 0, 35):
            dd = np.array((s * math.cos(math.radians(a)), -0.35, math.sin(math.radians(a)))); dd /= np.linalg.norm(dd)
            fig.add(S.capsule(tuple(wr + (s * 0.1, -0.05, 0.03)), tuple(wr + (s * 0.1, -0.05, 0.03) + dd * 0.32), 0.08, 0.075), LIMB, k=0.06, layer='ant')
    # ---- 다리 (짧고 가는 다리 + 둥근 발) ----
    for s in (-1, 1):
        top = np.array((s * 0.55, y0 + 1.0, 0.3))
        ft = np.array((s * 0.62, y0 + 0.22, 0.38))
        fig.add(S.capsule(tuple(top), tuple(ft), 0.17, 0.15), LIMB, k=0.1, layer='ant')
        fig.add(S.ellipsoid(tuple(ft + (0, -0.04, 0.12)), (0.24, 0.18, 0.36)), BODY_D, k=0.1, layer='ant')
    # ---- 받침: 둥근 회색 돌, 풀 덤불, 클로버 ----
    for i, (x, z, r) in enumerate(((-2.4, 1.7, 0.36), (2.5, 1.6, 0.34), (-3.2, -1.5, 0.25), (3.0, -1.7, 0.22))):
        fig.add(S.ellipsoid((x, y0 + r * 0.35, z), (r, r * 0.75, r * 0.9)), (0.66, 0.66, 0.69), k=0.0, layer='stone',
                metal=((0.62, 0.62, 0.66), (0.9, 0.9, 0.93)))
    for (x, z) in ((-3.3, 0.4), (3.3, 0.5), (-1.3, 3.2), (1.5, 3.1), (0.0, -3.3)):
        tuft(fig, x, z, rng, n=4, h=0.6, col=(0.33, 0.64, 0.18))
    for (x, z) in ((-1.9, 2.9), (2.0, 2.7), (-3.4, -0.6)):
        for k in range(3):
            a = k * 2 * math.pi / 3 + 0.3
            fig.add(S.ellipsoid((x + math.cos(a) * 0.16, y0 + 0.25, z + math.sin(a) * 0.16), (0.16, 0.04, 0.16)), (0.38, 0.70, 0.22), k=0.03, layer='clover')
