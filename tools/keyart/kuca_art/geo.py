"""
좌표와 OSM 읽기. CampusMap.GeoToMapPlane, CampusBuildings.BuildMeshes 와 같은 규칙.
월드: X 동쪽, Z 북쪽 (m), 지도 중심이 원점. 지면 텍스처 픽셀: 왼쪽 위가 북서쪽.
"""
import json
import math

MIN_LON, MAX_LON, MIN_LAT, MAX_LAT = 127.0720, 127.0880, 37.2370, 37.2510
MAP_W, MAP_H = 1418.0, 1548.0
LEVEL_H = 3.3
DEFAULT_H = {'apartments': 45.0, 'dormitory': 24.0, 'college': 18.0, 'university': 18.0, 'school': 18.0,
             'residential': 15.0, 'commercial': 12.0, 'retail': 12.0, 'office': 12.0, 'house': 7.0, 'detached': 7.0}


def to_world(lon, lat):
    return (((lon - MIN_LON) / (MAX_LON - MIN_LON) - 0.5) * MAP_W, ((lat - MIN_LAT) / (MAX_LAT - MIN_LAT) - 0.5) * MAP_H)


def inside_campus(lon, lat):
    return MIN_LON <= lon <= MAX_LON and MIN_LAT <= lat <= MAX_LAT


class Raster:
    """월드 (x, z) ↔ 지면 텍스처 픽셀"""

    def __init__(self, height_px):
        self.H = height_px
        self.W = round(height_px * MAP_W / MAP_H)
        self.ppm = self.H / MAP_H

    def px(self, x, z):
        return ((x / MAP_W + 0.5) * self.W, (0.5 - z / MAP_H) * self.H)

    def world(self, px, py):
        return ((px / self.W - 0.5) * MAP_W, (0.5 - py / self.H) * MAP_H)


def _meters(s):
    if not s:
        return 0.0
    try:
        v = float(str(s).strip().replace('m', '').strip())
        return v if v > 0 else 0.0
    except ValueError:
        return 0.0


def _heights(t):
    max_h = _meters(t.get('height')) or _meters(t.get('building:levels')) * LEVEL_H
    min_h = _meters(t.get('min_height')) or _meters(t.get('building:min_level')) * LEVEL_H
    if max_h <= 0:
        max_h = DEFAULT_H.get(t.get('building'), 10.0)
    if min_h >= max_h:
        min_h = 0.0
    return min_h, max_h


class Building:
    __slots__ = ('id', 'name', 'poly', 'min_h', 'h', 'tags')

    def __init__(self, id, name, poly, min_h, h, tags):
        self.id, self.name, self.poly, self.min_h, self.h, self.tags = id, name, poly, min_h, h, tags

    @property
    def area(self):
        from .mesh import signed_area
        return abs(signed_area(self.poly))

    @property
    def centroid(self):
        n = len(self.poly)
        return (sum(p[0] for p in self.poly) / n, sum(p[1] for p in self.poly) / n)


def load_buildings(path):
    """CampusBuildings.cs 와 같은 다각형 (시계 방향, 중복점 제거, 캠퍼스 범위 밖 제외)"""
    from .mesh import signed_area
    out = []
    for e in json.load(open(path, encoding='utf-8'))['elements']:
        t = e.get('tags') or {}
        outlines = []
        if e['type'] == 'way' and e.get('geometry'):
            outlines.append(e['geometry'])
        elif e['type'] == 'relation':
            outlines += [m['geometry'] for m in e.get('members', []) if m.get('role') == 'outer' and m.get('geometry')]
        min_h, max_h = _heights(t)
        for g in outlines:
            poly = []
            for p in g:
                v = to_world(p['lon'], p['lat'])
                if not poly or (poly[-1][0] - v[0]) ** 2 + (poly[-1][1] - v[1]) ** 2 > 1e-6:
                    poly.append(v)
            clon = sum(p['lon'] for p in g) / len(g)
            clat = sum(p['lat'] for p in g) / len(g)
            if len(poly) > 1 and (poly[0][0] - poly[-1][0]) ** 2 + (poly[0][1] - poly[-1][1]) ** 2 < 1e-6:
                poly.pop()
            if len(poly) < 3 or not inside_campus(clon, clat):
                continue
            if signed_area(poly) > 0:
                poly.reverse()
            out.append(Building(f"{e['type']}-{e['id']}", t.get('name', ''), poly, min_h, max_h, t))
    return out


