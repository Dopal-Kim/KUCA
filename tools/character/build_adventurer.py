"""
경희 모험가 플레이어 캐릭터 (Art/Concept/07_Player 시트) 를 블렌더에서 도형으로 빚어 Quaternius 뼈대에 붙인다.

    blender -b --python tools/character/build_adventurer.py -- [--gender=men|women] [--outfit=Adventurer] [--preview=out.png]
    (블렌더 없이: pip install bpy 후 python tools/character/build_adventurer.py -- ...)

- 모양은 "보이는 모습" 기준으로 만든다: Unity ModularCharacter 가 머리 1.8배 · 다리 0.68배 · 팔 0.85배로
  바꿔 보여 주므로, 내보낼 때는 그 반대로 줄이고 늘려 원래 뼈대 크기(rest)에 맞춘다.
- 부위별 FBX 4개 (머리 · 상의 · 하의 · 신발) → My project/Assets/Characters/{Men,Women}/KHU_Adventurer/
  Unity 메뉴 KUCA › Build Character Database 를 누르면 꾸미기에 "경희 모험가" 로 나온다.
- --outfit: 몸 (머리 · 목 · 몸통 · 팔 · 손 · 다리 · 발 위치와 굵기, 뼈 가중치) 은 경희 모험가와 똑같고 옷만 다른 한 벌.
  Adventurer (경희 모험가, 기본) · Varsity (경희 과잠) · Hoodie (후드티) · Neat (단정) · Summer (여름 나들이).
  모험가 말고는 머리 FBX 를 만들지 않는다 (꾸미기에서 머리는 늘 경희 모험가) → KHU_<outfit>/KHU_<outfit>_{Body,Legs,Feet}.fbx
- 재질 이름은 KHU_ / Skin 으로 시작해 Unity 가 파스텔로 바꾸지 않고 그림 색 그대로 쓴다.
"""
import math
import os
import sys

import bpy   # bpy 를 먼저 (pip 의 bpy 모듈은 bpy 를 불러야 bmesh 가 생긴다)
import bmesh
from mathutils import Vector

ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OPT = dict(a[2:].split('=', 1) for a in ARGS if a.startswith('--') and '=' in a)
GENDER = OPT.get('gender', 'men')
FOLDER = {'men': 'Men', 'women': 'Women'}[GENDER]
HERE = os.path.dirname(os.path.abspath(__file__))
CHAR = os.path.join(HERE, '..', '..', 'My project', 'Assets', 'Characters')
SKELETON = {'men': 'Men/Casual_2/Casual2_Body.fbx', 'women': 'Women/Casual/Casual_Body.fbx'}[GENDER]
OUTFIT = OPT.get('outfit', 'Adventurer')
OUT_DIR = os.path.join(CHAR, FOLDER, 'KHU_' + OUTFIT)

# Unity ModularCharacter 기본값과 같게
HEAD_SCALE, LEG_SCALE, ARM_SCALE = 1.8, 0.68, 0.85

# 시트 색 (sRGB)
COLORS = {
    'Skin_Adv': '#F4C7A8', 'KHU_Adv_Hair': '#5A3A2A', 'KHU_Adv_Hat': '#C9B48A', 'KHU_Adv_Crimson': '#A6192E',
    'KHU_Adv_Cream': '#F3EBD8', 'KHU_Adv_Olive': '#7C8C5A', 'KHU_Adv_Boot': '#B77A43', 'KHU_Adv_Sole': '#7A4A28',
    'KHU_Adv_Leather': '#8E5A35', 'KHU_Adv_Eye': '#3A2620', 'KHU_Adv_White': '#FFFFFF', 'KHU_Adv_Blush': '#F29C93',
    'KHU_Adv_Mouth': '#B5524A', 'KHU_Adv_Paper': '#EADBB8', 'KHU_Adv_Tongue': '#E98A86', 'KHU_Adv_Freckle': '#C98B6B',
    # 옷 (Unity 는 재질 이름으로 색을 나눠 쓰므로 같은 이름은 어느 옷에서나 같은 색)
    'KHU_Var_Red': '#A6192E', 'KHU_Var_Cream': '#F3EBD8', 'KHU_Var_Tee': '#FAFAF7', 'KHU_Var_Denim': '#4F6F9A', 'KHU_Var_DenimCuff': '#7392BA',
    'KHU_Hood_Gray': '#A7ABB2', 'KHU_Hood_Rib': '#8C9098', 'KHU_Hood_String': '#F4F4F1', 'KHU_Hood_Navy': '#2F3B5C', 'KHU_Hood_NavyRib': '#263150',
    'KHU_Neat_Navy': '#2C3B60', 'KHU_Neat_Shirt': '#FFFFFF', 'KHU_Neat_Button': '#1C2540', 'KHU_Neat_Beige': '#CDB48C',
    'KHU_Neat_Loafer': '#7B4B2B', 'KHU_Neat_Sole': '#4A2D1B',
    'KHU_Sum_Tee': '#BFE3AE', 'KHU_Sum_Rib': '#8FC47A', 'KHU_Sum_Denim': '#6C90BC', 'KHU_Sum_DenimCuff': '#94B3D6', 'KHU_Sum_Sock': '#FFFFFF',
    'KHU_Shoe_White': '#F6F5F1', 'KHU_Shoe_Sole': '#D8D3CA', 'KHU_Shoe_Lace': '#DAD6CE',
}

# 옷 한 벌: 속 (몸통 타원) · 겉 (셔츠 껍질, gap = 앞섶 반폭 → 없으면 앞이 막힌 티) · 소매 · 바지 (hem = 발에서 바짓단 높이, 없으면 반바지) · 신발
OUTFITS = {
    'Adventurer': dict(inner='KHU_Adv_Crimson', shell='KHU_Adv_Cream', gap=lambda z: 0.07 + max(0.0, z - (body_top - 0.22)) * 0.3, edge=0.008,
                       long_sleeve=False, sleeve='KHU_Adv_Cream', pants='KHU_Adv_Olive', pants_cuff='KHU_Adv_Olive', hem=None,
                       sock='KHU_Adv_Cream', shoe='boot'),
    'Varsity': dict(inner='KHU_Var_Tee', shell='KHU_Var_Red', gap=lambda z: 0.05 + max(0.0, z - (body_top - 0.12)) * 0.3, edge=0.012,
                    long_sleeve=True, sleeve='KHU_Var_Cream', cuff='KHU_Var_Red', cuff_r=0.075,
                    pants='KHU_Var_Denim', pants_cuff='KHU_Var_DenimCuff', hem=0.11, hem_r=0.088, shoe='sneaker', accent='KHU_Var_Red'),
    'Hoodie': dict(inner='KHU_Hood_Gray', shell='KHU_Hood_Gray', gap=None, long_sleeve=True, sleeve='KHU_Hood_Gray', cuff='KHU_Hood_Rib', cuff_r=0.075,
                   pants='KHU_Hood_Navy', pants_cuff='KHU_Hood_NavyRib', hem=0.13, hem_r=0.074, shoe='sneaker', accent='KHU_Hood_Navy'),
    'Neat': dict(inner='KHU_Neat_Shirt', shell='KHU_Neat_Navy', gap=lambda z: 0.012 + max(0.0, z - (body_top - 0.26)) * 0.42, edge=0.011,
                 long_sleeve=True, sleeve='KHU_Neat_Navy', sleeve_back=0.075, cuff='KHU_Neat_Shirt', cuff_r=0.062, cuff_span=(0.075, 0.02),
                 pants='KHU_Neat_Beige', pants_cuff=None, hem=0.1, hem_r=0.086, shoe='loafer'),
    'Summer': dict(inner='KHU_Sum_Tee', shell='KHU_Sum_Tee', gap=None, long_sleeve=False, sleeve='KHU_Sum_Tee',
                   pants='KHU_Sum_Denim', pants_cuff='KHU_Sum_DenimCuff', hem=None, sock='KHU_Sum_Sock', shoe='sneaker', accent='KHU_Sum_Rib'),
}


