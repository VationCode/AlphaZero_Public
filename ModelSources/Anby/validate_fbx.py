"""Reimport both exported FBX files and verify their usable rigged contents."""

import bpy
import json
from pathlib import Path


ASSET_DIR = Path(r"D:\Portfolio\AlphaZero\Assets\00Arts\Character\Anby")


def inspect_fbx(path):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(path))
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    rigs = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    return {
        "meshes": {obj.name: {
            "polygons": len(obj.data.polygons),
            "vertices": len(obj.data.vertices),
            "shape_keys": len(obj.data.shape_keys.key_blocks) if obj.data.shape_keys else 0,
            "weighted_vertices": sum(bool(vertex.groups) for vertex in obj.data.vertices),
            "armature_modifiers": [mod.object.name for mod in obj.modifiers if mod.type == "ARMATURE" and mod.object],
            "materials": [slot.material.name for slot in obj.material_slots if slot.material],
        } for obj in meshes},
        "rigs": {obj.name: {
            "count": len(obj.data.bones),
            "skirt_bones": sorted(bone.name for bone in obj.data.bones if bone.name.startswith("Skirt_")),
            "core_bones": sorted(bone.name for bone in obj.data.bones
                                 if bone.name in {"Hips", "Spine", "Chest", "Neck", "Head",
                                                  "LeftUpperArm", "RightUpperArm", "LeftUpperLeg", "RightUpperLeg"}),
        } for obj in rigs},
        "other_objects": [obj.name for obj in bpy.data.objects if obj.type not in {"MESH", "ARMATURE"}],
    }


character = inspect_fbx(ASSET_DIR / "Anby_UnityReady.fbx")
assert len(character["meshes"]) == 10, character
assert len(character["rigs"]) == 1, character
assert sum(item["polygons"] for item in character["meshes"].values()) == 30678, character
assert character["meshes"]["Anby_Face"]["shape_keys"] == 57, character
assert len(character["rigs"]["Anby_Rig"]["skirt_bones"]) >= 64, character
assert len(character["rigs"]["Anby_Rig"]["core_bones"]) == 9, character
assert all(item["armature_modifiers"] and item["weighted_vertices"] > 0
           for item in character["meshes"].values()), character

weapon = inspect_fbx(ASSET_DIR / "Anby_Weapon.fbx")
assert len(weapon["meshes"]) == 1, weapon
assert len(weapon["rigs"]) == 1, weapon
assert weapon["meshes"]["Anby_Weapon"]["polygons"] == 2494, weapon
assert weapon["meshes"]["Anby_Weapon"]["armature_modifiers"], weapon
assert weapon["meshes"]["Anby_Weapon"]["weighted_vertices"] > 0, weapon

report = {"character": character, "weapon": weapon}
Path(__file__).with_name("ValidationReport.json").write_text(
    json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("VALIDATION_JSON=" + json.dumps(report, ensure_ascii=False))
