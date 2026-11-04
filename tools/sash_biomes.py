"""
Hausi: one sash per biome, each its own object and not a clasp on a shared strap.

Imported by sash_build.py. Every design is a diagonal chest strap on the same loop the baldric
used, so it sits where the body was measured and the worn offset holds, and differs in width,
material, drape, ends and a larger signature piece.

The game skins each material group with a vanilla donor and the token group, "bone", is what glows
on an upgraded sash, so every design keeps one signature piece in "bone". The groups used are the
ones Skins.cs already knows: cloth, bone, bark, wood, moss, iron, stone, rope and meal. Tints below
are for the previews and icons only. In game the donors decide the colour, so the silhouette has to
carry the material.

A dropped sash is the same mesh laid face down at 0.45, which shows its back. The pattern therefore
runs all the way round the loop and only the signature piece sits on the front.
"""

import math
import random

from sash_designs import GAP, band
from vhbuild import material

import bpy  # noqa: E402
from mathutils import Euler, Matrix, Vector  # noqa: E402

TOP = (-0.19, 0.0, 1.56)
BOTTOM = (0.17, 0.0, 1.0)
WORLD = Matrix.Identity(3)

TINT_SETS = {
    "meadows": {"cloth": (0.34, 0.17, 0.08, 1.0), "bone": (0.84, 0.78, 0.60, 1.0), "wood": (0.16, 0.08, 0.04, 1.0)},
    "blackforest": {"bark": (0.20, 0.14, 0.09, 1.0), "moss": (0.22, 0.38, 0.12, 1.0),
                    "bone": (0.40, 0.24, 0.15, 1.0), "wood": (0.56, 0.42, 0.24, 1.0)},
    "swamp": {"rope": (0.30, 0.22, 0.12, 1.0), "moss": (0.20, 0.30, 0.11, 1.0),
              "bone": (0.84, 0.80, 0.64, 1.0), "wood": (0.04, 0.035, 0.03, 1.0)},
    "mountain": {"cloth": (0.36, 0.56, 0.82, 1.0), "meal": (0.93, 0.95, 1.0, 1.0),
                 "iron": (0.32, 0.33, 0.38, 1.0), "bone": (0.88, 0.84, 0.72, 1.0)},
    "plains": {"cloth": (0.52, 0.09, 0.07, 1.0), "rope": (0.76, 0.58, 0.32, 1.0),
               "bone": (0.90, 0.86, 0.72, 1.0), "iron": (0.46, 0.47, 0.52, 1.0)},
    "mistlands": {"cloth": (0.30, 0.14, 0.52, 1.0), "stone": (0.17, 0.08, 0.32, 1.0),
                  "bone": (0.86, 0.76, 1.0, 1.0)},
    "ashlands": {"iron": (0.07, 0.07, 0.08, 1.0), "bone": (1.0, 0.42, 0.08, 1.0),
                 "stone": (0.18, 0.11, 0.10, 1.0)},
}


def radius_at(radii, angle):
    count = len(radii)
    slot = (angle / (2 * math.pi)) % 1.0 * count
    index = int(slot)
    frac = slot - index
    return radii[index % count] * (1.0 - frac) + radii[(index + 1) % count] * frac


class Strap:
    """A fitted loop, with a way to stand things on it: angle round it and a lift off the skin."""

    def __init__(self, made):
        self.obj, self.centre, self.u, self.v, self.normal, self.radii = made

    def frame(self, angle, lift=0.0):
        """The point and a basis whose columns are across the strap, out of the body, along the strap."""
        out = self.u * math.cos(angle) + self.v * math.sin(angle)
        along = self.u * -math.sin(angle) + self.v * math.cos(angle)
        point = self.centre + out * (radius_at(self.radii, angle) + GAP + lift)
        across = out.cross(along)
        basis = Matrix(((across.x, out.x, along.x), (across.y, out.y, along.y), (across.z, out.z, along.z)))
        return point, basis

    def spaced(self, spacing, start=0.0, end=2 * math.pi):
        """Angles at an even distance apart along the strap, which a fixed angle step is not."""
        angles = []
        last = None
        travelled = spacing
        samples = 720
        for k in range(samples + 1):
            angle = start + (end - start) * k / samples
            point, _ = self.frame(angle)
            if last is not None:
                travelled += (point - last).length
            last = point
            if travelled >= spacing:
                angles.append(angle)
                travelled = 0.0
        return angles


def jitter(degrees=3.0):
    return tuple(random.uniform(-degrees, degrees) for _ in range(3))


