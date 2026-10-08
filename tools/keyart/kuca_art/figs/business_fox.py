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


def head_frame(fig, c, yaw, pitch, layer='body', out=0.0):
    """머리 중심 c 에서 (yaw, pitch) 방향의 실제 조형 표면 위 Frame"""
    y, p = math.radians(yaw), math.radians(pitch)
    d = np.array((math.cos(p) * math.sin(y), math.sin(p), math.cos(p) * math.cos(y)))
    return Fg.surface_frame(fig, np.asarray(c) + d * 7.0, -d, out=out, layer=layer)


def front_frame(fig, x, y, layer):
    return Fg.surface_frame(fig, (x, y, 6.0), (0, 0, -1), layer=layer)


def w_mouth(fig, f, w, col):
    for s in (-1, 1):
        for k in range(9):
            a = math.pi * k / 8
            Fg._ellipsoid(fig.extra, (s * w * 0.5 + math.cos(a) * w * 0.5, -math.sin(a) * w * 0.42, 0.01), (0.045, 0.045, 0.04), col, 6, 4, f)


def tri_head(c, r, taper):
    """역삼각 두상: 위(관자놀이)는 넓고 아래(턱)로 갈수록 좁아지는 타원체"""
    c = np.asarray(c, np.float64)
    base_ = S.ellipsoid((0, 0, 0), r)

    def f(P):
        q = P - c
        t = np.clip(q[:, 1] / r[1], -1, 1)
        sx = 1 + taper * t
        q2 = q.copy()
        q2[:, 0] = q[:, 0] / sx
        return base_(q2) * np.minimum(sx, 1.0)
    return f


