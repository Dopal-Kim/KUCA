"""
면마다 정점을 따로 두는(각진) 로우폴리 메시 조립기. 좌표는 Unity 월드 좌표 그대로 (Y 위, Z 북쪽, 1 = 1 m).
색은 sRGB 0~1 버텍스 색. 감는 방향은 도형 안쪽 점을 기준으로 맞춰 바깥 법선이 cross(b-a, c-a) 가 되게 한다
(Unity 왼손 좌표계에서 앞면).
"""
import math
import struct

import numpy as np


def shade(c, k):
    return (c[0] * k, c[1] * k, c[2] * k)


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


class Frame:
    """원점 + 세 축 (회전·균일 배율). 로컬 점을 월드로 옮긴다."""

    __slots__ = ('o', 'x', 'y', 'z')

    def __init__(self, o=(0.0, 0.0, 0.0), x=(1.0, 0.0, 0.0), y=(0.0, 1.0, 0.0), z=(0.0, 0.0, 1.0)):
        self.o, self.x, self.y, self.z = o, x, y, z

    @staticmethod
    def yaw(pos, deg, scale=1.0):
        """Unity Quaternion.Euler(0, deg, 0): +Z 를 +X 쪽으로 돌린다."""
        r = math.radians(deg)
        c, s = math.cos(r) * scale, math.sin(r) * scale
        return Frame(tuple(pos), (c, 0.0, -s), (0.0, scale, 0.0), (s, 0.0, c))

    @staticmethod
    def look(pos, forward):
        """Unity Quaternion.LookRotation(forward, up): 로컬 +Z 가 forward(수평) 를 향한다."""
        fx, fz = forward[0], forward[2]
        L = math.hypot(fx, fz) or 1.0
        fx, fz = fx / L, fz / L
        return Frame(tuple(pos), (fz, 0.0, -fx), (0.0, 1.0, 0.0), (fx, 0.0, fz))

    def p(self, v):
        o, x, y, z = self.o, self.x, self.y, self.z
        return (o[0] + v[0] * x[0] + v[1] * y[0] + v[2] * z[0],
                o[1] + v[0] * x[1] + v[1] * y[1] + v[2] * z[1],
                o[2] + v[0] * x[2] + v[1] * y[2] + v[2] * z[2])

    def dir(self, v):
        x, y, z = self.x, self.y, self.z
        return (v[0] * x[0] + v[1] * y[0] + v[2] * z[0],
                v[0] * x[1] + v[1] * y[1] + v[2] * z[1],
                v[0] * x[2] + v[1] * y[2] + v[2] * z[2])

    def child(self, pos, yaw_deg=0.0):
        """이 프레임 안에서 pos 로 옮기고 Y 축으로 돌린 프레임"""
        r = math.radians(yaw_deg)
        c, s = math.cos(r), math.sin(r)
        return Frame(self.p(pos), self.dir((c, 0, -s)), self.y, self.dir((s, 0, c)))


IDENT = Frame()


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _cross(u, v):
    return (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])


def _dot(u, v):
    return u[0] * v[0] + u[1] * v[1] + u[2] * v[2]


_T = (1 + 5 ** 0.5) / 2
_ICO_V = [(-1, _T, 0), (1, _T, 0), (-1, -_T, 0), (1, -_T, 0), (0, -1, _T), (0, 1, _T), (0, -1, -_T), (0, 1, -_T),
          (_T, 0, -1), (_T, 0, 1), (-_T, 0, -1), (-_T, 0, 1)]
_ICO_V = [tuple(c / math.sqrt(sum(k * k for k in v)) for c in v) for v in _ICO_V]
_ICO_F = [0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
          3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1]


