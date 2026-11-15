import bpy
import json
import math
from pathlib import Path
from mathutils import Matrix, Vector, Quaternion

if not bpy.app.background:
    raise RuntimeError('Run this builder in Blender background mode to preserve interactive scenes')
project=Path(__file__).resolve().parents[2]
authoring=project/'SourceAssets/EnglishArcher'
authoring.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
output=project/'Artifacts/CharacterRigging'
output.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(project/'Assets/ThirdParty/EnglishArcher/Models/EnglishArcher.fbx'))
props={'defaultMaterial.'+str(n).zfill(3) for n in range(3,14)}
meshes=[]
for obj in list(bpy.context.scene.objects):
    if obj.type!='MESH':continue
    if obj.name in props:bpy.data.objects.remove(obj,do_unlink=True)
    else:meshes.append(obj)
low=min((obj.matrix_world@v.co).z for obj in meshes for v in obj.data.vertices)
high=max((obj.matrix_world@v.co).z for obj in meshes for v in obj.data.vertices)
scale=1.75/(high-low)
normalization=Matrix.Translation((0,0,-low*scale))@Matrix.Scale(scale,4)
for obj in meshes:
    world=normalization@obj.matrix_world
    obj.parent=None
    obj.matrix_world=world
bpy.ops.object.select_all(action='DESELECT')
for obj in meshes:obj.select_set(True)
bpy.context.view_layer.objects.active=meshes[0]
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
before=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=str(project/'Assets/ThirdParty/ErikaArcher/Models/Erika Archer.fbx'))
imported=set(bpy.data.objects)-before
rig=next(obj for obj in imported if obj.type=='ARMATURE')
for obj in imported:
    if obj!=rig:bpy.data.objects.remove(obj,do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
rig.name='EnglishArcherRig'
rig.data.name='EnglishArcherSkeleton'
reference={bone.name:{'head':bone.head_local.copy(),'tail':bone.tail_local.copy(),'rotation':bone.matrix_local.to_quaternion()} for bone in rig.data.bones}
ratio=scale*100

# Converts source-metre anatomical landmarks into the normalized character space.
def point(v):
    result=Vector(v)*ratio
    result.z-=low*scale
    return result

landmarks={
'Hips':((0,0.035,0.91),(0,0.035,1.02)),
'Spine':((0,0.035,1.02),(0,0.025,1.16)),
'Spine1':((0,0.025,1.16),(0,0.015,1.30)),
'Spine2':((0,0.015,1.30),(0,0.005,1.42)),
'Neck':((0,0.005,1.445),(0,-0.015,1.51)),
'Head':((0,-0.015,1.51),(0,-0.015,1.66)),
'HeadTop_End':((0,-0.015,1.66),(0,-0.015,1.705)),
}
hands={'Left': {'wrist': [0.5314908027648926, 0.0030834164936095476, 1.0625648498535156],
          'tips': {'Thumb': [0.5952197909355164, -0.11664921790361404, 0.9563062191009521],
                   'Index': [0.6651734113693237, -0.12148753553628922, 0.9244116544723511],
                   'Middle': [0.7064039707183838, -0.08596928417682648, 0.9097583293914795],
                   'Ring': [0.70158451795578, -0.022844895720481873, 0.9122681021690369],
                   'Pinky': [0.6835705637931824, 0.018146932125091553, 0.9277954697608948]}},
 'Right': {'wrist': [-0.532315731048584, 0.0043225581757724285, 1.0606132745742798],
           'tips': {'Thumb': [-0.5961615443229675, -0.11584282666444778, 0.9567552208900452],
                    'Index': [-0.6669262647628784, -0.12108039855957031, 0.921965479850769],
                    'Middle': [-0.7049340009689331, -0.08388432115316391, 0.9074293375015259],
                    'Ring': [-0.7013921737670898, -0.022347593680024147, 0.9104688763618469],
                    'Pinky': [-0.6815925240516663, 0.016656601801514626, 0.9281136989593506]}}}
for side,sign in [('Left',1),('Right',-1)]:
    landmarks[side+'Shoulder']=((sign*0.07,0.005,1.37),(sign*0.185,0.005,1.36))
    landmarks[side+'Arm']=((sign*0.185,0.005,1.36),(sign*0.385,-0.005,1.19))
    landmarks[side+'ForeArm']=((sign*0.385,-0.005,1.19),hands[side]['wrist'])
    landmarks[side+'UpLeg']=((sign*0.11,0.03,0.91),(sign*0.105,-0.02,0.48))
    landmarks[side+'Leg']=((sign*0.105,-0.02,0.48),(sign*0.10,0.04,0.09))
    landmarks[side+'Foot']=((sign*0.10,0.04,0.09),(sign*0.10,-0.09,0.035))
    landmarks[side+'ToeBase']=((sign*0.10,-0.09,0.035),(sign*0.10,-0.17,0.035))
    landmarks[side+'Toe_End']=((sign*0.10,-0.17,0.035),(sign*0.10,-0.21,0.035))

bpy.ops.object.mode_set(mode='EDIT')
for bone in rig.data.edit_bones:bone.use_connect=False
for bone in rig.data.edit_bones:
    name=bone.name.removeprefix('mixamorig:')
    if name in landmarks:
        head,tail=landmarks[name]
        bone.head=point(head)
        bone.tail=point(tail)
    elif 'Hand' in name:
        side='Left' if name.startswith('Left') else 'Right'
        wrist=Vector(hands[side]['wrist'])
        if name==side+'Hand':
            base=sum((Vector(tip) for digit,tip in hands[side]['tips'].items() if digit!='Thumb'),Vector())/4
            bone.head=point(wrist)
            bone.tail=point(wrist.lerp(base,0.5))
        else:
            digit=next(digit for digit in hands[side]['tips'] if digit in name)
            tip=Vector(hands[side]['tips'][digit])
            fractions=[0.25,0.55,0.80,0.98,1.10] if digit=='Thumb' else [0.50,0.72,0.88,0.98,1.10]
            segment=int(name[-1])-1
            bone.head=point(wrist.lerp(tip,fractions[segment]))
            bone.tail=point(wrist.lerp(tip,fractions[segment+1]))
    elif name.endswith('Eye'):
        mesh=next(obj for obj in meshes if obj.name==('defaultMaterial.040' if name=='LeftEye' else 'defaultMaterial.041'))
        bone.head=sum((v.co for v in mesh.data.vertices),Vector())/len(mesh.data.vertices)
        bone.tail=bone.head+Vector((0,-0.02,0))
    else:raise RuntimeError('Missing fitted landmark: '+name)
    bone.use_deform=not(name.endswith('_End') or name.endswith('4') or name.endswith('Eye'))
bpy.ops.object.mode_set(mode='OBJECT')

rigid={}
for obj in meshes:
    if obj.name in {'defaultMaterial.039','defaultMaterial.040','defaultMaterial.041'}:
        rigid[obj.name]='Head' if obj.name.endswith('039') else ('LeftEye' if obj.name.endswith('040') else 'RightEye')
    elif obj.data.materials[0].name=='ammo':
        center=sum((v.co for v in obj.data.vertices),Vector())/len(obj.data.vertices)
        rigid[obj.name]='Spine' if center.z>1.06*ratio else 'Hips'
body_parts=[obj for obj in meshes if obj.name not in rigid]
all_deform={bone.name:bone.use_deform for bone in rig.data.bones}
allowed={
    'defaultMaterial.037':{'Hips','Spine','Spine1','Spine2','LeftShoulder','RightShoulder','LeftArm','RightArm','LeftForeArm','RightForeArm','LeftUpLeg','RightUpLeg'},
    'defaultMaterial.038':{'Hips','LeftUpLeg','RightUpLeg','LeftLeg','RightLeg','LeftFoot','RightFoot','LeftToeBase','RightToeBase'},
    'defaultMaterial.034':{'Spine1','Spine2','Neck','Head','LeftShoulder','RightShoulder'},
    'defaultMaterial.042':{'Neck','Head'},
}
for name,side in [('defaultMaterial.035','Left'),('defaultMaterial.036','Right')]:
    allowed[name]={bone.name.removeprefix('mixamorig:') for bone in rig.data.bones if bone.name.startswith('mixamorig:'+side+'Hand') and all_deform[bone.name]}
    allowed[name].add(side+'ForeArm')
from mathutils.kdtree import KDTree
repair_count=0
repair_distance=0.0
# Restrict each skin section to its anatomical bones to prevent distant finger or torso influences.
for part in body_parts:
    for bone in rig.data.bones:bone.use_deform=bone.name.removeprefix('mixamorig:') in allowed[part.name]
    bpy.ops.object.select_all(action='DESELECT')
    part.select_set(True);rig.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    unweighted=[v.index for v in part.data.vertices if not any(g.weight>0 for g in v.groups)]
    weighted=[v for v in part.data.vertices if any(g.weight>0 for g in v.groups)]
    if not weighted:raise RuntimeError('Bone heat could not bind '+part.name)
    tree=KDTree(len(weighted))
    for i,v in enumerate(weighted):tree.insert(v.co,i)
    tree.balance()
    # Coincident UV-seam details inherit the already weighted surface at the same position.
    for index in unweighted:
        _,near,distance=tree.find(part.data.vertices[index].co)
        if distance>0.02:raise RuntimeError('Unweighted island is too far from its skin section: '+str(distance))
        repair_count+=1;repair_distance=max(repair_distance,distance)
        for group in weighted[near].groups:
            if group.weight>0:part.vertex_groups[group.group].add([index],group.weight,'REPLACE')
for bone in rig.data.bones:bone.use_deform=all_deform[bone.name]
# A planted boot must not inherit motion from the opposite leg.
pants=next(part for part in body_parts if part.name=='defaultMaterial.038')
leg_side_pruned=0
for vertex in pants.data.vertices:
    if vertex.co.z>0.8*ratio or abs(vertex.co.x)<0.03*ratio:continue
    opposite='Right' if vertex.co.x>0 else 'Left'
    for group in list(vertex.groups):
        name=pants.vertex_groups[group.group].name
        if name.startswith('mixamorig:'+opposite):
            if group.weight>0.00001:leg_side_pruned+=1
            pants.vertex_groups[group.group].remove([vertex.index])
    if not any(g.weight>0 for g in vertex.groups):raise RuntimeError('Leg-side filtering removed every weight')

# Coincident UV-seam vertices need identical weights so animation cannot open cracks.
seam_vertices=0
for part in body_parts:
    buckets={}
    for vertex in part.data.vertices:buckets.setdefault(tuple(round(v,6) for v in vertex.co),[]).append(vertex)
    for vertices in buckets.values():
        if len(vertices)<2:continue
        seam_vertices+=len(vertices)
        sums={}
        for vertex in vertices:
            for group in vertex.groups:
                if group.weight>0:sums[group.group]=sums.get(group.group,0.0)+group.weight
        indices=[vertex.index for vertex in vertices]
        for index,total in sums.items():part.vertex_groups[index].add(indices,total/len(vertices),'REPLACE')
for name,bone in rigid.items():
    obj=bpy.data.objects[name]
    group=obj.vertex_groups.new(name='mixamorig:'+bone)
    group.add(list(range(len(obj.data.vertices))),1.0,'REPLACE')
    obj.parent=rig
    modifier=obj.modifiers.new('Armature','ARMATURE')
    modifier.object=rig
for side in ['Left','Right']:rig.data.bones['mixamorig:'+side+'Eye'].use_deform=True
bpy.ops.object.select_all(action='DESELECT')
for part in body_parts:part.select_set(True)
body=body_parts[0]
for name in rigid:bpy.data.objects[name].select_set(True)
bpy.context.view_layer.objects.active=body
bpy.ops.object.join()
body.name='EnglishArcherBody'
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL',limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)

# Poses the fitted skeleton into its reference T-stance before baking the bind mesh.
for name in reference:
    bone=rig.pose.bones[name]
    head=bone.head.copy()
    bone.matrix=Matrix.Translation(head)@reference[name]['rotation'].to_matrix().to_4x4()
    bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
bpy.context.view_layer.objects.active=body
for mod in list(body.modifiers):
    if mod.type=='ARMATURE':bpy.ops.object.modifier_apply(modifier=mod.name)
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode='OBJECT')
modifier=body.modifiers.new('Armature','ARMATURE')
modifier.object=rig
floor=min(v.co.z for v in body.data.vertices)
final_scale=1.75/(max(v.co.z for v in body.data.vertices)-floor)
for vertex in body.data.vertices:
    vertex.co.z-=floor
    vertex.co*=final_scale
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for bone in rig.data.edit_bones:
    bone.head.z-=floor
    bone.tail.z-=floor
    bone.head*=final_scale
    bone.tail*=final_scale
