"""
경희대 과잠 상의 만들기 → My project/Assets/Characters/{Men,Women}/KHU/KHU_Body.fbx

    blender -b --python tools/character/build_khu_outfit.py

Quaternius 긴소매 상의를 그대로 쓰고 색만 다시 칠한다 (뼈대 · 가중치는 그대로라 꾸미기 · 걷기 동작이 맞는다).
  몸통 = 경희 빨강, 소매(어깨 관절 바깥의 어깨 · 팔 · 손목 뼈 가중치가 가장 큰 면) = 크림색, 손(Skin 재질)은 그대로.
만든 뒤 Unity 메뉴 KUCA › Build Character Database 를 다시 누르면 꾸미기에 "경희대 과잠" 이 생긴다.
"""
import os
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
CHAR = os.path.join(HERE, '..', '..', 'My project', 'Assets', 'Characters')
# (성별 폴더, 바탕 상의 FBX)
SOURCES = [('Men', 'Casual_Hoodie/Casual_Body.fbx'), ('Women', 'Suit/Suit_Body.fbx')]
KHU_RED = (0.42, 0.03, 0.07)     # 선형 색. sRGB 로 약 #AC2049
CREAM = (0.90, 0.85, 0.72)       # sRGB 약 #F3EEDD
SLEEVE_X = 0.14                  # 어깨 관절(약 0.15m) 근처부터 소매 (겨드랑이 몸통 면은 빨강으로 남게)


def material(name, color):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes['Principled BSDF']
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = 0.6
    return m


for gender, src in SOURCES:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(CHAR, gender, src))
    body = next(o for o in bpy.data.objects if o.type == 'MESH')
    groups = {g.index: g.name for g in body.vertex_groups}
    skin = {i for i, m in enumerate(body.data.materials) if m and 'Skin' in m.name}
    body.data.materials.append(material('KHU_Red', KHU_RED))
    red = len(body.data.materials) - 1
    body.data.materials.append(material('KHU_Cream', CREAM))
    cream = len(body.data.materials) - 1
    for f in body.data.polygons:
        if f.material_index in skin:
            continue
        w = {}
        for vi in f.vertices:
            for g in body.data.vertices[vi].groups:
                w[groups[g.group]] = w.get(groups[g.group], 0) + g.weight
        top = max(w, key=w.get) if w else ''
        x = abs((body.matrix_world @ f.center).x)   # 월드 m (FBX 축 · 크기 변환 반영)
        sleeve = ('Arm' in top or 'Wrist' in top or 'Shoulder' in top) and x > SLEEVE_X
        f.material_index = cream if sleeve or x > SLEEVE_X + 0.04 else red
    with bpy.context.temp_override(object=body, active_object=body, selected_objects=[body]):
        bpy.ops.object.material_slot_remove_unused()
    body.name = body.data.name = 'KHU_Body'

    out_dir = os.path.join(CHAR, gender, 'KHU')
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, 'KHU_Body.fbx')
    bpy.ops.export_scene.fbx(filepath=out, use_selection=False, object_types={'ARMATURE', 'MESH'},
                             add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE')
    print('wrote', os.path.normpath(out), [m.name for m in body.data.materials])
