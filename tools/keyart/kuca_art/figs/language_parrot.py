"""외국어대 앵무새: 초록 몸 + 주홍 얼굴 + 파란 날개 끝, 빨간 헤드폰, 받침 위 지구본 · 말풍선 돌"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

GREEN = (0.36, 0.72, 0.20)
GREEN_LT = (0.62, 0.86, 0.38)
GREEN_DK = (0.24, 0.55, 0.14)
FACE = (1.0, 0.42, 0.24)
FACE_HI = (1.0, 0.56, 0.30)
BLUE = (0.14, 0.52, 0.92)
BLUE_DK = (0.08, 0.36, 0.78)
BEAK = (1.0, 0.82, 0.18)
BEAK_DK = (0.92, 0.62, 0.10)
FOOT = (0.42, 0.33, 0.28)
HP_RED = (0.84, 0.16, 0.16)
CREAM = (0.97, 0.93, 0.85)
BROW = (0.32, 0.18, 0.12)
OCEAN = (0.22, 0.56, 0.92)
LAND = (0.46, 0.76, 0.30)
STONE = (0.86, 0.85, 0.82)


def brow(fig, c, R, yaw, pitch, w=0.42, tilt=0.0, col=BROW):
    f = Fg._frame_on(c, R, yaw, pitch, -0.03)
    for k in range(13):
        t = (k / 12 - 0.5)
        Fg._ellipsoid(fig.extra, (t * w, -abs(t) * w * 0.25 + t * tilt, 0.0), (0.07, 0.055, 0.05), col, 6, 4, f)


def build(fig, rng):
    Fg.base(fig, rng, flowers=7)
    y0 = Fg.TOP
    g = (Fg.GOLD, Fg.GOLD_HI)
    # ---- 다리·발: 짧은 다리 + 발가락 3개 (앞) + 1개 (뒤) ----
    for s in (-1, 1):
        x = s * 0.62
        fig.add(S.capsule((x, y0 + 0.9, 0.05), (x, y0 + 0.28, 0.15), 0.26, 0.2), FOOT, k=0.1, layer='feet')
        for a in (-28, 0, 28):
            d = np.array((math.sin(math.radians(a + s * 6)), 0, math.cos(math.radians(a + s * 6))))
            fig.add(S.capsule((x, y0 + 0.2, 0.2), tuple(np.array((x, y0 + 0.14, 0.2)) + d * 0.55), 0.17, 0.12), FOOT, k=0.08, layer='feet')
        fig.add(S.capsule((x, y0 + 0.2, 0.1), (x, y0 + 0.14, -0.35), 0.15, 0.1), FOOT, k=0.08, layer='feet')
    # ---- 몸통: 서양배 모양 ----
    fig.add(S.ellipsoid((0, y0 + 1.75, 0.0), (1.5, 1.25, 1.35)), GREEN, k=0.4)
    fig.add(S.ellipsoid((0, y0 + 2.6, 0.0), (1.28, 1.05, 1.15)), GREEN, k=0.5)
    # 배: 연두 + 비늘 깃털 무늬
    belly = S.ellipsoid((0, y0 + 1.95, 1.0), (1.05, 1.35, 0.9))
    fig.paint(belly, GREEN_LT, soft=0.35)
    for row, (yy, xs) in enumerate(((y0 + 3.05, (-0.45, 0.0, 0.45)), (y0 + 2.7, (-0.68, -0.23, 0.23, 0.68)), (y0 + 2.35, (-0.45, 0.0, 0.45)))):
        for x in xs:
            c = (x, yy, 1.2)
            arc = S.intersect(S.onion(S.sphere(c, 0.3), 0.025), lambda P, yy=yy: (P[:, 1] - yy))
            fig.paint(S.intersect(arc, S.sphere((0, y0 + 2.4, 1.0), 1.4)), (0.52, 0.78, 0.30), soft=0.04)
    # ---- 날개: 어깨에서 비스듬히 아래·바깥으로 펼친 넓은 날개, 끝은 부채꼴 파란 깃 ----
    for s in (-1, 1):
        root = np.array((s * 1.2, y0 + 3.05, 0.15))
        R = S.rot(s * 30, 0, s * 44)
        down = R @ np.array((0, -1.0, 0))
        fig.add(S.ellipsoid(tuple(root + down * 0.85), (0.72, 1.05, 0.3), R=R), GREEN, k=0.3)
        for i, xo in enumerate((-0.45, -0.22, 0.0, 0.22, 0.45)):
            ln = 1.2 - abs(xo) * 0.7
            Rf = R @ S.rot(0, 0, s * xo * -60)
            fd = Rf @ np.array((0, -1.0, 0))
            c = root + down * 1.45 + R @ np.array((s * xo * -1.1, 0, -0.04 * i)) + fd * ln * 0.5
            fig.add(S.ellipsoid(tuple(c), (0.2, ln * 0.6, 0.12), R=Rf), BLUE, k=0.12)
        # 날개 덮깃 무늬 (초록 비늘 2줄)
        for j in range(2):
            for xo in (-0.4, 0.0, 0.4):
                c = root + down * (0.6 + 0.42 * j) + R @ np.array((xo, 0, 0.3))
                fig.paint(S.intersect(S.onion(S.sphere(tuple(c), 0.26), 0.028), lambda P, yy=c[1]: (P[:, 1] - yy)), GREEN_DK, soft=0.04)
        fig.paint(S.ellipsoid(tuple(root + down * 2.25), (0.9, 0.45, 0.6)), BLUE_DK, soft=0.3)
    # ---- 꼬리: 등 아래에서 뒤·아래로 파란 깃 ----
    for i, a in enumerate((-18, -6, 6, 18)):
        Rt = S.rot(a, -60, 0)
        c = np.array((0, y0 + 1.45, -1.25)) + Rt @ np.array((0, -0.85, 0))
        fig.add(S.ellipsoid(tuple(c), (0.26, 1.0, 0.13), R=Rt), BLUE if i in (0, 3) else BLUE_DK, k=0.12)
    fig.add(S.ellipsoid((0, y0 + 1.35, -1.2), (0.45, 0.45, 0.3)), GREEN, k=0.3)
    # ---- 머리 ----
    hc = (0, y0 + 4.95, 0.1)
    HR = 2.15
    fig.add(S.ellipsoid(hc, (HR * 1.05, HR * 0.97, HR)), GREEN, k=0.55)
    for s in (-1, 1):   # 볼살
        fig.add(S.sphere((s * 1.05, hc[1] - 0.9, hc[2] + 0.95), 0.75), GREEN, k=0.6)
    # 볏: 머리 위 초록 깃 다발
    for x, ang, ln, z, pt in ((-0.52, -32, 1.05, -0.3, -30), (-0.2, -12, 1.4, -0.2, -26), (0.2, 12, 1.4, -0.2, -26),
                              (0.52, 32, 1.05, -0.3, -30), (0.0, 0, 1.05, 0.25, -12), (0.0, 0, 0.95, -0.75, -50)):
        Rk = S.rot(0, pt, -ang)
        root = np.array((x, hc[1] + HR * 0.82, hc[2] + z))
        fig.add(S.ellipsoid(tuple(root + Rk @ np.array((0, ln * 0.55, 0))), (0.24, ln * 0.62, 0.17), R=Rk), GREEN, k=0.3)
    # 얼굴 주홍 가면
    mask = S.intersect(S.ellipsoid((0, hc[1] - 0.15, hc[2] + 1.25), (1.95, 1.75, 1.55)), lambda P: (hc[1] - 1.6) - P[:, 1])
    fig.paint(mask, FACE, soft=0.09)
    fig.paint(S.ellipsoid((0, hc[1] + 0.1, hc[2] + 2.0), (1.1, 0.8, 0.5)), FACE_HI, soft=0.4)
    # ---- 부리: 노란 갈고리 윗부리 + 작은 아랫부리 ----
    bf = Fg._frame_on(hc, HR, 0, -14, 0.0)
    bp = np.array(bf.p((0, 0, 0)))
    beak_up = S.ellipsoid(tuple(bp + (0, 0.1, 0.05)), (0.56, 0.47, 0.45))
    beak_hook = lambda P: S.smin(S.capsule(tuple(bp + (0, 0.12, 0.3)), tuple(bp + (0, -0.25, 0.5)), 0.4, 0.22)(P), S.capsule(tuple(bp + (0, -0.25, 0.5)), tuple(bp + (0, -0.62, 0.36)), 0.22, 0.06)(P), 0.1)
    fig.add(beak_up, BEAK, k=0.15, layer='beak')
    fig.add(beak_hook, BEAK, k=0.3, layer='beak')
    fig.add(S.ellipsoid(tuple(bp + (0, -0.38, 0.0)), (0.3, 0.17, 0.26)), BEAK_DK, k=0.05, layer='beak2')
    fig.paint(lambda P: np.minimum(beak_up(P), beak_hook(P)) - 0.06, BEAK, soft=0.03)
    fig.paint(S.ellipsoid(tuple(bp + (0, 0.3, 0.3)), (0.25, 0.18, 0.2)), (1.0, 0.92, 0.5), soft=0.15)
    # ---- 헤드폰: 빨간 밴드 + 금 슬라이더 + 금 테 · 크림 쿠션 · 빨간 컵 ----
    band = S.intersect(S.torus((0, hc[1] - 0.05, hc[2] - 0.15), HR * 1.04 + 0.1, 0.19, Rm=S.rot(0, 90, 0)),
                       lambda P: hc[1] + 0.9 - P[:, 1])
    fig.add(band, HP_RED, k=0.0, layer='hp')
    for s in (-1, 1):
        cx = s * (HR * 1.05 + 0.05)
        Rc = S.rot(0, 0, 90)
        fig.add(S.cylinder((s * (HR * 1.04 + 0.1), hc[1] + 0.85, hc[2] - 0.15), 0.23, 0.2, round_=0.06), Fg.GOLD, k=0.0, layer='hpg', metal=g)
        fig.add(S.cylinder((cx + s * 0.06, hc[1] - 0.1, hc[2] - 0.15), 0.72, 0.16, round_=0.1, R=Rc), CREAM, k=0.0, layer='cush')
        fig.add(S.cylinder((cx + s * 0.32, hc[1] - 0.1, hc[2] - 0.15), 0.78, 0.17, round_=0.12, R=Rc), HP_RED, k=0.0, layer='hp')
        fig.add(S.torus((cx + s * 0.32, hc[1] - 0.1, hc[2] - 0.15), 0.78, 0.07, Rm=Rc), Fg.GOLD, k=0.0, layer='hpg', metal=g)
        fig.add(S.cylinder((cx + s * 0.5, hc[1] - 0.1, hc[2] - 0.15), 0.5, 0.06, round_=0.05, R=Rc), Fg.GOLD, k=0.0, layer='hpg', metal=g)
    # ---- 얼굴 부품 ----
    Fg.kawaii_eyes(fig, hc, HR + 0.02, spread=27, pitch=0, size=0.6, tall=1.1)
    for s in (-1, 1):
        brow(fig, hc, HR + 0.03, s * 29, 21, w=0.52, tilt=-s * 0.08)
        p = np.array(Fg._frame_on(hc, HR, s * 44, -18, 0).p((0, 0, 0)))
        fig.paint(S.sphere(p, 0.5), (0.96, 0.30, 0.26), soft=0.6)
    # ---- 받침 소품: 지구본 (왼쪽 날개 끝이 닿음) ----
    gx, gz = -3.4, 1.05
    gc = (gx, y0 + 1.75, gz)
    fig.add(S.cylinder((gx, y0 + 0.1, gz), 0.62, 0.1, round_=0.06), Fg.GOLD, k=0.05, layer='globe_st', metal=g)
    fig.add(S.capsule((gx, y0 + 0.2, gz), (gx, y0 + 0.75, gz), 0.12, 0.09), Fg.GOLD, k=0.1, layer='globe_st', metal=g)
    Rm = S.rot(-35, 0, 23)
    fig.add(S.torus(gc, 0.97, 0.055, Rm=Rm @ S.rot(90, 90, 0)), Fg.GOLD, k=0.0, layer='globe_st', metal=g)
    fig.add(S.capsule((gx, y0 + 0.7, gz), tuple(np.array(gc) + Rm @ np.array((0, -0.96, 0))), 0.08), Fg.GOLD, k=0.06, layer='globe_st', metal=g)
    fig.add(S.sphere(gc, 0.86), OCEAN, k=0.0, layer='globe')

    def land(P):
        q = (P - np.array(gc)) @ Rm
        n = q / np.maximum(np.linalg.norm(q, axis=1, keepdims=True), 1e-9)
        lat, lon = np.arcsin(np.clip(n[:, 1], -1, 1)), np.arctan2(n[:, 0], n[:, 2])
        v = (np.sin(lon * 2.0 + 0.6) * np.cos(lat * 2.6 - 0.4) + 0.45 * np.sin(lon * 5.0 + lat * 3.0) + 0.25 * np.cos(lat * 7.0 + lon * 3.0))
        return np.maximum(0.25 - v, np.linalg.norm(P - np.array(gc), axis=1) - 0.95)
    fig.paint(land, LAND, soft=0.06)
    # ---- 말풍선 돌 (오른쪽 앞) ----
    sx, sz = 2.85, 1.55
    Rs = S.rot(-25, -6, 0)
    sc_ = np.array((sx, y0 + 0.95, sz))

    def bub(P):
        q = (P - sc_) @ Rs
        d2 = np.linalg.norm(np.maximum(np.abs(q[:, :2]) - np.array((0.55, 0.25)), 0), axis=1) - 0.48
        return np.linalg.norm(np.stack([np.maximum(d2 + 0.22, 0), np.maximum(np.abs(q[:, 2]) - 0.08, 0)], axis=1), axis=1) - 0.22 + np.minimum(np.maximum(d2 + 0.22, np.abs(q[:, 2]) - 0.08), 0)
    tail = S.capsule(tuple(sc_ + Rs @ np.array((-0.45, -0.4, 0.0))), tuple(sc_ + Rs @ np.array((-0.85, -0.95, 0.0))), 0.24, 0.06)
    stone = lambda P: S.smin(bub(P), tail(P), 0.12) + 0.006 * np.sin(P[:, 0] * 23) * np.sin(P[:, 1] * 19 + P[:, 2] * 17)
    fig.add(stone, STONE, k=0.0, layer='stone')
    fig.paint(lambda P: np.abs(np.sin(P[:, 0] * 31) * np.sin(P[:, 1] * 27) * np.sin(P[:, 2] * 29)) - 0.03 + np.maximum(0, np.linalg.norm(P - np.array((sx, y0 + 0.8, sz)), axis=1) - 1.3) * 5,
              (0.66, 0.65, 0.62), soft=0.03)
