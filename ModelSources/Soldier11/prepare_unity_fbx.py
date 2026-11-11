"""Run inside the open Blender scene through its MCP connection.

The untouched PMX import is saved as Soldier11_Original.blend first.
"""

import bpy
import json
import os
import re
import shutil


PROJECT = r"D:\Portfolio\AlphaZero"
SOURCE_DIR = os.path.join(PROJECT, "ModelSources", "Soldier11")
ASSET_DIR = os.path.join(PROJECT, "Assets", "00Arts", "InGame", "Character", "Soldier11")
TEXTURE_DIR = os.path.join(ASSET_DIR, "Textures")
FBX_PATH = os.path.join(ASSET_DIR, "Soldier11_UnityReady.fbx")
BLEND_PATH = os.path.join(SOURCE_DIR, "Soldier11_UnityReady.blend")

source = bpy.data.objects["11号_mesh"]
rig = bpy.data.objects["11号_arm"]
root = bpy.data.objects["11号"]
assert len(source.data.polygons) == 34097
assert len(source.data.shape_keys.key_blocks) == 56

# The goggle frame shares the Outfit material. Its disconnected geometry sits
# entirely above the collar, so classify those faces by position before the
# material-based split. The lenses have their own Goggles material.
goggle_frame_faces = {
    polygon.index
    for polygon in source.data.polygons
    if polygon.material_index == 12
    and all((source.matrix_world @ source.data.vertices[index].co).z > 1.35
            for index in polygon.vertices)
}
assert len(goggle_frame_faces) == 162, len(goggle_frame_faces)
goggle_faces = goggle_frame_faces | {
    polygon.index for polygon in source.data.polygons if polygon.material_index == 20
}
assert len(goggle_faces) == 192, len(goggle_faces)

# Each functional part has its own SkinnedMeshRenderer while all parts keep
# weights for the same skeleton.
material_parts = {
    "Face": set(range(0, 12)),
    "Outfit": {12, 13},
    "Backpack": {14},
    "Body": {15, 16},
    "Hair": {17, 21},
    "Weapon": {18, 19},
}
parts = {
    name: {
        polygon.index for polygon in source.data.polygons
        if polygon.material_index in slots and polygon.index not in goggle_faces
    }
    for name, slots in material_parts.items()
}
parts["Goggles"] = goggle_faces
expected = {
    name: len(face_indices) for name, face_indices in parts.items()
}
assert sum(expected.values()) == len(source.data.polygons)
assert set().union(*parts.values()) == set(range(len(source.data.polygons)))

old_test = bpy.data.objects.get("Soldier11_Face_TEST")
if old_test:
    bpy.data.objects.remove(old_test, do_unlink=True)

created = []
for part_name, face_indices in parts.items():
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = "Soldier11_" + part_name
    obj.data.name = obj.name + "_Mesh"
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    # Edit-mode deletion updates all shape keys and keeps the original UV loops
    # and vertex weights. FACE deletion keeps isolated source polygons.
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    for polygon in obj.data.polygons:
        polygon.select = polygon.index not in face_indices
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.delete(type="FACE")
    bpy.ops.object.mode_set(mode="OBJECT")
    assert len(obj.data.polygons) == expected[part_name], part_name

    if part_name != "Face":
        obj.shape_key_clear()
        assert obj.data.shape_keys is None
    else:
        assert len(obj.data.shape_keys.key_blocks) == 56

    bpy.ops.object.material_slot_remove_unused()
    weighted = {
        assignment.group
        for vertex in obj.data.vertices
        for assignment in vertex.groups
        if assignment.weight > 1e-7
    }
    for group in reversed(list(obj.vertex_groups)):
        if group.index not in weighted or group.name in {"mmd_vertex_order", "mmd_edge_scale"}:
            obj.vertex_groups.remove(group)
    assert any(mod.type == "ARMATURE" and mod.object == rig for mod in obj.modifiers)
    created.append(obj)

assert sum(len(obj.data.polygons) for obj in created) == 34097

# English names for imported material slots and external texture assets.
material_names = [
    "Face", "FaceOverlay", "Eyelashes", "Eyebrows", "EyelidCrease",
    "MouthLine", "EyeWhite", "Iris", "Tongue", "MouthInterior", "Teeth",
    "EyeShadow", "Outfit", "Boots", "BackAccessory", "Skin", "SkinDetail",
    "Hair", "Weapon", "Blade", "Goggles", "HairOverlay",
]
for index, name in enumerate(material_names):
    material = source.material_slots[index].material
    if material:
        material.name = "Soldier11_" + name

texture_names = {
    "hair_s.bmp": "HairSpecular.bmp",
    "metal.png": "Metal.png",
    "skin.bmp": "SkinToon.bmp",
    "sp.png": "SharedDetails.png",
    "toon01.bmp": "Toon01.bmp",
    "toon_defo.bmp": "ToonDefault.bmp",
    "体.png": "Outfit.png",
    "武器.png": "Weapon.png",
    "肌.png": "Skin.png",
    "颜.png": "Face.png",
    "髮.png": "Hair.png",
}
os.makedirs(TEXTURE_DIR, exist_ok=True)
copied_textures = []
for image in list(bpy.data.images):
    if image.source != "FILE":
        continue
    old_path = bpy.path.abspath(image.filepath)
    new_name = texture_names.get(os.path.basename(old_path))
    if not new_name:
        continue
    assert os.path.isfile(old_path), old_path
    destination = os.path.join(TEXTURE_DIR, new_name)
    shutil.copy2(old_path, destination)
    image.filepath = destination
    image.name = "Soldier11_" + os.path.splitext(new_name)[0]
    copied_textures.append(new_name)

