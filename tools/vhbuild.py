"""
Shared model-building helpers for the design scripts in this repo.

Third copy of these was one too many. Import from a design script with:

    import os, sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from vhbuild import *

Nothing here paints anything for real. Material names are group names only; at
runtime each group is skinned with a material lifted off a vanilla prefab, so the
shapes are ours and the surfaces are the game's. The TINTS below exist so a render
says something useful about form, and nothing more.
"""

import bpy
import math
import os
import random

from mathutils import Euler, Vector

# Jitter is seeded, not arbitrary. A rebuild has to produce the same mesh or the
# .obj churns in git and no render can be compared with the one before it.
SEED = 20260814

# How far anything may wander. Small on purpose: this is meant to remove the machined
# look, not to make the model drunk. Carpentry should pass a lower number - a bench
# is built by someone with a square, even if the timber is not perfect.
TILT = 4.0
SHIFT = 0.008

COLLIDERS = []

TINTS = {
    "bark":  (0.24, 0.17, 0.11, 1.0),
    "moss":  (0.20, 0.28, 0.14, 1.0),
    "wood":  (0.30, 0.19, 0.10, 1.0),
    "iron":  (0.19, 0.19, 0.21, 1.0),
    "stone": (0.44, 0.43, 0.40, 1.0),
    "seed":  (0.34, 0.28, 0.16, 1.0),

    # Warm gold. Mistlands owns teal and pale green-white, so the family resemblance
    # to a wisp is kept and the colour is where it parts company.
    "core":  (1.00, 0.74, 0.30, 1.0),
}

EMISSIVE = ("core",)


# --------------------------------------------------------------------------- scene

def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects,
                  bpy.data.lights, bpy.data.cameras):
        for item in list(block):
            if item.users == 0:
                block.remove(item)
    del COLLIDERS[:]
    random.seed(SEED)


def material(name):
    mat = bpy.data.materials.get(name)
    return mat if mat else bpy.data.materials.new(name)


def collide(centre, size):
    COLLIDERS.append((centre, size))


# --------------------------------------------------------------------------- parts

def wobble(tilt=TILT, shift=SHIFT):
    """
    A few degrees and a few millimetres of nothing-in-particular.

    Every part being exactly axis-aligned is most of why the first models read as
    machined. Nothing in a forest is square to anything else, and the eye picks that
    up long before it can say why.
    """
    return ((random.uniform(-tilt, tilt), random.uniform(-tilt, tilt),
             random.uniform(-tilt, tilt)),
            (random.uniform(-shift, shift), random.uniform(-shift, shift),
             random.uniform(-shift, shift)))


def box(size, location, mat, rot_x=0.0, rot_y=0.0, rot_z=0.0, tilt=TILT, hit=False):
    (jx, jy, jz), (dx, dy, dz) = wobble(tilt)

    bpy.ops.mesh.primitive_cube_add(
        size=1.0, location=(location[0] + dx, location[1] + dy, location[2] + dz))
    obj = bpy.context.active_object
    obj.scale = (size[0], size[1], size[2])
    obj.rotation_euler = (math.radians(rot_x + jx), math.radians(rot_y + jy),
                          math.radians(rot_z + jz))
    obj.data.materials.append(material(mat))
    if hit:
        collide(location, size)
    return obj


def taper(bottom, top, height, location, mat, sides=7, rot_x=0.0, rot_y=0.0,
          tilt=TILT, spin=True):
    """
    Odd side counts by default. An even-sided cylinder presents a flat face square to
    the camera and reads as a box with the corners knocked off; an odd one always
    shows an edge, which is what makes it read as round.
    """
    (jx, jy, jz), (dx, dy, dz) = wobble(tilt)

    # The random spin only applies to upright cylinders. Blender composes euler XYZ as
    # Rz @ Ry @ Rx, so the spin lands *after* the part has been stood up - on a disc
    # laid over by rot_x=90 it does not turn the disc in its own plane, it swings the
    # whole face to point in a random horizontal direction. Every chest disc came out
    # edge-on to the camera before this.
    upright = abs(rot_x) < 0.001 and abs(rot_y) < 0.001
    turn = random.uniform(0.0, 360.0) if (spin and upright) else 0.0

    bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=bottom, radius2=top, depth=height,
        location=(location[0] + dx, location[1] + dy, location[2] + dz),
        rotation=(0.0, 0.0, math.radians(turn)))
    obj = bpy.context.active_object
    obj.rotation_euler = (math.radians(rot_x + jx), math.radians(rot_y + jy),
                          obj.rotation_euler.z + math.radians(jz))
    obj.data.materials.append(material(mat))
    return obj


def disc(radius, thickness, location, mat, sides=13, rot_x=90.0, tilt=1.0):
    """A flat round plate facing along +y by default, for anything set into a chest."""
    return taper(radius, radius, thickness, location, mat, sides=sides,
                 rot_x=rot_x, tilt=tilt)