class Builder:
    """삼각형 목록. ao_floor 위 ao_height 안쪽 정점은 어둡게 (바닥 접지 그늘)."""

    def __init__(self):
        self.pos = []
        self.col = []
        self.ao_floor = 0.0
        self.ao_height = 2.0
        self.ao_strength = 0.28
        self.aux = None          # 외벽 메시만: 정점마다 (벽 위 위치 u, 벽 길이) — 셰이더가 창 배치에 씀
        self.alpha = []          # 정점 알파: 0 = 밤에 빛나는 부분 (가로등 머리, 유리문 등)
        self.emissive = False

    @property
    def tri_count(self):
        return len(self.pos) // 9

    def _ao(self, y):
        if self.ao_strength <= 0:
            return 1.0
        t = (y - self.ao_floor) / self.ao_height
        t = 0.0 if t < 0 else (1.0 if t > 1 else t)
        return 1.0 - self.ao_strength * (1.0 - t) ** 2

    def tri(self, m, a, b, d, col, inside, aux=None):
        """로컬 a, b, d. inside(로컬 안쪽 점) 반대쪽이 앞면이 되게 감는다. aux: 정점별 (u, 벽 길이)"""
        cen = ((a[0] + b[0] + d[0]) / 3, (a[1] + b[1] + d[1]) / 3, (a[2] + b[2] + d[2]) / 3)
        if _dot(_cross(_sub(b, a), _sub(d, a)), _sub(cen, inside)) < 0:
            b, d = d, b
            if aux:
                aux = (aux[0], aux[2], aux[1])
        if self.aux is not None:
            for q in (aux or ((0.0, 0.0),) * 3):
                self.aux.extend(q)
        a_val = 0.0 if self.emissive else 1.0
        self.alpha.extend((a_val, a_val, a_val))
        for v in (a, b, d):
            w = m.p(v)
            self.pos.extend(w)
            k = self._ao(w[1])
            self.col.extend((col[0] * k, col[1] * k, col[2] * k))

    def quad(self, m, a, b, d, e, col, inside):
        self.tri(m, a, b, d, col, inside)
        self.tri(m, a, d, e, col, inside)

    # ---------- 기본 도형 (LowPolyMeshBuilder.cs 와 같은 모양) ----------

    def box(self, m, center, size, col, top=None, bottom=True):
        cx, cy, cz = center
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2

        def P(x, y, z):
            return (cx + x * hx, cy + y * hy, cz + z * hz)

        top = top or shade(col, 1.05)
        self.quad(m, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), top, center)
        if bottom:
            self.quad(m, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), shade(col, 0.8), center)
        self.quad(m, P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), col, center)
        self.quad(m, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), shade(col, 0.97), center)
        self.quad(m, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), shade(col, 0.95), center)
        self.quad(m, P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), shade(col, 0.97), center)

    def bevel_box(self, m, center, size, bevel, col, top=None):
        """윗모서리를 깎은 상자 (산울타리, 관목 블록). 바닥면 없음"""
        cx, cy, cz = center
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        b = min(bevel, hx * 0.9, hz * 0.9, hy)
        top = top or shade(col, 1.08)
        y0, y1, yt = cy - hy, cy + hy - b, cy + hy
        lo = [(cx - hx, y0, cz - hz), (cx + hx, y0, cz - hz), (cx + hx, y0, cz + hz), (cx - hx, y0, cz + hz)]
        mid = [(x, y1, z) for (x, _, z) in lo]
        up = [(cx - hx + b, yt, cz - hz + b), (cx + hx - b, yt, cz - hz + b), (cx + hx - b, yt, cz + hz - b), (cx - hx + b, yt, cz + hz - b)]
        sides = [0.94, 0.97, 1.0, 0.92]
        for i in range(4):
            j = (i + 1) % 4
            self.quad(m, lo[i], lo[j], mid[j], mid[i], shade(col, sides[i]), center)
            self.quad(m, mid[i], mid[j], up[j], up[i], shade(col, sides[i] * 1.04 + 0.03), center)
        self.quad(m, up[0], up[1], up[2], up[3], top, (cx, y0, cz))

    def prism(self, m, base, r, height, sides, col, caps=False, r_top=None, rot=0.0):
        r_top = r if r_top is None else r_top
        axis = (base[0], base[1] + height / 2, base[2])
        rr = math.radians(rot)
        for i in range(sides):
            a0, a1 = rr + i * 2 * math.pi / sides, rr + (i + 1) * 2 * math.pi / sides
            p0 = (base[0] + math.cos(a0) * r, base[1], base[2] + math.sin(a0) * r)
            p1 = (base[0] + math.cos(a1) * r, base[1], base[2] + math.sin(a1) * r)
            q0 = (base[0] + math.cos(a0) * r_top, base[1] + height, base[2] + math.sin(a0) * r_top)
            q1 = (base[0] + math.cos(a1) * r_top, base[1] + height, base[2] + math.sin(a1) * r_top)
            self.quad(m, p0, q0, q1, p1, shade(col, 0.92 + 0.12 * ((i * 3) % sides) / sides), axis)
            if caps:
                self.tri(m, q0, q1, (base[0], base[1] + height, base[2]), shade(col, 1.05), axis)

    def cone(self, m, base, r, height, sides, col, rot=0.0, jitter=None, bottom=True):
        tip = (base[0], base[1] + height, base[2])
        inner = (base[0], base[1] + height * 0.3, base[2])
        rr = math.radians(rot)
        rad = [r * (jitter[i % len(jitter)] if jitter else 1.0) for i in range(sides)]
        for i in range(sides):
            j = (i + 1) % sides
            a0, a1 = rr + i * 2 * math.pi / sides, rr + (i + 1) * 2 * math.pi / sides
            p0 = (base[0] + math.cos(a0) * rad[i], base[1], base[2] + math.sin(a0) * rad[i])
            p1 = (base[0] + math.cos(a1) * rad[j], base[1], base[2] + math.sin(a1) * rad[j])
            self.tri(m, p0, tip, p1, shade(col, 0.9 + 0.2 * ((i * 37) % 7) / 6), inner)
            if bottom:
                self.tri(m, p0, p1, base, shade(col, 0.72), inner)

    def frustum4(self, m, base, base_half, top_half, height, col):
        inside = (base[0], base[1] + height / 2, base[2])
        s = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
        for i in range(4):
            j = (i + 1) % 4
            B = lambda k: (base[0] + s[k][0] * base_half, base[1], base[2] + s[k][1] * base_half)
            T = lambda k: (base[0] + s[k][0] * top_half, base[1] + height, base[2] + s[k][1] * top_half)
            self.quad(m, B(i), B(j), T(j), T(i), shade(col, 0.9 + 0.05 * i), inside)

    def ico(self, m, center, radius, col, var=0.12):
        for f in range(0, len(_ICO_F), 3):
            pts = []
            for k in range(3):
                v = _ICO_V[_ICO_F[f + k]]
                pts.append((center[0] + v[0] * radius[0], center[1] + v[1] * radius[1], center[2] + v[2] * radius[2]))
            # 위를 향한 면은 밝게, 아래는 어둡게 (일러스트식 덩어리 음영)
            ny = (_ICO_V[_ICO_F[f]][1] + _ICO_V[_ICO_F[f + 1]][1] + _ICO_V[_ICO_F[f + 2]][1]) / 3
            k = 0.94 + var * ((f * 13) % 5) / 4 + 0.08 * ny
            self.tri(m, pts[0], pts[2], pts[1], shade(col, k), center)

    def hemisphere(self, m, base, r, segments, rings, col):
        def P(th, ph):
            return (base[0] + math.cos(th) * math.cos(ph) * r, base[1] + math.sin(th) * r, base[2] + math.cos(th) * math.sin(ph) * r)
        for ring in range(rings):
            t0, t1 = ring * math.pi / 2 / rings, (ring + 1) * math.pi / 2 / rings
            for s in range(segments):
                p0, p1 = s * 2 * math.pi / segments, (s + 1) * 2 * math.pi / segments
                sh = shade(col, 0.95 + 0.05 * ((s + ring) % 2))
                if ring < rings - 1:
                    self.tri(m, P(t0, p0), P(t1, p0), P(t1, p1), sh, base)
                    self.tri(m, P(t0, p0), P(t1, p1), P(t0, p1), sh, base)
                else:   # 꼭대기: 꼭짓점 하나로 모이는 삼각형 (예전엔 넓이 0 이라 구멍이 났다)
                    self.tri(m, P(t0, p0), P(t1, p0), P(t0, p1), sh, base)

    def arc_tier(self, m, center, r0, r1, a0, a1, h, segments, col, y0=0.0):
        def At(r, a, y):
            return (center[0] + math.cos(a) * r, y, center[2] + math.sin(a) * r)
        mid = (a0 + a1) / 2
        inside = (center[0] + math.cos(mid) * (r0 + r1) / 2, y0 + h * 0.5, center[2] + math.sin(mid) * (r0 + r1) / 2)
        for s in range(segments):
            b0, b1 = a0 + (a1 - a0) * s / segments, a0 + (a1 - a0) * (s + 1) / segments
            bm = (b0 + b1) / 2
            seg_in = (center[0] + math.cos(bm) * (r0 + r1) / 2, y0 + h * 0.5, center[2] + math.sin(bm) * (r0 + r1) / 2)
            self.quad(m, At(r0, b0, y0 + h), At(r1, b0, y0 + h), At(r1, b1, y0 + h), At(r0, b1, y0 + h), shade(col, 1.04), seg_in)
            self.quad(m, At(r0, b0, y0), At(r0, b0, y0 + h), At(r0, b1, y0 + h), At(r0, b1, y0), shade(col, 0.86), seg_in)
            self.quad(m, At(r1, b0, y0), At(r1, b0, y0 + h), At(r1, b1, y0 + h), At(r1, b1, y0), shade(col, 0.9), seg_in)
        self.quad(m, At(r0, a0, y0), At(r1, a0, y0), At(r1, a0, y0 + h), At(r0, a0, y0 + h), shade(col, 0.92), inside)
        self.quad(m, At(r0, a1, y0), At(r1, a1, y0), At(r1, a1, y0 + h), At(r0, a1, y0 + h), shade(col, 0.92), inside)

    def gable(self, m, base, width, depth, height, col, ridge_along_z=True, end_col=None):
        w, d = width / 2, depth / 2
        inside = (base[0], base[1] + height * 0.3, base[2])
        end_col = end_col or shade(col, 0.9)

        def P(x, y, z):
            return (base[0] + x, base[1] + y, base[2] + z)
        if ridge_along_z:
            self.quad(m, P(-w, 0, -d), P(0, height, -d), P(0, height, d), P(-w, 0, d), shade(col, 0.95), inside)
            self.quad(m, P(w, 0, -d), P(0, height, -d), P(0, height, d), P(w, 0, d), shade(col, 1.03), inside)
            self.tri(m, P(-w, 0, -d), P(w, 0, -d), P(0, height, -d), end_col, inside)
            self.tri(m, P(-w, 0, d), P(w, 0, d), P(0, height, d), shade(end_col, 1.02), inside)
        else:
            self.quad(m, P(-w, 0, -d), P(-w, height, 0), P(w, height, 0), P(w, 0, -d), shade(col, 0.95), inside)
            self.quad(m, P(-w, 0, d), P(-w, height, 0), P(w, height, 0), P(w, 0, d), shade(col, 1.03), inside)
            self.tri(m, P(-w, 0, -d), P(-w, 0, d), P(-w, height, 0), end_col, inside)
            self.tri(m, P(w, 0, -d), P(w, 0, d), P(w, height, 0), shade(end_col, 1.02), inside)

    def disc(self, m, center, r, thickness, sides, col):
        back = (center[0], center[1], center[2] - thickness)
        for i in range(sides):
            a0, a1 = i * 2 * math.pi / sides, (i + 1) * 2 * math.pi / sides
            p0 = (center[0] + math.cos(a0) * r, center[1] + math.sin(a0) * r, center[2])
            p1 = (center[0] + math.cos(a1) * r, center[1] + math.sin(a1) * r, center[2])
            self.tri(m, center, p0, p1, col, back)

    def arch_panel(self, m, center, width, height, col, segs=6):
        """아치 창/문: 직사각형 + 반원 머리 (로컬 Z 를 바라보는 얇은 판)"""
        cx, cy, cz = center
        r = width / 2
        rect_h = max(0.0, height - r)
        back = (cx, cy + height / 2, cz - 0.2)
        self.quad(m, (cx - r, cy, cz), (cx + r, cy, cz), (cx + r, cy + rect_h, cz), (cx - r, cy + rect_h, cz), col, back)
        top = (cx, cy + rect_h, cz)
        for i in range(segs):
            a0, a1 = math.pi * i / segs, math.pi * (i + 1) / segs
            self.tri(m, top, (cx + math.cos(a0) * r, cy + rect_h + math.sin(a0) * r, cz),
                     (cx + math.cos(a1) * r, cy + rect_h + math.sin(a1) * r, cz), col, back)

    # ---------- 다각형 띠 (기단, 코니스, 옥상 난간) ----------

    def band(self, poly, y0, y1, out_off, in_off, side_col, top_col=None, inner_col=None):
        """
        시계 방향(위에서 볼 때) 다각형 poly 를 따라 두른 띠. 바깥 out_off, 안쪽 in_off 만큼 오프셋.
        in_off 가 음수면 벽 바깥에 붙은 띠 (안쪽 면은 벽에 가려 만들지 않음).
        """
        outer = offset_polygon(poly, out_off)
        inner = offset_polygon(poly, -in_off)
        top_col = top_col or shade(side_col, 1.06)
        inner_col = inner_col or shade(side_col, 0.9)
        n = len(poly)
        for i in range(n):
            j = (i + 1) % n
            oa, ob, ia, ib = outer[i], outer[j], inner[i], inner[j]
            # 바깥 면
            mx, mz = (ia[0] + ib[0]) / 2, (ia[1] + ib[1]) / 2
            ins = (mx, (y0 + y1) / 2, mz)
            self.quad(IDENT, (oa[0], y0, oa[1]), (ob[0], y0, ob[1]), (ob[0], y1, ob[1]), (oa[0], y1, oa[1]),
                      shade(side_col, 0.94 + 0.08 * ((i * 7) % 3) / 2), _outward_inside(oa, ob, ia, ib, y0, y1, True))
            # 윗면
            self.quad(IDENT, (oa[0], y1, oa[1]), (ob[0], y1, ob[1]), (ib[0], y1, ib[1]), (ia[0], y1, ia[1]), top_col,
                      (mx, y1 - 1.0, mz))
            if in_off > 0:
                self.quad(IDENT, (ia[0], y0, ia[1]), (ib[0], y0, ib[1]), (ib[0], y1, ib[1]), (ia[0], y1, ia[1]), inner_col,
                          _outward_inside(oa, ob, ia, ib, y0, y1, False))
            elif out_off > 0:
                # 밑면 (내민 띠 아래쪽)
                self.quad(IDENT, (oa[0], y0, oa[1]), (ob[0], y0, ob[1]), (ib[0], y0, ib[1]), (ia[0], y0, ia[1]),
                          shade(side_col, 0.7), (mx, y0 + 1.0, mz))