def load_osm(path):
    return json.load(open(path, encoding='utf-8'))['elements']


def point_in_poly(x, z, poly):
    inside = False
    n = len(poly)
    j = n - 1
    for i in range(n):
        xi, zi = poly[i]
        xj, zj = poly[j]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / (zj - zi + 1e-12) + xi:
            inside = not inside
        j = i
    return inside


def dist_to_segment(px, pz, a, b):
    ax, az = a
    bx, bz = b
    dx, dz = bx - ax, bz - az
    L2 = dx * dx + dz * dz
    t = 0.0 if L2 < 1e-9 else max(0.0, min(1.0, ((px - ax) * dx + (pz - az) * dz) / L2))
    qx, qz = ax + dx * t, az + dz * t
    return math.hypot(px - qx, pz - qz), (qx, qz)


def dist_to_poly_edge(px, pz, poly):
    return min(dist_to_segment(px, pz, poly[i - 1], poly[i])[0] for i in range(len(poly)))


class Footprint:
    """건물 바닥 볼록껍질의 최소 넓이 사각형 (KeyArtLandmarks.Footprint 와 같음)"""

    def __init__(self, poly, height):
        self.height = height
        hull = _hull(list(poly))
        best = float('inf')
        for i in range(len(hull)):
            a, b = hull[i], hull[(i + 1) % len(hull)]
            ex, ez = b[0] - a[0], b[1] - a[1]
            L = math.hypot(ex, ez)
            if L < 1e-6:
                continue
            e = (ex / L, ez / L)
            n = (-e[1], e[0])
            us = [p[0] * e[0] + p[1] * e[1] for p in hull]
            vs = [p[0] * n[0] + p[1] * n[1] for p in hull]
            area = (max(us) - min(us)) * (max(vs) - min(vs))
            if area < best:
                best = area
                self.u, self.v = e, n
                self.half_u, self.half_v = (max(us) - min(us)) / 2, (max(vs) - min(vs)) / 2
                cu, cv = (max(us) + min(us)) / 2, (max(vs) + min(vs)) / 2
                self.center = (e[0] * cu + n[0] * cv, e[1] * cu + n[1] * cv)

    def face(self, target):
        """네 면 중 target 쪽 면: (면 중심 x,z), 바깥 법선, 면 방향, 면 길이, 깊이"""
        dx, dz = target[0] - self.center[0], target[1] - self.center[1]
        L = math.hypot(dx, dz) or 1.0
        dx, dz = dx / L, dz / L
        du = dx * self.u[0] + dz * self.u[1]
        dv = dx * self.v[0] + dz * self.v[1]
        if abs(du) > abs(dv):
            s = 1 if du >= 0 else -1
            n, t, half, other = (self.u[0] * s, self.u[1] * s), self.v, self.half_u, self.half_v
        else:
            s = 1 if dv >= 0 else -1
            n, t, half, other = (self.v[0] * s, self.v[1] * s), self.u, self.half_v, self.half_u
        c = (self.center[0] + n[0] * half, self.center[1] + n[1] * half)
        return {'center': (c[0], 0.0, c[1]), 'normal': (n[0], 0.0, n[1]), 'tangent': (t[0], 0.0, t[1]),
                'length': other * 2, 'depth': half * 2}

    def long_axis(self):
        if self.half_u >= self.half_v:
            return self.u, self.half_u * 2, self.half_v * 2
        return self.v, self.half_v * 2, self.half_u * 2


def _hull(pts):
    pts = sorted(set(pts))
    if len(pts) < 3:
        return pts

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]
