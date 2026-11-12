using System.Linq;
using CurtainCall.Settings;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitClimbIkTests
    {
        static readonly string[] Models = { "Male_01", "Male_02", "Male_03", "Male_04", "Female_01", "Female_02", "Female_03", "Female_04", "Female_05", "Female_06" };
        Scene scene;
        GameObject fixture, go, platform;
        readonly Vector3 origin = new(24000, 0, -24000);
        PursuitSettings rules;
        [SetUp] public void Setup()
        {
            // EditMode Runner가 준비한 임시 씬을 사용하고 이 fixture의 오브젝트만 정리한다.
            scene = SceneManager.GetActiveScene();
            fixture = new GameObject("Pursuit IK fixture");
            rules = new PursuitSettings();
            Cube("바닥", origin + new Vector3(0, -.25f, 0), new Vector3(8, .5f, 8));
            platform = Cube("검토용 소품", origin + new Vector3(0, .6f, 1.4f), new Vector3(3, 1.2f, 2));
            Physics.SyncTransforms();
        }
        GameObject Cube(string name, Vector3 p, Vector3 size)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube); c.transform.SetParent(fixture.transform); c.name = name; c.transform.position = p; c.transform.localScale = size; return c;
        }
        PursuitActor Actor(string model)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PursuitClimbAnimationImporter.Root + "/Prefabs/Pursuit_" + model + ".prefab");
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); go.transform.SetParent(fixture.transform); go.transform.position = origin;
            return go.GetComponent<PursuitActor>();
        }
        [TearDown] public void Cleanup()
        {
            if (fixture != null) Object.DestroyImmediate(fixture);
        }
        [TestCaseSource(nameof(Models))]
        public void AllModelsPlantVisibleLimbsWithoutStretchAndRestore(string model)
        {
            var actor = Actor(model); var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            var ik = new PursuitClimbIk(go.transform, skin);
            int expected = model == "Male_01" || model == "Male_03" || model == "Female_05" || model == "Female_06" ? 3 : 4;
            Assert.AreEqual(expected, ik.LimbCount, "결손된 손/발은 IK에서 제외한다.");
            var from = origin; var to = origin + new Vector3(0, 1.2f, .8f);
            ik.Begin(from, to, true, rules);
            int hands = 0, feet = 0;
            var transforms = go.GetComponentsInChildren<Transform>();
            for (int frame = 0; frame <= 60; frame++)
            {
                ik.Restore(); float t = frame / 60f;
                go.transform.position = PursuitActor.ClimbPose(from, to, true, t);
                actor.Clips[7].SampleAnimation(actor.Motion.gameObject, t * actor.Clips[7].length);
                var positions = transforms.Select(b => b.localPosition).ToArray();
                var rotations = transforms.Select(b => b.localRotation).ToArray();
                var scales = transforms.Select(b => b.localScale).ToArray();
                ik.Apply(t, rules);
                hands += ik.PlantedHands; feet += ik.PlantedFeet;
                Assert.Less(ik.MaxContactError, .01f, model + " 접점 오차 " + t);
                for (int b = 0; b < transforms.Length; b++)
                {
                    Assert.AreEqual(positions[b], transforms[b].localPosition, "본 길이/루트 이동 금지");
                    Assert.AreEqual(scales[b], transforms[b].localScale);
                    Assert.IsTrue(float.IsFinite(transforms[b].rotation.x));
                }
                ik.Restore();
                for (int b = 0; b < transforms.Length; b++) Assert.Less(Quaternion.Angle(rotations[b], transforms[b].localRotation), .1f);
            }
            Assert.Greater(hands, 0, model + " 손 완전 고정 구간 없음");
            Assert.Greater(feet, 0, model + " 발 완전 고정 구간 없음");
            ik.Clear(); Assert.AreEqual(0, ik.ActiveContacts);
        }
        [TestCaseSource(nameof(Models))]
        public void AllModelsPlantFeetOnDropAndRelease(string model)
        {
            var actor = Actor(model); var ik = new PursuitClimbIk(go.transform, go.GetComponentInChildren<SkinnedMeshRenderer>());
            var from = origin + new Vector3(0, 1.2f, .8f); var to = origin;
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(Vector3.back);
            ik.Begin(from, to, false, rules); int feet = 0;
            for (int f = 0; f <= 60; f++) {
                ik.Restore(); float t = f / 60f;
                go.transform.position = PursuitActor.ClimbPose(from, to, false, t);
                actor.Clips[8].SampleAnimation(actor.Motion.gameObject, t * actor.Clips[8].length);
                ik.Apply(t, rules);
                if (t > .8f) feet += ik.PlantedFeet;
                Assert.Less(ik.MaxContactError, .01f, model + " 내려오기 접점 " + t);
            }
            Assert.Greater(feet, 0, model + " 착지 발 고정 없음");
            ik.Clear(); Assert.AreEqual(0, ik.ActiveContacts);
        }
        [Test] public void LostSupportAndUnreachableSurfaceReleaseHands()
        {
            var actor = Actor("Male_02"); var ik = new PursuitClimbIk(go.transform, go.GetComponentInChildren<SkinnedMeshRenderer>());
            var to = origin + new Vector3(0, 1.2f, .8f);
            ik.Begin(origin, to, true, rules);
            go.transform.position = PursuitActor.ClimbPose(origin, to, true, .15f);
            actor.Clips[7].SampleAnimation(actor.Motion.gameObject, .15f * actor.Clips[7].length);
            ik.Apply(.15f, rules); Assert.Greater(ik.PlantedHands, 0);
            ik.Restore(); var poses = go.GetComponentsInChildren<Transform>().Select(b => b.localRotation).ToArray();
            platform.SetActive(false); ik.Apply(.15f, rules); Assert.AreEqual(0, ik.PlantedHands);
            Object.DestroyImmediate(platform); ik.Apply(.15f, rules); Assert.AreEqual(0, ik.PlantedHands);
            ik.Clear();
            var bones = go.GetComponentsInChildren<Transform>();
            for (int i = 0; i < bones.Length; i++) Assert.Less(Quaternion.Angle(poses[i], bones[i].localRotation), .1f);
            platform = Cube("닿지 않는 소품", origin + new Vector3(0, .6f, 1.4f), new Vector3(3, 1.2f, 2));
            Physics.SyncTransforms(); ik.Begin(origin, to, true, rules);
            go.transform.position += Vector3.down * 5;
            ik.Apply(.15f, rules); Assert.AreEqual(0, ik.PlantedHands); ik.Clear();
        }
        [TestCase(.6f)] [TestCase(1.5f)]
        public void HeightVariantsKeepReachAndRelease(float height)
        {
            platform.transform.position = origin + new Vector3(0, height / 2, 1.4f);
            platform.transform.localScale = new Vector3(3, height, 2); Physics.SyncTransforms();
            var actor = Actor("Male_02"); var ik = new PursuitClimbIk(go.transform, go.GetComponentInChildren<SkinnedMeshRenderer>());
            var from = origin; var to = origin + new Vector3(0, height, .8f);
            ik.Begin(from, to, true, rules); int contacts = 0;
            for (int f = 0; f <= 60; f++) {
                ik.Restore(); float t = f / 60f; go.transform.position = PursuitActor.ClimbPose(from,to,true,t);
                actor.Clips[7].SampleAnimation(actor.Motion.gameObject, t * actor.Clips[7].length); ik.Apply(t,rules);
                contacts += ik.PlantedHands + ik.PlantedFeet; Assert.Less(ik.MaxContactError,.01f);
            }
            Assert.Greater(contacts,0); Assert.AreEqual(0,ik.ActiveContacts); ik.Clear();
        }
        [Test] public void DisabledOrMissingSurfacesNeverInventContacts()
        {
            var actor = Actor("Male_02"); var ik = new PursuitClimbIk(go.transform, go.GetComponentInChildren<SkinnedMeshRenderer>());
            rules.ClimbIkSurfaceMask = 0; ik.Begin(origin,origin+new Vector3(0,1.2f,.8f),true,rules); ik.Apply(.2f,rules);
            Assert.AreEqual(0,ik.ActiveContacts);
            rules.ClimbIkSurfaceMask = Physics.DefaultRaycastLayers; ik.Begin(origin,origin+new Vector3(0,1.2f,.8f),true,rules);
            rules.ClimbContactIk = false; ik.Apply(.2f,rules); Assert.AreEqual(0,ik.ActiveContacts);
        }
        [TestCase(PursuitMotion.Climb, 0f)] [TestCase(PursuitMotion.Drop, 1000f)]
        public void SnapshotCarriesTraversalAndRemotePhase(PursuitMotion motion, float x)
        {
            var actor = Actor("Male_02");
            for(int i=0;i<actor.Clips.Length;i++) actor.Motion.AddClip(actor.Clips[i],((PursuitMotion)i).ToString());
            var a = new PursuitDirector.ActorState { motion=(int)motion, rotation=Quaternion.identity,
                climbFrom=new Vector3(x,0,0), climbTo=new Vector3(x,1.5f,.8f), climbProgress=.4f, climbDuration=1.3f };
            var s = new PursuitDirector.Snapshot { actors=new[]{a} };
            using var writer = new FastBufferWriter(PursuitSnapshotCodec.MaxSize(s),Allocator.Temp);
            PursuitSnapshotCodec.Write(writer,s);
            using var reader = new FastBufferReader(writer,Allocator.Temp);
            var result = PursuitSnapshotCodec.Read(reader); Assert.IsNotNull(result);
            var b = result.actors[0]; Assert.Less(Vector3.Distance(a.climbTo,b.climbTo),.011f);
            Assert.AreEqual(a.climbDuration,b.climbDuration); Assert.AreEqual(a.climbProgress,b.climbProgress);
            actor.Receive(Vector3.zero,Quaternion.identity,motion,null,0,Vector3.zero,false);
            actor.ReceiveTraversal(b.climbFrom,b.climbTo,b.climbProgress,b.climbDuration);
            Assert.AreEqual(.4f,actor.TraversalProgress,.01f); Assert.AreEqual(.4f,actor.Motion[motion.ToString()].normalizedTime,.01f);
            actor.Receive(Vector3.zero,Quaternion.identity,PursuitMotion.Walk,null,0,Vector3.zero,false);
            Assert.AreEqual(0,actor.ClimbIkContacts);
        }
    }
}
