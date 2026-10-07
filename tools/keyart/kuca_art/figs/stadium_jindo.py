"""운동장 진돗개 강아지: 흰 털, 짧고 뾰족하게 선 세모 귀(분홍 안쪽), 말린 꼬리, 빨간 머리띠, 흰 민소매(빨간 테),
빨간 반바지(흰 줄), 빨간 손목밴드, 빨간 운동화(흰 밑창·끈), 빨간 바통, 받침 위 빨간 트랙."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .clock_rooster import Fast, hit, layer_paint, eye_on, beads, rmax

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

    # ---------- 운동화 (빨강 + 흰 밑창·줄·끈) ----------
    for s in (-1, 1):
        x = s * 0.72
        fig.add(S.ellipsoid((x, y0 + 0.13, 0.35), (0.56, 0.16, 0.78)), (0.99, 0.99, 0.99), k=0.0, layer='sole')
        fig.add(S.ellipsoid((x, y0 + 0.42, 0.3), (0.52, 0.36, 0.72)), RED, k=0.15, layer='shoe')
        fig.add(S.capsule((x, y0 + 0.5, 0.0), (x, y0 + 0.75, -0.05), 0.44), RED, k=0.15, layer='shoe')
        fig.add(S.torus((x, y0 + 0.86, -0.05), 0.4, 0.07), (0.99, 0.99, 0.99), k=0.0, layer='sole')
        for j in range(3):   # 끈
            zc = 0.72 - j * 0.15
            q = hit(fig, (x, y0 + 0.95, zc), (0, -1, 0.0))
            beads(fig, [q + (-0.17, 0.02, 0), q + (0.17, 0.02, 0)], 0.04, (1, 1, 1))
        for j in range(2):   # 옆 흰 줄 두 개
            off = j * 0.2
            for sd in (s,):
                a = hit(fig, (x + sd * 1.5, y0 + 0.32, 0.45 - off), (-sd, 0, 0))
                b = hit(fig, (x + sd * 1.5, y0 + 0.62, 0.15 - off), (-sd, 0, 0))
                fig.add(S.capsule(tuple(a), tuple(b), 0.05), (0.99, 0.99, 0.99), k=0.0, layer='stripe')

    # ---------- 다리 ----------
    for s in (-1, 1):
        fig.add(S.capsule((s * 0.72, y0 + 0.85, -0.02), (s * 0.74, y0 + 1.6, 0.0), 0.38, 0.45), FUR, k=0.2)

    # ---------- 몸통 ----------
    torso = [((0, y0 + 2.55, 0.0), (1.32, 1.25, 1.1)), ((0, y0 + 1.75, 0.02), (1.3, 0.7, 1.05))]
    for c, r in torso:
        fig.add(S.ellipsoid(c, r), FUR, k=0.45)

    def torso_f(P):
        return S.smin(S.ellipsoid(*torso[0])(P), S.ellipsoid(*torso[1])(P), 0.45)

    # 반바지 (빨강) + 다리통
    def shorts(P):
        d = np.abs(torso_f(P) - 0.09) - 0.07
        return rmax(d, np.abs(P[:, 1] - (y0 + 1.62)) - 0.42, 0.05)
    fig.add(shorts, RED, k=0.0, layer='shorts')
    for s in (-1, 1):
        fig.add(S.intersect(S.capsule((s * 0.75, y0 + 1.15, 0.0), (s * 0.7, y0 + 1.9, 0.0), 0.56), lambda P: (y0 + 1.15) - P[:, 1]),
                RED, k=0.12, layer='shorts')
        fig.add(S.torus((s * 0.75, y0 + 1.17, 0.0), 0.5, 0.06), RED_DK, k=0.0, layer='shortshem')
    # 반바지 옆 흰 줄
    for s in (-1, 1):
        pts = [hit(fig, (s * 4.5, y, 0.0), (-s, 0, 0)) for y in np.linspace(y0 + 1.22, y0 + 1.98, 8)]
        for a, b in zip(pts, pts[1:]):
            fig.add(S.capsule(tuple(a), tuple(b), 0.06), (0.99, 0.99, 0.99), k=0.0, layer='stripe')

    # 민소매 (흰 껍질) + 빨간 테
    def tank(P):
        d = np.abs(torso_f(P) - 0.2) - 0.07
        d = rmax(d, (y0 + 1.9) - P[:, 1], 0.06)
        d = np.maximum(d, P[:, 1] - (y0 + 3.75))
        # 앞 목둘레 U 파임
        neck = np.maximum(np.hypot(P[:, 0] / 0.62, (P[:, 1] - (y0 + 3.75)) / 0.55) - 1.0, -(P[:, 2] - 0.2))
        d = np.maximum(d, -neck * 0.5)
        return d
    fig.add(tank, SHIRT, k=0.0, layer='tank')
    # 목 테 (U)
    pts = []
    for a in np.linspace(-math.pi * 0.5, math.pi * 0.5, 15):
        x = math.sin(a) * 0.62
        y = y0 + 3.75 - math.cos(a) * 0.55
        pts.append(hit(fig, (x, y, 4.5), (0, 0, -1), field=tank))
    for a, b in zip(pts, pts[1:]):
        fig.add(S.capsule(tuple(a), tuple(b), 0.07), RED, k=0.02, layer='trim')
    # 밑단 테

    # ---------- 팔 + 손목밴드 ----------
    arms = {}
    arm_holes = []
    for s in (-1, 1):
        sh = (s * 1.18, y0 + 3.3, 0.05)
        el = (s * 1.6, y0 + 2.55, 0.25)
        hd = (s * 1.82, y0 + 1.95, 0.5)
        fig.add(S.sphere(sh, 0.45), FUR, k=0.25)
        fig.add(S.capsule(sh, el, 0.4, 0.36), FUR, k=0.25)
        fig.add(S.capsule(el, hd, 0.36, 0.34), FUR, k=0.2)
        fig.add(S.sphere(hd, 0.38), FUR, k=0.15)
        arms[s] = (sh, el, hd)
        ad = np.array(el) - np.array(sh)
        ad /= np.linalg.norm(ad)
        arm_holes.append((S.capsule(sh, el, 0.4, 0.36), S.sphere(sh, 0.62)))
        u = np.array(hd) - np.array(el)
        u /= np.linalg.norm(u)
        wc = np.array(el) + u * 0.48
        R = S.rot(0, 0, s * 20)
        fig.add(S.cylinder(tuple(wc), 0.42, 0.15, round_=0.06, R=R), RED, k=0.0, layer='band')

    for ah, near in arm_holes:   # 소매 둘레 빨간 테 (민소매 껍질 위, 어깨 근처만)
        layer_paint(fig, 'tank', lambda P, ah=ah, near=near: np.maximum(np.abs(ah(P) - 0.08) - 0.11, near(P)), RED, 0.03, tol=0.03)
    # ---------- 바통 (왼손, 화면 왼쪽) ----------
    hd = np.array(arms[-1][2])
    b0 = hd + np.array((0.15, 0.45, 0.25))
    b1 = hd + np.array((-0.55, -1.05, 0.55))
    fig.add(S.subtract(S.capsule(tuple(b0), tuple(b1), 0.17), S.capsule(tuple(b0 - (b1 - b0) * 0.1), tuple(b1 + (b1 - b0) * 0.1), 0.11)),
            BATON, k=0.0, layer='baton', metal=RB)
    for k in range(3):   # 손가락이 감쌈
        p = hd + np.array((0.05, 0.1 - k * 0.17, 0.35))
        fig.add(S.sphere(tuple(p), 0.15), FUR, k=0.08)

    # ---------- 꼬리 (등 위로 말린 진돗개 꼬리) ----------
    tail = [((0, y0 + 2.2, -1.0), 0.32), ((0, y0 + 2.75, -1.55), 0.4), ((0.05, y0 + 3.4, -1.75), 0.45),
            ((0.12, y0 + 3.85, -1.45), 0.42), ((0.2, y0 + 3.8, -1.05), 0.34), ((0.25, y0 + 3.5, -0.85), 0.25)]
    for (a, ra), (b, rb) in zip(tail, tail[1:]):
        fig.add(S.capsule(a, b, ra, rb), FUR, k=0.25, layer='tail')

    # ---------- 머리 ----------
    hc = (0, y0 + 5.45, 0.1)
    hr = (2.02, 1.8, 1.85)
    fig.add(S.ellipsoid(hc, hr), FUR, k=0.5)
    fig.add(S.ellipsoid((0, y0 + 3.85, 0.1), (1.0, 0.5, 0.85)), FUR, k=0.5)
    for s in (-1, 1):   # 볼살
        fig.add(S.sphere((s * 1.1, hc[1] - 1.0, hc[2] + 0.8), 0.7), FUR, k=0.6)
    fig.add(S.ellipsoid((0, y0 + 4.78, 1.75), (0.62, 0.44, 0.55)), FUR, k=0.3)       # 주둥이
    # 귀: 짧고 뾰족한 세모, 바짝 섬 (진돗개)
    for s in (-1, 1):
        R = S.rot(s * 10, 6, -s * 14)
        base_c = (s * 1.05, hc[1] + 1.28, -0.05)
        ear = tri_ear(base_c, R, 0.66, 1.15, 0.22, 0.13)
        inner = tri_ear(tuple(np.array(base_c) + R @ np.array((0, 0.12, 0.2))), R, 0.36, 0.85, 0.08, 0.05)
        fig.add(S.subtract(ear, inner, k=0.05), FUR, k=0.25, layer='ear')
        fig.paint(tri_ear(tuple(np.array(base_c) + R @ np.array((0, 0.12, 0.14))), R, 0.4, 0.92, 0.12, 0.05), EAR_IN, soft=0.05)

    # 머리띠 (이마를 두른 빨간 밴드, 귀 아래)
    hb_c = np.array((0, hc[1] + 1.0, hc[2]))
    Rh = S.rot(0, -4, 0)

    def headband(P):
        d = np.abs(S.ellipsoid(hc, (hr[0] + 0.06, hr[1] + 0.06, hr[2] + 0.06))(P)) - 0.11
        q = (P - hb_c) @ Rh
        return rmax(d, np.abs(q[:, 1]) - 0.27, 0.07)
    fig.add(headband, RED, k=0.0, layer='headband')

    # 얼굴
    for s in (-1, 1):
        eye_on(fig, hc, s * 27, -2, size=0.58, tall=1.1, side=s, sink=-0.04)
    nz = hit(fig, (0, y0 + 4.98, 4.5), (0, 0, -1))
    Fg._ellipsoid(fig.extra, tuple(nz + (0, 0, 0.02)), (0.2, 0.14, 0.12), NOSE, 12, 6)
    Fg._ellipsoid(fig.extra, tuple(nz + (-0.06, 0.06, 0.1)), (0.05, 0.03, 0.03), (0.7, 0.6, 0.6), 6, 4)
    for s in (-1, 1):
        pts = []
        for k in range(8):
            a = math.pi * k / 7
            x = s * (0.14 + math.cos(a) * 0.14)
            y = y0 + 4.7 - math.sin(a) * 0.12
            pts.append(hit(fig, (x, y, 4.5), (0, 0, -1)))
        beads(fig, pts, 0.035, (0.32, 0.18, 0.16))
    beads(fig, [nz + (0, -0.1, 0), hit(fig, (0, y0 + 4.71, 4.5), (0, 0, -1))], 0.03, (0.32, 0.18, 0.16))
    for s in (-1, 1):
        q = hit(fig, (s * 1.3, y0 + 4.85, 4.5), (0, 0, -1))
        layer_paint(fig, 'body', S.sphere(tuple(q), 0.42), Fg.BLUSH, 0.5, tol=0.1)