shutil.copy2(
    r"C:\Users\User\Downloads\09KDQnNr10\readme【一定要看】.txt",
    os.path.join(SOURCE_DIR, "Original_Model_License.txt"),
)

# FBX does not translate the MMD shader graph. Connect each diffuse image to a
# standard Principled material so Unity can resolve the corresponding texture.
for material in source.data.materials:
    if not material or not material.use_nodes:
        continue
    image = next(
        (node.image for node in material.node_tree.nodes
         if node.type == "TEX_IMAGE" and node.name == "mmd_base_tex" and node.image),
        None,
    )
    if not image:
        continue
    nodes = material.node_tree.nodes
    nodes.clear()
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "Unity_BaseColor"
    texture.image = image
    texture.location = (-400, 0)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (-100, 0)
    shader.inputs["Roughness"].default_value = 0.8
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (200, 0)
    links = material.node_tree.links
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    links.new(texture.outputs["Alpha"], shader.inputs["Alpha"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])

# Use the PMX English labels when available, with stable unique fallbacks.
def ascii_name(value):
    value = re.sub(r"[^A-Za-z0-9_.]+", "_", value or "").strip("_.")
    return value if re.search(r"[A-Za-z]", value) else ""


core_bones = {
    "全ての親": "Root", "センター": "Center", "グルーブ": "Groove",
    "腰": "Waist", "下半身": "Hips", "上半身": "Spine",
    "上半身1": "Chest", "上半身2": "UpperChest",
    "首": "Neck", "頭": "Head",
    "肩": "Shoulder", "腕": "UpperArm", "ひじ": "LowerArm",
    "手首": "Hand", "足": "UpperLeg", "ひざ": "LowerLeg",
    "足首": "Foot", "足D": "UpperLegDeform", "ひざD": "LowerLegDeform",
    "足首D": "FootDeform", "足先EX": "Toe",
}
bone_map = {}
reserved = set()
for index, bone in enumerate(list(rig.data.bones), 1):
    old_name = bone.name
    side = "Left" if old_name.endswith(".L") else "Right" if old_name.endswith(".R") else ""
    base = old_name[:-2] if side else old_name
    candidate = core_bones.get(base)
    if candidate:
        candidate = side + candidate
    else:
        english = ascii_name(rig.pose.bones[old_name].mmd_bone.name_e)
        candidate = (side + english if side else english) if english else (f"Bone_{index:03d}" + ("_" + side if side else ""))
    unique = candidate
    suffix = 2
    while unique in reserved:
        unique = f"{candidate}_{suffix:02d}"
        suffix += 1
    reserved.add(unique)
    bone_map[old_name] = unique

for old_name, new_name in bone_map.items():
    rig.data.bones[old_name].name = new_name

for obj in created:
    for old_name, new_name in bone_map.items():
        group = obj.vertex_groups.get(old_name)
        if group:
            group.name = new_name
    weighted_names = {
        obj.vertex_groups[assignment.group].name
        for vertex in obj.data.vertices
        for assignment in vertex.groups
        if assignment.weight > 1e-7
    }
    assert weighted_names <= set(rig.data.bones.keys()), (obj.name, weighted_names - set(rig.data.bones.keys()))

face = next(obj for obj in created if obj.name == "Soldier11_Face")
morph_labels = {m.name: m.name_e for m in root.mmd_root.vertex_morphs}
shape_map = {}
reserved = {"Basis"}
for index, key in enumerate(list(face.data.shape_keys.key_blocks)[1:], 1):
    old_name = key.name
    candidate = ascii_name(morph_labels.get(old_name, "")) or f"Expression_{index:02d}"
    unique = candidate
    suffix = 2
    while unique in reserved:
        unique = f"{candidate}_{suffix:02d}"
        suffix += 1
    reserved.add(unique)
    key.name = unique
    shape_map[old_name] = unique

root.name = "Soldier11"
rig.name = "Soldier11_Rig"
rig.data.name = "Soldier11_Skeleton"
bpy.data.objects.remove(source, do_unlink=True)

with open(os.path.join(SOURCE_DIR, "NameMapping.json"), "w", encoding="utf-8") as output:
    json.dump({"bones": bone_map, "shape_keys": shape_map}, output, ensure_ascii=False, indent=2)

# Physics helpers are PMX-specific; keep the Blender source editable while
# exporting only the skeleton and seven weighted meshes to Unity.
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
for obj in created:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
# Keep PMX rigid-body helpers available for later editing, but out of the
# character viewport and renders. They are excluded from the FBX selection.
for obj in bpy.data.objects:
    if obj not in created and obj not in {rig, root}:
        obj.hide_set(True)
        obj.hide_render = True
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, check_existing=False)
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={"MESH", "ARMATURE"},
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    # Unity에 100배 로컬 Transform 대신 미터 단위를 기록한다.
    apply_scale_options="FBX_SCALE_UNITS",
    use_mesh_modifiers=False,
    add_leaf_bones=False,
    use_armature_deform_only=True,
    bake_anim=False,
    path_mode="COPY",
    embed_textures=True,
)

print(json.dumps({
    "blend": BLEND_PATH,
    "fbx": FBX_PATH,
    "meshes": {obj.name: {"polygons": len(obj.data.polygons), "vertices": len(obj.data.vertices), "shape_keys": len(obj.data.shape_keys.key_blocks) if obj.data.shape_keys else 0} for obj in created},
    "bones": len(rig.data.bones),
    "textures": copied_textures,
}, ensure_ascii=False))
