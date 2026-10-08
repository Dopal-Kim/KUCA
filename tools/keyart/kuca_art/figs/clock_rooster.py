"""시계탑 수탉: 크림 조끼(금색 테) + 빨간 단추 + 금색 회중시계, 빨간 볏·볏살, 갈색 날개깃·꼬리, 받침 위 시계판."""
import math

import numpy as np

from .. import sculpt as S
from .. import figures as Fg

TIER = 'gold'

WHITE = (0.985, 0.975, 0.965)
CREAM = (0.99, 0.93, 0.79)
COMB = (0.90, 0.16, 0.15)
COMB_DK = (0.78, 0.10, 0.11)
BEAK = (1.0, 0.72, 0.14)
FOOT = (1.0, 0.70, 0.16)
RUST = (0.68, 0.28, 0.13)
RUST_DK = (0.55, 0.20, 0.10)
RUST_LT = (0.80, 0.40, 0.20)
DKRED = (0.58, 0.11, 0.10)
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


def bounded(f, m=1.6, thr=0.45, h=0.3):
    """f 가 음수/표면 근처인 곳의 상자(+m) 안에서만 f 를 계산, 밖은 m (참 거리의 하한).
    m 은 부드러운 합의 k 보다 충분히 커야 한다: 밖의 상수값 여러 개가 smin 으로 겹쳐도 (m - k/4 ...) 다른 부품 표면에
    닿지 않아야 상자 경계에서 주름(고리 자국)이 생기지 않는다."""
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


def face_frame(fig, c, yaw, pitch, out=0.0, layer='body', up=(0, 1, 0)):
    """머리 중심 c 에서 (yaw, pitch) 방향의 실제 표면(layer) 점 Frame (밖에서 광선을 쏴서 찾음)"""
    yr, pr = math.radians(yaw), math.radians(pitch)
    d = np.array((math.cos(pr) * math.sin(yr), math.sin(pr), math.cos(pr) * math.cos(yr)))
    return Fg.surface_frame(fig, np.asarray(c, np.float64) + d * 6.0, -d, up=up, out=out, layer=layer)


def frame_pts(f, pts):
    """Frame 로컬 점들 → 세계 점"""
    o, x, y, z = (np.asarray(v, np.float64) for v in (f.o, f.x, f.y, f.z))
    return [o + x * a + y * b + z * c for a, b, c in pts]


def surf_beads(fig, f0, pts2d, r, col, layer='body', out=0.0):
    """얼굴 위 선 (눈썹·입): f0 Frame 의 로컬 (u, v) 점들을 표면으로 투영해 구슬 줄로"""
    o, x, y, z = (np.asarray(v, np.float64) for v in (f0.o, f0.x, f0.y, f0.z))
    pts = []
    for u, v in pts2d:
        q = Fg.surface_frame(fig, o + x * u + y * v + z * 1.5, -z, layer=layer, out=out)
        pts.append(np.asarray(q.o))
    beads(fig, pts, r, col)
    return pts