def srgb(hexstr):
    c = [int(hexstr[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


def material(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        p = m.node_tree.nodes['Principled BSDF']
        p.inputs['Base Color'].default_value = (*srgb(COLORS[name]), 1)
        p.inputs['Roughness'].default_value = 0.55
    return m


# ---------------------------------------------------------------- 뼈대 (원래 크기) 와 보이는 모습의 관절 위치
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(CHAR, SKELETON))
RIG = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    bpy.data.objects.remove(o)
RIG.name = 'CharacterArmature'
BONE = {b.name: RIG.matrix_world @ b.head_local for b in RIG.data.bones}

HIP_H = (BONE['UpperLeg.L'].z + BONE['UpperLeg.R'].z) / 2 - BONE['Root'].z
DROP = Vector((0, 0, (1 - LEG_SCALE) * HIP_H))


def shown(bone):
    """Unity 에서 보이는 관절 위치 (머리 · 몸통은 골반만큼 내려가고, 팔 · 다리는 줄어든다)."""
    p = BONE[bone]
    if bone.startswith(('UpperLeg', 'LowerLeg')):
        hip = BONE['UpperLeg' + bone[-2:]]
        return hip - DROP + (p - hip) * LEG_SCALE
    if bone.startswith('Foot'):
        hip = BONE['UpperLeg' + bone[-2:]]
        return hip - DROP + (p - hip) * LEG_SCALE
    if bone.startswith(('UpperArm', 'LowerArm', 'Wrist')):
        sh = BONE['UpperArm' + bone[-2:]]
        return sh - DROP + (p - sh) * ARM_SCALE
    return p - DROP


def to_rest(p, kind, side):
    """보이는 모습의 점 → 원래 뼈대 크기의 점 (Unity 가 하는 변환의 반대)."""
    if kind == 'head':
        pivot = BONE['Head']
        return pivot + (p - (pivot - DROP)) / HEAD_SCALE
    if kind == 'arm':
        sh = BONE['UpperArm' + side]
        return sh + (p - (sh - DROP)) / ARM_SCALE
    if kind == 'leg':
        hip = BONE['UpperLeg' + side]
        return hip + (p - (hip - DROP)) / LEG_SCALE
    if kind == 'foot':
        return p + (BONE['Foot' + side] - shown('Foot' + side))
    return p + DROP


# ---------------------------------------------------------------- 도형 (보이는 모습 기준, z 위, -y 앞)
PARTS = []   # (bmesh, material, part, kind)


def add(bm, mat, part, kind):
    PARTS.append((bm, mat, part, kind))
    return bm


def ellipsoid(center, radii, segs=28, rings=18):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2])) + Vector(center)
    return bm


def capsule(a, b, r0, r1=None, segs=20, rings=14):
    """a → b 캡슐. 반지름 r0 (a 쪽) → r1 (b 쪽)."""
    r1 = r0 if r1 is None else r1
    a, b = Vector(a), Vector(b)
    axis = b - a
    L = axis.length
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1)
    q = Vector((0, 0, 1)).rotation_difference(axis.normalized())
    for v in bm.verts:
        z = v.co.z
        t = 1.0 if z > 0 else 0.0
        r = r0 + (r1 - r0) * t
        local = Vector((v.co.x * r, v.co.y * r, z * r + (L if z > 0 else 0)))
        v.co = a + q @ local
    return bm


def cylinder(center, radius_bottom, radius_top, height, segs=32, cap=True):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, segments=segs, radius1=radius_bottom, radius2=radius_top, depth=height)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return bm


def loft(cx, cy, rings, segs=40):
    """z 마다 타원 단면 (z, 반지름 x, 반지름 y[, 중심 x, 중심 y]) 을 이은 통. 위 · 아래는 막는다."""
    bm = bmesh.new()
    loops = []
    for z, rx, ry, *c in rings:
        x0, y0 = c if c else (cx, cy)
        loops.append([bm.verts.new((x0 + math.cos(2 * math.pi * i / segs) * rx, y0 + math.sin(2 * math.pi * i / segs) * ry, z))
                      for i in range(segs)])
    for a, b in zip(loops, loops[1:]):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(loops[0][::-1])
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def tuck(bm, center, amount, inside=0.86):
    """amount(점) 이 1 인 곳은 머리 안으로 밀어 넣어 숨긴다. 면을 지우지 않아 경계가 매끈하다."""
    for v in bm.verts:
        m = amount(v.co)
        if m > 0:
            v.co = center + (v.co - center) * (1 - (1 - inside) * m)
    return bm


def tube(points, r, segs=10):
    """점들을 잇는 끊김 없는 관 (끈)."""
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(points):
        t = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        a = t.orthogonal().normalized()
        b = t.cross(a)
        rings.append([bm.verts.new(p + (a * math.cos(2 * math.pi * k / segs) + b * math.sin(2 * math.pi * k / segs)) * r) for k in range(segs)])
    for ra, rb in zip(rings, rings[1:]):
        for k in range(segs):
            j = (k + 1) % segs
            bm.faces.new((ra[k], ra[j], rb[j], rb[k]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def add_inner(bm):
    """열린 껍질 안쪽이 보이도록 뒤집은 안쪽 면을 붙인다 (Unity 는 뒷면을 그리지 않는다)."""
    inner = bmesh.ops.duplicate(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:])
    faces = [g for g in inner['geom'] if isinstance(g, bmesh.types.BMFace)]
    verts = [g for g in inner['geom'] if isinstance(g, bmesh.types.BMVert)]
    bmesh.ops.reverse_faces(bm, faces=faces)
    center = sum((v.co for v in verts), Vector()) / max(1, len(verts))
    for v in verts:   # 안쪽으로 살짝 (겉면과 겹쳐 깜빡이지 않게)
        v.co = center + (v.co - center) * 0.985
    return bm


def delete_where(bm, test):
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if test(f.calc_center_median())], context='FACES')
    return add_inner(bm)


male = GENDER == 'men'
HEAD = shown('Head')                      # 목 위 (머리 회전 중심)
SH = {s: shown('UpperArm' + s) for s in ('.L', '.R')}
HIP = {s: shown('UpperLeg' + s) for s in ('.L', '.R')}
KNEE = {s: shown('LowerLeg' + s) for s in ('.L', '.R')}
FOOT = {s: shown('Foot' + s) for s in ('.L', '.R')}
WRIST = {s: shown('Wrist' + s) for s in ('.L', '.R')}

# 머리
hc = HEAD + Vector((0, -0.01, 0.235))
add(ellipsoid(hc, (0.25, 0.235, 0.245)), 'Skin_Adv', 'Head', 'head')
for s in (1, -1):
    add(ellipsoid(hc + Vector((0.245 * s, 0.01, -0.03)), (0.045, 0.03, 0.06)), 'Skin_Adv', 'Head', 'head')       # 귀
