"""예술대 카멜레온: 파스텔 무지개 몸, 금테 큰 눈, 진홍 베레모, 왼손 붓 · 오른손 팔레트, 갈색 가방(물감 튜브), 말린 꼬리, 바닥 물감 자국."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .clock_rooster import Fast, hit, layer_paint, eye1, beads

TIER = 'gold'

MINT = (0.46, 0.89, 0.66)
PINKP = (1.0, 0.56, 0.76)
YELLOWP = (1.0, 0.88, 0.36)
BLUEP = (0.46, 0.70, 1.0)
LILAC = (0.70, 0.54, 0.98)
PEACH = (1.0, 0.66, 0.46)
BELLY = (0.99, 0.93, 0.74)
BERET = (0.74, 0.07, 0.20)
LEATHER = (0.55, 0.32, 0.16)
LEATHER_DK = (0.42, 0.23, 0.11)
WOOD = (0.86, 0.66, 0.42)
HANDLE = (0.52, 0.28, 0.13)
G = (Fg.GOLD, Fg.GOLD_HI)


def build(fig, rng):
    fig = Fast(fig)
    Fg.base(fig, rng, flowers=8)
    y0 = Fg.TOP
    B = MINT

    # ---------- 다리·발 ----------
    for s in (-1, 1):
        x = s * 0.72
        fig.add(S.capsule((x, y0 + 0.35, 0.1), (x * 1.02, y0 + 1.4, 0.0), 0.42, 0.46), B, k=0.25)
        fig.add(S.ellipsoid((x, y0 + 0.24, 0.32), (0.5, 0.26, 0.58)), B, k=0.2)
        for a in (-35, 0, 35):
            R = S.rot(a, 0, 0)
            tip = np.array((x, y0 + 0.16, 0.3)) + R @ np.array((0, 0, 0.7))
            fig.add(S.sphere(tuple(tip), 0.17), B, k=0.12)

    # ---------- 몸통 ----------
    bc, br = (0, y0 + 2.2, 0.0), (1.32, 1.35, 1.12)
    fig.add(S.ellipsoid(bc, br), B, k=0.4)
    fig.add(S.ellipsoid((0, y0 + 1.5, 0.0), (1.25, 0.75, 1.05)), B, k=0.4)

    # ---------- 팔 ----------
    # 왼팔 (화면 왼쪽, -x): 붓을 들고 위로
    shL, elL, hdL = (-1.15, y0 + 3.0, 0.15), (-1.7, y0 + 2.35, 0.45), (-2.05, y0 + 2.6, 0.85)
    fig.add(S.capsule(shL, elL, 0.36, 0.32), B, k=0.25)
    fig.add(S.capsule(elL, hdL, 0.32, 0.3), B, k=0.2)
    fig.add(S.sphere(hdL, 0.34), B, k=0.15)
    # 오른팔 (+x): 팔레트를 받쳐 듦
    shR, elR, hdR = (1.15, y0 + 3.0, 0.15), (1.7, y0 + 2.4, 0.3), (2.05, y0 + 2.55, 0.55)
    fig.add(S.capsule(shR, elR, 0.36, 0.32), B, k=0.25)
    fig.add(S.capsule(elR, hdR, 0.32, 0.3), B, k=0.2)
    fig.add(S.sphere(hdR, 0.34), B, k=0.15)

    # ---------- 꼬리 (뒤로 나와 둥글게 말림, 세로면 나선) ----------
    cy, cz, cx = y0 + 1.25, -2.05, -0.35
    pts = [(-0.2, y0 + 1.7, -0.7), (-0.3, y0 + 2.05, -1.45)]
    n = 40
    for i in range(n):
        th = 4.2 * math.pi * i / n
        r = 0.95 * (1 - th / (5.0 * math.pi))
        phi = math.pi / 2 + th
        pts.append((cx, cy + r * math.sin(phi), cz + r * math.cos(phi)))
    for i in range(len(pts) - 1):
        r0 = 0.42 * (1 - 0.72 * i / len(pts))
        r1 = 0.42 * (1 - 0.72 * (i + 1) / len(pts))
        fig.add(S.capsule(pts[i], pts[i + 1], r0, r1), B, k=0.15)

    # ---------- 머리 (넓적한 카멜레온 머리 + 튀어나온 눈) ----------
    hc = (0, y0 + 4.75, 0.15)
    hr = (1.95, 1.55, 1.7)
    fig.add(S.ellipsoid(hc, hr), B, k=0.5)
    fig.add(S.ellipsoid((0, y0 + 4.25, 0.55), (1.75, 0.95, 1.45)), B, k=0.5)       # 넓은 턱
    fig.add(S.ellipsoid((0, y0 + 3.55, 0.1), (1.15, 0.6, 0.95)), B, k=0.5)         # 목
    eyes = []
    for s in (-1, 1):
        tc = (s * 1.25, y0 + 5.25, 1.0)
        fig.add(S.sphere(tc, 0.98), B, k=0.35)
        eyes.append((s, tc))
    # 등 볏 (작은 혹 줄)
    for i in range(11):
        t = i / 10
        y = y0 + 6.1 - 4.2 * t
        p0 = (0, y, -0.2)
        q = hit(fig, (0, y, -4.5), (0, 0, 1))
        fig.add(S.sphere(tuple(q + (0, 0, 0.02)), 0.2 - 0.07 * t), B, k=0.1)

    # ---------- 무지개 칠 (몸 layer 에만) ----------
    lp = lambda f, c, soft: layer_paint(fig, 'body', f, c, soft, tol=0.05)
    lp(S.sphere((-1.2, y0 + 6.3, -0.2), 1.6), PINKP, 1.1)
    lp(S.sphere((0.4, y0 + 6.6, 0.4), 1.4), YELLOWP, 1.0)
    lp(S.sphere((1.9, y0 + 5.0, -0.5), 1.2), BLUEP, 0.9)
    lp(S.sphere((-2.0, y0 + 4.3, -0.4), 1.0), LILAC, 0.9)
    lp(S.sphere((1.6, y0 + 2.7, 0.3), 1.0), BLUEP, 0.9)
    lp(S.sphere((-1.6, y0 + 2.6, 0.7), 0.9), PINKP, 0.7)
    lp(S.sphere((-0.9, y0 + 1.6, 1.0), 0.6), YELLOWP, 0.5)
    lp(S.sphere((0.0, y0 + 2.5, -1.4), 1.2), LILAC, 1.0)
    lp(S.sphere((0.9, y0 + 0.8, 0.3), 0.9), LILAC, 0.8)
    lp(S.sphere((-0.9, y0 + 0.8, 0.3), 0.9), BLUEP, 0.8)
    lp(S.sphere((-0.4, y0 + 1.9, -2.2), 0.7), PINKP, 0.5)
    lp(S.sphere((-0.4, y0 + 0.5, -1.9), 0.6), YELLOWP, 0.5)
    lp(S.sphere((-0.4, y0 + 1.2, -3.1), 0.5), BLUEP, 0.45)
    lp(S.sphere((-0.4, y0 + 1.25, -2.05), 0.35), LILAC, 0.35)
    for s in (-1, 1):
        lp(S.sphere((s * 1.75, y0 + 5.5, 1.3), 0.7), PINKP if s < 0 else YELLOWP, 0.6)
    # 배·턱 (크림)
    lp(S.ellipsoid((0, y0 + 2.0, 1.1), (0.72, 1.0, 0.35)), BELLY, 0.12)
    lp(S.ellipsoid((0, y0 + 3.85, 1.85), (1.25, 0.45, 0.5)), BELLY, 0.15)
    for s in (-1, 1):   # 볼터치
        lp(S.sphere((s * 1.55, y0 + 4.35, 1.3), 0.38), Fg.BLUSH, 0.45)

    # ---------- 눈: 금테 + 큰 갈색 눈 ----------
    for s, tc in eyes:
        yaw, pitch = s * 34, 4
        d = np.array((math.cos(math.radians(pitch)) * math.sin(math.radians(yaw)), math.sin(math.radians(pitch)),
                      math.cos(math.radians(pitch)) * math.cos(math.radians(yaw))))
        ring_c = np.array(tc) + d * 0.7
        fig.add(S.torus(tuple(ring_c), 0.66, 0.1, Rm=S.rot(yaw, 90 - pitch, 0)), Fg.GOLD, k=0.0, layer='trim', metal=G)
        Fg._ellipsoid(fig.extra, tuple(np.array(tc) + d * 0.25), (0.78, 0.78, 0.78), (0.99, 0.99, 0.99), 40, 20)
        eye1(fig, tuple(np.array(tc) + d * 0.25), 0.78, yaw, pitch, size=0.56, tall=1.0, side=s)
    # 입 (넓은 미소 선) + 콧구멍
    mpts = []
    for i in range(15):
        x = -0.85 + 1.7 * i / 14
        y = y0 + 4.3 + 0.16 * (x / 0.85) ** 2
        mpts.append(hit(fig, (x, y, 4.5), (0, 0, -1)) + (0, 0, 0.0))
    beads(fig, mpts, 0.045, (0.42, 0.26, 0.24))
    for s in (-1, 1):
        q = hit(fig, (s * 0.28, y0 + 4.95, 4.5), (0, 0, -1))
        Fg._ellipsoid(fig.extra, tuple(q), (0.05, 0.035, 0.03), (0.35, 0.3, 0.3), 6, 4)

    # ---------- 베레모 ----------
    top = hit(fig, (0, y0 + 9, 0.1), (0, -1, 0))
    bcn = top + (0.0, 0.05, -0.05)
    Rb = S.rot(0, -10, -13)
    fig.add(S.ellipsoid(tuple(bcn + (0.2, 0.25, 0)), (2.0, 0.55, 1.8), R=Rb), BERET, k=0.25, layer='beret')
    fig.add(S.torus(tuple(bcn + (0.05, -0.08, 0)), 1.25, 0.18, Rm=Rb), BERET, k=0.2, layer='beret')
    fig.add(S.capsule(tuple(bcn + (0.15, 0.6, 0)), tuple(bcn + (0.2, 0.9, 0)), 0.15, 0.17), BERET, k=0.08, layer='beret')

    # ---------- 붓 (왼손) ----------
    a = np.array((-1.95, y0 + 1.85, 0.95))
    b = np.array((-2.55, y0 + 3.85, 1.05))
    fig.add(S.capsule(tuple(a), tuple(b), 0.09, 0.075), HANDLE, k=0.0, layer='brush')
    u = (b - a) / np.linalg.norm(b - a)
    fg_ = b + u * 0.18
    fig.add(S.capsule(tuple(b - u * 0.05), tuple(fg_), 0.11), Fg.GOLD, k=0.0, layer='ferrule', metal=G)
    tip = fg_ + u * 0.75
    fig.add(S.capsule(tuple(fg_ + u * 0.18), tuple(tip), 0.2, 0.03), (0.99, 0.94, 0.85), k=0.12, layer='bristle')
    fig.add(S.capsule(tuple(fg_ + u * 0.05), tuple(fg_ + u * 0.25), 0.13, 0.2), (0.99, 0.94, 0.85), k=0.12, layer='bristle')
    layer_paint(fig, 'bristle', S.sphere(tuple(tip), 0.42), (1.0, 0.55, 0.20), soft=0.25)
    # 손가락이 붓을 감싼다
    for k in range(3):
        p = np.array(hdL) + np.array((0.0, 0.12 - k * 0.15, 0.25))
        fig.add(S.sphere(tuple(p), 0.13), B, k=0.08)

    # ---------- 팔레트 (오른손) ----------
    pc = np.array((2.2, y0 + 2.75, 0.8))
    Rp = S.rot(28, 78, 0)
    pal = S.ellipsoid(tuple(pc), (0.95, 0.1, 0.72), R=Rp)
    pal = S.subtract(pal, S.cylinder(tuple(pc + Rp @ np.array((-0.45, 0, 0.25))), 0.15, 0.4, R=Rp))
    fig.add(pal, WOOD, k=0.0, layer='palette')
    for (u_, v_), col in zip(((0.1, -0.4), (0.5, -0.15), (0.55, 0.25), (0.15, 0.45), (-0.25, 0.55), (-0.45, -0.25)),
                             ((0.92, 0.15, 0.15), (1.0, 0.85, 0.1), (0.2, 0.45, 0.95), (0.2, 0.72, 0.3), (0.62, 0.3, 0.85), (1.0, 0.55, 0.15))):
        p = pc + Rp @ np.array((u_, 0.06, v_ * 0.9))
        fig.add(S.ellipsoid(tuple(p), (0.14, 0.07, 0.14), R=Rp), col, k=0.0, layer='paint')
    for k in range(3):   # 엄지·손가락 (팔레트 아래 가장자리 잡기)
        p = np.array(hdR) + np.array((0.0 + k * 0.1, 0.2, 0.25 - k * 0.05))
        fig.add(S.sphere(tuple(p), 0.13), B, k=0.08)

    # ---------- 가방 끈 + 가방 (물감 튜브) ----------
    bag_c = np.array((-1.2, y0 + 1.5, 1.0))
    Rg = S.rot(-30, 0, 0)
    strap = []
    for i in range(13):   # 오른쪽 어깨 → 가슴 대각선 → 왼쪽 허리 가방
        t = i / 12
        x = 0.95 - 2.0 * t
        y = y0 + 3.45 - 1.4 * t
        q = hit(fig, (x, y, 4.5), (0, 0, -1))
        strap.append(q + (0, 0, 0.04))
    back = []
    for i in range(9):    # 등쪽
        t = i / 8
        q = hit(fig, (0.95 - 2.0 * t, y0 + 3.45 - 1.3 * t, -4.5), (0, 0, 1))
        back.append(q + (0, 0, -0.04))
    for a_, b_ in zip(back, back[1:]):
        fig.add(S.capsule(tuple(a_), tuple(b_), 0.07), LEATHER, k=0.02, layer='strap')
    for a_, b_ in zip(strap, strap[1:]):
        fig.add(S.capsule(tuple(a_), tuple(b_), 0.07), LEATHER, k=0.02, layer='strap')
    fig.add(S.capsule(tuple(strap[0]), (1.05, y0 + 3.55, 0.0), 0.07), LEATHER, k=0.02, layer='strap')
    fig.add(S.capsule((1.05, y0 + 3.55, 0.0), tuple(back[0]), 0.07), LEATHER, k=0.02, layer='strap')
    fig.add(S.box(tuple(bag_c), (0.6, 0.5, 0.27), round_=0.15, R=Rg), LEATHER, k=0.0, layer='bag')
    fig.add(S.box(tuple(bag_c + Rg @ np.array((0, 0.22, 0.24))), (0.62, 0.28, 0.06), round_=0.05, R=Rg), LEATHER_DK, k=0.0, layer='flap')
    fig.add(S.box(tuple(bag_c + Rg @ np.array((0, -0.02, 0.31))), (0.1, 0.09, 0.03), round_=0.02, R=Rg), Fg.GOLD, k=0.0, layer='buckle', metal=G)
    for dx, cap in ((-0.2, (0.85, 0.15, 0.15)), (0.05, (0.2, 0.45, 0.95)), (0.27, (0.95, 0.75, 0.1))):
        p0 = bag_c + Rg @ np.array((dx, 0.38, -0.05))
        p1 = p0 + np.array((dx * 0.25, 0.42, 0.02))
        fig.add(S.capsule(tuple(p0), tuple(p1), 0.1), (0.97, 0.97, 0.97), k=0.03, layer='tube')
        fig.add(S.capsule(tuple(p1), tuple(p1 + (0, 0.12, 0)), 0.08), cap, k=0.0, layer='cap')

    # ---------- 받침: 물감 자국 ----------
    for (x, z, r), col in zip(((2.7, 2.3, 0.5), (-2.9, 1.9, 0.42), (2.9, -1.6, 0.45), (-2.6, -2.2, 0.4), (0.3, 3.4, 0.38)),
                              ((0.92, 0.15, 0.15), (1.0, 0.85, 0.1), (0.2, 0.45, 0.95), (0.2, 0.72, 0.3), (0.62, 0.3, 0.85))):
        fig.add(S.ellipsoid((x, y0 + 0.2, z), (r, 0.1, r * 0.85)), col, k=0.15, layer='splat%d' % int(x * 10))
        for j in range(5):
            a = j * 1.3 + x
            fig.add(S.ellipsoid((x + math.cos(a) * r * 0.95, y0 + 0.19, z + math.sin(a) * r * 0.9), (r * 0.35, 0.08, r * 0.35)), col, k=0.15,
                    layer='splat%d' % int(x * 10))
            fig.add(S.sphere((x + math.cos(a + 0.5) * r * 1.6, y0 + 0.2, z + math.sin(a + 0.5) * r * 1.5), 0.09), col, k=0.0,
                    layer='splat%d' % int(x * 10))
