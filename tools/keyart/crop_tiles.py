import numpy as np
from PIL import Image
im = np.asarray(Image.open('../../Art/Concept/04_System_and_Visuals/[지도 바닥 타일 세트] - [8종 타일].png').convert('RGB')).astype(np.int16)
H, W, _ = im.shape
nonwhite = (np.abs(im - 255).sum(axis=2) > 30)
names = ['grass', 'paving', 'road', 'forest', 'plaza', 'stairs', 'water', 'slope']
k = 0
for r in range(2):
    for c in range(4):
        y0, y1 = r * H // 2, (r + 1) * H // 2
        x0, x1 = c * W // 4, (c + 1) * W // 4
        m = nonwhite[y0:y1, x0:x1]
        rows = np.where(m.mean(axis=1) > 0.6)[0]; cols = np.where(m.mean(axis=0) > 0.6)[0]
        by0, by1 = y0 + rows[0], y0 + rows[-1]; bx0, bx1 = x0 + cols[0], x0 + cols[-1]
        pad = 6
        tile = Image.fromarray(im[by0 + pad:by1 - pad, bx0 + pad:bx1 - pad].astype(np.uint8))
        tile.save(f'tiles/{names[k]}.png')
        print(names[k], tile.size)
        k += 1