face_y = hc.y - 0.228


def face_y_at(x, z):
    """머리 타원 앞면의 y (앞 = -y). 얼굴 부위를 곡면에 맞춰 붙인다."""
    k = 1 - (x / 0.25) ** 2 - ((z - hc.z) / 0.245) ** 2
    return hc.y - 0.235 * max(0.0, k) ** 0.5


def arc(cx, cz, rx, rz, a0, a1, r, n=10, lift=-0.001):
    """얼굴 곡면 위의 호 (각도 a0 → a1, z 위). 선 같은 눈 · 입 · 눈썹에 쓴다."""
    pts = [Vector((cx + math.cos(a) * rx, 0, cz + math.sin(a) * rz)) for a in [a0 + (a1 - a0) * i / n for i in range(n + 1)]]
    for p in pts:
        p.y = face_y_at(p.x, p.z) + lift
    return tube(pts, r, 8)


# 얼굴 부위는 고를 수 있게 갈래마다 따로 만든다 (Face_<부위>_<번호>, 꾸미기 화면에서 하나만 켠다).
# 0 번이 시트의 기본 얼굴. 없음은 Unity 에서 모두 끈다. 눈 · 눈썹에는 크기 · 간격 · 높이 셰이프 키가 붙는다 (eye_keys).
EYE = {s: Vector((0.085 * s, face_y + 0.012, hc.z - 0.035)) for s in (1, -1)}
for s in (1, -1):
    e = EYE[s]
    # 0 기본: 세로로 긴 눈 + 하이라이트
    add(ellipsoid(e, (0.03, 0.014, 0.042), 16, 12), 'KHU_Adv_Eye', 'Face_Eyes_0', 'head')
    add(ellipsoid(e + Vector((0.009 * s, -0.012, 0.016)), (0.009, 0.005, 0.01), 10, 8), 'KHU_Adv_White', 'Face_Eyes_0', 'head')
    # 1 동글: 크고 둥근 눈 + 하이라이트 두 개
    add(ellipsoid(e, (0.037, 0.015, 0.039), 18, 12), 'KHU_Adv_Eye', 'Face_Eyes_1', 'head')
    add(ellipsoid(e + Vector((0.011 * s, -0.013, 0.014)), (0.012, 0.005, 0.012), 10, 8), 'KHU_Adv_White', 'Face_Eyes_1', 'head')
    add(ellipsoid(e + Vector((-0.012 * s, -0.012, -0.016)), (0.005, 0.004, 0.005), 8, 6), 'KHU_Adv_White', 'Face_Eyes_1', 'head')
    # 2 웃는 눈: 위로 볼록한 호 (^ ^)
    add(arc(e.x, e.z - 0.012, 0.03, 0.026, math.radians(20), math.radians(160), 0.0075), 'KHU_Adv_Eye', 'Face_Eyes_2', 'head')
    # 3 또렷: 기본 눈 + 바깥 위 속눈썹
    add(ellipsoid(e, (0.03, 0.014, 0.042), 16, 12), 'KHU_Adv_Eye', 'Face_Eyes_3', 'head')
    add(ellipsoid(e + Vector((0.009 * s, -0.012, 0.016)), (0.009, 0.005, 0.01), 10, 8), 'KHU_Adv_White', 'Face_Eyes_3', 'head')
    lash = [e + Vector((0.02 * s, 0, 0.034)), e + Vector((0.034 * s, 0, 0.042)), e + Vector((0.045 * s, 0, 0.056))]
    for p in lash:
        p.y = face_y_at(p.x, p.z) - 0.001
    add(tube(lash, 0.0055, 8), 'KHU_Adv_Eye', 'Face_Eyes_3', 'head')
    # 눈썹 0 기본 · 1 둥근 아치 · 2 짙은 일자 (머리 색)
    add(capsule(e + Vector((-0.025, 0.0, 0.07)), e + Vector((0.028, 0.004, 0.075)), 0.008), 'KHU_Adv_Hair', 'Face_Brows_0', 'head')
    add(arc(e.x, e.z + 0.058, 0.03, 0.016, math.radians(25), math.radians(155), 0.0068), 'KHU_Adv_Hair', 'Face_Brows_1', 'head')
    add(capsule(e + Vector((-0.02 * s, 0.0, 0.066)), e + Vector((0.022 * s, 0.003, 0.068)), 0.0115), 'KHU_Adv_Hair', 'Face_Brows_2', 'head')
    # 볼 0 볼터치 · 1 동그란 볼 · 2 주근깨
    add(ellipsoid(Vector((0.135 * s, face_y + 0.035, hc.z - 0.09)), (0.04, 0.01, 0.025), 14, 10), 'KHU_Adv_Blush', 'Face_Cheeks_0', 'head')
    add(ellipsoid(Vector((0.13 * s, face_y_at(0.13, hc.z - 0.088) - 0.004, hc.z - 0.088)), (0.042, 0.011, 0.036), 16, 10), 'KHU_Adv_Blush', 'Face_Cheeks_1', 'head')
    for dx, dz in ((0.0, 0.0), (0.022, 0.008), (0.012, -0.016), (0.034, -0.006)):
        x, z = (0.115 + dx) * s, hc.z - 0.08 + dz
        add(ellipsoid(Vector((x, face_y_at(x, z) + 0.0015, z)), (0.0055, 0.004, 0.0055), 8, 6), 'KHU_Adv_Freckle', 'Face_Cheeks_2', 'head')
# 코 0 동글 · 1 작게 (없음은 Unity 에서)
add(ellipsoid(Vector((0, face_y - 0.004, hc.z - 0.075)), (0.014, 0.01, 0.016), 12, 10), 'Skin_Adv', 'Face_Nose_0', 'head')
add(ellipsoid(Vector((0, face_y - 0.0, hc.z - 0.075)), (0.009, 0.007, 0.009), 10, 8), 'Skin_Adv', 'Face_Nose_1', 'head')
# 입 0 미소 · 1 활짝 · 2 고양이 입 · 3 작은 o
MOUTH_Z = hc.z - 0.115
pts = [Vector((math.cos(a) * 0.04, 0, -math.sin(a) * 0.022)) for a in [math.pi * (0.15 + 0.7 * i / 10) for i in range(11)]]
for i in range(len(pts) - 1):
    seg = capsule(pts[i] + Vector((0, face_y + 0.012, MOUTH_Z)), pts[i + 1] + Vector((0, face_y + 0.012, MOUTH_Z)), 0.006, None, 8, 6)
    add(seg, 'KHU_Adv_Mouth', 'Face_Mouth_0', 'head')
grin_c = Vector((0, face_y_at(0, MOUTH_Z + 0.004) - 0.003, MOUTH_Z + 0.004))
grin = ellipsoid(grin_c, (0.038, 0.012, 0.03), 24, 14)
bmesh.ops.delete(grin, geom=[v for v in grin.verts if v.co.z > grin_c.z + 0.0005], context='VERTS')
bmesh.ops.contextual_create(grin, geom=[e for e in grin.edges if e.is_boundary])   # 위를 평평하게 막는다
add(grin, 'KHU_Adv_Mouth', 'Face_Mouth_1', 'head')
add(ellipsoid(grin_c + Vector((0, -0.006, -0.017)), (0.018, 0.008, 0.009), 12, 8), 'KHU_Adv_Tongue', 'Face_Mouth_1', 'head')
for s in (1, -1):
    add(arc(0.016 * s, MOUTH_Z + 0.006, 0.016, 0.014, math.radians(200), math.radians(340), 0.0055, 8, -0.006), 'KHU_Adv_Mouth', 'Face_Mouth_2', 'head')