def _outward_inside(oa, ob, ia, ib, y0, y1, outer_face):
    """바깥 면이면 안쪽 고리 쪽 점, 안쪽 면이면 바깥 고리 쪽 점을 '안쪽'으로 준다."""
    if outer_face:
        return ((ia[0] + ib[0]) / 2, (y0 + y1) / 2, (ia[1] + ib[1]) / 2)
    return ((oa[0] + ob[0]) / 2, (y0 + y1) / 2, (oa[1] + ob[1]) / 2)


def signed_area(poly):
    a = 0.0
    n = len(poly)
    for i in range(n):
        x0, z0 = poly[i - 1]
        x1, z1 = poly[i]
        a += x0 * z1 - x1 * z0
    return a / 2


def offset_polygon(poly, d, miter_limit=2.5):
    """다각형을 바깥(+) / 안쪽(-)으로 d 만큼 민다. 방향은 넓이 부호로 판단."""
    if d == 0:
        return list(poly)
    n = len(poly)
    sgn = -1.0 if signed_area(poly) > 0 else 1.0  # 바깥 법선 방향 보정
    out = []
    for i in range(n):
        p0, p1, p2 = poly[i - 1], poly[i], poly[(i + 1) % n]
        e0 = (p1[0] - p0[0], p1[1] - p0[1])
        e1 = (p2[0] - p1[0], p2[1] - p1[1])
        l0 = math.hypot(*e0) or 1e-6
        l1 = math.hypot(*e1) or 1e-6
        n0 = (-e0[1] / l0 * sgn, e0[0] / l0 * sgn)
        n1 = (-e1[1] / l1 * sgn, e1[0] / l1 * sgn)
        bx, bz = n0[0] + n1[0], n0[1] + n1[1]
        bl = math.hypot(bx, bz)
        if bl < 1e-6:
            bx, bz, bl = n1[0], n1[1], 1.0
        bx, bz = bx / bl, bz / bl
        cos_half = bx * n1[0] + bz * n1[1]
        k = d / max(cos_half, 1.0 / miter_limit)
        out.append((p1[0] + bx * k, p1[1] + bz * k))
    return out


