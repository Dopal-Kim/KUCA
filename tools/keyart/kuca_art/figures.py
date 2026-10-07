"""
고품질 수집 동물 피규어 (SDF 조형, sculpt.py). 키아트 3D 레퍼런스 시트를 보고 만든다.

한 캐릭터 = 몸통 조형(부드럽게 녹아 붙은 하나의 표면, 접촉 그늘 구움) + 얼굴 부품(눈·코·입, 또렷한 별도 메시)
           + 받침(등급 색 금속 테 + 잔디 + 꽃).
좌표: 받침 중심 원점, +Y 위, +Z 얼굴 앞, m. 받침 윗면 y = TOP.
"""
import math
import random

import numpy as np

from . import sculpt as S
from .mesh import Builder, Frame, IDENT

TOP = 1.05

# ---------- 팔레트 (sRGB) ----------
WHITE = (0.985, 0.975, 0.965)
SUIT = (0.96, 0.955, 0.95)
GOLD = (0.99, 0.74, 0.20)
GOLD_HI = (1.0, 0.93, 0.62)
SILVER = (0.80, 0.82, 0.86)
PINK = (0.99, 0.70, 0.74)
BLUSH = (1.0, 0.62, 0.66)
IRIS = (0.36, 0.20, 0.11)
IRIS_HI = (0.62, 0.38, 0.20)
PUPIL = (0.07, 0.04, 0.04)
SHINE = (1.0, 1.0, 1.0)
CRIMSON = (0.72, 0.12, 0.20)
RED = (0.88, 0.20, 0.20)
GRASS = (0.42, 0.72, 0.22)
GRASS_HI = (0.62, 0.86, 0.34)
RIMS = {'green': ((0.36, 0.74, 0.30), (0.70, 0.94, 0.56)), 'blue': ((0.25, 0.55, 0.92), (0.66, 0.86, 1.0)),
        'gold': ((0.96, 0.66, 0.12), (1.0, 0.92, 0.55))}


# ---------- 금속·광택 색 (버텍스 색으로 굽는 가짜 반사) ----------

def metal_shade(C, N, base, hi):
    """금속 부위: 위를 볼수록 밝은 반사, 옆 아래는 짙은 색 + 가로 반사 띠"""
    ny = N[:, 1]
    t = np.clip(0.55 + 0.45 * ny, 0, 1) ** 1.6
    band = np.exp(-((ny - 0.25) / 0.12) ** 2) * 0.35
    dark = np.asarray(base) * 0.72
    col = dark[None, :] * (1 - t[:, None]) + np.asarray(hi)[None, :] * t[:, None]
    return np.clip(col + band[:, None] * 0.6, 0, 1)


class Figure:
    """조형 + 금속 부위 표시 + 얼굴 부품 Builder"""

    def __init__(self, tier):
        self.s = S.Sculpt()
        self.metal = []           # (part index, base, hi)
        self.extra = Builder()    # 눈·코·입 등 또렷한 부품
        self.extra.ao_strength = 0.0
        self.tier = tier

    def add(self, f, col, k=0.2, layer='body', metal=None):
        self.s.add(f, col, k, layer)
        if metal:
            self.metal.append((len(self.s.parts) - 1, metal[0], metal[1]))

    def paint(self, f, col, soft=0.05):
        self.s.paint(f, col, soft)

    def build(self, tris, voxel=0.05, ao=0.55, lo=(-4.8, -0.05, -4.8), hi=(4.8, 11.8, 4.8)):
        V, F, C = self.s.build(lo, hi, voxel, tris, ao=ao)
        if self.metal:
            dmin, per = self.s.field(V, with_parts=True)
            D = np.stack(per, axis=1)
            owner = D.argmin(axis=1)
            N = self.s.normals(V)
            for idx, base, hi_ in self.metal:
                m = owner == idx
                if m.any():
                    ao_keep = C[m] / np.maximum(np.asarray(self.s.parts[idx][1])[None, :], 1e-3)
                    shade = np.clip(ao_keep.mean(axis=1, keepdims=True), 0.5, 1.0)
                    C[m] = metal_shade(C[m], N[m], base, hi_) * shade
        return V, F, C


# ---------- 공통: 받침 ----------