def orb(radius, location, mat, subdivisions=2, stretch=1.0, tilt=1.0):
    """
    A faceted low-poly sphere.

    Cylinders were standing in for these and it does not work: a cylinder has flat
    caps and straight sides, so however brightly it glows it reads as a can or a
    lantern. Roundness is what makes a light read as a living thing rather than as
    an object someone made.

    An icosphere rather than a UV sphere - even triangles all over, no pole pinching,
    and at two subdivisions it is 80 faces and visibly faceted, which suits the game.
    """
    (jx, jy, jz), (dx, dy, dz) = wobble(tilt)

    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=subdivisions, radius=radius,
        location=(location[0] + dx, location[1] + dy, location[2] + dz))

    obj = bpy.context.active_object
    obj.scale = (1.0, 1.0, stretch)
    obj.rotation_euler = (math.radians(jx), math.radians(jy), math.radians(jz))
    obj.data.materials.append(material(mat))
    return obj


def ring(radius, thickness, location, mat, major=18, minor=7, rot_x=90.0, tilt=1.0):
    """
    A real torus, facing along +y by default.

    Faked first as a bright disc with a dark disc in front of it, which does not
    survive bloom: the halation from the bright ring washes straight over the dark
    centre and closes the hole back up. A ring has to actually have nothing in the
    middle of it.
    """
    (jx, jy, jz), (dx, dy, dz) = wobble(tilt)

    bpy.ops.mesh.primitive_torus_add(
        location=(location[0] + dx, location[1] + dy, location[2] + dz),
        major_radius=radius, minor_radius=thickness,
        major_segments=major, minor_segments=minor,
        rotation=(math.radians(rot_x + jx), math.radians(jy), math.radians(jz)))

    obj = bpy.context.active_object
    obj.data.materials.append(material(mat))
    return obj


def shell(bottom, top, height, location, mat, sides=9, rot_x=0.0, rot_y=0.0):
    """
    An open frustum: both caps deleted, so whatever is inside can be seen.

    Face select mode, and set before anything is selected. In vertex mode every vertex
    of a frustum belongs to one cap or the other, so selecting both caps selects the
    whole mesh and the shell vanishes.
    """
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=bottom, radius2=top,
                                    depth=height, location=location)
    obj = bpy.context.active_object
    # rot_y as well as rot_x, because rot_x lays a tube along y and there is no way to
    # lay one along x without it. The cap selection below reads local normals and runs
    # after this, so object rotation does not disturb which faces are the caps.
    obj.rotation_euler = (math.radians(rot_x), math.radians(rot_y), 0.0)

    mesh = obj.data
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_mode(type="FACE")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")

    for face in mesh.polygons:
        if abs(face.normal.z) > 0.9:
            face.select = True

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.delete(type="FACE")
    bpy.ops.object.mode_set(mode="OBJECT")

    obj.data.materials.append(material(mat))
    return obj


def limb(base, length, segments, thick, taper_to, pitch, yaw, curve, mat, sides=5):
    """
    A bent, tapering branch built from overlapping segments.

    One rotated box is a stick, and a creature made of sticks is a scarecrow. Segments
    overlap by 8% of their length, because a butt joint between two cones at different
    angles leaves a visible wedge of daylight on the outside of a bend.

    Returns the tip, so a fork can be grown from it.
    """
    pos = Vector(base)
    step = length / float(segments)
    angle = pitch

    for i in range(segments):
        t0 = i / float(segments)
        t1 = (i + 1) / float(segments)
        r0 = thick + (taper_to - thick) * t0
        r1 = thick + (taper_to - thick) * t1

        euler = Euler((math.radians(angle + random.uniform(-2.0, 2.0)), 0.0,
                       math.radians(yaw + random.uniform(-3.0, 3.0))), "XYZ")
        heading = Vector((0.0, 0.0, 1.0))
        heading.rotate(euler)

        bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r0, radius2=r1,
                                        depth=step, location=pos + heading * (step / 2.0))
        obj = bpy.context.active_object
        obj.rotation_euler = euler
        obj.data.materials.append(material(mat))

        pos = pos + heading * step * 0.92
        angle += curve

    return pos


def roots(at, spread, count=5, length=0.34, thick=0.075):
    """
    Root feet instead of a foot.

    A block on the end of a leg is a boot, and a boot is the single most human thing a
    silhouette can have.
    """
    x, y, z = at
    for i in range(count):
        yaw = 360.0 / count * i + random.uniform(-14.0, 14.0)
        rad = math.radians(yaw)
        limb((x + math.cos(rad) * spread * 0.5, y + math.sin(rad) * spread * 0.5, z),
             length, 3, thick, thick * 0.35, 118.0, yaw, 16.0, "bark", sides=5)


# --------------------------------------------------------------------------- output

