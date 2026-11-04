"""
Hausi: the shipped models. A sash for each biome and a relic for each.

    blender --background --python tools/sash_build.py

The seven biome sashes are built in sash_biomes.py, each its own design. The Deep North sash is still
the baldric of sash_designs.py with a placeholder ice token. Relics are a slab with the biome's token,
and the tokens below are the ones the relics wear, which is how a player sees that a relic and a sash
belong together.

Prints triangles, colliders and open edges for each, and fails over 10,000 triangles or on any open
edge, because a surface with no thickness is a hole in the game that no render shows.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sash_designs import *  # noqa: E402,F403
from sash_designs import antler, baldric, body, ground, lights, open_edges, reference_cube, torso_points  # noqa: E402

from sash_biomes import SASHES, TINT_SETS  # noqa: E402

import bmesh  # noqa: E402
import bpy  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
RENDERS = os.path.join(ROOT, "renders")


def root_knot(at, mat):
    """Black Forest: three gnarled roots out of one knot."""
    x, y, z = at
    orb(0.03, (x, y, z + 0.05), mat, stretch=1.1)
    taper(0.018, 0.006, 0.12, (x - 0.03, y, z - 0.01), mat, sides=5, rot_y=-24.0, rot_x=180.0, tilt=4.0)
    taper(0.02, 0.006, 0.15, (x + 0.002, y, z - 0.03), mat, sides=5, rot_x=180.0, tilt=4.0)
    taper(0.018, 0.006, 0.11, (x + 0.03, y, z - 0.01), mat, sides=5, rot_y=26.0, rot_x=180.0, tilt=4.0)


def skull(at, mat):
    """Swamp: a small skull, a dome over a jaw."""
    x, y, z = at
    orb(0.042, (x, y, z + 0.03), mat, stretch=1.05)
    box((0.05, 0.04, 0.03), (x, y + 0.004, z - 0.012), mat, tilt=3.0)


def fangs(at, mat):
    """Mountain: three wolf fangs in a row."""
    x, y, z = at
    for offset, length in ((-0.036, 0.09), (0.0, 0.125), (0.036, 0.09)):
        taper(0.018, 0.004, length, (x + offset, y, z - length * 0.3), mat, sides=5, rot_x=180.0, tilt=3.0)


def tusk(at, mat):
    """Plains: one big curved tusk, thick at the root."""
    x, y, z = at
    taper(0.03, 0.022, 0.09, (x - 0.012, y, z + 0.015), mat, sides=7, rot_y=-16.0, tilt=2.0)
    taper(0.022, 0.012, 0.08, (x - 0.002, y, z - 0.055), mat, sides=7, rot_y=12.0, rot_x=180.0, tilt=2.0)
    taper(0.012, 0.003, 0.07, (x + 0.022, y, z - 0.115), mat, sides=7, rot_y=38.0, rot_x=180.0, tilt=2.0)


def plates(at, mat):
    """Mistlands: three overlapping carapace plates."""
    x, y, z = at
    for offset, turn, wide in ((0.05, -8.0, 0.06), (0.0, 7.0, 0.075), (-0.05, -5.0, 0.055)):
        box((wide, 0.016, 0.062), (x, y, z + offset), mat, rot_y=turn, rot_x=12.0, tilt=3.0)


def spikes(at, mat):
    """Ashlands: a jagged cluster of cooled spikes."""
    x, y, z = at
    for dx, dz, turn, length in ((-0.03, 0.0, -28.0, 0.09), (0.0, 0.01, 2.0, 0.14),
                                 (0.03, 0.0, 30.0, 0.095), (0.015, -0.04, 12.0, 0.07)):
        taper(0.022, 0.003, length, (x + dx, y, z + dz + length * 0.3), mat, sides=5, rot_y=turn, tilt=4.0)


def ice(at, mat):
    """Deep North: three ice crystals, each a six-sided prism with a point, the big one upright.

    Six sides on purpose, against the odd-sided rule: a crystal is meant to show a flat face, and
    that facet is what tells it from the Ashlands' round spikes at a glance. A placeholder until
    the real token is drawn.
    """
    x, y, z = at
    for dx, turn, wide, tall in ((0.0, 4.0, 0.026, 0.1), (-0.032, -30.0, 0.018, 0.065), (0.03, 28.0, 0.017, 0.06)):
        taper(wide, wide * 0.92, tall, (x + dx, y, z - 0.03), mat, sides=6, rot_y=turn, tilt=3.0)
        # Cones are placed by their centre, so the point sits half the prism plus most of half itself
        # along the prism's axis: that leaves the two overlapping, never touching end to end.
        axis = math.radians(turn)
        reach = tall * 0.5 + wide * 1.6 * 0.4
        taper(wide * 0.94, 0.003, wide * 1.6, (x + dx + math.sin(axis) * reach, y,
                                               z - 0.03 + math.cos(axis) * reach), mat, sides=6, rot_y=turn, tilt=3.0)


BIOMES = {
    "meadows": antler,
    "blackforest": root_knot,
    "swamp": skull,
    "mountain": fangs,
    "plains": tusk,
    "mistlands": plates,
    "ashlands": spikes,
    "deepnorth": ice,
}


def relic(token):
    """A worn stone slab on a loop of cord, the token standing proud of it. Small, and solid."""
    box((0.16, 0.05, 0.2), (0.0, 0.0, 0.1), "bone", tilt=3.0)
    disc(0.06, 0.02, (0.0, 0.03, 0.12), "cloth", sides=13)
    token((0.0, 0.05, 0.1), "bone")


def finish_model(name):
    """Joined and unwrapped. Bevel is skipped on the band, which it would turn into a spiked ring."""
    bpy.ops.object.select_all(action="SELECT")
    bpy.context.view_layer.objects.active = bpy.context.selected_objects[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.shade_flat()

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60.0), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    tris = len(bm.faces)
    bm.free()

    edges = open_edges(obj)
    print("HAUSI %s: %d triangles, 0 colliders, %d open edges" % (name, tris, edges))
    if tris > 10000 or edges != 0:
        raise RuntimeError(name + " is over the limit or not closed")

    return obj


def icon(obj, name, view=(0.35, 3.0, 0.25)):
    """Orthographic, its own lights, transparent, 128px, per section 8 of the suite's notes."""
    lo = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    centre = [(lo[i] + hi[i]) / 2 for i in range(3)]
    span = max(hi[i] - lo[i] for i in range(3))

    for mat in bpy.data.materials:
        mat.use_nodes = True
    tint()

    bpy.ops.object.camera_add(location=(centre[0] + span * view[0], centre[1] + span * view[1], centre[2] + span * view[2]))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = span * 1.12
    aim = bpy.data.objects.new("aim", None)
    bpy.context.collection.objects.link(aim)
    aim.location = centre
    track = cam.constraints.new(type="TRACK_TO")
    track.target = aim
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(1, 2, 2))
    bpy.context.active_object.data.energy = 2.8
    bpy.context.active_object.rotation_euler = (math.radians(-60.0), 0.0, math.radians(20.0))
    bpy.ops.object.light_add(type="SUN", location=(-1, 2, 0))
    bpy.context.active_object.data.energy = 1.0
    bpy.context.active_object.rotation_euler = (math.radians(-100.0), 0.0, math.radians(-30.0))

    world = bpy.data.worlds.new("icon_world")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.0

    bpy.context.scene.render.film_transparent = True
    render(os.path.join(ASSETS, name + ".png"), width=128, height=128, bloom=False)
    bpy.context.scene.render.film_transparent = False


