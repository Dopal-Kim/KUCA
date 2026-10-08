"""캠퍼스 길고양이 (blue): 주황 치즈태비 + 흰 주둥이·가슴·발, 왼쪽 귀 끝 TNR 컷, 파란 목줄 + 금방울 + 진홍 이름표, 파란 생선 장난감"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .pigeon import (axis_R, basis, surf_point, surf_frame, masked, pebble, ell, kawaii_eyes2, smile2, blush2,
                     soft_head, face_frame, eye_pair, mouth_w, blush_paint)

TIER = 'blue'

FUR = (0.98, 0.69, 0.33)
STRIPE = (0.87, 0.43, 0.10)
WHITE = (0.995, 0.975, 0.95)
EAR_IN = (0.99, 0.70, 0.73)
PAD = (0.98, 0.62, 0.66)
TOY = (0.30, 0.62, 0.96)
TOY_HI = (0.62, 0.84, 1.0)
COLLAR = (0.25, 0.58, 0.95)


def build(fig, rng):
    Fg.base(fig, rng, flowers=8)
    y0 = Fg.TOP
    # ---- 머리 ----
    # 넓적한 고양이 두상: 볼 쪽(아래 옆)이 넓은 한 덩어리 (따로 붙인 볼 없음), 위는 살짝 평평
    hc = np.array((0.0, y0 + 6.25, 0.1))
    head_parts = [soft_head(hc, (2.12, 1.72, 1.8), flare=0.14, taper=0.05, flare_y=-0.45)]
    # ---- 몸통 / 다리 / 팔: 날씬하고 유연한 몸, 다리가 조금 긴 편 (약 2.2 등신) ----
    torso = S.ellipsoid((0, y0 + 2.95, 0.0), (1.1, 1.55, 0.95))
    chest = S.ellipsoid((0, y0 + 3.5, 0.28), (0.88, 0.9, 0.8))
    legs = []
    for s in (-1, 1):
        legs.append(S.ellipsoid((s * 0.6, y0 + 1.55, 0.02), (0.56, 0.85, 0.64)))
        legs.append(S.capsule((s * 0.6, y0 + 1.2, 0.15), (s * 0.63, y0 + 0.42, 0.28), 0.4, 0.37))
        legs.append(S.ellipsoid((s * 0.64, y0 + 0.31, 0.48), (0.44, 0.3, 0.56)))
    sh_l, sh_r = np.array((-0.95, y0 + 3.95, 0.12)), np.array((0.95, y0 + 3.95, 0.12))
    paw_l = np.array((-1.85, y0 + 3.35, 0.95))
    el_r = np.array((1.75, y0 + 3.25, 0.5))
    paw_r = np.array((2.1, y0 + 4.0, 1.0))
    arms = [S.capsule(sh_l, paw_l, 0.4, 0.34), S.ellipsoid(tuple(paw_l), (0.42, 0.44, 0.32), R=basis((-0.3, 0.1, 1.0))),
            S.capsule(sh_r, el_r, 0.4, 0.35), S.capsule(el_r, paw_r, 0.35, 0.34), S.sphere(paw_r, 0.39)]
    # 꼬리 (뒤에서 +x 쪽으로 감아 올림)
    tail_pts = [np.array(p) for p in ((0.1, y0 + 1.7, -0.8), (0.55, y0 + 1.2, -1.6), (0.9, y0 + 1.7, -2.2),
                                       (0.95, y0 + 2.7, -2.3), (0.7, y0 + 3.5, -2.0))]
    tail = [S.capsule(tuple(a), tuple(b), 0.36, 0.38) for a, b in zip(tail_pts, tail_pts[1:])]
    tail.append(S.sphere(tuple(tail_pts[-1]), 0.4))

    def core(P):
        d = S.smin(torso(P), chest(P), 0.5)
        for g in legs:
            d = S.smin(d, g(P), 0.3)
        for g in arms:
            d = S.smin(d, g(P), 0.25)
        d = S.smin(d, head_parts[0](P), 0.5)
        t = tail[0](P)
        for g in tail[1:]:
            t = S.smin(t, g(P), 0.3)
        return S.smin(d, t, 0.25)

    # ---- 귀 (세모, 안쪽 분홍 오목) — 왼쪽(+x) 귀 끝은 TNR 컷 ----
    ear_fs = []
    for s in (-1, 1):
        b = np.array((s * 1.25, y0 + 7.35, -0.05))
        t = np.array((s * 1.9, y0 + 9.05, -0.05))
        Re = axis_R(t - b, (s * 0.15, 0, 1))
        cone = S.capsule(tuple(b), tuple(t), 0.95, 0.1)
        slab = S.box(tuple((b + t) / 2), (2, 2, 0.32), round_=0.0, R=Re)
        ear = S.intersect(cone, slab)
        inner = S.capsule(tuple(b + (0, 0.3, 0.5)), tuple(t + (-s * 0.12, -0.35, 0.5)), 0.62, 0.04)
        ear = S.subtract(ear, S.intersect(inner, lambda P, b=b, Re=Re: -((P - b) @ Re[:, 2]) + 0.05), k=0.08)
        if s == 1:   # TNR: 끝을 평평하게 자름
            cut = S.box(tuple(t + (0, 0.1, 0)), (0.7, 0.5, 0.7), R=axis_R(t - b, (0, 0, 1)))
            ear = S.subtract(ear, cut, k=0.05)
        ear_fs.append((s, ear, b, t, Re))

    def body_all(P):
        d = core(P)
        for s, e, b, t, Re in ear_fs:
            d = S.smin(d, e(P), 0.3)
        return d
    fig.add(body_all, FUR, k=0.2)
    nb = lambda P: np.abs(body_all(P))

    # ---- 무늬: 줄무늬 (뒤·옆, 머리 위) ----
    # 줄무늬: 거리장으로 정의한 굵은 태비 줄 (칠 가장자리를 짧게 → 또렷)
    def band_d(u, period, w):
        t = u / period
        return np.abs(t - np.round(t)) * period - w

    def taper(v, a, b):      # 영역 끝에서 줄 폭을 0 으로 (줄 끝이 뾰족하게 깔끔히 끝남)
        return np.clip((v - a) / (b - a), 0, 1) ** 0.6

    def stripes_body(P):     # 몸통: 고른 폭의 가로 띠가 등에서 옆으로 돌아 흰 배 앞에서 뾰족하게 끝남
        y = P[:, 1]
        back = -P[:, 2] + 0.55 - 0.15 * np.abs(P[:, 0])
        w = 0.12 * taper(back, 0.25, 0.85)
        d = band_d(y - (y0 + 2.0) - 0.12 * np.abs(P[:, 0]), 0.62, 0) - w
        return np.maximum(d, (y - (y0 + 4.2)) * 3)

    head0 = head_parts[0]
    def stripes_head(P):     # 머리: 정수리~뒤통수 세로 줄 3 + 옆 가로 줄 (얼굴 앞은 깨끗하게)
        q = P - hc
        th = np.arctan2(q[:, 0], q[:, 2])
        ath = np.abs(th)
        # 옆·뒤 가로 줄: 머리 둘레를 따라 고른 폭
        side_w = 0.11 * taper(ath, 1.05, 1.5)
        d_side = band_d(q[:, 1] + 0.35, 0.58, 0) - side_w
        d_side = np.maximum(d_side, q[:, 1] - 0.9)
        # 정수리 세로 줄 (앞이마 → 뒤통수, x 방향 간격 고르게)
        top_w = 0.1 * taper(q[:, 1], 0.55, 1.1)
        d_top = np.minimum(np.abs(q[:, 0]) - top_w, np.abs(np.abs(q[:, 0]) - 0.48) - top_w * 0.85)
        d_top = np.maximum(d_top, (-0.7 - q[:, 2]) * 2)     # 정수리 세로 줄은 뒤통수 위쪽에서 끝남
        return np.minimum(d_side, d_top)
    fig.paint(masked(stripes_body, nb, 0.1), STRIPE, soft=0.2)
    fig.paint(masked(stripes_head, lambda P: np.abs(head0(P)), 0.12), STRIPE, soft=0.2)
    for s in (-1, 1):     # 볼 옆 짧은 가로 줄 2 개
        for k, pt in enumerate((-4, -16)):
            p0 = np.array(face_frame(fig, hc, s * 64, pt, 'body').o)
            p1 = np.array(face_frame(fig, hc, s * 86, pt + 2, 'body').o)
            fig.paint(masked(S.capsule(p0, p1, 0.075, 0.03), nb, 0.1), STRIPE, soft=0.2)

    def min_tail(P):
        t = tail[0](P)
        for g in tail[1:]:
            t = np.minimum(t, g(P))
        return t
    for k in range(4):     # 꼬리 고리 (축에 수직, 고른 폭)
        a, b = tail_pts[k], tail_pts[k + 1]
        c = (a + b) / 2
        n = (b - a) / np.linalg.norm(b - a)
        fig.paint(masked(lambda P, c=c, n=n: np.abs((P - c) @ n) - 0.11, lambda P: np.abs(min_tail(P)), 0.1), STRIPE, soft=0.2)

    # 팔·허벅지 줄 (팔다리 축에 수직인 고른 띠, 앞쪽 흰 면 전에 끝남)
    for a_, b_ in ((sh_l, paw_l), (sh_r, el_r), (el_r, paw_r)):
        L = np.linalg.norm(b_ - a_)
        n = (b_ - a_) / L
        for t in (0.32, 0.66):
            c = a_ + (b_ - a_) * t
            fig.paint(masked(lambda P, c=c, n=n: np.maximum(np.abs((P - c) @ n) - 0.08, (P[:, 2] - c[2] - 0.25) * 4), nb, 0.08), STRIPE, soft=0.2)
    for s in (-1, 1):
        for yy in (y0 + 1.05, y0 + 1.6):
            fig.paint(masked(lambda P, s=s, yy=yy: np.maximum(np.abs(P[:, 1] - yy) - 0.09, (0.3 - s * P[:, 0]) * 4 + np.clip(P[:, 2] - 0.3, 0, None) * 4), nb, 0.08), STRIPE, soft=0.2)
    # ---- 흰 부분: 주둥이·볼 아래, 이마 가운데, 가슴·배, 발, 꼬리 끝 ----
    def muzzle(P):
        q = P - (hc + (0, -0.8, 1.25))
        return np.hypot(q[:, 0] / 1.7, q[:, 1] / 1.0) - 1.0 + np.clip(-q[:, 2], 0, None) * 0.8
    fig.paint(masked(muzzle, nb, 0.1), WHITE, soft=0.08)
    fig.paint(masked(lambda P: np.hypot(P[:, 0] / (0.12 + 0.3 * np.clip((hc[1] + 0.75 - P[:, 1]) / 0.8, 0, 1)), (P[:, 1] - (hc[1] - 0.1)) / 0.85) - 1.0 + np.clip(1.4 - P[:, 2], 0, None) * 2, nb, 0.1), WHITE, soft=0.1)
    def belly(P):
        q = P - np.array((0, y0 + 2.9, 1.0))
        return np.hypot(q[:, 0] / 0.95, q[:, 1] / 1.75) - 1.0 + np.clip(0.3 - P[:, 2], 0, None) * 2
    fig.paint(masked(belly, nb, 0.1), WHITE, soft=0.08)
    for s in (-1, 1):
        fig.paint(masked(lambda P, s=s: (P[:, 1] - (y0 + 0.62)) * 3 + np.abs(P[:, 0] - s * 0.75) * 0.5 - 0.3, nb, 0.1), WHITE, soft=0.06)
    fig.paint(masked(lambda P: np.linalg.norm(P - paw_l, axis=1) - 0.5, nb, 0.1), WHITE, soft=0.12)
    fig.paint(masked(lambda P: np.linalg.norm(P - paw_r, axis=1) - 0.52, nb, 0.1), WHITE, soft=0.12)
    fig.paint(masked(lambda P: np.linalg.norm(P - tail_pts[-1], axis=1) - 0.62, nb, 0.1), WHITE, soft=0.06)
    # 귀 안쪽 분홍 + 흰 솜털
    for s, e, b, t, Re in ear_fs:
        inner_c = (b + t) / 2 + Re[:, 2] * 0.1
        fig.paint(masked(lambda P, b=b, t=t, Re=Re: S.capsule(tuple(b + (0, 0.25, 0.0)), tuple(t + (0, -0.4, 0.0)), 0.5, 0.06)(P) + ((P - b) @ Re[:, 2] < 0.0) * 1.0, nb, 0.1), EAR_IN, soft=0.12)
    # TNR 컷 끝 흰 털 뭉치
    s, e, b, t, Re = ear_fs[1]
    tipc = t - (t - b) / np.linalg.norm(t - b) * 0.42
    for k, (dx, dy) in enumerate(((-0.22, 0.05), (0.0, 0.14), (0.2, 0.04))):
        p = tipc + Re[:, 0] * dx + Re[:, 1] * dy
        fig.add(S.capsule(tuple(p - Re[:, 1] * 0.1), tuple(p + Re[:, 1] * 0.16 + Re[:, 0] * dx * 0.4), 0.13, 0.06), WHITE, k=0.1, layer='tuft')

    # ---- 얼굴 ----
    face_all = head_parts[0]
    # 고양이 눈: 호박색 큰 홍채 + 세로 동공, 눈꼬리 살짝 올라감
    eye_pair(fig, hc, 28, -4, 'cat', 0.56, iris=(0.88, 0.56, 0.12), layer='body', tilt=8, sink=0.15)
    fn_ = face_frame(fig, hc, 0, -17, 'body', out=-0.02)
    Fg._ellipsoid(fig.extra, (0, 0.02, 0), (0.15, 0.1, 0.08), PAD, 12, 6, fn_)
    mouth_w(fig, face_frame(fig, hc, 0, -24, 'body', out=0.0), w=0.3)
    blush_paint(fig, hc, 45, -20, 'body', size=0.45, soft=0.6)
    # 수염 (가는 흰 선, 양쪽 3가닥)
    for s in (-1, 1):
        for k, (dy, ang) in enumerate(((0.12, 8), (0.0, 0), (-0.12, -8))):
            ff = face_frame(fig, hc, s * 40, -18 + k * -3, 'body')
            p0, n0 = np.array(ff.o), np.array(ff.z)
            dvec = np.array((s * math.cos(math.radians(ang)), math.sin(math.radians(ang)), 0.25))
            dvec /= np.linalg.norm(dvec)
            for j in range(22):
                q = p0 + dvec * (j * 0.05) + n0 * 0.02 + np.array((0, dy - 0.0012 * j * j * 0.3, 0))
                Fg._ellipsoid(fig.extra, tuple(q), (0.045, 0.018, 0.018), (1.0, 1.0, 1.0), 6, 3)

    # ---- 발바닥 (들어 올린 -x 앞발, 앞을 향함) ----
    pn = np.array((-0.3, 0.1, 1.0)); pn /= np.linalg.norm(pn)
    Rp = basis(pn)
    def on_paw(dx, dy):
        p, n = surf_point(core, paw_l + Rp[:, 0] * dx + Rp[:, 1] * dy - pn * 0.3, 0, 0) if False else (None, None)
        q = paw_l + Rp[:, 0] * dx + Rp[:, 1] * dy
        lo, hi = 0.0, 1.5
        for _ in range(30):
            m = (lo + hi) / 2
            if core((q + pn * m)[None, :])[0] < 0:
                lo = m
            else:
                hi = m
        return q + pn * lo
    c0 = on_paw(0, -0.1)
    fig.add(S.ellipsoid(tuple(c0), (0.21, 0.17, 0.09), R=Rp), PAD, k=0.0, layer='pad')
    for k, (dx, dy) in enumerate(((-0.24, 0.13), (-0.09, 0.24), (0.07, 0.24), (0.22, 0.13))):
        fig.add(S.ellipsoid(tuple(on_paw(dx, dy)), (0.08, 0.085, 0.06), R=Rp), PAD, k=0.0, layer='pad')

    # ---- 목줄 (몸 표면을 따라 도는 파란 띠) + 금방울 + 진홍 이름표 ----
    yc = y0 + 4.42
    neck = lambda P: S.smin(S.smin(torso(P), chest(P), 0.5), head_parts[0](P), 0.55)
    ax_ = surf_point(neck, (0, yc, 0.0), 90, 0)[0][0]
    az_ = surf_point(neck, (0, yc, 0.0), 0, 0)[0][2]
    azb = -surf_point(neck, (0, yc, 0.0), 180, 0)[0][2]
    def collar(P):
        az = np.where(P[:, 2] > 0, az_, azb)
        rr = np.hypot(P[:, 0] / ax_, P[:, 2] / az)
        d = (rr - 1.0) * np.minimum(ax_, az) - 0.08
        return np.hypot(np.maximum(np.abs(P[:, 1] - yc) - 0.12, 0), np.maximum(d, 0)) + np.minimum(np.maximum(np.abs(P[:, 1] - yc) - 0.12, d), 0) - 0.07
    fig.add(collar, COLLAR, k=0.0, layer='collar', metal=(COLLAR, (0.7, 0.88, 1.0)))
    cp, cn = surf_point(collar, (0, yc, 0), 0, 0)
    bell = cp + np.array((0, -0.3, 0.12))
    fig.add(S.sphere(tuple(bell), 0.28), Fg.GOLD, k=0.0, layer='bell', metal=(Fg.GOLD, Fg.GOLD_HI))
    fig.add(S.torus(tuple(bell + (0, 0.27, -0.02)), 0.09, 0.035, Rm=S.rot(0, 90, 0) @ S.rot(0, 0, 90)), Fg.GOLD, k=0.0, layer='bell2', metal=(Fg.GOLD, Fg.GOLD_HI))
    fig.add(S.box(tuple(bell + (0, 0.0, 0.0)), (0.3, 0.03, 0.4)), (0.35, 0.22, 0.05), k=0.0, layer='bell3')
    fig.add(S.sphere(tuple(bell + (0, -0.12, 0.24)), 0.05), (0.30, 0.18, 0.05), k=0.0, layer='bell3')
    tp, tn = surf_point(collar, (0, yc, 0), 40, 0)
    fig.add(S.box(tuple(tp + tn * 0.03 + (0, -0.12, 0)), (0.14, 0.2, 0.05), round_=0.04, R=basis(tn)), Fg.CRIMSON, k=0.0, layer='tag')

    # ---- 파란 생선 장난감 (오른쪽 +x 앞발이 쥠) ----
    fa = paw_r + np.array((0.0, 0.05, 0.3))
    fdir = np.array((-0.1, 1.0, 0.12)); fdir /= np.linalg.norm(fdir)
    Rf = axis_R(fdir, (0.35, 0, 1))
    fcen = fa + fdir * 0.45
    fish = S.ellipsoid(tuple(fcen), (0.5, 0.95, 0.3), R=Rf)
    tail_c = fcen - fdir * 1.05
    fins = [S.ellipsoid(tuple(tail_c + Rf[:, 0] * s * 0.24 - fdir * 0.1), (0.3, 0.38, 0.08), R=Rf @ S.rot(0, 0, s * 40)) for s in (-1, 1)]
    def fish_f(P):
        d = S.smin(fish(P), S.smin(fins[0](P), fins[1](P), 0.05), 0.12)
        d = S.smin(d, S.ellipsoid(tuple(fcen + Rf[:, 0] * 0.45 - fdir * 0.05), (0.18, 0.32, 0.05), R=Rf)(P), 0.08)
        return d
    fig.add(fish_f, TOY, k=0.0, layer='fish', metal=(TOY, TOY_HI))
    for k in range(3):   # 물결 비늘 무늬
        c = fcen - fdir * (0.4 - k * 0.27)
        fig.paint(masked(lambda P, c=c: np.abs(((P - c) @ fdir) - 0.04 * np.sin(((P - c) @ Rf[:, 0]) * 22)) - 0.025, lambda P: np.abs(fish(P)), 0.05), (0.85, 0.94, 1.0), soft=0.03)
    ep = fcen + fdir * 0.6 + Rf[:, 2] * 0.2
    fig.add(S.sphere(tuple(ep), 0.1), (1, 1, 1), k=0.0, layer='fisheye')
    fig.add(S.sphere(tuple(ep + Rf[:, 2] * 0.06), 0.06), Fg.PUPIL, k=0.0, layer='fisheye2')
    # 쥔 손가락 (생선을 감싼 발가락)
    fig.add(S.ellipsoid(tuple(fa + Rf[:, 2] * 0.24 + fdir * 0.02), (0.36, 0.2, 0.16), R=Rf), WHITE, k=0.0, layer='fing')

    # ---- 받침: 조약돌 ----
    for i, (x, z, r) in enumerate(((-3.0, 1.6, 0.3), (2.7, 2.0, 0.26), (-3.4, -1.0, 0.24), (3.3, -1.0, 0.22), (-1.5, 3.2, 0.2))):
        pebble(fig, x, z, r, seed=i)