def _seat(obj, at, basis, shift, turn, scale):
    local = Matrix.Translation(Vector(shift)) @ Euler([math.radians(a) for a in turn]).to_matrix().to_4x4()
    obj.matrix_world = Matrix.Translation(Vector(at)) @ basis.to_4x4() @ local @ Matrix.Diagonal(Vector((*scale, 1.0)))


def block(size, at, basis, mat, shift=(0.0, 0.0, 0.0), turn=(0.0, 0.0, 0.0), tilt=2.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    obj = bpy.context.active_object
    obj.data.materials.append(material(mat))
    spin = jitter(tilt)
    _seat(obj, at, basis, shift, tuple(t + s for t, s in zip(turn, spin)), size)
    return obj


def spike(bottom, top, height, at, basis, mat, sides=5, shift=(0.0, 0.0, 0.0), turn=(0.0, 0.0, 0.0), tilt=2.0):
    """A cone standing on its base at `at` and pointing along the frame's z, turned from there."""
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=bottom, radius2=top, depth=height)
    obj = bpy.context.active_object
    obj.data.materials.append(material(mat))
    for vert in obj.data.vertices:
        vert.co.z += height * 0.5
    spin = jitter(tilt)
    _seat(obj, at, basis, shift, tuple(t + s for t, s in zip(turn, spin)), (1.0, 1.0, 1.0))
    return obj


def blob(radius, at, basis, mat, scale=(1.0, 1.0, 1.0), shift=(0.0, 0.0, 0.0), turn=(0.0, 0.0, 0.0), detail=2, tilt=3.0):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=detail, radius=radius)
    obj = bpy.context.active_object
    obj.data.materials.append(material(mat))
    spin = jitter(tilt)
    _seat(obj, at, basis, shift, tuple(t + s for t, s in zip(turn, spin)), scale)
    return obj


def loop(radius, tube, at, basis, mat, scale=(1.0, 1.0, 1.0), shift=(0.0, 0.0, 0.0), turn=(0.0, 0.0, 0.0),
         major=10, minor=5):
    """A torus whose hole runs along the frame's z."""
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=tube, major_segments=major, minor_segments=minor)
    obj = bpy.context.active_object
    obj.data.materials.append(material(mat))
    _seat(obj, at, basis, shift, turn, scale)
    return obj


def hang(at, length, width, mat, lean=0.0, thick=0.014, point=True):
    """A strip hanging from `at`, one clean piece with a pointed end rather than a stack of planks."""
    x, y, z = at
    block((width, thick, length), (x, y, z - length * 0.5), WORLD, mat, turn=(0.0, lean, 0.0), tilt=0.5)
    if point:
        edge = width * 0.7071
        block((edge, thick, edge), (x, y, z - length), WORLD, mat, turn=(0.0, lean + 45.0, 0.0), tilt=0.5)


def stitches(strap, spacing, offset, mat, skip=(), lift=0.0, size=(0.006, 0.004, 0.014)):
    for angle in strap.spaced(spacing):
        if any(lo <= angle <= hi for lo, hi in skip):
            continue
        point, basis = strap.frame(angle, lift)
        for side in (-1, 1):
            block(size, point, basis, mat, shift=(side * offset, size[1] * 0.5, 0.0), tilt=3.0)