def outward_normal(poly, i):
    """변 i (poly[i] → poly[i+1]) 의 바깥 법선 (x, z)"""
    n = len(poly)
    a, b = poly[i], poly[(i + 1) % n]
    ex, ez = b[0] - a[0], b[1] - a[1]
    L = math.hypot(ex, ez) or 1e-6
    sgn = -1.0 if signed_area(poly) > 0 else 1.0
    return (-ez / L * sgn, ex / L * sgn)


# ---------- 청크와 파일 ----------

class Layer:
    """지도를 grid x grid 칸으로 나눠 칸마다 Builder 하나 (Unity 에서 보이는 칸만 그리게)."""

    def __init__(self, name, half_w, half_h, grid=6, aux=False):
        self.name, self.hw, self.hh, self.grid = name, half_w, half_h, grid
        self.chunks = {}
        self.with_aux = aux

    def at(self, x, z):
        g = self.grid
        cx = min(max(int((x / (2 * self.hw) + 0.5) * g), 0), g - 1)
        cz = min(max(int((z / (2 * self.hh) + 0.5) * g), 0), g - 1)
        key = cz * g + cx
        b = self.chunks.get(key)
        if b is None:
            b = self.chunks[key] = Builder()
            if self.with_aux:
                b.aux = []
        return b

    def meshes(self):
        for key in sorted(self.chunks):
            b = self.chunks[key]
            if b.pos:
                yield f'{self.name}_{key}', b


