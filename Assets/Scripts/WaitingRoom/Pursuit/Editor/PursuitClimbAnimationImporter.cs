using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>Blender에서 베이크한 실제 추격자 본 포즈를 Legacy 클립으로 옮긴다. 원본 FBX/메시/씬은 바꾸지 않는다.</summary>
    public static class PursuitClimbAnimationImporter
    {
        public const string Root = "Assets/_ThirdParty/curtaincall/Pursuit";
        public const string Folder = Root + "/Climbing";
        [Serializable] public sealed class Export { public int version; public string conceptManifestSha256; public ClipData[] clips; }
        [Serializable] public sealed class ClipData { public string model, motion, action; public float duration, fps; public Sample[] samples; }
        [Serializable] public sealed class Sample { public float time; public Bone[] bones; }
        [Serializable] public sealed class Bone { public string name; public float[] rotation, position; }
        [Serializable] public sealed class AuthoredBounds { public float[] center, extents; }
        static Vector3 V(float[] v) => new(v[0], v[1], v[2]);
        static Quaternion Q(float[] q) => new(q[0], q[1], q[2], q[3]);

        [MenuItem("Tools/Curtain Call/Pursuit/Import Blender Climbing")]
        public static void Import()
        {
            var input = JsonConvert.DeserializeObject<Export>(File.ReadAllText(Folder + "/Source~/blender-clips.json"));
            if (input == null || input.version != 1 || input.clips == null || input.clips.Length != 20)
                throw new InvalidDataException("Blender 10종×2클립 내보내기가 필요합니다.");
            var manifestBytes = File.ReadAllBytes(Folder + "/Source~/Concepts/manifest.json");
            using var sha = System.Security.Cryptography.SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(manifestBytes)).Replace("-", "").ToLowerInvariant();
            if (hash != input.conceptManifestSha256) throw new InvalidDataException("선행 원화 manifest가 일치하지 않습니다.");
            if (!AssetDatabase.IsValidFolder(Folder + "/Clips")) AssetDatabase.CreateFolder(Folder, "Clips");
            var authored = JsonConvert.DeserializeObject<Dictionary<string, AuthoredBounds>>(File.ReadAllText(Folder + "/Source~/authored-bounds.json"));
            var report = new List<object>();
            foreach (var group in input.clips.GroupBy(c => c.model))
            {
                string path = Root + "/Prefabs/" + group.Key + ".prefab";
                var go = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var actor = go.GetComponent<PursuitActor>();
                    if (actor == null || actor.Motion == null) throw new InvalidDataException(path);
                    var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
                    var bones = skin.bones.ToDictionary(b => b.name);
                    var transforms = go.GetComponentsInChildren<Transform>(true);
                    var positions = transforms.Select(t => t.localPosition).ToArray();
                    var rotations = transforms.Select(t => t.localRotation).ToArray();
                    var bindRotations = bones.ToDictionary(k => k.Key, k => k.Value.rotation);
                    var bindPositions = bones.ToDictionary(k => k.Key, k => k.Value.position);
                    var oldClips = actor.Clips.Take(7).ToArray();
                    var mesh = skin.sharedMesh; var materials = skin.sharedMaterials;
                    void Restore()
                    {
                        for (int i = 0; i < transforms.Length; i++) { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; }
                    }
                    var clips = actor.Clips.ToList();
                    while (clips.Count < 9) clips.Add(null);
                    float maxBoneError = 0;
                    foreach (var source in group)
                    {
                        if (source.motion != "Climb" && source.motion != "Drop") throw new InvalidDataException(source.motion);
                        var clip = new AnimationClip { name = source.model + "_" + source.motion, legacy = true,
                            frameRate = source.fps, wrapMode = WrapMode.ClampForever };
                        var curves = new Dictionary<(string path, string property), List<Keyframe>>();
                        void Key(Transform bone, string property, float time, float value)
                        {
                            var key = (AnimationUtility.CalculateTransformPath(bone, actor.Motion.transform), property);
                            if (!curves.TryGetValue(key, out var values)) curves[key] = values = new List<Keyframe>();
                            values.Add(new Keyframe(time, value));
                        }
                        foreach (var sample in source.samples)
                        {
                            Restore();
                            // 부모→자식 순서는 Blender 원본 리그 순서. 월드 차이를 실제 Unity bind 회전에 적용한다.
                            foreach (var value in sample.bones)
                            {
                                var bone = bones[value.name];
                                bone.rotation = Q(value.rotation) * bindRotations[value.name];
                                if (value.name == "Hips") bone.position = bindPositions[value.name] + go.transform.TransformVector(V(value.position));
                            }
                            foreach (var value in sample.bones)
                            {
                                var bone = bones[value.name];
                                var expected = bindPositions[value.name] + go.transform.TransformVector(V(value.position));
                                maxBoneError = Mathf.Max(maxBoneError, Vector3.Distance(bone.position, expected));
                                var q = bone.localRotation;
                                Key(bone, "localRotation.x", sample.time, q.x); Key(bone, "localRotation.y", sample.time, q.y);
                                Key(bone, "localRotation.z", sample.time, q.z); Key(bone, "localRotation.w", sample.time, q.w);
                                if (value.name == "Hips")
                                {
                                    var p = bone.localPosition;
                                    Key(bone, "localPosition.x", sample.time, p.x); Key(bone, "localPosition.y", sample.time, p.y); Key(bone, "localPosition.z", sample.time, p.z);
                                }
                            }
                        }
                        Restore();
                        if (maxBoneError > .002f) throw new InvalidDataException(group.Key + " 본 변환 오차: " + maxBoneError);
                        foreach (var pair in curves)
                        {
                            var curve = new AnimationCurve(pair.Value.ToArray());
                            for (int i = 0; i < curve.length; i++)
                            {
                                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                            }
                            clip.SetCurve(pair.Key.path, typeof(Transform), pair.Key.property, curve);
                        }
                        clip.EnsureQuaternionContinuity();
                        string clipPath = Folder + "/Clips/" + clip.name + ".anim";
                        var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if (saved == null) { AssetDatabase.CreateAsset(clip, clipPath); saved = clip; }
                        else { EditorUtility.CopySerialized(clip, saved); UnityEngine.Object.DestroyImmediate(clip); EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved); }
                        int index = source.motion == "Climb" ? (int)PursuitMotion.Climb : (int)PursuitMotion.Drop;
                        clips[index] = saved;
                        // 동작 별칭은 Initialize가 붙인다. 에셋 참조는 아래 Editor API로 직렬화한다.
                    }
                    actor.Clips = clips.ToArray();
                    AnimationUtility.SetAnimationClips(actor.Motion, actor.Clips);
                    // updateWhenOffscreen은 읽은 localBounds도 갱신한다. 최초 prefab 직렬화 기준을 함께 보존한다.
                    var baseline = authored[group.Key];
                    var bounds = new Bounds(V(baseline.center), V(baseline.extents) * 2);
                    bounds.Encapsulate(skin.localBounds.min); bounds.Encapsulate(skin.localBounds.max);
                    var baked = new Mesh();
                    try
                    {
                        foreach (var clip in actor.Clips.Skip(7).Where(c => c != null))
                            for (int frame = 0; frame <= 33; frame++)
                            {
                                clip.SampleAnimation(actor.Motion.gameObject, clip.length * frame / 33f);
                                skin.BakeMesh(baked, true);
                                foreach (var v in baked.vertices) bounds.Encapsulate(v);
                            }
                    }
                    finally { Restore(); UnityEngine.Object.DestroyImmediate(baked); }
                    skin.localBounds = bounds;
                    if (skin.sharedMesh != mesh || !skin.sharedMaterials.SequenceEqual(materials) || !actor.Clips.Take(7).SequenceEqual(oldClips))
                        throw new InvalidOperationException("기존 외형/클립 참조가 변경되었습니다.");
                    PrefabUtility.SaveAsPrefabAsset(go, path);
                    report.Add(new { model = group.Key, clips = actor.Clips.Length, maxBoneError, meshPreserved = true, materialsPreserved = true, previousClipsPreserved = true });
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            File.WriteAllText(Folder + "/Source~/unity-import-report.json", JsonConvert.SerializeObject(report, Formatting.Indented));
            Debug.Log("Blender 오르기/내려오기 20클립을 기존 추격자 10종에 연결했습니다.");
        }
    }
}