def meadows():
    strap = Strap(band(TOP, BOTTOM, 0.09, 0.016, "cloth", stations=36))
    stitches(strap, 0.05, 0.034, "wood", skip=((0.2 * math.pi, 0.4 * math.pi), (0.93 * math.pi, 1.1 * math.pi)),
             lift=0.016)

    for angle in (0.06 * math.pi, 1.58 * math.pi):
        point, basis = strap.frame(angle, 0.016)
        spike(0.011, 0.011, 0.012, point, basis, "bone", sides=7, turn=(-90.0, 0.0, 0.0))

    point, basis = strap.frame(0.3 * math.pi, 0.016)
    block((0.08, 0.012, 0.11), point, basis, "cloth", shift=(0.0, 0.006, 0.0))
    for side in (-1, 1):
        block((0.016, 0.026, 0.092), point, basis, "bone", shift=(side * 0.048, 0.02, 0.0), tilt=1.5)
    for end in (-1, 1):
        block((0.112, 0.026, 0.016), point, basis, "bone", shift=(0.0, 0.02, end * 0.044), tilt=1.5)
    block((0.012, 0.03, 0.09), point, basis, "bone", shift=(0.0, 0.022, 0.0), tilt=1.0)

    point, basis = strap.frame(0.52 * math.pi, 0.016)
    block((0.07, 0.01, 0.05), point, basis, "cloth", shift=(0.0, 0.006, 0.0))

    hip = strap.frame(math.pi, 0.0)[0]
    hang((hip.x + 0.02, hip.y, hip.z), 0.22, 0.075, "cloth", lean=4.0)
    hang((hip.x - 0.045, hip.y + 0.018, hip.z), 0.15, 0.07, "cloth", lean=-4.0)
    knot = (hip.x + 0.02, hip.y + 0.01, hip.z - 0.25)
    blob(0.03, knot, WORLD, "cloth", scale=(1.0, 0.8, 0.9), detail=1)
    tip = (knot[0], knot[1] + 0.012, knot[2] - 0.015)
    spike(0.03, 0.005, 0.24, tip, WORLD, "bone", sides=7, turn=(180.0, 0.0, 0.0))
    for turn, length, drop in ((40.0, 0.1, 0.05), (-40.0, 0.1, 0.11)):
        spike(0.016, 0.003, length, (tip[0], tip[1], tip[2] - drop), WORLD, "bone", sides=5,
              turn=(180.0, turn, 0.0))


def blackforest():
    strap = Strap(band(TOP, BOTTOM, 0.105, 0.028, "bark", stations=36))

    for k, angle in enumerate(strap.spaced(0.05)):
        point, basis = strap.frame(angle, 0.028)
        block((0.118, 0.007, 0.02), point, basis, "bark", shift=(0.0, 0.003, 0.0),
              turn=(0.0, 34.0 if k % 2 == 0 else -34.0, 0.0))

    for angle, radius, across in ((0.02 * math.pi, 0.07, 0.0), (0.07 * math.pi, 0.045, 0.03),
                                  (0.62 * math.pi, 0.036, -0.02), (0.95 * math.pi, 0.03, 0.02),
                                  (1.45 * math.pi, 0.04, 0.0), (1.8 * math.pi, 0.032, 0.0)):
        point, basis = strap.frame(angle, 0.028)
        blob(radius, point, basis, "moss", scale=(1.0, 0.75, 1.0), shift=(across, radius * 0.45, 0.0), detail=2)
        blob(radius * 0.6, point, basis, "moss", scale=(1.0, 0.8, 1.0),
             shift=(across + radius * 0.8, radius * 0.3, radius * 0.5), detail=1)

    point, basis = strap.frame(0.24 * math.pi, 0.028)
    for fan in range(-4, 5):
        spike(0.008, 0.0015, 0.17 - abs(fan) * 0.01, point, basis, "moss", sides=4,
              shift=(0.0, 0.01, 0.0), turn=(-48.0, 180.0 + fan * 16.0, 0.0), tilt=3.0)
    point, basis = strap.frame(0.36 * math.pi, 0.028)
    blob(0.036, point, basis, "bone", scale=(1.0, 0.8, 1.0), shift=(0.0, 0.03, 0.0), detail=1)
    for turn in (0.0, 90.0, 180.0, 270.0):
        spike(0.024, 0.01, 0.1, point, basis, "bone", sides=5, shift=(0.0, 0.03, 0.0),
              turn=(-14.0, turn + 20.0, 0.0), tilt=6.0)

    hip = strap.frame(math.pi, 0.0)[0]
    for dx, dy, length in ((-0.045, 0.0, 0.3), (-0.015, 0.02, 0.36), (0.018, 0.0, 0.32), (0.048, 0.018, 0.27)):
        top = (hip.x + dx, hip.y + dy, hip.z - 0.02)
        spike(0.024, 0.02, length, (top[0], top[1], top[2] - length), WORLD, "bark", sides=7, tilt=3.0)
        spike(0.02, 0.019, 0.008, (top[0], top[1], top[2] - length - 0.006), WORLD, "wood", sides=7, tilt=1.0)
    for drop in (0.06, 0.2):
        loop(0.056, 0.011, (hip.x + 0.002, hip.y + 0.01, hip.z - drop), WORLD, "wood", scale=(1.0, 0.75, 1.0))


def bone_charm(at, length, mat="bone", turn=0.0):
    """A long bone with a knob at each end, hanging from a cord."""
    x, y, z = at
    spike(0.012, 0.012, 0.045, (x, y, z - 0.045), WORLD, "rope", sides=4, tilt=1.0)
    spike(0.011, 0.011, length, (x, y, z - 0.045 - length), WORLD, mat, sides=5, turn=(0.0, turn, 0.0), tilt=2.0)
    for end in (0.0, length):
        for side in (-0.011, 0.011):
            blob(0.014, (x + side, y, z - 0.045 - end), WORLD, mat, detail=1, tilt=1.0)


