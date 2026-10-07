"""
피규어 하나(또는 여럿)를 만들어 미리보기 파일에 쓰고 4방향(정면·3/4·옆·뒤) + 클로즈업을 렌더한다.

    cd tools/keyart
    uv run --with numpy --with scikit-image --with fast-simplification python figure_preview.py <id>[,<id>] [tris=30000] [voxel=0.05]
결과: preview/shots/fig_<id>.png (4방향), preview/shots/fig_<id>_close.png
"""
import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuca_art import figures
from kuca_art.mesh import write_bytes

HERE = os.path.dirname(os.path.abspath(__file__))
ids = sys.argv[1].split(',')
opts = dict(a.split('=') for a in sys.argv[2:])
tris = int(opts.get('tris', 30000))
voxel = float(opts.get('voxel', 0.05))
for cid in ids:
    t = time.time()
    mb, glass = figures.build(cid, tris, voxel=voxel)
    out = [(f'Creature_{cid}', mb)] + ([(f'Creature_{cid}__glass', glass)] if glass else [])
    path = os.path.join(HERE, 'preview', f'data_{cid}.bytes')
    write_bytes(path, out, pos_unit=0.0005)
    print(cid, len(mb.pos) // 9, 'tris', f'{time.time() - t:.0f}s')
    f = f'/tools/keyart/preview/data_{cid}.bytes'
    pv = os.path.join(HERE, 'preview')
    subprocess.run(['node', 'creatures.mjs', f'shots/fig_{cid}.png', f'file={f}&cols=4&ids={cid},{cid},{cid},{cid}&yaws=0,35,90,180&pitch=8'], cwd=pv)
    subprocess.run(['node', 'creatures.mjs', f'shots/fig_{cid}_close.png', f'file={f}&cols=1&ids={cid}&yaws=20&pitch=10&sp=8'], cwd=pv)