def write_bytes(path, meshes, pos_unit=None):
    """
    KUCA 지오메트리 파일 v2 (Unity KeyArtGeometry 컴포넌트, 웹 미리보기가 읽음)
      'KUCA' int32 version=3, 이후 전부 gzip:
      int32 meshCount
      반복: int32 nameLen, utf8 name, float32[3] origin, int32 vertexCount, int32 flags(1 = 벽 좌표 있음),
            int16[vc*3] position (origin 기준, 2 cm 단위), uint8[vc*4] sRGB color,
            (flags&1) uint16[vc*2] 벽 좌표 (u, 벽 길이) 10 cm 단위
      color 알파 0 = 밤에 빛나는 부분
    삼각형은 정점 3개씩 순서대로 (인덱스 없음). 법선은 읽는 쪽에서 면 법선으로 계산.
    """
    import gzip
    import io
    meshes = list(meshes)
    body = io.BytesIO()
    body.write(struct.pack('<i', len(meshes)))
    total = 0
    for name, b in meshes:
        nb = name.encode('utf-8')
        p = np.asarray(b.pos, np.float64).reshape(-1, 3)
        vc = len(p)
        total += vc
        origin = (p.min(axis=0) + p.max(axis=0)) / 2
        q = np.round((p - origin) / (pos_unit or POS_UNIT))
        assert np.abs(q).max() < 32767, f'{name}: 청크가 너무 큼'
        body.write(struct.pack('<i', len(nb)))
        body.write(nb)
        has_aux = b.aux is not None and len(b.aux) == vc * 2
        nrm = getattr(b, 'nrm', None)
        has_nrm = nrm is not None and len(nrm) == vc * 3
        body.write(struct.pack('<fffii', *origin, vc, (1 if has_aux else 0) | (2 if has_nrm else 0)))
        body.write(q.astype('<i2').tobytes())
        c = np.clip(np.asarray(b.col, np.float32).reshape(-1, 3) * 255 + 0.5, 0, 255).astype(np.uint8)
        al = np.asarray(b.alpha, np.float32) if len(b.alpha) == vc else np.ones(vc, np.float32)
        c = np.concatenate([c, (al * 255).astype(np.uint8)[:, None]], axis=1)
        body.write(c.tobytes())
        if has_aux:
            body.write(np.clip(np.round(np.asarray(b.aux, np.float64) * 10), 0, 65535).astype('<u2').tobytes())
        if has_nrm:   # flags&2: 정점 법선 int8[vc*3] (x127) — 동물 피규어의 매끈한 곡면
            body.write(np.clip(np.round(np.asarray(nrm, np.float64) * 127), -127, 127).astype('i1').tobytes())
    with open(path, 'wb') as f:
        f.write(b'KUCA')
        f.write(struct.pack('<i', 3))
        f.write(gzip.compress(body.getvalue(), 9))
    return total