def base(fig, rng, flowers=7):
    rim, rim_hi = RIMS[fig.tier]
    fig.add(S.cylinder((0, 0.5, 0), 4.5, 0.5, round_=0.22), rim, k=0.0, layer='base', metal=(rim, rim_hi))
    grass_top = lambda P: S.cylinder((0, TOP - 0.12, 0), 4.22, 0.14, round_=0.12)(P) - 0.05 * np.sin(P[:, 0] * 3.1) * np.sin(P[:, 2] * 2.7)
    fig.add(grass_top, GRASS, k=0.0, layer='grass')
    fig.paint(lambda P: 0.15 - np.abs(np.sin(P[:, 0] * 1.7 + P[:, 2] * 1.3) * np.cos(P[:, 2] * 1.9)) - (TOP - 0.3 - P[:, 1]) * 9, GRASS_HI, soft=0.4)
    b = fig.extra
    for k in range(70):   # 잔디 잎
        a = rng.uniform(0, 2 * math.pi)
        d = math.sqrt(rng.uniform(0.0, 1.0)) * 4.05
        x, z = math.cos(a) * d, math.sin(a) * d
        if abs(x) < 1.8 and -1.2 < z < 1.8:
            continue
        h = rng.uniform(0.25, 0.5)
        b.cone(Frame.yaw((x, TOP - 0.06, z), rng.uniform(0, 90)), (0, 0, 0), 0.09, h, 4, GRASS_HI if k % 3 else GRASS)
    placed = 0
    while placed < flowers:   # 데이지
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(2.3, 3.9)
        x, z = math.cos(a) * d, math.sin(a) * d
        placed += 1
        for p in range(6):
            pa = p * math.pi / 3
            _ellipsoid(b, (x + math.cos(pa) * 0.16, TOP + 0.07, z + math.sin(pa) * 0.16), (0.12, 0.04, 0.12), WHITE, 8, 4)
        _ellipsoid(b, (x, TOP + 0.1, z), (0.08, 0.05, 0.08), (1.0, 0.80, 0.25), 8, 4)


# ---------- 또렷한 부품 (Builder) ----------

def _ellipsoid(mb, c, r, col, seg=16, rings=10, m=IDENT):
    def P(i, j):
        th = -math.pi / 2 + math.pi * i / rings
        ph = 2 * math.pi * j / seg
        return (c[0] + r[0] * math.cos(th) * math.cos(ph), c[1] + r[1] * math.sin(th), c[2] + r[2] * math.cos(th) * math.sin(ph))
    for i in range(rings):
        for j in range(seg):
            a, b_, d, e = P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)
            if i < rings - 1:
                mb.tri(m, a, b_, d, col, c)
            if i > 0:
                mb.tri(m, a, d, e, col, c)


def _frame_on(center, R, yaw, pitch, out=0.0):
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    p = (center[0] + (R + out) * cp * sy, center[1] + (R + out) * sp, center[2] + (R + out) * cp * cy)
    Rm = S.rot(yaw, -pitch, 0)
    return Frame(p, tuple(Rm[:, 0]), tuple(Rm[:, 1]), tuple(Rm[:, 2]))


def kawaii_eyes(fig, head_c, R, spread=24, pitch=-12, size=0.62, iris=IRIS, tall=1.12, look=0.0):
    """키아트 눈: 큰 갈색 홍채(아래로 밝아짐) + 검은 동공 + 큰 흰 반짝임 + 작은 반짝임, 살짝 볼록"""
    b = fig.extra
    for s in (-1, 1):
        f = _frame_on(head_c, R, s * spread, pitch, -size * 0.18)
        _ellipsoid(b, (0, 0, 0), (size * 0.9, size * tall, size * 0.34), PUPIL, 20, 10, f)
        _ellipsoid(b, (0, -size * 0.18, size * 0.06), (size * 0.78, size * tall * 0.78, size * 0.32), iris, 20, 10, f)
        _ellipsoid(b, (0, -size * 0.42, size * 0.1), (size * 0.5, size * 0.38, size * 0.28), IRIS_HI if iris == IRIS else tuple(min(1, c * 1.5) for c in iris), 16, 8, f)
        _ellipsoid(b, (0, size * 0.05, size * 0.16), (size * 0.42, size * 0.5, size * 0.28), PUPIL, 16, 8, f)
        _ellipsoid(b, (-size * 0.3 * s + look, size * 0.36, size * 0.3), (size * 0.3, size * 0.3, size * 0.12), SHINE, 12, 6, f)
        _ellipsoid(b, (size * 0.32 * s, -size * 0.45, size * 0.3), (size * 0.12, size * 0.12, size * 0.06), SHINE, 8, 4, f)


