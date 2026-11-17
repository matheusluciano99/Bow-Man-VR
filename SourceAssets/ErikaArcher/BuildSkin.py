import bpy
import json
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree

if not bpy.app.background:
    raise RuntimeError('Run this builder in Blender background mode to preserve interactive scenes')
project = Path(__file__).resolve().parents[2]
output = project / 'Artifacts/CharacterRigging'
output.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(project / 'Assets/ThirdParty/ErikaArcher/Models/Erika Archer.fbx'))
rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
repaired = 0
maximum_distance = 0.0
for mesh in meshes:
    world = [mesh.matrix_world @ vertex.co for vertex in mesh.data.vertices]
    left = [i for i, p in enumerate(world) if p.x > 0.70 and 1.30 < p.z < 1.60]
    right = [i for i, p in enumerate(world) if p.x < -0.70 and 1.30 < p.z < 1.60]
    if not right:
        continue
    if not left:
        raise RuntimeError('Missing mirrored hand surface: ' + mesh.name)
    tree = KDTree(len(left))
    for index in left:
        tree.insert(world[index], index)
    tree.balance()
    for index in right:
        point = world[index]
        _, source, distance = tree.find(Vector((-point.x, point.y, point.z)))
        if distance > 0.015:
            raise RuntimeError('Right-hand surface differs from its left reference by more than 1.5 cm')
        maximum_distance = max(maximum_distance, distance)
        weights = [(mesh.vertex_groups[g.group].name.replace('Left', 'Right'), g.weight)
                   for g in mesh.data.vertices[source].groups if g.weight > 0]
        for group in list(mesh.data.vertices[index].groups):
            mesh.vertex_groups[group.group].remove([index])
        for name, weight in weights:
            if name not in rig.data.bones:
                raise RuntimeError('Unknown mirrored bone: ' + name)
            group = mesh.vertex_groups.get(name) or mesh.vertex_groups.new(name=name)
            group.add([index], weight, 'REPLACE')
        repaired += 1
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
if repaired == 0:
    raise RuntimeError('No right-hand skin vertices were corrected')
bpy.ops.object.select_all(action='DESELECT')
for obj in [rig] + meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=str(project / 'Assets/ThirdParty/ErikaArcher/Models/ErikaArcher_Rigged.fbx'),
    use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
    bake_anim=False, use_armature_deform_only=False, axis_forward='-Z', axis_up='Y',
    apply_scale_options='FBX_SCALE_UNITS', use_mesh_modifiers=True, use_tspace=True)
report = {'repairedRightHandVertices': repaired,
          'maximumMirroredSurfaceDistance': maximum_distance,
          'bones': len(rig.data.bones),
          'vertices': sum(len(mesh.data.vertices) for mesh in meshes)}
(output / 'ErikaSkinWeights.json').write_text(json.dumps(report, indent=2))
print('ERIKA_SKIN_EXPORT_DONE ' + json.dumps(report))