o_c = Vector((0, face_y_at(0, MOUTH_Z - 0.004) + 0.002, MOUTH_Z - 0.004))
add(ellipsoid(o_c, (0.013, 0.008, 0.016), 14, 10), 'KHU_Adv_Mouth', 'Face_Mouth_3', 'head')


def eye_keys(p):
    """눈 · 눈썹 점 p (보이는 모습) 가 슬라이더 끝에서 갈 자리. {셰이프 키 이름: 새 위치}"""
    s = 1 if p.x > 0 else -1
    e = EYE[s]
    def onface(q, d):
        q = q.copy()
        q.y += face_y_at(q.x, q.z) - face_y_at(q.x - d.x, q.z - d.z)
        return q
    keys = {}
    for name, scale in (('Eye_Big', 1.25), ('Eye_Small', 0.8)):
        q = e + (p - e) * scale
        q.y = p.y + (face_y_at(q.x, q.z) - face_y_at(p.x, p.z))
        keys[name] = q
    for name, dx in (('Eye_Wide', 0.016), ('Eye_Narrow', -0.014)):
        d = Vector((dx * s, 0, 0))
        keys[name] = onface(p + d, d)
    for name, dz in (('Eye_Up', 0.016), ('Eye_Down', -0.016)):
        d = Vector((0, 0, dz))
        keys[name] = onface(p + d, d)
    return keys


# 머리카락: 머리보다 살짝 큰 껍질 하나. 얼굴 쪽은 앞머리 끝선(fringe_at) 아래만 머리 안으로 밀어 넣는다.
# (공 여러 개를 붙이지 않아 모동숲 캐릭터처럼 한 덩어리로 매끈하다)
hair = ellipsoid(hc + Vector((0, 0.01, 0.014)), (0.27, 0.258, 0.266), 112, 72)
if male:
    # 뾰족한 앞머리 네 갈래: 끝은 눈썹 바로 위, 사이는 조금 위로
    LOCKS = (-0.15, -0.055, 0.04, 0.135)
    def fringe_at(x):
        tip = max(max(0.0, 1 - abs(x - lx) / 0.06) for lx in LOCKS)
        return hc.z + 0.09 - 0.04 * tip + max(0.0, abs(x) - 0.17) * 0.6
else:
    # 일자 앞머리: 가운데는 눈썹 위, 옆으로 갈수록 살짝 올라가 얼굴을 감싼다
    def fringe_at(x):
        return hc.z + 0.055 + 0.9 * x * x
def hair_cut(c):
    d = (c - hc).normalized()
    f = fringe_at(c.x)
    face = smoothstep(-0.36, -0.5, d.y) * smoothstep(f + 0.018, f - 0.018, c.z)   # 얼굴 쪽, 끝선 아래
    edge = hc.z - 0.17 + max(0.0, -d.y) * 0.2                                       # 뒤는 목덜미, 옆 · 앞은 비스듬히 위로
    below = smoothstep(edge + 0.02, edge - 0.02, c.z)
    return max(face, below)
tuck(hair, hc, hair_cut)
for v in hair.verts:   # 앞머리 끝 쪽을 살짝 도톰하게 (이마 위에 얹힌 앞머리처럼)
    d = (v.co - hc).normalized()
    f = fringe_at(v.co.x)
    lip = smoothstep(-0.3, -0.5, d.y) * smoothstep(f + 0.09, f + 0.02, v.co.z) * smoothstep(f - 0.005, f + 0.015, v.co.z)
    if lip > 0:
        v.co = hc + (v.co - hc) * (1 + 0.035 * lip)
add(hair, 'KHU_Adv_Hair', 'Head', 'head')
if not male:   # 단발: 옆 · 뒤로 어깨 위까지
    bob_c = hc + Vector((0, 0.03, -0.11))
    bob = ellipsoid(bob_c, (0.29, 0.26, 0.2), 48, 30)
    for v in bob.verts:   # 얼굴 앞으로 감기는 머리는 귀 뒤로 넘기고, 윗부분은 머리 안으로
        front = smoothstep(hc.y - 0.0, hc.y - 0.12, v.co.y)
        if front > 0:
            v.co.y += (hc.y + 0.06 - v.co.y) * front
            v.co.x *= 1 - 0.08 * front
        top = smoothstep(hc.z - 0.03, hc.z + 0.01, v.co.z)
        if top > 0:
            v.co = bob_c + (v.co - bob_c) * (1 - 0.25 * top)
    add(bob, 'KHU_Adv_Hair', 'Head', 'head')

# 모자: 둥근 정수리 + 아래로 살짝 처진 챙 + 크림슨 띠.
# 지도 카메라는 위에서 내려다보므로 모자를 뒤로 젖혀 써서 (HAT_TILT) 이마와 눈코입이 보이게 한다.
HAT_TILT = math.radians(float(OPT.get('hat_tilt', 12)))
BRIM_R = float(OPT.get('brim', 0.42))   # 챙 반지름
hat_start = len(PARTS)
# 머리를 감싸게 푹 눌러쓴다: 정수리는 머리카락보다 크게, 챙은 앞머리 끝 바로 위 (앞머리 끝과 눈코입만 보이게)
hat_z = hc.z + 0.2
CROWN = (0.302, 0.292, 0.215)
crown = ellipsoid(Vector((0, hc.y + 0.01, hat_z)), CROWN, 48, 28)
delete_where(crown, lambda c: c.z < hat_z - 0.105)
add(crown, 'KHU_Adv_Hat', 'Head', 'head')
band_r = CROWN[0] * (1 - (0.06 / CROWN[2]) ** 2) ** 0.5 + 0.006
add(cylinder(Vector((0, hc.y + 0.01, hat_z - 0.06)), band_r + 0.004, band_r, 0.05, 48), 'KHU_Adv_Crimson', 'Head', 'head')
brim = cylinder(Vector((0, hc.y + 0.01, hat_z - 0.118)), BRIM_R, 0.292, 0.065, 64)
for v in brim.verts:   # 챙 끝을 둥글게 살짝 아래로 (버킷햇처럼)
    if (v.co.xy - Vector((0, hc.y + 0.01))).length > BRIM_R - 0.05:
        v.co.z -= 0.012
add(brim, 'KHU_Adv_Hat', 'Head', 'head')
# 뒤쪽 챙 끝을 축으로 앞을 들어 올린다 (x 축 회전, 앞 = -y)
hat_pivot = Vector((0, hc.y + 0.3, hat_z - 0.12))
for bm_, *_ in PARTS[hat_start:]:
    for v in bm_.verts:
        d = v.co - hat_pivot
        v.co = hat_pivot + Vector((d.x, d.y * math.cos(HAT_TILT) + d.z * math.sin(HAT_TILT),
                                   -d.y * math.sin(HAT_TILT) + d.z * math.cos(HAT_TILT)))

