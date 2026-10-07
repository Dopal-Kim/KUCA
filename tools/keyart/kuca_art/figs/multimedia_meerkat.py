"""멀티미디어관 미어캣: 날씬한 모래색 몸, 짙은 눈 무늬, 진홍 줄 크림 비니, 목에 건 콤팩트 카메라, 받침 위 미니 삼각대"""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

SAND = (0.89, 0.76, 0.56)
SAND_DK = (0.78, 0.62, 0.42)
CREAM = (0.98, 0.93, 0.83)
PATCH = (0.40, 0.25, 0.17)
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


def build(fig, rng):
    Fg.base(fig, rng, flowers=7)
    y0 = Fg.TOP
    g = (Fg.GOLD, Fg.GOLD_HI)
    sil = (CAM_SIL, (1.0, 1.0, 1.0))
    # ---- 다리·발 ----
    for s in (-1, 1):
        x = s * 0.5
        fig.add(S.ellipsoid((x, y0 + 0.22, 0.25), (0.42, 0.25, 0.58)), DARK, k=0.1, layer='feet')
        fig.add(S.capsule((x, y0 + 0.45, 0.05), (x * 1.1, y0 + 1.35, 0.0), 0.36, 0.48), SAND, k=0.25)
    # ---- 몸통: 길쭉하고 날씬, 크림 배 ----
    fig.add(S.ellipsoid((0, y0 + 1.7, 0.0), (0.98, 0.85, 0.82)), SAND, k=0.4)
    fig.add(S.ellipsoid((0, y0 + 2.75, 0.0), (0.88, 1.15, 0.76)), SAND, k=0.5)
    fig.paint(S.ellipsoid((0, y0 + 2.25, 0.7), (0.62, 1.45, 0.45)), CREAM, soft=0.25)
    # ---- 팔: 가늘게 아래로, 짙은 손 ----
    for s in (-1, 1):
        sh = np.array((s * 0.78, y0 + 3.45, 0.05))
        el = np.array((s * 1.18, y0 + 2.75, 0.18))
        wr = np.array((s * 1.5, y0 + 2.15, 0.35))
        fig.add(S.sphere(tuple(sh), 0.36), SAND, k=0.25)
        fig.add(S.capsule(tuple(sh), tuple(el), 0.3, 0.27), SAND, k=0.2)
        fig.add(S.capsule(tuple(el), tuple(wr), 0.27, 0.25), SAND, k=0.15)
        hand = wr + (wr - el) / np.linalg.norm(wr - el) * 0.2
        fig.add(S.ellipsoid(tuple(hand), (0.27, 0.3, 0.24)), DARK, k=0.12, layer='hand')
        for k in range(3):
            fig.add(S.capsule(tuple(hand + np.array(((k - 1) * 0.11, -0.12, 0.08))), tuple(hand + np.array(((k - 1) * 0.13, -0.38, 0.12))), 0.075, 0.06), DARK, k=0.06, layer='hand')
    # ---- 꼬리: 등 뒤로 길게, 끝은 짙은 갈색 ----
    tp = [np.array(p) for p in ((-0.1, y0 + 1.3, -0.7), (-0.5, y0 + 0.6, -1.5), (-1.2, y0 + 0.5, -2.2), (-1.9, y0 + 0.9, -2.55), (-2.3, y0 + 1.45, -2.6))]
    tr = (0.4, 0.34, 0.3, 0.25, 0.14)
    for (a, b), (r0, r1) in zip(zip(tp, tp[1:]), zip(tr, tr[1:])):
        fig.add(S.capsule(tuple(a), tuple(b), r0, r1), SAND, k=0.25)
    fig.paint(S.sphere(tuple(tp[-1]), 0.75), DARK, soft=0.2)
    # ---- 머리 ----
    hc = (0, y0 + 5.55, 0.1)
    HR = 1.95
    head_f = S.ellipsoid(hc, (HR * 1.06, HR * 0.97, HR * 0.97))
    fig.add(head_f, SAND, k=0.5)
    fig.add(S.capsule((0, y0 + 3.6, 0), (0, hc[1] - 1.4, 0.05), 0.6), SAND, k=0.35)
    for s in (-1, 1):
        fig.add(S.sphere((s * 0.95, hc[1] - 0.85, hc[2] + 0.85), 0.7), SAND, k=0.55)
    muz_c = (0, hc[1] - 0.72, hc[2] + 1.55)
    fig.add(S.ellipsoid(muz_c, (0.7, 0.52, 0.62)), SAND, k=0.45)
    fig.paint(S.ellipsoid((0, hc[1] - 1.0, hc[2] + 1.0), (1.55, 0.95, 1.2)), CREAM, soft=0.3)
    fig.paint(S.ellipsoid((0, hc[1] - 0.2, hc[2] + 1.9), (0.35, 0.9, 0.5)), CREAM, soft=0.3)
    # 눈 무늬: 눈 둘레 짙은 갈색 물방울 (바깥 아래로 처짐)
    for s in (-1, 1):
        p = np.array(Fg._frame_on(hc, HR * 0.97, s * 26, -6, 0).p((0, 0, 0)))
        fig.paint(S.ellipsoid(tuple(p + np.array((s * 0.1, -0.1, 0))), (0.72, 0.6, 0.6), R=S.rot(s * 26, 0, s * 25)), PATCH, soft=0.04)
    # 귀: 옆에 작은 반달 (짙은 색)
    for s in (-1, 1):
        ec = (s * 2.02, hc[1] + 0.05, hc[2] - 0.2)
        fig.add(S.subtract(S.ellipsoid(ec, (0.3, 0.5, 0.45), R=S.rot(s * 30, 0, 0)), S.ellipsoid((s * 2.2, hc[1] + 0.05, hc[2] - 0.02), (0.14, 0.33, 0.3), R=S.rot(s * 30, 0, 0)), k=0.05), SAND_DK, k=0.15)
        fig.paint(S.ellipsoid((s * 2.15, hc[1] + 0.05, hc[2] - 0.1), (0.25, 0.45, 0.38)), DARK, soft=0.05)
    # ---- 비니: 골지 크림 + 진홍 줄 + 접은 단 + 방울 ----
    bc = np.array((0, hc[1] + 0.55, hc[2] - 0.1))
    Rb = S.rot(0, -12, 0)
    qy = lambda P: ((P - bc) @ Rb)[:, 1]
    hr = np.array((HR * 1.06, HR * 0.97, HR * 0.97))

    def rib(P, n=44, amp=0.022):
        q = (P - bc) @ Rb
        return amp * np.cos(np.arctan2(q[:, 0], q[:, 2]) * n)
    dome0 = S.ellipsoid(tuple(np.array(hc) + (0, 0.1, -0.05)), tuple(hr + (0.13, 0.3, 0.13)))
    fig.add(lambda P: rmax(dome0(P) + rib(P), 0.5 - qy(P), 0.05), KNIT, k=0.0, layer='beanie')
    cuff0 = S.ellipsoid(hc, tuple(hr + 0.26))
    fig.add(lambda P: rmax(cuff0(P) + rib(P, 44, 0.03), np.abs(qy(P) - 0.12) - 0.36, 0.1), KNIT, k=0.0, layer='cuff')
    band0 = S.ellipsoid(hc, tuple(hr + 0.33))
    fig.add(lambda P: rmax(band0(P), np.abs(qy(P) - 0.6) - 0.15, 0.06), STRAP, k=0.0, layer='band')
    top = bc + Rb @ np.array((0, HR * 0.97 + 0.4 - 0.55 + 0.1, 0)) + np.array((0, 0, -0.05))
    fig.add(lambda P: S.sphere(tuple(top + Rb @ np.array((0, 0.32, 0))), 0.42)(P) + 0.02 * np.sin(P[:, 0] * 26) * np.sin(P[:, 1] * 24) * np.sin(P[:, 2] * 25), KNIT, k=0.0, layer='pom')
    # ---- 카메라: 가슴 앞 (검정 몸 + 은색 윗판 + 렌즈) + 진홍 끈 ----
    cc = np.array((0, y0 + 2.9, 1.0))
    Rcam = S.rot(0, -6, 0)
    cs = 1.22
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
        pts = [lug, np.array((s * 0.66, y0 + 3.55, 0.7)), np.array((s * 0.55, y0 + 3.95, 0.25)), np.array((s * 0.4, y0 + 4.1, -0.3)), np.array((0, y0 + 4.15, -0.55))]
        for a, b in zip(pts, pts[1:]):
            fig.add(S.capsule(tuple(a), tuple(b), 0.07), STRAP, k=0.03, layer='strap')
        fig.add(S.torus(tuple(lug), 0.07, 0.03, Rm=S.rot(0, 0, 90)), (0.95, 0.75, 0.2), k=0.0, layer='ring', metal=g)
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
    # ---- 얼굴 ----
    Fg.kawaii_eyes(fig, hc, HR * 0.97 + 0.03, spread=26, pitch=-6, size=0.5, tall=1.1)
    nf = Fg._frame_on(muz_c, 0.62, 0, 22, -0.03)
    Fg._ellipsoid(fig.extra, (0, 0, 0), (0.2, 0.13, 0.13), (0.1, 0.07, 0.07), 12, 6, nf)
    Fg._ellipsoid(fig.extra, (-0.06, 0.05, 0.09), (0.06, 0.03, 0.03), (0.6, 0.6, 0.6), 6, 4, nf)
    Fg.smile(fig, muz_c, 0.6, pitch=-28, w=0.2, col=(0.28, 0.14, 0.1))
    for s in (-1, 1):   # 눈썹: 눈 무늬 위 짧은 갈색 호
        f = Fg._frame_on(hc, HR * 0.97 + 0.04, s * 28, 14, -0.03)
        for k in range(9):
            t = k / 8 - 0.5
            Fg._ellipsoid(fig.extra, (t * 0.5, -abs(t) * 0.12 - s * t * 0.08, 0.0), (0.07, 0.05, 0.05), PATCH, 6, 4, f)
    for s in (-1, 1):
        p = np.array(Fg._frame_on(hc, HR * 1.0, s * 42, -36, 0).p((0, 0, 0)))
        fig.paint(S.sphere(p, 0.3), Fg.BLUSH, soft=0.35)
