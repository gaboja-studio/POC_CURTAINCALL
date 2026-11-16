"""Author one distance-matched Revision7 Run action without changing source assets.

Blender background usage:
  blender --background --factory-startup --threads 2 --python THIS_FILE -- \
    --output .harness/local/pursuer-weight-20261116/heavy-858-v1

The output directory must be new and inside the task's ignored output root.
Only the new Run action is exported; the existing look FBX and 24 other actions
are not exported or overwritten. --variant fallback uses 7.48 m / .32 stance.
"""

import argparse
import array
import ast
import csv
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

import bpy
from mathutils import Euler, Matrix, Quaternion, Vector


WORKSPACE = Path(__file__).resolve().parents[4]
OUTPUT_ROOT = (WORKSPACE / ".harness/local/pursuer-weight-20261116").resolve()
SOURCE_TASK = Path("C:/Unity/github-worktrees/POC_CURTAINCALL/pursuer-doll-model/Tasks/Active/Task-20261114-130-pursuer-doll-model")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves


def action_digest(action):
    h = hashlib.sha256()
    for fc in curves(action):
        h.update((fc.data_path + ":" + str(fc.array_index)).encode())
        values = array.array("f", [0]) * (len(fc.keyframe_points) * 2)
        fc.keyframe_points.foreach_get("co", values)
        h.update(values.tobytes())
    return h.hexdigest()