def skull(at, size, mat="bone"):
    """A cranium over a jaw, with dark sockets and a nose, facing the front."""
    x, y, z = at
    blob(size, (x, y, z), WORLD, mat, scale=(1.0, 0.95, 0.95), detail=2, tilt=2.0)
    block((size * 0.95, size * 0.8, size * 0.5), (x, y + size * 0.15, z - size * 0.75), WORLD, mat, tilt=2.0)
    for side in (-1, 1):
        blob(size * 0.26, (x + side * size * 0.38, y + size * 0.8, z - size * 0.05), WORLD, "wood", detail=1, tilt=1.0)
    block((size * 0.16, size * 0.18, size * 0.28), (x, y + size * 0.9, z - size * 0.4), WORLD, "wood", tilt=1.0)


def swamp():
    strap = Strap(band(TOP, BOTTOM, 0.07, 0.02, "rope", stations=36))

    for k, angle in enumerate(strap.spaced(0.036)):
        point, basis = strap.frame(angle, 0.02)
        lane = k % 3
        blob(0.032, point, basis, "moss" if k % 2 else "rope", scale=(0.8, 0.7, 1.45),
             shift=((lane - 1) * 0.017, 0.01, 0.0), turn=(0.0, (1, -1, 1)[lane] * 20.0, 0.0), detail=1, tilt=1.5)

    for angle, length in ((0.12 * math.pi, 0.1), (0.62 * math.pi, 0.12)):
        point, _ = strap.frame(angle, 0.03)
        bone_charm((point.x, point.y + 0.012, point.z), length, turn=12.0)

    point, _ = strap.frame(0.36 * math.pi, 0.03)
    spike(0.008, 0.008, 0.07, (point.x, point.y + 0.01, point.z - 0.07), WORLD, "rope", sides=4, tilt=1.0)
    skull((point.x, point.y + 0.06, point.z - 0.12), 0.06)

    back, _ = strap.frame(1.5 * math.pi, 0.03)
    bone_charm((back.x, back.y - 0.012, back.z), 0.1, turn=-10.0)

    hip = strap.frame(math.pi, 0.0)[0]
    for k in range(4):
        blob(0.03, (hip.x + 0.01 * (k % 2), hip.y + 0.01, hip.z - 0.03 - k * 0.05), WORLD,
             "moss" if k % 2 else "rope", scale=(0.9, 0.7, 1.5), turn=(0.0, 25.0 * (1 if k % 2 else -1), 0.0),
             detail=1, tilt=3.0)
    for dx, length in ((-0.05, 0.3), (-0.02, 0.38), (0.015, 0.33), (0.05, 0.27)):
        spike(0.014, 0.008, length, (hip.x + dx, hip.y + 0.015, hip.z - 0.02 - length), WORLD, "moss", sides=5,
              tilt=2.5)
    loop(0.05, 0.011, (hip.x, hip.y + 0.014, hip.z - 0.1), WORLD, "rope", scale=(1.0, 0.7, 1.0))


