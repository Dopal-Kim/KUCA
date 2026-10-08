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


def head_frame(fig, c, yaw, pitch, layer='body', out=0.0):
    """머리 중심 c 에서 (yaw, pitch) 방향의 실제 조형 표면 위 Frame"""
    y, p = math.radians(yaw), math.radians(pitch)
    d = np.array((math.cos(p) * math.sin(y), math.sin(p), math.cos(p) * math.cos(y)))
    return Fg.surface_frame(fig, np.asarray(c) + d * 7.0, -d, out=out, layer=layer)


def brow(fig, f, w=0.42, tilt=0.0, col=BROW):
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
    # ---- 몸통: 서양배 (아래가 넓고 묵직, 위로 갈수록 좁아져 목 없이 머리로 이어짐) ----
    fig.add(S.ellipsoid((0, y0 + 1.6, 0.0), (1.62, 1.3, 1.45)), GREEN, k=0.4)
    fig.add(S.ellipsoid((0, y0 + 2.85, 0.02), (1.12, 1.15, 1.02)), GREEN, k=0.5)
    # 배: 연두 + 비늘 깃털 무늬
    belly = S.ellipsoid((0, y0 + 1.95, 1.0), (1.1, 1.4, 0.95))
    fig.paint(belly, GREEN_LT, soft=0.35)
    for row, (yy, xs) in enumerate(((y0 + 3.05, (-0.42, 0.0, 0.42)), (y0 + 2.7, (-0.64, -0.21, 0.21, 0.64)), (y0 + 2.35, (-0.45, 0.0, 0.45)))):
        for x in xs:
            c = (x, yy, 1.2)
            arc = S.intersect(S.onion(S.sphere(c, 0.3), 0.025), lambda P, yy=yy: (P[:, 1] - yy))
            fig.paint(S.intersect(arc, S.sphere((0, y0 + 2.4, 1.0), 1.4)), (0.52, 0.78, 0.30), soft=0.04)
    # ---- 날개: 어깨에서 비스듬히 아래·바깥으로 펼친 날개, 끝은 부채꼴 파란 깃 ----
    for s in (-1, 1):
        root = np.array((s * 1.08, y0 + 3.15, 0.15))
        R = S.rot(s * 24, 0, s * 52)
        down = R @ np.array((0, -1.0, 0))
        fig.add(S.ellipsoid(tuple(root + down * 0.85), (0.7, 1.05, 0.3), R=R), GREEN, k=0.15)
        for i, xo in enumerate((-0.45, -0.22, 0.0, 0.22, 0.45)):
            ln = 1.2 - abs(xo) * 0.7
            Rf = R @ S.rot(0, 0, s * xo * -60)
            fd = Rf @ np.array((0, -1.0, 0))
            c = root + down * 1.45 + R @ np.array((s * xo * -1.1, 0, -0.04 * i)) + fd * ln * 0.5
            fig.add(S.ellipsoid(tuple(c), (0.2, ln * 0.6, 0.12), R=Rf), BLUE, k=0.12)
        for j in range(2):
            for xo in (-0.4, 0.0, 0.4):
                c = root + down * (0.6 + 0.42 * j) + R @ np.array((xo, 0, 0.3))
                fig.paint(S.intersect(S.onion(S.sphere(tuple(c), 0.26), 0.028), lambda P, yy=c[1]: (P[:, 1] - yy)), GREEN_DK, soft=0.04)
        fig.paint(S.ellipsoid(tuple(root + down * 2.25), (0.9, 0.45, 0.6)), BLUE_DK, soft=0.3)
    # ---- 꼬리: 등 아래에서 뒤·아래로 파란 깃 ----
    for i, a in enumerate((-18, -6, 6, 18)):
        Rt = S.rot(a, -60, 0)
        c = np.array((0, y0 + 1.45, -1.32)) + Rt @ np.array((0, -0.85, 0))
        fig.add(S.ellipsoid(tuple(c), (0.26, 1.0, 0.13), R=Rt), BLUE if i in (0, 3) else BLUE_DK, k=0.12)
    fig.add(S.ellipsoid((0, y0 + 1.35, -1.25), (0.45, 0.45, 0.3)), GREEN, k=0.3)
    # ---- 머리: 둥근 머리, 목 없이 몸과 이어짐. 볼은 머리 아래쪽 볼륨으로만 ----
    hc = (0, y0 + 5.15, 0.1)
    HR = 1.9
    fig.add(S.ellipsoid(hc, (HR * 1.04, HR * 0.98, HR * 0.98)), GREEN, k=0.4)
    fig.add(S.ellipsoid((0, hc[1] - 0.5, hc[2] + 0.1), (HR * 0.98, HR * 0.72, HR * 0.9)), GREEN, k=0.8)
    # 볏: 머리 위 초록 깃 다발
    for x, ang, ln, z, pt in ((-0.5, -32, 1.0, -0.3, -30), (-0.19, -12, 1.3, -0.2, -26), (0.19, 12, 1.3, -0.2, -26),
                              (0.5, 32, 1.0, -0.3, -30), (0.0, 0, 1.0, 0.25, -12), (0.0, 0, 0.9, -0.75, -50)):
        Rk = S.rot(0, pt, -ang)
        root = np.array((x, hc[1] + HR * 0.82, hc[2] + z))
        fig.add(S.ellipsoid(tuple(root + Rk @ np.array((0, ln * 0.55, 0))), (0.24, ln * 0.62, 0.17), R=Rk), GREEN, k=0.3)
    # 얼굴 주홍 가면
    mask = S.intersect(S.ellipsoid((0, hc[1] - 0.15, hc[2] + 1.2), (1.8, 1.65, 1.5)), lambda P: (hc[1] - 1.5) - P[:, 1])
    fig.paint(mask, FACE, soft=0.09)
    fig.paint(S.ellipsoid((0, hc[1] + 0.1, hc[2] + 1.9), (1.0, 0.75, 0.5)), FACE_HI, soft=0.4)
    # ---- 부리: 노란 갈고리 윗부리 + 작은 아랫부리 (표면에 붙임) ----
    bf = head_frame(fig, hc, 0, -16)
    bp = np.array(bf.o) + np.array((0, 0, -0.12))
    beak_up = S.ellipsoid(tuple(bp + (0, 0.08, 0.02)), (0.44, 0.38, 0.38))
    beak_hook = lambda P: S.smin(S.capsule(tuple(bp + (0, 0.12, 0.28)), tuple(bp + (0, -0.22, 0.48)), 0.37, 0.2)(P), S.capsule(tuple(bp + (0, -0.22, 0.48)), tuple(bp + (0, -0.58, 0.34)), 0.2, 0.06)(P), 0.1)
    fig.add(beak_up, BEAK, k=0.15, layer='beak')
    fig.add(beak_hook, BEAK, k=0.3, layer='beak')
    fig.add(S.ellipsoid(tuple(bp + (0, -0.36, 0.0)), (0.28, 0.16, 0.25)), BEAK_DK, k=0.05, layer='beak2')
    fig.paint(lambda P: np.minimum(beak_up(P), beak_hook(P)) - 0.06, BEAK, soft=0.03)
    # ---- 헤드폰: 빨간 밴드 + 금 슬라이더 + 금 테 · 크림 쿠션 · 빨간 컵 ----
    band = S.intersect(S.torus((0, hc[1] - 0.05, hc[2] - 0.15), HR * 1.04 + 0.1, 0.19, Rm=S.rot(0, 90, 0)),
                       lambda P: hc[1] + 0.9 - P[:, 1])
    fig.add(band, HP_RED, k=0.0, layer='hp')
    for s in (-1, 1):
        cx = s * (HR * 1.05 + 0.05)
        Rc = S.rot(0, 0, 90)
        fig.add(S.cylinder((s * (HR * 1.04 + 0.1), hc[1] + 0.85, hc[2] - 0.15), 0.23, 0.2, round_=0.06), Fg.GOLD, k=0.0, layer='hpg', metal=g)
        fig.add(S.cylinder((cx + s * 0.06, hc[1] - 0.1, hc[2] - 0.15), 0.7, 0.16, round_=0.1, R=Rc), CREAM, k=0.0, layer='cush')
        fig.add(S.cylinder((cx + s * 0.32, hc[1] - 0.1, hc[2] - 0.15), 0.76, 0.17, round_=0.12, R=Rc), HP_RED, k=0.0, layer='hp')
        fig.add(S.torus((cx + s * 0.32, hc[1] - 0.1, hc[2] - 0.15), 0.76, 0.07, Rm=Rc), Fg.GOLD, k=0.0, layer='hpg', metal=g)
        fig.add(S.cylinder((cx + s * 0.5, hc[1] - 0.1, hc[2] - 0.15), 0.48, 0.06, round_=0.05, R=Rc), Fg.GOLD, k=0.0, layer='hpg', metal=g)
    # ---- 얼굴 부품: 흰 눈테 두른 앵무새 눈 (ring), 짧은 눈썹, 칠한 볼터치 ----
    for s in (-1, 1):
        ef = head_frame(fig, hc, s * 31, 4)
        Fg.eye_at(fig, ef, 'ring', size=0.46, iris=(0.34, 0.19, 0.10), side=s)
        bf_ = head_frame(fig, hc, s * 33, 22, out=0.02)
        brow(fig, bf_, w=0.46, tilt=-s * 0.06)
        p = np.array(head_frame(fig, hc, s * 46, -16).o)
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