POS_UNIT = 0.02


def triangulate(poly):
    """단순 다각형 귀 자르기 삼각분할 → 인덱스 삼각형 목록 (방향 무관)"""
    n = len(poly)
    if n < 3:
        return []
    idx = list(range(n))
    if signed_area(poly) < 0:
        idx.reverse()   # 내부적으로 반시계

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    def inside(p, a, b, c):
        return cross(a, b, p) >= 0 and cross(b, c, p) >= 0 and cross(c, a, p) >= 0

    tris = []
    guard = n * n
    while len(idx) > 3 and guard > 0:
        guard -= 1
        for k in range(len(idx)):
            ia, ib, ic = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
            a, b, c = poly[ia], poly[ib], poly[ic]
            if cross(a, b, c) <= 0:
                continue
            if any(inside(poly[j], a, b, c) for j in idx if j not in (ia, ib, ic)):
                continue
            tris.append((ia, ib, ic))
            idx.pop(k)
            break
        else:
            return tris   # 꼬인 외곽선: 지금까지만
    if len(idx) == 3:
        tris.append(tuple(idx))
    return tris


def glass_mix_cols(poly, min_run=6.0):
    """유리 건물 벽 종류 (변마다 버텍스 색): 긴 벽을 번갈아 커튼월(0.5) · 석재 슬릿(0), 모서리 짧은 벽은 석재.
    외벽 셰이더가 R 값으로 무늬를 고른다 (멀티미디어·글로벌관 키아트: 유리 덩어리와 돌 덩어리가 엇갈린다)."""
    runs = _wall_runs(poly)
    n = len(runs)
    ids, rid = [None] * n, -1
    for i, (u0, _, _) in enumerate(runs):
        if u0 == 0.0:
            rid += 1
        ids[i] = rid
    ids = [rid if r < 0 else r for r in ids]     # 0 번 변 앞에서 시작한 벽은 마지막 벽에 이어진다
    long_ids = []
    for i in range(n):
        if runs[i][2] >= min_run and ids[i] not in long_ids:
            long_ids.append(ids[i])
    return [(0.5, 0.5, 0.5) if runs[i][2] >= min_run and long_ids.index(ids[i]) % 2 == 0 else (0.0, 0.0, 0.0)
            for i in range(n)]


