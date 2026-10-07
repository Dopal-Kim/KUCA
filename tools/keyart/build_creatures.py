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

meshes = creatures.build_all()
n = write_bytes(os.path.join(OUT, 'CreatureMeshes.bytes'), meshes)
for name, mb in meshes:
    print(f'{name}: {len(mb.pos) // 9} tris')
info = [{'id': cid, 'name': nm, 'tier': tier} for cid, (nm, tier, _) in creatures.CREATURES.items()]
with open(os.path.join(HERE, 'preview', 'creatures.json'), 'w', encoding='utf-8') as f:
    json.dump(info, f, ensure_ascii=False)
print('total verts', n)
