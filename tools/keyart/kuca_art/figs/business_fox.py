"""경영대 여우: 주황 털 + 흰 가슴·꼬리 끝, 흰 셔츠 + 회색 조끼 + 진홍 넥타이, 갈색 서류가방, 받침 위 서류 더미"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

ORANGE = (0.96, 0.50, 0.14)
ORANGE_LT = (1.0, 0.64, 0.30)
WHITE = (0.99, 0.96, 0.92)
DARK = (0.36, 0.20, 0.13)
EAR_IN = (1.0, 0.82, 0.80)
SHIRT = (0.98, 0.98, 0.97)
VEST = (0.50, 0.51, 0.54)
VEST_DK = (0.38, 0.39, 0.42)
TIE = (0.72, 0.10, 0.18)
CASE = (0.45, 0.25, 0.15)
CASE_DK = (0.33, 0.17, 0.10)
PAPER = (0.97, 0.97, 0.95)


def squash(f_local, c, R, sc):
    """로컬 좌표에서 정의한 SDF f_local 을 축별로 sc 만큼 늘려 세계에 놓는다 (근사 거리)"""
    c = np.asarray(c, np.float64)
    sc = np.asarray(sc, np.float64)
    m = float(sc.min())

    def f(P):
        q = (P - c) @ R if R is not None else P - c
        return f_local(q / sc) * m
    return f


def rmax(a, b, r):
    """둥근 교집합 (모서리 반지름 r)"""
    a, b = a + r, b + r
    return np.linalg.norm(np.maximum(np.stack([a, b], 1), 0), axis=1) + np.minimum(np.maximum(a, b), 0) - r


def build(fig, rng):
    Fg.base(fig, rng, flowers=7)
    y0 = Fg.TOP
    g = (Fg.GOLD, Fg.GOLD_HI)
    # ---- 다리: 주황 + 짙은 갈색 발 ----
    for s in (-1, 1):
        x = s * 0.66
        fig.add(S.ellipsoid((x, y0 + 0.28, 0.25), (0.5, 0.32, 0.62)), DARK, k=0.1, layer='feet')
        fig.add(S.capsule((x, y0 + 0.55, 0.05), (x * 1.05, y0 + 1.55, 0.0), 0.5, 0.58), ORANGE, k=0.25)
    # ---- 몸통: 흰 배 + 셔츠 ----
    hips = S.ellipsoid((0, y0 + 1.8, 0.0), (1.28, 0.8, 1.04))
    chest = S.ellipsoid((0, y0 + 2.8, 0.0), (1.2, 1.0, 0.98))
    fig.add(hips, ORANGE, k=0.35)
    fig.paint(S.ellipsoid((0, y0 + 1.45, 0.75), (0.8, 0.6, 0.6)), WHITE, soft=0.25)
    fig.add(chest, SHIRT, k=0.35, layer='shirt')
    # 조끼: 몸통을 따라 감싼 껍질 (가슴~엉덩이 위), V 넥 + 팔 구멍
    torso = lambda P: S.smin(hips(P), chest(P), 0.5)
    vneck = lambda P: np.maximum((np.abs(P[:, 0]) * 1.5 - (P[:, 1] - (y0 + 2.45))) * 0.55, 0.2 - P[:, 2])
    vest = lambda P: rmax(rmax(torso(P) - 0.12, np.maximum((y0 + 1.72 - 0.18 * np.clip(1 - np.abs(P[:, 0]) / 0.7, 0, 1)) - P[:, 1], P[:, 1] - (y0 + 3.55)), 0.06), -vneck(P), 0.05)
    for s in (-1, 1):
        vest = S.subtract(vest, S.ellipsoid((s * 1.25, y0 + 3.2, 0.0), (0.38, 0.5, 0.55)), k=0.04)
    fig.add(vest, VEST, k=0.0, layer='vest')
    fig.paint(lambda P: np.maximum(np.abs(P[:, 0] - 0.0) - 0.02, np.maximum(0.6 - P[:, 2], P[:, 1] - (y0 + 2.55))), VEST_DK, soft=0.02)
    for k in range(3):
        y = y0 + 2.35 - k * 0.3
        fig.add(S.ellipsoid((0.14, y, 1.13 - 0.03 * k), (0.11, 0.11, 0.06)), Fg.GOLD, k=0.0, layer='btn', metal=g)
    for s in (-1, 1):   # 주머니 덮개
        fig.add(S.box((s * 0.68, y0 + 2.05, 0.9), (0.25, 0.05, 0.06), round_=0.025, R=S.rot(s * 38, 0, 0)), VEST_DK, k=0.0, layer='vest2')
    fig.add(S.box((0.62, y0 + 3.05, 0.92), (0.2, 0.04, 0.05), round_=0.02, R=S.rot(34, 0, 0)), VEST_DK, k=0.0, layer='vest2')
    # 넥타이: 매듭 + 날
    fig.add(S.ellipsoid((0, y0 + 3.42, 0.86), (0.17, 0.14, 0.1)), TIE, k=0.0, layer='tie')
    def tie_shape(P):
        w = 0.19 - 0.1 * np.clip((P[:, 1] - (y0 + 2.85)) / 0.5, 0, 1)
        ax = np.abs(P[:, 0])
        return np.maximum.reduce([ax - w, (y0 + 2.5 + ax * 1.1) - P[:, 1], P[:, 1] - (y0 + 3.35)])
    tie = S.intersect(S.box((0, y0 + 2.95, 0.97), (0.3, 0.6, 0.05), round_=0.03, R=S.rot(0, -14, 0)), tie_shape)
    fig.add(tie, TIE, k=0.0, layer='tie')
    # 셔츠 깃
    for s in (-1, 1):
        fig.add(S.box((s * 0.28, y0 + 3.52, 0.72), (0.22, 0.14, 0.05), round_=0.04, R=S.rot(s * 30, -40, s * 35)), SHIRT, k=0.0, layer='collar')
    # ---- 팔: 흰 셔츠 소매 → 주황 팔뚝 → 짙은 갈색 손 ----
    for s in (-1, 1):
        sh = np.array((s * 1.12, y0 + 3.2, 0.0))
        el = np.array((s * 1.62, y0 + 2.6, 0.12))
        wr = np.array((s * 1.95, y0 + 2.05, 0.3))
        fig.add(S.sphere(tuple(sh), 0.48), SHIRT, k=0.3, layer='shirt')
        fig.add(S.capsule(tuple(sh), tuple(el), 0.44, 0.42), SHIRT, k=0.25, layer='shirt')
        fig.add(S.capsule(tuple(el), tuple(wr), 0.36, 0.35), ORANGE, k=0.15, layer='arm')
        fig.add(S.sphere(tuple(wr + (wr - el) * 0.35), 0.36), DARK, k=0.14, layer='arm')
    # ---- 꼬리: 크고 복슬복슬, 오른쪽 뒤에서 위로, 흰 끝 ----
    pts = [np.array(p) for p in ((0.3, y0 + 1.6, -0.85), (1.1, y0 + 1.5, -1.5), (1.95, y0 + 1.95, -1.75), (2.45, y0 + 2.75, -1.55), (2.55, y0 + 3.45, -1.2))]
    rs = (0.35, 0.68, 0.85, 0.75, 0.42)
    for (a, b), (r0, r1) in zip(zip(pts, pts[1:]), zip(rs, rs[1:])):
        fig.add(S.capsule(tuple(a), tuple(b), r0, r1), ORANGE, k=0.35, layer='tail')
    fig.add(S.cone(tuple(pts[-1]), tuple(pts[-1] + (0.05, 0.45, 0.15)), 0.4, 0.06), ORANGE, k=0.3, layer='tail')
    fig.paint(lambda P: np.maximum((y0 + 2.95) - P[:, 1] + 0.18 * np.sin(np.arctan2(P[:, 2] + 1.4, P[:, 0] - 2.5) * 5), np.linalg.norm(P - np.array((2.5, y0 + 3.3, -1.35)), axis=1) - 1.3), WHITE, soft=0.06)
    # ---- 머리 ----
    hc = (0, y0 + 5.55, 0.1)
    HR = 2.05
    head_f = S.ellipsoid(hc, (HR * 1.1, HR * 0.95, HR * 0.98))
    fig.add(head_f, ORANGE, k=0.5)
    fig.add(S.capsule((0, y0 + 3.4, 0), (0, hc[1] - 1.4, 0.05), 0.7), ORANGE, k=0.3)
    for s in (-1, 1):   # 볼털: 옆으로 뾰족하게 뻗은 흰 털 두 갈래
        fig.add(S.sphere((s * 1.2, hc[1] - 0.85, hc[2] + 0.7), 0.85), ORANGE, k=0.5)
        for dy, ln, dz in ((0.15, 0.9, -0.1), (-0.3, 0.8, 0.0), (-0.7, 0.55, 0.15)):
            a = np.array((s * 1.7, hc[1] - 0.7 + dy, hc[2] + 0.35 + dz))
            fig.add(S.cone(tuple(a), tuple(a + np.array((s * ln, -0.3 + dy * 0.3, -0.15))), 0.5, 0.07), ORANGE, k=0.3)
    fig.add(S.ellipsoid((0, hc[1] - 0.75, hc[2] + 1.55), (0.85, 0.6, 0.6)), ORANGE, k=0.45)   # 주둥이
    # 흰 얼굴 아래쪽 (주둥이·볼털)
    fig.paint(lambda P: np.maximum.reduce([S.ellipsoid((0, hc[1] - 1.15, hc[2] + 0.6), (2.9, 1.0, 1.9))(P), P[:, 1] - (hc[1] - 0.55), (y0 + 3.75) - P[:, 1]]), WHITE, soft=0.15)
    for s in (-1, 1):   # 눈 위 흰 점
        p = np.array(Fg._frame_on(hc, HR, s * 25, 26, 0).p((0, 0, 0)))
        fig.paint(S.ellipsoid(tuple(p), (0.24, 0.15, 0.25)), WHITE, soft=0.03)
    # 앞머리 털 (작은 불꽃 모양)
    for x, ang, ln in ((-0.25, 20, 0.6), (0.05, -5, 0.75), (0.32, -25, 0.55)):
        a = np.array((x, hc[1] + HR * 0.86, hc[2] + 0.55))
        fig.add(S.cone(tuple(a), tuple(a + S.rot(0, -35, ang) @ np.array((0, ln, 0))), 0.25, 0.04), ORANGE, k=0.22)
    # ---- 귀: 큰 삼각 귀, 짙은 끝, 밝은 안쪽 ----
    for s in (-1, 1):
        Re = S.rot(s * 12, -8, -s * 22)
        ec = np.array((s * 1.3, hc[1] + 1.35, hc[2] - 0.25))
        ear_l = lambda q: S.cone((0, 0, 0), (0, 2.0, 0), 0.85, 0.07)(q)
        ear = squash(ear_l, ec, Re, (1.0, 1.0, 0.45))
        inner = squash(lambda q: S.cone((0, 0.05, 0), (0, 1.7, 0), 0.62, 0.04)(q), ec + Re @ np.array((0, 0.0, 0.22)), Re, (1.0, 1.0, 0.35))
        fig.add(S.subtract(ear, inner, k=0.08), ORANGE, k=0.35)
        fig.paint(squash(lambda q: S.cone((0, 0.1, 0), (0, 1.7, 0), 0.6, 0.04)(q), ec + Re @ np.array((0, 0.0, 0.16)), Re, (1.0, 1.0, 0.4)), EAR_IN, soft=0.08)
        tip_c = ec + Re @ np.array((0, 2.05, 0))
        fig.paint(S.sphere(tuple(tip_c), 0.72), DARK, soft=0.25)
    # ---- 얼굴 ----
    Fg.kawaii_eyes(fig, hc, HR * 0.98 + 0.03, spread=25, pitch=-2, size=0.58, tall=1.1)
    nf = Fg._frame_on((0, hc[1] - 0.75, hc[2] + 1.55), 0.6, 0, 10, -0.02)
    Fg._ellipsoid(fig.extra, (0, 0, 0), (0.18, 0.12, 0.12), (0.12, 0.08, 0.08), 12, 6, nf)
    Fg._ellipsoid(fig.extra, (-0.05, 0.05, 0.08), (0.06, 0.03, 0.03), (0.6, 0.6, 0.6), 6, 4, nf)
    Fg.smile(fig, (0, hc[1] - 0.75, hc[2] + 1.55), 0.6 + 0.03, pitch=-30, w=0.2, col=(0.25, 0.12, 0.1))
    for s in (-1, 1):
        p = np.array(Fg._frame_on(hc, HR * 1.05, s * 45, -20, 0).p((0, 0, 0)))
        fig.paint(S.sphere(p, 0.45), Fg.BLUSH, soft=0.55)
    # ---- 서류가방 (오른손) ----
    hand = np.array((1.95, y0 + 2.05, 0.3)) + (np.array((1.95, y0 + 2.05, 0.3)) - np.array((1.62, y0 + 2.6, 0.12))) * 0.35
    Rc = S.rot(25, 0, 0)
    cc = np.array((hand[0] + 0.15, y0 + 0.95, hand[2] + 0.05))
    fig.add(S.box(tuple(cc), (0.78, 0.55, 0.24), round_=0.1, R=Rc), CASE, k=0.0, layer='case')
    fig.add(S.box(tuple(cc + Rc @ np.array((0, 0.28, 0.0))), (0.8, 0.06, 0.26), round_=0.04, R=Rc), CASE_DK, k=0.0, layer='case2')
    fig.add(S.box(tuple(cc + Rc @ np.array((0, 0.2, 0.25))), (0.11, 0.1, 0.04), round_=0.02, R=Rc), Fg.GOLD, k=0.0, layer='clasp', metal=g)
    fig.add(S.torus(tuple(cc + Rc @ np.array((0, 0.62, 0))), 0.25, 0.06, Rm=Rc @ S.rot(0, 90, 0)), CASE_DK, k=0.0, layer='case2')
    for s in (-1, 1):
        fig.add(S.box(tuple(cc + Rc @ np.array((s * 0.25, 0.57, 0))), (0.06, 0.04, 0.08), round_=0.02, R=Rc), Fg.GOLD, k=0.0, layer='clasp', metal=g)
    # ---- 받침 소품: 서류 더미 (왼쪽 앞) ----
    sx, sz = -2.7, 1.4
    Rp = S.rot(-15, 0, 0)
    for k in range(6):
        Rk = Rp @ S.rot(rng.uniform(-6, 6), 0, 0)
        fig.add(S.box((sx + rng.uniform(-0.04, 0.04), y0 + 0.07 + k * 0.1, sz), (0.72, 0.055, 0.52), round_=0.02, R=Rk), PAPER, k=0.0, layer='paper')
    fig.paint(lambda P: np.maximum(np.abs(np.sin((P[:, 1] - y0) * math.pi / 0.1)) - 0.25, np.maximum(np.linalg.norm((P - np.array((sx, y0 + 0.35, sz)))[:, [0, 2]], axis=1) - 1.0, P[:, 1] - (y0 + 0.75))), (0.82, 0.82, 0.80), soft=0.05)
