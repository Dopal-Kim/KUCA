"""개미 (green): 둥근 주황갈색 큰 머리, 흰자 큰 눈, 꺾인 더듬이, 잎 모자, 마디진 몸 + 진홍 가슴 점"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .pigeon import (axis_R, basis, surf_point, surf_frame, masked, pebble, ell, smile2, blush2, tuft,
                     soft_head, face_frame, blush_paint)
from .snail import leaf_fn, leaf_paints
from ..mesh import Frame

TIER = 'green'

HEAD = (0.86, 0.53, 0.30)
BODY = (0.74, 0.40, 0.20)
BODY_D = (0.50, 0.25, 0.11)
LIMB = (0.68, 0.36, 0.18)
LEAF = (0.42, 0.76, 0.16)
LEAF_HI = (0.55, 0.84, 0.28)


def dazed_eye(fig, f, size, side, iris=(0.34, 0.18, 0.08)):
    """멍한 큰 눈: 큰 흰자 + 가운데 조금 아래 eye_at('round') 홍채 (초점 없이 앞을 봄)"""
    ell(fig.extra, f, (0, 0, 0), (size, size * 1.04, size * 0.3), (0.99, 0.98, 0.97), 26, 12)
    fi = Frame(f.p((-side * size * 0.03, -size * 0.08, size * 0.2)), f.x, f.y, f.z)
    Fg.eye_at(fig, fi, 'round', size * 0.72, iris, side=side)


def build(fig, rng):
    Fg.base(fig, rng, flowers=6)
    y0 = Fg.TOP
    # ---- 머리: 몸에 비해 아주 큰 동그란 머리 (아래가 살짝 통통), 약 2.0 등신 ----
    hc = np.array((0.0, y0 + 5.3, 0.1))
    head = soft_head(hc, (1.92, 1.8, 1.78), flare=0.06, flare_y=-0.35)
    fig.add(head, HEAD, k=0.2, layer='head')
    near_head = lambda P: np.abs(head(P))
    fig.paint(masked(lambda P: (y0 + 6.3 - P[:, 1]) * 2, near_head, 0.1), (0.76, 0.43, 0.22), soft=1.0)   # 정수리 조금 짙게
    for s in (-1, 1):
        f = face_frame(fig, hc, s * 24, -6, 'head', out=-0.1)
        dazed_eye(fig, f, 0.66, s)
    # 눈썹 점 (짧은 짙은 점 두 개씩)
    for s in (-1, 1):
        for a, p in ((17, 25), (29, 23)):
            fig.paint(S.sphere(face_frame(fig, hc, s * a, p, 'head').o, 0.08), (0.62, 0.34, 0.16), soft=0.06)
    # 'o' 입 + 콧구멍 점 + 칠한 볼터치
    fm = face_frame(fig, hc, 0, -25, 'head', out=-0.03)
    ell(fig.extra, fm, (0, 0, 0), (0.12, 0.155, 0.06), (0.30, 0.10, 0.10), 14, 8)
    ell(fig.extra, fm, (0, -0.055, 0.02), (0.075, 0.065, 0.05), (0.85, 0.40, 0.42), 10, 6)
    for s in (-1, 1):
        fig.paint(S.sphere(face_frame(fig, hc, s * 3, -13, 'head').o, 0.04), (0.45, 0.22, 0.10), soft=0.03)
    blush_paint(fig, hc, 42, -20, 'head', size=0.4, soft=0.55)
    # ---- 더듬이 (이마 위에서 솟아 → 바깥으로 꺾이고 끝이 둥글다) ----
    for s in (-1, 1):
        fa = face_frame(fig, hc, s * 16, 40, 'head')
        a, an = np.array(fa.o), np.array(fa.z)
        b = np.array((s * 0.75, y0 + 7.35, 1.75))
        c2 = np.array((s * 1.05, y0 + 8.25, 1.9))
        d = np.array((s * 1.55, y0 + 8.5, 1.95))
        for p, q in ((a - an * 0.2, b), (b, c2), (c2, d)):
            fig.add(S.capsule(tuple(p), tuple(q), 0.13, 0.12), LIMB, k=0.12, layer='ant')
        fig.add(S.sphere(tuple(d), 0.16), LIMB, k=0.1, layer='ant')
    # ---- 잎 모자: 머리를 두건처럼 덮음 (잎맥이 앞→뒤, 뒤쪽 끝이 목덜미까지, +x 쪽이 조금 더 내려옴) ----
    O = np.array((0.05, y0 + 7.55, -0.3))
    dvec = np.array((-0.12, 0.0, 1.0)); dvec /= np.linalg.norm(dvec)
    R = np.stack([np.cross((0, 1, 0), dvec), np.array((0, 1.0, 0)), dvec], axis=1)
    R = R @ S.rot(0, 0, 7)
    u0, u1 = -2.55, 1.95
    lf, local, height, width, to_world = leaf_fn(O, R, u0, u1, 2.5, 0.3, 0.58, 0.27, th=0.1, rib=0.06, tip_pow=0.9)
    fig.add(lf, LEAF, k=0.0, layer='leaf')
    leaf_paints(fig, lf, local, height, width, u0, u1, under=(0.64, 0.86, 0.38), vein=LEAF_HI, spacing=0.6)
    pb, nb = to_world(u0 + 0.05, 0.0)
    fig.add(S.capsule(tuple(pb), tuple(pb + np.array((0.05, -0.4, -0.05))), 0.1, 0.08), (0.46, 0.74, 0.22), k=0.05, layer='leaf')
    # ---- 몸: 가는 목 → 작은 가슴(진홍 점) → 아주 가는 허리 마디 → 둥근 배 + 통통한 허벅지 ----
    thor = S.ellipsoid((0, y0 + 2.9, 0.08), (0.55, 0.6, 0.5))
    neck = S.capsule((0, y0 + 3.2, 0.08), (0, y0 + 3.75, 0.1), 0.24)
    waist = S.capsule((0, y0 + 2.45, 0.02), (0, y0 + 2.2, -0.1), 0.2)
    gaster = S.ellipsoid((0, y0 + 1.6, -0.55), (0.95, 0.9, 1.0))
    thighs = [S.ellipsoid((s * 0.48, y0 + 1.45, 0.22), (0.56, 0.66, 0.6)) for s in (-1, 1)]
    def body(P):
        d = S.smin(S.smin(thor(P), neck(P), 0.15), waist(P), 0.12)
        g = S.smin(gaster(P), S.smin(thighs[0](P), thighs[1](P), 0.12), 0.25)
        yy = P[:, 1] - (y0 + 1.5)
        groove = 0.045 * np.exp(-((yy - 0.38) / 0.06) ** 2) + 0.045 * np.exp(-((yy + 0.22) / 0.06) ** 2)
        g = g + groove
        return S.smin(d, g, 0.14)
    fig.add(body, BODY, k=0.2, layer='ant')
    near_b = lambda P: np.abs(body(P))
    for yy in (0.38, -0.22):
        fig.paint(masked(lambda P, yy=yy: np.abs(P[:, 1] - (y0 + 1.5 + yy)) - 0.05, near_b, 0.05), BODY_D, soft=0.08)
    cp, cn = surf_point(thor, (0, y0 + 2.9, 0.08), 0, 0)
    fig.add(S.sphere(tuple(cp + cn * 0.02), 0.15), (0.78, 0.08, 0.10), k=0.0, layer='dot')
    # ---- 팔 (가는 팔, 바깥 아래로, 손가락 3) ----
    for s in (-1, 1):
        sh = np.array((s * 0.45, y0 + 3.05, 0.08))
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
