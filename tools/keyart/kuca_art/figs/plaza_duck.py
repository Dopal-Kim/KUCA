"""광장 호수 오리 (금 전설): 하얀 아기 오리, 주황 넓적 부리, 노란 비옷 모자 (흰 컬 꼭지 + 빨간 단추),
노란 튜브 (오리 얼굴), 주황 물갈퀴. 받침: 연잎·흰 수련이 뜬 물웅덩이."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

WHITE = (0.985, 0.98, 0.97)
BILL = (1.0, 0.60, 0.15)
BILL_DK = (0.93, 0.47, 0.10)
HAT = (1.0, 0.86, 0.32)
HAT_DK = (0.93, 0.72, 0.18)
RING = (1.0, 0.84, 0.26)
BTN = (0.80, 0.12, 0.18)
WATER = (0.50, 0.76, 0.97)
PAD = (0.30, 0.62, 0.26)
PAD_DK = (0.26, 0.52, 0.22)
CRIMSON = (0.72, 0.10, 0.14)
SILVER = (0.80, 0.82, 0.86)
SIL = (SILVER, (1.0, 1.0, 1.0))
GLD = (Fg.GOLD, Fg.GOLD_HI)


# ---------- 공통 도우미 ----------

_GRID = []


def bounded(f, margin=1.0):
    """빠르게: 성긴 격자로 부품의 경계 상자를 찾아, 상자에서 margin 안쪽 점만 진짜 SDF 를 계산 (밖은 상자까지 거리 = 하한)"""
    if not _GRID:
        xs, ys = np.arange(-4.8, 4.81, 0.2), np.arange(-0.05, 12.61, 0.2)
        X, Y, Z = np.meshgrid(xs, ys, xs, indexing='ij')
        _GRID.append(np.stack([X.ravel(), Y.ravel(), Z.ravel()], axis=1))
    G = _GRID[0]
    m = f(G) < 0.32
    if not m.any():
        return f
    lo, hi = G[m].min(axis=0) - 0.32, G[m].max(axis=0) + 0.32
    c, h = (lo + hi) / 2, (hi - lo) / 2

    def g(P):
        db = np.linalg.norm(np.maximum(np.abs(P - c) - h, 0.0), axis=1)
        sel = db < margin
        if sel.any():
            db[sel] = f(P[sel])
        return db
    return g


def U(fs, k=0.0):
    """여러 SDF 를 하나로 (k>0 이면 부드럽게)"""
    fs = list(fs)
    if len(fs) > 2:
        fs = [bounded(g, 0.6) for g in fs]

    def f(P):
        d = fs[0](P)
        for g in fs[1:]:
            d = S.smin(d, g(P), k) if k > 0 else np.minimum(d, g(P))
        return d
    return f


class Fast:
    """Figure 대리: add 하는 모든 부품을 bounded 로 감싼다"""

    def __init__(self, fig):
        self._fig = fig

    def add(self, f, col, k=0.2, layer='body', metal=None):
        self._fig.add(bounded(f), col, k, layer, metal)

    def __getattr__(self, n):
        return getattr(self._fig, n)


def _dir(yaw, pitch):
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    return np.array((cp * sy, sp, cp * cy))


def hit(f, c, d, rmax=6.0, n=3000):
    """c 에서 d 방향으로 나가며 f 가 처음 0 을 넘는 거리 (표면 찾기)"""
    d = np.asarray(d, np.float64)
    d = d / np.linalg.norm(d)
    ts = np.linspace(0.0, rmax, n)
    v = f(np.asarray(c, np.float64)[None, :] + ts[:, None] * d[None, :])
    i = int(np.argmax(v > 0))
    return ts[i]


def surf(f, c, yaw, pitch, out=0.0):
    d = _dir(yaw, pitch)
    return np.asarray(c, np.float64) + d * (hit(f, c, d) + out)


def frame_at(f, c, yaw, pitch, out=0.0):
    """머리 표면 위 (yaw, pitch) 지점의 Frame (Z = 바깥)"""
    t = hit(f, c, _dir(yaw, pitch))
    return Fg._frame_on(c, t, yaw, pitch, out)


def axes(ydir, zdir):
    """로컬 Y = ydir, Z ≈ zdir 인 회전 행렬 (열 = 로컬 축)"""
    y = np.asarray(ydir, np.float64)
    y = y / np.linalg.norm(y)
    z = np.asarray(zdir, np.float64)
    z = z - y * (z @ y)
    z = z / np.linalg.norm(z)
    x = np.cross(y, z)
    return np.stack([x, y, z], axis=1)


def ribbon(fig, pts, nrm, width, thick, col, layer, k=0.06, metal=None):
    """점 열을 따라 납작한 띠 (목도리 끝, 끈). nrm = 띠 면의 바깥 방향"""
    pts = [np.asarray(p, np.float64) for p in pts]
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        L = np.linalg.norm(b - a)
        n = nrm[i] if isinstance(nrm, list) else nrm
        R = axes(b - a, n)
        w = width[i] if isinstance(width, list) else width
        fig.add(S.box((a + b) / 2, (w, L / 2 + thick * 0.6, thick), round_=thick * 0.95, R=R), col, k=k, layer=layer, metal=metal)


def feather(pivot, yaw, pitch, roll, dist, size):
    """pivot 에서 아래(-Y)로 dist 떨어진 깃털 하나 (yaw·pitch·roll 로 방향), size = (폭, 길이, 두께)"""
    R = S.rot(yaw, pitch, roll)
    c = np.asarray(pivot, np.float64) + R @ np.array((0.0, -dist, 0.0))
    return S.ellipsoid(c, size, R=R)


def eyes(fig, f, c, spread, pitch, size, iris=Fg.IRIS, tall=1.1, sclera=True, out=0.0, iris_hi=None):
    """반짝이는 큰 눈 (표면에 붙임): 흰자 테두리 + 홍채 + 동공 + 반짝임 2개"""
    b = fig.extra
    E = Fg._ellipsoid
    ih = iris_hi or tuple(min(1.0, x * 1.6 + 0.05) for x in iris)
    for s in (-1, 1):
        fr = frame_at(f, c, s * spread, pitch, -size * 0.2 + out)
        if sclera:
            E(b, (s * size * 0.13, -size * 0.07, -size * 0.02), (size * 0.92, size * tall * 0.92, size * 0.33), (0.99, 0.98, 0.97), 22, 10, fr)
        E(b, (0, 0, size * 0.04), (size * 0.86, size * tall * 0.86, size * 0.34), Fg.PUPIL, 22, 10, fr)
        E(b, (0, -size * 0.12, size * 0.09), (size * 0.76, size * tall * 0.74, size * 0.32), iris, 20, 10, fr)
        E(b, (0, -size * 0.38, size * 0.12), (size * 0.5, size * 0.34, size * 0.28), ih, 16, 8, fr)
        E(b, (0, size * 0.05, size * 0.19), (size * 0.42, size * 0.48, size * 0.28), Fg.PUPIL, 16, 8, fr)
        E(b, (-size * 0.3 * s, size * 0.34, size * 0.33), (size * 0.3, size * 0.3, size * 0.12), Fg.SHINE, 12, 6, fr)
        E(b, (size * 0.3 * s, -size * 0.42, size * 0.31), (size * 0.12, size * 0.12, size * 0.06), Fg.SHINE, 8, 4, fr)


def gear(fig, gc, gr, layer='gear'):
    teeth = []
    for k in range(8):
        R = S.rot(k * 45 + 10, 0, 0)
        teeth.append(S.box(tuple(np.array(gc) + R @ np.array((gr * 1.12, 0, 0))), (gr * 0.22, 0.11, gr * 0.2), round_=0.03, R=R))
    fig.add(S.subtract(U([S.cylinder(gc, gr, 0.12, round_=0.04)] + teeth), S.cylinder(gc, gr * 0.38, 0.3)), SILVER, k=0.0, layer=layer, metal=SIL)


def moon_rock(fig, c, r, layer='rock'):
    c = np.asarray(c, np.float64)
    base = lambda P: S.ellipsoid(c, (r, r * 0.82, r))(P) + 0.025 * np.sin(P[:, 0] * 9) * np.sin(P[:, 2] * 8)
    craters = []
    for (yaw, pit, rr) in ((-20, 25, 0.24), (40, 15, 0.2), (5, 55, 0.18), (-60, 5, 0.17), (80, 40, 0.15), (150, 30, 0.2)):
        d = _dir(yaw, pit)
        craters.append((c + d * np.array((r, r * 0.82, r)) * 1.04, rr * r / 0.8))

    def f(P):
        d = base(P)
        for cc, rr in craters:
            d = np.maximum(d, -(S.sphere(cc, rr)(P)))
        return d
    fig.add(f, (0.68, 0.68, 0.70), k=0.0, layer=layer)
    for cc, rr in craters:
        fig.paint(S.sphere(cc, rr * 1.05), (0.55, 0.55, 0.58), soft=0.05)


def bottle_cap(fig, c, R, r=0.2, col=Fg.GOLD, metal=GLD, layer='cap'):
    """병뚜껑: 납작한 원판 + 톱니 테 (R = 회전 행렬, 로컬 Y = 뚜껑 축)"""
    c = np.asarray(c, np.float64)
    fs = [S.cylinder(c, r, 0.035, round_=0.02, R=R)]
    for k in range(14):
        a = 2 * math.pi * k / 14
        fs.append(S.sphere(c + R @ np.array((math.cos(a) * r, -0.03, math.sin(a) * r)), 0.045))
    fig.add(U(fs, 0.03), col, k=0.0, layer=layer, metal=metal)


def flower_brooch(fig, c, n, size=0.17):
    """빨간 꽃 브로치 (꽃잎 5장 + 금색 가운데). n = 바깥 방향"""
    n = np.asarray(n, np.float64)
    n = n / np.linalg.norm(n)
    R = axes((0, 1, 0) - n * n[1], n)
    c = np.asarray(c, np.float64)
    for k in range(5):
        a = math.pi / 2 + 2 * math.pi * k / 5
        p = c + R @ np.array((math.cos(a) * size * 0.75, math.sin(a) * size * 0.75, 0.0))
        fig.add(S.ellipsoid(p, (size * 0.55, size * 0.55, size * 0.25), R=R), CRIMSON, k=0.04, layer='brooch')
    fig.add(S.sphere(c + n * size * 0.15, size * 0.38), Fg.GOLD, k=0.0, layer='brooch_c', metal=GLD)


def fringe(fig, end, tang, n, gap, L, col, layer='fringe'):
    """띠 끝 술 장식 (tang: 띠 끝의 로컬 축, Y = 띠 진행 방향)"""
    fs = []
    for i in range(n):
        u = (i - (n - 1) / 2) * gap
        a = end + tang @ np.array((u, 0.0, 0))
        fs.append(S.capsule(a, a + tang @ np.array((u * 0.3, L, 0.0)), 0.045, 0.04))
    fig.add(U(fs), col, k=0.0, layer=layer)


def tube_loop(fig, core, c, nrm, out, r, col, layer, n=28, k=0.15, start=0.0):
    """core 표면을 따라 c 를 지나는 (nrm 에 수직인) 고리 관 (목도리 등)"""
    nrm = np.asarray(nrm, np.float64)
    nrm = nrm / np.linalg.norm(nrm)
    u = np.cross(nrm, (0, 0, 1.0))
    u /= np.linalg.norm(u)
    v = np.cross(nrm, u)
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n + start
        d = u * math.cos(a) + v * math.sin(a)
        pts.append(np.asarray(c) + d * (hit(core, c, d) + out))
    fig.add(U([S.capsule(pts[i], pts[(i + 1) % n], r) for i in range(n)], k), col, k=k, layer=layer)
    return pts


def clear_grass(fig, inside, ymax=None):
    """받침 잔디 잎·꽃(fig.extra) 중 inside(중심점) 인 삼각형을 지운다 (웅덩이·돌계단 밑)"""
    b = fig.extra
    if not b.pos:
        return
    P = np.asarray(b.pos, np.float64).reshape(-1, 3, 3)
    cen = P.mean(axis=1)
    m = inside(cen) & (cen[:, 1] < (Fg.TOP + 0.9 if ymax is None else ymax))
    keep = ~m
    b.pos = P[keep].ravel().tolist()
    b.col = np.asarray(b.col, np.float64).reshape(-1, 9)[keep].ravel().tolist()
    if b.alpha and len(b.alpha) == len(P) * 3:
        b.alpha = np.asarray(b.alpha, np.float64).reshape(-1, 3)[keep].ravel().tolist()


# ---------- 오리 ----------

def curl(c, R, r0, size=0.38, turns=1.55, n=16):
    """컬 (? 모양 털 뭉치): 로컬 XY 평면에서 아래→위→안쪽으로 말림"""
    fs = []
    pts = []
    for i in range(n):
        t = i / (n - 1)
        th = t * turns * math.pi
        rho = size * (1 - 0.45 * t)
        pts.append(np.asarray(c) + R @ np.array((rho * math.sin(th), -rho * math.cos(th) + size, 0.0)))
    for i in range(n - 1):
        t = i / (n - 1)
        fs.append(S.capsule(pts[i], pts[i + 1], r0 * (1 - 0.5 * t), r0 * (1 - 0.5 * (i + 1) / (n - 1))))
    return U(fs, 0.05)


def build(fig, rng):
    Fg.base(fig, rng, flowers=6)
    y0 = Fg.TOP
    pc = np.array((2.15, y0, 1.95))
    pud = lambda P: S.smin(S.smin(S.cylinder(pc, 1.05, 0.05, round_=0.04)(P), S.cylinder(pc + (0.75, 0, -0.45), 0.6, 0.05, round_=0.04)(P), 0.3),
                           S.cylinder(pc + (-0.55, 0, 0.55), 0.55, 0.05, round_=0.04)(P), 0.3)
    clear_grass(fig, lambda C: pud(np.column_stack([C[:, 0], np.full(len(C), y0), C[:, 2]])) < 0.12)
    fig = Fast(fig)
    hc = np.array((0.0, y0 + 5.45, 0.1))
    head = S.ellipsoid(hc, (2.15, 1.95, 2.0))
    torso = S.ellipsoid((0, y0 + 2.15, 0.0), (1.45, 1.5, 1.4))
    core = lambda P: S.smin(head(P), torso(P), 0.7)
    fig.add(core, WHITE, k=0.3)
    # 모자: 단단한 둥근 머리통 + 아래로 처진 챙 (뒤로 기울임) + 솔기 + 빨간 단추 + 흰 컬 꼭지
    Rh = S.rot(0, -18, 5)
    Hc = hc + np.array((0, 0.72, -0.3))
    crown = lambda P: np.maximum(S.ellipsoid(Hc, (2.3, 1.55, 2.26), R=Rh)(P), -((P - Hc) @ Rh[:, 1]) - 0.55)
    fig.add(crown, HAT, k=0.0, layer='hat')

    def brim(P):
        q = (P - Hc) @ Rh
        r = np.linalg.norm(q[:, [0, 2]], axis=1)
        a, b = np.array((2.0, -0.5)), np.array((3.1, -0.98))
        p2 = np.stack([r, q[:, 1]], axis=1)
        ba = b - a
        h = np.clip(((p2 - a) @ ba) / (ba @ ba), 0, 1)
        return np.linalg.norm(p2 - a - h[:, None] * ba, axis=1) - 0.075
    fig.add(brim, HAT, k=0.12, layer='hat')
    for k in range(4):
        a = math.pi * k / 4 + math.pi / 8
        n = Rh @ np.array((math.cos(a), 0, math.sin(a)))
        fig.paint(lambda P, n=n: np.maximum(np.abs((P - Hc) @ n) - 0.035, np.maximum(((P - Hc) @ Rh[:, 1]) * -1 - 0.5, -crown(P) - 0.15)), HAT_DK, soft=0.03)
    fig.paint(lambda P: np.maximum(np.abs((P - Hc) @ Rh[:, 1] + 0.36) - 0.035, np.abs(crown(P)) - 0.1), HAT_DK, soft=0.04)
    bd = Rh @ _dir(52, 8)
    bpos = Hc + bd * hit(crown, Hc, bd)
    bR = axes(Rh[:, 1] - bd * (Rh[:, 1] @ bd), bd)
    fig.add(S.cylinder(bpos, 0.4, 0.08, round_=0.06, R=axes(bd, (0, 0, 1))), BTN, k=0.0, layer='button')
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        fig.add(S.sphere(bpos + bd * 0.06 + bR @ np.array((math.cos(a) * 0.13, math.sin(a) * 0.13, 0)), 0.06), (0.98, 0.78, 0.72), k=0.0, layer='btn_d')
    top = Hc + Rh @ np.array((0, 1.52, 0))
    fig.add(curl(top - Rh[:, 1] * 0.15, S.rot(25, 0, 0) @ Rh, 0.27, 0.42), WHITE, k=0.0, layer='curl')
    fig.add(curl(surf(head, hc, -15, 5, -0.08), S.rot(-15, 4, 14), 0.14, 0.22), WHITE, k=0.16)
    # 얼굴: 눈·부리·볼
    eyes(fig, head, hc, spread=25, pitch=-13, size=0.56, tall=1.08, sclera=False)
    bf = frame_at(head, hc, 0, -27)
    bp, fwd, up = np.array(bf.p((0, 0, 0.0))), np.array(bf.dir((0, 0, 1))), np.array(bf.dir((0, 1, 0)))
    Rb = axes(up, fwd)
    fig.add(S.ellipsoid(bp + fwd * 0.28 + up * 0.04, (0.66, 0.2, 0.52), R=Rb), BILL, k=0.08, layer='bill')
    fig.add(S.ellipsoid(bp + fwd * 0.2 - up * 0.17, (0.5, 0.13, 0.42), R=Rb), BILL_DK, k=0.06, layer='bill')
    for s in (-1, 1):
        fig.paint(S.sphere(surf(head, hc, s * 40, -24), 0.42), Fg.BLUSH, soft=0.35)
    # 다리·물갈퀴
    for s in (-1, 1):
        fig.add(S.ellipsoid((s * 0.62, y0 + 1.0, 0.1), (0.5, 0.55, 0.5)), WHITE, k=0.3)
        fig.add(S.capsule((s * 0.66, y0 + 0.75, 0.25), (s * 0.7, y0 + 0.28, 0.4), 0.16), BILL, k=0.06, layer='feet')
        fc = np.array((s * 0.74, y0 + 0.16, 0.75))
        R = S.rot(s * 12, 0, 0)
        foot = [S.ellipsoid(fc, (0.55, 0.12, 0.6), R=R)] + [S.sphere(fc + R @ np.array((u, 0.0, 0.48)), 0.17) for u in (-0.36, 0, 0.36)]
        fig.add(U(foot, 0.12), BILL, k=0.06, layer='feet')
    # 꼬리 (뒤로 살짝 올라간 흰 깃)
    fig.add(S.capsule((0, y0 + 2.65, -1.15), (0, y0 + 3.15, -1.75), 0.36, 0.12), WHITE, k=0.3)
    # 튜브: 허리 노란 고리 + 앞쪽 오리 얼굴
    rc = np.array((0, y0 + 2.0, 0.1))
    Rr = S.rot(0, 4, 0)
    fig.add(S.torus(rc, 1.78, 0.56, Rm=Rr), RING, k=0.0, layer='ring')
    fig.paint(S.torus(rc + (0, 0.42, 0), 1.6, 0.22, Rm=Rr), (1.0, 0.94, 0.62), soft=0.25)
    ff = rc + Rr @ np.array((0, 0.05, 1.78 + 0.53))
    for s in (-1, 1):
        fig.add(S.sphere(ff + np.array((s * 0.33, 0.14, -0.03)), 0.11), (0.12, 0.08, 0.06), k=0.0, layer='ringface')
    fig.add(S.ellipsoid(ff + np.array((0, -0.08, 0.04)), (0.27, 0.1, 0.16)), BILL, k=0.0, layer='ringbill')
    # 날개: 튜브 위에 얹은 작은 흰 날개 (깃 끝 3)
    for s in (-1, 1):
        sh = np.array((s * 1.25, y0 + 3.2, 0.2))
        tip = np.array((s * 1.62, y0 + 2.62, 1.15))
        R = axes(tip - sh, (s * 0.8, 0.2, 0.3))
        w = [S.ellipsoid((sh + tip) / 2, (0.32, 0.62, 0.42), R=R)]
        for u in (-0.2, 0.05, 0.28):
            w.append(S.ellipsoid(tip + R @ np.array((u, 0.0, u * 1.2)), (0.14, 0.32, 0.17), R=R))
        fig.add(U(w, 0.12), WHITE, k=0.25)
    # 받침 소품: 물웅덩이 + 연잎 2 + 흰 수련
    fig.add(lambda P: pud(P) - 0.0, WATER, k=0.0, layer='water')
    for (dx, dz, r) in ((-0.5, -0.35, 0.16), (0.55, 0.45, 0.1), (0.9, -0.55, 0.12)):
        fig.paint(S.ellipsoid(pc + (dx, 0.06, dz), (r * 1.8, 0.05, r)), (0.85, 0.95, 1.0), soft=0.05)
    lp = pc + np.array((0.15, 0.09, -0.05))
    padf = S.subtract(S.cylinder(lp, 0.72, 0.03, round_=0.025), S.box(lp + np.array((0.5, 0, 0.5)), (0.07, 0.2, 0.6), R=S.rot(-45, 0, 0)))
    fig.add(padf, PAD, k=0.0, layer='pad')
    for k in range(7):
        a = 2 * math.pi * k / 7
        d = np.array((math.cos(a), 0, math.sin(a)))
        fig.paint(lambda P, d=d: np.maximum(np.linalg.norm(np.cross(P - lp, d), axis=1) - 0.018, np.maximum(-(P - lp) @ d, np.abs(P[:, 1] - lp[1]) - 0.06)), PAD_DK, soft=0.02)
    fig.add(S.cylinder(pc + np.array((-0.6, 0.08, 0.55)), 0.26, 0.02, round_=0.015), PAD, k=0.0, layer='pad')
    fl = lp + np.array((0.0, 0.05, 0.0))
    pet = []
    for k in range(8):
        a = 2 * math.pi * k / 8
        R = S.rot(math.degrees(-a) + 90, -50, 0)
        pet.append(S.ellipsoid(fl + R @ np.array((0, 0.2, 0)), (0.1, 0.24, 0.05), R=R))
    for k in range(6):
        a = 2 * math.pi * (k + 0.5) / 6
        R = S.rot(math.degrees(-a) + 90, -25, 0)
        pet.append(S.ellipsoid(fl + R @ np.array((0, 0.2, 0)), (0.09, 0.22, 0.05), R=R))
    fig.add(U(pet), (0.99, 0.98, 0.96), k=0.0, layer='lotus')
    fig.add(S.sphere(fl + np.array((0, 0.12, 0)), 0.1), (1.0, 0.82, 0.2), k=0.0, layer='lotus_c')
