"""Create a neutral Humanoid reference without changing the downloaded foot performance."""
import bpy
import hashlib
import json
from pathlib import Path
from mathutils import Matrix, Vector

if not bpy.app.background:
    raise RuntimeError('Run in Blender background mode to preserve interactive scenes')
project = Path(__file__).resolve().parents[2]
source = project / 'Assets/ThirdParty/CosindraAnimations/Motions/MotionSmith_pain_right_foot_take1.fbx'
assert hashlib.sha256(source.read_bytes()).hexdigest() == 'e49bfb9c5c4ac2a8bcc5945c07d19a881f91718d98ff390e198ab9d918b95e7d', 'Unexpected source take'
output = project / 'Assets/ThirdParty/CosindraAnimations/Derived'
output.mkdir(parents=True, exist_ok=True)
report_folder = project / 'Artifacts/FootReview/Prepared'
report_folder.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(source))
rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
scene = bpy.context.scene
start, end = map(int, rig.animation_data.action.frame_range)
world = rig.matrix_world.copy()
frames = []
for frame in range(start, end + 1):
    scene.frame_set(frame)
    frames.append({b.name: b.matrix.copy() for b in rig.pose.bones})
rig.animation_data_clear()
for bone in rig.pose.bones:
    bone.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()

# Rotates a chain around its joint while preserving the measured bone lengths.
def aim(name, child, direction):
    bone = rig.pose.bones[name]
    origin = world @ bone.head
    tip = world @ (rig.pose.bones[child].head if child else bone.tail)
    rotation = (tip - origin).rotation_difference(Vector(direction)).to_matrix().to_4x4()
    transform = Matrix.Translation(origin) @ rotation @ Matrix.Translation(-origin)
    bone.matrix = world.inverted() @ transform @ world @ bone.matrix
    bpy.context.view_layer.update()

for parent, child in [('pelvis', 'spine_01'), ('spine_01', 'spine_02'),
                      ('spine_02', 'spine_03'), ('spine_03', 'spine_04'),
                      ('spine_04', 'spine_05'), ('spine_05', 'neck_01'),
                      ('neck_01', 'neck_02'), ('neck_02', 'head'), ('head', None)]:
    aim(parent, child, (0, 0, 1))
for side, sign in [('l', 1), ('r', -1)]:
    for parent, child in [('clavicle', 'upperarm'), ('upperarm', 'lowerarm'), ('lowerarm', 'hand')]:
        aim(parent + '_' + side, child + '_' + side, (sign, 0, 0))
    for digit in ['index', 'middle', 'ring', 'pinky', 'thumb']:
        for index in [1, 2, 3]:
            name = f'{digit}_{index:02d}_{side}'
            child = f'{digit}_{index + 1:02d}_{side}' if index < 3 else None
            aim(name, child, (sign, -0.4 if digit == 'thumb' else 0, 0))
    aim('thigh_' + side, 'calf_' + side, (0, 0, -1))
    aim('calf_' + side, 'foot_' + side, (0, 0, -1))
    # The neutral ankle-to-toe slope follows the prepared Erika rig instead of flattening the foot bones.
    aim('foot_' + side, 'ball_' + side, (0, -0.773, -0.6344))
    aim('ball_' + side, None, (0, -1, 0))
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode='OBJECT')
rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
rig.animation_data_create()
rig.animation_data.action = bpy.data.actions.new('Right Foot Pain')
maximum_position_error = 0.0
maximum_rotation_error = 0.0
for index, desired in enumerate(frames):
    frame = start + index
    scene.frame_set(frame)
    for bone in rig.pose.bones:
        parent = bone.parent.name if bone.parent else None
        local_rest = rest[parent].inverted() @ rest[bone.name] if parent else rest[bone.name]
        local_pose = desired[parent].inverted() @ desired[bone.name] if parent else desired[bone.name]
        bone.rotation_mode = 'QUATERNION'
        bone.matrix_basis = local_rest.inverted() @ local_pose
        bone.keyframe_insert('location', frame=frame)
        bone.keyframe_insert('rotation_quaternion', frame=frame)
        bone.keyframe_insert('scale', frame=frame)
    bpy.context.view_layer.update()
    for bone in rig.pose.bones:
        expected = world @ desired[bone.name]
        actual = rig.matrix_world @ bone.matrix
        maximum_position_error = max(maximum_position_error, (actual.translation - expected.translation).length)
        maximum_rotation_error = max(maximum_rotation_error, actual.to_quaternion().rotation_difference(expected.to_quaternion()).angle)
assert maximum_position_error < 0.0001, f'Joint positions changed by {maximum_position_error} metres'
assert maximum_rotation_error < 0.002, f'Joint orientations changed by {maximum_rotation_error} radians'
# Frame zero is a reference only; Unity imports the original performance from frames 1–120.
scene.frame_set(0)
for bone in rig.pose.bones:
    bone.matrix_basis = Matrix.Identity(4)
    bone.keyframe_insert('location', frame=0)
    bone.keyframe_insert('rotation_quaternion', frame=0)
    bone.keyframe_insert('scale', frame=0)
bpy.context.view_layer.update()
scene.frame_start, scene.frame_end = 0, end
scene.render.fps = 30
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(output / 'RightFootPain_Humanoid.fbx'), use_selection=True,
    object_types={'ARMATURE'}, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0, use_armature_deform_only=False,
    axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS')
report = {'source': source.name, 'frames': len(frames), 'fps': 30,
          'maximumPositionErrorMetres': maximum_position_error,
          'maximumRotationErrorRadians': maximum_rotation_error,
          'bones': list(rest)}
(report_folder / 'preparation.json').write_text(json.dumps(report, indent=2))
print('FOOT_REFERENCE_PREPARED ' + json.dumps({k: v for k, v in report.items() if k != 'bones'}))
