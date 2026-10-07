"""
지형 높낮이. 큰 특징만 또렷하게, 나머지는 아주 완만하게:
  - 동쪽 숲 산: 숲 안쪽으로 갈수록 최대 18 m 까지 부드럽게 오른다
  - 제2기숙사 → 중앙도서관 사이 언덕길: 길 가운데쯤 9 m 높이의 타원 언덕
  - 그 밖: 0~1.8 m 의 아주 큰 물결
건물·물·운동장·광장 아래는 평평한 터(평균 높이)로 다지고 둘레 18 m 에 걸쳐 부드럽게 이어 붙인다.
지도 가장자리 120 m 는 0 으로 내려가 지도 밖 잔디(y = -0.3)와 만난다.
"""
import math
import struct

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

from .geo import MAP_H, MAP_W

CELL = 2.0           # 높이 격자 (m)
MESH_CELL = 8.0      # 지면 메시 격자 (m)

DORM = (-270.0, -95.0)        # 제2기숙사 남자동·여자동 사이
LIBRARY = (-34.0, -351.0)     # 중앙도서관


def _smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


class Terrain:
    def __init__(self, ground, buildings, hidden_ids=(), seed=11):
        nx, nz = int(MAP_W / CELL) + 1, int(MAP_H / CELL) + 1
        self.nx, self.nz = nx, nz
        xs = -MAP_W / 2 + np.arange(nx) * CELL
        zs = -MAP_H / 2 + np.arange(nz) * CELL
        X, Z = np.meshgrid(xs, zs)          # [z, x]
        self.xs, self.zs = xs, zs

        # 지면 마스크를 높이 격자로 (지면 텍스처: 위가 북쪽 → 격자: z 오름차순이 북쪽)
        def to_grid(mask):
            img = Image.fromarray(mask.astype(np.uint8) * 255).resize((nx, nz), Image.BILINEAR)
            return np.flipud(np.asarray(img).astype(np.float32) / 255)

        # 1) 아주 큰 물결
        rng = np.random.default_rng(seed)
        coarse = rng.uniform(-1, 1, (8, 8)).astype(np.float32)
        wave = np.asarray(Image.fromarray(((coarse + 1) * 127.5).astype(np.uint8)).resize((nx, nz), Image.BICUBIC)).astype(np.float32) / 127.5 - 1
        h = (wave + 1.0) * 0.9          # 0 ~ 1.8 m (지도 밖 잔디 y = -0.3 아래로 내려가지 않게)

        # 2) 동쪽 숲 산: 숲 안쪽 깊이에 따라
        forest = to_grid(ground.forest) > 0.5
        depth = ndimage.distance_transform_edt(forest) * CELL
        depth = ndimage.gaussian_filter(depth, 12)
        h += 18.0 * _smoothstep(10, 160, depth)

        # 3) 기숙사 → 도서관 언덕: 두 점 사이 가운데에 길 방향으로 긴 타원 언덕
        mx, mz = (DORM[0] + LIBRARY[0]) / 2, (DORM[1] + LIBRARY[1]) / 2
        ux, uz = LIBRARY[0] - DORM[0], LIBRARY[1] - DORM[1]
        L = math.hypot(ux, uz)
        ux, uz = ux / L, uz / L
        a = (X - mx) * ux + (Z - mz) * uz
        b = -(X - mx) * uz + (Z - mz) * ux
        h += 9.0 * np.exp(-(a / (L * 0.45)) ** 2 - (b / 110.0) ** 2)

        # 4) 지도 가장자리로 갈수록 0
        edge = np.minimum.reduce([X + MAP_W / 2, MAP_W / 2 - X, Z + MAP_H / 2, MAP_H / 2 - Z])
        h *= _smoothstep(0, 120, edge)

        # 5) 평평한 터: 건물(숨긴 랜드마크 포함)·물·운동장·광장·주차장
        lab_img = Image.new('I', (nx, nz), 0)
        d = ImageDraw.Draw(lab_img)
        k = 0
        for bb in buildings:
            k += 1
            pts = [((x + MAP_W / 2) / CELL, (z + MAP_H / 2) / CELL) for x, z in bb.poly]
            d.polygon(pts, fill=k)
        labels = np.asarray(lab_img).astype(np.int32)       # 이미지 y 가 z 오름차순 (뒤집지 않음)
        flat_masks = [ground.water, ground.track | ground.pitch | ground.stadium, ground.square, ground.parking]
        for fm in flat_masks:
            g = to_grid(fm) > 0.5
            lab, n = ndimage.label(g)
            labels = np.where((lab > 0) & (labels == 0), lab + k, labels)
            k += n
        pad = labels > 0
        ids = np.arange(1, k + 1)
        means = ndimage.mean(h, labels, ids) if k else []
        level = np.zeros(k + 1, np.float32)
        level[1:] = np.nan_to_num(np.asarray(means, np.float32))
        # 터 밖은 가장 가까운 터의 높이로, 거리에 따라 섞는다
        dist, (iz, ix) = ndimage.distance_transform_edt(~pad, return_indices=True)
        dist = dist * CELL
        near_level = level[labels[iz, ix]]
        w = 1.0 - _smoothstep(4.0, 22.0, dist)
        h = h * (1 - w) + near_level * w
        self.h = ndimage.gaussian_filter(h, 1.0).astype(np.float32)
        self.pad_level = {}
        for i, bb in enumerate(buildings, start=1):
            self.pad_level.setdefault(bb.id, float(level[i]))

    def at(self, x, z):
        """쌍선형 보간 높이 (지도 밖은 0)"""
        fx = (x + MAP_W / 2) / CELL
        fz = (z + MAP_H / 2) / CELL
        if fx < 0 or fz < 0 or fx >= self.nx - 1 or fz >= self.nz - 1:
            return 0.0
        i, j = int(fx), int(fz)
        tx, tz = fx - i, fz - j
        h = self.h
        return float((h[j, i] * (1 - tx) + h[j, i + 1] * tx) * (1 - tz) + (h[j + 1, i] * (1 - tx) + h[j + 1, i + 1] * tx) * tz)

    def displace(self, builder):
        """조립된 메시의 모든 정점을 지형 높이만큼 올린다 (건물·물 터는 평평해서 모양이 그대로)"""
        if not builder.pos:
            return
        p = np.asarray(builder.pos, np.float64).reshape(-1, 3)
        fx = np.clip((p[:, 0] + MAP_W / 2) / CELL, 0, self.nx - 1.001)
        fz = np.clip((p[:, 2] + MAP_H / 2) / CELL, 0, self.nz - 1.001)
        inside = ((p[:, 0] > -MAP_W / 2) & (p[:, 0] < MAP_W / 2) & (p[:, 2] > -MAP_H / 2) & (p[:, 2] < MAP_H / 2))
        i, j = fx.astype(int), fz.astype(int)
        tx, tz = fx - i, fz - j
        h = self.h
        val = (h[j, i] * (1 - tx) + h[j, i + 1] * tx) * (1 - tz) + (h[j + 1, i] * (1 - tx) + h[j + 1, i + 1] * tx) * tz
        p[:, 1] += np.where(inside, val, 0.0)
        builder.pos = p.reshape(-1).tolist()

    def mesh_layer(self, layer):
        """지면 메시 (8 m 격자, 각진 면). 이름 Terrain_n → Unity 는 지면 재질, UV 는 셰이더가 월드 좌표로 계산"""
        from .mesh import IDENT
        step = int(MESH_CELL / CELL)
        hs = self.h[::step, ::step]
        xs, zs = self.xs[::step], self.zs[::step]
        for j in range(len(zs) - 1):
            for i in range(len(xs) - 1):
                x0, x1, z0, z1 = xs[i], xs[i + 1], zs[j], zs[j + 1]
                p00 = (x0, float(hs[j, i]), z0)
                p10 = (x1, float(hs[j, i + 1]), z0)
                p01 = (x0, float(hs[j + 1, i]), z1)
                p11 = (x1, float(hs[j + 1, i + 1]), z1)
                mb = layer.at((x0 + x1) / 2, (z0 + z1) / 2)
                mb.ao_strength = 0.0
                below = ((x0 + x1) / 2, min(p00[1], p11[1]) - 5, (z0 + z1) / 2)
                mb.tri(IDENT, p00, p10, p11, (1, 1, 1), below)
                mb.tri(IDENT, p00, p11, p01, (1, 1, 1), below)

    def write_heightmap(self, path):
        """Unity KeyArtTerrain 이 읽는 높이 격자: 'KUHT' int32 nx, nz, float32 cell, originX, originZ, float16[nz*nx] (z 오름차순)"""
        with open(path, 'wb') as f:
            f.write(b'KUHT')
            f.write(struct.pack('<iifff', self.nx, self.nz, CELL, -MAP_W / 2, -MAP_H / 2))
            f.write(self.h.astype('<f2').tobytes())
