"""
퀘스트 보상 액세서리 (A-3): 12종을 24종 동물 머리·눈·목에 맞춰 3D 로 만든다.
  uv run --with numpy --with scipy --with scikit-image --with fast-simplification python tools/keyart/build_accessories.py
출력  My project/Assets/Resources/KUCA/CreatureAccessories.bytes  (메시 이름 Acc_<동물>_<액세서리>, 동물 메시와 같은 좌표)
      tools/keyart/preview/creatures_acc.bytes (미리보기용: 동물 + 액세서리를 합친 메시, --preview)
디자인: Art/Concept/06_A_Round/A3/ (챗쿠 제미나이 시안)

맞추는 방법: 동물 조형(SDF)을 다시 세워 눈 위치를 기록하고, 눈을 품은 머리 부품만으로 광선을 쏴
머리 꼭대기·옆·아래(턱)를 찾는다 (모자·우산 같은 소품은 무시). 목걸이는 턱 아래 몸 단면을 따라 고리를 만든다.
"""
import math
import os
import random
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from kuca_art import figures as Fg      # noqa: E402
from kuca_art import sculpt as S        # noqa: E402
from kuca_art import mesh as M          # noqa: E402

RES = os.path.join(HERE, '..', '..', 'My project', 'Assets', 'Resources', 'KUCA')

# ---------- 색 ----------
CRIMSON = (0.66, 0.06, 0.20)
GOLD = (1.0, 0.74, 0.16)
PINK = (0.98, 0.42, 0.64)
BLOSSOM = (1.0, 0.74, 0.84)
LEAF = (0.36, 0.66, 0.24)
SKY = (0.55, 0.80, 1.0)
LENS = (0.20, 0.10, 0.14)
PEARL = (0.97, 0.94, 0.96)
BEADS = [(0.15, 0.55, 0.95), CRIMSON, GOLD, (0.15, 0.70, 0.30)]

# 몸이 머리보다 아주 작은 동물: 펜던트 크기 배율 (머리 크기로 재면 다리까지 내려온다)
PENDANT_SCALE = {'art_chameleon': 0.6}

ITEMS = ['headband_blossom', 'headband_crimson_ears', 'headband_laurel',
         'necklace_star', 'necklace_beads', 'necklace_heart',
         'glasses_round', 'glasses_heart', 'glasses_star',
         'earrings_star', 'earrings_pearl', 'earrings_blossom']


# ---------- 메시 조각 ----------

class Mesh:
    def __init__(self):
        self.pos, self.nrm, self.col = [], [], []

    def add_grid(self, P, N, col, closed_u=False):
        """P, N: (nu, nv, 3) 격자 → 삼각형. col: 색 또는 (nu, nv, 3)"""
        nu, nv = P.shape[:2]
        C = np.broadcast_to(np.asarray(col, np.float64), P.shape) if np.ndim(col) == 1 else col
        us = range(nu if closed_u else nu - 1)
        for i in us:
            i2 = (i + 1) % nu
            for j in range(nv - 1):
                for a, b, c in (((i, j), (i2, j + 1), (i2, j)), ((i, j), (i, j + 1), (i2, j + 1))):   # 바깥(법선 쪽)에서 반시계
                    for q in (a, b, c):
                        self.pos.append(P[q])
                        self.nrm.append(N[q])
                        self.col.append(C[q])

    def merge(self, other):
        self.pos += other.pos
        self.nrm += other.nrm
        self.col += other.col


def tube(path, r, col, closed=False, seg=10):
    """폴리라인을 따라가는 둥근 관"""
    path = np.asarray(path, np.float64)
    n = len(path)
    T = np.gradient(path, axis=0)
    if closed:
        T = np.roll(path, -1, 0) - np.roll(path, 1, 0)
    T /= np.linalg.norm(T, axis=1, keepdims=True) + 1e-9
    ref = np.array([0.0, 1.0, 0.0]) if abs(T[0][1]) < 0.9 else np.array([1.0, 0.0, 0.0])
    B = np.cross(T[0], ref)
    B /= np.linalg.norm(B)
    P = np.zeros((n, seg + 1, 3))
    N = np.zeros_like(P)
    rr = np.broadcast_to(np.asarray(r, np.float64), (n,))
    for i in range(n):
        B = B - T[i] * (B @ T[i])
        B /= np.linalg.norm(B) + 1e-9
        Nn = np.cross(T[i], B)
        for j in range(seg + 1):
            a = 2 * math.pi * j / seg
            d = B * math.cos(a) + Nn * math.sin(a)
            P[i, j] = path[i] + d * rr[i]
            N[i, j] = d
    m = Mesh()
    m.add_grid(P, N, col, closed_u=closed)
    if not closed:          # 끝을 둥글게 막는다
        for end, sgn in ((0, -1), (n - 1, 1)):
            m.merge(sphere(path[end], rr[end], col, 8, 6))
    return m