def extrude_poly(mb, poly, y0, y1, col=(1.0, 1.0, 1.0), roof=True, roof_col=None, inward=False, wall_cols=None):
    """다각형 기둥 (건물 외벽 셰이더용 덩어리). 벽은 바깥 법선, 지붕은 위를 본다.
    mb.aux 가 있으면 벽 정점마다 (u, 벽 길이): 거의 일직선으로 이어진 변들은 한 벽으로 친다."""
    n = len(poly)
    runs = _wall_runs(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        nx, nz = outward_normal(poly, i)
        mx, mz = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        k = -1.0 if inward else 1.0     # inward: 안쪽(중정)을 바라보는 벽
        ins = (mx - nx * k, (y0 + y1) / 2, mz - nz * k)
        u0, u1, L = runs[i]
        A, B = (u0, L), (u1, L)
        wc = wall_cols[i] if wall_cols else col
        mb.tri(IDENT, (a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], y1, b[1]), wc, ins, aux=(A, B, B))
        mb.tri(IDENT, (a[0], y0, a[1]), (b[0], y1, b[1]), (a[0], y1, a[1]), wc, ins, aux=(A, B, A))
    if roof:
        for ia, ib, ic in triangulate(poly):
            a, b, c = poly[ia], poly[ib], poly[ic]
            cx, cz = (a[0] + b[0] + c[0]) / 3, (a[1] + b[1] + c[1]) / 3
            mb.tri(IDENT, (a[0], y1, a[1]), (b[0], y1, b[1]), (c[0], y1, c[1]), roof_col or col, (cx, y1 - 1.0, cz))


def _wall_runs(poly, max_turn_deg=12.0):
    """변마다 (시작 u, 끝 u, 벽 전체 길이). 방향이 max_turn 이하로 꺾이는 변들은 한 벽으로 이어 센다."""
    n = len(poly)
    dirs = []
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        dirs.append((math.atan2(b[1] - a[1], b[0] - a[0]), math.hypot(b[0] - a[0], b[1] - a[1])))

    def turn(i):
        d = abs(dirs[i][0] - dirs[i - 1][0]) % (2 * math.pi)
        return math.degrees(min(d, 2 * math.pi - d))
    starts = [i for i in range(n) if turn(i) > max_turn_deg]
    out = [None] * n
    if not starts:
        total = sum(l for _, l in dirs)
        u = 0.0
        for i in range(n):
            out[i] = (u, u + dirs[i][1], total)
            u += dirs[i][1]
        return out
    for k, s0 in enumerate(starts):
        s1 = starts[(k + 1) % len(starts)]
        idx = []
        i = s0
        while True:
            idx.append(i)
            i = (i + 1) % n
            if i == s1:
                break
        total = sum(dirs[j][1] for j in idx)
        u = 0.0
        for j in idx:
            out[j] = (u, u + dirs[j][1], total)
            u += dirs[j][1]
    return out


