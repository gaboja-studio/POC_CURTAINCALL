using System;
using System.Linq;
using CurtainCall.CharacterArt;
using CurtainCall.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>Bake the player's existing humanoid crouch/death onto the mannequin's legacy rig.</summary>
    public static class PursuitLocomotionImporter
    {
        static readonly string[] Names = { "Hips", "Chest", "Head", "UpperArm_L", "Forearm_L", "Hand_L", "UpperArm_R", "Forearm_R", "Hand_R", "Thigh_L", "Shin_L", "Foot_L", "Thigh_R", "Shin_R", "Foot_R" };
        static readonly HumanBodyBones[] Human = { HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };
        public const string Output = "Assets/_ThirdParty/curtaincall/Pursuit/Resources/Animations/Pursuit";

        [MenuItem("Tools/Curtain Call/Pursuit/Import player crouch and death")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject source = null, target = null;
            try {
                source = (GameObject)PrefabUtility.InstantiatePrefab(PlayerAppearanceCatalog.Current.models[0].prefab, scene);
                target = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_01.prefab"), scene);
                var animator = source.GetComponentInChildren<Animator>();
                var controller = animator.runtimeAnimatorController as AnimatorController;
                if (controller == null || !animator.isHuman) throw new InvalidOperationException("Player humanoid controller required.");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.applyRootMotion = false;
                var sourceBones = Human.Select(b => animator.GetBoneTransform(b)).ToArray();
                var targetBones = Names.Select(n => target.GetComponentsInChildren<Transform>().First(t => t.name == n)).ToArray();
                var sourceRest = sourceBones.Select(t => t.rotation).ToArray();
                var targetRest = targetBones.Select(t => t.rotation).ToArray();
                int[] child = { -1, 2, -1, 4, 5, -1, 7, 8, -1, 10, 11, -1, 13, 14, -1 };
                var restDirections = targetBones.Select((t, i) => child[i] < 0 ? Vector3.zero : targetBones[child[i]].position - t.position).ToArray();
                var targetLocalP = targetBones.Select(t => t.localPosition).ToArray();
                var targetLocalQ = targetBones.Select(t => t.localRotation).ToArray();
                var skins = target.GetComponentsInChildren<SkinnedMeshRenderer>();
                var sampleMesh = new Mesh();
                float sourceHip = source.transform.InverseTransformPoint(sourceBones[0].position).y;
                float targetHip = target.transform.InverseTransformPoint(targetBones[0].position).y;
                string folder = "Assets";
                foreach (string part in Output.Split('/').Skip(1)) {
                    if (!AssetDatabase.IsValidFolder(folder + "/" + part)) AssetDatabase.CreateFolder(folder, part);
                    folder += "/" + part;
                }
                foreach (string state in new[] { "Crouch", "CrouchWalk", "Death" }) {
                    var stateMotion = controller.layers[0].stateMachine.states.First(s => s.state.name == state).state.motion;
                    var clip = stateMotion as AnimationClip;
                    if (clip == null) throw new InvalidOperationException(state + " must resolve to an animation clip");
                    var graph = PlayableGraph.Create("Pursuit retarget");
                    try {
                        var playable = AnimationClipPlayable.Create(graph, clip);
                        AnimationPlayableOutput.Create(graph, "Player pose", animator).SetSourcePlayable(playable);
                        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
                        var baked = new AnimationClip { name = state, legacy = true, frameRate = 30,
                            wrapMode = state == "Death" ? WrapMode.ClampForever : WrapMode.Loop };
                        var curves = targetBones.Select(_ => Enumerable.Range(0, 7).Select(__ => new AnimationCurve()).ToArray()).ToArray();
                        int frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * 30));
                        for (int f = 0; f <= frames; f++) {
                            float time = clip.length * f / frames;
                            playable.SetTime(time); graph.Evaluate(0);
                            for (int b = 0; b < targetBones.Length; b++) {
                                targetBones[b].localPosition = targetLocalP[b]; targetBones[b].localRotation = targetLocalQ[b];
                            }
                            for (int b = 0; b < targetBones.Length; b++)
                                targetBones[b].rotation = child[b] < 0
                                    ? sourceBones[b].rotation * Quaternion.Inverse(sourceRest[b]) * targetRest[b]
                                    : Quaternion.FromToRotation(restDirections[b], sourceBones[child[b]].position - sourceBones[b].position) * targetRest[b];
                            var hip = target.transform.InverseTransformPoint(targetBones[0].position);
                            hip.y = source.transform.InverseTransformPoint(sourceBones[0].position).y * targetHip / Mathf.Max(.01f, sourceHip);
                            targetBones[0].position = target.transform.TransformPoint(hip);
                            // Humanoid root translation is discarded by a legacy rig. Replant its lowest surface,
                            // including the torso during death, so neither crouching nor the corpse floats.
                            float floor = float.PositiveInfinity;
                            foreach (var skin in skins) {
                                skin.BakeMesh(sampleMesh);
                                foreach (var v in sampleMesh.vertices)
                                    floor = Mathf.Min(floor, (skin.transform.position + skin.transform.rotation * v).y);
                            }
                            if (float.IsFinite(floor)) targetBones[0].position += Vector3.up * (target.transform.position.y - floor + .015f);
                            if (state != "Death") {
                                // Fit both body sizes under the same player-sized vent. Keep the player's
                                // footfall timing, plant the feet, and deepen the knee bend instead of scaling the body.
                                float ceiling = PursuitActor.CrouchBodyHeight / 1.16f - .04f;
                                for (int fit = 0; fit < 3; fit++) {
                                    float highest = float.NegativeInfinity;
                                    foreach (var skin in skins) {
                                        skin.BakeMesh(sampleMesh);
                                        foreach (var v in sampleMesh.vertices) highest = Mathf.Max(highest, (skin.transform.position + skin.transform.rotation * v).y);
                                    }
                                    float lower = highest - ceiling;
                                    if (lower <= .001f) break;
                                    var feet = new[] { targetBones[11].position, targetBones[14].position };
                                    var rotations = new[] { targetBones[11].rotation, targetBones[14].rotation };
                                    targetBones[0].position -= Vector3.up * lower;
                                    for (int leg = 0; leg < 2; leg++) {
                                        int first = leg == 0 ? 9 : 12;
                                        PursuitClimbIk.Solve(targetBones[first], targetBones[first + 1], targetBones[first + 2], feet[leg], target.transform.forward);
                                        targetBones[first + 2].rotation = rotations[leg];
                                    }
                                }
                            }
                            for (int b = 0; b < targetBones.Length; b++) {
                                var p = targetBones[b].localPosition; var q = targetBones[b].localRotation;
                                float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                                for (int c = 0; c < 7; c++) curves[b][c].AddKey(time, values[c]);
                            }
                        }
                        string[] properties = { "localPosition.x", "localPosition.y", "localPosition.z", "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };
                        for (int b = 0; b < targetBones.Length; b++) for (int c = 0; c < 7; c++)
                            baked.SetCurve(AnimationUtility.CalculateTransformPath(targetBones[b], target.transform), typeof(Transform), properties[c], curves[b][c]);
                        baked.EnsureQuaternionContinuity();
                        string path = Output + "/" + state + ".anim";
                        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if (existing == null) AssetDatabase.CreateAsset(baked, path);
                        else { EditorUtility.CopySerialized(baked, existing); Object.DestroyImmediate(baked); EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); }
                    } finally { graph.Destroy(); }
                }
                Object.DestroyImmediate(sampleMesh);
            } finally { if (source != null) Object.DestroyImmediate(source); if (target != null) Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