def sphere(c, r, col, seg=14, rings=8, scale=(1, 1, 1), basis=None):
    c = np.asarray(c, np.float64)
    R = np.eye(3) if basis is None else np.asarray(basis, np.float64)
    sc = np.asarray(scale, np.float64) * r
    P = np.zeros((seg + 1, rings + 1, 3))
    N = np.zeros_like(P)
    for i in range(seg + 1):
        a = 2 * math.pi * i / seg
        for j in range(rings + 1):
            b = math.pi * j / rings - math.pi / 2
            u = np.array([math.cos(b) * math.cos(a), math.sin(b), math.cos(b) * math.sin(a)])
            P[i, j] = c + R @ (u * sc)
            nn = R @ (u / np.maximum(sc, 1e-6))
            N[i, j] = nn / np.linalg.norm(nn)
    m = Mesh()
    m.add_grid(P, N, col)
    return m


def shape2d(kind, n=60):
    """윤곽선 (단위 크기, 반시계)"""
    t = np.linspace(0, 2 * math.pi, n, endpoint=False)
    if kind == 'circle':
        return np.stack([np.cos(t), np.sin(t)], 1)
    if kind == 'star':
        pts = []
        for k in range(10):
            a = math.pi / 2 + k * math.pi / 5
            rr = 1.0 if k % 2 == 0 else 0.48
            pts.append((math.cos(a) * rr, math.sin(a) * rr))
        # 꼭짓점 사이를 나눠 매끈한 테두리
        out = []
        for k in range(10):
            p, q = np.array(pts[k]), np.array(pts[(k + 1) % 10])
            for s in np.linspace(0, 1, 4, endpoint=False):
                out.append(p * (1 - s) + q * s)
        return np.array(out)
    if kind == 'heart':
        x = 16 * np.sin(t) ** 3
        y = 13 * np.cos(t) - 5 * np.cos(2 * t) - 2 * np.cos(3 * t) - np.cos(4 * t)
        h = np.stack([x, y], 1) / 16.0
        h[:, 1] += 0.15
        return h[::-1]
    if kind == 'tri':
        return np.array([(-0.85, -0.6), (0.85, -0.6), (0.15, 0.9), (-0.15, 0.9)])
    if kind == 'petal5':
        r = 0.62 + 0.38 * np.abs(np.cos(2.5 * t)) ** 0.6
        return np.stack([np.cos(t) * r, np.sin(t) * r], 1)
    raise ValueError(kind)


def slab(kind, center, xax, yax, size, depth, col, ring=None):
    """2D 모양을 두께 depth 로 밀어낸 판 (ring 이면 가운데가 빈 테: 안쪽 비율)"""
    c = np.asarray(center, np.float64)
    xa, ya = np.asarray(xax, np.float64), np.asarray(yax, np.float64)
    za = np.cross(xa, ya)
    za /= np.linalg.norm(za)
    o = shape2d(kind) * size
    m = Mesh()
    k = len(o)
    if ring is None:
        inner = o * 0.0
    else:
        inner = o * ring
    for f, sgn in ((depth / 2, 1), (-depth / 2, -1)):
        for i in range(k):
            a, b = o[i], o[(i + 1) % k]
            ia, ib = inner[i], inner[(i + 1) % k]
            quad = [ia, a, b, ib] if ring is not None else [ia, a, b]
            pts = [c + xa * p[0] + ya * p[1] + za * f for p in quad]
            tris = [(0, 1, 2), (0, 2, 3)] if ring is not None else [(0, 1, 2)]
            for tri in tris:
                idx = tri if sgn > 0 else tri[::-1]
                for q in idx:
                    m.pos.append(pts[q])
                    m.nrm.append(za * sgn)
                    m.col.append(col)
    for edge in ([o] if ring is None else [o, inner]):
        out = edge is o
        for i in range(k):
            a, b = edge[i], edge[(i + 1) % k]
            tdir = b - a
            n2 = np.array([tdir[1], -tdir[0]])
            n2 /= np.linalg.norm(n2) + 1e-9
            if not out:
                n2 = -n2
            nw = xa * n2[0] + ya * n2[1]
            P = [c + xa * a[0] + ya * a[1] + za * depth / 2, c + xa * b[0] + ya * b[1] + za * depth / 2,
                 c + xa * b[0] + ya * b[1] - za * depth / 2, c + xa * a[0] + ya * a[1] - za * depth / 2]
            for tri in ((0, 2, 1), (0, 3, 2)) if out else ((0, 1, 2), (0, 2, 3)):
                for q in tri:
                    m.pos.append(P[q])
                    m.nrm.append(nw)
                    m.col.append(col)
    return m


