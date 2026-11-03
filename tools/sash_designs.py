"""
Hausi: candidates for the sash, worn on the real body.

    blender --background --python tools/sash_designs.py

A sash is seen on a player and nowhere else that matters, so every candidate is fitted to, and
rendered on, the player's own bind-pose mesh out of a Devkit rip (rips/Player/Player.obj, scaled
by 0.95 because the rig's scale is 95 where the mesh's unit is a hundredth). It is rendered from
the front and from behind, because the camera in play stands behind the wearer and other players
see the front.

Rigid, not skinned. An attach_skin mesh would bend with the body, but it needs bone weights and
OBJ has none, so the sash is parented to one spine bone and has to be shaped so that this does not
show: hugging the ribcage, clear of the armpit, and stopping above the hip that moves most.

Five outlines, and the differences are in the outline, per section 8 of the suite's notes:

    baldric  one narrow strap over the left shoulder, a round clasp on the chest, two short tails
    pelt     a broad hide slung the same way, with a torn lower edge and a tusk pin
    crossed  two straps crossing front and back in an X, with a square boss where they meet
    girdle   a broad band round the waist, a knot on the left hip and two long tails
    tokens   a thin cord over the shoulder hung with five big bone tokens

Two material groups only, "cloth" and "bone", which is the most a vanilla prop wears. Tints here
exist only so a render says something about form; the game's own materials go on at runtime. The
Meadows token, an antler tine, stands in for the per-biome detail on all five.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vhbuild import *  # noqa: E402,F403

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RIP = os.path.join(ROOT, "testprofile", "BepInEx", "rips", "Player", "Player.obj")
OUT = os.path.join(ROOT, "renders")
VARIANTS = os.path.join(ROOT, "assets", "variants")

RIG_SCALE = 0.95
GAP = 0.014

TINTS.update({
    "cloth": (0.22, 0.14, 0.07, 1.0),
    "bone": (0.62, 0.56, 0.42, 1.0),
    "skin": (0.30, 0.30, 0.31, 1.0),
})

_torso = None


def torso_points():
    """
    The body's vertices without the arms, read off the imported mesh and not worked out from the
    file. The axis settings are what make a round trip exact, and guessing the same mapping by hand
    put the first pass of these on backwards. In this space the face looks towards +y and the wearer's
    left is -x, which is checked below at the nose rather than taken on trust.
    """
    global _torso
    if _torso is not None:
        return _torso

    clear_scene()
    obj = body()
    world = [obj.matrix_world @ vert.co for vert in obj.data.vertices]

    # The mesh is about a thousand vertices, so a torso face is a hand wide and a strap fitted to
    # vertices alone sinks between them. It is the skin that has to be cleared, so sample the faces.
    for poly in obj.data.polygons:
        corners = [obj.matrix_world @ obj.data.vertices[i].co for i in poly.vertices]
        for i in range(1, len(corners) - 1):
            a, b, c = corners[0], corners[i], corners[i + 1]
            steps = max(1, int(max((a - b).length, (b - c).length, (c - a).length) / 0.015))
            for m in range(steps + 1):
                for n in range(steps + 1 - m):
                    world.append(a + (b - a) * (m / steps) + (c - a) * (n / steps))

    head = [p for p in world if p.z > 1.66]
    nose = max(head, key=lambda p: abs(p.y))
    if nose.y <= 0:
        raise RuntimeError("the face no longer looks towards +y: nose at %s" % nose)

    # Everything past the shoulder is an arm, standing straight out in the bind pose and hanging at
    # the side in play, so it must not push a strap outwards.
    _torso = [p for p in world if abs(p.x) < 0.30 and (abs(p.x) < 0.26 or p.z > 1.42)]
    clear_scene()
    return _torso


def surface(x, z, front):
    """Where the body's skin is, at this x and height, on its front or its back."""
    near = [p for p in torso_points() if abs(p.x - x) < 0.05 and abs(p.z - z) < 0.05]
    return max(p.y for p in near) if front else min(p.y for p in near)


def fit_radius(centre, u, v, normal, angle, spread=0.13, slab=0.05):
    """How far out from the centre the body reaches in this direction, within this plane."""
    direction = u * math.cos(angle) + v * math.sin(angle)
    reach = 0.05

    for point in torso_points():
        rel = point - centre
        if abs(rel.dot(normal)) > slab:
            continue

        along = rel.dot(direction)
        if along <= 0:
            continue

        side = (rel - direction * along - normal * rel.dot(normal)).length
        if side / along < spread:
            reach = max(reach, along)

    return reach


