"""멀티미디어관 미어캣: 날씬한 모래색 몸, 짙은 눈 무늬, 진홍 줄 크림 비니, 목에 건 콤팩트 카메라, 받침 위 미니 삼각대"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

SAND = (0.91, 0.75, 0.50)
SAND_DK = (0.78, 0.62, 0.42)
CREAM = (0.98, 0.93, 0.83)
PATCH = (0.36, 0.21, 0.13)
DARK = (0.35, 0.22, 0.15)
KNIT = (0.97, 0.94, 0.87)
STRAP = (0.72, 0.12, 0.18)
CAM_BLK = (0.16, 0.16, 0.18)
CAM_SIL = (0.80, 0.81, 0.84)
LENS = (0.10, 0.14, 0.22)
TRI = (0.18, 0.18, 0.20)


def rmax(a, b, r):
    """둥근 교집합 (모서리 반지름 r)"""
    a, b = a + r, b + r
    return np.linalg.norm(np.maximum(np.stack([a, b], 1), 0), axis=1) + np.minimum(np.maximum(a, b), 0) - r


def head_frame(fig, c, yaw, pitch, layer='body', out=0.0):
    """머리 중심 c 에서 (yaw, pitch) 방향의 실제 조형 표면 위 Frame"""
    y, p = math.radians(yaw), math.radians(pitch)
    d = np.array((math.cos(p) * math.sin(y), math.sin(p), math.cos(p) * math.cos(y)))
    return Fg.surface_frame(fig, np.asarray(c) + d * 7.0, -d, out=out, layer=layer)


def w_mouth(fig, f, w, col):
    for s in (-1, 1):
        for k in range(9):
            a = math.pi * k / 8
            Fg._ellipsoid(fig.extra, (s * w * 0.5 + math.cos(a) * w * 0.5, -math.sin(a) * w * 0.42, 0.01), (0.045, 0.045, 0.04), col, 6, 4, f)


def build(fig, rng):
    Fg.base(fig, rng, flowers=7)
    y0 = Fg.TOP
    g = (Fg.GOLD, Fg.GOLD_HI)
    sil = (CAM_SIL, (1.0, 1.0, 1.0))
    # ---- 다리·발: 가늘고 곧게 ----
    for s in (-1, 1):
        x = s * 0.46
        fig.add(S.ellipsoid((x, y0 + 0.22, 0.25), (0.38, 0.24, 0.55)), DARK, k=0.1, layer='feet')
        fig.add(S.capsule((x, y0 + 0.42, 0.03), (x * 1.05, y0 + 1.65, 0.0), 0.32, 0.42), SAND, k=0.12)
    # ---- 몸통: 키 크고 가는 몸, 꼿꼿이 선 자세, 크림 배 ----
    fig.add(S.ellipsoid((0, y0 + 2.0, 0.0), (0.9, 0.78, 0.78)), SAND, k=0.4)
    fig.add(S.ellipsoid((0, y0 + 3.3, 0.0), (0.78, 1.35, 0.7)), SAND, k=0.6)
    fig.add(S.capsule((0, y0 + 4.2, -0.02), (0, y0 + 5.2, 0.02), 0.52, 0.5), SAND, k=0.3)
    fig.paint(S.ellipsoid((0, y0 + 2.9, 0.62), (0.55, 1.75, 0.42)), CREAM, soft=0.25)
    # ---- 카메라: 가슴 앞, 두 손으로 받쳐 든 모습 (검정 몸 + 은색 윗판 + 렌즈) ----
    cc = np.array((0, y0 + 3.35, 0.98))
    Rcam = S.rot(0, -4, 0)
    cs = 1.1
    C = lambda v: tuple(cc + Rcam @ (np.array(v) * cs))
    fig.add(S.box(tuple(cc), np.array((0.66, 0.32, 0.2)) * cs, round_=0.08, R=Rcam), CAM_BLK, k=0.0, layer='cam')
    fig.add(S.box(C((0, 0.3, 0)), np.array((0.66, 0.1, 0.2)) * cs, round_=0.07, R=Rcam), CAM_SIL, k=0.02, layer='cam', metal=sil)
    fig.add(S.box(C((-0.35, 0.44, -0.02)), np.array((0.16, 0.07, 0.12)) * cs, round_=0.04, R=Rcam), CAM_SIL, k=0.0, layer='cam2', metal=sil)
    fig.add(S.cylinder(C((0.38, 0.44, 0)), 0.08 * cs, 0.05 * cs, round_=0.02), CAM_SIL, k=0.0, layer='cam2', metal=sil)
    Rl = Rcam @ S.rot(0, 90, 0)
    fig.add(S.cylinder(C((0.12, -0.03, 0.28)), 0.25 * cs, 0.12 * cs, round_=0.04, R=Rl), CAM_SIL, k=0.0, layer='lens', metal=sil)
    fig.add(S.cylinder(C((0.12, -0.03, 0.38)), 0.19 * cs, 0.06 * cs, round_=0.03, R=Rl), CAM_BLK, k=0.0, layer='lens2')
    fig.add(S.sphere(C((0.12, -0.03, 0.32)), 0.15 * cs), LENS, k=0.0, layer='glass')
    fig.add(S.box(C((-0.42, 0.12, 0.2)), np.array((0.1, 0.06, 0.03)) * cs, round_=0.02, R=Rcam), (0.55, 0.62, 0.70), k=0.0, layer='cam2')
    Fg._ellipsoid(fig.extra, C((0.06, 0.04, 0.47)), (0.06, 0.06, 0.025), (1, 1, 1), 8, 4)
    for s in (-1, 1):   # 끈: 카메라 양옆 → 어깨 → 목 뒤
        lug = cc + Rcam @ (np.array((s * 0.68, 0.22, 0)) * cs)
        pts = [lug, np.array((s * 0.6, y0 + 4.05, 0.62)), np.array((s * 0.52, y0 + 4.55, 0.3)), np.array((s * 0.42, y0 + 4.75, -0.2)), np.array((0, y0 + 4.8, -0.5))]
        for a, b in zip(pts, pts[1:]):
            fig.add(S.capsule(tuple(a), tuple(b), 0.065), STRAP, k=0.03, layer='strap')
        fig.add(S.torus(tuple(lug), 0.07, 0.03, Rm=S.rot(0, 0, 90)), (0.95, 0.75, 0.2), k=0.0, layer='ring', metal=g)
    # ---- 팔: 팔꿈치를 몸 옆에 붙이고 앞으로 굽혀 두 손으로 카메라 양옆을 잡음 ----
    for s in (-1, 1):
        sh = np.array((s * 0.72, y0 + 4.15, 0.0))
        el = np.array((s * 0.98, y0 + 3.1, 0.2))
        wr = np.array((s * 0.92, y0 + 3.22, 0.78))
        fig.add(S.sphere(tuple(sh), 0.32), SAND, k=0.12)
        fig.add(S.capsule(tuple(sh), tuple(el), 0.28, 0.25), SAND, k=0.1)
        fig.add(S.capsule(tuple(el), tuple(wr), 0.25, 0.23), SAND, k=0.15)
        hand = np.array((s * 0.84, y0 + 3.3, 0.98))
        fig.add(S.ellipsoid(tuple(hand), (0.2, 0.27, 0.24)), DARK, k=0.1, layer='hand')
        for k in range(3):   # 손가락: 카메라 앞면을 감쌈
            fig.add(S.capsule(tuple(hand + np.array((-s * 0.05, 0.12 - k * 0.12, 0.12))), tuple(hand + np.array((-s * 0.22, 0.12 - k * 0.12, 0.22))), 0.06, 0.055), DARK, k=0.05, layer='hand')
    # ---- 꼬리: 등 뒤로 길게, 끝은 짙은 갈색 ----
    tp = [np.array(p) for p in ((-0.1, y0 + 1.5, -0.6), (-0.4, y0 + 0.65, -1.35), (-1.0, y0 + 0.45, -2.05), (-1.7, y0 + 0.8, -2.45), (-2.15, y0 + 1.35, -2.55))]
    tr = (0.36, 0.3, 0.27, 0.22, 0.12)
    for (a, b), (r0, r1) in zip(zip(tp, tp[1:]), zip(tr, tr[1:])):
        fig.add(S.capsule(tuple(a), tuple(b), r0, r1), SAND, k=0.25)
    fig.paint(S.sphere(tuple(tp[-1]), 0.7), DARK, soft=0.2)
    # ---- 머리: 좁고 긴 두상 (세로 타원) + 앞으로 가늘어지는 주둥이, 따로 붙인 볼 없음 ----
    hc = (0, y0 + 6.45, 0.1)
    hr = np.array((1.42, 1.72, 1.5))
    head_f = S.ellipsoid(hc, tuple(hr))
    fig.add(head_f, SAND, k=0.3)
    muz_a, muz_b = np.array((0, hc[1] - 0.55, hc[2] + 0.75)), np.array((0, hc[1] - 0.85, hc[2] + 1.6))
    fig.add(S.capsule(tuple(muz_a), tuple(muz_b), 0.75, 0.33), SAND, k=0.45)
    fig.paint(S.ellipsoid((0, hc[1] - 1.05, hc[2] + 1.0), (1.3, 0.85, 1.2)), CREAM, soft=0.3)
    fig.paint(S.ellipsoid((0, hc[1] - 0.2, hc[2] + 1.7), (0.3, 0.85, 0.5)), CREAM, soft=0.3)
    # 얼굴 부품 자리
    eye_f = {s: head_frame(fig, hc, s * 26, 3) for s in (-1, 1)}
    brow_f = {s: head_frame(fig, hc, s * 27, 18, out=0.02) for s in (-1, 1)}
    blush_p = {s: np.array(head_frame(fig, hc, s * 40, -30).o) for s in (-1, 1)}
    nose_f = head_frame(fig, hc, 0, -24)
    mouth_f = head_frame(fig, hc, 0, -33)
    # 눈 무늬: 눈 둘레 짙은 갈색 물방울 (바깥 아래로 처짐)
    for s in (-1, 1):
        p = np.array(eye_f[s].o)
        fig.paint(S.ellipsoid(tuple(p + np.array((s * 0.06, -0.08, 0))), (0.66, 0.6, 0.6), R=S.rot(s * 26, 0, s * 25)), PATCH, soft=0.04)
    # 귀: 옆에 작은 반달 (짙은 색)
    for s in (-1, 1):
        ec = (s * 1.42, hc[1] + 0.05, hc[2] - 0.2)
        fig.add(S.subtract(S.ellipsoid(ec, (0.26, 0.45, 0.4), R=S.rot(s * 30, 0, 0)), S.ellipsoid((s * 1.58, hc[1] + 0.05, hc[2] - 0.04), (0.12, 0.3, 0.27), R=S.rot(s * 30, 0, 0)), k=0.05), SAND_DK, k=0.15)
        fig.paint(S.ellipsoid((s * 1.55, hc[1] + 0.05, hc[2] - 0.1), (0.22, 0.4, 0.34)), DARK, soft=0.05)
    # ---- 비니: 골지 크림 + 진홍 줄 + 접은 단 + 방울 ----
    bc = np.array((0, hc[1] + 0.55, hc[2] - 0.1))
    Rb = S.rot(0, -12, 0)
    qy = lambda P: ((P - bc) @ Rb)[:, 1]

    def rib(P, n=44, amp=0.022):
        q = (P - bc) @ Rb
        return amp * np.cos(np.arctan2(q[:, 0], q[:, 2]) * n)
    dome0 = S.ellipsoid(tuple(np.array(hc) + (0, 0.1, -0.05)), tuple(hr + (0.13, 0.3, 0.13)))
    fig.add(lambda P: rmax(dome0(P) + rib(P), 0.5 - qy(P), 0.05), KNIT, k=0.0, layer='beanie')
    cuff0 = S.ellipsoid(hc, tuple(hr + 0.26))
    fig.add(lambda P: rmax(cuff0(P) + rib(P, 44, 0.03), np.abs(qy(P) - 0.12) - 0.36, 0.1), KNIT, k=0.0, layer='cuff')
    band0 = S.ellipsoid(hc, tuple(hr + 0.33))
    fig.add(lambda P: rmax(band0(P), np.abs(qy(P) - 0.6) - 0.15, 0.06), STRAP, k=0.0, layer='band')
    top = bc + Rb @ np.array((0, hr[1] + 0.4 - 0.55 + 0.1, 0)) + np.array((0, 0, -0.05))
    fig.add(lambda P: S.sphere(tuple(top + Rb @ np.array((0, 0.32, 0))), 0.4)(P) + 0.02 * np.sin(P[:, 0] * 26) * np.sin(P[:, 1] * 24) * np.sin(P[:, 2] * 25), KNIT, k=0.0, layer='pom')
    # ---- 받침 소품: 미니 삼각대 (오른쪽 앞) + 돌 ----
    tx, tz = 2.75, 1.25
    hub = np.array((tx, y0 + 1.2, tz))
    for k in range(3):
        a = math.radians(k * 120 + 20)
        foot = np.array((tx + math.cos(a) * 0.75, y0 + 0.06, tz + math.sin(a) * 0.75))
        fig.add(S.capsule(tuple(hub), tuple(foot), 0.09, 0.06), TRI, k=0.04, layer='tri')
    fig.add(S.cylinder(tuple(hub + (0, 0.1, 0)), 0.16, 0.12, round_=0.04), TRI, k=0.04, layer='tri')
    fig.add(S.capsule(tuple(hub), tuple(hub + (0, 0.55, 0)), 0.08), CAM_SIL, k=0.0, layer='tri2', metal=sil)
    fig.add(S.sphere(tuple(hub + (0, 0.7, 0)), 0.16), TRI, k=0.0, layer='tri3')
    fig.add(S.cylinder(tuple(hub + (0, 0.88, 0)), 0.24, 0.05, round_=0.02), TRI, k=0.03, layer='tri3')
    fig.add(S.torus(tuple(hub + (0, 0.2, 0)), 0.17, 0.035), STRAP, k=0.0, layer='tri4')
    rock = lambda P: S.ellipsoid((-2.8, y0 + 0.25, 1.5), (0.7, 0.45, 0.55))(P) + 0.03 * np.sin(P[:, 0] * 9) * np.sin(P[:, 2] * 8)
    fig.add(rock, (0.70, 0.69, 0.68), k=0.0, layer='rock')
    # ---- 얼굴: 짙은 눈 무늬 안의 작은 둥근 눈, 코, 'w' 입, 눈썹, 볼터치는 칠만 ----
    for s in (-1, 1):
        Fg.eye_at(fig, eye_f[s], 'round', size=0.3, side=s)
        for k in range(9):
            t = k / 8 - 0.5
            Fg._ellipsoid(fig.extra, (t * 0.46, -abs(t) * 0.12 - s * t * 0.08, 0.0), (0.065, 0.045, 0.045), PATCH, 6, 4, brow_f[s])
        fig.paint(S.sphere(blush_p[s], 0.3), Fg.BLUSH, soft=0.35)
    Fg._ellipsoid(fig.extra, (0, 0, 0.02), (0.2, 0.13, 0.13), (0.1, 0.07, 0.07), 12, 6, nose_f)
    Fg._ellipsoid(fig.extra, (-0.06, 0.05, 0.11), (0.06, 0.03, 0.03), (0.6, 0.6, 0.6), 6, 4, nose_f)
    w_mouth(fig, mouth_f, 0.2, (0.28, 0.14, 0.1))