# ---------- 동물 머리 재기 ----------

def _record():
    rec = _Rec()
    orig = Fg.eye_at

    def eye_at(fig, f, *a, **k):
        size = k.get('size', a[1] if len(a) > 1 else 0.45)
        rec.append((np.array(f.o, float), np.array(f.z, float), float(size)))
        return orig(fig, f, *a, **k)
    Fg.eye_at = eye_at
    for mod in list(sys.modules.values()):
        fn = getattr(mod, 'closed_eye', None) if mod and getattr(mod, '__name__', '').startswith('kuca_art') else None
        if fn and not getattr(fn, '_rec', False):
            def wrap(fig, f, s, *a, _fn=fn, **k):
                rec.append((np.array(f.o, float), np.array(f.z, float), float(s)))
                return _fn(fig, f, s, *a, **k)
            wrap._rec = True
            mod.closed_eye = wrap
    # 눈을 face_frame 으로 직접 그리는 동물(두더지 등): 얼굴 위 좌표계를 따로 기록해 눈 대신 쓴다
    faces = []
    for mod in list(sys.modules.values()):
        fn = getattr(mod, 'face_frame', None) if mod and getattr(mod, '__name__', '').startswith('kuca_art') else None
        if fn and not getattr(fn, '_rec', False):
            def wrapf(*a, _fn=fn, **k):
                fr = _fn(*a, **k)
                faces.append((np.array(fr.o, float), np.array(fr.z, float), 0.4))
                return fr
            wrapf._rec = True
            mod.face_frame = wrapf
    rec.faces = faces
    return rec


class _Rec(list):
    faces = None


def _field(parts):
    def f(P):
        out = None
        for g, _, k, _ in parts:
            v = g(P)
            out = v if out is None else S.smin(out, v, k)
        return out
    return f


def _hit(field, o, d, maxt=12.0):
    o = np.asarray(o, float)
    d = np.asarray(d, float)
    d = d / np.linalg.norm(d)
    if field(o[None])[0] < 0:
        o, d = o + d * maxt, -d
    t = 0.0
    for _ in range(600):
        dist = field((o + d * t)[None])[0]
        if dist < 1e-3:
            p = o + d * t
            e = np.eye(3) * 1e-3
            n = np.array([field((p + e[i])[None])[0] - field((p - e[i])[None])[0] for i in range(3)])
            return p, n / (np.linalg.norm(n) + 1e-9)
        t += max(dist * 0.9, 1e-3)
        if t > maxt * 2:
            break
    return None, None