def dropped(obj, name):
    """Dropped, the mesh lies face down at 0.45, so it is the back that shows."""
    lo = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    span = max(hi[0] - lo[0], hi[2] - lo[2])

    bpy.ops.object.camera_add(location=((lo[0] + hi[0]) / 2, lo[1] - 3.0, (lo[2] + hi[2]) / 2),
                              rotation=(math.radians(90.0), 0.0, 0.0))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = span * 1.15 / 0.45
    bpy.context.scene.camera = cam
    render(os.path.join(RENDERS, "%s_dropped.png" % name), width=160, height=160, bloom=False)
    bpy.data.objects.remove(cam)


def preview(obj, name):
    lights()
    tint()
    dropped(obj, name)

    body()
    ground()
    reference_cube((1.05, 0.0, 0.5))
    tint()

    for side, sign, at, aim, lens in (("front", 1, (0.0, 3.0, 1.7), (0.0, 0.0, 1.1), 42),
                                       ("back", -1, (0.0, -3.0, 1.7), (0.0, 0.0, 1.1), 42),
                                       ("detail", 1, (0.35, 1.7, 1.55), (-0.02, 0.0, 1.3), 50),
                                       ("hip", 1, (0.5, 1.7, 1.0), (0.12, 0.0, 0.92), 50)):
        for cam in [o for o in bpy.data.objects if o.type == "CAMERA" or o.name.startswith("aim")]:
            bpy.data.objects.remove(cam)
        camera(at, aim, lens=lens)
        render(os.path.join(RENDERS, "%s_%s.png" % (name, side)), width=620, height=640, bloom=False)


def main():
    os.makedirs(RENDERS, exist_ok=True)
    torso_points()
    base_tints = dict(TINTS)

    for biome, token in BIOMES.items():
        name = "sash_" + biome
        make = SASHES.get(biome, lambda token=token: baldric(token))
        TINTS.clear()
        TINTS.update(base_tints)
        TINTS.update(TINT_SETS.get(biome, {}))

        clear_scene()
        random.seed(SEED)
        make()
        obj = finish_model(name)
        export(obj, name, ASSETS)

        # Square onto the loop and a little to the chest. From the front a strap is its own edge,
        # a stick with a token on it, and an icon is a shape you tell from its neighbours.
        icon(obj, name, view=(2.4, 1.6, 1.3))

        clear_scene()
        random.seed(SEED)
        make()
        preview(finish_model(name), name)

        TINTS.clear()
        TINTS.update(base_tints)
        name = "relic_" + biome
        clear_scene()
        random.seed(SEED)
        relic(token)
        obj = finish_model(name)
        export(obj, name, ASSETS)
        icon(obj, name)


main()