def build(fig, rng):
    Fg.base(fig, rng, flowers=7)
    y0 = Fg.TOP
    g = (Fg.GOLD, Fg.GOLD_HI)
    # ---- 다리: 길고 가는 주황 다리 + 짙은 갈색 발 ----
    for s in (-1, 1):
        x = s * 0.55
        fig.add(S.ellipsoid((x, y0 + 0.26, 0.25), (0.42, 0.28, 0.58)), DARK, k=0.1, layer='feet')
        fig.add(S.capsule((x, y0 + 0.5, 0.05), (x * 1.05, y0 + 1.75, 0.0), 0.4, 0.5), ORANGE, k=0.12)
        fig.paint(S.capsule((x, y0 + 0.3, 0.05), (x, y0 + 0.75, 0.05), 0.55), DARK, soft=0.2)
    # ---- 몸통: 날씬 (흰 배 + 셔츠) ----
    hips = S.ellipsoid((0, y0 + 2.0, 0.0), (1.05, 0.78, 0.88))
    chest = S.ellipsoid((0, y0 + 2.95, 0.0), (0.98, 1.0, 0.84))
    fig.add(hips, ORANGE, k=0.35)
    fig.paint(S.ellipsoid((0, y0 + 1.65, 0.7), (0.65, 0.55, 0.5)), WHITE, soft=0.25)
    fig.add(chest, SHIRT, k=0.35, layer='shirt')
    # 조끼: 몸통을 따라 감싼 껍질 (가슴~엉덩이 위), V 넥 + 팔 구멍
    torso = lambda P: S.smin(hips(P), chest(P), 0.5)
    vneck = lambda P: np.maximum((np.abs(P[:, 0]) * 1.5 - (P[:, 1] - (y0 + 2.6))) * 0.55, 0.2 - P[:, 2])
    vest = lambda P: rmax(rmax(torso(P) - 0.12, np.maximum((y0 + 1.9 - 0.16 * np.clip(1 - np.abs(P[:, 0]) / 0.6, 0, 1)) - P[:, 1], P[:, 1] - (y0 + 3.7)), 0.06), -vneck(P), 0.05)
    for s in (-1, 1):
        vest = S.subtract(vest, S.ellipsoid((s * 1.02, y0 + 3.35, 0.0), (0.34, 0.48, 0.5)), k=0.04)
    fig.add(vest, VEST, k=0.0, layer='vest')
    fig.paint(lambda P: np.maximum(np.abs(P[:, 0] - 0.0) - 0.02, np.maximum(0.5 - P[:, 2], P[:, 1] - (y0 + 2.7))), VEST_DK, soft=0.02)
    for k in range(3):
        f = front_frame(fig, 0.13, y0 + 2.5 - k * 0.27, 'vest')
        Fg._ellipsoid(fig.extra, (0, 0, 0.0), (0.09, 0.09, 0.05), Fg.GOLD, 10, 6, f)
    for s in (-1, 1):   # 주머니 덮개
        f = front_frame(fig, s * 0.55, y0 + 2.2, 'vest')
        fig.add(S.box(f.o, (0.18, 0.03, 0.03), round_=0.015, R=S.rot(s * 32, 0, 0)), VEST_DK, k=0.0, layer='vest2')
    f = front_frame(fig, 0.52, y0 + 3.15, 'vest')
    fig.add(S.box(f.o, (0.16, 0.035, 0.045), round_=0.02, R=S.rot(30, 0, 0)), VEST_DK, k=0.0, layer='vest2')
    # 넥타이: 매듭 + 날 (셔츠 표면에 붙임)
    zt = front_frame(fig, 0.0, y0 + 3.1, 'shirt').o[2]
    fig.add(S.ellipsoid((0, y0 + 3.6, zt - 0.08), (0.15, 0.12, 0.1)), TIE, k=0.0, layer='tie')
    def tie_shape(P):
        w = 0.17 - 0.09 * np.clip((P[:, 1] - (y0 + 3.0)) / 0.5, 0, 1)
        ax = np.abs(P[:, 0])
        return np.maximum.reduce([ax - w, (y0 + 2.7 + ax * 1.1) - P[:, 1], P[:, 1] - (y0 + 3.52)])
    tie = S.intersect(S.box((0, y0 + 3.12, zt + 0.02), (0.3, 0.6, 0.045), round_=0.03, R=S.rot(0, -16, 0)), tie_shape)
    fig.add(tie, TIE, k=0.0, layer='tie')
    # 셔츠 깃
    for s in (-1, 1):
        fig.add(S.box((s * 0.26, y0 + 3.68, zt - 0.2), (0.2, 0.13, 0.05), round_=0.04, R=S.rot(s * 30, -40, s * 35)), SHIRT, k=0.0, layer='collar')
    # ---- 팔: 흰 셔츠 소매 → 주황 팔뚝 → 짙은 갈색 손 ----
    for s in (-1, 1):
        sh = np.array((s * 0.95, y0 + 3.4, 0.0))
        el = np.array((s * 1.38, y0 + 2.78, 0.12))
        wr = np.array((s * 1.66, y0 + 2.22, 0.3))
        fig.add(S.sphere(tuple(sh), 0.42), SHIRT, k=0.15, layer='shirt')
        fig.add(S.capsule(tuple(sh), tuple(el), 0.39, 0.37), SHIRT, k=0.12, layer='shirt')
        fig.add(S.capsule(tuple(el), tuple(wr), 0.31, 0.3), ORANGE, k=0.15, layer='arm')
        fig.add(S.sphere(tuple(wr + (wr - el) * 0.35), 0.32), DARK, k=0.14, layer='arm')
    # ---- 꼬리: 아주 크고 복슬복슬, 오른쪽 뒤에서 위로, 흰 끝 ----
    pts = [np.array(p) for p in ((0.3, y0 + 1.75, -0.75), (1.1, y0 + 1.45, -1.5), (2.05, y0 + 1.9, -1.85), (2.6, y0 + 2.85, -1.7), (2.7, y0 + 3.75, -1.3))]
    rs = (0.32, 0.75, 1.0, 0.92, 0.5)
    for (a, b), (r0, r1) in zip(zip(pts, pts[1:]), zip(rs, rs[1:])):
        fig.add(S.capsule(tuple(a), tuple(b), r0, r1), ORANGE, k=0.4, layer='tail')
    fig.add(S.cone(tuple(pts[-1]), tuple(pts[-1] + (0.05, 0.55, 0.18)), 0.48, 0.06), ORANGE, k=0.35, layer='tail')
    fig.paint(lambda P: np.maximum((y0 + 3.1) - P[:, 1] + 0.2 * np.sin(np.arctan2(P[:, 2] + 1.5, P[:, 0] - 2.65) * 5), np.linalg.norm(P - np.array((2.65, y0 + 3.55, -1.45)), axis=1) - 1.45), WHITE, soft=0.06)
    # ---- 머리: 역삼각 얼굴 (넓은 관자놀이 → 좁은 턱) + 뾰족한 주둥이, 따로 붙인 볼 없음 ----
    hc = (0, y0 + 5.5, 0.1)
    HR = 1.95
    head_f = tri_head(hc, (HR * 1.12, HR * 0.92, HR * 0.95), 0.38)
    fig.add(head_f, ORANGE, k=0.3)
    fig.add(S.capsule((0, y0 + 3.6, 0), (0, hc[1] - 1.3, 0.05), 0.62), ORANGE, k=0.2)
    fig.add(S.capsule((0, hc[1] - 0.5, hc[2] + 0.8), (0, hc[1] - 0.85, hc[2] + 2.15), 0.82, 0.24), ORANGE, k=0.4)   # 뾰족 주둥이
    for s in (-1, 1):   # 볼털: 얼굴 아래 옆으로 뾰족하게 뻗은 흰 털 (작게, 아래·뒤로)
        for dy, ln, dz in ((-0.15, 0.62, 0.0), (-0.55, 0.45, 0.1)):
            a = np.array((s * 1.65, hc[1] + dy, hc[2] + 0.35 + dz))
            fig.add(S.cone(tuple(a), tuple(a + np.array((s * ln, -0.3, -0.12))), 0.36, 0.05), ORANGE, k=0.25)
    # 흰 얼굴 아래쪽 (주둥이·볼털)
    fig.paint(lambda P: np.maximum.reduce([S.ellipsoid((0, hc[1] - 0.95, hc[2] + 0.6), (2.9, 1.0, 1.9))(P), P[:, 1] - (hc[1] - 0.3) - 0.25 * np.abs(P[:, 0]) + 0.15 * np.maximum(0, np.abs(P[:, 0]) - 1.4) * 3, (y0 + 3.85) - P[:, 1]]), WHITE, soft=0.15)
    # 앞머리 털 (작은 불꽃 모양)
    for x, ang, ln in ((-0.25, 20, 0.55), (0.05, -5, 0.7), (0.32, -25, 0.5)):
        a = np.array((x, hc[1] + HR * 0.84, hc[2] + 0.5))
        fig.add(S.cone(tuple(a), tuple(a + S.rot(0, -35, ang) @ np.array((0, ln, 0))), 0.24, 0.04), ORANGE, k=0.22)
    # 얼굴 부품 자리 (귀 넣기 전 표면)
    eye_f = {s: head_frame(fig, hc, s * 27, 2) for s in (-1, 1)}
    dot_p = {s: np.array(head_frame(fig, hc, s * 24, 25).o) for s in (-1, 1)}
    blush_p = {s: np.array(head_frame(fig, hc, s * 44, -16).o) for s in (-1, 1)}
    nose_f = head_frame(fig, hc, 0, -19.5)
    mouth_f = head_frame(fig, hc, 0, -24)
    for s in (-1, 1):   # 눈 위 흰 점
        fig.paint(S.ellipsoid(tuple(dot_p[s]), (0.22, 0.14, 0.25)), WHITE, soft=0.03)
    # ---- 귀: 큰 삼각 귀, 짙은 끝, 밝은 안쪽 ----
    for s in (-1, 1):
        Re = S.rot(s * 12, -8, -s * 20)
        ec = np.array((s * 1.3, hc[1] + 1.3, hc[2] - 0.25))
        ear_l = lambda q: S.cone((0, 0, 0), (0, 2.0, 0), 0.85, 0.07)(q)
        ear = squash(ear_l, ec, Re, (1.0, 1.0, 0.45))
        inner = squash(lambda q: S.cone((0, 0.05, 0), (0, 1.7, 0), 0.62, 0.04)(q), ec + Re @ np.array((0, 0.0, 0.22)), Re, (1.0, 1.0, 0.35))
        fig.add(S.subtract(ear, inner, k=0.08), ORANGE, k=0.15)
        fig.paint(squash(lambda q: S.cone((0, 0.1, 0), (0, 1.7, 0), 0.6, 0.04)(q), ec + Re @ np.array((0, 0.0, 0.16)), Re, (1.0, 1.0, 0.4)), EAR_IN, soft=0.08)
        tip_c = ec + Re @ np.array((0, 2.05, 0))
        fig.paint(S.sphere(tuple(tip_c), 0.72), DARK, soft=0.25)
    # ---- 얼굴: 아몬드 눈 (눈꼬리 살짝 올라간 여유 있는 표정), 주둥이 끝 코, 'w' 입, 볼터치는 칠만 ----
    for s in (-1, 1):
        Fg.eye_at(fig, eye_f[s], 'almond', size=0.5, side=s, tilt=13)
        fig.paint(S.sphere(blush_p[s], 0.42), Fg.BLUSH, soft=0.5)
    Fg._ellipsoid(fig.extra, (0, 0, 0.02), (0.19, 0.13, 0.12), (0.12, 0.08, 0.08), 12, 6, nose_f)
    Fg._ellipsoid(fig.extra, (-0.05, 0.05, 0.1), (0.06, 0.03, 0.03), (0.6, 0.6, 0.6), 6, 4, nose_f)
    w_mouth(fig, mouth_f, 0.2, (0.25, 0.12, 0.1))
    # ---- 서류가방 (오른손) ----
    hand = np.array((1.66, y0 + 2.22, 0.3)) + (np.array((1.66, y0 + 2.22, 0.3)) - np.array((1.38, y0 + 2.78, 0.12))) * 0.35
    Rc = S.rot(25, 0, 0)
    cc = np.array((hand[0] + 0.1, hand[1] - 0.92, hand[2] + 0.05))
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