def measure(cid, rec):
    tier, fn = Fg.FIGURES[cid]
    rec.clear()
    rec.faces.clear()
    fig = Fg.Figure(tier)
    fn(fig, random.Random(3))
    whole = _field(fig.s.parts)
    found = list(rec[:2])
    if len(found) < 2:
        # 얼굴 좌표계 중 좌우로 짝을 이루는 첫 두 개 (눈 자리)
        for i, fa in enumerate(rec.faces):
            for fb in rec.faces[i + 1:]:
                if fa[0][0] * fb[0][0] < 0 and abs(fa[0][1] - fb[0][1]) < 0.2:
                    found = [fa, fb]
                    break
            if len(found) == 2:
                break
    eyes = sorted(found, key=lambda e: e[0][0])
    if len(eyes) < 2:
        raise RuntimeError(f'{cid}: 눈을 찾지 못함')
    L, R = eyes[0][0], eyes[1][0]
    eye_size = (eyes[0][2] + eyes[1][2]) / 2
    fwd = eyes[0][1] + eyes[1][1]
    fwd[1] = 0
    fwd /= np.linalg.norm(fwd)
    mid = (L + R) / 2
    spread = float(np.linalg.norm(R - L))
    # 머리 부품: 눈 뒤쪽 점을 품은 부품들 (모자·우산 등 소품 제외)
    probe = mid - fwd * spread * 0.55
    head_parts = [p for p in fig.s.parts if p[0](probe[None])[0] < 0.05]
    head = _field(head_parts) if head_parts else whole
    back, _ = _hit(head, probe, -fwd)
    front, _ = _hit(head, probe, fwd)
    C = (back + front) / 2 if back is not None and front is not None else probe
    C[1] = mid[1] + spread * 0.15
    top, _ = _hit(head, C + np.array([0, 9.0, 0]), [0, -1, 0])
    bottom, _ = _hit(head, C - np.array([0, 9.0, 0]), [0, 1, 0])
    if head(C[None])[0] > 0 and top is not None and bottom is not None:   # 눈이 머리 꼭대기에 붙은 개구리: C 가 머리 밖
        C[1] = (top[1] + bottom[1]) / 2
    sideR, _ = _hit(head, C + np.cross([0, 1, 0], fwd) * -9, np.cross([0, 1, 0], fwd))
    sideL, _ = _hit(head, C - np.cross([0, 1, 0], fwd) * -9, -np.cross([0, 1, 0], fwd))
    return dict(cid=cid, fig=fig, whole=whole, head=head, L=L, R=R, mid=mid, fwd=fwd, spread=spread, eye=eye_size,
                C=C, top=top, sideL=sideL, sideR=sideR, bottom=bottom)


# ---------- 액세서리 ----------

def axes(a):
    f = a['fwd']
    right = np.cross([0, 1, 0], f)
    right /= np.linalg.norm(right)
    if right @ (a['R'] - a['L']) < 0:
        right = -right
    return f, right, np.array([0.0, 1.0, 0.0])


def band_path(a, tilt=-4.0, span=88, out=0.06, n=41):
    """머리 위를 귀에서 귀로 넘는 띠 경로 (머리 표면을 따라감)"""
    f, right, up = axes(a)
    C = a['C']
    t = math.radians(tilt)
    upb = up * math.cos(t) - f * math.sin(t)
    hits = []
    for ang in np.linspace(-span, span, n):
        r = math.radians(ang)
        d = upb * math.cos(r) + right * math.sin(r)
        p, nn = _hit(a['head'], C + d * 9, -d)
        if p is not None:
            hits.append((r, d, p, nn, np.linalg.norm(p - C)))
    # 귀·더듬이 테두리를 타고 올라가지 않게: 정수리·옆 거리로 만든 타원보다 먼 점은 타원 위로 (띠가 귀 밑동을 지난다)
    rt = min([h[4] for h in hits if abs(h[0]) <= math.radians(20)], default=None)
    rs = min([h[4] for h in hits if abs(h[0]) >= math.radians(70)], default=None)
    pts, nrms = [], []
    for r, d, p, nn, dist in hits:
        if rt and rs:
            e = 1 / math.hypot(math.cos(r) / rt, math.sin(r) / rs)
            if dist > e * 1.12:
                p, nn = C + d * e, d
        pts.append(p + nn * out)
        nrms.append(nn)
    return np.array(pts), np.array(nrms)


