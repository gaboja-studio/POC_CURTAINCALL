using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapePursuerRevision7Builder
    {
        public const string Model = "Assets/Resources/Models/Characters/Enemies/CircusGolemRevision7/";
        public const string Motions = "Assets/Resources/Animations/Enemies/CircusGolemRevision7/";
        public const string Prefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab";
        static readonly string[] Parts = { "Foot.L", "Foot.R", "Hand.L", "Hand.R", "Leg.L", "Leg.R", "Arm.L", "Arm.R" };
        static readonly string[] Starts = { "Foot.L", "Foot.R", "Hand.L", "Hand.R", "Thigh.L", "Thigh.R", "UpperArm.L", "UpperArm.R" };
        static readonly string[] Middles = { "Toe.L", "Toe.R", "Middle1.L", "Middle1.R", "Shin.L", "Shin.R", "Forearm.L", "Forearm.R" };
        static readonly string[] Ends = { "Toe.L", "Toe.R", "GripSocket.L", "GripSocket.R", "Foot.L", "Foot.R", "Hand.L", "Hand.R" };

        [MenuItem("Tools/CurtainCall/탈출로/최신 Revision7 전체 연결")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            ValidateSource();
            var prefab = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                Connect(prefab.GetComponentInChildren<EscapeUnderstageChase>(true));
                PrefabUtility.SaveAsPrefabAsset(prefab, Prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (string path in new[] { EscapePursuitRemodel.ScenePath, EscapeBackgroundBuilder.LabyrinthScenePath,
                EscapeMppmReviewConfig.PursuerScene }.Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path)))
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var chase in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EscapeUnderstageChase>(true)))
                        Connect(chase);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            AssetDatabase.SaveAssets();
        }

        static AnimationClip[] LoadClips() => Enum.GetNames(typeof(EscapePursuerDoll.Motion)).Select(name =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Motions + "Golem_" + (name == "Roar" ? "ChargeWarning" : name) + ".anim")).ToArray();

        static void ValidateSource()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model + "circus-golem-look.fbx");
            if (!model) throw new InvalidOperationException("먼저 Revision7 모델/25모션을 임포트하세요.");
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skins.Length != 9 || skins.SelectMany(s => s.bones).Distinct().Count() != 207)
                throw new InvalidOperationException("Revision7 9스킨/207본 계약 불일치");
            var clips = LoadClips();
            if (clips.Any(c => !c) || clips.Distinct().Count() != 25) throw new InvalidOperationException("25모션 누락");
            foreach (var clip in clips.Distinct())
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.path.Length > 0 && !model.transform.Find(binding.path))
                        throw new InvalidOperationException(clip.name + " 경로 불일치: " + binding.path);
        }

        public static void Configure(EscapePursuerDoll doll)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Model + "circus-golem-look.fbx");
            var rest = source.GetComponentsInChildren<Transform>(true).Where(t => !t.GetComponent<Renderer>()).ToArray();
            Transform Rest(string name) => rest.Single(t => t.name == name);
            doll.revision = 7; doll.clips = LoadClips(); doll.stretchIsolation = null;
            doll.bones = doll.GetComponentsInChildren<Transform>(true)
                .Where(t => t != doll.transform && !t.GetComponent<Renderer>() && !t.GetComponent<Collider>()).ToArray();
            Transform Bone(string name) => doll.bones.Single(t => t.name == name);
            doll.grip = Bone("GripSocket.R"); doll.release = Bone("ThrowSocket"); doll.wrist = Bone("Hand.R"); doll.forearm = Bone("Forearm.R");
            doll.neck = Bone("Neck"); doll.head = Bone("Head"); doll.poseRoot = Bone("Root");
            doll.poseRootRotation = Rest("Root").localRotation;
            doll.neckAim = Rest("Neck").InverseTransformDirection(source.transform.forward);
            doll.headAim = Rest("Head").InverseTransformDirection(source.transform.forward);
            doll.sleeves = Enumerable.Range(0, 6).Select(i => Bone($"Sleeve{i:00}.R")).ToArray();
            doll.folds = Enumerable.Range(0, 145).Select(i => Bone($"Fold{i:00}.R")).ToArray();
            doll.forearmRestLength = Vector3.Distance(Rest("Forearm.R").position, Rest("Hand.R").position);
            doll.foldPerpendicular = Rest("Forearm.R").InverseTransformDirection(Rest("Fold00.R").position - Rest("Forearm.R").position).normalized;
            doll.foldPanelLength = .275f;
            var manifest = JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Model + "animation-manifest.json").text);
            float fps = (float)manifest["fps"]; var entries = manifest["clips"];
            float Cue(string motion, string cue) => ((float)entries["Golem_" + motion][cue] - 1) / fps;
            doll.graspTime = Cue("GrabStretch", "grasp_frame"); doll.releaseTime = Cue("Throw", "release_frame");
            doll.impactTime = Cue("WallStrike", "impact_frame"); doll.takeoffTime = Cue("JumpLand", "takeoff_frame"); doll.landingTime = Cue("JumpLand", "landing_frame");
            doll.walkCycleDistance = (float)entries["Golem_Walk"]["cycle_distance_m"];
            doll.runCycleDistance = (float)entries["Golem_Run"]["cycle_distance_m"];
            doll.crouchCycleDistance = (float)entries["Golem_CrouchWalk"]["cycle_distance_m"];
            EscapePursuerHeavyGaitBuilder.Configure(doll);
            EscapePursuerEntranceGaitBuilder.Configure(doll.chase);
            doll.stanceBones = new[] { Bone("Hips"), Bone("Spine"), Bone("Chest") };
            var calibration = Object.Instantiate(source); calibration.SetActive(false);
            try
            {
                var joints = doll.stanceBones.Select(b => calibration.transform.Find(AnimationUtility.CalculateTransformPath(b, doll.transform))).ToArray();
                doll.clips[(int)EscapePursuerDoll.Motion.Idle].SampleAnimation(calibration, 0);
                var positions = joints.Select(t => t.localPosition).ToArray();
                var rotations = joints.Select(t => t.localRotation).ToArray();
                doll.clips[(int)EscapePursuerDoll.Motion.MaimedCrawl].SampleAnimation(calibration, 0);
                doll.crawlOffsets = joints.Select((t, i) => t.localPosition - positions[i]).ToArray();
                doll.crawlTurns = joints.Select((t, i) => t.localRotation * Quaternion.Inverse(rotations[i])).ToArray();
                doll.crawlHipHeight = calibration.transform.InverseTransformPoint(joints[0].position).y;
                var death = doll.clips[(int)EscapePursuerDoll.Motion.Death]; death.SampleAnimation(calibration, death.length);
                doll.deadHipHeight = calibration.transform.InverseTransformPoint(joints[0].position).y;
            }
            finally { Object.DestroyImmediate(calibration); }
            foreach (var skin in doll.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.sharedMaterials = skin.sharedMaterials.Select(m => AssetDatabase.LoadAssetAtPath<Material>(Model + "Materials/" + m.name + ".mat")).ToArray();
                if (skin.sharedMaterials.Any(m => !m)) throw new InvalidOperationException("Revision7 재질 누락: " + skin.name);
                skin.updateWhenOffscreen = true;
            }
            EditorUtility.SetDirty(doll);
        }

        static void Connect(EscapeUnderstageChase chase)
        {
            if (!chase) throw new InvalidOperationException("추격자 컨트롤러 누락");
            var old = chase.GetComponentInChildren<EscapePursuerDoll>(true);
            if (old && old.revision >= 7)
            {
                old.chase = chase; old.boss = chase.GetComponent<EscapePursuerBoss>();
                var linked = new SerializedObject(chase); linked.FindProperty("doll").objectReferenceValue = old;
                linked.ApplyModifiedPropertiesWithoutUndo(); Configure(old); return;
            }
            var actor = new SerializedObject(chase);
            var gait = (Animation)actor.FindProperty("gait").objectReferenceValue;
            var legacy = old ? old.legacyMeshes : gait.GetComponentsInChildren<Renderer>(true).Where(r => !(r is LineRenderer)).ToArray();
            if (!old)
                foreach (string name in new[] { "extendedForearm", "graspingHand" })
                    legacy = legacy.Concat(((Transform)actor.FindProperty(name).objectReferenceValue).GetComponentsInChildren<Renderer>(true)).ToArray();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model + "circus-golem-look.fbx"), gait.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Circus Golem Revision7";
            foreach (var animation in model.GetComponents<Animation>()) Object.DestroyImmediate(animation);
            foreach (var animator in model.GetComponents<Animator>()) Object.DestroyImmediate(animator);
            var doll = model.AddComponent<EscapePursuerDoll>(); doll.chase = chase; doll.boss = chase.GetComponent<EscapePursuerBoss>();
            doll.legacyMeshes = legacy;
            foreach (var mesh in legacy) if (mesh) mesh.enabled = false;
            Configure(doll);
            if (old) Object.DestroyImmediate(old.gameObject);
            var skins = doll.GetComponentsInChildren<SkinnedMeshRenderer>();
            Transform Bone(string name) => doll.bones.Single(t => t.name == name);
            doll.hitboxes = new BoxCollider[doll.boss ? 8 : 0]; doll.starts = new Transform[doll.hitboxes.Length];
            doll.middles = new Transform[doll.hitboxes.Length]; doll.ends = new Transform[doll.hitboxes.Length];
            if (doll.boss)
            {
                var boss = new SerializedObject(doll.boss); var parts = boss.FindProperty("parts");
                for (int i = 0; i < 8; i++)
                {
                    var slot = parts.GetArrayElementAtIndex(i); var previous = (BoxCollider)slot.FindPropertyRelative("hitbox").objectReferenceValue;
                    if (previous) previous.enabled = false;
                    var box = new GameObject("Doll cut target " + Parts[i]).AddComponent<BoxCollider>(); box.transform.SetParent(model.transform, false); box.isTrigger = true;
                    doll.hitboxes[i] = box; doll.starts[i] = Bone(Starts[i]); doll.middles[i] = Bone(Middles[i]); doll.ends[i] = Bone(Ends[i]);
                    slot.FindPropertyRelative("hitbox").objectReferenceValue = box;
                    var meshes = slot.FindPropertyRelative("meshes"); meshes.arraySize = 1; meshes.GetArrayElementAtIndex(0).objectReferenceValue = skins.Single(s => s.name == Parts[i]);
                }
                boss.ApplyModifiedPropertiesWithoutUndo();
            }
            actor.Update(); actor.FindProperty("doll").objectReferenceValue = doll; actor.ApplyModifiedPropertiesWithoutUndo();
            doll.Sample(EscapePursuerDoll.Motion.Idle, 0); doll.UpdateHitboxes();
            EditorUtility.SetDirty(doll);
        }
    }
}
