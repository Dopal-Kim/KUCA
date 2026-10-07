"""전자정보대 고슴도치: 갈색 가시 (끝마다 밤에 빛나는 따뜻한 LED), 크림 얼굴·배, 진홍 쿠션 헤드셋 + 마이크,
회청색 조끼 (진홍 스티치), 손에 초록 회로 기판, 받침 위 마이크로칩"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

BROWN = (0.55, 0.35, 0.21)
BROWN_LT = (0.74, 0.52, 0.34)
SPINE = (0.50, 0.32, 0.20)
SPINE_TIP = (0.80, 0.62, 0.44)
CREAM = (0.99, 0.94, 0.86)
LED = (1.0, 0.86, 0.42)
VEST = (0.42, 0.53, 0.70)
VEST_DK = (0.44, 0.51, 0.62)
HP_W = (0.96, 0.94, 0.90)
PCB = (0.16, 0.52, 0.24)
CHIP = (0.13, 0.13, 0.15)
PIN = (0.82, 0.82, 0.85)


def fib_dirs(n):
    out = []
    ga = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        y = 1 - 2 * (i + 0.5) / n
        r = math.sqrt(1 - y * y)
        out.append(np.array((math.cos(ga * i) * r, y, math.sin(ga * i) * r)))
    return out


def spine_field(spines, bound_c, bound_r, inner_r, pad=0.5):
    """가시 묶음 하나의 SDF: 경계 타원체 밖 먼 점은 경계 거리 (빠름), 가까운 점만 가시별 계산"""
    A = np.array([s[0] for s in spines])
    B = np.array([s[1] for s in spines])
    R0 = np.array([s[2] for s in spines])
    R1 = np.array([s[3] for s in spines])
    bound = S.ellipsoid(bound_c, bound_r)
    inner = S.ellipsoid(bound_c, inner_r)

    def f(P):
        d = bound(P)
        out = np.where(inner(P) < -0.1, 1.0, d)
        near = np.where((d < pad) & (out < 1.0))[0]
        if len(near):
            Q = P[near]
            best = np.full(len(Q), 1e9)
            for a, b, r0, r1 in zip(A, B, R0, R1):
                ba = b - a
                pa = Q - a
                h = np.clip(pa @ ba / (ba @ ba), 0, 1)
                dd = np.linalg.norm(pa - h[:, None] * ba, axis=1) - (r0 + (r1 - r0) * h)
                best = np.minimum(best, dd)
            out[near] = best
        return out
    return f


def build(fig, rng):
    Fg.base(fig, rng, flowers=6)
    y0 = Fg.TOP
    g = (Fg.GOLD, Fg.GOLD_HI)
    # ---- 다리·발 ----
    for s in (-1, 1):
        x = s * 0.68
        fig.add(S.ellipsoid((x, y0 + 0.3, 0.25), (0.52, 0.32, 0.62)), CREAM, k=0.1, layer='feet')
        fig.add(S.capsule((x, y0 + 0.55, 0.05), (x, y0 + 1.45, 0.0), 0.5, 0.56), BROWN, k=0.25)
    # ---- 몸통: 갈색 + 크림 배 ----
    hips = S.ellipsoid((0, y0 + 1.95, 0.0), (1.42, 0.95, 1.18))
    chest = S.ellipsoid((0, y0 + 2.8, 0.0), (1.28, 1.0, 1.08))
    fig.add(hips, BROWN, k=0.4)
    fig.add(chest, BROWN, k=0.45)
    # 조끼: 앞이 트인 회청색 조끼 + 진홍 스티치
    VR = (1.5, 1.2, 1.27)
    vest0 = lambda P: S.smin(hips(P), chest(P), 0.45) - 0.1
    vest = S.intersect(vest0, lambda P: np.maximum((y0 + 1.75) - P[:, 1], P[:, 1] - (y0 + 3.6)))
    opening = lambda P: (np.abs(P[:, 0]) - 0.3 - 0.25 * np.clip((P[:, 1] - (y0 + 2.4)) / 1.1, 0, 1)) * 1.0
    vest = S.subtract(vest, S.intersect(opening, lambda P: 0.15 - P[:, 2]), k=0.05)
    for s in (-1, 1):     # 팔 구멍
        vest = S.subtract(vest, S.ellipsoid((s * 1.42, y0 + 3.2, 0.05), (0.42, 0.55, 0.55)), k=0.05)
    fig.add(vest, VEST, k=0.0, layer='vest')
    fig.paint(lambda P: np.maximum(S.ellipsoid((0, y0 + 2.2, 0.9), (0.95, 1.3, 0.8))(P), 0.25 - 6 * vest(P)), CREAM, soft=0.2)
    # 스티치: 앞트임·밑단을 따라 진홍 점선
    vest_shell = lambda P: np.abs(vest(P)) - 0.04
    def stitch(P):
        ax = np.abs(P[:, 0])
        edge = np.abs(ax - (0.38 + 0.25 * np.clip((P[:, 1] - (y0 + 2.4)) / 1.1, 0, 1)))
        hem = np.abs(P[:, 1] - (y0 + 1.86))
        line = np.minimum(np.where(P[:, 2] > 0.3, edge, 9), hem)
        return np.maximum(line - 0.05, vest_shell(P))
    fig.paint(stitch, Fg.CRIMSON, soft=0.04)
    # ---- 팔: 갈색 + 크림 손 ----
    hands = {}
    for s in (-1, 1):
        sh = np.array((s * 1.18, y0 + 3.15, 0.05))
        if s > 0:   # 오른손(+x): 기판을 가슴 앞으로 들어 올림
            el, wr = np.array((1.75, y0 + 2.5, 0.45)), np.array((1.45, y0 + 2.75, 1.05))
        else:
            el, wr = np.array((-1.65, y0 + 2.55, 0.15)), np.array((-1.95, y0 + 2.0, 0.35))
        fig.add(S.sphere(tuple(sh), 0.46), BROWN, k=0.3)
        fig.add(S.capsule(tuple(sh), tuple(el), 0.42, 0.38), BROWN, k=0.25)
        fig.add(S.capsule(tuple(el), tuple(wr), 0.38, 0.36), BROWN, k=0.2)
        hand = wr + (wr - el) / np.linalg.norm(wr - el) * 0.25
        fig.add(S.sphere(tuple(hand), 0.36), CREAM, k=0.12, layer='paw')
        hands[s] = hand
    # ---- 회로 기판 (오른손) ----
    hb = hands[1]
    Rb = S.rot(-12, -8, 8)
    bc = hb + np.array((-0.05, 0.55, 0.32))
    fig.add(S.box(tuple(bc), (0.62, 0.72, 0.045), round_=0.03, R=Rb), PCB, k=0.0, layer='pcb')
    fig.add(S.box(tuple(bc + Rb @ np.array((0.02, 0.05, 0.06))), (0.26, 0.26, 0.045), round_=0.02, R=Rb), CHIP, k=0.0, layer='chip')
    for cx, cy in ((-0.45, 0.55), (0.45, 0.55), (-0.45, -0.55), (0.45, -0.55)):
        fig.add(S.cylinder(tuple(bc + Rb @ np.array((cx, cy, 0.03))), 0.07, 0.03, R=Rb @ S.rot(0, 90, 0)), Fg.GOLD, k=0.0, layer='pin', metal=g)
    for cx, cy, w, h in ((-0.32, -0.42, 0.12, 0.08), (0.34, 0.44, 0.12, 0.07)):
        fig.add(S.box(tuple(bc + Rb @ np.array((cx, cy, 0.06))), (w, h, 0.035), round_=0.01, R=Rb), (0.22, 0.22, 0.24), k=0.0, layer='chip')
    # 엄지가 기판을 잡도록
    # ---- 머리 ----
    hc = (0, y0 + 5.45, 0.05)
    HR = 2.0
    head_f = S.ellipsoid(hc, (HR * 1.08, HR * 0.97, HR))
    fig.add(head_f, BROWN, k=0.5)
    fig.add(S.capsule((0, y0 + 3.4, 0), (0, hc[1] - 1.4, 0.05), 0.75), BROWN, k=0.3)
    for s in (-1, 1):
        fig.add(S.sphere((s * 1.0, hc[1] - 0.85, hc[2] + 0.95), 0.72), BROWN, k=0.55)
    fig.add(S.ellipsoid((0, hc[1] - 0.6, hc[2] + 1.62), (0.6, 0.45, 0.5)), BROWN, k=0.45)   # 작은 주둥이
    face = S.ellipsoid((0, hc[1] - 0.2, hc[2] + 1.2), (1.8, 1.65, 1.35))
    fig.paint(lambda P: np.maximum.reduce([face(P), -S.ellipsoid((0, hc[1] + 1.45, hc[2] + 1.5), (0.3, 0.55, 0.9))(P), head_f(P) - 0.4, (hc[1] - 2.1) - P[:, 1]]), CREAM, soft=0.06)
    # ---- 가시: 머리 위·뒤 + 등. 끝마다 LED ----
    spines = []
    tips = []
    for d in fib_dirs(110):
        in_face = d[2] > 0.3 and abs(d[0]) < 0.78 and d[1] < 0.62
        if in_face or d[1] < -0.6 or (d[2] > 0.0 and d[1] < -0.2):
            continue
        if abs(d[2] - 0.05) < 0.13 and d[1] > 0.2:      # 헤드셋 밴드 자리 (가르마)
            continue
        if abs(d[0]) > 0.8 and abs(d[1]) < 0.3 and abs(d[2]) < 0.45:      # 헤드셋 컵 자리
            continue
        p = np.array(hc) + d * np.array((HR * 1.08, HR * 0.97, HR)) * 0.95
        dd = d + np.array((0, 0.15, -0.25))
        dd /= np.linalg.norm(dd)
        L = 1.0 + 0.15 * rng.random()
        tip = p + dd * L
        spines.append((p, tip, 0.5, 0.12))
        tips.append(tip + dd * 0.06)
    fig.add(spine_field(spines, hc, (HR * 1.08 + 1.4, HR * 0.97 + 1.4, HR + 1.4), (HR * 0.75, HR * 0.7, HR * 0.75)), SPINE, k=0.12)
    bspines = []
    bc_ = np.array((0, y0 + 2.7, 0.0))
    for d in fib_dirs(52):
        if d[2] > -0.35 or d[1] < -0.45 or d[1] > 0.8:
            continue
        p = bc_ + d * np.array((1.5, 1.2, 1.27)) * 0.9
        dd = d + np.array((0, 0.25, -0.3))
        dd /= np.linalg.norm(dd)
        tip = p + dd * (0.9 + 0.1 * rng.random())
        bspines.append((p, tip, 0.46, 0.12))
        tips.append(tip + dd * 0.06)
    fig.add(spine_field(bspines, tuple(bc_), (2.6, 2.3, 2.5), (1.0, 0.8, 0.85)), SPINE, k=0.12)
    # 가시 끝은 밝은 갈색
    tipsA = np.array(tips)
    def tipglow(P):
        d = np.full(len(P), 9.0)
        for t in tipsA:
            d = np.minimum(d, np.linalg.norm(P - t, axis=1))
        return d - 0.38
    fig.paint(tipglow, SPINE_TIP, soft=0.35)
    fig.extra.emissive = True
    for t in tips:
        Fg._ellipsoid(fig.extra, tuple(t), (0.13, 0.13, 0.13), LED, 10, 6)
    fig.extra.emissive = False
    # ---- 헤드셋: 크림 밴드, 진홍 쿠션, 금 테, 마이크 ----
    band = S.intersect(S.torus((0, hc[1] - 0.35, hc[2] + 0.1), 2.85, 0.19, Rm=S.rot(0, 90, 0)), lambda P: hc[1] - 0.05 - P[:, 1])
    fig.add(band, HP_W, k=0.0, layer='hp')
    Rc = S.rot(0, 0, 90)
    for s in (-1, 1):
        cx = s * (HR * 1.08)
        fig.add(S.torus((cx + s * 0.12, hc[1] - 0.15, hc[2] - 0.05), 0.55, 0.25, Rm=Rc), Fg.CRIMSON, k=0.0, layer='cush')
        fig.add(S.cylinder((cx + s * 0.42, hc[1] - 0.15, hc[2] - 0.05), 0.72, 0.17, round_=0.12, R=Rc), HP_W, k=0.0, layer='hp')
        fig.add(S.torus((cx + s * 0.56, hc[1] - 0.15, hc[2] - 0.05), 0.52, 0.07, Rm=Rc), Fg.GOLD, k=0.0, layer='hpg', metal=g)
        fig.add(S.cylinder((cx + s * 0.58, hc[1] - 0.15, hc[2] - 0.05), 0.46, 0.05, round_=0.04, R=Rc), Fg.CRIMSON, k=0.0, layer='cush')
        fig.add(S.cylinder((s * 2.64, hc[1] + 0.75, hc[2] + 0.1), 0.24, 0.17, round_=0.06), Fg.GOLD, k=0.0, layer='hpg', metal=g)
    # 마이크 (왼쪽 컵 → 입 옆)
    m0 = np.array((-(HR * 1.08 + 0.45), hc[1] - 0.45, hc[2] + 0.3))
    m1 = np.array((-1.6, hc[1] - 1.05, hc[2] + 1.35))
    m2 = np.array((-0.85, hc[1] - 1.05, hc[2] + 1.85))
    fig.add(S.capsule(tuple(m0), tuple(m1), 0.06), (0.25, 0.25, 0.28), k=0.04, layer='mic')
    fig.add(S.capsule(tuple(m1), tuple(m2), 0.06), (0.25, 0.25, 0.28), k=0.04, layer='mic')
    fig.add(S.sphere(tuple(m2 + np.array((0.08, 0.0, 0.03))), 0.2), (0.16, 0.16, 0.18), k=0.03, layer='mic')
    # ---- 얼굴 ----
    Fg.kawaii_eyes(fig, hc, HR + 0.02, spread=25, pitch=-6, size=0.52, tall=1.1)
    nf = Fg._frame_on((0, hc[1] - 0.6, hc[2] + 1.62), 0.5, 0, 12, -0.02)
    Fg._ellipsoid(fig.extra, (0, 0, 0), (0.16, 0.12, 0.12), (0.14, 0.09, 0.08), 12, 6, nf)
    Fg._ellipsoid(fig.extra, (-0.05, 0.05, 0.08), (0.05, 0.03, 0.03), (0.6, 0.6, 0.6), 6, 4, nf)
    Fg.smile(fig, (0, hc[1] - 0.6, hc[2] + 1.62), 0.5 + 0.03, pitch=-38, w=0.18, col=(0.3, 0.14, 0.12))
    for s in (-1, 1):
        p = np.array(Fg._frame_on(hc, HR * 1.04, s * 44, -22, 0).p((0, 0, 0)))
        fig.paint(S.sphere(p, 0.45), Fg.BLUSH, soft=0.55)
    # ---- 받침 소품: 마이크로칩 (왼쪽 앞) + 회색 돌 ----
    cx_, cz_ = -2.5, 1.9
    Rk = S.rot(25, 0, 0)
    fig.add(S.box((cx_, y0 + 0.2, cz_), (0.62, 0.15, 0.42), round_=0.04, R=Rk), CHIP, k=0.0, layer='mchip')
    fig.add(S.cylinder(tuple(np.array((cx_, y0 + 0.36, cz_)) + Rk @ np.array((-0.4, 0, -0.22))), 0.07, 0.01), (0.3, 0.3, 0.33), k=0.0, layer='mchip')
    for k in range(5):
        for s in (-1, 1):
            a = np.array((cx_, y0 + 0.2, cz_)) + Rk @ np.array(((k - 2) * 0.25, 0, s * 0.42))
            b = np.array((cx_, y0 + 0.02, cz_)) + Rk @ np.array(((k - 2) * 0.25, 0, s * 0.6))
            fig.add(S.capsule(tuple(a), tuple(b), 0.045), PIN, k=0.0, layer='mpin', metal=(PIN, (1, 1, 1)))
    rock = lambda P: S.ellipsoid((2.9, y0 + 0.25, 1.6), (0.75, 0.5, 0.6))(P) + 0.03 * np.sin(P[:, 0] * 9) * np.sin(P[:, 2] * 8)
    fig.add(rock, (0.66, 0.66, 0.68), k=0.0, layer='rock')
    # 마이크는 얼굴 칠에 덮이지 않게 다시 칠
    fig.paint(lambda P: S.sphere(tuple(m2 + np.array((0.08, 0.0, 0.03))), 0.2)(P) - 0.04, (0.16, 0.16, 0.18), soft=0.02)
    fig.paint(lambda P: np.minimum(S.capsule(tuple(m0), tuple(m1), 0.06)(P), S.capsule(tuple(m1), tuple(m2), 0.06)(P)) - 0.03, (0.25, 0.25, 0.28), soft=0.02)
    # 기판: 얼굴 칠이 번지지 않게 다시 칠하고 금색 배선
    board = S.box(tuple(bc), (0.62, 0.72, 0.045), round_=0.03, R=Rb)
    fig.paint(lambda P: board(P) - 0.025, PCB, soft=0.02)
    fig.paint(lambda P: np.maximum(np.abs(np.sin(((P - bc) @ Rb)[:, 0] * 14)) - 0.07, np.maximum(board(P) - 0.02, np.abs(((P - bc) @ Rb)[:, 1]) - 0.62)), (0.80, 0.68, 0.26), soft=0.02)