def smile(fig, head_c, R, pitch=-30, w=0.32, col=(0.30, 0.16, 0.16), open_=False):
    """'w' 입 (작은 곡선 두 개). open_ 이면 분홍 혀가 보이는 열린 입"""
    b = fig.extra
    f = _frame_on(head_c, R, 0, pitch, -0.04)
    if open_:
        _ellipsoid(b, (0, -w * 0.15, 0), (w * 0.9, w * 0.75, 0.1), (0.42, 0.14, 0.16), 14, 8, f)
        _ellipsoid(b, (0, -w * 0.45, 0.04), (w * 0.6, w * 0.35, 0.08), (0.98, 0.50, 0.55), 12, 6, f)
        return
    for s in (-1, 1):
        for k in range(9):
            a = math.pi * k / 8
            _ellipsoid(b, (s * w * 0.5 + math.cos(a) * w * 0.5, -math.sin(a) * w * 0.42, 0.02), (0.055, 0.055, 0.04), col, 6, 4, f)


def nose(fig, head_c, R, pitch=-20, size=0.16, col=PINK):
    f = _frame_on(head_c, R, 0, pitch, -0.02)
    _ellipsoid(fig.extra, (0, 0, 0), (size * 1.2, size * 0.8, size * 0.6), col, 12, 6, f)


def blush(fig, head_c, R, spread=46, pitch=-24, size=0.55):
    for s in (-1, 1):
        p = np.array(_frame_on(head_c, R, s * spread, pitch, 0).p((0, 0, 0)))
        fig.paint(S.sphere(p, size), BLUSH, soft=size * 1.1)


# ---------- 공학관 우주 토끼 ----------

