"""Split the imported Anby PMX into weighted Unity meshes and export FBX files.

Run on Anby_Original.blend. The original file is never modified.
"""

import bpy
import json
import re
import shutil
from pathlib import Path


PROJECT = Path(r"D:\Portfolio\AlphaZero")
SOURCE_DIR = PROJECT / "ModelSources" / "Anby"
ASSET_DIR = PROJECT / "Assets" / "00Arts" / "InGame" / "Character" / "Anby"
TEXTURE_DIR = ASSET_DIR / "Textures"
INPUT_DIR = Path(r"C:\Users\User\Downloads\MyGtYUhe6t")
BLEND_PATH = SOURCE_DIR / "Anby_UnityReady.blend"
CHARACTER_FBX = ASSET_DIR / "Anby_UnityReady.fbx"
WEAPON_FBX = ASSET_DIR / "Anby_Weapon.fbx"

source = bpy.data.objects["安比_mesh"]
rig = bpy.data.objects["安比_arm"]
root = bpy.data.objects["安比"]
weapon = bpy.data.objects["刀_mesh"]
weapon_rig = bpy.data.objects["刀_arm"]
weapon_root = bpy.data.objects["刀"]
assert len(source.data.polygons) == 30678
assert len(source.data.shape_keys.key_blocks) == 57
assert len(weapon.data.polygons) == 2494
assert len(rig.data.bones) == 285

ASSET_DIR.mkdir(parents=True, exist_ok=True)
TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
shutil.copy2(INPUT_DIR / "readme【一定要看】.txt", SOURCE_DIR / "Original_Model_License.txt")

# Separate parts by their original material slots. Every copy retains the same
# armature modifier and vertex weights, including the skirt and outfit.
material_parts = {
    "Face": set(range(0, 11)) | {25},
    "Outfit": {11, 14, 16, 17, 20, 23, 27},
    "Hair": {12},
    "Body": {13},
    "ThighStrap": {15},
    "Battery": {18},
    "Headphones": {19},
    "Skirt": {21, 22},
    "ClearCover": {24},
    "HairOverlay": {26},
}
assert set().union(*material_parts.values()) == set(range(28))
assert sum(len(slots) for slots in material_parts.values()) == 28

created = []
expected = {}
for part_name, slots in material_parts.items():
    face_indices = {poly.index for poly in source.data.polygons if poly.material_index in slots}
    assert face_indices, part_name
    expected[part_name] = len(face_indices)
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = f"Anby_{part_name}"
    obj.data.name = f"Anby_{part_name}_Mesh"
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    # Edit-mode deletion retains matching UVs, skin weights and face morphs.
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    for poly in obj.data.polygons:
        poly.select = poly.index not in face_indices
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.delete(type="FACE")
    bpy.ops.object.mode_set(mode="OBJECT")
    assert len(obj.data.polygons) == expected[part_name], part_name

    if part_name != "Face":
        obj.shape_key_clear()
    else:
        assert len(obj.data.shape_keys.key_blocks) == 57
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

assert sum(len(obj.data.polygons) for obj in created) == len(source.data.polygons)

material_names = {
    "颜": "Face", "口舌": "Mouth", "齿": "Teeth", "眉": "Eyebrows",
    "睫": "Eyelashes", "二重": "EyelidCrease", "白目": "EyeWhite",
    "目": "Eyes", "瞳": "Iris", "目光": "EyeHighlight",
    "目光2": "EyeHighlightSoft", "上衣": "Jacket", "髮": "Hair",
    "肌": "Skin", "裤": "Pants", "腿环": "ThighStrap",
    "手套": "Gloves", "袜": "Socks", "电池": "Battery",
    "耳机": "Headphones", "鞋": "Shoes", "裙": "Skirt",
    "裙饰": "SkirtTrim", "袖饰": "SleeveTrim", "透明罩": "ClearCover",
    "目影": "EyeShadow", "髮+": "HairOverlay", "新規材質1": "OutfitAccent",
    "武器": "Weapon",
}
for old_name, new_name in material_names.items():
    material = bpy.data.materials.get(old_name)
    assert material is not None, old_name
    material.name = "Anby_" + new_name

# The weapon PMX points to a missing diffuse name. The actual supplied file is
# tex/武器.tga; relink it before copying project-local textures.
weapon_image = bpy.data.images.get("Anbi_Weapon_D.tga")
assert weapon_image is not None
repaired_weapon_image = bpy.data.images.load(str(INPUT_DIR / "tex" / "武器.tga"), check_existing=False)
for material in bpy.data.materials:
    if material.use_nodes:
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image == weapon_image:
                node.image = repaired_weapon_image
bpy.data.images.remove(weapon_image)
repaired_weapon_image.name = "Anbi_Weapon_D.tga"

