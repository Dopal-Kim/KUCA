"""노천극장 개구리: 연두 몸, 크림 턱·배, 머리 위 둥근 눈, 분홍 볼, 진홍 나비넥타이, 빈티지 스탠드 마이크(은·금), 연잎 무대."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .clock_rooster import Fast, hit, layer_paint, eye1

TIER = 'gold'

GREEN = (0.47, 0.76, 0.26)
CREAMY = (0.99, 0.95, 0.68)
BOW = (0.76, 0.12, 0.16)
BOW_DK = (0.62, 0.08, 0.12)
PAD = (0.24, 0.52, 0.22)
PAD_HI = (0.36, 0.64, 0.30)
SILV = (0.78, 0.80, 0.83)
SV = (SILV, (1.0, 1.0, 1.0))
G = (Fg.GOLD, Fg.GOLD_HI)


def build(fig, rng):
    fig = Fast(fig)
    Fg.base(fig, rng, flowers=8)
    y0 = Fg.TOP
    B = GREEN

    # ---------- 연잎 (받침 무대) ----------
    for (x, z, r, a) in ((-0.3, 2.55, 0.95, 20), (-2.6, 0.9, 0.85, 150), (2.4, -2.2, 0.7, 260)):
        pad = S.cylinder((x, y0 + 0.36, z), r, 0.05, round_=0.03)
        notch = lambda P, x=x, z=z, r=r, a=a: _wedge(P, (x, z), a, 22)
        fig.add(S.subtract(pad, notch), PAD, k=0.0, layer='pad%d' % int(x * 10))
        for k in range(7):   # 잎맥
            b = math.radians(a + 25 + k * 45)
            fig.add(S.capsule((x, y0 + 0.41, z), (x + math.cos(b) * r * 0.85, y0 + 0.41, z + math.sin(b) * r * 0.85), 0.025),
                    PAD_HI, k=0.0, layer='vein%d' % int(x * 10))

    # ---------- 발 (긴 발가락) ----------
    for s in (-1, 1):
        x = s * 0.75
        fig.add(S.ellipsoid((x, y0 + 0.28, 0.35), (0.48, 0.26, 0.5)), B, k=0.18)
        for a in (-38, -5, 28):
            R = S.rot(a + s * 8, 0, 0)
            tip = np.array((x, y0 + 0.17, 0.35)) + R @ np.array((0, 0, 0.85))
            fig.add(S.capsule((x, y0 + 0.2, 0.35), tuple(tip), 0.14, 0.12), B, k=0.1)
            fig.add(S.sphere(tuple(tip), 0.18), B, k=0.08)
        fig.add(S.capsule((x, y0 + 0.35, 0.1), (x * 1.05, y0 + 1.35, 0.0), 0.42, 0.5), B, k=0.25)

    # ---------- 몸통 ----------
    fig.add(S.ellipsoid((0, y0 + 2.25, 0.0), (1.38, 1.4, 1.15)), B, k=0.45)
    fig.add(S.ellipsoid((0, y0 + 1.55, 0.05), (1.32, 0.75, 1.08)), B, k=0.45)

    # ---------- 팔 ----------
    shL, elL, hdL = (-1.15, y0 + 3.1, 0.15), (-1.6, y0 + 2.45, 0.25), (-1.85, y0 + 1.95, 0.45)
    fig.add(S.capsule(shL, elL, 0.38, 0.33), B, k=0.25)
    fig.add(S.capsule(elL, hdL, 0.33, 0.3), B, k=0.2)
    fig.add(S.sphere(hdL, 0.33), B, k=0.15)
    for a in (-30, 5, 40):
        R = S.rot(-90 + a * 0.3, 0, 0)
        tip = np.array(hdL) + np.array((-0.15 - 0.1 * math.cos(math.radians(a)), -0.3, 0.05 + math.sin(math.radians(a)) * 0.3))
        fig.add(S.capsule(hdL, tuple(tip), 0.13, 0.11), B, k=0.08)
        fig.add(S.sphere(tuple(tip), 0.14), B, k=0.06)
    # 오른팔: 마이크 스탠드를 잡음
    mx, mz = 1.8, 1.45
    shR, elR, hdR = (1.15, y0 + 3.1, 0.15), (1.65, y0 + 2.45, 0.55), (mx - 0.12, y0 + 2.75, mz - 0.08)
    fig.add(S.capsule(shR, elR, 0.38, 0.33), B, k=0.25)
    fig.add(S.capsule(elR, hdR, 0.33, 0.3), B, k=0.2)
    fig.add(S.sphere(hdR, 0.32), B, k=0.15)
    for k in range(3):   # 기둥을 감싼 손가락
        y = y0 + 2.88 - k * 0.2
        fig.add(S.capsule((mx - 0.2, y, mz - 0.05), (mx + 0.05, y - 0.03, mz + 0.18), 0.11), B, k=0.06)
        fig.add(S.sphere((mx + 0.08, y - 0.03, mz + 0.16), 0.12), B, k=0.05)

    # ---------- 머리 (넓적, 위에 눈 언덕 둘) ----------
    hc = (0, y0 + 5.15, 0.15)
    hr = (2.2, 1.68, 1.85)
    fig.add(S.ellipsoid(hc, hr), B, k=0.5)
    fig.add(S.ellipsoid((0, y0 + 3.75, 0.15), (1.1, 0.5, 0.95)), B, k=0.5)        # 목
    domes = []
    for s in (-1, 1):
        dc = (s * 1.15, y0 + 6.5, 0.55)
        fig.add(S.sphere(dc, 0.88), B, k=0.4)
        domes.append((s, dc))

    # 크림 턱·배, 볼터치
    lp = lambda f, c, soft, tol=0.06: layer_paint(fig, 'body', f, c, soft, tol=tol)
    lp(S.ellipsoid((0, y0 + 4.35, 1.3), (1.95, 1.05, 1.2)), CREAMY, 0.1)
    lp(S.ellipsoid((0, y0 + 2.15, 1.0), (0.98, 1.2, 0.5)), CREAMY, 0.1)
    for s in (-1, 1):
        q = hit(fig, (s * 1.6, y0 + 4.85, 4.5), (0, 0, -1))
        lp(S.sphere(tuple(q), 0.42), (1.0, 0.58, 0.60), 0.12, tol=0.08)

    # 눈 (흰 눈알 + 큰 갈색 눈)
    for s, dc in domes:
        yaw, pitch = s * 14, 6
        d = np.array((math.sin(math.radians(yaw)), math.sin(math.radians(pitch)), math.cos(math.radians(yaw))))
        d /= np.linalg.norm(d)
        ec = np.array(dc) + d * 0.45
        Fg._ellipsoid(fig.extra, tuple(ec), (0.7, 0.7, 0.7), (0.995, 0.995, 0.99), 40, 20)   # 흰자: 또렷한 메시 (경계가 매끈)
        eye1(fig, tuple(ec), 0.7, yaw, pitch, size=0.45, tall=1.04, side=s)
    # 콧구멍
    for s in (-1, 1):
        q = hit(fig, (s * 0.26, y0 + 5.35, 4.5), (0, 0, -1))
        Fg._ellipsoid(fig.extra, tuple(q), (0.06, 0.05, 0.03), (0.16, 0.24, 0.10), 8, 4)

    # ---------- 나비넥타이 ----------
    bc = hit(fig, (0, y0 + 3.62, 4.5), (0, 0, -1)) + (0, 0, 0.12)
    for s in (-1, 1):
        lobe = S.capsule(tuple(bc + (s * 0.1, 0, 0)), tuple(bc + (s * 0.62, 0.0, -0.06)), 0.1, 0.34)
        lobe = S.intersect(lobe, lambda P, z=bc[2] - 0.03: np.abs(P[:, 2] - z) - 0.15)
        fig.add(lobe, BOW, k=0.08, layer='bow')
        fig.add(S.capsule(tuple(bc + (s * 0.15, 0.08, 0.1)), tuple(bc + (s * 0.6, 0.18, 0.08)), 0.04), BOW_DK, k=0.06, layer='bow')
        fig.add(S.capsule(tuple(bc + (s * 0.15, -0.08, 0.1)), tuple(bc + (s * 0.6, -0.18, 0.08)), 0.04), BOW_DK, k=0.06, layer='bow')
    fig.add(S.ellipsoid(tuple(bc + (0, 0, 0.06)), (0.17, 0.2, 0.15)), BOW, k=0.08, layer='bowknot')

    # ---------- 스탠드 마이크 ----------
    fig.add(S.cylinder((mx, y0 + 0.32, mz), 0.55, 0.07, round_=0.05), SILV, k=0.0, layer='mic', metal=SV)
    fig.add(S.torus((mx, y0 + 0.37, mz), 0.5, 0.05), Fg.GOLD, k=0.0, layer='micg', metal=G)
    fig.add(S.capsule((mx, y0 + 0.36, mz), (mx, y0 + 0.62, mz), 0.15, 0.1), SILV, k=0.05, layer='mic', metal=SV)
    fig.add(S.capsule((mx, y0 + 0.5, mz), (mx, y0 + 3.55, mz), 0.085), SILV, k=0.0, layer='pole', metal=SV)
    fig.add(S.cylinder((mx, y0 + 1.7, mz), 0.11, 0.08, round_=0.03), Fg.GOLD, k=0.0, layer='micg', metal=G)
    fig.add(S.cylinder((mx, y0 + 3.45, mz), 0.1, 0.07, round_=0.03), Fg.GOLD, k=0.0, layer='micg', metal=G)
    # 요크 (U 자 받침)
    hc2 = np.array((mx, y0 + 4.05, mz))
    fig.add(S.intersect(S.torus(tuple(hc2), 0.47, 0.055, Rm=S.rot(-25, 0, 90)), lambda P: P[:, 1] - (hc2[1] - 0.05)), SILV, k=0.0, layer='yoke', metal=SV)
    for s in (-1, 1):
        R = S.rot(-25, 0, 0)
        p = hc2 + R @ np.array((0, 0, s * 0.47))
        fig.add(S.sphere(tuple(p), 0.08), Fg.GOLD, k=0.0, layer='micg', metal=G)
    fig.add(S.capsule(tuple(hc2 + (0, -0.52, 0)), (mx, y0 + 3.55, mz), 0.07), SILV, k=0.0, layer='pole', metal=SV)
    # 머리 (은색 그릴 캡슐 + 홈 + 금 띠)
    fig.add(S.capsule(tuple(hc2 + (0, -0.02, 0)), tuple(hc2 + (0, 0.5, 0)), 0.38), SILV, k=0.0, layer='michead', metal=SV)
    for y in (-0.22, -0.1, 0.36, 0.48, 0.6):
        r = math.sqrt(max(0.38 ** 2 - (y - 0.48) ** 2, 0.02)) if y > 0.48 else 0.38
        fig.add(S.torus((mx, hc2[1] + y, mz), r, 0.032), (0.36, 0.37, 0.41), k=0.0, layer='grille')
    fig.add(S.torus((mx, hc2[1] + 0.13, mz), 0.385, 0.05), Fg.GOLD, k=0.0, layer='micg', metal=G)


def _wedge(P, c, a_deg, half_deg):
    """중심 c 에서 방향 a 로 벌어진 쐐기 (연잎 홈) 의 대략 SDF"""
    x, z = P[:, 0] - c[0], P[:, 2] - c[1]
    a = math.radians(a_deg)
    u = x * math.cos(a) + z * math.sin(a)
    v = -x * math.sin(a) + z * math.cos(a)
    h = math.radians(half_deg)
    d1 = np.abs(v) * math.cos(h) - u * math.sin(h)
    return np.maximum(d1, -u + 0.05)
