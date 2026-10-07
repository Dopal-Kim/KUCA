"""
키아트 지면 텍스처. 컨셉아트 바닥 타일(tiles/*.png)과 OSM 도로·녹지·물로 그린다.
1단계(Ground): 마스크와 도로 선 → 건물 입구·나무 배치가 피할 영역을 알려 준다.
2단계(paint): 입구 앞 포장, 나무·산울타리 그늘(AO), 건물 둘레 그늘까지 넣어 저장.
"""
import math
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

from .geo import Raster, to_world

# 키아트 아이콘에서 뽑은 색 (sRGB)
GRASS = np.array([140, 182, 56], np.float32)       # 햇빛 받은 잔디가 이 색이 되게
ASPHALT = np.array([122, 125, 131], np.float32)
CURB = np.array([229, 223, 211], np.float32)
PAVING = np.array([226, 213, 199], np.float32)
PLAZA = np.array([232, 220, 204], np.float32)
PARKING = np.array([206, 203, 197], np.float32)
WATER_EDGE = np.array([118, 214, 236], np.float32)
WATER_DEEP = np.array([44, 162, 214], np.float32)
STONE_RIM = np.array([224, 214, 198], np.float32)
TRACK = np.array([200, 104, 82], np.float32)
LINE = np.array([248, 246, 238], np.float32)

ROAD_W = {'primary': 16, 'secondary': 14, 'tertiary': 11, 'residential': 8, 'unclassified': 8,
          'living_street': 6, 'service': 5}
WALK_W = {'footway': 3.2, 'path': 2.6, 'pedestrian': 6, 'steps': 3.5, 'track': 3.2, 'cycleway': 3}
CENTER_LINE = {'primary', 'secondary', 'tertiary'}


