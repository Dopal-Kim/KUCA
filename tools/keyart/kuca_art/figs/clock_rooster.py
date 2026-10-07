"""시계탑 수탉: 크림 조끼(금색 테) + 빨간 단추 + 금색 회중시계, 빨간 볏·볏살, 갈색 날개깃·꼬리, 받침 위 시계판."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

WHITE = (0.985, 0.975, 0.965)
CREAM = (0.97, 0.88, 0.70)
COMB = (0.90, 0.16, 0.15)
COMB_DK = (0.78, 0.10, 0.11)
BEAK = (1.0, 0.72, 0.14)
FOOT = (1.0, 0.70, 0.16)
RUST = (0.68, 0.28, 0.13)
RUST_DK = (0.55, 0.20, 0.10)
RUST_LT = (0.80, 0.40, 0.20)
BROW = (0.55, 0.32, 0.20)
G = (Fg.GOLD, Fg.GOLD_HI)


def ell_z(c, r, x, y, out=0.0, back=False):
    """타원체 c,r 표면에서 (x,y) 앞(뒤)쪽 z"""
    t = 1.0 - ((x - c[0]) / r[0]) ** 2 - ((y - c[1]) / r[1]) ** 2
    z = r[2] * math.sqrt(max(t, 0.0)) + out
    return c[2] - z if back else c[2] + z


def chain(fig, pts, r, col, layer, k=0.02, metal=None):
    for a, b in zip(pts, pts[1:]):
        fig.add(S.capsule(a, b, r), col, k=k, layer=layer, metal=metal)


# ---------- 빠른 조형: 부품마다 영향 상자를 구해 그 밖은 계산하지 않는다 ----------
_GRID = None


def _grid(h=0.3):
    global _GRID
    if _GRID is None:
        xs, ys = np.arange(-4.8, 4.81, h), np.arange(-0.05, 12.61, h)
        X, Y, Z = np.meshgrid(xs, ys, xs, indexing='ij')
        _GRID = np.stack([X.ravel(), Y.ravel(), Z.ravel()], axis=1)
    return _GRID


def bounded(f, m=0.8, thr=0.45, h=0.3):
    """f 가 음수/표면 근처인 곳의 상자(+m) 안에서만 f 를 계산, 밖은 m (참 거리의 하한)"""
    G = _grid()
    sel = G[f(G) < thr]
    if not len(sel):
        return f
    lo, hi = sel.min(axis=0) - h - m, sel.max(axis=0) + h + m

    def g(P):
        inside = np.all((P >= lo) & (P <= hi), axis=1)
        out = np.full(len(P), m)
        if inside.any():
            out[inside] = f(P[inside])
        return out
    return g


class Fast:
    """Figure 대리: add 할 때 bounded 로 감싼다 (조형 속도 수 배)"""

    def __init__(self, fig):
        self._f = fig

    def add(self, f, col, k=0.2, layer='body', metal=None):
        self._f.add(bounded(f), col, k, layer, metal)

    def __getattr__(self, n):
        return getattr(self._f, n)


def hit(fig, p, d, max_t=8.0, field=None):
    """점 p 에서 방향 d 로 표면까지 (sphere tracing) → 표면 점. field 를 주면 그 SDF 만 본다"""
    p = np.asarray(p, np.float64)
    d = np.asarray(d, np.float64)
    d = d / np.linalg.norm(d)
    fld = field or fig.s.field
    t = 0.0
    for _ in range(200):
        v = float(fld(p[None, :] + t * d[None, :])[0])
        if v < 0.002:
            break
        t += max(v * 0.9, 0.002)
        if t > max_t:
            break
    return p + t * d


def layer_field(fig, layer):
    """한 layer 의 SDF (조형과 같은 부드러운 합)"""
    parts = [(f, k) for f, _, k, l in fig.s.parts if l == layer]

    def g(P):
        out = None
        for f, k in parts:
            d = f(P)
            out = d if out is None else S.smin(out, d, k)
        return out
    return g


def layer_paint(fig, layer, f, col, soft=0.05, tol=0.03):
    """layer 표면에만 칠한다 (다른 부품으로 번지지 않게). 부품을 다 넣은 뒤 부른다"""
    lf = layer_field(fig, layer)
    fig.paint(lambda P: np.maximum(f(P), (np.abs(lf(P)) - tol) * 40), col, soft)


def eye1(fig, c, R, yaw, pitch, size=0.5, tall=1.1, iris=Fg.IRIS, look=0.0, side=1, frame=None):
    """눈 하나 (kawaii_eyes 와 같은 층): 구 c,R 위 (yaw, pitch) 방향, 또는 frame 을 직접 준다"""
    b = fig.extra
    s = side
    f = frame or Fg._frame_on(c, R, yaw, pitch, -size * 0.18)
    hi = Fg.IRIS_HI if iris == Fg.IRIS else tuple(min(1, x * 1.5) for x in iris)
    Fg._ellipsoid(b, (0, 0, 0), (size * 0.9, size * tall, size * 0.34), Fg.PUPIL, 20, 10, f)
    Fg._ellipsoid(b, (0, -size * 0.18, size * 0.06), (size * 0.78, size * tall * 0.78, size * 0.32), iris, 20, 10, f)
    Fg._ellipsoid(b, (0, -size * 0.42, size * 0.1), (size * 0.5, size * 0.38, size * 0.28), hi, 16, 8, f)
    Fg._ellipsoid(b, (0, size * 0.05, size * 0.16), (size * 0.42, size * 0.5, size * 0.28), Fg.PUPIL, 16, 8, f)
    Fg._ellipsoid(b, (-size * 0.3 * s + look, size * 0.36, size * 0.3), (size * 0.3, size * 0.3, size * 0.12), Fg.SHINE, 12, 6, f)
    Fg._ellipsoid(b, (size * 0.32 * s, -size * 0.45, size * 0.3), (size * 0.12, size * 0.12, size * 0.06), Fg.SHINE, 8, 4, f)


def surf_normal(fig, p, e=0.01):
    p = np.asarray(p, np.float64)
    E = np.eye(3) * e
    g = np.array([fig.s.field((p + E[i])[None])[0] - fig.s.field((p - E[i])[None])[0] for i in range(3)])
    return g / np.linalg.norm(g)


def frame_at(p, n):
    """점 p, 앞 방향 n (위쪽은 +Y 에 맞춤) 의 Frame"""
    from ..mesh import Frame
    n = np.asarray(n, np.float64)
    n = n / np.linalg.norm(n)
    x = np.cross((0, 1.0, 0), n)
    x /= np.linalg.norm(x)
    y = np.cross(n, x)
    return Frame(tuple(p), tuple(x), tuple(y), tuple(n))


def eye_on(fig, c, yaw, pitch, size=0.5, tall=1.1, iris=Fg.IRIS, side=1, sink=0.0, mix=0.15):
    """머리 중심 c 에서 (yaw, pitch) 방향으로 실제 표면을 찾아, 표면 법선(과 방사 방향을 mix)으로 눈을 붙인다"""
    yr, pr = math.radians(yaw), math.radians(pitch)
    d = np.array((math.cos(pr) * math.sin(yr), math.sin(pr), math.cos(pr) * math.cos(yr)))
    q = hit(fig, np.asarray(c) + d * 6, -d)
    n = surf_normal(fig, q)
    n = n * mix + d * (1 - mix)
    n /= np.linalg.norm(n)
    f = frame_at(q - n * (size * 0.18 + sink), n)
    eye1(fig, c, 0, yaw, pitch, size=size, tall=tall, iris=iris, side=side, frame=f)
    return q, n


def rmax(a, b, r):
    """둥근 모서리 교집합 (a, b 둘 다 안쪽)"""
    a, b = a + r, b + r
    return np.hypot(np.maximum(a, 0), np.maximum(b, 0)) + np.minimum(np.maximum(a, b), 0) - r


def beads(fig, pts, r, col, seg=6):
    """또렷한 작은 구슬 줄 (입·눈썹·수염)"""
    pts = [np.asarray(p, np.float64) for p in pts]
    for a, b in zip(pts, pts[1:]):
        n = max(1, int(np.linalg.norm(b - a) / (r * 0.8)))
        for k in range(n):
            p = a + (b - a) * k / n
            Fg._ellipsoid(fig.extra, tuple(p), (r, r, r), col, seg, 4)
    Fg._ellipsoid(fig.extra, tuple(pts[-1]), (r, r, r), col, seg, 4)


def build(fig, rng):
    fig = Fast(fig)
    Fg.base(fig, rng, flowers=8)
    y0 = Fg.TOP

    # ---------- 다리·발 (노란 발, 앞 발가락 3 + 뒤 1) ----------
    for s in (-1, 1):
        x = s * 0.72
        fig.add(S.capsule((x, y0 + 0.35, 0.15), (x * 0.95, y0 + 1.25, 0.05), 0.24, 0.3), FOOT, k=0.1, layer='feet')
        fig.add(S.ellipsoid((x, y0 + 0.24, 0.35), (0.42, 0.24, 0.42)), FOOT, k=0.15, layer='feet')
        for a in (-32, 0, 32):
            R = S.rot(a + s * 6, 0, 0)
            tip = np.array((x, y0 + 0.17, 0.35)) + R @ np.array((0, 0, 0.78))
            fig.add(S.capsule((x, y0 + 0.2, 0.35), tuple(tip), 0.2, 0.17), FOOT, k=0.12, layer='feet')
        fig.add(S.capsule((x, y0 + 0.2, 0.2), (x, y0 + 0.17, -0.3), 0.17, 0.14), FOOT, k=0.1, layer='feet')

    # ---------- 몸통 (흰 통통한 배) ----------
    bc, br = (0, y0 + 2.3, 0.0), (1.55, 1.38, 1.35)
    fig.add(S.ellipsoid(bc, br), WHITE, k=0.4)
    fig.add(S.ellipsoid((0, y0 + 1.55, 0.05), (1.25, 0.75, 1.15)), WHITE, k=0.4)
    # 가슴 깃털 (목 앞 보송한 흰 털)
    for x, y, z, r in ((0, y0 + 3.45, 0.95, 0.55), (-0.45, y0 + 3.35, 0.85, 0.42), (0.45, y0 + 3.35, 0.85, 0.42)):
        fig.add(S.sphere((x, y, z), r), WHITE, k=0.3)

    # ---------- 조끼 (크림, 앞 V 트임, 금색 테) ----------
    vc, vr = bc, (br[0] + 0.14, br[1] + 0.12, br[2] + 0.14)
    yv = y0 + 1.85                   # V 트임 꼭짓점

    def vest(P):
        d = S.ellipsoid(vc, vr)(P)
        d = np.maximum(d, -(P[:, 1] - (y0 + 1.22)))                                      # 아래 단
        d = np.maximum(d, P[:, 1] - (y0 + 3.45))                                          # 위
        vopen = (np.abs(P[:, 0]) - (P[:, 1] - yv) * 0.45) / 1.1                         # V 트임
        vopen = np.maximum(vopen, -(P[:, 2] - 0.4))
        d = np.maximum(d, -vopen)
        return d
    fig.add(vest, CREAM, k=0.0, layer='vest')
    # 테: V 가장자리 + 아래 단 (조끼 표면 위 금색 끈)
    for s in (-1, 1):
        pts = []
        for k in range(9):
            y = yv + (y0 + 3.4 - yv) * k / 8
            x = s * (y - yv) * 0.45
            pts.append((x, y, ell_z(vc, vr, x, y, 0.0)))
        chain(fig, pts, 0.075, Fg.GOLD, 'trim', metal=G)
    # 아래 단 테: 둘레 고리 (조끼 아래 가장자리 전체)
    fig.add(S.intersect(S.onion(S.ellipsoid(vc, vr), 0.06), lambda P: np.abs(P[:, 1] - (y0 + 1.29)) - 0.07), Fg.GOLD, k=0.0, layer='trim', metal=G)
    # 빨간 보석 단추 (왼쪽) + 뒤 단추
    bx, by = -0.42, y0 + 2.1
    fig.add(S.sphere((bx, by, ell_z(vc, vr, bx, by, 0.02)), 0.15), Fg.CRIMSON, k=0.0, layer='gem')
    fig.add(S.torus((bx, by, ell_z(vc, vr, bx, by, -0.02)), 0.15, 0.045, Rm=S.rot(-12, 90, 0)), Fg.GOLD, k=0.0, layer='trim', metal=G)
    fig.add(S.sphere((0, y0 + 3.0, ell_z(vc, vr, 0, y0 + 3.0, -0.04, back=True)), 0.12), Fg.GOLD, k=0.0, layer='trim', metal=G)
    # 오른쪽 주머니 (작은 덮개 + 테)
    px, py = 0.78, y0 + 2.35
    pz = ell_z(vc, vr, px, py, 0.0)
    Rp = S.rot(math.degrees(math.atan2(px, pz)) * 0.9, 0, 0)
    fig.add(S.box((px, py - 0.12, pz - 0.04), (0.34, 0.24, 0.09), round_=0.05, R=Rp), (0.95, 0.85, 0.66), k=0.0, layer='pocket')
    fig.add(S.capsule(tuple(np.array((px, py + 0.12, pz + 0.03)) + Rp @ np.array((-0.33, 0, 0))),
                      tuple(np.array((px, py + 0.12, pz + 0.03)) + Rp @ np.array((0.33, 0, 0))), 0.045), Fg.GOLD, k=0.0, layer='trim', metal=G)

    # ---------- 회중시계 (금색 + 사슬) ----------
    wc = (0.86, y0 + 1.5, ell_z(vc, vr, 0.86, y0 + 1.5, 0.12))
    Rw = S.rot(26, 82, 0)                    # 원기둥 Y축 → 앞쪽 (살짝 오른쪽)
    fig.add(S.cylinder(wc, 0.56, 0.09, round_=0.05, R=Rw), Fg.GOLD, k=0.0, layer='watch', metal=G)
    fig.add(S.torus(np.array(wc) + Rw @ np.array((0, 0.08, 0)), 0.5, 0.06, Rm=Rw), Fg.GOLD, k=0.0, layer='watch', metal=G)
    face_c = np.array(wc) + Rw @ np.array((0, 0.085, 0))
    fig.add(S.cylinder(tuple(face_c), 0.46, 0.02, R=Rw), (0.99, 0.97, 0.92), k=0.0, layer='watchface')
    stem = np.array(wc) + Rw @ np.array((0, 0, -0.64))
    fig.add(S.capsule(tuple(np.array(wc) + Rw @ np.array((0, 0, -0.52))), tuple(stem), 0.08), Fg.GOLD, k=0.0, layer='watch', metal=G)
    fig.add(S.torus(tuple(stem + Rw @ np.array((0, 0, -0.1))), 0.11, 0.035, Rm=Rw @ S.rot(0, 0, 90)), Fg.GOLD, k=0.0, layer='watch', metal=G)
    # 사슬: 주머니 → 고리 (작은 구슬)
    a = np.array((px - 0.15, py - 0.02, pz + 0.04))
    b = stem + Rw @ np.array((0, 0, -0.16))
    for k in range(12):
        t = k / 11
        p = a * (1 - t) + b * t
        p[2] = max(p[2], ell_z(vc, vr, p[0], p[1], 0.06)) + 0.05 * math.sin(t * math.pi)
        p[1] -= 0.12 * math.sin(t * math.pi)
        fig.add(S.sphere(tuple(p), 0.055), Fg.GOLD, k=0.0, layer='chain', metal=G)
    # 시계판 눈금 + 바늘 (또렷한 부품)
    _clock_marks(fig, face_c, Rw, 0.46, 0.02, small=True)

    # ---------- 날개 (흰 어깨깃 + 갈색 날개깃, 손가락처럼 펼침) ----------
    for s in (-1, 1):
        sh = np.array((s * 1.5, y0 + 2.75, 0.1))
        fig.add(S.ellipsoid(tuple(sh + (s * 0.12, 0.05, 0)), (0.62, 0.85, 0.62), R=S.rot(0, 0, s * 28)), WHITE, k=0.3, layer='wing')
        for i, (ang, L, col) in enumerate(((-10, 1.5, RUST), (12, 1.6, RUST_DK), (34, 1.5, RUST), (56, 1.2, RUST_LT))):
            a = math.radians(ang)
            root = sh + np.array((s * 0.3, -0.45, 0.15 - i * 0.14))
            d = np.array((s * math.sin(a), -math.cos(a), 0.06))
            mid = root + d * L * 0.55
            fig.add(S.ellipsoid(tuple(mid), (0.3, L * 0.55, 0.2), R=S.rot(s * 8, 0, s * ang)), col, k=0.12, layer='wing')
        for i, ang in enumerate((-2, 24, 48)):   # 흰 덮깃 (가리비 모양)
            a = math.radians(ang)
            p = sh + np.array((s * (0.25 + math.sin(a) * 0.5), -0.45 - math.cos(a) * 0.38, 0.22 - i * 0.12))
            fig.add(S.ellipsoid(tuple(p), (0.3, 0.48, 0.26), R=S.rot(0, 0, s * ang)), WHITE, k=0.12, layer='wing')

    # ---------- 꼬리 (갈색 낫깃 다발: 뒤로 솟았다 말려 내려옴) ----------
    feathers = ((0.0, 1.25, RUST, 0.36), (-0.45, 1.05, RUST_DK, 0.32), (0.45, 1.05, RUST_DK, 0.32),
                (-0.22, 0.85, RUST_LT, 0.3), (0.22, 0.85, RUST_LT, 0.3), (-0.7, 0.75, RUST, 0.26), (0.7, 0.75, RUST, 0.26),
                (0.0, 0.62, RUST_DK, 0.28))
    for i, (x, h, col, rr) in enumerate(feathers):
        pts = []
        for k in range(12):
            t = k / 11
            ang = math.radians(-35 + 245 * t)
            pts.append((x * (1 + 0.5 * t), y0 + 2.15 + h * math.sin(ang) * 1.05 + 0.25 * h,
                        -1.15 - h * (1 - math.cos(ang)) * 0.8))
        for k in range(len(pts) - 1):
            a_, b_ = np.array(pts[k]), np.array(pts[k + 1])
            t_ = b_ - a_
            L = float(np.linalg.norm(t_))
            pitch = math.degrees(math.atan2(t_[2], t_[1]))
            w = rr * (1 - 0.55 * (k + 0.5) / 11)
            fig.add(S.ellipsoid(tuple((a_ + b_) / 2), (w * 1.45, L * 0.85, w * 0.8), R=S.rot(0, pitch, 0)), col, k=0.14, layer='tail')

    # ---------- 머리 ----------
    hc, HR = (0, y0 + 5.05, 0.1), 2.0
    fig.add(S.ellipsoid(hc, (HR * 1.05, HR * 0.96, HR * 0.98)), WHITE, k=0.5)
    fig.add(S.ellipsoid((0, y0 + 3.85, 0.2), (1.35, 0.7, 1.15)), WHITE, k=0.5)      # 목
    for s in (-1, 1):   # 볼살
        fig.add(S.sphere((s * 1.0, hc[1] - 0.75, hc[2] + 0.95), 0.75), WHITE, k=0.6)
    # 볏 (빨간 세 봉우리)
    for z, h, r in ((0.9, 1.2, 0.55), (0.1, 1.8, 0.66), (-0.75, 1.45, 0.6)):
        fig.add(S.ellipsoid((0, hc[1] + HR * 0.8 + h * 0.6, hc[2] + z), (0.5, h * 0.62, r), R=S.rot(0, -z * 20, 0)), COMB, k=0.25, layer='comb')
    fig.add(S.ellipsoid((0, hc[1] + HR * 0.9, hc[2] - 0.0), (0.45, 0.4, 1.2)), COMB, k=0.25, layer='comb')
    fig.add(S.ellipsoid((0.32, hc[1] + HR * 0.8 + 0.8, hc[2] + 0.45), (0.34, 0.62, 0.42), R=S.rot(0, 0, -28)), COMB, k=0.2, layer='comb')
    # 눈·눈썹·부리·볏살·볼터치
    Fg.kawaii_eyes(fig, hc, HR + 0.0, spread=26, pitch=3, size=0.6, tall=1.1)
    for s in (-1, 1):
        f = Fg._frame_on(hc, HR, s * 27, 28, -0.03)
        for k in range(13):
            t = (k - 6) / 6
            Fg._ellipsoid(fig.extra, (t * 0.27, -t * t * 0.07, 0), (0.07 - 0.02 * abs(t), 0.055 - 0.015 * abs(t), 0.05), BROW, 6, 4, f)
    bk = np.array(Fg._frame_on(hc, HR, 0, -10, -0.15).p((0, 0, 0)))
    fig.add(S.cone(tuple(bk), tuple(bk + (0, -0.15, 0.72)), 0.38, 0.07), BEAK, k=0.0, layer='beak')
    fig.add(S.ellipsoid(tuple(bk + (0, -0.05, 0.05)), (0.42, 0.3, 0.34)), BEAK, k=0.12, layer='beak')
    for s in (-1, 1):
        w = bk + (s * 0.15, -0.6, 0.2)
        fig.add(S.ellipsoid(tuple(w), (0.2, 0.34, 0.18), R=S.rot(0, 0, s * 8)), COMB, k=0.15, layer='wattle')
    fig.add(S.capsule(tuple(bk + (-0.12, -0.25, 0.15)), tuple(bk + (0.12, -0.25, 0.15)), 0.12), COMB, k=0.15, layer='wattle')
    for s in (-1, 1):
        p = np.array(Fg._frame_on(hc, HR, s * 44, -12, 0).p((0, 0, 0)))
        fig.paint(S.sphere(p, 0.5), Fg.BLUSH, soft=0.6)

    # ---------- 받침: 바닥 시계판 + 돌 ----------
    cc = np.array((2.45, y0 + 0.3, 2.25))
    Rc = S.rot(-32, 12, 0)
    fig.add(S.cylinder(tuple(cc), 1.05, 0.2, round_=0.08, R=Rc), Fg.GOLD, k=0.0, layer='plate', metal=G)
    fig.add(S.cylinder(tuple(cc + Rc @ np.array((0, 0.15, 0))), 0.9, 0.08, R=Rc), (0.98, 0.95, 0.86), k=0.0, layer='plateface')
    _clock_marks(fig, cc + Rc @ np.array((0, 0.23, 0)), Rc @ np.eye(3), 0.9, 0.0, small=False, hands=(10.1, 2.0))
    for p, r in (((-2.6, y0 + 0.05, 1.9), 0.32), ((-2.2, y0 + 0.02, 2.4), 0.2), ((3.2, y0 + 0.02, -0.6), 0.28), ((-3.1, y0, -1.2), 0.25)):
        fig.add(S.ellipsoid(p, (r * 1.3, r * 0.7, r)), (0.62, 0.62, 0.64), k=0.0, layer='stone')


def _clock_marks(fig, c, R, rad, lift, small=True, hands=(10.15, 1.8)):
    """원기둥(로컬 Y 위)의 판 위 눈금 12개 + 시·분침 (로컬 XZ 평면). R: 로컬→월드"""
    b = fig.extra
    R = np.asarray(R)
    from ..mesh import Frame
    c = np.asarray(c, np.float64) + R @ np.array((0, lift, 0))
    f = Frame(tuple(c), tuple(R[:, 0]), tuple(R[:, 1]), tuple(R[:, 2]))
    ink = (0.22, 0.14, 0.10)
    for h in range(12):
        a = math.radians(h * 30)
        d = rad * 0.8
        u, v = math.sin(a), -math.cos(a)        # 12시 = 로컬 -Z (판의 위쪽)
        big = h % 3 == 0
        L = rad * (0.14 if big else 0.08)
        w = rad * (0.05 if big else 0.03)
        Fg._ellipsoid(b, (u * d, 0.0, v * d), (w + abs(u) * (L - w), 0.025, w + abs(v) * (L - w)), ink, 6, 4, f)
    for frac, Lr, w in ((hands[0] / 12, 0.48, 0.05), (hands[1] / 12, 0.7, 0.035)):
        a = 2 * math.pi * frac
        u, v = math.sin(a), -math.cos(a)
        for k in range(6):
            t = (k + 0.5) / 6 * Lr * rad
            Fg._ellipsoid(b, (u * t, 0.01, v * t), (rad * w, 0.03, rad * w), ink, 6, 4, f)
    Fg._ellipsoid(b, (0, 0.02, 0), (rad * 0.07, 0.04, rad * 0.07), Fg.GOLD, 8, 4, f)
