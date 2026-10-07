"""운동장 진돗개 강아지: 흰 털, 짧고 뾰족하게 선 세모 귀(분홍 안쪽), 말린 꼬리, 빨간 머리띠, 흰 민소매(빨간 테),
빨간 반바지(흰 줄), 빨간 손목밴드, 빨간 운동화(흰 밑창·끈), 빨간 바통, 받침 위 빨간 트랙."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .clock_rooster import Fast, hit, layer_paint, beads, rmax, face_frame, surf_beads

TIER = 'gold'

FUR = (0.985, 0.975, 0.962)
SHIRT = (0.995, 0.995, 0.995)
RED = (0.84, 0.13, 0.16)
RED_DK = (0.66, 0.08, 0.11)
EAR_IN = (1.0, 0.70, 0.74)
NOSE = (0.26, 0.15, 0.12)
TRACK = (0.82, 0.30, 0.26)
BATON = (0.86, 0.10, 0.12)
RB = (BATON, (1.0, 0.42, 0.40))


def tri_ear(c, R, half_w, h, th, rnd):
    """세모 귀: 로컬 u(가로) v(위) w(두께). 밑변 중심 c, 둥근 모서리"""
    c = np.asarray(c, np.float64)
    L = math.hypot(half_w, h)

    def f(P):
        q = (P - c) @ R
        u, v, w = np.abs(q[:, 0]), q[:, 1], q[:, 2]
        d2 = np.maximum(-v, (u * h + v * half_w - half_w * h) / L) + rnd
        # 앞뒤로 볼록 (가운데 두껍고 가장자리 얇게)
        t = th * np.clip(1.0 - np.maximum(v, 0) / h * 0.6, 0.3, 1.0)
        dz = np.abs(w) - t
        a = np.stack([d2, dz], 1)
        return np.linalg.norm(np.maximum(a, 0), axis=1) + np.minimum(a.max(axis=1), 0) - rnd
    return f


def align_y(d):
    """로컬 Y 를 d 로 보내는 회전 행렬"""
    d = np.asarray(d, np.float64)
    d = d / np.linalg.norm(d)
    a = np.array((1.0, 0, 0)) if abs(d[0]) < 0.9 else np.array((0, 0, 1.0))
    x = np.cross(d, a)
    x /= np.linalg.norm(x)
    z = np.cross(x, d)
    return np.stack([x, d, z], axis=1)


def build(fig, rng):
    fig = Fast(fig)
    Fg.base(fig, rng, flowers=7)
    y0 = Fg.TOP

    # ---------- 받침: 빨간 트랙 조각 (흰 레인 선) ----------
    a0, a1 = math.radians(25), math.radians(150)
    r_in, r_out = 3.05, 4.0
    ytop = y0 + 0.34

    def track(P):
        x, y, z = P[:, 0], P[:, 1], P[:, 2]
        r = np.hypot(x, z)
        a = np.arctan2(z, x)
        dr = np.abs(r - (r_in + r_out) / 2) - (r_out - r_in) / 2
        da = np.maximum(a0 - a, a - a1) * r
        dy = np.abs(y - (ytop - 0.25)) - 0.25
        q = np.stack([np.maximum(dr, da) + 0.12, dy + 0.12], 1)
        return np.linalg.norm(np.maximum(q, 0), axis=1) + np.minimum(q.max(axis=1), 0) - 0.12
    fig.add(track, TRACK, k=0.0, layer='track')
    for rr in (3.35, 3.68):
        pts = [(math.cos(a) * rr, ytop + 0.005, math.sin(a) * rr) for a in np.linspace(a0 + 0.05, a1 - 0.05, 40)]
        for p, q in zip(pts, pts[1:]):
            fig.add(S.capsule(p, q, 0.035), (0.99, 0.99, 0.99), k=0.0, layer='lane')

    # ---------- 체형: 다리 긴 날씬한 스포츠 체형 (약 2.4 등신) ----------
    LEG_TOP = y0 + 2.35          # 다리 위 (엉덩이)
    # ---------- 운동화 (빨강 + 흰 밑창·줄·끈) ----------
    for s in (-1, 1):
        x = s * 0.6
        fig.add(S.ellipsoid((x, y0 + 0.13, 0.32), (0.5, 0.16, 0.74)), (0.99, 0.99, 0.99), k=0.0, layer='sole')
        fig.add(S.ellipsoid((x, y0 + 0.4, 0.28), (0.46, 0.34, 0.68)), RED, k=0.15, layer='shoe')
        fig.add(S.capsule((x, y0 + 0.48, 0.0), (x, y0 + 0.72, -0.05), 0.38), RED, k=0.15, layer='shoe')
        fig.add(S.torus((x, y0 + 0.83, -0.05), 0.34, 0.065), (0.99, 0.99, 0.99), k=0.0, layer='sole')
        for j in range(3):   # 끈
            zc = 0.66 - j * 0.15
            q = hit(fig, (x, y0 + 0.95, zc), (0, -1, 0.0))
            beads(fig, [q + (-0.15, 0.02, 0), q + (0.15, 0.02, 0)], 0.038, (1, 1, 1))
        for j in range(2):   # 옆 흰 줄 두 개
            off = j * 0.2
            a = hit(fig, (x + s * 1.5, y0 + 0.3, 0.42 - off), (-s, 0, 0))
            b = hit(fig, (x + s * 1.5, y0 + 0.6, 0.12 - off), (-s, 0, 0))
            fig.add(S.capsule(tuple(a), tuple(b), 0.05), (0.99, 0.99, 0.99), k=0.0, layer='stripe')

    # ---------- 다리 (길고 가는 다리, 종아리 살짝) ----------
    for s in (-1, 1):
        fig.add(S.capsule((s * 0.6, y0 + 0.8, -0.04), (s * 0.6, y0 + 1.45, 0.0), 0.3, 0.33), FUR, k=0.15)
        fig.add(S.capsule((s * 0.6, y0 + 1.45, 0.0), (s * 0.64, LEG_TOP, 0.0), 0.33, 0.4), FUR, k=0.2)

    # ---------- 몸통: 어깨가 살짝 넓고 허리가 잘록한 날씬한 몸 ----------
    torso = [((0, y0 + 3.3, 0.0), (1.08, 0.95, 0.82)), ((0, y0 + 2.5, 0.0), (0.98, 0.62, 0.78))]
    for c, r in torso:
        fig.add(S.ellipsoid(c, r), FUR, k=0.4)

    def torso_f(P):
        return S.smin(S.ellipsoid(*torso[0])(P), S.ellipsoid(*torso[1])(P), 0.4)

    # 반바지 (빨강) + 다리통
    SH_Y = y0 + 2.42

    def shorts(P):
        d = np.abs(torso_f(P) - 0.08) - 0.06
        return rmax(d, np.abs(P[:, 1] - SH_Y) - 0.4, 0.05)
    fig.add(shorts, RED, k=0.0, layer='shorts')
    for s in (-1, 1):
        fig.add(S.intersect(S.capsule((s * 0.62, y0 + 1.8, 0.0), (s * 0.6, y0 + 2.6, 0.0), 0.47), lambda P: (y0 + 1.8) - P[:, 1]),
                RED, k=0.12, layer='shorts')
        fig.add(S.torus((s * 0.62, y0 + 1.82, 0.0), 0.43, 0.055), RED_DK, k=0.0, layer='shortshem')
    # 반바지 옆 흰 줄
    for s in (-1, 1):
        pts = [hit(fig, (s * 4.5, y, -0.05), (-s, 0, 0)) for y in np.linspace(y0 + 1.86, y0 + 2.75, 8)]
        for a, b in zip(pts, pts[1:]):
            fig.add(S.capsule(tuple(a), tuple(b), 0.055), (0.99, 0.99, 0.99), k=0.0, layer='stripe')

    # 민소매 (흰 껍질, 팔 구멍·목 U 파임을 또렷하게 자름) — 테는 아래에서 칠
    T_TOP = y0 + 4.08
    ARM = {s_: ((s_ * 1.02, y0 + 3.75, 0.02), (s_ * 1.35, y0 + 2.95, 0.18)) for s_ in (-1, 1)}

    def neck_u(P):
        return np.hypot(P[:, 0] / 0.48, (P[:, 1] - T_TOP) / 0.34)

    def armcut(P):
        d = None
        for sh_, el_ in ARM.values():
            v = np.minimum(S.capsule(sh_, el_, 0.31, 0.28)(P), S.sphere(sh_, 0.36)(P))
            d = v if d is None else np.minimum(d, v)
        return d

    def tank(P):
        d = np.abs(torso_f(P) - 0.16) - 0.075
        d = rmax(d, (y0 + 2.62) - P[:, 1], 0.05)
        d = np.maximum(d, P[:, 1] - T_TOP)
        d = np.maximum(d, -np.maximum(neck_u(P) - 1.0, -(P[:, 2] - 0.15)))
        d = np.maximum(d, -(armcut(P) - 0.05))
        return d
    fig.add(tank, SHIRT, k=0.0, layer='tank')

    # ---------- 팔 (가늘고 김) + 손목밴드 ----------
    arms = {}
    arm_holes = []
    for s in (-1, 1):
        sh = (s * 1.02, y0 + 3.75, 0.02)
        el = (s * 1.35, y0 + 2.95, 0.18)
        hd = (s * 1.55, y0 + 2.25, 0.42)
        fig.add(S.sphere(sh, 0.36), FUR, k=0.2)
        fig.add(S.capsule(sh, el, 0.31, 0.28), FUR, k=0.2)
        fig.add(S.capsule(el, hd, 0.28, 0.27), FUR, k=0.18)
        fig.add(S.sphere(hd, 0.31), FUR, k=0.12)
        arms[s] = (sh, el, hd)
        arm_holes.append((S.capsule(sh, el, 0.31, 0.28), S.sphere(sh, 0.52)))
        u = np.array(hd) - np.array(el)
        u /= np.linalg.norm(u)
        wc = np.array(el) + u * 0.45
        R = S.rot(0, 0, s * 16)
        fig.add(S.cylinder(tuple(wc), 0.34, 0.13, round_=0.05, R=R), RED, k=0.0, layer='band')

    # 민소매 빨간 테: 목 U 둘레 + 팔 구멍 둘레 (또렷한 띠)
    layer_paint(fig, 'tank', lambda P: np.where(P[:, 2] > 0.15, neck_u(P) - 1.28, 1.0), RED, 0.02, tol=0.04)
    layer_paint(fig, 'tank', lambda P: armcut(P) - 0.2, RED, 0.02, tol=0.04)
    # ---------- 바통 (왼손, 화면 왼쪽) ----------
    hd = np.array(arms[-1][2])
    b0 = hd + np.array((0.12, 0.4, 0.22))
    b1 = hd + np.array((-0.5, -0.95, 0.5))
    fig.add(S.subtract(S.capsule(tuple(b0), tuple(b1), 0.15), S.capsule(tuple(b0 - (b1 - b0) * 0.1), tuple(b1 + (b1 - b0) * 0.1), 0.1)),
            BATON, k=0.0, layer='baton', metal=RB)
    for k in range(3):   # 손가락이 감쌈
        p = hd + np.array((0.04, 0.08 - k * 0.15, 0.28))
        fig.add(S.sphere(tuple(p), 0.13), FUR, k=0.08)

    # ---------- 꼬리 (등 위로 말린 진돗개 꼬리) ----------
    tail = [((0, y0 + 2.55, -0.7), 0.26), ((0, y0 + 3.0, -1.2), 0.34), ((0.05, y0 + 3.55, -1.38), 0.38),
            ((0.12, y0 + 3.95, -1.12), 0.36), ((0.18, y0 + 3.92, -0.8), 0.28), ((0.22, y0 + 3.65, -0.66), 0.2)]
    for (a, ra), (b, rb) in zip(tail, tail[1:]):
        fig.add(S.capsule(a, b, ra, rb), FUR, k=0.25, layer='tail')

    # ---------- 머리: 주둥이가 앞으로 나온 진돗개 두상 (볼 구 없음, 아래 볼 볼륨만) ----------
    hc = np.array((0, y0 + 5.55, 0.0))
    cran = (tuple(hc), (1.5, 1.42, 1.42))
    fig.add(S.ellipsoid(*cran), FUR, k=0.45)
    fig.add(S.ellipsoid(tuple(hc + (0, -0.45, 0.1)), (1.5, 0.98, 1.32)), FUR, k=0.5)               # 아래 볼 볼륨
    fig.add(S.ellipsoid(tuple(hc + (0, -0.55, 1.42)), (0.66, 0.5, 0.78)), FUR, k=0.38)              # 주둥이 (앞으로)
    fig.add(S.ellipsoid(tuple(hc + (0, -0.2, 1.1)), (0.52, 0.45, 0.7)), FUR, k=0.4)                 # 콧등 (stop)
    fig.add(S.ellipsoid((0, y0 + 4.2, 0.0), (0.7, 0.4, 0.62)), FUR, k=0.4)                           # 목
    # 귀: 짧고 뾰족한 세모, 바짝 섬
    for s in (-1, 1):
        R = S.rot(s * 12, 4, -s * 16)
        base_c = (s * 0.9, hc[1] + 1.05, -0.12)
        ear = tri_ear(base_c, R, 0.56, 1.0, 0.2, 0.12)
        inner = tri_ear(tuple(np.array(base_c) + R @ np.array((0, 0.12, 0.18))), R, 0.3, 0.74, 0.07, 0.05)
        fig.add(S.subtract(ear, inner, k=0.05), FUR, k=0.25, layer='ear')
        fig.paint(tri_ear(tuple(np.array(base_c) + R @ np.array((0, 0.12, 0.13))), R, 0.34, 0.8, 0.11, 0.05), EAR_IN, soft=0.05)

    # 머리띠 (이마를 두른 빨간 밴드, 귀 아래)
    hb_c = hc + (0, 0.78, 0)
    Rh = S.rot(0, -6, 0)

    def headband(P):
        d = np.abs(S.ellipsoid(*cran)(P) - 0.06) - 0.1
        q = (P - hb_c) @ Rh
        return rmax(d, np.abs(q[:, 1]) - 0.24, 0.07)
    fig.add(headband, RED, k=0.0, layer='headband')

    # ---------- 얼굴: 밝은 아몬드 눈 (눈꼬리 살짝 올라감) ----------
    for s in (-1, 1):
        f = face_frame(fig, hc, s * 29, 4, out=-0.06)
        Fg.eye_at(fig, f, style='almond', size=0.44, iris=(0.42, 0.23, 0.09), side=s, tilt=13)
    fn = face_frame(fig, hc + (0, -0.45, 0), 0, 0)          # 주둥이 끝
    no, nn = np.asarray(fn.o), np.asarray(fn.z)
    Fg._ellipsoid(fig.extra, tuple(no + nn * 0.02 + (0, 0.02, 0)), (0.2, 0.14, 0.13), NOSE, 12, 6)
    Fg._ellipsoid(fig.extra, tuple(no + nn * 0.13 + (-0.06, 0.08, 0)), (0.05, 0.03, 0.03), (0.7, 0.6, 0.6), 6, 4)
    fm = face_frame(fig, hc + (0, -0.45, 0), 0, -13)
    mo = np.asarray(fm.o)
    pts2 = [(0, 0.1), (0, -0.02)]
    surf_beads(fig, fm, pts2, 0.03, (0.32, 0.18, 0.16))
    for s in (-1, 1):
        surf_beads(fig, fm, [(s * (0.13 + math.cos(math.pi * k / 7) * 0.13), -0.02 - math.sin(math.pi * k / 7) * 0.1) for k in range(8)],
                   0.032, (0.32, 0.18, 0.16))
    for s in (-1, 1):
        q = np.asarray(face_frame(fig, hc, s * 46, -18).o)
        layer_paint(fig, 'body', S.sphere(tuple(q), 0.38), Fg.BLUSH, 0.45, tol=0.08)