def _seamless(a):
    h, w = a.shape[:2]
    b = np.roll(np.roll(a, h // 2, axis=0), w // 2, axis=1)
    yy, xx = np.mgrid[0:h, 0:w]
    wy = 1 - np.abs(yy / (h - 1) * 2 - 1)
    wx = 1 - np.abs(xx / (w - 1) * 2 - 1)
    m = np.clip(np.minimum(wy, wx) * 2.2, 0, 1)
    if a.ndim == 3:
        m = m[..., None]
    return a * m + b * (1 - m)


def _recolor(t, target):
    """타일의 결은 남기고 평균색만 target 으로"""
    mean = t.reshape(-1, 3).mean(axis=0, dtype=np.float64).astype(np.float32)
    return np.clip(t * (target / mean), 0, 255)


def _smooth_noise(shape, cells, seed):
    rng = np.random.default_rng(seed)
    g = rng.uniform(-1, 1, (cells, max(2, int(cells * shape[1] / shape[0])))).astype(np.float32)
    img = Image.fromarray(((g + 1) * 127.5).astype(np.uint8)).resize((shape[1], shape[0]), Image.BICUBIC)
    return np.asarray(img).astype(np.float32) / 127.5 - 1


class Ground:
    def __init__(self, osm, tiles_dir, height_px=4096, seed=7):
        self.r = r = Raster(height_px)
        self.W, self.H, self.ppm = r.W, r.H, r.ppm
        self.osm = osm
        self.tiles_dir = tiles_dir
        self.rand = random.Random(seed)

        self.forest = self._area(lambda t: t.get('natural') == 'wood' or t.get('landuse') == 'forest' or t.get('leisure') == 'golf_course')
        self.park = self._area(lambda t: t.get('leisure') == 'park' or t.get('landuse') in ('grass', 'flowerbed', 'cemetery') or t.get('natural') == 'grassland')
        self.flowerbed = self._area(lambda t: t.get('landuse') == 'flowerbed')
        self.pitch = self._area(lambda t: t.get('leisure') in ('pitch', 'sports_centre'))
        self.track = self._area(lambda t: t.get('leisure') in ('track', 'stadium'))
        self.stadium = self._area(lambda t: t.get('leisure') == 'stadium')
        self.square = self._area(lambda t: t.get('place') == 'square')
        self.parking = self._area(lambda t: t.get('amenity') == 'parking' or t.get('landuse') == 'construction')
        self.paved = self._area(lambda t: bool(t.get('area:highway')) or (t.get('highway') == 'pedestrian' and t.get('area') == 'yes'))
        self.water = self._area(lambda t: t.get('natural') == 'water' or t.get('landuse') in ('reservoir', 'basin'))

        self.road_lines = list(self._lines(ROAD_W))   # (kind, [(x,z)...]) 월드 좌표
        self.walk_lines = list(self._lines(WALK_W))
        self.walk = self._stroke(self.walk_lines, WALK_W)
        self.curb = self._stroke(self.road_lines, ROAD_W, extra=2.2)
        self.road = self._stroke(self.road_lines, ROAD_W)

        self.apron = np.zeros((self.H, self.W), bool)
        self.campus = np.ones((self.H, self.W), bool)
        self.campus_soft = np.ones((self.H, self.W), np.float32)
        self.lake = np.zeros((self.H, self.W), bool)
        self.buildings = np.zeros((self.H, self.W), bool)
        self.occupied = np.zeros((self.H, self.W), bool)
        self.ao_img = Image.new('L', (self.W, self.H), 0)
        self._ao_draw = ImageDraw.Draw(self.ao_img)
        self.blocked = None

        # 대운동장 범위 (월드) — 조명탑·관중석 배치용
        fa = np.argwhere(self.stadium)
        if len(fa):
            (y0, x0), (y1, x1) = fa.min(axis=0), fa.max(axis=0)
            self.stadium_box = (r.world(x0, y1), r.world(x1, y0))   # (서남, 동북)
        else:
            self.stadium_box = None

    # ---------- OSM → 마스크 ----------

    def _rings(self, e):
        if e['type'] == 'way' and e.get('geometry'):
            g = e['geometry']
            if len(g) >= 4 and g[0] == g[-1]:
                yield 'outer', [self.r.px(*to_world(p['lon'], p['lat'])) for p in g]
        elif e['type'] == 'relation':
            for m in e.get('members', []):
                if m.get('geometry') and m.get('role') in ('outer', 'inner'):
                    yield m['role'], [self.r.px(*to_world(p['lon'], p['lat'])) for p in m['geometry']]

    def _area(self, pred):
        m = Image.new('L', (self.W, self.H), 0)
        d = ImageDraw.Draw(m)
        for e in self.osm:
            if pred(e.get('tags', {})):
                for role, pts in self._rings(e):
                    if len(pts) >= 3:
                        d.polygon(pts, fill=0 if role == 'inner' else 255)
        return np.asarray(m) > 127

    def _lines(self, kinds):
        for e in self.osm:
            t = e.get('tags', {})
            hw = t.get('highway')
            if e['type'] == 'way' and hw in kinds and e.get('geometry') and t.get('area') != 'yes':
                yield hw, [to_world(p['lon'], p['lat']) for p in e['geometry']]

    def _stroke(self, lines, widths, extra=0.0):
        m = Image.new('L', (self.W, self.H), 0)
        d = ImageDraw.Draw(m)
        for hw, pts in lines:
            w = max(2, int(round((widths[hw] + extra) * self.ppm)))
            px = [self.r.px(*p) for p in pts]
            d.line(px, fill=255, width=w, joint='curve')
            rr = w / 2
            for x, y in (px[0], px[-1]):
                d.ellipse((x - rr, y - rr, x + rr, y + rr), fill=255)
        return np.asarray(m) > 127

    def _grow(self, m, meters):
        if meters <= 0:
            return m
        return ndimage.distance_transform_edt(~m) <= meters * self.ppm

    # ---------- 캠퍼스 영역 (경계 안은 화사하게, 밖은 차분하게) ----------

    def compute_campus(self, seeds, smooth_m=18.0):
        """캠퍼스 건물(seeds)을 90 m 넓혀 220 m 닫기 → 구멍 메우고 부드럽게. 결과: self.campus (bool), self.campus_soft (0~1)"""
        img = Image.new('L', (self.W, self.H), 0)
        d = ImageDraw.Draw(img)
        for b in seeds:
            d.polygon([self.r.px(*p) for p in b.poly], fill=255)
        m = np.asarray(img) > 127
        ppm = self.ppm
        # 200 m 넓힌 뒤 110 m 줄이기 = 90 m 넓히기 + 220 m 이하 틈 메우기
        big = ndimage.distance_transform_edt(~m) <= 200 * ppm
        closed = ndimage.distance_transform_edt(big) > 110 * ppm
        closed = ndimage.binary_fill_holes(closed)
        # 캠퍼스와 맞닿은 숲(동쪽 산)도 캠퍼스 땅
        lab, n = ndimage.label(self.forest)
        touch = set(np.unique(lab[closed & self.forest])) - {0}
        closed |= np.isin(lab, list(touch))
        soft = ndimage.gaussian_filter(closed.astype(np.float32), smooth_m * ppm)
        self.campus = soft > 0.5
        self.campus_soft = np.clip((soft - 0.3) / 0.4, 0, 1)
        return self.campus

    def campus_contours(self):
        """캠퍼스 경계선 (월드 좌표 폴리라인 목록, 지도 가장자리는 제외)"""
        from skimage import measure
        out = []
        for c in measure.find_contours(self.campus.astype(np.float32), 0.5):
            pts = [self.r.world(x, y) for y, x in c[::3]]
            out.append(pts)
        return out

    def add_lake(self, center, rx, rz, yaw_deg=0.0):
        """사색의 광장 호수: 둥근 타원 물 + 석재 테 (지면 마스크에 더함)"""
        img = Image.new('L', (self.W, self.H), 0)
        d = ImageDraw.Draw(img)
        a = math.radians(yaw_deg)
        pts = []
        for k in range(72):
            t = 2 * math.pi * k / 72
            # 살짝 부푼 타원 (슈퍼엘립스) → 귀여운 연못 모양
            c, s_ = math.cos(t), math.sin(t)
            ex = math.copysign(abs(c) ** 0.8, c) * rx
            ez = math.copysign(abs(s_) ** 0.8, s_) * rz
            pts.append(self.r.px(center[0] + ex * math.cos(a) - ez * math.sin(a), center[1] + ex * math.sin(a) + ez * math.cos(a)))
        d.polygon(pts, fill=255)
        self.lake_poly = [self.r.world(x, y) for x, y in pts]
        lake = np.asarray(img) > 127
        self.water |= lake
        around = self._grow(lake, 1.6)
        self.square &= ~around
        self.walk &= ~around          # 광장을 가로지르던 보행로 선이 호수 위에 그려지지 않게
        self.walk_lines = [(hw, pts) for hw, pts in self.walk_lines
                           if not any(self.has(lake, x, z) for x, z in pts)]
        self.lake = lake
        return lake

    # ---------- 배치 도우미 ----------

    def add_buildings(self, buildings):
        img = Image.new('L', (self.W, self.H), 0)
        d = ImageDraw.Draw(img)
        for b in buildings:
            d.polygon([self.r.px(*p) for p in b.poly], fill=255)
        self.buildings = np.asarray(img) > 127

    def add_apron(self, p0, p1, width):
        """건물 입구에서 길까지 포장 (월드 점 두 개)"""
        img = Image.new('L', (self.W, self.H), 0)
        d = ImageDraw.Draw(img)
        w = max(2, int(width * self.ppm))
        d.line([self.r.px(*p0), self.r.px(*p1)], fill=255, width=w)
        self.apron |= np.asarray(img) > 127

    def finalize_blocking(self):
        """나무·산울타리가 들어가면 안 되는 곳"""
        g = self._grow
        self.blocked_env = (g(self.road | self.curb, 1.5) | g(self.walk, 1.0) | g(self.paved, 1.5)
                        | g(self.parking, 1.5) | g(self.water, 3.0) | g(self.pitch, 2.5) | g(self.track, 3.0)
                        | g(self.square, 1.5) | g(self.apron, 1.0))
        self.blocked = self.blocked_env | g(self.buildings, 3.0)
        self.blocked_near_bld = self.blocked_env | g(self.buildings, 1.6)

    def _ix(self, x, z):
        px, py = self.r.px(x, z)
        return int(px), int(py)

    def inside(self, x, z, margin=2):
        ix, iy = self._ix(x, z)
        return margin <= ix < self.W - margin and margin <= iy < self.H - margin

    def is_free(self, x, z, radius=0.0, near_building=False):
        """near_building: 건물 바로 옆(1.6 m)까지 허용 (산울타리)"""
        ix, iy = self._ix(x, z)
        if not (0 <= ix < self.W and 0 <= iy < self.H):
            return True   # 지도 밖 (바깥 잔디)
        blocked = self.blocked_near_bld if near_building else self.blocked
        if blocked[iy, ix] or self.occupied[iy, ix]:
            return False
        if radius > 0:
            k = int(radius * self.ppm)
            y0, y1, x0, x1 = max(iy - k, 0), min(iy + k + 1, self.H), max(ix - k, 0), min(ix + k + 1, self.W)
            return not (blocked[y0:y1, x0:x1].any() or self.occupied[y0:y1, x0:x1].any())
        return True

    def has(self, mask, x, z):
        ix, iy = self._ix(x, z)
        return 0 <= ix < self.W and 0 <= iy < self.H and bool(mask[iy, ix])

    def occupy(self, x, z, radius, ao=0.0, ao_radius=None):
        """자리를 차지하고(다른 나무가 못 들어오게) 바닥 그늘을 찍는다."""
        px, py = self.r.px(x, z)
        k = radius * self.ppm
        ix0, iy0 = int(px - k), int(py - k)
        ix1, iy1 = int(px + k) + 1, int(py + k) + 1
        if ix1 > 0 and iy1 > 0 and ix0 < self.W and iy0 < self.H:
            self.occupied[max(iy0, 0):min(iy1, self.H), max(ix0, 0):min(ix1, self.W)] = True
        if ao > 0:
            rr = (ao_radius or radius) * self.ppm
            self._ao_draw.ellipse((px - rr, py - rr, px + rr, py + rr), fill=int(255 * ao))

    def occupy_line(self, a, b, width, ao=0.0):
        pa, pb = self.r.px(*a), self.r.px(*b)
        img = Image.new('L', (self.W, self.H), 0)
        w = max(2, int(width * self.ppm))
        ImageDraw.Draw(img).line([pa, pb], fill=255, width=w)
        self.occupied |= np.asarray(img) > 127
        if ao > 0:
            self._ao_draw.line([pa, pb], fill=int(255 * ao), width=int(w * 1.5))

    def nearest_walk_point(self, x, z, max_dist=45.0):
        """가장 가까운 보행로·차도 위 점 (월드). 없으면 None"""
        from .geo import dist_to_segment
        best, bp = max_dist, None
        for _, pts in self.walk_lines + self.road_lines:
            for a, b in zip(pts, pts[1:]):
                if abs(a[0] - x) > best + 60 and abs(b[0] - x) > best + 60:
                    continue
                d, q = dist_to_segment(x, z, a, b)
                if d < best:
                    best, bp = d, q
        return bp

    # ---------- 타일 ----------

    def _tile(self, name, meters, crop=None, median=0, blur=0.0):
        img = Image.open(f'{self.tiles_dir}/{name}.png').convert('RGB')
        if median:
            img = img.filter(ImageFilter.MedianFilter(median))
        if blur:
            img = img.filter(ImageFilter.GaussianBlur(blur))
        if crop:
            w, h = img.size
            img = img.crop((int(crop[0] * w), int(crop[1] * h), int(crop[2] * w), int(crop[3] * h)))
        t = _seamless(np.asarray(img).astype(np.float32))
        size = max(8, int(round(meters * self.ppm)))
        t = np.asarray(Image.fromarray(t.astype(np.uint8)).resize((size, size), Image.LANCZOS)).astype(np.float32)
        reps = (math.ceil(self.H / size) + 1, math.ceil(self.W / size) + 1, 1)
        return np.tile(t, reps)[:self.H, :self.W]

    # ---------- 그리기 ----------

    def paint(self, out_path, detail_path=None):
        H, W, ppm = self.H, self.W, self.ppm
        print('ground: tiles', W, H)
        grass = _recolor(self._tile('grass', 34, median=7, blur=2.0), GRASS)
        # 큰 얼룩: 밝은 연두 / 짙은 초록이 넓게 번갈아 (키아트 잔디의 부드러운 결)
        n1 = _smooth_noise((H, W), 26, 1)[..., None]
        n2 = _smooth_noise((H, W), 70, 2)[..., None]
        grass = grass * (1 + 0.04 * n1 + 0.03 * n2) + np.array([6, 3, -3], np.float32) * n1
        canvas = grass.copy()

        def blend(mask, tex, soften=1.0):
            nonlocal canvas
            m = mask.astype(np.float32)
            if soften:
                m = ndimage.gaussian_filter(m, soften)
            m = m[..., None]
            canvas = canvas * (1 - m) + tex * m

        def flat(c):
            return np.broadcast_to(c, canvas.shape)

        # 숲 바닥: 조금 짙게 (나무 사이가 휑하지 않게)
        forest_soft = ndimage.gaussian_filter(self.forest.astype(np.float32), 6 * ppm)[..., None]
        canvas = canvas * (1 - 0.12 * forest_soft) + np.array([-4, 2, 4], np.float32) * forest_soft

        # 화단: 짙은 초록 바탕에 꽃 점
        bed = ndimage.gaussian_filter(self.flowerbed.astype(np.float32), 0.8)[..., None]
        canvas = canvas * (1 - 0.18 * bed)
        dots = Image.new('RGB', (W, H), (0, 0, 0))
        dd = ImageDraw.Draw(dots)
        ys, xs = np.nonzero(self.flowerbed[::3, ::3])
        rng = np.random.default_rng(3)
        palette = [(244, 150, 184), (252, 236, 240), (250, 214, 92), (236, 112, 150)]
        for i in rng.choice(len(xs), size=min(len(xs), 60000), replace=False) if len(xs) else []:
            if rng.random() < 0.35:
                x, y = xs[i] * 3, ys[i] * 3
                rr = 0.28 * ppm
                dd.ellipse((x - rr, y - rr, x + rr, y + rr), fill=palette[rng.integers(len(palette))])
        dots_a = np.asarray(dots).astype(np.float32)
        dm = (dots_a.sum(axis=2) > 0)[..., None]
        canvas = np.where(dm, dots_a, canvas)

        # 길가 꽃 띠: 공원 잔디에서 보행로 가장자리 1~2.5 m 에 분홍·노랑·흰 꽃 점 (지오메트리 없이 텍스처로)
        d_walk = ndimage.distance_transform_edt(~(self.walk | self.square)) / ppm
        strip = (d_walk > 1.0) & (d_walk < 2.5) & self.park & ~self.forest
        ys, xs = np.nonzero(strip[::2, ::2])
        if len(xs):
            band = Image.new('RGB', (W, H), (0, 0, 0))
            bd = ImageDraw.Draw(band)
            # 길을 따라 띠가 끊겼다 이어지게: 큰 얼룩 노이즈로 구간 선택
            sel = _smooth_noise((H, W), 160, 5)
            for i in rng.choice(len(xs), size=min(len(xs), 120000), replace=False):
                x, y = xs[i] * 2, ys[i] * 2
                if sel[y, x] < 0.15 or rng.random() > 0.55:
                    continue
                rr = 0.24 * ppm
                bd.ellipse((x - rr, y - rr, x + rr, y + rr), fill=palette[rng.integers(len(palette))])
            ba = np.asarray(band).astype(np.float32)
            canvas = np.where((ba.sum(axis=2) > 0)[..., None], ba, canvas)

        # 운동장: 붉은 트랙, 줄무늬 잔디 구장, 흰 선
        paving_t = self._tile('paving', 14)
        track_tex = _recolor(paving_t, TRACK)
        blend(self.track, track_tex)
        field = ndimage.binary_erosion(self.stadium, iterations=int(12 * ppm))
        yy, xx = np.mgrid[0:H, 0:W]
        stripes = (((xx + yy * 0.0) // int(6 * ppm)) % 2).astype(np.float32)[..., None]
        pitch_tex = np.clip(grass * (1.04 + 0.06 * stripes) + np.array([-6, 8, -4], np.float32), 0, 255)
        blend(field, pitch_tex)
        blend(self.pitch, pitch_tex)
        lines = Image.new('L', (W, H), 0)
        ld = ImageDraw.Draw(lines)
        fa = np.argwhere(field)
        if len(fa):
            (y0, x0), (y1, x1) = fa.min(axis=0), fa.max(axis=0)
            cx, cy, lw = (x0 + x1) / 2, (y0 + y1) / 2, max(2, int(0.5 * ppm))
            m4 = 4 * ppm
            ld.rectangle((x0 + m4, y0 + m4, x1 - m4, y1 - m4), outline=255, width=lw)
            if (y1 - y0) > (x1 - x0):
                ld.line((x0 + m4, cy, x1 - m4, cy), fill=255, width=lw)
                for sy in (y0 + m4, y1 - m4):   # 골 에어리어
                    dy = 16 * ppm * (1 if sy < cy else -1)
                    ld.rectangle((cx - 20 * ppm, min(sy, sy + dy), cx + 20 * ppm, max(sy, sy + dy)), outline=255, width=lw)
            else:
                ld.line((cx, y0 + m4, cx, y1 - m4), fill=255, width=lw)
                for sx in (x0 + m4, x1 - m4):
                    dx = 16 * ppm * (1 if sx < cx else -1)
                    ld.rectangle((min(sx, sx + dx), cy - 20 * ppm, max(sx, sx + dx), cy + 20 * ppm), outline=255, width=lw)
            r9 = 9 * ppm
            ld.ellipse((cx - r9, cy - r9, cx + r9, cy + r9), outline=255, width=lw)
        # 트랙 레인 (흰 줄) — 트랙 안쪽 경계에서 1.2 m 간격
        track_only = self.track & ~field
        if track_only.any():
            d_in = ndimage.distance_transform_edt(~field) / ppm
            lane = np.zeros_like(track_only)
            for k in range(1, 7):
                lane |= np.abs(d_in - 1.2 * k) < 0.09
            lines_arr = np.maximum(np.asarray(lines), (lane & track_only).astype(np.uint8) * 200)
        else:
            lines_arr = np.asarray(lines)
        blend(lines_arr > 0, flat(LINE), 0.5)

        # 포장: 광장, 주차장, 보행 포장, 입구 앞
        plaza = _recolor(self._tile('plaza', 18), PLAZA)
        paving = _recolor(paving_t, PAVING)
        parking = _recolor(paving_t, PARKING)
        blend(self.square, plaza)
        blend(self.paved, plaza)
        blend(self.parking, parking)
        blend(self.apron, paving, 0.8)

        # 물: 가장자리는 밝은 청록, 안쪽은 깊은 파랑 + 물결 무늬, 둘레 석재 테
        water_t = self._tile('water', 22)
        lum = water_t.mean(axis=2, keepdims=True)
        caust = np.clip((lum - lum.mean()) / 60, -0.6, 1.2)
        d_w = (ndimage.distance_transform_edt(self.water) / ppm)[..., None]
        wt = np.clip(d_w / 9, 0, 1)
        water = WATER_EDGE * (1 - wt) + WATER_DEEP * wt + caust * 38
        water = np.where(d_w < 0.7, water * 0.6 + np.array([235, 248, 250], np.float32) * 0.4, water)
        blend(self.water, np.clip(water, 0, 255), 0.6)
        rim = self._grow(self.water, 1.4) & ~self.water
        blend(rim, flat(STONE_RIM), 0.5)

        # 도로: 연석 → 아스팔트 → 중앙 점선 → 횡단보도
        print('ground: roads')
        walk_m = self.walk
        blend(walk_m, paving, 0.7)
        blend(self.curb, flat(CURB), 0.7)
        asphalt = _recolor(self._tile('road', 9, crop=(0.10, 0.0, 0.40, 1.0)), ASPHALT)
        blend(self.road, asphalt, 0.7)
        marks = Image.new('L', (W, H), 0)
        md = ImageDraw.Draw(marks)
        lw = max(2, int(0.3 * ppm))
        for hw, pts in self.road_lines:
            if hw not in CENTER_LINE:
                continue
            for a, b in zip(pts, pts[1:]):
                L = math.hypot(b[0] - a[0], b[1] - a[1])
                n = int(L // 7)
                for i in range(n):
                    t0, t1 = (i * 7 + 1) / L, (i * 7 + 4) / L
                    p0 = (a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0)
                    p1 = (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)
                    md.line([self.r.px(*p0), self.r.px(*p1)], fill=255, width=lw)
        self._crosswalks(md)
        blend(np.asarray(marks) > 0, flat(LINE), 0.5)

        # 포장 가장자리 선: 포장 안쪽 0.35 m 를 살짝 어둡게 (타일 테두리처럼 또렷하게)
        light_paved = walk_m | self.square | self.paved | self.apron | self.parking
        light_paved &= ~self.road
        d_in = ndimage.distance_transform_edt(light_paved) / ppm
        edge = ((d_in > 0) & (d_in < 0.35)).astype(np.float32)
        canvas *= (1 - 0.12 * ndimage.gaussian_filter(edge, 0.6))[..., None]

        # 잔디 쪽 경계 그늘: 포장·물 바로 옆 잔디를 살짝 어둡게 (턱이 있는 느낌)
        hard = light_paved | self.road | self.curb | rim | self.track
        d_out = ndimage.distance_transform_edt(~hard) / ppm
        canvas *= (1 - 0.16 * np.exp(-d_out / 0.6) * (d_out > 0))[..., None]

        # 건물 둘레 그늘 (접지감)
        d_b = ndimage.distance_transform_edt(~self.buildings) / ppm
        canvas *= (1 - 0.32 * np.exp(-d_b / 2.2) * (d_b > 0))[..., None]

        # 나무·산울타리 아래 그늘
        ao = ndimage.gaussian_filter(np.asarray(self.ao_img).astype(np.float32) / 255, 0.9 * ppm)
        canvas *= (1 - ao)[..., None]

        # 담장 안쪽 꽃 띠: 캠퍼스 경계에서 안으로 2~5 m (길·물 제외)
        d_in = ndimage.distance_transform_edt(self.campus) / ppm
        band = (d_in > 2.0) & (d_in < 5.0) & ~(self.road | self.curb | self.walk | self.water | self.buildings | self.parking)
        ys, xs = np.nonzero(band[::2, ::2])
        if len(xs):
            fl = Image.new('RGB', (W, H), (0, 0, 0))
            fd = ImageDraw.Draw(fl)
            for i in rng.choice(len(xs), size=min(len(xs), 90000), replace=False):
                x, y = xs[i] * 2, ys[i] * 2
                rr = 0.26 * ppm
                fd.ellipse((x - rr, y - rr, x + rr, y + rr), fill=palette[rng.integers(len(palette))])
            fa = np.asarray(fl).astype(np.float32)
            bed_m = ndimage.gaussian_filter(band.astype(np.float32), 0.6 * ppm)[..., None]
            canvas = canvas * (1 - 0.12 * bed_m)
            canvas = np.where((fa.sum(axis=2) > 0)[..., None], fa, canvas)

        # 캠퍼스 경계 안팎 대비: 밖은 채도를 45% 빼고 밝은 회녹색 쪽으로, 안은 채도 +8% · 따뜻하게
        cs = self.campus_soft[..., None]
        lum = canvas.mean(axis=2, keepdims=True)
        outside = lum + (canvas - lum) * 0.55
        outside = outside * 0.86 + np.array([214, 220, 210], np.float32) * 0.14
        inside = np.clip(lum + (canvas - lum) * 1.08 + np.array([3, 2, -3], np.float32), 0, 255)
        canvas = outside * (1 - cs) + inside * cs

        out = np.clip(canvas, 0, 255).astype(np.uint8)
        Image.fromarray(out).save(out_path, quality=90)
        g = out[self.park & ~self.forest]
        print('ground: saved', out_path, 'park grass mean', g.reshape(-1, 3).mean(axis=0).round(1) if len(g) else '-')

        if detail_path:
            self._detail(detail_path)

    def paint_lights(self, path, lights, size=2048):
        """밤 바닥 불빛 지도 (회색 1채널, 지면 텍스처와 같은 배치): 가로등·현관·담장 등 아래 부드러운 빛 웅덩이"""
        h = size
        w = round(size * self.W / self.H)
        ppm = h / self.H * self.ppm
        acc = np.zeros((h, w), np.float32)
        img = Image.new('L', (w, h), 0)
        d = ImageDraw.Draw(img)
        for x, z, r, s in lights:
            px, py = self.r.px(x, z)
            px, py = px * w / self.W, py * h / self.H
            rr = r * ppm
            d.ellipse((px - rr, py - rr, px + rr, py + rr), fill=int(255 * min(1.0, s)))
        pts = np.asarray(img).astype(np.float32) / 255
        # 등 아래 또렷한 웅덩이 + 25 m 에 걸쳐 넓게 번지는 따뜻한 빛 (등이 모인 곳일수록 밝은 동네 불빛)
        tight = ndimage.gaussian_filter(pts, 2.5 * ppm)
        wide = ndimage.gaussian_filter(pts, 14.0 * ppm)
        wide = wide / (np.percentile(wide[wide > 0.001], 95) + 1e-6) if (wide > 0.001).any() else wide
        acc = np.clip(tight * 1.3 + np.clip(wide, 0, 1.4) * 0.45, 0, 1) ** 0.85
        Image.fromarray((acc * 255).astype(np.uint8)).save(path)
        print('lights', len(lights), path)

    def _crosswalks(self, md):
        """보행로가 차도를 가로지르는 곳에 얼룩말 무늬"""
        stripe_w = max(2, int(0.55 * self.ppm))
        done = []
        for hw, rpts in self.road_lines:
            if hw == 'service':
                continue
            half = ROAD_W[hw] / 2
            for _, wpts in self.walk_lines:
                for a, b in zip(rpts, rpts[1:]):
                    for c, d in zip(wpts, wpts[1:]):
                        p = _seg_intersect(a, b, c, d)
                        if p is None or any(math.hypot(p[0] - q[0], p[1] - q[1]) < 12 for q in done):
                            continue
                        done.append(p)
                        L = math.hypot(b[0] - a[0], b[1] - a[1]) or 1
                        rx, rz = (b[0] - a[0]) / L, (b[1] - a[1]) / L
                        nx, nz = -rz, rx
                        s = -half + 0.8
                        while s < half - 0.6:
                            cx, cz = p[0] + nx * s, p[1] + nz * s
                            p0 = (cx - rx * 1.6, cz - rz * 1.6)
                            p1 = (cx + rx * 1.6, cz + rz * 1.6)
                            md.line([self.r.px(*p0), self.r.px(*p1)], fill=255, width=stripe_w)
                            s += 1.1

    def _detail(self, path):
        """셰이더용 잔디 결 텍스처 (회색, 평균 128, 이음매 없음)"""
        img = Image.open(f'{self.tiles_dir}/grass.png').convert('L').resize((256, 256), Image.LANCZOS)
        a = _seamless(np.asarray(img).astype(np.float32))
        a = a - ndimage.gaussian_filter(a, 24, mode='wrap')
        a = 128 + a / (np.abs(a).max() + 1e-6) * 90
        Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(path)


def _seg_intersect(a, b, c, d):
    d1 = (b[0] - a[0], b[1] - a[1])
    d2 = (d[0] - c[0], d[1] - c[1])
    den = d1[0] * d2[1] - d1[1] * d2[0]
    if abs(den) < 1e-9:
        return None
    t = ((c[0] - a[0]) * d2[1] - (c[1] - a[1]) * d2[0]) / den
    u = ((c[0] - a[0]) * d1[1] - (c[1] - a[1]) * d1[0]) / den
    if 0 <= t <= 1 and 0 <= u <= 1:
        return (a[0] + d1[0] * t, a[1] + d1[1] * t)
    return None