def headband(a, kind):
    s = min(a['spread'], 2.1)   # 눈 사이가 아주 넓은 개구리·카멜레온: 액세서리가 너무 커지지 않게
    pts, nrms = band_path(a, out=0.07 * s)
    col = {'headband_blossom': PINK, 'headband_crimson_ears': CRIMSON, 'headband_laurel': GOLD}[kind]
    m = tube(pts, 0.11 * s, col)
    f, right, up = axes(a)
    if kind == 'headband_blossom':
        i = int(len(pts) * 0.7)
        p, n = pts[i], nrms[i]
        x = np.cross(n, f)
        x /= np.linalg.norm(x)
        y = np.cross(n, x)
        for k, ang in enumerate((-50, 40)):
            ra = math.radians(ang)
            leaf_dir = x * math.cos(ra) + y * math.sin(ra)
            m.merge(sphere(p + leaf_dir * 0.38 * s + n * 0.05 * s, 0.22 * s, LEAF, scale=(1.0, 0.45, 0.28),
                           basis=np.stack([leaf_dir, np.cross(n, leaf_dir), n], 1)))
        m.merge(slab('petal5', p + n * 0.12 * s, x, y, 0.42 * s, 0.1 * s, BLOSSOM))
        m.merge(sphere(p + n * 0.2 * s, 0.1 * s, CRIMSON))
    elif kind == 'headband_crimson_ears':
        for frac in (0.32, 0.68):
            i = int(len(pts) * frac)
            p, n = pts[i], nrms[i]
            tang = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
            tang /= np.linalg.norm(tang)
            ear_up = n
            # 고양이 귀 모양 리본: 크림슨 세모 + 안쪽 분홍
            m.merge(slab('tri', p + ear_up * 0.38 * s, tang, ear_up, 0.42 * s, 0.14 * s, CRIMSON))
            m.merge(slab('tri', p + ear_up * 0.36 * s + f * 0.08 * s, tang, ear_up, 0.26 * s, 0.05 * s, (0.98, 0.55, 0.62)))
        i = len(pts) // 2
        m.merge(sphere(pts[i] + nrms[i] * 0.12 * s, 0.2 * s, CRIMSON, scale=(1.2, 0.8, 0.8)))
    else:   # 월계수: 띠를 따라 잎을 두 줄로
        for i in range(2, len(pts) - 2, 2):
            p, n = pts[i], nrms[i]
            tang = pts[i + 1] - pts[i - 1]
            tang /= np.linalg.norm(tang)
            side = np.cross(n, tang)
            for sgn in (1, -1):
                d = tang * 0.6 + side * sgn * 0.8
                d /= np.linalg.norm(d)
                m.merge(sphere(p + d * 0.2 * s + n * 0.05 * s, 0.16 * s, GOLD, 10, 6, scale=(1.0, 0.45, 0.25),
                               basis=np.stack([d, np.cross(n, d), n], 1)))
        i = len(pts) // 2
        m.merge(sphere(pts[i] + nrms[i] * 0.14 * s + f * 0.1 * s, 0.11 * s, CRIMSON))
    return m


def glasses(a, kind):
    s = min(a['spread'], 2.1)   # 눈 사이가 아주 넓은 개구리·카멜레온: 액세서리가 너무 커지지 않게
    f, right, up = axes(a)
    r = max(a['eye'] * 1.25, 0.34 * min(s, 1.9))
    shape, frame_col, lens_col = {'glasses_round': ('circle', CRIMSON, LENS), 'glasses_heart': ('heart', PINK, (0.55, 0.20, 0.32)),
                                  'glasses_star': ('star', GOLD, SKY)}[kind]
    m = Mesh()
    centers = []
    for e in (a['L'], a['R']):
        c = e + f * (0.12 * min(s, 1.9) + a['eye'] * 0.35)
        centers.append(c)
        m.merge(slab(shape, c, right, up, r * (1.25 if shape == 'star' else 1.0), 0.12 * s, frame_col, ring=0.78))
        m.merge(slab(shape, c - f * 0.01 * s, right, up, r * (1.25 if shape == 'star' else 1.0) * 0.8, 0.04 * s, lens_col))
    # 다리(브리지)
    m.merge(tube([centers[0] + right * r * 0.75 + up * r * 0.2, (centers[0] + centers[1]) / 2 + up * r * 0.35,
                  centers[1] - right * r * 0.75 + up * r * 0.2], 0.06 * s, GOLD if kind == 'glasses_round' else frame_col))
    # 안경다리: 눈 바깥에서 머리 옆을 따라 뒤로
    for c, sgn, side in ((centers[0], -1, a['sideL']), (centers[1], 1, a['sideR'])):
        start = c + right * sgn * r * 0.95
        if side is None or abs((side - a['C']) @ right) > 1.3 * min(s, 1.9) + a['eye']:
            side = a['C'] + right * sgn * (1.1 * min(s, 1.9))
        end = side + right * sgn * 0.08 * s - f * 0.3 * s
        end[1] = max(start[1] - 0.8 * s, min(start[1], side[1]))   # 눈이 머리 위에 있으면(개구리) 다리를 머리 옆으로 내린다
        m.merge(tube([start, (start + end) / 2 + right * sgn * 0.1 * s, end], 0.06 * s, frame_col))
    return m


