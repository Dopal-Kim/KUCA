"""
수집 동물 3D 모델 만들기 (kuca_art/creatures.py) → My project/Assets/Resources/KUCA/CreatureMeshes.bytes

    cd tools/keyart
    python3 build_creatures.py        (numpy 필요: uv run --with numpy python build_creatures.py)

형식은 KeyArtGeometry.bytes 와 같은 v3 (mesh.py write_bytes). 메시 이름 = Creature_<id>.
Unity CreatureLibrary 가 Resources 에서 읽어 같은 위치·색 정점을 합쳐 부드러운 법선으로 그린다.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuca_art import creatures
from kuca_art.mesh import write_bytes

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'My project', 'Assets', 'Resources', 'KUCA')

import sys as _sys
from kuca_art import figures

# 고품질 피규어(kuca_art/figures.py, figs/)가 있으면 그것을, 없으면 예전 단순 모델을 쓴다.
# 지도용은 가볍게 (삼각형 MAP_TRIS), 도감 확대용 고품질은 CreatureMeshesHQ.bytes (HQ_TRIS)
MAP_TRIS, HQ_TRIS = 9000, 60000
hq = '--hq' in _sys.argv
only = [a.split('=')[1].split(',') for a in _sys.argv if a.startswith('--only=')]
only = only[0] if only else None
old = dict(creatures.build_all())
meshes = []
for cid in creatures.CREATURES:
    if cid in figures.FIGURES and (only is None or cid in only):
        mb, glass = figures.build(cid, HQ_TRIS if hq else MAP_TRIS, voxel=0.04 if hq else 0.05)
        meshes.append((f'Creature_{cid}', mb))
        if glass:
            meshes.append((f'Creature_{cid}__glass', glass))
        print('figure', cid)
    else:
        meshes.append((f'Creature_{cid}', old[f'Creature_{cid}']))
n = write_bytes(os.path.join(OUT, 'CreatureMeshesHQ.bytes' if hq else 'CreatureMeshes.bytes'), meshes, pos_unit=creatures.POS_UNIT)
for name, mb in meshes:
    print(f'{name}: {len(mb.pos) // 9} tris')
info = [{'id': cid, 'name': nm, 'tier': tier} for cid, (nm, tier, _) in creatures.CREATURES.items()]
with open(os.path.join(HERE, 'preview', 'creatures.json'), 'w', encoding='utf-8') as f:
    json.dump(info, f, ensure_ascii=False)
print('total verts', n)
