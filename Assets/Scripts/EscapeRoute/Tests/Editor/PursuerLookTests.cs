using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class PursuerLookTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Folder = "Assets/Resources/Models/Characters/Enemies/CircusGolemRevision7/";
        const string Prefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab";
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
        static object Get(object o, string field) => o.GetType().GetField(field, Flags).GetValue(o);
        static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Flags).Invoke(o, args);
        GameObject model;
        Component doll;
        Transform neck, head;
        SkinnedMeshRenderer body;
        Transform Bone(string name) => model.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        Vector3 Aim => head.TransformDirection((Vector3)Get(doll, "headAim"));
        void Sample(string name, float time = .25f) => Call(doll, "Sample", Enum.Parse(doll.GetType().GetNestedType("Motion"), name), time);
        void Look(Vector3? point, float dt = 1f / 60) => Call(doll, "ApplyLook", point, dt);

        [SetUp] public void Setup()
        {
            model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "circus-golem-look.fbx"));
            model.SetActive(false);
            doll = model.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapePursuerDoll"));
            var isolation = doll.GetType().GetMethod("PrepareRig").Invoke(null, new object[] { model.transform });
            Set(doll, "stretchIsolation", isolation);
            Set(doll, "clips", Enum.GetNames(doll.GetType().GetNestedType("Motion")).Select(n =>
                AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/Animations/Enemies/CircusGolemRevision7/Golem_" + (n == "Roar" ? "ChargeWarning" : n) + ".anim")).ToArray());
            neck = Bone("Neck"); head = Bone("Head");
            Set(doll, "neck", neck); Set(doll, "head", head);
            Set(doll, "neckAim", neck.InverseTransformDirection(model.transform.forward));
            Set(doll, "headAim", head.InverseTransformDirection(model.transform.forward));
            body = model.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s => s.name == "Body");
            Assert.That(body.sharedMesh, Is.Not.Null, "Import the latest look FBX first.");
        }

        [TearDown] public void Cleanup() => Object.DestroyImmediate(model);

        [TestCase("Idle")][TestCase("Walk")][TestCase("Run")][TestCase("CrouchWalk")]
        [TestCase("GrabStretch")][TestCase("Throw")][TestCase("WallStrike")][TestCase("JumpLand")][TestCase("Roar")]
        public void HeadTracksAfterEveryAnimationSampleWithoutMovingTheBodyOrHand(string motion)
        {
            model.transform.SetPositionAndRotation(new Vector3(60, 4, -30), Quaternion.Euler(0, 113, 0));
            Vector3 point = model.transform.TransformPoint(new Vector3(3, 1.2f, 9));
            for (int frame = 0; frame < 120; frame++)
            {
                Sample(motion, .15f + frame / 240f);
                var chest = Bone("Chest").rotation; var hand = Bone("GripSocket.R").position;
                Look(point);
                Assert.That(Quaternion.Angle(chest, Bone("Chest").rotation), Is.LessThan(.05f));
                Assert.That(Vector3.Distance(hand, Bone("GripSocket.R").position), Is.LessThan(.0001f));
            }
            Assert.That(Vector3.Angle(Aim, point - head.position), Is.LessThan(2f));
        }

        [Test] public void TargetChangesSmoothlyAndLostTargetRestoresAuthoredPoseWithoutAccumulation()
        {
            Sample("Walk"); var point = head.position + new Vector3(4, -1, 6);
            for (int i = 0; i < 90; i++) { Sample("Walk"); Look(point); }
            var right = Aim;
            point = head.position + new Vector3(-4, 1, 6);
            Sample("Walk"); Look(point);
            Assert.That(Vector3.Angle(right, Aim), Is.LessThan(20), "No snap on target switch.");
            for (int i = 0; i < 90; i++) { Sample("Walk"); Look(point); }
            Assert.That(Aim.x, Is.LessThan(-.4f));
            for (int i = 0; i < 90; i++) Look(null);
            var releasedNeck = neck.localRotation; var releasedHead = head.localRotation;
            Sample("Walk");
            Assert.That(Quaternion.Angle(releasedNeck, neck.localRotation), Is.LessThan(.05f));
            Assert.That(Quaternion.Angle(releasedHead, head.localRotation), Is.LessThan(.05f));
        }

        [TestCase(8, 2, -8)][TestCase(-8, 20, -8)][TestCase(0, -20, 1)]
        public void BehindAndExtremeHeightTargetsRespectAnatomicalLimits(float x, float y, float z)
        {
            Sample("Idle"); var point = head.position + new Vector3(x, y, z);
            for (int i = 0; i < 120; i++) { Sample("Idle"); Look(point); }
            var local = model.transform.InverseTransformDirection(Aim);
            Assert.That(Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg), Is.LessThanOrEqualTo(80.1f));
            float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            Assert.That(pitch, Is.InRange(-50.1f, 65.1f));
            Assert.That(float.IsFinite(Quaternion.Dot(head.rotation, head.rotation)), Is.True);
        }

        [Test] public void LatestSkinTurnsTheBlackFaceRigidly()
        {
            int material = Array.FindIndex(body.sharedMaterials, m => m.name.Contains("Face recess"));
            var face = body.sharedMesh.GetTriangles(material).Distinct().Where((_, i) => i % 40 == 0).ToArray();
            int headIndex = Array.FindIndex(body.bones, b => b == head);
            foreach (int i in face)
            {
                Assert.That(body.sharedMesh.boneWeights[i].boneIndex0, Is.EqualTo(headIndex));
                Assert.That(body.sharedMesh.boneWeights[i].weight0, Is.EqualTo(1).Within(.00001f));
            }
            var baked = new Mesh();
            try
            {
                Sample("Idle"); body.BakeMesh(baked, true); var before = baked.vertices;
                var point = head.position + new Vector3(5, -1, 5);
                for (int i = 0; i < 120; i++) { Sample("Idle"); Look(point); }
                body.BakeMesh(baked, true); var after = baked.vertices;
                Assert.That(face.Average(i => Vector3.Distance(before[i], after[i])), Is.GreaterThan(.1f));
                foreach (int i in face)
                    Assert.That(Vector3.Distance(after[face[0]], after[i]), Is.EqualTo(Vector3.Distance(before[face[0]], before[i])).Within(.002f));
            }
            finally { Object.DestroyImmediate(baked); }
        }

        [Test] public void CommonPrefabContainsCalibratedNeckAndUpdatedSkin()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var component = prefab.GetComponentInChildren(doll.GetType(), true);
            Assert.That(Get(component, "head"), Is.Not.Null); Assert.That(Get(component, "neck"), Is.Not.Null);
            Assert.That(((Vector3)Get(component, "headAim")).magnitude, Is.EqualTo(1).Within(.001f));
            Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "Body").sharedMesh, Is.EqualTo(body.sharedMesh));
        }

        [Test] public void LiveSelectedPlayerFollowsMotionAndBodyHeightAndRejectsDisconnectedActors()
        {
            var root = new GameObject("Look target test"); root.SetActive(false);
            var actor = new GameObject("Remote selected player"); actor.SetActive(false);
            var playerType = TypeOf("CurtainCall.Network.PlayerSync.NetworkPlayer");
            var players = (IList)playerType.GetField("all", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Component player = null;
            try
            {
                var chase = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapeUnderstageChase"));
                player = actor.AddComponent(playerType);
                playerType.GetProperty("OwnerClientId").SetValue(player, 72UL);
                playerType.GetProperty("IsSpawned").SetValue(player, true);
                players.Add(player);
                var moverType = TypeOf("CurtainCall.Player.PlayerMover");
                var mover = actor.GetComponent(moverType) ?? actor.AddComponent(moverType);
                var controller = actor.GetComponent<CharacterController>();
                Set(mover, "controller", controller);
                controller.height = 1.7f; actor.transform.position = new Vector3(3, 4, 5);
                var args = new object[] { 72UL, Vector3.zero };
                Assert.That(Call(chase, "LookPoint", args), Is.True);
                Assert.That((Vector3)args[1], Is.EqualTo(actor.transform.position + Vector3.up * (1.7f * .85f)));
                controller.height = .8f; actor.transform.position += new Vector3(-6, 3, 1);
                Assert.That(Call(chase, "LookPoint", args), Is.True);
                Assert.That(Vector3.Distance((Vector3)args[1], actor.transform.position + Vector3.up * (.8f * .85f)), Is.LessThan(.001f));
                playerType.GetProperty("IsSpawned").SetValue(player, false);
                Assert.That(Call(chase, "LookPoint", args), Is.False);
            }
            finally { if (player) players.Remove(player); Object.DestroyImmediate(actor); Object.DestroyImmediate(root); }
        }

        [Test] public void CommittedAttackWinsOverThreatAndDeathClearsTheTarget()
        {
            var root = new GameObject("Look authority test"); root.SetActive(false);
            try
            {
                var chase = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapeUnderstageChase"));
                ulong Actor() => (ulong)chase.GetType().GetProperty("HostLookActor", Flags).GetValue(chase);
                var threat = Get(chase, "threat");
                Set(threat, "Phase", Enum.Parse(threat.GetType().GetField("Phase").FieldType, "DebrisWarning"));
                Set(threat, "Actor", 17UL); Set(chase, "threat", threat);
                Assert.That(Actor(), Is.EqualTo(17UL));
                var attack = Get(chase, "attack");
                Set(attack, "Phase", Enum.Parse(attack.GetType().GetField("Phase").FieldType, "Grab"));
                Set(attack, "Actor", 24UL); Set(chase, "attack", attack);
                Assert.That(Actor(), Is.EqualTo(24UL));
                var boss = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapePursuerBoss")); Set(chase, "boss", boss);
                var state = Get(boss, "state");
                Set(state, "Phase", Enum.Parse(state.GetType().GetField("Phase").FieldType, "Dying")); Set(boss, "state", state);
                Assert.That(Actor(), Is.EqualTo(ulong.MaxValue));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void StaleOrOtherRoundSnapshotCannotReplaceTheRemoteLookTarget()
        {
            var root = new GameObject("Look snapshot test"); root.SetActive(false);
            try
            {
                var chase = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapeUnderstageChase"));
                var state = Activator.CreateInstance(chase.GetType().GetNestedType("Snapshot", BindingFlags.NonPublic));
                Set(state, "Round", 100d); Set(state, "Time", 103d); Set(state, "Rotation", Quaternion.identity); Set(state, "LookActor", 72UL);
                Assert.That(Call(chase, "Accept", state, 100d), Is.True);
                Set(state, "Time", 102d); Set(state, "LookActor", 41UL);
                Assert.That(Call(chase, "Accept", state, 100d), Is.False);
                Set(state, "Time", 104d); Set(state, "Round", 101d);
                Assert.That(Call(chase, "Accept", state, 100d), Is.False);
                Assert.That(Get(Get(chase, "latest"), "LookActor"), Is.EqualTo(72UL));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