def _exit(field, o, d, step=0.05, maxt=8.0):
    """몸 안 o 에서 d 쪽으로 걸어 처음 밖으로 나가는 점 (팔·꼬리처럼 떨어진 부위는 무시)"""
    o = np.asarray(o, float)
    if field(o[None])[0] > 0:
        p, n = _hit(field, o, d)
        return p, n
    t = 0.0
    while t < maxt:
        t += step
        if field((o + d * t)[None])[0] > 0:
            p = o + d * t
            e = np.eye(3) * 1e-3
            n = np.array([field((p + e[i])[None])[0] - field((p - e[i])[None])[0] for i in range(3)])
            return p, n / (np.linalg.norm(n) + 1e-9)
    return None, None


def chin_y(a):
    """얼굴 앞면이 가장 안쪽으로 들어간 높이 (턱 밑·목). 눈 아래 0.5~1.8 눈 간격 사이에서 찾는다"""
    s = min(a['spread'], 1.9)   # 눈 사이가 아주 넓은 동물(개구리·카멜레온)은 머리 크기로 제한
    f = a['fwd']
    best, best_y = None, a['mid'][1] - 0.9 * s
    for y in np.linspace(a['mid'][1] - 0.5 * s, a['mid'][1] - 1.35 * s, 21):
        o = np.array([a['C'][0], y, a['C'][2]])
        p, _ = _hit(a['whole'], o + f * 9, -f)
        if p is None:
            continue
        depth = (p - o) @ f
        if best is None or depth < best:
            best, best_y = depth, y
    return best_y


def neck_ring(a):
    """턱 아래 몸통 단면을 따라 도는 고리"""
    s = min(a['spread'], 2.1)   # 눈 사이가 아주 넓은 개구리·카멜레온: 액세서리가 너무 커지지 않게
    y = chin_y(a) - 0.05 * s
    axis_c = np.array([a['C'][0], y, a['C'][2]])
    pts = []
    for k in range(48):
        ang = 2 * math.pi * k / 48
        d = np.array([math.cos(ang), 0.0, math.sin(ang)])
        p, nn = _exit(a['whole'], axis_c, d, step=0.03 * s)
        if p is None or np.linalg.norm(p - axis_c) > 2.5 * s:
            p = axis_c + d * 0.9 * s
            nn = d
        pts.append(p + nn * 0.08 * s)
    pts = np.array(pts)
    # 팔·어깨로 튀어나간 점은 둘레 중간값 근처로 당긴다 (매끈한 고리)
    off = (pts - axis_c) * [1, 0, 1]
    rad = np.linalg.norm(off, axis=1)
    lim = np.median(rad) * 1.15
    for i in range(len(pts)):
        if rad[i] > lim:
            pts[i] = axis_c + off[i] / rad[i] * lim + [0, pts[i][1] - axis_c[1], 0]
    # 한 번 부드럽게
    pts = (np.roll(pts, 1, 0) + pts * 2 + np.roll(pts, -1, 0)) / 4
    # 앞쪽은 살짝 아래로 늘어뜨린다
    f, right, up = axes(a)
    for i in range(len(pts)):
        dirn = pts[i] - axis_c
        dirn[1] = 0
        frontness = max(0.0, dirn @ f / (np.linalg.norm(dirn) + 1e-9))
        pts[i][1] -= frontness ** 2 * 0.25 * min(s, 1.9)
    # 정면 방향(각도)에 가장 가까운 점: 팔처럼 앞으로 튀어나온 곳에 펜던트가 가지 않게
    front_i = int(np.argmax([((p - axis_c) * [1, 0, 1]) @ f / (np.linalg.norm((p - axis_c) * [1, 0, 1]) + 1e-9) for p in pts]))
    return pts, front_i