def round_corners(poly, radius=2.6, segs=3, min_turn_deg=25.0):
    """모서리를 둥글린 다각형 (귀여운 덩어리감). 짧은 변은 반지름을 줄이고, 완만한 꺾임은 그대로"""
    n = len(poly)
    if n < 3:
        return list(poly)
    out = []
    for i in range(n):
        p0, p1, p2 = poly[i - 1], poly[i], poly[(i + 1) % n]
        e0 = (p1[0] - p0[0], p1[1] - p0[1])
        e1 = (p2[0] - p1[0], p2[1] - p1[1])
        l0, l1 = math.hypot(*e0), math.hypot(*e1)
        if l0 < 1e-6 or l1 < 1e-6:
            continue
        cosang = (e0[0] * e1[0] + e0[1] * e1[1]) / (l0 * l1)
        turn = math.degrees(math.acos(max(-1.0, min(1.0, cosang))))
        r = min(radius, l0 * 0.35, l1 * 0.35)
        if turn < min_turn_deg or r < 0.4:
            out.append(p1)
            continue
        a = (p1[0] - e0[0] / l0 * r, p1[1] - e0[1] / l0 * r)
        b = (p1[0] + e1[0] / l1 * r, p1[1] + e1[1] / l1 * r)
        # 2차 베지에로 모서리를 잇는다 (원호와 거의 같고 오목한 모서리에도 안전)
        for k in range(segs + 1):
            t = k / segs
            out.append(((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * p1[0] + t * t * b[0],
                        (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * p1[1] + t * t * b[1]))
    return out


def rect_poly(center, u, v, half_u, half_v):
    """중심·축·반길이로 사각형 (시계 방향, 건물 다각형과 같은 방향)"""
    cx, cz = center
    pts = [(cx + u[0] * su * half_u + v[0] * sv * half_v, cz + u[1] * su * half_u + v[1] * sv * half_v)
           for su, sv in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    if signed_area(pts) > 0:
        pts.reverse()
    return pts


def vault(mb, m, length, width, rise, segs, col, end_col=None):
    """반원통 지붕 (배럴 볼트). 로컬 Z 가 길이 방향, X 가 폭, 바닥 y=0"""
    import math as _m
    end_col = end_col or shade(col, 0.92)
    hl = length / 2
    pts = [(-width / 2 * _m.cos(_m.pi * k / segs), rise * _m.sin(_m.pi * k / segs)) for k in range(segs + 1)]
    for k in range(segs):
        (x0, y0), (x1, y1) = pts[k], pts[k + 1]
        mid = ((x0 + x1) / 2, (y0 + y1) / 2)
        mb.quad(m, (x0, y0, -hl), (x1, y1, -hl), (x1, y1, hl), (x0, y0, hl), shade(col, 0.94 + 0.1 * k / segs), (mid[0] * 0.5, mid[1] * 0.3, 0))
        mb.tri(m, (0, 0, -hl), (x0, y0, -hl), (x1, y1, -hl), end_col, (0, rise * 0.3, 0))
        mb.tri(m, (0, 0, hl), (x0, y0, hl), (x1, y1, hl), end_col, (0, rise * 0.3, 0))


def clip_half_plane(poly, origin, axis, keep_ge):
    """다각형을 (p - origin)·axis >= keep_ge 쪽만 남기게 자른다 (Sutherland–Hodgman)"""
    def d(p):
        return (p[0] - origin[0]) * axis[0] + (p[1] - origin[1]) * axis[1] - keep_ge
    out = []
    n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        da, db = d(a), d(b)
        if da >= 0:
            out.append(a)
        if (da >= 0) != (db >= 0):
            t = da / (da - db)
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out
