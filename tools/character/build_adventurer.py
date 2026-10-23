"""
경희 모험가 플레이어 캐릭터 (Art/Concept/07_Player 시트) 를 블렌더에서 도형으로 빚어 Quaternius 뼈대에 붙인다.

    blender -b --python tools/character/build_adventurer.py -- [--gender=men|women] [--preview=out.png]

- 모양은 "보이는 모습" 기준으로 만든다: Unity ModularCharacter 가 머리 1.8배 · 다리 0.68배 · 팔 0.85배로
  바꿔 보여 주므로, 내보낼 때는 그 반대로 줄이고 늘려 원래 뼈대 크기(rest)에 맞춘다.
- 부위별 FBX 4개 (머리 · 상의 · 하의 · 신발) → My project/Assets/Characters/{Men,Women}/KHU_Adventurer/
  Unity 메뉴 KUCA › Build Character Database 를 누르면 꾸미기에 "경희 모험가" 로 나온다.
- 재질 이름은 KHU_ / Skin 으로 시작해 Unity 가 파스텔로 바꾸지 않고 그림 색 그대로 쓴다.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OPT = dict(a[2:].split('=', 1) for a in ARGS if a.startswith('--') and '=' in a)
GENDER = OPT.get('gender', 'men')
FOLDER = {'men': 'Men', 'women': 'Women'}[GENDER]
HERE = os.path.dirname(os.path.abspath(__file__))
CHAR = os.path.join(HERE, '..', '..', 'My project', 'Assets', 'Characters')
SKELETON = {'men': 'Men/Casual_2/Casual2_Body.fbx', 'women': 'Women/Casual/Casual_Body.fbx'}[GENDER]
OUT_DIR = os.path.join(CHAR, FOLDER, 'KHU_Adventurer')

# Unity ModularCharacter 기본값과 같게
HEAD_SCALE, LEG_SCALE, ARM_SCALE = 1.8, 0.68, 0.85

# 시트 색 (sRGB)
COLORS = {
    'Skin_Adv': '#F4C7A8', 'KHU_Adv_Hair': '#5A3A2A', 'KHU_Adv_Hat': '#C9B48A', 'KHU_Adv_Crimson': '#A6192E',
    'KHU_Adv_Cream': '#F3EBD8', 'KHU_Adv_Olive': '#7C8C5A', 'KHU_Adv_Boot': '#B77A43', 'KHU_Adv_Sole': '#7A4A28',
    'KHU_Adv_Leather': '#8E5A35', 'KHU_Adv_Eye': '#3A2620', 'KHU_Adv_White': '#FFFFFF', 'KHU_Adv_Blush': '#F29C93',
    'KHU_Adv_Mouth': '#B5524A', 'KHU_Adv_Paper': '#EADBB8',
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
    """z 마다 타원 단면 (z, 반지름 x, 반지름 y) 을 이은 통. 위 · 아래는 막는다."""
    bm = bmesh.new()
    loops = []
    for z, rx, ry in rings:
        loops.append([bm.verts.new((cx + math.cos(2 * math.pi * i / segs) * rx, cy + math.sin(2 * math.pi * i / segs) * ry, z))
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
for s in (1, -1):
    eye = Vector((0.085 * s, face_y + 0.012, hc.z - 0.035))
    add(ellipsoid(eye, (0.03, 0.014, 0.042), 16, 12), 'KHU_Adv_Eye', 'Head', 'head')
    add(ellipsoid(eye + Vector((0.009 * s, -0.012, 0.016)), (0.009, 0.005, 0.01), 10, 8), 'KHU_Adv_White', 'Head', 'head')
    add(capsule(eye + Vector((-0.025, 0.0, 0.07)), eye + Vector((0.028, 0.004, 0.075)), 0.008), 'KHU_Adv_Hair', 'Head', 'head')  # 눈썹
    add(ellipsoid(Vector((0.135 * s, face_y + 0.035, hc.z - 0.09)), (0.04, 0.01, 0.025), 14, 10), 'KHU_Adv_Blush', 'Head', 'head')
add(ellipsoid(Vector((0, face_y - 0.004, hc.z - 0.075)), (0.014, 0.01, 0.016), 12, 10), 'Skin_Adv', 'Head', 'head')  # 코
mouth = bmesh.new()
bmesh.ops.create_circle(mouth, cap_ends=False, segments=24, radius=1)
pts = [Vector((math.cos(a) * 0.04, 0, -math.sin(a) * 0.022)) for a in [math.pi * (0.15 + 0.7 * i / 10) for i in range(11)]]
bmesh.ops.delete(mouth, geom=mouth.verts[:], context='VERTS')
for i in range(len(pts) - 1):
    seg = capsule(pts[i] + Vector((0, face_y + 0.012, hc.z - 0.115)), pts[i + 1] + Vector((0, face_y + 0.012, hc.z - 0.115)), 0.006, None, 8, 6)
    add(seg, 'KHU_Adv_Mouth', 'Head', 'head')

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

# 몸통: 크림슨 티 + 앞이 열린 크림 셔츠 (반팔)
body_top = HEAD.z - 0.02
body_bot = HIP['.L'].z + 0.07      # 셔츠 밑단 (반바지 허리를 올려 상체가 짧아 보이게)
tc = Vector((0, -0.005, (body_top + body_bot) / 2))
TORSO = (0.2, 0.135, (body_top - body_bot) / 2 + 0.03)
add(ellipsoid(tc, TORSO), 'KHU_Adv_Crimson', 'Body', 'body')
SHIRT_C = tc + Vector((0, 0, -0.01))
SHIRT_R = (0.218, 0.152, (body_top - body_bot) / 2 + 0.04)


def shirt_profile(z):
    """셔츠 단면 반지름 (몸통 타원을 따르되 밑단은 오므리지 않는다)."""
    k = max(0.0, 1 - ((z - SHIRT_C.z) / SHIRT_R[2]) ** 2) ** 0.5
    if z < SHIRT_C.z:
        k = max(k, 0.93)
    return SHIRT_R[0] * k, SHIRT_R[1] * k


def shirt_open(zs, segs=72):
    """앞이 V 로 열린 셔츠: 각 높이마다 앞 (-y) 가운데를 빼고 둘레를 잇는다. 가장자리가 매끈하다."""
    bm = bmesh.new()
    rows = []
    for z in zs:
        rx, ry = shirt_profile(z)
        gap = 0.07 + max(0.0, z - (body_top - 0.22)) * 0.3          # 앞섶 반폭 (m), 위로 갈수록 넓게
        half = math.asin(min(0.95, gap / max(rx, 1e-3)))
        a0 = -math.pi / 2 + half
        a1 = 3 * math.pi / 2 - half
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


zs = [body_bot - 0.03 + (body_top - 0.01 - (body_bot - 0.03)) * i / 30 for i in range(31)]
add(shirt_open(zs), 'KHU_Adv_Cream', 'Body', 'body')
for side in (1, -1):   # 앞섶 가장자리 두께 (얇은 껍질이 종이처럼 보이지 않게)
    edge = []
    for z in zs:
        rx, ry = shirt_profile(z)
        gap = 0.07 + max(0.0, z - (body_top - 0.22)) * 0.3
        half = math.asin(min(0.95, gap / max(rx, 1e-3)))
        a = -math.pi / 2 + half * side
        edge.append(Vector((SHIRT_C.x + math.cos(a) * rx, SHIRT_C.y + math.sin(a) * ry, z)))
    add(tube(edge, 0.008, 8), 'KHU_Adv_Cream', 'Body', 'body')
for s in (1, -1):   # 깃
    collar = capsule(Vector((0.03 * s, tc.y - 0.08, body_top + 0.0)), Vector((0.11 * s, tc.y - 0.02, body_top - 0.01)), 0.02, 0.016, 12, 8)
    add(collar, 'KHU_Adv_Cream', 'Body', 'body')
add(cylinder(Vector((0, tc.y + 0.01, body_top + 0.0)), 0.075, 0.07, 0.06), 'Skin_Adv', 'Body', 'body')   # 목

# 팔 (T 자세: 어깨에서 +x / -x 로 곧게)
for s, side in ((1, '.L'), (-1, '.R')):
    sh, wr = SH[side], WRIST[side]
    sleeve_end = sh + (wr - sh) * 0.5
    add(capsule(sh + Vector((-0.03 * s, 0, 0)), sleeve_end, 0.085, 0.078), 'KHU_Adv_Cream', 'Body', 'arm' + side)
    add(capsule(sh + Vector((0.02 * s, 0, 0)), wr, 0.058, 0.052), 'Skin_Adv', 'Body', 'arm' + side)
    add(ellipsoid(wr + Vector((0.055 * s, -0.005, 0)), (0.065, 0.045, 0.058)), 'Skin_Adv', 'Body', 'arm' + side)
    add(ellipsoid(wr + Vector((0.035 * s, -0.04, 0.02)), (0.025, 0.022, 0.025), 12, 10), 'Skin_Adv', 'Body', 'arm' + side)   # 엄지

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


strap = []
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
arc = []
y0, y1 = front[0].y, back[0].y
for i in range(1, 12):   # 어깨 위: 위에서 아래로 쏴서 어깨 윗면을 따라간다
    y = y0 + (y1 - y0) * i / 12
    p = on_body((sx, y, top_z + 1.0), (0, 0, -1))
    if p is not None:
        arc.append(p)
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
strap = front[::-1] + arc + back
strap.insert(0, Vector((bag_top.x, bag_top.y - 0.045, bag_top.z)))
strap.append(Vector((bag_top.x, bag_top.y + 0.045, bag_top.z)))
add(tube(strap, 0.014, 10), 'KHU_Adv_Leather', 'Body', 'body')

# 반바지 (골반 + 두 다리) · 다리 · 양말
# 골반: 허리 → 사타구니까지 반듯한 한 덩어리 (아래 모서리만 살짝 둥글게). 다리 통이 그 아래에서 이어진다.
hz = HIP['.L'].z
crotch_z = hz - 0.085
pelvis_y = HIP['.L'].y
add(loft(0, pelvis_y, [(crotch_z, 0.19, 0.118), (crotch_z + 0.02, 0.205, 0.13), (hz, 0.212, 0.136), (hz + 0.1, 0.205, 0.134), (hz + 0.13, 0.198, 0.13)]),
    'KHU_Adv_Olive', 'Legs', 'pelvis')
for s, side in ((1, '.L'), (-1, '.R')):
    hip, knee, ft = HIP[side], KNEE[side], FOOT[side]
    short_end = hip + (knee - hip) * 0.7 + Vector((0.01 * s, 0, 0))
    lx = hip.x + 0.008 * s
    add(loft(lx, hip.y, [(short_end.z, 0.106, 0.108), (crotch_z + 0.01, 0.104, 0.112), (crotch_z + 0.045, 0.1, 0.11)], 28),
        'KHU_Adv_Olive', 'Legs', 'leg' + side)
    cuff = cylinder(Vector((lx, hip.y, short_end.z + 0.012)), 0.112, 0.11, 0.03, 28)
    add(cuff, 'KHU_Adv_Olive', 'Legs', 'leg' + side)   # 접단
    add(ellipsoid(Vector((lx + 0.1 * s, hip.y, crotch_z - 0.04)), (0.014, 0.055, 0.05), 14, 10), 'KHU_Adv_Olive', 'Legs', 'leg' + side)  # 옆주머니 (납작)
    ankle = Vector((ft.x, ft.y + 0.01, ft.z + 0.1))
    add(capsule(Vector((lx, hip.y, short_end.z + 0.03)), ankle, 0.072, 0.066), 'Skin_Adv', 'Legs', 'leg' + side)
    add(cylinder(ankle + Vector((0, 0, 0.07)), 0.078, 0.084, 0.07), 'KHU_Adv_Cream', 'Legs', 'leg' + side)

    # 부츠 (발 뼈, 크기 그대로)
    bc = Vector((ft.x, ft.y - 0.045, ft.z + 0.085))
    boot = ellipsoid(bc, (0.098, 0.155, 0.095))
    add(boot, 'KHU_Adv_Boot', 'Feet', 'foot' + side)
    add(capsule(Vector((ft.x, ft.y + 0.015, ft.z + 0.1)), Vector((ft.x, ft.y + 0.015, ft.z + 0.17)), 0.088, 0.085), 'KHU_Adv_Boot', 'Feet', 'foot' + side)
    sole = ellipsoid(Vector((bc.x, bc.y, ft.z + 0.014)), (0.104, 0.165, 0.026))
    add(sole, 'KHU_Adv_Sole', 'Feet', 'foot' + side)
    for i in range(2):   # 끈
        y = bc.y - 0.07 + i * 0.04
        add(capsule(Vector((bc.x - 0.04, y, bc.z + 0.06 + i * 0.02)), Vector((bc.x + 0.04, y, bc.z + 0.06 + i * 0.02)), 0.009), 'KHU_Adv_Sole', 'Feet', 'foot' + side)


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
    for part in ('Head', 'Body', 'Legs', 'Feet'):
        items = [it for it in PARTS if it[2] == part]
        mesh = bpy.data.meshes.new(f'KHU_Adventurer_{part}')
        obj = bpy.data.objects.new(f'KHU_Adventurer_{part}', mesh)
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
    objs = build_objects(rest=False)
    RIG.hide_render = True
    sc = bpy.context.scene
    views = [0, 90, 180]   # 앞 · 오른쪽 옆 · 뒤
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
    cam.data.ortho_scale = 3.7
    cam.location = (1.2, -8, 0.95)
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
    sc.render.resolution_x, sc.render.resolution_y = 1500, 800
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)


def export():
    objs = build_objects(rest=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    for part, o in objs.items():
        bpy.ops.object.select_all(action='DESELECT')
        RIG.select_set(True)
        o.select_set(True)
        bpy.context.view_layer.objects.active = RIG
        path = os.path.join(OUT_DIR, f'KHU_Adventurer_{part}.fbx')
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE')
        print('wrote', os.path.normpath(path), len(o.data.polygons), 'faces')


if 'preview' in OPT:
    preview(OPT['preview'])
else:
    export()