def model_digest(rig, meshes):
    h = hashlib.sha256()
    for bone in rig.data.bones:
        h.update((bone.name + ":" + (bone.parent.name if bone.parent else "")).encode())
        h.update(struct.pack("16d", *(v for row in bone.matrix_local for v in row)))
    for obj in sorted(meshes, key=lambda item: item.name):
        h.update(obj.name.encode())
        h.update(str([(g.index, g.name) for g in obj.vertex_groups]).encode())
        coords = array.array("f", [0]) * (len(obj.data.vertices) * 3)
        obj.data.vertices.foreach_get("co", coords)
        h.update(coords.tobytes())
        for vertex in obj.data.vertices:
            for group in vertex.groups:
                h.update(struct.pack("Id", group.group, group.weight))
    return h.hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--source-task", default=str(SOURCE_TASK))
    parser.add_argument("--variant", choices=("heavy", "fallback"), default="heavy")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    output = Path(args.output).resolve()
    source = Path(args.source_task).resolve()
    if output == OUTPUT_ROOT or not output.is_relative_to(OUTPUT_ROOT):
        raise RuntimeError("Output must be a NEW child of " + str(OUTPUT_ROOT))
    if output.exists() or output.is_relative_to(source):
        raise RuntimeError("Refusing to overwrite an existing output or source directory")
    source_script = source / "Source/animate-original.py"
    source_blend = source / "Model/Revision7/animated.blend"
    source_hashes = {str(p): sha(p) for p in (source_script, source_blend)}
    output.mkdir(parents=True)

    intervals, stance = (78, .30) if args.variant == "heavy" else (68, .32)
    fps, speed = 60, 6.6
    seconds = intervals / fps
    distance = speed * seconds
    meta = dict(loop=True, speed_mps=speed, cycle_distance_m=distance,
                stance_fraction=stance, frames=intervals + 1, duration_seconds=seconds,
                footfall_phases=[0, .5], root_motion="IN_PLACE_GAME_OWNED",
                lift_m=.72, flight_pelvis_lift_m=.20, landing_compression_m=.10,
                source_coordinate_system="Blender Z up, forward -Y")
    report = dict(status="RUNNING", variant=args.variant, metadata=meta,
                  source_sha256=source_hashes, output=str(output))

    def save_report():
        (output / "verification.json").write_text(json.dumps(report, indent=2), encoding="utf-8")

    save_report()
    bpy.ops.wm.open_mainfile(filepath=str(source_blend))
    rig = bpy.data.objects["PursuerDollRig"]
    scene = bpy.context.scene
    scene.render.fps = fps
    meshes = [o for o in rig.children if o.type == "MESH"]
    assert len(meshes) == 9 and len(rig.data.bones) == 207
    original_other = {a.name: action_digest(a) for a in bpy.data.actions if a.name != "Golem_Run"}
    assert len(original_other) == 24, list(original_other)
    original_model = model_digest(rig, meshes)

    # Reuse only definitions, never the source file's open/save/bake top-level code.
    names = {"ease", "put", "fk", "head", "aim", "limb", "foot", "arm", "reset", "body"}
    tree = ast.parse(source_script.read_text(encoding="utf-8-sig"))
    definitions = [node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name in names]
    assert {node.name for node in definitions} == names
    context = dict(bpy=bpy, math=math, Vector=Vector, Matrix=Matrix, Quaternion=Quaternion,
                   Euler=Euler, rig=rig, scene=scene, bone=rig.data.bones,
                   poses={}, clamps=[], active="Golem_Run")
    exec(compile(ast.Module(body=definitions, type_ignores=[]), str(source_script), "exec"), context)

    old_run = bpy.data.actions.get("Golem_Run")
    rig.animation_data.action = None
    if old_run:
        bpy.data.actions.remove(old_run)
    action = bpy.data.actions.new("Golem_Run")
    action.use_fake_user = True
    rig.animation_data.action = action
    amp = distance * stance / 2

    def pose(t):
        half = (t % 1) % .5
        flight = math.sin(math.pi * (half - stance) / (.5 - stance)) if half > stance else 0
        impact = math.sin(math.pi * half / .12) ** 2 if half < .12 else 0
        context["body"](z=-.66 + .20 * flight - .10 * impact,
                        lean=22 + 3 * impact - 2 * flight,
                        yaw=7 * math.sin(t * math.tau), sway=.8 * math.sin(t * math.tau),
                        y=-.08, x=.025 * math.sin(t * math.tau))
        for side, offset, sign in (("L", 0, 1), ("R", .5, -1)):
            p = (t + offset) % 1
            if p < stance:
                y, lift, pitch, outward = -amp + distance * p, 0, 0, 0
            else:
                q = (p - stance) / (1 - stance)
                y = amp - 2 * amp * context["ease"](q)
                lift = .72 * math.sin(math.pi * q) ** 1.4
                pitch = 25 * math.sin(math.tau * q)
                outward = .10 * math.sin(math.pi * q) ** 2
            context["foot"](side, (sign * (.69 + outward), y,
                                      .33 + lift + abs(math.sin(math.radians(pitch))) * .76), pitch)
            shoulder = context["head"]("UpperArm." + side)
            swing = .82 * math.cos((t - .075) * math.tau + offset * math.tau)
            context["arm"](side, shoulder + Vector((sign * .40, swing, -1.12)), grip=.62)

    for frame in range(1, intervals + 2):
        scene.frame_set(frame)
        context["reset"]()
        pose((frame - 1) / intervals)
        for bone in rig.pose.bones:
            if any(bone.name.startswith(prefix) for prefix in ("Index", "Middle", "Ring", "Little", "Thumb")):
                continue
            for channel in ("location", "rotation_euler", "scale"):
                bone.keyframe_insert(channel, frame=frame, group=bone.name)
        for prop in ("grip_l", "grip_r"):
            rig.keyframe_insert(data_path='["' + prop + '"]', frame=frame, group="Hand grip controls")
    for fc in curves(action):
        for key in fc.keyframe_points:
            key.interpolation = "LINEAR"
    scene.frame_start, scene.frame_end = 1, intervals + 1

    contact_error, drift, scale_error, minimum = 0, 0, 0, float("inf")
    previous = {}
    first, root_first = None, None
    root_drift, flight_samples, airborne_samples = 0, 0, 0
    per_frame = []
    depsgraph = bpy.context.evaluated_depsgraph_get()
    with (output / "bone-curves-blender.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream)
        writer.writerow(["frame", "time_seconds", "bone", "parent", "px", "py", "pz", "qw", "qx", "qy", "qz", "sx", "sy", "sz"])
        for frame in range(1, intervals + 2):
            scene.frame_set(frame)
            t = (frame - 1) / intervals
            matrices = {b.name: b.matrix.copy() for b in rig.pose.bones}
            if first is None:
                first, root_first = matrices, matrices["Root"].copy()
            root_drift = max(root_drift, max(abs(matrices["Root"][a][b] - root_first[a][b]) for a in range(4) for b in range(4)))
            for bone in rig.pose.bones:
                scale_error = max(scale_error, max(abs(v - 1) for v in bone.matrix.to_scale()))
                local = bone.parent.matrix.inverted() @ bone.matrix if bone.parent else bone.matrix
                pos, rotation, scale = local.decompose()
                writer.writerow([frame, (frame - 1) / fps, bone.name, bone.parent.name if bone.parent else "",
                                 *pos, rotation.w, rotation.x, rotation.y, rotation.z, *scale])
            feet = {}
            for side, offset in (("L", 0), ("R", .5)):
                phase = (t + offset) % 1
                point = matrices["Foot." + side].translation.copy()
                if phase < stance:
                    contact_error = max(contact_error, abs(point.z - .33))
                    point.y -= distance * t
                    if side in previous and phase > previous[side][0]:
                        drift = max(drift, (point - previous[side][1]).length)
                    previous[side] = (phase, point)
                else:
                    previous.pop(side, None)
            frame_min = float("inf")
            for obj in meshes:
                evaluated = obj.evaluated_get(depsgraph)
                mesh = evaluated.to_mesh()
                low = min((obj.matrix_world @ vertex.co).z for vertex in mesh.vertices)
                evaluated.to_mesh_clear()
                frame_min = min(frame_min, low)
                if obj.name in ("Foot.L", "Foot.R"):
                    feet[obj.name] = low
            minimum = min(minimum, frame_min)
            airborne = len(feet) == 2 and min(feet.values()) > .02
            if stance + .02 < t % .5 < .48:
                flight_samples += 1
                airborne_samples += int(airborne)
            per_frame.append(dict(frame=frame, phase=t, geometry_min_z=frame_min, soles=feet, both_airborne=airborne))
    loop_error = max(abs(b.matrix[a][c] - first[b.name][a][c]) for b in rig.pose.bones for a in range(4) for c in range(4))
    other_unchanged = original_other == {a.name: action_digest(a) for a in bpy.data.actions if a.name != "Golem_Run"}
    model_unchanged = original_model == model_digest(rig, meshes)
    meta["ik_clamps"] = len(context["clamps"])
    report.update(ik_clamps=context["clamps"], contact_height_error=contact_error,
                  world_plant_drift_per_frame=drift, loop_matrix_error=loop_error,
                  maximum_scale_error=scale_error, root_matrix_drift=root_drift,
                  geometry_min_z=minimum, planned_flight_samples=flight_samples,
                  both_feet_airborne_samples=airborne_samples, other_24_actions_unchanged=other_unchanged,
                  model_rest_mesh_weights_unchanged=model_unchanged, frames=per_frame)
    passed = (not context["clamps"] and contact_error < .0001 and drift < .0001
              and loop_error < .001 and scale_error < .0001 and root_drift < .0001
              and minimum > -.035 and flight_samples > 0 and airborne_samples == flight_samples
              and other_unchanged and model_unchanged)
    (output / "animation-manifest-run.json").write_text(json.dumps(dict(fps=fps, clips={"Golem_Run": meta}), indent=2), encoding="utf-8")
    report["status"] = "SOURCE_PASS" if passed else "FAIL"
    save_report()
    print("RUN_SOURCE_VALIDATION", json.dumps({k: v for k, v in report.items() if k not in ("frames", "source_sha256")}), flush=True)
    if not passed:
        raise RuntimeError("Run validation failed; no FBX exported. Inspect verification.json")

    fbx = output / "Golem_Run_Heavy.fbx"
    bpy.ops.object.select_all(action="DESELECT")
    rig.hide_set(False)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={"ARMATURE"},
                            axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                            use_armature_deform_only=False, apply_unit_scale=True,
                            apply_scale_options="FBX_SCALE_UNITS", bake_anim=True,
                            bake_anim_use_all_bones=True, bake_anim_use_all_actions=False,
                            bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0)
    assert all(sha(Path(p)) == digest for p, digest in source_hashes.items())
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx))
    imported = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    imported_actions = list(bpy.data.actions)
    assert len(imported.data.bones) == 207 and len(imported_actions) == 1
    assert not any(o.type == "MESH" for o in bpy.data.objects)
    imported.animation_data.action = imported_actions[0]
    export_scale_error = 0
    for frame in range(1, intervals + 2):
        bpy.context.scene.frame_set(frame)
        for bone in imported.pose.bones:
            export_scale_error = max(export_scale_error, max(abs(v - 1) for v in bone.matrix.to_scale()))
    assert export_scale_error < .001, export_scale_error
    report.update(status="PASS", exported_file=str(fbx), exported_sha256=sha(fbx),
                  exported_bones=207, exported_actions=[a.name for a in imported_actions],
                  exported_meshes=0, exported_scale_error=export_scale_error,
                  source_files_unchanged=True,
                  limitations=["Unity import/contact/socket checks and in-game playback are not run here.",
                               "CSV uses Blender parent-local coordinates; do not treat it as Unity coordinates.",
                               "60fps sampling includes both exact footfall phases."])
    save_report()
    print("HEAVY_RUN_PASS", str(fbx), flush=True)


if __name__ == "__main__":
    main()