def mountain():
    strap = Strap(band(TOP, BOTTOM, 0.1, 0.02, "cloth", stations=36))
    stitches(strap, 0.07, 0.043, "meal", lift=0.02, size=(0.008, 0.005, 0.03))

    point, basis = strap.frame(0.01 * math.pi, 0.02)
    centre = point + basis @ Vector((0.0, 0.012, 0.0))
    blob(0.075, centre, basis, "meal", scale=(1.3, 0.9, 1.1), detail=2)
    for _ in range(130):
        direction = Vector((random.uniform(-1.2, 1.2), random.uniform(0.05, 1.0), random.uniform(-1.0, 1.0)))
        direction.normalize()
        world = basis @ direction
        base = centre + basis @ Vector((direction.x * 0.095, direction.y * 0.06, direction.z * 0.08))
        spike(0.02, 0.002, random.uniform(0.06, 0.11), base, world.to_track_quat("Z", "Y").to_matrix(), "meal",
              sides=4, tilt=0.0)

    for angle, length in ((0.3 * math.pi, 0.13), (0.43 * math.pi, 0.18), (0.57 * math.pi, 0.14)):
        point, basis = strap.frame(angle, 0.02)
        blob(0.016, point, basis, "iron", shift=(0.0, 0.02, 0.0), detail=1)
        top = (point.x, point.y + 0.025, point.z - 0.03)
        block((0.04, 0.024, 0.036), top, WORLD, "iron", tilt=2.0)
        spike(0.025, 0.004, length, (top[0], top[1] + 0.004, top[2] - 0.018), WORLD, "bone", sides=5,
              turn=(180.0, 0.0, 0.0), tilt=2.0)

    hip = strap.frame(math.pi, 0.0)[0]
    hang((hip.x + 0.015, hip.y, hip.z), 0.22, 0.09, "cloth", lean=3.0)
    hang((hip.x - 0.05, hip.y + 0.02, hip.z), 0.15, 0.075, "cloth", lean=-3.0)
    for dx in (-0.04, 0.0, 0.04):
        for dz in (0.0, -0.03):
            direction = Vector((dx * 3.0, 0.2, -1.0)).normalized()
            spike(0.016, 0.002, 0.08, (hip.x + 0.015 + dx, hip.y + 0.012, hip.z - 0.22 + dz), direction.to_track_quat(
                "Z", "Y").to_matrix(), "meal", sides=4, tilt=0.0)


def facing(direction):
    return Vector(direction).normalized().to_track_quat("Z", "Y").to_matrix()


def plains():
    strap = Strap(band(TOP, BOTTOM, 0.06, 0.016, "cloth", stations=36))

    for k, angle in enumerate(strap.spaced(0.038)):
        point, basis = strap.frame(angle, 0.016)
        mat = "cloth" if k % 2 == 0 else "rope"
        for side in (-1, 1):
            block((0.04, 0.012, 0.062), point, basis, mat, shift=(side * 0.014, 0.008, 0.0),
                  turn=(0.0, side * -34.0, 0.0), tilt=1.0)

    point, basis = strap.frame(0.02 * math.pi, 0.016)
    base = point + basis @ Vector((0.0, 0.03, 0.0))
    heading = Vector((-0.75, 0.3, 0.4)).normalized()
    sweep = Vector((0.0, 0.0, 1.0))
    cursor = base
    steps = 6
    for step in range(steps):
        bottom = 0.036 - 0.032 * (step / steps) ** 1.2
        top = 0.036 - 0.032 * ((step + 1) / steps) ** 1.2
        length = 0.06
        spike(bottom, max(top, 0.004), length * 1.1, cursor, facing(heading), "bone", sides=7, tilt=0.0)
        cursor = cursor + heading * length
        heading = (heading + sweep * 0.2).normalized()

    wrap = base + Vector((-0.75, 0.3, 0.4)).normalized() * 0.04
    loop(0.036, 0.012, wrap, facing((-0.75, 0.3, 0.4)), "cloth", major=9)
    clasp, clasp_basis = strap.frame(0.02 * math.pi, 0.016)
    spike(0.05, 0.05, 0.016, clasp, clasp_basis, "iron", sides=6, turn=(-90.0, 0.0, 0.0))
    blob(0.022, clasp, clasp_basis, "iron", scale=(1.0, 0.6, 1.0), shift=(0.0, 0.02, 0.0), detail=1)

    hip = strap.frame(math.pi, 0.0)[0]
    blob(0.032, (hip.x + 0.01, hip.y + 0.01, hip.z - 0.03), WORLD, "cloth", scale=(1.0, 0.8, 1.0), detail=1)
    for k, dx in enumerate((-0.04, -0.025, -0.01, 0.005, 0.02, 0.035, 0.05)):
        length = 0.24 + 0.03 * ((k * 3) % 4)
        spike(0.011, 0.005, length, (hip.x + dx, hip.y + 0.01 + 0.004 * (k % 2), hip.z - 0.05 - length), WORLD,
              "cloth" if k % 2 == 0 else "rope", sides=4, tilt=2.0)
    for dx in (-0.04, 0.02):
        blob(0.012, (hip.x + dx, hip.y + 0.014, hip.z - 0.3), WORLD, "bone", detail=1, tilt=0.0)


