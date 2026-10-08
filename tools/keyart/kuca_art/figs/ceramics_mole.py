"""도예관 두더지: 벨벳 회색 큰 머리, 분홍 별 코, 웃는 감은 눈, 흙 묻은 리넨 앞치마(빨간 박음질·주머니·뒤 리본),
분홍 손발, 손에 든 비뚤어진 항아리, 받침 위 나무 의자 + 물레, 흙 그릇."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg
from .clock_rooster import Fast, hit, layer_paint, beads, face_frame, surf_beads

TIER = 'gold'

FUR = (0.40, 0.39, 0.41)
FUR_LT = (0.56, 0.54, 0.55)
PAW = (1.0, 0.72, 0.74)
STAR = (1.0, 0.58, 0.64)
LINEN = (0.92, 0.86, 0.73)
STITCH = (0.80, 0.16, 0.18)
CLAY = (0.66, 0.47, 0.30)
POT = (0.80, 0.58, 0.38)
WOOD = (0.62, 0.42, 0.25)
WOOD_DK = (0.50, 0.32, 0.18)
STEEL = (0.68, 0.70, 0.73)
G = (Fg.GOLD, Fg.GOLD_HI)


def star_sdf(c, R, r_in, th, Rm):
    """앞(로컬 +Z)을 보는 5각 별 판 (둥근 모서리), 반두께 th"""
    c = np.asarray(c, np.float64)

    def f(P):
        q = (P - c) @ Rm
        x, y = q[:, 0], q[:, 1]
        a = np.arctan2(x, y)                       # 위쪽 꼭짓점부터
        seg = (2 * math.pi / 5)
        a = np.mod(a + seg / 2, seg) - seg / 2
        rr = np.hypot(x, y)
        # 꼭짓점(각 0, R) 과 안쪽 골(각 ±seg/2, r_in) 사이 선분까지의 2D 거리
        px, py = rr * np.sin(np.abs(a)), rr * np.cos(a)
        ax, ay = 0.0, R
        bx, by = r_in * math.sin(seg / 2), r_in * math.cos(seg / 2)
        ex, ey = bx - ax, by - ay
        h = np.clip(((px - ax) * ex + (py - ay) * ey) / (ex * ex + ey * ey), 0, 1)
        dx, dy = px - ax - h * ex, py - ay - h * ey
        d2 = np.hypot(dx, dy)
        inside = (dx * ey - dy * ex) < 0
        d2 = np.where(inside, d2, -d2)
        dz = np.abs(q[:, 2]) - th
        w = np.stack([d2, dz], 1)
        return np.linalg.norm(np.maximum(w, 0), axis=1) + np.minimum(w.max(axis=1), 0)
    return f


def build(fig, rng):
    fig = Fast(fig)
    Fg.base(fig, rng, flowers=7)
    y0 = Fg.TOP

    # ---------- 발 (분홍, 발가락) ----------
    for s in (-1, 1):
        x = s * 0.75
        fig.add(S.ellipsoid((x, y0 + 0.25, 0.55), (0.55, 0.27, 0.7)), PAW, k=0.15, layer='feet')
        for j in range(4):
            a = math.radians(-36 + 24 * j)
            fig.add(S.sphere((x + math.sin(a) * 0.45, y0 + 0.17, 0.55 + math.cos(a) * 0.62), 0.17), PAW, k=0.1, layer='feet')
        fig.add(S.capsule((x, y0 + 0.3, 0.2), (x, y0 + 1.1, 0.0), 0.42), FUR, k=0.25)

    # ---------- 몸: 목 없이 이어진 총알형 머리 + 아래가 넓은 서양배형 몸 (한 덩어리) ----------
    torso = [((0, y0 + 1.7, 0.05), (1.72, 1.15, 1.42)), ((0, y0 + 2.85, 0.0), (1.5, 1.25, 1.3))]
    hc, hr = (0, y0 + 4.55, 0.05), (1.45, 1.75, 1.38)
    for c, r in torso:
        fig.add(S.ellipsoid(c, r), FUR, k=0.6)
    fig.add(S.ellipsoid(hc, hr), FUR, k=0.72)
    fig.add(S.ellipsoid((0, y0 + 1.25, -1.35), (0.3, 0.28, 0.28)), FUR_LT, k=0.15)       # 꼬리

    def torso_f(P):
        d = S.smin(S.ellipsoid(*torso[0])(P), S.ellipsoid(*torso[1])(P), 0.6)
        return S.smin(d, S.ellipsoid(hc, hr)(P), 0.72)

    # 긴 주둥이 (앞으로 뻗어 끝이 살짝 올라감)
    fig.add(S.capsule((0, y0 + 4.32, 0.95), (0, y0 + 4.24, 2.05), 0.56, 0.3), FUR_LT, k=0.4)

    # ---------- 팔 + 분홍 손 ----------
    # 왼팔: 옆으로 벌림 (화면 왼쪽)
    shL, hdL = (-1.4, y0 + 3.1, 0.2), (-2.35, y0 + 2.6, 0.55)
    fig.add(S.capsule(shL, hdL, 0.48, 0.38), FUR, k=0.3)
    fig.add(S.ellipsoid(hdL, (0.36, 0.4, 0.34)), PAW, k=0.12, layer='paw')
    for j in range(4):
        a = math.radians(-40 + 27 * j)
        fig.add(S.capsule(hdL, (hdL[0] - 0.42, hdL[1] + math.sin(a) * 0.4, hdL[2] + 0.15 + math.cos(a) * 0.1), 0.14, 0.12), PAW, k=0.08, layer='paw')
    # 오른팔: 항아리를 감싸 듦
    shR, hdR = (1.4, y0 + 3.1, 0.25), (1.8, y0 + 2.55, 1.35)
    fig.add(S.capsule(shR, hdR, 0.48, 0.4), FUR, k=0.3)
    fig.add(S.ellipsoid(hdR, (0.32, 0.3, 0.32)), PAW, k=0.12, layer='paw')

    # ---------- 항아리 (비뚤어진 토기) ----------
    pc = np.array((1.65, y0 + 3.1, 1.78))
    Rpot = S.rot(-20, 12, -14)
    body = S.ellipsoid(tuple(pc), (0.55, 0.5, 0.52), R=Rpot)
    neck = S.capsule(tuple(pc + Rpot @ np.array((0, 0.3, 0))), tuple(pc + Rpot @ np.array((0.05, 0.62, 0))), 0.34, 0.38)
    potf = lambda P: S.smin(S.smin(body(P), neck(P), 0.15),
                            S.torus(tuple(pc + Rpot @ np.array((0.05, 0.62, 0))), 0.36, 0.07, Rm=Rpot)(P), 0.06)
    potf2 = S.subtract(potf, S.capsule(tuple(pc + Rpot @ np.array((0.04, 0.2, 0))), tuple(pc + Rpot @ np.array((0.06, 1.2, 0))), 0.28))
    fig.add(potf2, POT, k=0.0, layer='pot')
    layer_paint(fig, 'pot', S.sphere(tuple(pc + (0.1, -0.25, 0.45)), 0.3), CLAY, 0.2)
    layer_paint(fig, 'pot', S.sphere(tuple(pc + (-0.35, 0.25, 0.35)), 0.2), (0.88, 0.72, 0.55), 0.15)
    # 손가락: 항아리 아래·옆을 감싼다
    for j in range(4):
        p = pc + np.array((-0.45 + j * 0.06, -0.3 + j * 0.12, -0.25 + j * 0.05))
        fig.add(S.sphere(tuple(p), 0.15), PAW, k=0.08, layer='paw')
    fig.add(S.capsule(hdR, tuple(pc + (-0.42, -0.2, -0.2)), 0.22), PAW, k=0.12, layer='paw')
    # 왼손 대신 몸 쪽 손가락 하나 더 (항아리 오른쪽 아래 받침)
    fig.add(S.capsule(tuple(pc + (0.2, -0.55, 0.0)), tuple(pc + (0.45, -0.4, 0.05)), 0.13), PAW, k=0.08, layer='paw')

    # ---------- 앞치마 (몸 따라 감싼 껍질) ----------
    def apron(P):
        d = np.abs(torso_f(P) - 0.11) - 0.055
        y = P[:, 1]
        bib_w = np.where(y > y0 + 2.75, 0.9, 1.95)
        d = np.maximum(d, np.abs(P[:, 0]) - bib_w)
        d = np.maximum(d, -(P[:, 2] + 0.15))
        d = np.maximum(d, y - (y0 + 3.5))
        d = np.maximum(d, (y0 + 0.75) - y)
        d = np.maximum(d, -(np.minimum(S.capsule(shL, hdL, 0.48, 0.38)(P), S.capsule(shR, hdR, 0.48, 0.4)(P)) - 0.16))   # 팔 자리는 비움
        return d
    fig.add(apron, LINEN, k=0.0, layer='apron')

    def pocket(P):
        d = np.abs(torso_f(P) - 0.2) - 0.05
        d = np.maximum(d, np.abs(P[:, 0]) - 0.72)
        d = np.maximum(d, np.abs(P[:, 1] - (y0 + 1.75)) - 0.45)
        d = np.maximum(d, -(P[:, 2] - 0.3))
        return d
    fig.add(pocket, LINEN, k=0.0, layer='pocket')
    # 흙 얼룩
    for (x, y, r) in ((-0.95, y0 + 1.25, 0.3), (1.05, y0 + 1.55, 0.26), (0.55, y0 + 2.45, 0.2), (-0.55, y0 + 3.15, 0.15), (1.25, y0 + 1.0, 0.18),
                      (-0.2, y0 + 1.3, 0.14), (0.35, y0 + 2.1, 0.17)):
        q = hit(fig, (x, y, 4.5), (0, 0, -1))
        lay = 'pocket' if abs(x) < 0.72 and abs(y - y0 - 1.75) < 0.45 else 'apron'
        layer_paint(fig, lay, lambda P, q=q, r=r: S.sphere(q, r)(P) + 0.06 * np.sin(P[:, 0] * 23) * np.sin(P[:, 1] * 19), CLAY, 0.08)

    # 빨간 박음질 (점선)
    def stitch(pts2d, front=True, on=None):
        pts = []
        for x, y in pts2d:
            q = hit(fig, (x, y, 4.5 if front else -4.5), (0, 0, -1 if front else 1))
            if on is not None and abs(float(on(q[None])[0])) > 0.03:
                continue
            pts.append(q + (0, 0, 0.01))
        for a, b in zip(pts, pts[1:]):
            n = max(1, int(np.linalg.norm(b - a) / 0.16))
            for k in range(n):
                p = a + (b - a) * (k + 0.25) / n
                p2 = a + (b - a) * (k + 0.75) / n
                for t in range(4):
                    Fg._ellipsoid(fig.extra, tuple(p + (p2 - p) * t / 3), (0.03, 0.03, 0.03), STITCH, 5, 3)

    def poly(*xy, n=8):
        out = []
        for (x0, y0_), (x1, y1) in zip(xy, xy[1:]):
            for k in range(n):
                out.append((x0 + (x1 - x0) * k / n, y0_ + (y1 - y0_) * k / n))
        out.append(xy[-1])
        return out
    stitch(poly((-0.76, y0 + 2.8), (-0.76, y0 + 3.36), (0.76, y0 + 3.36), (0.76, y0 + 2.8)), on=apron)
    stitch(poly((-0.6, y0 + 2.08), (-0.6, y0 + 1.42), (0.6, y0 + 1.42), (0.6, y0 + 2.08), (-0.6, y0 + 2.08)), on=pocket)
    stitch(poly((-1.35, y0 + 0.92), (1.35, y0 + 0.92), n=16), on=apron)

    # 어깨끈 + 금색 단추: 가슴판 위 모서리에서 옆구리(팔 위)를 돌아 등 가운데 리본으로
    for s in (-1, 1):
        b0 = hit(fig, (s * 0.76, y0 + 3.4, 4.5), (0, 0, -1))
        fig.add(S.sphere(tuple(b0 + (0, 0, 0.06)), 0.14), Fg.GOLD, k=0.0, layer='button', metal=G)
        pts = [b0 + (0, 0, -0.02)]
        for k in range(1, 13):
            t = k / 12
            th = math.radians(s * (28 + 152 * t))
            y = y0 + 3.42 + 0.95 * math.sin(math.pi * t) ** 0.6 - 0.75 * t
            q = hit(fig, (math.sin(th) * 4.5, y, math.cos(th) * 4.5), (-math.sin(th), 0, -math.cos(th)), field=lambda P: torso_f(P) - 0.12)
            pts.append(q)
        for a, b in zip(pts, pts[1:]):
            fig.add(S.capsule(tuple(a), tuple(b), 0.09), LINEN, k=0.04, layer='strap')
    bw = hit(fig, (0, y0 + 2.95, -4.5), (0, 0, 1), field=lambda P: torso_f(P) - 0.12) + (0, 0, -0.14)
    fig.add(S.ellipsoid(tuple(bw), (0.22, 0.2, 0.15)), LINEN, k=0.05, layer='bowknot')
    for s in (-1, 1):
        loop = S.subtract(S.ellipsoid(tuple(bw + (s * 0.5, 0.08, 0.02)), (0.5, 0.28, 0.13), R=S.rot(0, 0, s * 14)),
                          S.ellipsoid(tuple(bw + (s * 0.52, 0.08, -0.12)), (0.3, 0.12, 0.1), R=S.rot(0, 0, s * 14)), k=0.04)
        fig.add(loop, LINEN, k=0.08, layer='bow')
        fig.add(S.capsule(tuple(bw + (s * 0.1, -0.12, 0)), tuple(bw + (s * 0.45, -0.85, 0.05)), 0.13, 0.1), LINEN, k=0.06, layer='bow')
    # 허리 끈 (뒤로 이어짐)
    for s in (-1, 1):
        pts = []
        for k in range(7):
            ang = math.radians(s * (75 + 105 * k / 6))
            y = y0 + 2.85
            q = hit(fig, (math.sin(ang) * 4.5, y, math.cos(ang) * 4.5), (-math.sin(ang), 0, -math.cos(ang)), field=lambda P: torso_f(P) - 0.12)
            pts.append(q)
        for a, b in zip(pts, pts[1:]):
            fig.add(S.capsule(tuple(a), tuple(b), 0.09), LINEN, k=0.03, layer='strap')

    # ---------- 얼굴 ----------
    hcv = np.asarray(hc)
    # 분홍 별 코 (긴 주둥이 끝)
    sn = hit(fig, (0, y0 + 4.25, 4.5), (0, 0, -1))
    Rs = S.rot(0, -8, 0)
    fig.add(star_sdf(tuple(sn + (0, 0, 0.06)), 0.4, 0.19, 0.1, Rs), STAR, k=0.0, layer='star')
    fig.add(S.sphere(tuple(sn + (0, 0, 0.02)), 0.19), STAR, k=0.0, layer='star')
    # 웃으며 감은 눈 (∩ 실눈): 머리 표면 위에
    for s in (-1, 1):
        fe = face_frame(fig, hcv, s * 26, 13)
        surf_beads(fig, fe, [(math.cos(math.pi * k / 10) * 0.27, math.sin(math.pi * k / 10) * 0.15 - 0.05) for k in range(11)],
                   0.06, (0.10, 0.08, 0.08), out=0.0)
    # 수염 (밝은 회색, 주둥이 옆에서)
    for s in (-1, 1):
        for j, dy in enumerate((0.1, -0.04, -0.18)):
            a = hit(fig, (s * 4.5, y0 + 4.25 + dy, 1.45), (-s, 0, 0))
            b = a + np.array((s * 0.9, dy * 0.9 + 0.05 - j * 0.03, 0.1 - j * 0.06))
            beads(fig, [a + (s * 0.01, 0, 0), b], 0.022, (0.95, 0.94, 0.92))
    # 입 (주둥이 아래 작은 ㅅ)
    mpts = [hit(fig, (x, y0 + 3.98 - 0.06 * (1 - (x / 0.16) ** 2), 4.5), (0, 0, -1)) for x in np.linspace(-0.16, 0.16, 7)]
    beads(fig, mpts, 0.035, (0.25, 0.14, 0.14))
    for s in (-1, 1):   # 볼터치 (칠만)
        q = np.asarray(face_frame(fig, hcv, s * 42, -6).o)
        layer_paint(fig, 'body', S.sphere(q, 0.4), (0.80, 0.52, 0.56), 0.4, tol=0.1)
    layer_paint(fig, 'body', S.ellipsoid((0, y0 + 4.15, 1.35), (0.85, 0.62, 0.8)), FUR_LT, 0.25, tol=0.1)

    # ---------- 받침: 나무 의자 + 물레, 흙 그릇 ----------
    sc = np.array((2.65, y0, 1.75))
    for a in (45, 135, 225, 315):
        r = math.radians(a)
        top = sc + (math.cos(r) * 0.48, 1.0, math.sin(r) * 0.48)
        bot = sc + (math.cos(r) * 0.66, 0.0, math.sin(r) * 0.66)
        fig.add(S.capsule(tuple(bot), tuple(top), 0.13, 0.11), WOOD, k=0.05, layer='stool')
    fig.add(S.torus(tuple(sc + (0, 0.45, 0)), 0.57, 0.07), WOOD_DK, k=0.05, layer='stool')
    fig.add(S.cylinder(tuple(sc + (0, 1.05, 0)), 0.8, 0.13, round_=0.06), WOOD, k=0.05, layer='stool')
    fig.add(S.cylinder(tuple(sc + (0, 1.27, 0)), 0.32, 0.1, round_=0.03), STEEL, k=0.0, layer='wheel', metal=(STEEL, (1, 1, 1)))
    fig.add(S.cylinder(tuple(sc + (0, 1.42, 0)), 0.72, 0.08, round_=0.04), STEEL, k=0.0, layer='wheel', metal=(STEEL, (1, 1, 1)))
    for r in (0.22, 0.4, 0.58):
        fig.add(S.torus(tuple(sc + (0, 1.5, 0)), r, 0.022), (0.5, 0.52, 0.55), k=0.0, layer='groove')
    layer_paint(fig, 'stool', S.sphere(tuple(sc + (0.3, 1.1, 0.5)), 0.3), CLAY, 0.2)
    # 흙 그릇 + 막대
    bc = np.array((-2.75, y0 + 0.35, 1.9))
    bowl = S.subtract(S.ellipsoid(tuple(bc), (0.7, 0.42, 0.7)), S.ellipsoid(tuple(bc + (0, 0.3, 0)), (0.58, 0.42, 0.58)))
    bowl = S.intersect(bowl, lambda P: P[:, 1] - (bc[1] + 0.3))
    fig.add(bowl, (0.93, 0.88, 0.80), k=0.0, layer='bowl')
    fig.add(S.ellipsoid(tuple(bc + (0, 0.12, 0)), (0.56, 0.12, 0.56)), CLAY, k=0.0, layer='clay')
    fig.add(S.capsule(tuple(bc + (0.1, 0.15, 0)), tuple(bc + (0.55, 0.9, -0.1)), 0.06), WOOD, k=0.0, layer='stick')
    layer_paint(fig, 'bowl', S.sphere(tuple(bc + (0.3, 0.0, 0.5)), 0.25), CLAY, 0.15)
