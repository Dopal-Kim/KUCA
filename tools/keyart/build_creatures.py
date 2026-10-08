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

# 고품질 피규어(kuca_art/figures.py, figs/) 로 지도용 두 단계 + 도감용 고품질을 만든다.
#   CreatureMeshes.bytes     가까이 (LOD0, 약 18k 삼각형)  — 확대했을 때 매끈하게
#   CreatureMeshesFar.bytes  멀리   (LOD1, 약 5k 삼각형)   — 지도에서 여러 마리가 떠 있어도 부드럽게
#   --hq → CreatureMeshesHQ.bytes (도감 확대용 60k)
LEVELS = {'near': ('CreatureMeshes.bytes', 13000, 0.045, 0.4, 6000),
          'far': ('CreatureMeshesFar.bytes', 3000, 0.06, 0.2, 1500),
          'hq': ('CreatureMeshesHQ.bytes', 60000, 0.04, 1.0, None)}
hq = '--hq' in _sys.argv
only = [a.split('=')[1].split(',') for a in _sys.argv if a.startswith('--only=')]
only = only[0] if only else None
levels = ['hq'] if hq else ['near', 'far']
old = dict(creatures.build_all())
for lv in levels:
    fname, tris, voxel, lod, cap = LEVELS[lv]
    meshes = []
    for cid in creatures.CREATURES:
        if cid in figures.FIGURES and (only is None or cid in only):
            mb, glass = figures.build(cid, tris, voxel=voxel, lod=lod, extra_cap=cap)
            meshes.append((f'Creature_{cid}', mb))
            if glass:
                meshes.append((f'Creature_{cid}__glass', glass))
            print(lv, cid, len(mb.pos) // 9, 'tris', flush=True)
        else:
            meshes.append((f'Creature_{cid}', old[f'Creature_{cid}']))
    n = write_bytes(os.path.join(OUT, fname), meshes, pos_unit=creatures.POS_UNIT)
    print(lv, 'total verts', n, flush=True)
info = [{'id': cid, 'name': nm, 'tier': tier} for cid, (nm, tier, _) in creatures.CREATURES.items()]
with open(os.path.join(HERE, 'preview', 'creatures.json'), 'w', encoding='utf-8') as f:
    json.dump(info, f, ensure_ascii=False)
