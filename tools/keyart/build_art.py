"""
키아트 지도 한 번에 만들기: 지면 텍스처 + 건물 디테일·랜드마크·산울타리·나무 지오메트리 + 매니페스트.

    cd tools/keyart
    uv run --with numpy --with pillow --with scipy --with scikit-image python build_art.py

결과 (My project/Assets/Art/KeyArt):
  CampusGround.jpg      지면 텍스처 (지도 1418 x 1548 m 전체)
  GrassDetail.png       잔디 결 디테일 (셰이더가 가까이서 겹쳐 씀)
  KeyArtGeometry.bytes  버텍스 색 로우폴리 메시 (Unity KUCA → Map Style → Apply Key Art 가 읽음)
  KeyArtManifest.json   건물별 외벽 스타일, 숨길 상자 건물
"""
import json
import os
import random
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from kuca_art import buildings as bld, landmarks, nature
from kuca_art.geo import MAP_H, MAP_W, load_buildings, load_osm
from kuca_art.ground import Ground
from kuca_art.mesh import Layer, round_corners, write_bytes

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(HERE, '..', '..', 'My project', 'Assets')
OUT = os.path.join(ASSETS, 'Art', 'KeyArt')


def main():
    t0 = time.time()
    tiles = os.path.join(HERE, 'tiles')
    if not os.path.exists(os.path.join(tiles, 'grass.png')):
        os.makedirs(tiles, exist_ok=True)
        import crop_tiles  # noqa: F401  (실행하면 tiles/*.png 를 만든다)

    rng = random.Random(7)
    look = bld.load_look(os.path.join(OUT, 'KeyArtLook.json'))
    buildings = load_buildings(os.path.join(ASSETS, 'Data', 'CampusBuildings.json'))
    # 미니어처 비율: 건물 높이를 키운다 (Unity 는 Buildings 루트의 Y 배율로 같은 값을 적용)
    hs = look.get('buildingHeightScale', 1.0)
    # 실제 층수로 높이 보정 (Unity 는 매니페스트 heights 로 건물마다 같은 배율을 적용)
    height_fix = {}
    for b in buildings:
        lv = landmarks.REAL_LEVELS.get(b.id)
        if lv and b.min_h < 0.5:
            new_h = lv * landmarks.LEVEL_H
            height_fix[b.id] = round(new_h / b.h, 4)
            b.h = new_h
    for b in buildings:
        b.h *= hs
        b.min_h *= hs
    # 귀여운 덩어리감: 모든 건물 모서리를 둥글린다 (외벽·기단·난간·산울타리가 같은 외곽을 따른다)
    for b in buildings:
        if b.id not in landmarks.HIDDEN:
            b.poly = round_corners(b.poly, radius=2.8 if b.area > 400 else 1.6)
    osm = load_osm(os.path.join(HERE, 'osm_ground.json'))
    print(f'buildings {len(buildings)}, osm {len(osm)}')

    ground = Ground(osm, tiles)
    # 캠퍼스 영역: 대학 건물에서 넓혀 만든 부드러운 경계 (안은 화사하게, 밖은 차분하게)
    seeds = [b for b in buildings if landmarks.is_campus_seed(b)]
    ground.compute_campus(seeds)
    bld.OUTSIDE_IDS.update(b.id for b in buildings if not ground.has(ground.campus, *b.centroid))
    # 캠퍼스 밖은 낮게 (배경으로 물러나게): 높이 0.75배, Unity 도 매니페스트로 같은 배율
    for b in buildings:
        if b.id in bld.OUTSIDE_IDS and b.id not in landmarks.REAL_LEVELS:
            b.h *= 0.75
            b.min_h *= 0.75
            height_fix[b.id] = round(height_fix.get(b.id, 1.0) * 0.75, 4)
    lake = landmarks.plan_lake(ground, osm)
    print(f'campus seeds {len(seeds)}, outside buildings {len(bld.OUTSIDE_IDS)}, lake {lake}')
    ground.add_buildings([b for b in buildings if b.id not in landmarks.HIDDEN])
    entrances = bld.plan_entrances(buildings, ground, landmarks.NO_DETAILS)
    ground.finalize_blocking()
    print(f'entrances {len(entrances)}  ({time.time() - t0:.0f}s)')

    layer = Layer('KeyArt', MAP_W / 2, MAP_H / 2, grid=6)
    # 건물 외벽 덩어리: 외벽 셰이더 재질로 그린다 (스타일마다 레이어, Unity 는 이름 앞부분으로 재질을 고른다)
    shells = {st: Layer(f'Shell{st}', MAP_W / 2, MAP_H / 2, grid=3, aux=True) for st in bld.PALETTE}
    bld.HEIGHT_SCALE = hs
    landmarks.build(buildings, layer, ground)
    print('axis', landmarks.gate_avenue(layer, ground), landmarks.plaza_ring(layer, ground, osm))
    if lake:
        landmarks.lake_features(layer, ground, lake)
    print('campus wall', landmarks.campus_wall(layer, ground, rng))
    print('gate fountain', landmarks.gate_fountain(layer, ground), 'pavilions', landmarks.pavilions(layer, ground, rng))
    bld.build_shells(buildings, shells, landmarks.HIDDEN)
    bld.build_details(buildings, layer, shells, entrances, landmarks.NO_DETAILS, rng)
    hedges = bld.build_hedges(buildings, layer, ground, entrances, landmarks.NO_DETAILS, rng)
    print(f'hedges {hedges}  ({time.time() - t0:.0f}s)')
    counts = nature.plant_all(layer, ground, buildings, rng, landmarks.NO_DETAILS)
    print('plants', counts, f'({time.time() - t0:.0f}s)')
    print('props', nature.place_props(layer, ground, rng))

    ground.paint(os.path.join(OUT, 'CampusGround.jpg'), os.path.join(OUT, 'GrassDetail.png'))
    meshes = list(layer.meshes())
    for sl in shells.values():
        meshes += list(sl.meshes())
    verts = write_bytes(os.path.join(OUT, 'KeyArtGeometry.bytes'), meshes)
    print(f'geometry: {len(meshes)} meshes, {verts} verts, {verts // 3} tris')

    styles = sorted({(b.id, bld.style_of(b)) for b in buildings if bld.style_of(b) != 'Default'})
    manifest = {
        'styles': [{'id': i, 'style': s} for i, s in styles],
        'hidden': sorted(landmarks.HIDDEN),
        'heights': [{'id': i, 'scale': v} for i, v in sorted(height_fix.items())],
        'replaceBuildings': True,   # 상자 건물은 숨기고 Shell 메시(둥근 모서리)가 보인다
    }
    with open(os.path.join(OUT, 'KeyArtManifest.json'), 'w', encoding='utf-8') as f:
        json.dump(manifest, f, ensure_ascii=False, indent=1)
    print(f'done in {time.time() - t0:.0f}s')


if __name__ == '__main__':
    main()