# ---------------------------------------------------------------- 옷 (--outfit)
# 몸 규격 (몸통 타원 · 셔츠 껍질 크기 · 목 · 팔 · 손 · 다리 · 발 위치와 굵기) 은 모든 옷이 같고, 덮는 옷의 도형 · 색만 다르다.
# 긴소매 · 긴바지 안에는 피부를 만들지 않는다 (걸을 때 피부가 옷 밖으로 뚫고 나오지 않게).
FIT = OUTFITS[OUTFIT]
body_top = HEAD.z - 0.02
body_bot = HIP['.L'].z + 0.07      # 셔츠 밑단 (반바지 허리를 올려 상체가 짧아 보이게)
tc = Vector((0, -0.005, (body_top + body_bot) / 2))
TORSO = (0.2, 0.135, (body_top - body_bot) / 2 + 0.03)
add(ellipsoid(tc, TORSO), FIT['inner'], 'Body', 'body')
SHIRT_C = tc + Vector((0, 0, -0.01))
SHIRT_R = (0.218, 0.152, (body_top - body_bot) / 2 + 0.04)


def shirt_profile(z):
    """셔츠 단면 반지름 (몸통 타원을 따르되 밑단은 오므리지 않는다)."""
    k = max(0.0, 1 - ((z - SHIRT_C.z) / SHIRT_R[2]) ** 2) ** 0.5
    if z < SHIRT_C.z:
        k = max(k, 0.93)
    return SHIRT_R[0] * k, SHIRT_R[1] * k


def open_angles(z, gap):
    """앞섶 반폭 gap(z) 만큼 앞 (-y) 가운데를 뺀 둘레의 시작 · 끝 각도."""
    rx, _ = shirt_profile(z)
    half = math.asin(min(0.95, gap(z) / max(rx, 1e-3)))
    return -math.pi / 2 + half, 3 * math.pi / 2 - half


