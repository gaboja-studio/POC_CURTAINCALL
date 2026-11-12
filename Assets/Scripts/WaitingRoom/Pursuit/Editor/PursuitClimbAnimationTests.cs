using System.IO;
using System.Linq;
using Newtonsoft.Json;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitClimbAnimationTests
    {
        static readonly string[] Models = { "Male_01", "Male_02", "Male_03", "Male_04", "Female_01", "Female_02", "Female_03", "Female_04", "Female_05", "Female_06" };

        [TestCaseSource(nameof(Models))]
        public void EveryCurrentModelHasBlenderClipsWithValidBindings(string model)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PursuitClimbAnimationImporter.Root + "/Prefabs/Pursuit_" + model + ".prefab");
            Assert.IsNotNull(prefab);
            var actor = prefab.GetComponent<PursuitActor>();
            Assert.GreaterOrEqual(actor.Clips.Length, 9);
            foreach (var motion in new[] { PursuitMotion.Climb, PursuitMotion.Drop })
            {
                var clip = actor.Clips[(int)motion];
                Assert.IsNotNull(clip, model + " " + motion);
                Assert.IsTrue(clip.legacy);
                Assert.AreEqual(WrapMode.ClampForever, clip.wrapMode);
                Assert.AreEqual(motion == PursuitMotion.Climb ? 1.1f : .5f, clip.length, .001f);
                CollectionAssert.Contains(AnimationUtility.GetAnimationClips(actor.Motion.gameObject), clip);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                Assert.Greater(bindings.Length, 50);
                foreach (var binding in bindings)
                {
                    Assert.IsNotEmpty(binding.path, "게임 루트 이동을 클립에 중복해서 굽지 않는다.");
                    Assert.IsNotNull(actor.Motion.transform.Find(binding.path), binding.path);
                    Assert.IsFalse(binding.propertyName.Contains("Scale"));
                }
            }
        }

        [TestCaseSource(nameof(Models))]
        public void SavedClipsReproduceBlenderBonePositions(string model)
        {
            var source = JsonConvert.DeserializeObject<PursuitClimbAnimationImporter.Export>(File.ReadAllText(PursuitClimbAnimationImporter.Folder + "/Source~/blender-clips.json"));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PursuitClimbAnimationImporter.Root + "/Prefabs/Pursuit_" + model + ".prefab");
            var go = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
            try
            {
                var actor = go.GetComponent<PursuitActor>();
                var bones = go.GetComponentInChildren<SkinnedMeshRenderer>().bones.ToDictionary(b => b.name);
                var rest = bones.ToDictionary(b => b.Key, b => b.Value.position);
                foreach (var clipData in source.clips.Where(c => c.model == "Pursuit_" + model))
                {
                    var clip = actor.Clips[clipData.motion == "Climb" ? (int)PursuitMotion.Climb : (int)PursuitMotion.Drop];
                    foreach (var sample in clipData.samples)
                    {
                        clip.SampleAnimation(actor.Motion.gameObject, sample.time);
                        foreach (var b in sample.bones)
                        {
                            var expected = rest[b.name] + new Vector3(b.position[0], b.position[1], b.position[2]);
                            Assert.Less(Vector3.Distance(expected, bones[b.name].position), .002f, model + "/" + clipData.motion + "/" + b.name + "/" + sample.time);
                        }
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        [TestCase(PursuitMotion.Climb)]
        [TestCase(PursuitMotion.Drop)]
        public void HostAndRemoteUseDurationScaledOneShotAndExitToWalk(PursuitMotion motion)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PursuitClimbAnimationImporter.Root + "/Prefabs/Pursuit_Male_02.prefab");
            var go = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
            try
            {
                var actor = go.GetComponent<PursuitActor>();
                // Initialize의 클립 별칭 등록과 같은 입력. 행동 그래프/씬 준비와 재생 검사를 분리한다.
                for (int i = 0; i < actor.Clips.Length; i++) actor.Motion.AddClip(actor.Clips[i], ((PursuitMotion)i).ToString());
                var rules = GameSettings.WaitingRoom.Pursuit;
                float duration = Mathf.Max(.1f, motion == PursuitMotion.Climb ? rules.ClimbSeconds : rules.DropSeconds);
                actor.SetMotion(motion, true);
                var state = actor.Motion[motion.ToString()];
                Assert.AreEqual(WrapMode.ClampForever, state.wrapMode);
                Assert.AreEqual(state.length / duration, state.speed, .001f);
                Assert.IsTrue(PursuitActor.IsTraversalMotion(actor.State));
                actor.SetMotion(PursuitMotion.Walk);
                actor.Receive(Vector3.zero, Quaternion.identity, motion, null, 0, Vector3.zero, false);
                Assert.AreEqual(motion, actor.State);
                Assert.AreEqual(state.length / duration, state.speed, .001f);
                actor.Receive(Vector3.zero, Quaternion.identity, PursuitMotion.Walk, null, 0, Vector3.zero, false);
                Assert.IsFalse(PursuitActor.IsTraversalMotion(actor.State));
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        [Test]
        public void SnapshotMotionNumbersRemainCompatible()
        {
            Assert.AreEqual(5, (int)PursuitMotion.Grab);
            Assert.AreEqual(6, (int)PursuitMotion.Tear);
            Assert.AreEqual(7, (int)PursuitMotion.Climb);
            Assert.AreEqual(8, (int)PursuitMotion.Drop);
            Assert.IsFalse(PursuitActor.IsTraversalMotion(PursuitMotion.Grab));
            Assert.IsTrue(PursuitActor.IsTraversalMotion(PursuitMotion.Drop));
        }
    }
}
