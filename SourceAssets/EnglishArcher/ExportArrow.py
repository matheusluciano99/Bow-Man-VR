from pathlib import Path
import bpy

# Extracts the textured loose arrow prop from the English Archer display model into the duel's tip-origin space.
def export_arrow(project):
    if not bpy.app.background:
        raise RuntimeError("Use Blender in background mode to preserve the open scene.")
    models = project / "Assets/ThirdParty/EnglishArcher/Models"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(models / "EnglishArcher.fbx"))
    shaft = bpy.data.objects["defaultMaterial.009"]
    vertices = [shaft.matrix_world @ vertex.co for vertex in shaft.data.vertices]
    tip = max(vertices, key=lambda p: p.z)
    scale = 0.69 / (tip.z - min(p.z for p in vertices))
    lines = ["# English Archer by count_zero (CC BY 4.0). See ../ATTRIBUTION.txt.", "o ShaftAndFeathers"]
    vertex_count = uv_count = 0
    for number in range(3, 10):
        obj = bpy.data.objects[f"defaultMaterial.{number:03d}"]
        mesh = obj.data
        for vertex in mesh.vertices:
            p = ((obj.matrix_world @ vertex.co) - tip) * scale
            lines.append(f"v {p.x:.7f} {p.y:.7f} {p.z:.7f}")
        for uv in mesh.uv_layers.active.data:
            lines.append(f"vt {uv.uv.x:.7f} {uv.uv.y:.7f}")
        mesh.calc_loop_triangles()
        for tri in mesh.loop_triangles:
            face = [f"{vertex_count + mesh.loops[i].vertex_index + 1}/{uv_count + i + 1}" for i in tri.loops]
            lines.append("f " + " ".join(face))
        vertex_count += len(mesh.vertices)
        uv_count += len(mesh.loops)
    (models / "ArrowShaftAndFeathers.obj").write_text("\n".join(lines) + "\n", encoding="utf-8")

export_arrow(Path(__file__).resolve().parents[2])