bpy.ops.object.mode_set(mode='OBJECT')
for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
# Level each boot sole in the neutral bind mesh with a smooth transition into the ankle.
sole_offsets={}
for side,sign in [('Left',1),('Right',-1)]:
    sole=min(v.co.z for v in body.data.vertices if sign*v.co.x>0.03 and v.co.z<0.17)
    if sole>0.03:raise RuntimeError('Boot sole needs an artist correction larger than 3 cm')
    sole_offsets[side]=sole
    for vertex in body.data.vertices:
        if sign*vertex.co.x<=0.03 or vertex.co.z>=0.30:continue
        blend=max(0.0,min(1.0,(vertex.co.z-0.15)/0.15))
        blend=blend*blend*(3-2*blend)
        vertex.co.z-=sole*(1-blend)
body.data.update()
weights=[sum(g.weight for g in v.groups) for v in body.data.vertices]
assert min(weights)>0.999 and max(weights)<1.001,(min(weights),max(weights))
assert max(len([g for g in v.groups if g.weight>0]) for v in body.data.vertices)<=4
for image in bpy.data.images:
    candidate=project/'Assets/ThirdParty/EnglishArcher/Textures'/Path(image.filepath).name
    if candidate.exists():image.filepath=str(candidate)
for obj in list(bpy.context.scene.objects):
    if obj not in {body,rig}:bpy.data.objects.remove(obj,do_unlink=True)
