"""Inspect material regions and shape-key coverage of the imported character."""

import bpy
import json
from collections import Counter
from pathlib import Path


source = bpy.data.objects["安比_mesh"]
mesh = source.data
regions = {}
for slot_index, slot in enumerate(source.material_slots):
    polygons = [poly for poly in mesh.polygons if poly.material_index == slot_index]
    vertices = sorted({index for poly in polygons for index in poly.vertices})
    if not vertices:
        continue
    coords = [source.matrix_world @ mesh.vertices[index].co for index in vertices]
    regions[slot.name] = {
        "slot": slot_index,
        "polygons": len(polygons),
        "vertices": len(vertices),
        "bounds": [[round(min(co[axis] for co in coords), 3) for axis in range(3)],
                   [round(max(co[axis] for co in coords), 3) for axis in range(3)]],
        "alpha": slot.material.diffuse_color[3] if slot.material else None,
        "image_nodes": [
            {"node": node.name, "image": node.image.name}
            for node in slot.material.node_tree.nodes
            if node.type == "TEX_IMAGE" and node.image
        ] if slot.material and slot.material.use_nodes else [],
        "shape_key_moved": Counter({
            key.name: sum((key.data[index].co - mesh.shape_keys.key_blocks[0].data[index].co).length > 1e-5
                          for index in vertices)
            for key in mesh.shape_keys.key_blocks[1:]
        }),
    }
    regions[slot.name]["shape_key_moved"] = {
        name: count for name, count in regions[slot.name]["shape_key_moved"].items() if count
    }

Path(__file__).with_name("materials_detail.json").write_text(
    json.dumps(regions, ensure_ascii=False, indent=2), encoding="utf-8")
print("MATERIAL_INSPECTION_COMPLETE")
