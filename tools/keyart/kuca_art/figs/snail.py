"""달팽이 (green): 크림색 말랑 몸, 눈자루 위 졸린 눈 + 큰 얼굴 눈, 캐러멜 나선 껍데기(진홍 점), 초록 잎 우산"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .pigeon import (axis_R, basis, surf_point, surf_frame, masked, pebble, sleepy_eye, kawaii_eyes2, smile2, blush2, tuft,
                     soft_head, face_frame, eye_pair, mouth_w, blush_paint)

TIER = 'green'

CREAM = (1.0, 0.885, 0.67)
CREAM_D = (0.92, 0.82, 0.64)
SHELL = (0.83, 0.57, 0.29)
SHELL_HI = (0.97, 0.78, 0.47)
SHELL_D = (0.58, 0.35, 0.16)
LEAF = (0.42, 0.76, 0.22)
LEAF_HI = (0.58, 0.86, 0.34)
LEAF_UNDER = (0.56, 0.84, 0.32)
STEM = (0.40, 0.70, 0.20)
BROW = (0.70, 0.47, 0.28)


def leaf_fn(O, R, u0, u1, Wm, k_tip, k_base, k_cup, th=0.08, rib=0.05, tip_pow=0.75, k_cup_pos=None):
    """잎 (얇은 휜 판): 로컬 z = 잎맥 방향(u), x = 폭(v), y = 위. 높이 h(u, v) = -k u^2 - k_cup v^2.
    반환: (sdf, 로컬 변환 함수)"""
    O = np.asarray(O, np.float64)
    L = u1 - u0

    def local(P):
        q = (P - O) @ R
        return q[:, 0], q[:, 1], q[:, 2]

    kcp = k_cup if k_cup_pos is None else k_cup_pos

    def height(u, v):
        k = np.where(u > 0, k_tip, k_base)
        kc = np.where(v > 0, kcp, k_cup)
        return -k * u * u - kc * v * v, -2 * k * u, -2 * kc * v

    def width(u):
        t = np.clip((u - u0) / L, 0, 1)
        return Wm * np.sin(math.pi * t ** tip_pow) ** 0.7

    def f(P):
        v, y, u = local(P)
        h, dhu, dhv = height(u, v)
        d = (y - h) / np.sqrt(1 + dhu * dhu + dhv * dhv)
        thk = th + rib * np.exp(-(v / 0.07) ** 2) * np.clip((u1 - u) / 0.6, 0, 1)
        shell = np.abs(d) - thk
        w = width(u)
        outline = np.maximum(np.abs(v) - w, np.maximum(u0 - u, u - u1))
        return -S.smin(-shell, -outline, 0.08)

    def to_world(u, v, off=0.0):
        h, dhu, dhv = height(np.array([u]), np.array([v]))
        n = np.array((-dhv[0], 1.0, -dhu[0])); n /= np.linalg.norm(n)
        q = np.array((v, h[0], u)) + n * off
        return O + R @ q, R @ n
    return f, local, height, width, to_world


def leaf_paints(fig, f, local, height, width, u0, u1, under=LEAF_UNDER, vein=LEAF_HI, spacing=0.75):
    near = lambda P: np.abs(f(P)) + 0.0

    def under_f(P):
        v, y, u = local(P)
        h = height(u, v)[0]
        return (y - h) * 30
    fig.paint(masked(under_f, near, 0.05), under, soft=0.6)

    def vein_f(P):
        v, y, u = local(P)
        mid = np.abs(v) - 0.05
        s = (u - u0 - 0.7 * np.abs(v)) / spacing
        side = np.abs(s - np.round(s)) * spacing - 0.022
        side = np.where((np.abs(v) < width(u) * 0.85) & (u < u1 - 0.4) & (u > u0 + 0.3), side, 1.0)
        return np.minimum(mid, side)
    fig.paint(masked(vein_f, near, 0.05), vein, soft=0.06)


def spiral_shell(c, axis, Rin, Rax, turns_a=0.42, A=0.12):
    """나선 껍데기: 축 방향으로 납작한 타원체 + 옆면에 둥글게 감긴 나선 고리 (양면)"""
    c = np.asarray(c, np.float64)
    Rm = axis_R(axis, (0, 1, 0))           # 로컬 y = 축
    Rm = np.stack([Rm[:, 2], Rm[:, 0], Rm[:, 1]], axis=1)   # 로컬 z = 축
    base = S.ellipsoid(c, (Rin, Rin, Rax), R=Rm)

    def coil(P):
        q = (P - c) @ Rm
        rho = np.hypot(q[:, 0], q[:, 1])
        phi = np.arctan2(q[:, 1], q[:, 0])
        t = rho / turns_a - phi / (2 * math.pi)
        side = np.clip(np.abs(q[:, 2]) / Rax * 1.6 - 0.25, 0, 1)
        return np.cos(2 * math.pi * t) * side, t, q, rho

    def f(P):
        cv, t, q, rho = coil(P)
        apex = 0.25 * np.exp(-(rho / 0.35) ** 2)
        return base(P) - A * (0.5 + 0.5 * cv) - apex
    return f, coil, Rm


def build(fig, rng):
    Fg.base(fig, rng, flowers=6)
    y0 = Fg.TOP
    # ---- 몸: 말랑한 물방울형 머리가 목 없이 아래로 넓어지며 몸으로 흘러내리는 덩어리 (약 1.9 등신) ----
    hc = np.array((0.0, y0 + 4.55, 0.1))
    head = soft_head(hc, (1.75, 1.8, 1.6), flare=0.1, taper=0.11, flare_y=-0.55)
    torso = S.ellipsoid((0, y0 + 2.05, 0.0), (1.5, 1.4, 1.22))
    parts = [head, torso]
    for s in (-1, 1):
        parts.append(S.ellipsoid((s * 0.66, y0 + 0.48, 0.18), (0.58, 0.5, 0.64)))
    sh_r, sh_l = np.array((1.15, y0 + 2.7, 0.25)), np.array((-1.15, y0 + 2.7, 0.15))
    hand_r = np.array((1.78, y0 + 2.55, 0.95))
    hand_l = np.array((-1.9, y0 + 2.0, 0.45))
    parts += [S.capsule(sh_r, hand_r, 0.4, 0.34), S.sphere(hand_r, 0.4),
              S.capsule(sh_l, hand_l, 0.4, 0.34), S.sphere(hand_l, 0.38)]
    # 눈자루 + 눈 공 (정수리에서 V 자로)
    balls = []
    for s in (-1, 1):
        b0 = np.array((s * 0.55, y0 + 5.9, 0.0))
        bc = np.array((s * 0.98, y0 + 7.3, 0.2))
        parts.append(S.capsule(b0, bc, 0.34, 0.28))
        balls.append(bc)
        parts.append(S.ellipsoid(bc, (0.6, 0.56, 0.56)))

    def core(P):
        d = S.smin(parts[0](P), parts[1](P), 0.6)
        for i, g in enumerate(parts[2:]):
            d = S.smin(d, g(P), 0.45 if i < 2 else 0.25)
        return d
    fig.add(core, CREAM, k=0.2)
    near_body = lambda P: np.abs(core(P))
    # 배 살짝 밝게
    fig.paint(masked(lambda P: np.hypot(P[:, 0] / 1.0, (P[:, 1] - y0 - 2.0) / 1.0) - 0.9 + (0.6 - P[:, 2]) * 2, near_body, 0.1),
              (1.0, 0.97, 0.88), soft=0.6)

    # ---- 얼굴: 동그랗고 짙은 눈 (넓게, 조금 아래), 작은 'w' 입, 칠한 볼터치, 엷은 팔자 눈썹 ----
    eye_pair(fig, hc, 25, -12, 'round', 0.44, iris=(0.24, 0.13, 0.07), layer='body', sink=0.16)
    mouth_w(fig, face_frame(fig, hc, 0, -27, 'body', out=0.005), w=0.2, th=0.042)
    blush_paint(fig, hc, 41, -24, 'body', size=0.42, soft=0.6)
    for s in (-1, 1):
        p0 = face_frame(fig, hc, s * 17, 6).o
        p1 = face_frame(fig, hc, s * 31, 3).o
        fig.paint(masked(S.capsule(p0, p1, 0.075, 0.05), near_body, 0.12), BROW, soft=0.03)
    # 눈자루 끝: 반쯤 감긴 졸린 눈 (eye_at droopy, 크림색 눈꺼풀)
    for s, bc in zip((-1, 1), balls):
        f = face_frame(fig, bc, s * 6, -6, 'body', out=-0.06)
        Fg.eye_at(fig, f, 'droopy', 0.36, (0.30, 0.17, 0.08), side=s, lid=(0.93, 0.79, 0.58))

    # ---- 껍데기 (등, 나선 면이 -x/뒤를 향함) + 진홍 점 ----
    axis = np.array((-1.0, 0.12, -0.85)); axis /= np.linalg.norm(axis)
    scn = np.array((-0.45, y0 + 3.0, -1.55))
    shell_f, coil, Rm = spiral_shell(scn, axis, 1.85, 1.15, turns_a=0.46, A=0.14)
    fig.add(shell_f, SHELL, k=0.0, layer='shell')
    near_shell = lambda P: np.abs(shell_f(P))
    fig.paint(masked(lambda P: (0.25 - coil(P)[0]) * 3, near_shell, 0.08), SHELL_D, soft=1.2)
    fig.paint(masked(lambda P: (coil(P)[0] - 0.85) * -3 + 0.0, near_shell, 0.08), SHELL_HI, soft=0.6)
    fig.paint(masked(lambda P: P[:, 1] - (y0 + 1.6), near_shell, 0.08), SHELL_D, soft=0.8)
    gem = scn + np.array((0.25, 1.62, 0.25))
    p_g, n_g = surf_point(shell_f, scn, -95, 58)
    fig.add(S.ellipsoid(tuple(p_g + n_g * 0.02), (0.3, 0.3, 0.16), R=basis(n_g)), Fg.CRIMSON, k=0.0, layer='gem',
            metal=(Fg.CRIMSON, (1.0, 0.55, 0.55)))

    # ---- 잎 우산 (오른손 +x 으로 줄기를 쥠) ----
    O = np.array((0.3, y0 + 10.05, 0.0))
    d = np.array((1.0, 0.0, 0.3)); d /= np.linalg.norm(d)
    R = np.stack([np.cross((0, 1, 0), d), np.array((0, 1.0, 0)), d], axis=1)
    u0, u1 = -2.9, 3.4
    lf, local, height, width, to_world = leaf_fn(O, R, u0, u1, 3.15, 0.3, 0.17, 0.19, th=0.12, rib=0.06, tip_pow=0.65)
    fig.add(lf, LEAF, k=0.0, layer='leaf')
    leaf_paints(fig, lf, local, height, width, u0, u1, spacing=0.9)
    # 물방울
    for (u, v, r) in ((1.1, 1.0, 0.26), (-1.4, -0.6, 0.19), (2.1, -0.7, 0.15)):
        p, n = to_world(u, v, 0.1)
        Rd = axis_R(n, (0, 0, 1))
        fig.add(S.ellipsoid(tuple(p + n * r * 0.35), (r, r * 0.6, r), R=Rd),
                (0.78, 0.95, 0.72), k=0.0, layer='drop', metal=((0.55, 0.85, 0.5), (1.0, 1.0, 1.0)))
    # 줄기: 손 아래 → 손 → 위로 휘어 잎 밑면 중심
    top, _ = to_world(0.0, 0.0, -0.05)
    pts = [hand_r + (0.12, -0.95, 0.05), hand_r + (0.05, 0.0, 0.0), hand_r + (-0.05, 1.6, -0.1),
           np.array((1.4, y0 + 6.6, 0.6)), np.array((1.15, y0 + 8.0, 0.4)), top]
    for a, b in zip(pts, pts[1:]):
        fig.add(S.capsule(tuple(a), tuple(b), 0.13, 0.12), STEM, k=0.15, layer='stem')
    # 손가락 (줄기를 감싼 짧은 손가락 두 개)
    for k in range(2):
        fig.add(S.capsule(tuple(hand_r + (0.25, 0.12 - k * 0.22, 0.25)), tuple(hand_r + (-0.05, 0.1 - k * 0.22, 0.42)), 0.13, 0.12), CREAM, k=0.1, layer='fing')

    # ---- 받침: 풀 덤불, 조약돌 ----
    for (x, z) in ((-3.0, 1.2), (2.9, 1.5), (-2.2, -2.6), (2.3, -2.5), (0.2, 3.3)):
        tuft(fig, x, z, rng, n=5, h=0.75)
    for i, (x, z, r) in enumerate(((-1.6, 2.6, 0.2), (1.2, 2.8, 0.18), (3.4, -0.3, 0.2), (-3.5, -0.8, 0.17), (-0.6, -3.4, 0.16))):
        pebble(fig, x, z, r, seed=i)
