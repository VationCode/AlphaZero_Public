"""Reimport the Unity FBX in a fresh Blender process and report its contents."""

import bpy
import json


fbx_path = r"D:\Portfolio\AlphaZero\Assets\00Arts\Character\Soldier11\Soldier11_UnityReady.fbx"
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=fbx_path)

meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
rigs = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
report = {
    "meshes": {
        obj.name: {
            "vertices": len(obj.data.vertices),
            "polygons": len(obj.data.polygons),
            "shape_keys": len(obj.data.shape_keys.key_blocks) if obj.data.shape_keys else 0,
            "vertex_groups": len(obj.vertex_groups),
            "armature_modifiers": sum(mod.type == "ARMATURE" for mod in obj.modifiers),
            "materials": len(obj.data.materials),
        }
        for obj in meshes
    },
    "rigs": {obj.name: len(obj.data.bones) for obj in rigs},
    "materials": {
        material.name: [node.image.name for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image]
        if material.use_nodes else []
        for material in bpy.data.materials
    },
    "other_objects": [(obj.name, obj.type) for obj in bpy.data.objects if obj.type not in {"MESH", "ARMATURE"}],
}
print("VALIDATION_JSON=" + json.dumps(report, ensure_ascii=False))

assert len(meshes) == 7, report
assert {obj.name for obj in meshes} == {
    "Soldier11_Face", "Soldier11_Outfit", "Soldier11_Backpack",
    "Soldier11_Body", "Soldier11_Hair", "Soldier11_Weapon", "Soldier11_Goggles",
}, report
assert len(next(obj for obj in meshes if obj.name == "Soldier11_Goggles").data.polygons) == 192, report
assert len(rigs) == 1, report
assert sum(len(obj.data.polygons) for obj in meshes) == 34097, report
assert len(next(obj for obj in meshes if "Face" in obj.name).data.shape_keys.key_blocks) == 56, report
assert all(any(mod.type == "ARMATURE" for mod in obj.modifiers) for obj in meshes), report