def shirt_open(zs, gap, segs=72):
    """앞이 열린 셔츠 · 재킷: 각 높이마다 앞 (-y) 가운데를 빼고 둘레를 잇는다. 가장자리가 매끈하다."""
    bm = bmesh.new()
    rows = []
    for z in zs:
        rx, ry = shirt_profile(z)
        a0, a1 = open_angles(z, gap)
        rows.append([bm.verts.new((SHIRT_C.x + math.cos(a0 + (a1 - a0) * i / segs) * rx,
                                   SHIRT_C.y + math.sin(a0 + (a1 - a0) * i / segs) * ry, z)) for i in range(segs + 1)])
    for ra, rb in zip(rows, rows[1:]):
        for i in range(segs):
            bm.faces.new((ra[i], ra[i + 1], rb[i + 1], rb[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:   # 바깥을 향하게
        if f.normal.dot(f.calc_center_median() - SHIRT_C) < 0:
            f.normal_flip()
    return add_inner(bm)


def shell_pt(z, a, out=0.0):
    rx, ry = shirt_profile(z)
    return Vector((SHIRT_C.x + math.cos(a) * (rx + out), SHIRT_C.y + math.sin(a) * (ry + out), z))


def band(z, mat, r, gap=None, out=0.004, n=64):
    """셔츠 둘레를 따라 도는 시보리 줄 (gap 이 있으면 앞섶에서 끊긴다)."""
    a0, a1 = open_angles(z, gap) if gap else (-math.pi / 2, 3 * math.pi / 2)
    add(tube([shell_pt(z, a0 + (a1 - a0) * i / n, out) for i in range(n + 1)], r, 8), mat, 'Body', 'body')


def neck_ring(mat, r, rx=0.09, ry=0.086, cut=0.0, n=40):
    """목둘레 깃 (cut 만큼 앞을 비운다)."""
    a0, a1 = -math.pi / 2 + cut, 3 * math.pi / 2 - cut
    pts = [Vector((math.cos(a) * rx, tc.y + 0.01 + math.sin(a) * ry, body_top - 0.008)) for a in [a0 + (a1 - a0) * i / n for i in range(n + 1)]]
    add(tube(pts, r, 8), mat, 'Body', 'body')


def front_y(z):
    """셔츠 앞면 (-y) 의 y."""
    return SHIRT_C.y - shirt_profile(z)[1]


zs = [body_bot - 0.03 + (body_top - 0.01 - (body_bot - 0.03)) * i / 30 for i in range(31)]
if FIT['gap']:   # 앞이 열린 겉옷 + 앞섶 가장자리 두께 (얇은 껍질이 종이처럼 보이지 않게)
    add(shirt_open(zs, FIT['gap']), FIT['shell'], 'Body', 'body')
    for side in (1, -1):
        edge = []
        for z in zs:
            a0, a1 = open_angles(z, FIT['gap'])
            edge.append(shell_pt(z, a0 if side > 0 else a1 - 2 * math.pi))
        add(tube(edge, FIT['edge'], 8), FIT['shell'], 'Body', 'body')
else:            # 앞이 막힌 티 · 후드티
    add(loft(SHIRT_C.x, SHIRT_C.y, [(z, *shirt_profile(z)) for z in zs], 72), FIT['shell'], 'Body', 'body')

if OUTFIT == 'Adventurer':
    for s in (1, -1):   # 깃
        collar = capsule(Vector((0.03 * s, tc.y - 0.08, body_top + 0.0)), Vector((0.11 * s, tc.y - 0.02, body_top - 0.01)), 0.02, 0.016, 12, 8)
        add(collar, 'KHU_Adv_Cream', 'Body', 'body')
elif OUTFIT == 'Varsity':   # 야구 점퍼: 목 · 밑단 시보리 (빨강 + 크림 줄)
    neck_ring('KHU_Var_Red', 0.02, cut=0.45)
    band(zs[0] + 0.016, 'KHU_Var_Red', 0.02, FIT['gap'])
    band(zs[0] + 0.016, 'KHU_Var_Cream', 0.006, FIT['gap'], out=0.022)
elif OUTFIT == 'Hoodie':    # 등 뒤 후드 · 목둘레 · 끈 두 줄 · 앞 주머니 · 밑단 시보리
    hood_c = Vector((0, SHIRT_C.y + 0.12, body_top - 0.05))
    hood = ellipsoid(hood_c, (0.17, 0.075, 0.11), 40, 24)
    for v in hood.verts:   # 위쪽은 목 뒤로 둥글게 말려 넘어간 모양 (앞으로 살짝 숙인다)
        v.co.y -= max(0.0, v.co.z - hood_c.z) * 0.35
    add(hood, 'KHU_Hood_Gray', 'Body', 'body')
    neck_ring('KHU_Hood_Gray', 0.024, rx=0.095, ry=0.09)
    for s in (1, -1):
        top_z = body_top - 0.03
        pts = [Vector((0.035 * s, front_y(z) - 0.012, z)) for z in [top_z - 0.13 * i / 8 for i in range(9)]]
        pts[0].y = tc.y - 0.095   # 목둘레에서 시작
        add(tube(pts, 0.007, 8), 'KHU_Hood_String', 'Body', 'body')
        add(ellipsoid(pts[-1] + Vector((0, 0, -0.008)), (0.01, 0.01, 0.014), 10, 8), 'KHU_Hood_String', 'Body', 'body')
    pz = body_bot + 0.05
    add(ellipsoid(Vector((0, front_y(pz) + 0.012, pz)), (0.125, 0.022, 0.06), 32, 16), 'KHU_Hood_Rib', 'Body', 'body')
    band(zs[0] + 0.016, 'KHU_Hood_Rib', 0.02)
elif OUTFIT == 'Neat':      # 재킷 단추 두 개 + 흰 셔츠 깃
    for bz in (body_top - 0.3, body_top - 0.4):
        add(ellipsoid(Vector((0, front_y(bz) - 0.006, bz)), (0.014, 0.008, 0.014), 12, 8), 'KHU_Neat_Button', 'Body', 'body')
    for s in (1, -1):
        collar = capsule(Vector((0.03 * s, tc.y - 0.08, body_top + 0.0)), Vector((0.1 * s, tc.y - 0.025, body_top - 0.005)), 0.02, 0.016, 12, 8)
        add(collar, 'KHU_Neat_Shirt', 'Body', 'body')
elif OUTFIT == 'Summer':    # 둥근 목 티
    neck_ring('KHU_Sum_Rib', 0.016)
add(cylinder(Vector((0, tc.y + 0.01, body_top + 0.0)), 0.075, 0.07, 0.06), 'Skin_Adv', 'Body', 'body')   # 목


def limb(a, b, r0, r1, cuts=12):
    """길이 방향으로 잘게 나눈 캡슐 (팔꿈치 · 무릎이 접힐 때 소매 · 바짓단이 같이 굽게)."""
    bm = capsule(a, b, r0, r1)
    L = (Vector(b) - Vector(a)).length
    bmesh.ops.subdivide_edges(bm, edges=[e for e in bm.edges if e.calc_length() > L * 0.6], cuts=cuts, use_grid_fill=True)
    return bm


# 팔 (T 자세: 어깨에서 +x / -x 로 곧게)
for s, side in ((1, '.L'), (-1, '.R')):
    sh, wr = SH[side], WRIST[side]
    if FIT['long_sleeve']:   # 소매가 손목까지 (안에 피부 없음) + 소매 끝 시보리 · 셔츠 소맷부리
        d = (wr - sh).normalized()
        end = FIT.get('sleeve_back', 0.05)
        add(limb(sh + Vector((-0.03 * s, 0, 0)), wr - d * end, 0.085, 0.07), FIT['sleeve'], 'Body', 'arm' + side)
        c0, c1 = FIT.get('cuff_span', (end + 0.035, end - 0.005))
        add(capsule(wr - d * c0, wr - d * c1, FIT['cuff_r'], None, 20, 10), FIT['cuff'], 'Body', 'arm' + side)
    else:
        sleeve_end = sh + (wr - sh) * 0.5
        add(capsule(sh + Vector((-0.03 * s, 0, 0)), sleeve_end, 0.085, 0.078), FIT['sleeve'], 'Body', 'arm' + side)
        add(capsule(sh + Vector((0.02 * s, 0, 0)), wr, 0.058, 0.052), 'Skin_Adv', 'Body', 'arm' + side)
    add(ellipsoid(wr + Vector((0.055 * s, -0.005, 0)), (0.065, 0.045, 0.058)), 'Skin_Adv', 'Body', 'arm' + side)
    add(ellipsoid(wr + Vector((0.035 * s, -0.04, 0.02)), (0.025, 0.022, 0.025), 12, 10), 'Skin_Adv', 'Body', 'arm' + side)   # 엄지

if OUTFIT == 'Adventurer':
    # 가방 (왼쪽 허리) + 끈 (오른쪽 어깨 → 가방) + 지도 + 배지
    bag_c = Vector((0.25, -0.05, HIP['.L'].z + 0.06))
    add(ellipsoid(bag_c, (0.1, 0.06, 0.085)), 'KHU_Adv_Leather', 'Body', 'body')
    add(ellipsoid(bag_c + Vector((0, -0.035, 0.03)), (0.098, 0.035, 0.055)), 'KHU_Adv_Leather', 'Body', 'body')
    add(ellipsoid(bag_c + Vector((0, -0.072, 0.0)), (0.025, 0.01, 0.025), 14, 10), 'KHU_Adv_Crimson', 'Body', 'body')
    add(capsule(bag_c + Vector((-0.06, 0.0, 0.08)), bag_c + Vector((-0.11, 0.02, 0.17)), 0.02), 'KHU_Adv_Paper', 'Body', 'body')
    from mathutils.bvhtree import BVHTree
    _surf = bmesh.new()
    for bm_, _, part_, _ in PARTS:          # 지금까지 만든 몸통 · 셔츠 · 소매 겉면
        if part_ == 'Body':
            tmp = bpy.data.meshes.new('tmp')
            bm_.to_mesh(tmp)
            _surf.from_mesh(tmp)
            bpy.data.meshes.remove(tmp)
    SURF = BVHTree.FromBMesh(_surf)

    def on_body(origin, direction, lift=0.013):
        """origin 에서 direction 으로 쏜 광선이 처음 닿는 겉면 + 살짝 띄운 점."""
        hit, normal, _, _ = SURF.ray_cast(Vector(origin), Vector(direction).normalized(), 2.0)
        if hit is None:
            return None
        if normal.dot(Vector(direction)) > 0:
            normal = -normal
        return hit + normal * lift

    sx, top_z = -0.12, body_top + 0.02
    bag_top = bag_c + Vector((-0.035, 0, 0.075))
    front, back = [], []
    for i in range(26):
        t = i / 25
        x = sx + (bag_top.x - 0.03 - sx) * t
        z = top_z - 0.05 + (bag_top.z - (top_z - 0.05)) * t
        for side, out in ((-1, front), (1, back)):
            p = on_body((x, SHIRT_C.y + side * 1.0, z), (0, -side, 0))
            if p is not None:
                out.append(p)
    over = []
    y0, y1 = front[0].y, back[0].y
    for i in range(1, 12):   # 어깨 위: 위에서 아래로 쏴서 어깨 윗면을 따라간다
        y = y0 + (y1 - y0) * i / 12
        p = on_body((sx, y, top_z + 1.0), (0, 0, -1))
        if p is not None:
            over.append(p)

    def bridge(pts, side):
        """셔츠 앞섶처럼 움푹한 곳은 끈이 건너가게: 바깥쪽으로만 펴고 부드럽게."""
        for _ in range(6):
            out = [pts[0]]
            for i in range(1, len(pts) - 1):
                m = (pts[i - 1] + pts[i] * 2 + pts[i + 1]) / 4
                q = pts[i].copy()
                q.x, q.z = m.x, m.z
                q.y = min(q.y, m.y) if side < 0 else max(q.y, m.y)   # 몸 안쪽으로는 들어가지 않게
                out.append(q)
            out.append(pts[-1])
            pts = out
        return pts

    front, back = bridge(front, -1), bridge(back, 1)
    strap = front[::-1] + over + back
    strap.insert(0, Vector((bag_top.x, bag_top.y - 0.045, bag_top.z)))
    strap.append(Vector((bag_top.x, bag_top.y + 0.045, bag_top.z)))
    add(tube(strap, 0.014, 10), 'KHU_Adv_Leather', 'Body', 'body')


def shoe(style, s, side, ft):
    """신발 (발 뼈, 크기 그대로). 몸통 (발등 타원 · 밑창) 크기는 모든 신발이 같다."""
    bc = Vector((ft.x, ft.y - 0.045, ft.z + 0.085))
    kind = 'foot' + side
    if style == 'boot':
        add(ellipsoid(bc, (0.098, 0.155, 0.095)), 'KHU_Adv_Boot', 'Feet', kind)
        add(capsule(Vector((ft.x, ft.y + 0.015, ft.z + 0.1)), Vector((ft.x, ft.y + 0.015, ft.z + 0.17)), 0.088, 0.085), 'KHU_Adv_Boot', 'Feet', kind)
        add(ellipsoid(Vector((bc.x, bc.y, ft.z + 0.014)), (0.104, 0.165, 0.026)), 'KHU_Adv_Sole', 'Feet', kind)
        for i in range(2):   # 끈
            y = bc.y - 0.07 + i * 0.04
            add(capsule(Vector((bc.x - 0.04, y, bc.z + 0.06 + i * 0.02)), Vector((bc.x + 0.04, y, bc.z + 0.06 + i * 0.02)), 0.009), 'KHU_Adv_Sole', 'Feet', kind)
        return
    if style == 'sneaker':   # 흰 운동화: 두꺼운 밑창 + 옆 줄 + 끈
        add(ellipsoid(bc, (0.098, 0.155, 0.095)), 'KHU_Shoe_White', 'Feet', kind)
        add(capsule(Vector((ft.x, ft.y + 0.015, ft.z + 0.1)), Vector((ft.x, ft.y + 0.015, ft.z + 0.13)), 0.088, 0.085), 'KHU_Shoe_White', 'Feet', kind)
        add(ellipsoid(Vector((bc.x, bc.y, ft.z + 0.02)), (0.106, 0.167, 0.034)), 'KHU_Shoe_Sole', 'Feet', kind)
        for o in (1, -1):
            add(ellipsoid(Vector((bc.x + 0.088 * o, bc.y + 0.02, bc.z - 0.015)), (0.016, 0.075, 0.026), 16, 10), FIT['accent'], 'Feet', kind)
        for i in range(2):
            y = bc.y - 0.07 + i * 0.04
            add(capsule(Vector((bc.x - 0.04, y, bc.z + 0.06 + i * 0.02)), Vector((bc.x + 0.04, y, bc.z + 0.06 + i * 0.02)), 0.009), 'KHU_Shoe_Lace', 'Feet', kind)
        return
    # 로퍼: 발등 띠 + 짙은 밑창
    add(ellipsoid(bc, (0.098, 0.155, 0.095)), 'KHU_Neat_Loafer', 'Feet', kind)
    add(ellipsoid(Vector((bc.x, bc.y, ft.z + 0.016)), (0.104, 0.165, 0.028)), 'KHU_Neat_Sole', 'Feet', kind)
    add(capsule(Vector((bc.x - 0.075, bc.y - 0.03, bc.z + 0.045)), Vector((bc.x + 0.075, bc.y - 0.03, bc.z + 0.045)), 0.014), 'KHU_Neat_Sole', 'Feet', kind)


# 바지 (골반 + 두 다리) · 다리 · 양말 · 신발
# 골반: 허리 → 사타구니까지 반듯한 한 덩어리 (아래 모서리만 살짝 둥글게). 다리 통이 그 아래에서 이어진다.
hz = HIP['.L'].z
crotch_z = hz - 0.085
pelvis_y = HIP['.L'].y
add(loft(0, pelvis_y, [(crotch_z, 0.19, 0.118), (crotch_z + 0.02, 0.205, 0.13), (hz, 0.212, 0.136), (hz + 0.1, 0.205, 0.134), (hz + 0.13, 0.198, 0.13)]),
    FIT['pants'], 'Legs', 'pelvis')
for s, side in ((1, '.L'), (-1, '.R')):
    hip, knee, ft = HIP[side], KNEE[side], FOOT[side]
    lx = hip.x + 0.008 * s
    ankle = Vector((ft.x, ft.y + 0.01, ft.z + 0.1))
    if FIT['hem'] is None:   # 반바지 + 맨다리 + 양말
        short_end = hip + (knee - hip) * 0.7 + Vector((0.01 * s, 0, 0))
        add(loft(lx, hip.y, [(short_end.z, 0.106, 0.108), (crotch_z + 0.01, 0.104, 0.112), (crotch_z + 0.045, 0.1, 0.11)], 28),
            FIT['pants'], 'Legs', 'leg' + side)
        cuff = cylinder(Vector((lx, hip.y, short_end.z + 0.012)), 0.112, 0.11, 0.03, 28)
        add(cuff, FIT['pants_cuff'], 'Legs', 'leg' + side)   # 접단
        if OUTFIT == 'Adventurer':
            add(ellipsoid(Vector((lx + 0.1 * s, hip.y, crotch_z - 0.04)), (0.014, 0.055, 0.05), 14, 10), 'KHU_Adv_Olive', 'Legs', 'leg' + side)  # 옆주머니 (납작)
        add(capsule(Vector((lx, hip.y, short_end.z + 0.03)), ankle, 0.072, 0.066), 'Skin_Adv', 'Legs', 'leg' + side)
        add(cylinder(ankle + Vector((0, 0, 0.07)), 0.078, 0.084, 0.07), FIT['sock'], 'Legs', 'leg' + side)
    else:                    # 긴바지: 사타구니 → 발목까지 한 통 (무릎에서 굽게 잘게 나눈다, 안에 피부 없음)
        top_z, hem_z = crotch_z + 0.045, ft.z + FIT['hem']
        r_hem = FIT['hem_r']
        rings = []
        for i in range(19):
            z = top_z + (hem_z - top_z) * i / 18
            t = (hip.z - z) / (hip.z - hem_z)          # 0 엉덩이 → 1 밑단
            c = Vector((lx, hip.y)).lerp(Vector((ankle.x, ankle.y)), max(0.0, t))
            rx = 0.104 + (r_hem - 0.104) * t
            rings.append((z, rx, rx + 0.006 * (1 - t), c.x, c.y))
        add(loft(lx, hip.y, rings[::-1], 28), FIT['pants'], 'Legs', 'leg' + side)
        if FIT['pants_cuff']:   # 조거 밑단 시보리 · 청바지 접단
            add(cylinder(Vector((ankle.x, ankle.y, hem_z + 0.018)), r_hem + 0.006, r_hem + 0.004, 0.036, 28), FIT['pants_cuff'], 'Legs', 'leg' + side)
    shoe(FIT['shoe'], s, side, ft)

# ---------------------------------------------------------------- 뼈 가중치
def ramp(x, a, b):
    return min(1.0, max(0.0, (x - a) / (b - a)))


def weights(p, kind):
    """보이는 모습의 점 p 에 붙일 뼈 가중치 {뼈 이름: 가중치}."""
    if kind == 'head':
        return {'Head': 1.0}
    if kind.startswith('foot'):
        return {'Foot' + kind[-2:]: 1.0}
    if kind.startswith('arm'):
        side = kind[-2:]
        sh, el = SH[side], shown('LowerArm' + side)
        t = abs(p.x - sh.x)
        w_el = ramp(abs(p.x), abs(el.x) - 0.04, abs(el.x) + 0.04)
        w_ch = 1 - ramp(t, 0.0, 0.05)
        w = {'Chest': w_ch * 0.6, 'UpperArm' + side: (1 - w_el), 'LowerArm' + side: w_el}
        if abs(p.x) > abs(WRIST[side].x) - 0.01:
            w = {'Wrist' + side: 1.0}
        return w
    if kind.startswith('leg'):
        side = kind[-2:]
        k = KNEE[side].z
        w_low = 1 - ramp(p.z, k - 0.05, k + 0.05)
        w_hip = 1 - ramp(HIP[side].z - p.z, 0.0, 0.06)
        return {'Hips': w_hip * 0.5, 'UpperLeg' + side: 1 - w_low, 'LowerLeg' + side: w_low}
    if kind == 'pelvis':
        return {'Hips': 1.0}
    # 몸통: 아래부터 Hips → Abdomen → Torso → Chest
    z = p.z
    zs = [shown(b).z for b in ('Hips', 'Abdomen', 'Torso', 'Chest', 'Neck')]
    names = ['Hips', 'Abdomen', 'Torso', 'Chest']
    for i, n in enumerate(names):
        if z < zs[i + 1] or i == len(names) - 1:
            if i == 0:
                return {n: 1.0}
            t = ramp(z, zs[i] - 0.04, zs[i] + 0.04)
            return {names[i - 1]: 1 - t, n: t}
    return {'Chest': 1.0}


def kind_of(kind, p):
    """뼈 가중치가 가장 큰 쪽의 크기 변환을 쓴다 (관절 가까이서는 두 변환이 거의 같다)."""
    if kind.startswith('arm'):
        return 'arm', kind[-2:]
    if kind.startswith('leg'):
        return 'leg', kind[-2:]
    if kind.startswith('foot'):
        return 'foot', kind[-2:]
    if kind == 'head':
        return 'head', ''
    return 'body', ''


def build_objects(rest):
    """부위마다 메시 하나 (재질 여러 개). rest=True 면 원래 뼈대 크기로 바꾸고 가중치를 넣는다."""
    objs = {}
    for part in dict.fromkeys(it[2] for it in PARTS):
        items = [it for it in PARTS if it[2] == part]
        name = part if part.startswith('Face_') else f'KHU_{OUTFIT}_{part}'   # Unity 가 Face_ 이름으로 갈래를 찾는다
        mesh = bpy.data.meshes.new(name)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        mats = []
        big = bmesh.new()
        layer = big.verts.layers.int.new('kind')
        kinds = []
        for bm, mat, _, kind in items:
            if mat not in mats:
                mats.append(mat)
            tmp = bmesh.new()
            tmp.from_mesh(bpy.data.meshes.new('tmp')) if False else None
            m = bpy.data.meshes.new('tmp')
            bm.to_mesh(m)
            for poly in m.polygons:
                poly.material_index = mats.index(mat)
            if kind not in kinds:
                kinds.append(kind)
            base = len(big.verts)
            big.from_mesh(m)
            big.verts.ensure_lookup_table()
            for v in big.verts[base:]:
                v[layer] = kinds.index(kind)
            bpy.data.meshes.remove(m)
        for mat in mats:
            mesh.materials.append(material(mat))
        big.verts.ensure_lookup_table()
        shown_pos = [v.co.copy() for v in big.verts]
        vkinds = [kinds[v[layer]] for v in big.verts]
        if rest:
            for v, kind in zip(big.verts, vkinds):
                k, side = kind_of(kind, v.co)
                v.co = to_rest(v.co, k, side)
        big.to_mesh(mesh)
        big.free()
        for poly in mesh.polygons:
            poly.use_smooth = True
        if part.startswith(('Face_Eyes', 'Face_Brows')):
            # 꾸미기 슬라이더: 눈 크기 · 간격 · 높이 (눈썹도 눈을 따라 움직인다)
            obj.shape_key_add(name='Basis')
            moved = [eye_keys(p) for p in shown_pos]
            for name in moved[0]:
                key = obj.shape_key_add(name=name, from_mix=False)
                for i, m in enumerate(moved):
                    key.data[i].co = to_rest(m[name], 'head', '') if rest else m[name]
        if rest:
            groups = {}
            for i, (p, kind) in enumerate(zip(shown_pos, vkinds)):
                w = {n: x for n, x in weights(p, kind).items() if x > 1e-3}
                total = sum(w.values())
                for n, x in w.items():
                    if n not in groups:
                        groups[n] = obj.vertex_groups.new(name=n)
                    groups[n].add([i], x / total, 'REPLACE')
            obj.parent = RIG
            mod = obj.modifiers.new('Armature', 'ARMATURE')
            mod.object = RIG
        objs[part] = obj
    return objs


def preview(out):
    """--face=눈,눈썹,입,코,볼 (번호, -1 은 없음) · --tint=skin:#hex,hair:#hex,eye:#hex · --keys=Eye_Big:1 · --view=face (얼굴 가까이)"""
    objs = build_objects(rest=False)
    face = dict(zip(('Eyes', 'Brows', 'Mouth', 'Nose', 'Cheeks'), map(int, OPT.get('face', '0,0,0,0,0').split(','))))
    for part in [p for p in objs if p.startswith('Face_')]:
        _, cat, n = part.split('_')
        if face[cat] != int(n):
            bpy.data.objects.remove(objs.pop(part))
    for item in filter(None, OPT.get('tint', '').split(',')):
        k, hexstr = item.split(':')
        mat = {'skin': 'Skin_Adv', 'hair': 'KHU_Adv_Hair', 'eye': 'KHU_Adv_Eye'}[k]
        bpy.data.materials[mat].node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*srgb(hexstr), 1)
    for item in filter(None, OPT.get('keys', '').split(',')):
        k, v = item.split(':')
        for o in objs.values():
            if o.data.shape_keys and k in o.data.shape_keys.key_blocks:
                o.data.shape_keys.key_blocks[k].value = float(v)
    RIG.hide_render = True
    sc = bpy.context.scene
    close = OPT.get('view') == 'face'
    views = [0] if close else [0, 90, 180]   # 앞 · 오른쪽 옆 · 뒤
    holders = []
    for i, yaw in enumerate(views):
        h = bpy.data.objects.new(f'view{i}', None)
        sc.collection.objects.link(h)
        h.location.x = i * 1.2
        h.rotation_euler.z = math.radians(yaw)
        holders.append(h)
    for i, h in enumerate(holders):
        for part, o in objs.items():
            c = o.copy() if i else o
            if i:
                sc.collection.objects.link(c)
            c.parent = h
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 0.75 if close else 3.7
    cam.location = (0, -8, HEAD.z + 0.2) if close else (1.2, -8, 0.95)
    cam.rotation_euler = (math.radians(90), 0, 0)
    sc.camera = cam
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(55), 0, math.radians(-30))
    sc.collection.objects.link(sun)
    w = bpy.data.worlds.new('w')
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.95, 0.93, 0.88, 1)
    w.node_tree.nodes['Background'].inputs[1].default_value = 1.0
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 48
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = (700, 700) if close else (1500, 800)
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)


def export():
    objs = build_objects(rest=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    for part, o in objs.items():
        if part.startswith('Face_') or (part == 'Head' and OUTFIT != 'Adventurer'):
            continue   # 얼굴 갈래는 머리 FBX 에 같이 넣는다. 다른 옷은 경희 모험가 머리를 같이 쓴다
        bpy.ops.object.select_all(action='DESELECT')
        RIG.select_set(True)
        o.select_set(True)
        if part == 'Head':
            for p, f in objs.items():
                if p.startswith('Face_'):
                    f.select_set(True)
        bpy.context.view_layer.objects.active = RIG
        path = os.path.join(OUT_DIR, f'KHU_{OUTFIT}_{part}.fbx')
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE')
        print('wrote', os.path.normpath(path), len(o.data.polygons), 'faces')


if 'preview' in OPT:
    preview(OPT['preview'])
else:
    export()