texture_names = {
    "体.tga": "Outfit.tga",
    "颜.tga": "Face.tga",
    "髮.tga": "Hair.tga",
    "Anbi_Weapon_D.tga": "Weapon.tga",
    "sh.png": "HairOverlay.png",
    "hair_s.bmp": "HairSpecular.bmp",
    "skin.bmp": "SkinToon.bmp",
    "toon_defo.bmp": "ToonDefault.bmp",
    "toon01.bmp": "Toon01.bmp",
}
copied_textures = []
for image in bpy.data.images:
    destination_name = texture_names.get(image.name)
    if not destination_name:
        continue
    source_path = Path(bpy.path.abspath(image.filepath))
    assert source_path.is_file(), source_path
    destination = TEXTURE_DIR / destination_name
    shutil.copy2(source_path, destination)
    image.filepath = str(destination)
    image.name = "Anby_" + destination.stem
    copied_textures.append(destination_name)

# FBX does not translate MMD shader nodes. Use one Principled surface per
# material and link the diffuse image where it is available.
for material in (bpy.data.materials["Anby_" + name] for name in material_names.values()):
    if not material.use_nodes:
        material.use_nodes = True
    diffuse = tuple(material.diffuse_color)
    image = next((
        node.image for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.name == "mmd_base_tex" and node.image
        and Path(bpy.path.abspath(node.image.filepath)).is_file()
    ), None)
    nodes = material.node_tree.nodes
    nodes.clear()
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (-100, 0)
    shader.inputs["Base Color"].default_value = (*diffuse[:3], 1)
    shader.inputs["Roughness"].default_value = 0.8
    shader.inputs["Alpha"].default_value = diffuse[3]
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (200, 0)
    links = material.node_tree.links
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    if image:
        texture = nodes.new("ShaderNodeTexImage")
        texture.name = "Unity_BaseColor"
        texture.image = image
        texture.location = (-400, 0)
        shader.inputs["Base Color"].default_value = (1, 1, 1, 1)
        links.new(texture.outputs["Color"], shader.inputs["Base Color"])
        if diffuse[3] >= 0.999:
            links.new(texture.outputs["Alpha"], shader.inputs["Alpha"])
    if diffuse[3] < 0.999 or material.name in {"Anby_ClearCover", "Anby_HairOverlay"}:
        try:
            material.surface_render_method = "BLENDED"
        except (AttributeError, TypeError):
            pass


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
    known = core_bones.get(base)
    if known:
        candidate = side + known
    else:
        english = ascii_name(rig.pose.bones[old_name].mmd_bone.name_e)
        candidate = (side + english if side else english) if english else f"Bone_{index:03d}" + ("_" + side if side else "")
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
    assert weighted_names <= set(rig.data.bones.keys()), obj.name

morph_labels = {m.name: m.name_e for m in root.mmd_root.vertex_morphs}
face = next(obj for obj in created if obj.name == "Anby_Face")
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

root.name = "Anby"
rig.name = "Anby_Rig"
rig.data.name = "Anby_Skeleton"
weapon_root.name = "Anby_WeaponRoot"
weapon_rig.name = "Anby_WeaponRig"
weapon_rig.data.name = "Anby_WeaponSkeleton"
weapon.name = "Anby_Weapon"
weapon.data.name = "Anby_Weapon_Mesh"
weapon_bone_map = {}
for bone in list(weapon_rig.data.bones):
    if bone.name == "刀":
        weapon_bone_map[bone.name] = "WeaponRoot"
for old_name, new_name in weapon_bone_map.items():
    weapon_rig.data.bones[old_name].name = new_name
    group = weapon.vertex_groups.get(old_name)
    if group:
        group.name = new_name
bpy.data.objects.remove(source, do_unlink=True)

(SOURCE_DIR / "NameMapping.json").write_text(json.dumps({
    "bones": bone_map,
    "shape_keys": shape_map,
    "weapon_bones": weapon_bone_map,
}, ensure_ascii=False, indent=2), encoding="utf-8")

# Physics helper objects stay in the editable blend file, hidden from view.
kept = set(created) | {root, rig, weapon_root, weapon_rig, weapon}
for obj in bpy.data.objects:
    if obj not in kept:
        obj.hide_set(True)
        obj.hide_render = True

bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
for obj in created:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), check_existing=False)

export_options = dict(
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
bpy.ops.export_scene.fbx(filepath=str(CHARACTER_FBX), **export_options)
bpy.ops.object.select_all(action="DESELECT")
weapon_rig.select_set(True)
weapon.select_set(True)
bpy.context.view_layer.objects.active = weapon_rig
bpy.ops.export_scene.fbx(filepath=str(WEAPON_FBX), **export_options)

report = {
    "character_fbx": str(CHARACTER_FBX),
    "weapon_fbx": str(WEAPON_FBX),
    "blend": str(BLEND_PATH),
    "meshes": {obj.name: {
        "polygons": len(obj.data.polygons),
        "vertices": len(obj.data.vertices),
        "shape_keys": len(obj.data.shape_keys.key_blocks) if obj.data.shape_keys else 0,
    } for obj in created},
    "bones": len(rig.data.bones),
    "weapon_bones": len(weapon_rig.data.bones),
    "textures": copied_textures,
    "missing_source_images": [image.name for image in bpy.data.images
                              if image.source == "FILE" and not Path(bpy.path.abspath(image.filepath)).is_file()],
}
(SOURCE_DIR / "ExportReport.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("EXPORT_JSON=" + json.dumps(report, ensure_ascii=False))
