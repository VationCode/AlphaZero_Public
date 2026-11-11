"""Summarize the imported MMD model before Unity export."""

import bpy
import json
from collections import Counter
from pathlib import Path


source_dir = Path(__file__).resolve().parent
objects = []
for obj in bpy.data.objects:
    item = {
        "name": obj.name,
        "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "hidden": obj.hide_get(),
    }
    if obj.type == "MESH":
        item.update({
            "vertices": len(obj.data.vertices),
            "polygons": len(obj.data.polygons),
            "shape_keys": [key.name for key in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else [],
            "material_faces": dict(Counter(
                obj.material_slots[poly.material_index].material.name
                if obj.material_slots[poly.material_index].material else "<empty>"
                for poly in obj.data.polygons
            )),
            "vertex_groups": len(obj.vertex_groups),
            "armature_modifiers": [mod.object.name if mod.object else None for mod in obj.modifiers if mod.type == "ARMATURE"],
            "bounds": [
                [round(min((obj.matrix_world @ obj.data.vertices[i].co)[axis] for i in range(len(obj.data.vertices))), 3) for axis in range(3)],
                [round(max((obj.matrix_world @ obj.data.vertices[i].co)[axis] for i in range(len(obj.data.vertices))), 3) for axis in range(3)],
            ] if obj.data.vertices else None,
        })
    if obj.type == "ARMATURE":
        item.update({
            "bone_count": len(obj.data.bones),
            "bones": [{
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else None,
                "english": getattr(obj.pose.bones[bone.name].mmd_bone, "name_e", "")
                if hasattr(obj.pose.bones[bone.name], "mmd_bone") else "",
            } for bone in obj.data.bones],
        })
    objects.append(item)

report = {
    "blend": bpy.data.filepath,
    "objects": objects,
    "materials": [{
        "name": material.name,
        "images": [
            {"name": node.image.name, "filepath": bpy.path.abspath(node.image.filepath)}
            for node in material.node_tree.nodes
            if node.type == "TEX_IMAGE" and node.image
        ] if material.use_nodes else [],
    } for material in bpy.data.materials],
    "images": [{"name": image.name, "filepath": bpy.path.abspath(image.filepath), "packed": bool(image.packed_file)} for image in bpy.data.images],
}
(source_dir / "inspection.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("INSPECTION_COMPLETE")