report={'equalizedSeamVertices':seam_vertices,'neutralSoleOffsets':sole_offsets,'removedOppositeLegInfluences':leg_side_pruned,'repairedClothVertices':repair_count,'maximumSurfaceTransferDistance':repair_distance,'bones':len(rig.data.bones),'deformBones':sum(b.use_deform for b in rig.data.bones),'vertices':len(body.data.vertices),'triangles':sum(len(p.vertices)-2 for p in body.data.polygons),'weightedVertices':len(weights),'minWeightSum':min(weights),'maxWeightSum':max(weights),'height':max(v.co.z for v in body.data.vertices),'floor':min(v.co.z for v in body.data.vertices),'materials':[m.name for m in body.data.materials]}
(output/'EnglishRigWeights.json').write_text(json.dumps(report,indent=2))
bpy.data.orphans_purge(do_local_ids=True,do_linked_ids=True,do_recursive=True)
bpy.ops.export_scene.fbx(filepath=str(project/'Assets/ThirdParty/EnglishArcher/Models/EnglishArcher_Rigged.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,use_armature_deform_only=True,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True,use_tspace=True)
print('ENGLISH_RIG_EXPORT_DONE '+json.dumps(report))

# Rotates a pose bone about an armature-space axis while preserving its joint pivot.
def rotate_world(name,axis,degrees):
    bone=rig.pose.bones['mixamorig:'+name]
    head=bone.head.copy()
    matrix=bone.matrix.copy()
    matrix.translation=Vector()
    bone.matrix=Matrix.Translation(head)@Quaternion(Vector(axis),math.radians(degrees)).to_matrix().to_4x4()@matrix
    bpy.context.view_layer.update()

# Bakes a neutral/gesture/step check with fixed hip position and an untouched support leg.
bpy.context.scene.render.fps=30
bpy.context.scene.frame_start=1
bpy.context.scene.frame_end=361
rig.animation_data_create()
action=bpy.data.actions.new('RigMobilityCheck')
rig.animation_data.action=action
for bone in rig.pose.bones:bone.rotation_mode='QUATERNION'
for second in range(13):
    frame=1+second*30
    bpy.context.scene.frame_set(frame)
    for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    rotate_world('LeftArm',(0,1,0),60)
    rotate_world('RightArm',(0,1,0),-60)
    if second==2:
        rotate_world('Spine',(1,0,0),5)
        rotate_world('Neck',(1,0,0),5)
        rotate_world('Head',(0,0,1),12)
    if second==4:
        rotate_world('LeftShoulder',(0,1,0),-6)
        rotate_world('RightShoulder',(0,1,0),6)
        rotate_world('LeftArm',(0,1,0),-20)
        rotate_world('RightArm',(0,1,0),20)
        rotate_world('LeftForeArm',(0,0,1),-25)
        rotate_world('RightForeArm',(0,0,1),25)
    if second==6:
        for side,sign in [('Left',1),('Right',-1)]:
            for digit in ['Thumb','Index','Middle','Ring','Pinky']:
                for segment in range(1,4):rotate_world(side+'Hand'+digit+str(segment),(0,1,0),sign*(20 if digit=='Thumb' else 30))
    if second in [8,10]:
        side='Left' if second==8 else 'Right'
        rotate_world(side+'UpLeg',(1,0,0),-30)
        rotate_world(side+'Leg',(1,0,0),55)
        rotate_world(side+'Foot',(1,0,0),-25)
    for bone in rig.pose.bones:
        bone.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=bone.name)
        bone.keyframe_insert(data_path='location',frame=frame,group=bone.name)
for fcurve in action.fcurves:
    for key in fcurve.keyframe_points:
        key.handle_left_type='AUTO_CLAMPED';key.handle_right_type='AUTO_CLAMPED'
bpy.context.scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(project/'Assets/_Game/Animations/RigMobilityCheck.fbx'),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1.0,bake_anim_simplify_factor=0.0,use_armature_deform_only=True,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS')
for image in bpy.data.images:
    absolute=Path(bpy.path.abspath(image.filepath))
    if absolute.exists():image.filepath=bpy.path.relpath(str(absolute),start=str(authoring))
bpy.ops.wm.save_as_mainfile(filepath=str(authoring/'EnglishArcherRig.blend'),compress=True,relative_remap=False)
print('RIG_MOBILITY_BAKE_DONE')