def necklace(a, kind):
    s = min(a['spread'], 2.1)   # 눈 사이가 아주 넓은 개구리·카멜레온: 액세서리가 너무 커지지 않게
    f, right, up = axes(a)
    pts, fi = neck_ring(a)
    m = Mesh()
    front = pts[fi]
    if kind == 'necklace_beads':
        m.merge(tube(pts, 0.035 * s, GOLD, closed=True, seg=6))
        for k in range(-5, 6):
            p = pts[(fi + k) % len(pts)]
            m.merge(sphere(p + f * 0.04 * s, 0.13 * s, BEADS[k % 4], 10, 6))
        return m
    chain_col = CRIMSON if kind == 'necklace_star' else GOLD
    m.merge(tube(pts, 0.045 * s, chain_col, closed=True, seg=6))
    if kind == 'necklace_star':
        # 금빛 이음매 몇 개
        for k in range(0, len(pts), 4):
            m.merge(sphere(pts[k], 0.06 * s, GOLD, 6, 4))
    s = s * PENDANT_SCALE.get(a['cid'], 1.0)
    hang = front - up * 0.32 * s + f * 0.08 * s
    for _ in range(40):   # 가슴에 묻힌 펜던트는 앞으로 꺼낸다 (가운데·아래 끝이 몸 밖에)
        if min(a['whole'](hang[None])[0], a['whole']((hang - up * 0.25 * s)[None])[0]) >= 0.06 * s:
            break
        hang = hang + f * 0.03 * s
    m.merge(tube([front, hang + up * 0.12 * s], 0.035 * s, GOLD, seg=6))
    shape, col = ('star', GOLD) if kind == 'necklace_star' else ('heart', CRIMSON)
    m.merge(slab(shape, hang, right, up, 0.3 * s, 0.12 * s, col))
    if kind == 'necklace_heart':
        m.merge(slab('heart', hang - f * 0.01 * s, right, up, 0.36 * s, 0.08 * s, GOLD))
    return m


def earrings(a, kind):
    s = min(a['spread'], 2.1)   # 눈 사이가 아주 넓은 개구리·카멜레온: 액세서리가 너무 커지지 않게
    f, right, up = axes(a)
    m = Mesh()
    for side, sgn in ((a['sideL'], -1), (a['sideR'], 1)):
        if side is None:
            continue
        p = side - up * 0.35 * s - f * 0.1 * s
        p2, nn = _hit(a['head'], p + right * sgn * 6, -right * sgn)
        if p2 is not None:
            p = p2
        out = right * sgn
        if kind == 'earrings_star':
            m.merge(slab('star', p + out * 0.06 * s, f * -sgn, up, 0.22 * s, 0.08 * s, GOLD))
        else:
            hook_end = p + out * 0.08 * s - up * 0.18 * s
            m.merge(tube([p + out * 0.02 * s, p + out * 0.12 * s - up * 0.06 * s, hook_end], 0.03 * s, GOLD, seg=6))
            if kind == 'earrings_pearl':
                m.merge(sphere(hook_end - up * 0.16 * s, 0.16 * s, PEARL))
            else:
                c = hook_end - up * 0.2 * s
                m.merge(slab('petal5', c, f * -sgn, up, 0.2 * s, 0.07 * s, BLOSSOM))
                m.merge(sphere(c + out * 0.05 * s, 0.05 * s, CRIMSON, 6, 4))
    return m


def build_item(a, item):
    kind = item.split('_')[0]
    return {'headband': headband, 'glasses': glasses, 'necklace': necklace, 'earrings': earrings}[kind](a, item)


def to_builder(m):
    b = M.Builder()
    b.pos = np.asarray(m.pos, np.float64).ravel().tolist()
    b.col = np.clip(np.asarray(m.col, np.float64), 0, 1).ravel().tolist()
    b.nrm = np.asarray(m.nrm, np.float64).ravel().tolist()
    b.alpha = [1.0] * len(m.pos)
    return b


def main():
    only = [a.split('=', 1)[1].split(',') for a in sys.argv[1:] if a.startswith('--only=')]
    only = only[0] if only else None
    rec = _record()
    meshes = []
    for cid in Fg.FIGURES:
        if only and cid not in only:
            continue
        try:
            a = measure(cid, rec)
        except Exception as e:   # noqa: BLE001
            print('skip', cid, e)
            continue
        for item in ITEMS:
            mm = build_item(a, item)
            if not mm.pos:
                print('  empty', cid, item)
                continue
            meshes.append((f'Acc_{cid}_{item}', to_builder(mm)))
        print(cid, 'ok')
    out = os.path.join(RES, 'CreatureAccessories.bytes')
    M.write_bytes(out, meshes, pos_unit=0.0005)
    print('wrote', out, len(meshes))


if __name__ == '__main__':
    main()