def mistlands():
    strap = Strap(band(TOP, BOTTOM, 0.075, 0.012, "stone", stations=36))

    for k, angle in enumerate(strap.spaced(0.052)):
        point, basis = strap.frame(angle, 0.012)
        block((0.096, 0.012, 0.086), point, basis, "cloth", shift=(0.0, 0.008, 0.0),
              turn=(-10.0, 5.0 if k % 2 else -5.0, 0.0), tilt=1.0)
        blob(0.012, point, basis, "bone", shift=(0.0, 0.022, 0.006), detail=1, tilt=0.0)

    point, basis = strap.frame(0.0, 0.012)
    for lift, size, shift in ((0.03, (0.15, 0.016, 0.12), 0.0), (0.05, (0.12, 0.016, 0.095), 0.0),
                              (0.07, (0.09, 0.016, 0.07), 0.0)):
        block(size, point, basis, "stone", shift=(0.0, lift, shift), turn=(-8.0, 0.0, 0.0), tilt=1.0)
    blob(0.022, point, basis, "bone", shift=(0.0, 0.1, 0.0), detail=1, tilt=0.0)

    hip = strap.frame(math.pi, 0.0)[0]
    loop(0.04, 0.009, (hip.x + 0.01, hip.y + 0.012, hip.z - 0.01), WORLD, "bone", turn=(90.0, 0.0, 0.0))
    for j in range(4):
        width = 0.088 - 0.012 * j
        centre = (hip.x + 0.01, hip.y + 0.012 + 0.003 * j, hip.z - 0.05 - j * 0.052)
        block((width, 0.012, 0.07), centre, WORLD, "cloth" if j % 2 == 0 else "stone", turn=(-8.0, 0.0, 0.0), tilt=1.0)
        blob(0.011, (centre[0], centre[1] + 0.012, centre[2]), WORLD, "bone", detail=1, tilt=0.0)
    last = hip.z - 0.05 - 3 * 0.052 - 0.045
    block((0.052, 0.012, 0.052), (hip.x + 0.01, hip.y + 0.021, last), WORLD, "cloth", turn=(0.0, 45.0, 0.0), tilt=0.5)


def link(at, basis, flat, shift=(0.0, 0.0, 0.0)):
    loop(0.025, 0.011, at, basis, "iron", scale=(0.75, 1.6, 1.0), shift=shift,
         turn=(90.0, 0.0, 0.0) if flat else (90.0, 0.0, 90.0))


def ashlands():
    strap = Strap(band(TOP, BOTTOM, 0.022, 0.01, "iron", stations=36))

    for k, angle in enumerate(strap.spaced(0.056)):
        flat = k % 2 == 0
        point, basis = strap.frame(angle, 0.014 if flat else 0.032)
        link(point, basis, flat)
        if flat:
            blob(0.014, point, basis, "bone", shift=(0.0, 0.016, 0.0), detail=1, tilt=0.0)

    point, basis = strap.frame(0.34 * math.pi, 0.02)
    spike(0.05, 0.05, 0.018, point, basis, "iron", sides=6, turn=(-90.0, 0.0, 0.0), shift=(0.0, 0.02, 0.0))
    for pitch, roll, length in ((0.0, 0.0, 0.24), (-32.0, 0.0, 0.17), (32.0, 0.0, 0.17), (0.0, -32.0, 0.14),
                                (0.0, 32.0, 0.14)):
        spike(0.04, 0.005, length, point, basis, "stone", sides=5, shift=(0.0, 0.034, 0.0),
              turn=(-90.0 + pitch, 0.0, roll), tilt=3.0)
    blob(0.02, point, basis, "bone", shift=(0.0, 0.05, 0.0), detail=1, tilt=0.0)

    point, basis = strap.frame(0.01 * math.pi, 0.03)
    for direction, length in (((-0.8, 0.0, 0.6), 0.13), ((-0.4, 0.3, 0.9), 0.1), ((-0.9, 0.3, 0.2), 0.09)):
        spike(0.026, 0.004, length, point, facing(direction), "stone", sides=5, tilt=0.0)

    hip = strap.frame(math.pi, 0.0)[0]
    for j in range(6):
        link((hip.x + 0.01, hip.y + 0.012, hip.z - 0.03 - j * 0.045), WORLD, j % 2 == 0)
    spike(0.036, 0.004, 0.16, (hip.x + 0.01, hip.y + 0.012, hip.z - 0.3 - 0.16), WORLD, "stone", sides=5,
          tilt=1.0)
    blob(0.016, (hip.x + 0.01, hip.y + 0.03, hip.z - 0.32), WORLD, "bone", detail=1, tilt=0.0)


SASHES = {
    "meadows": meadows,
    "blackforest": blackforest,
    "swamp": swamp,
    "mountain": mountain,
    "plains": plains,
    "mistlands": mistlands,
    "ashlands": ashlands,
}
