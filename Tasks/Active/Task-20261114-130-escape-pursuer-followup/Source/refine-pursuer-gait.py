"""Derive only Walk/Run from the unchanged Revision7 rig; never write source assets.

Run with Blender 5.3 background --threads 2 --python THIS_FILE -- --output NEW_DIR.
NEW_DIR must be inside .harness/local/pursuer-space-20261116/gait/.
The animation-only FBXs, Blender parent-local CSVs, manifest, measurements and
four small pose renders are isolated evidence, not a Unity asset import.
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
OUTPUT_ROOT = (WORKSPACE / ".harness/local/pursuer-space-20261116/gait").resolve()
SOURCE_TASK = Path("C:/Unity/github-worktrees/POC_CURTAINCALL/pursuer-doll-model/Tasks/Active/Task-20261114-130-pursuer-doll-model")


def definitions(path, names, context):
    tree = ast.parse(path.read_text(encoding="utf-8-sig"))
    nodes = [n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name in names]
    assert {n.name for n in nodes} == set(names)
    exec(compile(ast.Module(body=nodes, type_ignores=[]), str(path), "exec"), context)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--run-hip", type=float, default=-.48)
    parser.add_argument("--run-stance", type=float, default=.30)
    parser.add_argument("--no-render", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    output = Path(args.output).resolve()
    if output.exists() or output == OUTPUT_ROOT or not output.is_relative_to(OUTPUT_ROOT):
        raise RuntimeError("Output must be a NEW child of " + str(OUTPUT_ROOT))
    original = SOURCE_TASK / "Source/animate-original.py"
    source_blend = SOURCE_TASK / "Model/Revision7/animated.blend"
    previous_script = Path(__file__).with_name("heavy-pursuer-run.py")
    utility = dict(hashlib=hashlib, array=array, struct=struct)
    definitions(previous_script, {"sha", "curves", "action_digest", "model_digest"}, utility)
    sha, curves = utility["sha"], utility["curves"]
    action_digest, model_digest = utility["action_digest"], utility["model_digest"]
    source_hashes = {str(p): sha(p) for p in (original, source_blend, previous_script)}
    output.mkdir(parents=True)
    report = dict(status="RUNNING", source_sha256=source_hashes, output=str(output), clips={})

    def save():
        (output / "verification.json").write_text(json.dumps(report, indent=2), encoding="utf-8")

    save()
    bpy.ops.wm.open_mainfile(filepath=str(source_blend))
    rig, scene = bpy.data.objects["PursuerDollRig"], bpy.context.scene
    scene.render.fps = 60
    meshes = [o for o in rig.children if o.type == "MESH"]
    assert len(meshes) == 9 and len(rig.data.bones) == 207
    excluded = {"Golem_Walk", "Golem_Run"}
    other_actions = {a.name: action_digest(a) for a in bpy.data.actions if a.name not in excluded}
    assert len(other_actions) == 23
    model_before = model_digest(rig, meshes)
    ctx = dict(bpy=bpy, math=math, Vector=Vector, Matrix=Matrix, Quaternion=Quaternion,
               Euler=Euler, rig=rig, scene=scene, bone=rig.data.bones, poses={}, clamps=[], active="")
    definitions(original, {"ease", "put", "fk", "head", "aim", "limb", "foot", "arm", "reset", "body"}, ctx)
    specs = {
        "Walk": dict(intervals=72, speed=2.7, stance=.58, hip=-.33, lift=.22, lean=7, arm=.38),
        "Run": dict(intervals=68, speed=6.6, stance=args.run_stance, hip=args.run_hip, lift=.50, lean=14, arm=.72),
    }
    manifest = dict(fps=60, clips={}, movement="In-place; phase = actual travelled distance / selected gait cycle distance.")
    capture = {}

    def author_pose(t, spec, running):
        half = (t % 1) % .5
        stance, distance = spec["stance"], spec["speed"] * spec["intervals"] / 60
        flight = math.sin(math.pi * (half - stance) / (.5 - stance)) if running and half > stance else 0
        compression = math.sin(math.pi * half / .12) ** 2 if half < .12 else 0
        height = spec["hip"] + (.07 * flight if running else .018 * math.cos(t * math.tau * 2))
        height -= (.045 if running else .022) * compression
        ctx["body"](z=height, lean=spec["lean"] + 2 * compression - flight,
                    yaw=(4.5 if running else 3.5) * math.sin(t * math.tau),
                    sway=.65 * math.sin(t * math.tau), y=-.035 if running else 0,
                    x=(.045 if running else .04) * math.sin(t * math.tau))
        amp = distance * stance / 2
        for side, offset, sign in (("L", 0, 1), ("R", .5, -1)):
            p = (t + offset) % 1
            if p < stance:
                y, up, pitch, outward = -amp + distance * p, 0, 0, 0
            else:
                q = (p - stance) / (1 - stance)
                # Retain some backward velocity at toe-off/landing, rather than
                # freezing the local foot at both ends of a smoothstep swing.
                tangent = distance * (1 - stance) * .45
                y = (2*q**3-3*q*q+1)*amp + (q**3-2*q*q+q)*tangent
                y += (-2*q**3+3*q*q)*(-amp) + (q**3-q*q)*tangent
                up = spec["lift"] * math.sin(math.pi*q)**1.35
                pitch = (18 if running else 10) * math.sin(math.tau*q)
                outward = (.045 if running else .025) * math.sin(math.pi*q)**2
            ctx["foot"](side, (sign*(.69+outward), y, .33+up+abs(math.sin(math.radians(pitch)))*.76), pitch)
            shoulder = ctx["head"]("UpperArm."+side)
            swing = spec["arm"] * math.cos((t-.065)*math.tau+offset*math.tau)
            ctx["arm"](side, shoulder+Vector((sign*.40, swing, -1.30 if running else -1.46)), grip=.44 if running else .25)

    for name, spec in specs.items():
        fullname, intervals = "Golem_"+name, spec["intervals"]
        rig.animation_data.action = None
        old = bpy.data.actions.get(fullname)
        if old:
            bpy.data.actions.remove(old)
        action = bpy.data.actions.new(fullname)
        action.use_fake_user = True
        rig.animation_data.action = action
        ctx["active"], ctx["clamps"] = fullname, []
        for frame in range(1, intervals+2):
            scene.frame_set(frame)
            ctx["reset"]()
            author_pose((frame-1)/intervals, spec, name == "Run")
            for bone in rig.pose.bones:
                if any(bone.name.startswith(p) for p in ("Index", "Middle", "Ring", "Little", "Thumb")):
                    continue
                for channel in ("location", "rotation_euler", "scale"):
                    bone.keyframe_insert(channel, frame=frame, group=bone.name)
            for prop in ("grip_l", "grip_r"):
                rig.keyframe_insert(data_path='["'+prop+'"]', frame=frame, group="Hand grip controls")
        for fc in curves(action):
            for key in fc.keyframe_points:
                key.interpolation = "LINEAR"
        scene.frame_start, scene.frame_end = 1, intervals+1
        distance, previous, first, quaternions = spec["speed"]*intervals/60, {}, None, {}
        drift = contact_error = scale_error = root_error = 0
        minimum, frames, hip_heights, active_flight, clear_flight = float("inf"), [], [], 0, 0
        depsgraph = bpy.context.evaluated_depsgraph_get()
        capture[name] = {}
        with (output/(fullname+"-curves-blender.csv")).open("w", newline="", encoding="utf-8") as stream:
            writer = csv.writer(stream)
            writer.writerow(["frame", "time_seconds", "bone", "parent", "px", "py", "pz", "qw", "qx", "qy", "qz", "sx", "sy", "sz"])
            for frame in range(1, intervals+2):
                scene.frame_set(frame)
                phase = (frame-1)/intervals
                matrices = {b.name:b.matrix.copy() for b in rig.pose.bones}
                if first is None:
                    first = matrices
                root_error = max(root_error, max(abs(matrices["Root"][i][j]-first["Root"][i][j]) for i in range(4) for j in range(4)))
                hip_heights.append(matrices["Hips"].translation.z)
                for bone in rig.pose.bones:
                    scale_error = max(scale_error, max(abs(s-1) for s in bone.matrix.to_scale()))
                    local = bone.parent.matrix.inverted()@bone.matrix if bone.parent else bone.matrix
                    pos, rot, scale = local.decompose()
                    if bone.name in quaternions and rot.dot(quaternions[bone.name]) < 0:
                        rot.negate()
                    quaternions[bone.name] = rot.copy()
                    writer.writerow([frame, (frame-1)/60, bone.name, bone.parent.name if bone.parent else "", *pos, rot.w, rot.x, rot.y, rot.z, *scale])
                for side, offset in (("L",0),("R",.5)):
                    p = (phase+offset)%1
                    point = matrices["Foot."+side].translation.copy()
                    if p < spec["stance"]:
                        contact_error = max(contact_error, abs(point.z-.33))
                        point.y -= distance*phase
                        if side in previous and p > previous[side][0]:
                            drift = max(drift, (point-previous[side][1]).length)
                        previous[side] = (p, point)
                    else:
                        previous.pop(side, None)
                feet, low = {}, float("inf")
                for obj in meshes:
                    evaluated = obj.evaluated_get(depsgraph)
                    mesh = evaluated.to_mesh()
                    lowest = min((obj.matrix_world@v.co).z for v in mesh.vertices)
                    evaluated.to_mesh_clear()
                    low = min(low, lowest)
                    if obj.name in ("Foot.L", "Foot.R"):
                        feet[obj.name] = lowest
                minimum = min(minimum, low)
                if name == "Run" and spec["stance"]+.02 < phase%.5 < .48:
                    active_flight += 1
                    clear_flight += int(len(feet)==2 and min(feet.values())>.02)
                frames.append(dict(frame=frame, phase=phase, min_z=low, soles=feet, hip_z=hip_heights[-1]))
                capture[name][round(phase,6)] = {b:list(matrices[b].translation) for b in ("Hips","Foot.L","Foot.R")}
        loop = max(abs(b.matrix[i][j]-first[b.name][i][j]) for b in rig.pose.bones for i in range(4) for j in range(4))
        passed = (not ctx["clamps"] and drift<.0001 and contact_error<.0001 and scale_error<.0001
                  and root_error<.0001 and loop<.001 and minimum>-.035 and active_flight==clear_flight)
        report["clips"][fullname] = dict(status="PASS" if passed else "FAIL", ik_clamps=ctx["clamps"],
            world_plant_drift_per_frame=drift, contact_height_error=contact_error, scale_error=scale_error,
            root_error=root_error, loop_error=loop, min_geometry_z=minimum, hip_height_range=[min(hip_heights),max(hip_heights)],
            planned_flight_samples=active_flight, clear_flight_samples=clear_flight, frames=frames)
        manifest["clips"][fullname] = dict(loop=True, frames=intervals+1, duration_seconds=intervals/60,
            speed_mps=spec["speed"], cycle_distance_m=distance, stance_fraction=spec["stance"], ik_clamps=len(ctx["clamps"]),
            footfall_phases=[0,.5], root_motion="IN_PLACE_GAME_OWNED", authoring=spec)
        save()
        print("GAIT_VALIDATION", fullname, json.dumps({k:v for k,v in report["clips"][fullname].items() if k!="frames"}), flush=True)

    report["other_23_actions_unchanged"] = other_actions == {a.name:action_digest(a) for a in bpy.data.actions if a.name not in excluded}
    report["model_rest_mesh_weights_unchanged"] = model_before == model_digest(rig,meshes)
    (output/"animation-manifest-gait.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")
    if not all(c["status"]=="PASS" for c in report["clips"].values()) or not report["other_23_actions_unchanged"] or not report["model_rest_mesh_weights_unchanged"]:
        report["status"]="FAIL"; save()
        raise RuntimeError("Gait validation failed; FBX files not exported")

    # Quantify the remaining immediate clip switch, independently of import.
    switches=[]
    for i in range(41):
        phase=i/40; points={}
        for name,spec in specs.items():
            rig.animation_data.action=bpy.data.actions["Golem_"+name]
            scene.frame_set(1+int(phase*spec["intervals"]),subframe=phase*spec["intervals"]%1)
            points[name]={b:rig.pose.bones[b].matrix.translation.copy() for b in ("Hips","Foot.L","Foot.R")}
        switches.append(dict(phase=phase,**{b:(points["Walk"][b]-points["Run"][b]).length for b in points["Walk"]}))
    report["immediate_switch_pose_deltas_m"]=switches
    report["fbx"]={}
    for name,spec in specs.items():
        rig.animation_data.action=bpy.data.actions["Golem_"+name]
        scene.frame_start,scene.frame_end=1,spec["intervals"]+1
        scene.frame_set(1)
        bpy.ops.object.select_all(action="DESELECT");rig.hide_set(False);rig.select_set(True)
        bpy.context.view_layer.objects.active=rig
        file=output/("Golem_"+name+"_Refined.fbx")
        bpy.ops.export_scene.fbx(filepath=str(file),use_selection=True,object_types={"ARMATURE"},
            axis_forward="-Z",axis_up="Y",add_leaf_bones=False,use_armature_deform_only=False,
            apply_unit_scale=True,apply_scale_options="FBX_SCALE_UNITS",bake_anim=True,
            bake_anim_use_all_bones=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
        report["fbx"][name]=dict(file=str(file),sha256=sha(file))

    if not args.no_render:
        scene.render.engine="BLENDER_WORKBENCH"
        scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
        scene.render.image_settings.file_format="PNG"
        scene.display.shading.light="STUDIO";scene.display.shading.color_type="SINGLE"
        scene.display.shading.single_color=(.58,.53,.46)
        scene.display.shading.background_type="WORLD"
        scene.world.color=(.12,.14,.17)
        scene.display.shading.show_shadows=True
        camera=scene.camera
        camera.data.type="ORTHO";camera.data.ortho_scale=6.0
        for obj in scene.objects:
            if obj.type=="MESH":obj.hide_render=obj not in meshes
        renders=[]
        for name,phase,view in (("Walk",.12,"side"),("Run",.08,"side"),("Run",.40,"side"),("Run",.18,"three-quarter")):
            rig.animation_data.action=bpy.data.actions["Golem_"+name]
            frame=phase*specs[name]["intervals"]
            scene.frame_set(1+int(frame),subframe=frame%1)
            camera.location=(8,0,3.0) if view=="side" else (7,-10,5)
            camera.rotation_euler=(Vector((0,0,2.2))-camera.location).to_track_quat("-Z","Y").to_euler()
            file=output/(name.lower()+"-"+str(round(phase*100))+"-"+view+".png")
            scene.render.filepath=str(file)
            bpy.ops.render.render(write_still=True)
            renders.append(str(file))
        report["renders"]=renders

    assert all(sha(Path(p))==digest for p,digest in source_hashes.items())
    # Verify the actual files contain one action and 207 bones, no replacement mesh.
    for name,item in report["fbx"].items():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=item["file"])
        imported=next(o for o in bpy.data.objects if o.type=="ARMATURE")
        assert len(imported.data.bones)==207 and len(bpy.data.actions)==1
        assert not any(o.type=="MESH" for o in bpy.data.objects)
        item.update(bones=207,actions=[a.name for a in bpy.data.actions],meshes=0)
    report.update(status="PASS",source_files_unchanged=True,
        limitations=["Unity import/contact and runtime transition checks remain for the root agent.",
                     "Numerical checks and selected pose renders do not prove natural motion across a whole cycle.",
                     "Immediate Walk/Run switch deltas are measured separately; full-body mixing is not a contact constraint."])
    save()
    print("REFINED_GAIT_PASS",str(output),flush=True)


if __name__=="__main__":
    main()