def bevel_all(width=0.014, segments=2):
    """
    A chamfer on every edge, applied per object before anything is joined.

    The largest single change and the cheapest. Valheim's props are low-poly but they
    are not raw primitives: every edge carries a small chamfer, giving it a bright
    line where it turns away from the sun. Without one, a box is four flat greys
    meeting at nothing and reads as an untextured primitive however good the
    proportions are.

    Per object rather than after the join, because beveling a joined mesh works on the
    intersections between parts too - and parts here overlap deliberately, so that
    produces spikes wherever two limbs cross.
    """
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH":
            continue

        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new("chamfer", "BEVEL")
        modifier.width = width
        modifier.segments = segments
        modifier.limit_method = "ANGLE"
        modifier.angle_limit = math.radians(30.0)
        modifier.harden_normals = False

        try:
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        except RuntimeError:
            obj.modifiers.remove(modifier)


def finish(name, bevel=True):
    if bevel:
        bevel_all()

    bpy.ops.object.select_all(action="SELECT")
    joined = bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active = joined
    bpy.ops.object.join()
    joined.name = name
    joined.data.name = name

    # Bake the transform into the mesh. join() adopts the transform of whichever
    # object happened to be first and rewrites every other vertex into that object's
    # local space to compensate - so on a join target that was a 5cm antler twig,
    # local coordinates come out ~20x. Renders and OBJ export both work in world
    # space so neither is wrong, but anything measured off obj.data.vertices is
    # nonsense until this runs.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    bpy.ops.object.shade_flat()
    return joined


def export(obj, name, assets_dir):
    bpy.ops.wm.obj_export(
        filepath=os.path.join(assets_dir, name + ".obj"),
        export_selected_objects=False, export_materials=True,
        export_normals=True, export_uv=True, export_triangulated_mesh=True,
        forward_axis="Z", up_axis="Y", path_mode="AUTO")


def write_col(path):
    # Blender is Z-up and Unity is Y-up, so y and z swap on the way out. The mesh
    # export does this itself; the sidecar has to be told.
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("# box  centre x y z  size x y z  qx qy qz qw\n")
        for (cx, cy, cz), (sx, sy, sz) in COLLIDERS:
            fh.write("box %.3f %.3f %.3f %.3f %.3f %.3f 0 0 0 1\n"
                     % (cx, cz, cy, sx, sz, sy))


def tint(strength=1.15):
    for mat in bpy.data.materials:
        key = mat.name.split(".")[0].lower()
        if key not in TINTS:
            continue
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if not bsdf:
            continue

        bsdf.inputs["Base Color"].default_value = TINTS[key]
        bsdf.inputs["Roughness"].default_value = 0.88
        if key in EMISSIVE:
            bsdf.inputs["Emission Color"].default_value = TINTS[key]
            bsdf.inputs["Emission Strength"].default_value = strength


def stage_scene(sun=3.2):
    bpy.ops.mesh.primitive_plane_add(size=30.0, location=(0, 0, 0))
    plane = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.19, 0.21, 0.16, 1)
    plane.data.materials.append(gm)

    bpy.ops.object.light_add(type="SUN", location=(3, 4, 6))
    bpy.context.active_object.data.energy = sun
    bpy.context.active_object.rotation_euler = (math.radians(52), 0, math.radians(200))

    world = bpy.data.worlds.new("w")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.36, 0.43, 0.53, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.65


def reference_cube(at):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=at)
    cube = bpy.context.active_object
    cm = bpy.data.materials.new("ref")
    cm.use_nodes = True
    cm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.52, 0.52, 0.56, 1)
    cube.data.materials.append(cm)


def camera(at, aim, lens=42):
    bpy.ops.object.camera_add(location=at)
    cam = bpy.context.active_object
    cam.data.lens = lens
    target = bpy.data.objects.new("aim", None)
    bpy.context.collection.objects.link(target)
    target.location = aim
    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = cam


def bloom_setup(size=8, threshold=0.75):
    """
    Soft halation around anything emissive.

    EEVEE Next dropped the old bloom checkbox, so this goes through the compositor
    instead. It is not decoration: an emissive surface with hard edges reads as
    painted plastic, and the entire difference between "a lit disc" and "a spirit"
    is light spilling past its own boundary.
    """
    scene = bpy.context.scene
    scene.use_nodes = True

    tree = scene.node_tree
    tree.nodes.clear()

    layers = tree.nodes.new("CompositorNodeRLayers")
    glare = tree.nodes.new("CompositorNodeGlare")
    output = tree.nodes.new("CompositorNodeComposite")

    # Fog glow rather than streaks: streaks read as a lens, and there is no camera in
    # the fiction. Fog glow is just light in air, which is what this is.
    glare.glare_type = "FOG_GLOW"
    glare.quality = "HIGH"
    glare.size = size
    glare.threshold = threshold

    tree.links.new(layers.outputs["Image"], glare.inputs["Image"])
    tree.links.new(glare.outputs["Image"], output.inputs["Image"])


def render(out_png, width=620, height=580, bloom=True):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"

    if bloom:
        bloom_setup()

    # Standard, not AgX. Blender 4.x defaults to AgX, which rolls bright values off
    # towards white - so an amber core at any useful emission strength rendered as a
    # cream disc and the whole point of choosing a warm colour was invisible.
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)