def space_rabbit(fig, rng):
    base(fig, rng)
    W = SUIT
    g = (GOLD, GOLD_HI)
    y0 = TOP
    # 다리·장화
    for s in (-1, 1):
        fig.add(S.capsule((s * 0.78, y0 + 0.9, 0.05), (s * 0.82, y0 + 2.1, 0.0), 0.74, 0.8), W, k=0.3)
        fig.add(S.ellipsoid((s * 0.84, y0 + 0.5, 0.28), (0.78, 0.55, 0.98)), W, k=0.3)
        fig.add(S.intersect(S.ellipsoid((s * 0.82, y0 + 1.05, 0.12), (0.92, 0.9, 0.95)),
                            S.box((s * 0.82, y0 + 1.05, 0.1), (1.2, 0.13, 1.2))), GOLD, k=0.0, layer='trim', metal=g)
    # 몸통 (배불뚝이 우주복)
    fig.add(S.ellipsoid((0, y0 + 2.75, 0.05), (1.95, 1.45, 1.7)), W, k=0.45)
    fig.add(S.ellipsoid((0, y0 + 3.6, 0.0), (1.7, 1.25, 1.5)), W, k=0.45)
    fig.add(S.intersect(S.ellipsoid((0, y0 + 2.95, 0.05), (2.04, 1.55, 1.8)), S.box((0, y0 + 2.95, 0), (3, 0.17, 3))), GOLD, k=0.0, layer='trim', metal=g)
    fig.add(S.box((0, y0 + 2.95, 1.78), (0.36, 0.26, 0.1), round_=0.07, R=S.rot(0, -8, 0)), GOLD, k=0.0, layer='trim', metal=g)
    fig.paint(S.box((0, y0 + 2.95, 1.86), (0.2, 0.12, 0.2)), SILVER, soft=0.02)
    fig.add(S.torus((0, y0 + 3.75, 1.47), 0.3, 0.07, Rm=S.rot(0, 90 - 18, 0)), GOLD, k=0.0, layer='trim', metal=g)   # 가슴 단추
    fig.paint(S.box((-1.45, y0 + 3.9, 0.55), (0.25, 0.08, 0.4), R=S.rot(-35, 0, 0)), RED, soft=0.02)               # 어깨 깃발
    fig.paint(S.box((-1.48, y0 + 3.72, 0.55), (0.25, 0.06, 0.4), R=S.rot(-35, 0, 0)), RED, soft=0.02)
    fig.paint(S.sphere((1.28, y0 + 4.05, 0.95), 0.1), CRIMSON, soft=0.03)
    for s in (-1, 1):   # 허리 회색 주머니
        fig.add(S.box((s * 1.35, y0 + 2.6, 1.1), (0.3, 0.35, 0.18), round_=0.1, R=S.rot(s * 38, 0, 0)), (0.66, 0.68, 0.72), k=0.06)
    # 팔·장갑·금색 손목
    for s, elbow, hand in ((-1, (-2.05, y0 + 3.0, 0.35), (-2.05, y0 + 2.45, 0.55)), (1, (2.05, y0 + 3.0, 0.35), (2.05, y0 + 2.45, 0.55))):
        fig.add(S.capsule((s * 1.45, y0 + 3.85, 0.0), elbow, 0.58, 0.52), W, k=0.3)
        fig.add(S.sphere(hand, 0.55), WHITE, k=0.12)
        fig.add(S.capsule((s * 2.27, y0 + 2.6, 0.5), (s * 2.0, y0 + 2.2, 0.85), 0.2), WHITE, k=0.15)   # 엄지
        c = np.array(elbow) * 0.35 + np.array(hand) * 0.65
        fig.add(S.torus(tuple(c), 0.5, 0.12, Rm=S.rot(0, 0, s * 10)), GOLD, k=0.0, layer='trim', metal=g)
    # 금색 코일 호스 (벨트 왼쪽 → 제트팩)
    for k in range(26):
        t = k / 25
        x, y, z = -1.85 - 0.25 * math.sin(t * math.pi), y0 + 2.9 - 0.75 * math.sin(t * math.pi), 0.9 - 2.2 * t
        a = t * 22 * math.pi
        fig.add(S.sphere((x + math.cos(a) * 0.1, y + math.sin(a) * 0.1, z), 0.11), GOLD, k=0.0, layer='trim', metal=g)
    # 목 고리
    fig.add(S.torus((0, y0 + 4.55, 0.05), 1.1, 0.24), GOLD, k=0.0, layer='trim', metal=g)
    # 머리 (헬멧 = 둥근 머리) + 귀 혹
    hc = (0, y0 + 6.55, 0.1)
    HR = 2.35
    fig.add(S.ellipsoid(hc, (HR * 1.04, HR * 0.97, HR)), WHITE, k=0.35, layer='head')
    for s in (-1, 1):
        a, b = (s * 0.82, hc[1] + 1.6, -0.1), (s * 1.05, hc[1] + 3.75, -0.25)
        fig.add(S.capsule(a, b, 0.66, 0.6), WHITE, k=0.45, layer='head')
        fig.paint(S.ellipsoid((s * 1.0, hc[1] + 3.0, 0.42), (0.36, 0.95, 0.3), R=S.rot(0, 0, -s * 6)), PINK, soft=0.08)
        # 헬멧 옆 금색 통신 포트
        fig.add(S.torus((s * 2.38, hc[1] - 0.1, 0.05), 0.52, 0.14, Rm=S.rot(0, 0, 90)), GOLD, k=0.0, layer='trim', metal=g)
        fig.add(S.ellipsoid((s * 2.36, hc[1] - 0.1, 0.05), (0.14, 0.45, 0.45)), WHITE, k=0.05, layer='trim')
    fig.add(S.sphere((0.25, hc[1] + 2.35, -0.15), 0.2), (0.80, 0.52, 0.22), k=0.08, layer='head')     # 꼭대기 단추
    # 헬멧 앞 테 (얼굴을 두른 은백색 고리) + 유리 반사
    fig.add(S.torus((0, hc[1] - 0.15, 1.38), 1.92, 0.15, Rm=S.rot(0, 90 - 6, 0)), (0.92, 0.93, 0.96), k=0.0, layer='trim', metal=((0.86, 0.88, 0.92), (1.0, 1.0, 1.0)))
    fig.paint(S.ellipsoid((-1.0, hc[1] + 1.0, 1.9), (0.55, 0.22, 0.3), R=S.rot(0, 0, 35)), (0.93, 0.97, 1.0), soft=0.12)
    # 제트팩 (등)
    fig.add(S.box((0, y0 + 3.4, -1.75), (1.15, 1.1, 0.55), round_=0.28), (0.93, 0.93, 0.95), k=0.08)
    fig.add(S.torus((0, y0 + 3.7, -2.32), 0.32, 0.09, Rm=S.rot(0, 90, 0)), GOLD, k=0.0, layer='trim', metal=g)
    fig.add(S.sphere((0, y0 + 3.7, -2.3), 0.22), GOLD, k=0.0, layer='trim', metal=g)
    for k in range(3):
        fig.paint(S.box((0, y0 + 3.15 - k * 0.18, -2.3), (0.35, 0.04, 0.1)), (0.62, 0.64, 0.68), soft=0.02)
    for s in (-1, 1):
        fig.add(S.cylinder((s * 1.2, y0 + 3.3, -1.75), 0.32, 0.6, round_=0.12), (0.88, 0.88, 0.9), k=0.05)
        fig.add(S.capsule((s * 1.2, y0 + 2.6, -1.75), (s * 1.2, y0 + 2.25, -1.75), 0.3, 0.38), GOLD, k=0.0, layer='trim', metal=g)
    # 받침 소품: 로켓 · 달 돌 · 톱니
    rx, rz = -3.0, 1.4
    fig.add(S.capsule((rx, y0 + 0.35, rz), (rx, y0 + 1.6, rz), 0.42, 0.34), WHITE, k=0.0, layer='prop')
    fig.add(S.capsule((rx, y0 + 1.55, rz), (rx, y0 + 2.25, rz), 0.36, 0.05), RED, k=0.0, layer='prop')
    fig.add(S.torus((rx, y0 + 1.15, rz + 0.36), 0.16, 0.05, Rm=S.rot(0, 90, 0)), GOLD, k=0.0, layer='prop', metal=g)
    for k in range(3):
        R = S.rot(k * 120 + 30, 0, 0)
        fig.add(S.box(tuple(np.array((rx, y0 + 0.45, rz)) + R @ np.array((0.5, 0, 0))), (0.22, 0.32, 0.05), round_=0.04, R=R), RED, k=0.0, layer='prop')
    rock = lambda P: S.sphere((2.8, y0 + 0.35, 1.7), 0.75)(P) + 0.05 * np.sin(P[:, 0] * 9) * np.sin(P[:, 2] * 8)
    fig.add(rock, (0.66, 0.66, 0.68), k=0.0, layer='prop')
    for cc in ((2.45, y0 + 0.75, 2.2), (3.25, y0 + 0.8, 1.8), (2.75, y0 + 1.0, 1.35)):
        fig.paint(S.sphere(cc, 0.18), (0.48, 0.48, 0.52), soft=0.05)
    gear = (1.4, y0 + 0.12, 2.9)
    fig.add(S.cylinder(gear, 0.42, 0.12, round_=0.04), SILVER, k=0.0, layer='prop', metal=(SILVER, (1, 1, 1)))
    for k in range(8):
        R = S.rot(k * 45, 0, 0)
        fig.add(S.box(tuple(np.array(gear) + R @ np.array((0.48, 0, 0))), (0.1, 0.11, 0.09), round_=0.03, R=R), SILVER, k=0.0, layer='prop', metal=(SILVER, (1, 1, 1)))
    # 얼굴
    kawaii_eyes(fig, hc, HR, spread=25, pitch=-12, size=0.6)
    nose(fig, hc, HR, pitch=-22, size=0.14)
    smile(fig, hc, HR, pitch=-29, w=0.3)
    blush(fig, hc, HR)
    # 오른손 스패너 (공구)
    w = S.rot(0, 0, -20)
    fig.add(S.capsule((2.05, y0 + 2.2, 0.75), (2.45, y0 + 3.4, 0.8), 0.12), SILVER, k=0.0, layer='tool', metal=(SILVER, (1, 1, 1)))
    fig.add(S.capsule((2.05, y0 + 2.25, 0.75), (2.15, y0 + 2.6, 0.76), 0.17), GOLD, k=0.0, layer='tool', metal=g)
    fig.add(S.subtract(S.cylinder((2.55, y0 + 3.62, 0.8), 0.32, 0.09, R=S.rot(0, 90, -20)),
                       S.box((2.62, y0 + 3.85, 0.8), (0.12, 0.25, 0.3), R=w)), SILVER, k=0.0, layer='tool', metal=(SILVER, (1, 1, 1)))


FIGURES = {
    'space_rabbit': ('gold', space_rabbit),
}


def build(cid, tris=16000, seed=3):
    tier, fn = FIGURES[cid]
    fig = Figure(tier)
    fn(fig, random.Random(seed))
    V, F, C = fig.build(tris)
    mb = Builder()
    S.add_to_builder(mb, V, F, C)
    mb.pos.extend(fig.extra.pos)
    mb.col.extend(fig.extra.col)
    return mb
