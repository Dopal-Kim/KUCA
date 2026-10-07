"""건물 하나를 북·동·남·서(비스듬히 35°)와 위(80°)에서 렌더해 한 장으로 묶는다.
    python3 turntable.py            # 주요 건물 전부 → shots/tt_<이름>.jpg
    python3 turntable.py gate arts  # 일부만
"""
import json, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
B = {  # 이름: (x, z, 거리)
    'gate': (-138, 368, 115), 'arts': (401, -253, 260), 'library': (-34, -351, 260), 'seonseung': (12, -119, 230),
    'pe': (24, 39, 260), 'observatory': (170, -583, 150), 'ceramics': (112, -40, 130), 'theater': (284, -643, 230),
    'foreign': (-210, 117, 260), 'engineering': (51, 259, 300), 'electronics': (298, -519, 270), 'ujeongwon': (-245, 224, 250),
    'multimedia': (-305, 20, 230), 'intl': (111, -212, 200), 'intlhall': (109, -462, 180),
}
names = sys.argv[1:] or list(B)
views = []
for n in names:
    x, z, d = B[n]
    for tag, yaw, pitch in (('N', 180, 35), ('E', 270, 35), ('S', 0, 35), ('W', 90, 35), ('T', 0, 80)):
        views.append(f'tt_{n}_{tag}={x},{z},{d},{yaw},{pitch}')
env = dict(os.environ, W='640', H='480')
subprocess.run(['node', 'render.mjs', *views], cwd=HERE, env=env, check=True, stdout=subprocess.DEVNULL)
from PIL import Image
for n in names:
    ims = [Image.open(f'{HERE}/shots/tt_{n}_{t}.png').convert('RGB') for t in 'NESWT']
    sheet = Image.new('RGB', (640 * 3, 480 * 2), 'white')
    for i, im in enumerate(ims):
        sheet.paste(im, ((i % 3) * 640, (i // 3) * 480))
    sheet.save(f'{HERE}/shots/tt_{n}.jpg', quality=85)
    for t in 'NESWT':
        os.remove(f'{HERE}/shots/tt_{n}_{t}.png')
    print('sheet', n)
