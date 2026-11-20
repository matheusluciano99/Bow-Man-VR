"""Prepare Poly Haven scenery with Blender 4.5. Pass the source and output folders after --."""
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector


# Imports each original glTF into an empty scene and returns its render meshes.
def load_model(directory, name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(directory / name / (name + "_1k.gltf")))
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


# Keeps the source UVs and material slots and reduces the stump to a scenery mesh.
def export_prop(directory, output, name):
    meshes = load_model(directory, name)
    for obj in meshes:
        if name == "tree_stump_01":
            modifier = obj.modifiers.new("Scenery detail", "DECIMATE")
            modifier.ratio = 0.15
    bpy.ops.export_scene.fbx(filepath=str(output / (name + ".fbx")), object_types={"MESH"},
                             apply_unit_scale=True, add_leaf_bones=False, bake_anim=False,
                             path_mode="STRIP", use_mesh_modifiers=True)


# Photographs two views of a full-resolution fir for the woodland's distant tree line.
def render_fir(directory, output):
    trees = load_model(directory, "fir_sapling_medium")
    tree = next(obj for obj in trees if "_a_LOD0" in obj.name)
    for other in trees:
        other.hide_render = other != tree
    corners = [tree.matrix_world @ Vector(c) for c in tree.bound_box]
    center = sum(corners, Vector()) / len(corners)
    height = max(c.z for c in corners) - min(c.z for c in corners)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 8
    scene.cycles.use_denoising = True
    world = bpy.data.worlds.new("Daylight")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.75, 0.83, 1, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.1
    scene.world = world
    scene.render.film_transparent = True
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.resolution_x = scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    bpy.ops.object.light_add(type="SUN")
    sun = bpy.context.object
    sun.rotation_euler = (math.radians(28), math.radians(-32), math.radians(22))
    sun.data.energy = 2.2
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = max(height, tree.dimensions.x, tree.dimensions.y) * 1.04
    scene.camera = camera
    for suffix, direction in [("front", Vector((0, -1, 0))), ("side", Vector((1, 0, 0)))]:
        camera.location = center + direction * (height * 2.5)
        camera.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(output / ("fir_sapling_medium_" + suffix + ".png"))
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    source, destination = map(Path, sys.argv[sys.argv.index("--") + 1:])
    destination.mkdir(parents=True, exist_ok=True)
    for prop in ("wine_barrel_01", "wooden_crate_01", "tree_stump_01"):
        export_prop(source, destination, prop)
    render_fir(source, destination)