def band(top, bottom, width, thick, mat, stations=30, edge=None, level=False):
    """
    A closed strap round the body in the plane through two points: over `top`, under `bottom`.

    Fitted station by station to the measured body and then smoothed, so it hugs the ribs. Closed
    on purpose, as a real solid of four faces a station: a surface with no thickness is a hole in
    the game from behind, whatever Blender shows.
    """
    top, bottom = Vector(top), Vector(bottom)
    centre = (top + bottom) * 0.5

    if level:
        # Round the waist: a level plane at this height, and top and bottom give the height only.
        u, v, normal = Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    else:
        u = (top - centre).normalized()
        v = Vector((0, 1, 0))
        v = (v - u * v.dot(u)).normalized()
        normal = u.cross(v).normalized()

    radii = [fit_radius(centre, u, v, normal, 2 * math.pi * i / stations) for i in range(stations)]
    # Smoothed, then never allowed back inside what was measured: a plain average cuts the corners
    # of the ribcage, which is where the strap would go through the skin.
    raw = radii
    for _ in range(3):
        radii = [(radii[i - 1] + radii[i] * 2 + radii[(i + 1) % stations]) / 4 for i in range(stations)]
    radii = [max(smooth, measured) for smooth, measured in zip(radii, raw)]

    mesh = bpy.data.meshes.new("band")
    bm = bmesh.new()
    rows = []

    for i in range(stations):
        angle = 2 * math.pi * i / stations
        out = u * math.cos(angle) + v * math.sin(angle)

        radius = radii[i] + GAP + random.uniform(-0.002, 0.002)
        half = width * 0.5 * (1.0 + random.uniform(-0.05, 0.05))

        low = half * (edge(i, stations) if edge else 1.0)
        at = centre + out * radius

        rows.append([
            bm.verts.new(at + normal * half),
            bm.verts.new(at + normal * half + out * thick),
            bm.verts.new(at - normal * low + out * thick),
            bm.verts.new(at - normal * low),
        ])

    for i in range(stations):
        a, b = rows[i], rows[(i + 1) % stations]
        for k in range(4):
            bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("band", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material(mat))
    return obj, centre, u, v, normal, radii


def out_at(centre, u, v, radii, angle, extra=0.0):
    """The point on a band's outside at this angle."""
    stations = len(radii)
    radius = radii[int(angle / (2 * math.pi) * stations) % stations] + GAP + extra
    return centre + (u * math.cos(angle) + v * math.sin(angle)) * radius


def tail(at, length, width, mat, lean_x=0.0, back=0.0):
    """A hanging tail: a flat tapering box in three pieces, so that it is not a plank."""
    z = at[2]
    x, y = at[0], at[1]
    for i in range(3):
        step = length / 3
        w = width * (1.0 - 0.22 * i)
        box((w, 0.018, step * 1.15), (x, y, z - step * 0.5), mat, rot_x=back, rot_y=lean_x, tilt=3.0)
        x += lean_x * 0.01 * step * 2
        y += back * 0.01 * step * 2
        z -= step


def antler(at, mat):
    """The Meadows token: a short curved tine, two tapers overlapping."""
    x, y, z = at
    taper(0.022, 0.014, 0.11, (x, y, z + 0.04), mat, sides=7, rot_y=14.0, tilt=2.0)
    taper(0.014, 0.004, 0.09, (x + 0.02, y, z + 0.12), mat, sides=7, rot_y=38.0, tilt=2.0)
    taper(0.012, 0.004, 0.06, (x - 0.008, y, z + 0.08), mat, sides=7, rot_y=-32.0, tilt=2.0)


def baldric(token=antler):
    strap, centre, u, v, normal, radii = band((-0.19, 0.0, 1.56), (0.17, 0.0, 1.0), 0.075, 0.012, "cloth")
    chest = out_at(centre, u, v, radii, 0.33 * math.pi, 0.012)
    disc(0.05, 0.022, tuple(chest), "bone", sides=13)
    token((chest.x, chest.y + 0.02, chest.z - 0.05), "bone")
    side = out_at(centre, u, v, radii, math.pi, 0.0)
    tail((side.x + 0.02, side.y, side.z), 0.24, 0.06, "cloth", lean_x=8.0)
    tail((side.x - 0.04, side.y + 0.03, side.z), 0.18, 0.055, "cloth", lean_x=-6.0)


def pelt():
    def torn(i, stations):
        # The lower edge only, and only in big bites: small ones read as noise at eye distance.
        return 1.0 + (0.55 if i % 3 == 0 else 0.0) + (0.25 if i % 5 == 0 else 0.0)

    hide, centre, u, v, normal, radii = band((-0.17, 0.0, 1.55), (0.18, 0.0, 1.02), 0.16, 0.022, "cloth",
                                             stations=26, edge=torn)
    chest = out_at(centre, u, v, radii, 0.3 * math.pi, 0.024)
    antler((chest.x, chest.y + 0.01, chest.z - 0.06), "bone")
    box((0.05, 0.02, 0.05), tuple(chest), "bone", rot_x=8.0, tilt=3.0)


def crossed():
    a, centre, u, v, normal, radii = band((-0.17, 0.0, 1.56), (0.16, 0.0, 1.08), 0.055, 0.012, "cloth")
    band((0.17, 0.0, 1.56), (-0.16, 0.0, 1.08), 0.055, 0.012, "cloth")

    front = Vector((0.0, surface(0.0, 1.32, True) + 0.03, 1.32))
    back = Vector((0.0, surface(0.0, 1.32, False) - 0.03, 1.32))
    box((0.085, 0.024, 0.085), tuple(front), "bone", tilt=3.0)
    box((0.07, 0.024, 0.07), tuple(back), "bone", tilt=3.0)
    antler((front.x, front.y + 0.02, front.z - 0.05), "bone")


