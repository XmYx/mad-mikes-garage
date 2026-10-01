"""Extra tiles: the Sedan exploded into its game parts, and the Pickup clean vs weathered."""
import os

import bpy
from mathutils import Vector

import hdlib as H
import render_common as rc
import refbuild

HERE = os.path.dirname(os.path.abspath(__file__))

# exploded offsets in game voxels (x right, y up, z forward)
EXPLODE = {
    "Door_R": (9, 0, 0), "Door_L": (-9, 0, 0), "DoorRear_R": (9, 0, -2), "DoorRear_L": (-9, 0, -2),
    "Hood": (0, 9, 7), "Trunk": (0, 9, -6), "Glass": (0, 11, 0), "Bumper_F": (0, 0, 9), "Bumper_R": (0, 0, -9),
    "Wheel_FR": (9, 0, 2), "Wheel_FL": (-9, 0, 2), "Wheel_RR": (9, 0, -2), "Wheel_RL": (-9, 0, -2),
    "Engine": (0, 5, 4), "Interior": (0, 20, 0), "Seat_Driver": (0, 20, 0), "Seat_Passenger": (0, 20, 0), "Lights": (0, 0, 0),
}


def _stage():
    import build
    rc.reset_scene()
    sc = rc.setup_stage()
    build.stage_tweaks(sc)
    H.MATDEF.clear()
    H.std_mats()


def exploded(out):
    _stage()
    H.WEAR, H.TAG = 1.0, ""
    v = refbuild.build("Sedan")
    for name, off in EXPLODE.items():
        if name in v.parts:
            v.parts[name].offset = Vector(off)
    root, objs = v.build()
    rc.render_tile("Sedan_exploded", objs, out)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "blend", "Sedan_exploded.blend"), compress=True)
    return objs


def wear(out):
    _stage()
    objs = []
    for w, tag, at in ((0.0, "_clean", (-1.55, -1.55, 0)), (1.0, "", (1.55, 1.55, 0))):
        H.WEAR, H.TAG = w, tag
        v = refbuild.build("Pickup")
        v.name = "Pickup" + (tag or "_weathered")
        root, o = v.build(at=at)
        objs += o
    H.WEAR, H.TAG = 1.0, ""
    rc.render_tile("Pickup_wear", objs, out)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "blend", "Pickup_wear.blend"), compress=True)
    return objs


def run(out):
    exploded(out)
    wear(out)