def build(fig, rng):
    fig = Fast(fig)
    Fg.base(fig, rng, flowers=8)
    y0 = Fg.TOP

    # ---------- 다리·발 (노란 발, 앞 발가락 3 + 뒤 1): 통통한 몸에 짧고 굵은 다리 ----------
    for s in (-1, 1):
        x = s * 0.7
        fig.add(S.capsule((x, y0 + 0.35, 0.18), (x * 0.95, y0 + 1.3, 0.08), 0.23, 0.3), FOOT, k=0.1, layer='feet')
        fig.add(S.ellipsoid((x, y0 + 0.24, 0.38), (0.42, 0.24, 0.42)), FOOT, k=0.15, layer='feet')
        for a in (-32, 0, 32):
            R = S.rot(a + s * 6, 0, 0)
            tip = np.array((x, y0 + 0.17, 0.38)) + R @ np.array((0, 0, 0.78))
            fig.add(S.capsule((x, y0 + 0.2, 0.38), tuple(tip), 0.2, 0.17), FOOT, k=0.12, layer='feet')
        fig.add(S.capsule((x, y0 + 0.2, 0.23), (x, y0 + 0.17, -0.3), 0.17, 0.14), FOOT, k=0.1, layer='feet')

    # ---------- 몸통: 가슴이 앞으로 나온 통통한 몸 (뒤는 둥근 엉덩이) ----------
    torso = [((0, y0 + 2.4, -0.05), (1.6, 1.42, 1.38)), ((0, y0 + 1.62, 0.0), (1.32, 0.78, 1.2)),
             ((0, y0 + 2.9, 0.42), (1.3, 1.12, 1.12))]
    for c, r in torso:
        fig.add(S.ellipsoid(c, r), WHITE, k=0.45)

    NECK = ((0, y0 + 3.85, 0.2), (1.45, 0.7, 1.25))

    def body_f(P):
        d = S.ellipsoid(*torso[0])(P)
        for c, r in torso[1:]:
            d = S.smin(d, S.ellipsoid(c, r)(P), 0.45)
        return d

    def shell_f(P):      # 조끼가 따라갈 표면 (몸 + 목)
        return S.smin(body_f(P), S.ellipsoid(*NECK)(P), 0.5)
    # 가슴 깃털 (V 트임에서 보이는 보송한 흰 털): 한 덩어리로 부드럽게
    q = hit(fig, (0, y0 + 3.15, 4.5), (0, 0, -1), field=body_f)
    fig.add(S.ellipsoid((0, y0 + 3.15, q[2] - 0.3), (0.7, 0.5, 0.42)), WHITE, k=0.35)

    # ---------- 조끼 (크림): 몸 전체를 엉덩이까지 감싼 껍질, 목 아래 작은 V 트임 ----------
    yv = y0 + 2.62                   # V 트임 꼭짓점
    hem_y = y0 + 1.12
    top_y = y0 + 3.12
    slope = 0.75

    def top_f(P):        # 목둘레: 뒤가 높고 앞이 낮게 기운 선
        return top_y + 0.05 - 0.22 * np.clip(P[:, 2], -1.5, 1.5)

    def vopen_f(P):      # 앞 V (양수 = 비움)
        return np.minimum(P[:, 2] - 0.6, (P[:, 1] - yv) * slope - np.abs(P[:, 0]))

    def vest(P):
        d = np.abs(shell_f(P) - 0.11) - 0.07
        d = rmax(d, hem_y - P[:, 1], 0.05)
        d = rmax(d, P[:, 1] - top_f(P), 0.05)
        return np.maximum(d, vopen_f(P))
    fig.add(vest, CREAM, k=0.0, layer='vest')

    def vz(x, y, out=0.0, back=False):
        q = hit(fig, (x, y, -4.5 if back else 4.5), (0, 0, 1 if back else -1), field=vest)
        return q[2] - out if back else q[2] + out
    # V 가장자리 금테
    for s in (-1, 1):
        pts = []
        for k in range(8):
            y = yv + 0.03 + (top_y - 0.2 - yv) * k / 7
            x = s * (y - yv) * slope + s * 0.02
            pts.append((x, y, vz(x, y, -0.02)))
        chain(fig, pts, 0.08, Fg.GOLD, 'trim', metal=G)
    # 앞 여밈선 (V 꼭짓점 → 아랫단) 금테 + 금 단추
    pts = [(0.0, y, vz(0.0, y, -0.02)) for y in np.linspace(yv, hem_y + 0.06, 9)]
    chain(fig, pts, 0.055, Fg.GOLD, 'trim', metal=G)
    for y in (y0 + 2.4, y0 + 1.95, y0 + 1.5):
        fig.add(S.sphere((-0.17, y, vz(-0.17, y, 0.02)), 0.11), Fg.GOLD, k=0.0, layer='btn', metal=G)
    # 아랫단 금테 (둘레 전체)
    fig.add(S.intersect(S.onion(lambda P: shell_f(P) - 0.11, 0.085), lambda P: np.abs(P[:, 1] - (hem_y + 0.05)) - 0.065),
            Fg.GOLD, k=0.0, layer='trim', metal=G)
    # 진홍 보석 + 뒤 단추
    gx, gy = -0.62, y0 + 2.55
    gz = vz(gx, gy)
    Rg = S.rot(math.degrees(math.atan2(gx, 1.4)), 80, 0)
    fig.add(S.sphere((gx, gy, gz + 0.05), 0.19), Fg.CRIMSON, k=0.0, layer='gem')
    fig.add(S.torus((gx, gy, gz + 0.0), 0.2, 0.06, Rm=Rg), Fg.GOLD, k=0.0, layer='trim', metal=G)
    for y in (y0 + 2.7, y0 + 2.2):
        fig.add(S.sphere((0, y, vz(0, y, 0.0, back=True)), 0.1), Fg.GOLD, k=0.0, layer='btn', metal=G)
    # 오른쪽 주머니 (덮개 + 금테)
    px, py = 0.8, y0 + 2.4
    pz = vz(px, py)
    Rp = S.rot(math.degrees(math.atan2(px, pz)) * 0.9, 0, 0)
    fig.add(S.box((px, py - 0.12, pz - 0.04), (0.36, 0.24, 0.09), round_=0.05, R=Rp), (0.95, 0.85, 0.66), k=0.0, layer='pocket')
    fig.add(S.capsule(tuple(np.array((px, py + 0.12, pz + 0.03)) + Rp @ np.array((-0.35, 0, 0))),
                      tuple(np.array((px, py + 0.12, pz + 0.03)) + Rp @ np.array((0.35, 0, 0))), 0.05), Fg.GOLD, k=0.0, layer='trim', metal=G)

    # ---------- 회중시계 (금색, 주머니에서 늘어진 사슬) ----------
    wc = (0.9, y0 + 1.5, vz(0.9, y0 + 1.5, 0.12))
    Rw = S.rot(26, 82, 0)                    # 원기둥 Y축 → 앞쪽 (살짝 오른쪽)
    fig.add(S.cylinder(wc, 0.56, 0.09, round_=0.05, R=Rw), Fg.GOLD, k=0.0, layer='watch', metal=G)
    fig.add(S.torus(np.array(wc) + Rw @ np.array((0, 0.08, 0)), 0.5, 0.06, Rm=Rw), Fg.GOLD, k=0.0, layer='watch', metal=G)
    face_c = np.array(wc) + Rw @ np.array((0, 0.085, 0))
    fig.add(S.cylinder(tuple(face_c), 0.46, 0.02, R=Rw), (0.99, 0.97, 0.92), k=0.0, layer='watchface')
    stem = np.array(wc) + Rw @ np.array((0, 0, -0.64))
    fig.add(S.capsule(tuple(np.array(wc) + Rw @ np.array((0, 0, -0.52))), tuple(stem), 0.08), Fg.GOLD, k=0.0, layer='watch', metal=G)
    fig.add(S.torus(tuple(stem + Rw @ np.array((0, 0, -0.1))), 0.11, 0.035, Rm=Rw @ S.rot(0, 0, 90)), Fg.GOLD, k=0.0, layer='watch', metal=G)
    a = np.array((px - 0.2, py - 0.05, pz + 0.04))
    b = stem + Rw @ np.array((0, 0, -0.16))
    for k in range(14):
        t = k / 13
        p = a * (1 - t) + b * t
        p[1] -= 0.18 * math.sin(t * math.pi)
        p[0] -= 0.12 * math.sin(t * math.pi)
        p[2] = max(p[2], vz(p[0], p[1], 0.05))
        fig.add(S.sphere(tuple(p), 0.055), Fg.GOLD, k=0.0, layer='chain', metal=G)
    _clock_marks(fig, face_c, Rw, 0.46, 0.02, small=True)

    # ---------- 날개: 조끼 아래 옆구리에서 바깥으로 펼친 적갈색 깃 부채 ----------
    for s in (-1, 1):
        root = np.array((s * 1.5, y0 + 2.65, 0.0))
        fan = ((28, 1.4, RUST_DK), (48, 1.65, RUST), (68, 1.7, DKRED), (88, 1.55, RUST), (108, 1.25, RUST_LT))
        for i, (ang, L, col) in enumerate(fan):
            a_ = math.radians(ang)
            d = np.array((s * math.sin(a_), -math.cos(a_), 0.0))
            mid = root + d * L * 0.55 + np.array((0, 0, 0.12 - i * 0.07))
            fig.add(S.ellipsoid(tuple(mid), (0.36, L * 0.56, 0.15), R=S.rot(s * 6, 0, s * ang)), col, k=0.06, layer='wing%d' % (i % 2))

    # ---------- 꼬리: 따로 떨어진 낫깃 부채 (뒤에서 보면 펼쳐짐) ----------
    tb = np.array((0.0, y0 + 2.45, -1.2))
    tails = ((-58, 0.85, RUST), (-38, 1.05, DKRED), (-19, 1.2, RUST_DK), (0, 1.32, RUST), (19, 1.2, DKRED), (38, 1.05, RUST_DK), (58, 0.85, RUST))
    for i, (phi, h, col) in enumerate(tails):
        ph = math.radians(phi)
        dvec = np.array((math.sin(ph), math.cos(ph), 0.0))
        nperp = np.array((math.cos(ph), -math.sin(ph), 0.0))
        pts = []
        for k in range(12):
            t = k / 11
            ang = math.radians(-30 + 235 * t)
            u = h * math.sin(ang) * 1.05 + 0.3 * h
            v = h * (1 - math.cos(ang)) * 0.8
            pts.append(tb + dvec * u + np.array((0, 0, -v)))
        for k in range(len(pts) - 1):
            a_, b_ = pts[k], pts[k + 1]
            tt = b_ - a_
            L = float(np.linalg.norm(tt))
            tt = tt / L
            w = 0.3 * (1 - 0.6 * (k + 0.5) / 11)
            wide = np.cross(tt, nperp)
            R = np.stack([nperp, tt, wide], axis=1)
            fig.add(S.ellipsoid(tuple((a_ + b_) / 2), (w * 1.05, L * 0.8, w * 0.7), R=R), col, k=0.1, layer='tail%d' % i)

    # ---------- 머리: 달걀형 (위가 좁고 아래 볼 쪽이 부드럽게 넓음), 볼 구 없음 ----------
    hc = np.array((0, y0 + 5.38, 0.12))
    fig.add(S.ellipsoid(tuple(hc), (1.7, 1.76, 1.68)), WHITE, k=0.6)
    fig.add(S.ellipsoid(tuple(hc + (0, -0.6, 0.12)), (1.84, 1.2, 1.62)), WHITE, k=0.6)       # 아래쪽 볼 볼륨
    fig.add(S.ellipsoid(*NECK), WHITE, k=0.5)                                                # 목
    top = hc[1] + 1.72
    # 볏 (빨간 세 봉우리)
    for z, h, r in ((0.8, 1.15, 0.52), (0.05, 1.75, 0.62), (-0.75, 1.4, 0.56)):
        fig.add(S.ellipsoid((0, top - 0.2 + h * 0.6, hc[2] + z), (0.46, h * 0.62, r), R=S.rot(0, -z * 20, 0)), COMB, k=0.25, layer='comb')
    fig.add(S.ellipsoid((0, top - 0.05, hc[2] - 0.05), (0.42, 0.4, 1.1)), COMB, k=0.25, layer='comb')
    fig.add(S.ellipsoid((0.3, top + 0.6, hc[2] + 0.45), (0.32, 0.6, 0.4), R=S.rot(0, 0, -28)), COMB, k=0.2, layer='comb')

    # ---------- 얼굴: 동그란 눈 (조금 작게, 높게) + 짧은 눈썹 (조급함: 안쪽이 살짝 올라감) ----------
    for s in (-1, 1):
        f = face_frame(fig, hc, s * 25, 1, out=-0.075)
        Fg.eye_at(fig, f, style='round', size=0.53, side=s)
        fb = face_frame(fig, hc, s * 26, 17)
        o = np.asarray(fb.o)
        surf_beads(fig, fb, [(s * (-0.21 + 0.42 * k / 6), 0.08 * (1 - k / 6) + 0.03 * math.sin(math.pi * k / 6)) for k in range(7)],
                   0.055, BROW, out=0.02)
    # 부리 (표면에서 앞으로)
    fb = face_frame(fig, hc, 0, -12)
    bo, bn = np.asarray(fb.o), np.asarray(fb.z)
    fig.add(S.cone(tuple(bo - bn * 0.15), tuple(bo + bn * 0.62 + np.array((0, -0.16, 0))), 0.36, 0.07), BEAK, k=0.0, layer='beak')
    fig.add(S.ellipsoid(tuple(bo + bn * 0.05 + np.array((0, -0.04, 0))), (0.4, 0.28, 0.3)), BEAK, k=0.12, layer='beak')
    # 볏살 (부리 아래 늘어진 빨간 살)
    for s in (-1, 1):
        w = bo + (s * 0.14, -0.62, 0.12)
        fig.add(S.ellipsoid(tuple(w), (0.19, 0.33, 0.17), R=S.rot(0, 0, s * 8)), COMB, k=0.15, layer='wattle')
    fig.add(S.capsule(tuple(bo + (-0.12, -0.28, 0.08)), tuple(bo + (0.12, -0.28, 0.08)), 0.12), COMB, k=0.15, layer='wattle')
    # 볼터치: 표면 칠만
    for s in (-1, 1):
        q = np.asarray(face_frame(fig, hc, s * 42, -16).o)
        layer_paint(fig, 'body', S.sphere(q, 0.4), Fg.BLUSH, 0.18, tol=0.08)

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