def girdle():
    belt, centre, u, v, normal, radii = band((0.0, 0.0, 1.06), (0.0, 0.0, 1.06), 0.12, 0.016, "cloth", level=True)
    knot = out_at(centre, u, v, radii, 0.1 * math.pi, 0.02)
    orb(0.05, tuple(knot), "cloth", stretch=0.9)
    tail((knot.x - 0.015, knot.y + 0.02, knot.z - 0.02), 0.48, 0.09, "cloth", lean_x=5.0)
    tail((knot.x + 0.03, knot.y - 0.025, knot.z - 0.02), 0.38, 0.08, "cloth", lean_x=-7.0)
    antler((knot.x - 0.01, knot.y + 0.05, knot.z - 0.14), "bone")


def tokens():
    cord, centre, u, v, normal, radii = band((-0.18, 0.0, 1.57), (0.18, 0.0, 1.03), 0.024, 0.016, "cloth", stations=30)

    for index, angle in enumerate((0.75, 1.10, 1.45, 1.80, 2.15)):
        at = out_at(centre, u, v, radii, angle, 0.012)
        size = 0.034 if index != 2 else 0.044
        taper(size, 0.007, 0.13 if index != 2 else 0.17, (at.x, at.y, at.z - 0.07), "bone", sides=5,
              rot_x=180.0, tilt=3.0)

    back = out_at(centre, u, v, radii, 4.7, 0.012)
    disc(0.04, 0.02, tuple(back), "bone", sides=13)


DESIGNS = {"baldric": baldric, "pelt": pelt, "crossed": crossed, "girdle": girdle, "tokens": tokens}


def body():
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=RIP, forward_axis="Z", up_axis="Y")
    for obj in set(bpy.data.objects) - before:
        obj.scale = (RIG_SCALE, RIG_SCALE, RIG_SCALE)
        obj.data.materials.clear()
        obj.data.materials.append(material("skin"))
        for poly in obj.data.polygons:
            poly.use_smooth = True
        obj.name = "body"
        return obj


def open_edges(obj):
    """Edges on exactly one face. A closed model has none."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    count = sum(1 for edge in bm.edges if len(edge.link_faces) == 1)
    bm.free()
    return count


def lights():
    bpy.ops.object.light_add(type="SUN", location=(3, 4, 6))
    bpy.context.active_object.data.energy = 1.4
    bpy.context.active_object.rotation_euler = (math.radians(55), 0, math.radians(150))
    bpy.ops.object.light_add(type="SUN", location=(-3, -4, 6))
    bpy.context.active_object.data.energy = 0.35
    bpy.context.active_object.rotation_euler = (math.radians(60), 0, math.radians(-30))

    world = bpy.data.worlds.new("w")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.36, 0.43, 0.53, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.28


def ground():
    bpy.ops.mesh.primitive_plane_add(size=30.0, location=(0, 0, 0))
    plane = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.19, 0.21, 0.16, 1)
    plane.data.materials.append(gm)


def shoot(name, side, sign):
    """Eye height, three metres back, 42mm, with the 1m cube beside the body."""
    for cam in [o for o in bpy.data.objects if o.type == "CAMERA" or o.name.startswith("aim")]:
        bpy.data.objects.remove(cam)

    camera((0.0, sign * 3.0, 1.7), (0.0, 0.0, 1.1), lens=42)
    render(os.path.join(OUT, "sash_%s_%s.png" % (name, side)), width=620, height=640, bloom=False)


def build(name):
    clear_scene()
    random.seed(SEED)

    DESIGNS[name]()

    bpy.ops.object.select_all(action="SELECT")
    bpy.context.view_layer.objects.active = bpy.context.selected_objects[0]
    bpy.ops.object.join()
    sash = bpy.context.active_object
    sash.name = "sash_" + name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.shade_flat()

    bm = bmesh.new()
    bm.from_mesh(sash.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    tris = len(bm.faces)
    bm.free()

    print("HAUSI %s: %d triangles, 0 colliders, %d open edges" % (name, tris, open_edges(sash)))

    os.makedirs(VARIANTS, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    sash.select_set(True)
    bpy.ops.wm.obj_export(filepath=os.path.join(VARIANTS, "sash_" + name + ".obj"),
                          export_selected_objects=True, export_materials=False,
                          export_normals=True, export_uv=True, export_triangulated_mesh=True,
                          forward_axis="Z", up_axis="Y")

    body()
    ground()
    lights()
    reference_cube((1.05, 0.0, 0.5))
    tint()

    shoot(name, "front", 1)
    shoot(name, "back", -1)


if __name__ == "__main__" and "sash_build" not in " ".join(sys.argv):
    os.makedirs(OUT, exist_ok=True)
    torso_points()
    for design in DESIGNS:
        build(design)
