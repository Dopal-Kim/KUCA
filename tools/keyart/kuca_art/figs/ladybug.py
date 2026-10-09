"""무당벌레 (green): 짙은 갈색 투구 머리 + 살구빛 얼굴, 공 끝 더듬이, 빨간 몸 검은 점, 초록 책가방 + 진홍 꽃 단추"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .pigeon import (axis_R, basis, surf_point, surf_frame, masked, ell, kawaii_eyes2, smile2, blush2, tuft,
                     soft_head, face_frame, eye_pair, blush_paint, dir_of)

TIER = 'green'

DARK = (0.28, 0.21, 0.19)
DARK_HI = (0.40, 0.31, 0.28)
SKIN = (0.99, 0.77, 0.61)
RED_B = (0.90, 0.22, 0.17)
SPOT = (0.14, 0.09, 0.09)
PACK = (0.50, 0.76, 0.28)
PACK_D = (0.40, 0.64, 0.20)
PETAL = (1.0, 0.80, 0.84)


def build(fig, rng):
    Fg.base(fig, rng, flowers=6)
    y0 = Fg.TOP
    # ---- 몸: 크고 둥근 빨간 등딱지 (가운데 솔기) — 몸이 머리보다 크다 ----
    bc = np.array((0.0, y0 + 2.7, 0.0))
    shell = S.ellipsoid(bc, (2.05, 1.95, 1.8))

    def body(P):
        seam = 0.05 * np.exp(-(P[:, 0] / 0.05) ** 2)
        return shell(P) + seam
    fig.add(body, RED_B, k=0.2, layer='body')
    near_b = lambda P: np.abs(shell(P))
    fig.paint(masked(lambda P: np.abs(P[:, 0]) - 0.04, near_b, 0.12), (0.55, 0.10, 0.08), soft=0.05)
    spots = [(-28, 24, 0.42), (28, 24, 0.42), (-25, -26, 0.4), (25, -26, 0.4), (-70, 0, 0.4), (70, 0, 0.4),
             (180 - 32, 22, 0.44), (180 + 32, 22, 0.44), (180 - 34, -26, 0.42), (180 + 34, -26, 0.42),
             (-112, -30, 0.34), (112, -30, 0.34)]
    for yaw, pitch, r in spots:
        p, n = surf_point(shell, bc, yaw, pitch)
        fig.add(S.intersect(lambda P: shell(P) - 0.08, S.sphere(p, r)), SPOT, k=0.0, layer='spot')   # 또렷한 검은 점 (얇은 판)
    # ---- 팔다리 (짙은 갈색, 짧고 통통) ----
    LIMB = DARK
    for s in (-1, 1):
        sh = np.array((s * 1.5, y0 + 3.45, 0.0))
        hd = np.array((s * 2.3, y0 + 2.45, 0.35))
        fig.add(S.capsule(tuple(sh), tuple(hd), 0.42, 0.38), LIMB, k=0.15, layer='limb')
        fig.add(S.sphere(tuple(hd + (s * 0.03, -0.1, 0.05)), 0.4), LIMB, k=0.12, layer='limb')
        fig.add(S.capsule((s * 0.7, y0 + 1.15, 0.05), (s * 0.72, y0 + 0.35, 0.12), 0.46, 0.44), LIMB, k=0.15, layer='limb')
        fig.add(S.ellipsoid((s * 0.72, y0 + 0.3, 0.22), (0.5, 0.3, 0.6)), LIMB, k=0.15, layer='limb')
    # ---- 머리: 짙은 투구 + 작은 살구빛 얼굴 (아래가 살짝 통통한 한 덩어리, 볼 구 없음) ----
    hc = np.array((0.0, y0 + 6.05, -0.05))
    helm0 = S.ellipsoid(hc, (1.98, 1.8, 1.82))
    hole = S.ellipsoid((0, y0 + 5.38, 1.65), (1.5, 1.3, 1.4))
    helm = S.subtract(helm0, hole, k=0.2)
    fig.add(helm, DARK, k=0.0, layer='helm')
    near_h = lambda P: np.abs(helm(P))
    fig.paint(masked(lambda P: np.abs(P[:, 0]) - 0.025, near_h, 0.06), (0.20, 0.14, 0.13), soft=0.04)
    fc = np.array((0.0, y0 + 5.55, 0.15))
    face0 = S.intersect(soft_head(fc, (1.6, 1.42, 1.62), flare=0.1, flare_y=-0.45), lambda P: (0.2 - P[:, 2]) * 0.8)
    fig.add(face0, SKIN, k=0.0, layer='face')
    # 활짝 웃는 'D' 입: 또렷한 부품 (짙은 입 안 + 분홍 혀), 표면에 붙임
    fmo = face_frame(fig, fc, 0, -23, 'face', out=-0.03)
    ell(fig.extra, fmo, (0, -0.06, 0.0), (0.27, 0.2, 0.05), (0.42, 0.09, 0.12), 18, 8)          # 입 안
    ell(fig.extra, fmo, (0, 0.1, 0.01), (0.3, 0.1, 0.06), SKIN, 18, 8)                          # 윗입술 (위를 평평하게 → 'D')
    ell(fig.extra, fmo, (0, -0.15, 0.025), (0.15, 0.08, 0.04), (1.0, 0.50, 0.58), 14, 6)        # 혀
    eye_pair(fig, fc, 25, 2, 'round', 0.42, iris=(0.40, 0.21, 0.09), layer='face', sink=0.14)
    blush_paint(fig, fc, 42, -18, 'face', size=0.36, soft=0.4)
    fig.paint(S.sphere(face_frame(fig, fc, 0, -11, 'face').o, 0.06), (0.88, 0.62, 0.55), soft=0.05)
    # ---- 더듬이 (가는 줄기 + 큰 공) ----
    for s in (-1, 1):
        a, _ = surf_point(helm, hc, s * 22, 60)
        m = np.array((s * 0.95, y0 + 8.45, 0.0))
        t = np.array((s * 1.45, y0 + 8.95, 0.15))
        fig.add(S.capsule(tuple(a - (0, 0.2, 0)), tuple(m), 0.13, 0.11), DARK, k=0.12, layer='helm2')
        fig.add(S.capsule(tuple(m), tuple(t), 0.11, 0.1), DARK, k=0.12, layer='helm2')
        fig.add(S.ellipsoid(tuple(t + (s * 0.12, 0.08, 0.0)), (0.44, 0.36, 0.38)), DARK, k=0.1, layer='helm2')
    # ---- 책가방 (등) ----
    pc = np.array((0.0, y0 + 3.0, -2.0))
    fig.add(S.box(tuple(pc), (0.95, 1.05, 0.5), round_=0.35), PACK, k=0.0, layer='pack')
    fig.add(S.ellipsoid(tuple(pc + (0, 1.0, 0.05)), (0.95, 0.35, 0.52)), PACK, k=0.15, layer='pack')       # 둥근 윗면
    fig.add(S.box(tuple(pc + (0, -0.35, -0.48)), (0.65, 0.45, 0.2), round_=0.15), PACK, k=0.0, layer='pack2')   # 앞주머니
    fig.add(S.box(tuple(pc + (0, 0.28, -0.52)), (0.7, 0.32, 0.12), round_=0.1, R=S.rot(0, 12, 0)), PACK_D, k=0.0, layer='pack3')  # 덮개
    fig.add(S.box(tuple(pc + (0, 0.1, -0.68)), (0.13, 0.12, 0.05), round_=0.03), Fg.GOLD, k=0.0, layer='buckle', metal=(Fg.GOLD, Fg.GOLD_HI))
    fig.add(S.torus(tuple(pc + (0, 1.32, 0.0)), 0.32, 0.08, Rm=S.rot(0, 0, 90) @ S.rot(90, 0, 0)), PACK_D, k=0.0, layer='pack3')  # 손잡이
    for s in (-1, 1):
        fig.add(S.box(tuple(pc + (s * 0.98, -0.3, 0.0)), (0.18, 0.45, 0.35), round_=0.14), PACK_D, k=0.0, layer='pack2')   # 옆주머니
    # 멜빵: 가방 위 → 어깨 넘어 → 가슴 앞 → 겨드랑이 아래 → 가방 아래 (등딱지 표면을 따라)
    for s_ in (-1, 1):   # 등딱지를 감싸는 납작한 띠 (평면으로 자른 껍질 → 또렷한 가장자리)
        nrm = np.array((1.0, 0.0, 0.12 * s_)); nrm /= np.linalg.norm(nrm)
        c0 = np.array((s_ * 0.98, 0, 0))
        def band(P, nrm=nrm, c0=c0):
            shell_d = np.abs(shell(P) - 0.06) - 0.07
            plane = np.abs((P - c0) @ nrm) - 0.13
            low = (bc[1] - 1.25) - P[:, 1]
            return np.maximum(np.maximum(shell_d, plane), low)
        fig.add(band, PACK, k=0.0, layer='strap')
    # 진홍 꽃 단추 (-x 멜빵 가슴)
    bx_, by_ = -0.98, 0.85
    bz_ = 1.8 * math.sqrt(1 - (bx_ / 2.05) ** 2 - (by_ / 1.95) ** 2)
    bn = np.array((bx_ / 2.05 ** 2, by_ / 1.95 ** 2, bz_ / 1.8 ** 2)); bn /= np.linalg.norm(bn)
    bp = bc + np.array((bx_, by_, bz_)) + bn * 0.18
    Rb = axis_R(bn)
    fig.add(S.cylinder(tuple(bp), 0.3, 0.07, round_=0.05, R=Rb), Fg.CRIMSON, k=0.0, layer='button')
    for k in range(5):
        a = k * 2 * math.pi / 5
        q = bp + bn * 0.07 + (Rb[:, 0] * math.cos(a) + Rb[:, 2] * math.sin(a)) * 0.11
        fig.add(S.ellipsoid(tuple(q), (0.075, 0.03, 0.075), R=Rb), (1.0, 0.96, 0.96), k=0.0, layer='flower')
    fig.add(S.sphere(tuple(bp + bn * 0.08), 0.045), (1.0, 0.78, 0.2), k=0.0, layer='flower2')
    # ---- 받침: 분홍 꽃잎, 클로버 ----
    for i, (x, z, a) in enumerate(((2.6, 2.0, 30), (-2.3, 2.6, -40), (3.2, -0.8, 80), (-3.0, -1.2, 10), (0.9, 3.4, -70))):
        R = S.rot(a, 0, 8 * (-1) ** i)
        fig.add(S.ellipsoid((x, y0 + 0.12, z), (0.32, 0.06, 0.22), R=R), PETAL, k=0.0, layer='petal')
        fig.paint(S.sphere((x - 0.2 * math.cos(math.radians(a)), y0 + 0.12, z + 0.2 * math.sin(math.radians(a))), 0.18), (1.0, 0.92, 0.94), soft=0.2)
    for (x, z) in ((-1.8, 3.0), (2.2, 3.0), (-3.4, 0.6), (3.5, 0.9), (-1.0, -3.4), (1.6, -3.2)):
        for k in range(3):
            a = k * 2 * math.pi / 3 + x
            fig.add(S.ellipsoid((x + math.cos(a) * 0.17, y0 + 0.22, z + math.sin(a) * 0.17), (0.17, 0.04, 0.17)), (0.36, 0.68, 0.2), k=0.03, layer='clover')
