using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("PursuerHeavyPresentation")]
    public sealed class PursuerRevision7Tests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Folder = "Assets/Resources/Models/Characters/Enemies/CircusGolemRevision7/";
        const string Prefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab";
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute." + name)).First(t => t != null);
        static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
        static object Get(object o, string field) => o.GetType().GetField(field, Flags).GetValue(o);
        static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Flags).Invoke(o, args);
        GameObject model;
        Component doll;
        Transform[] bones;
        Transform Bone(string name) => bones.Single(t => t.name == name);
        AnimationClip Clip(string name) => ((AnimationClip[])Get(doll, "clips"))[(int)Enum.Parse(doll.GetType().GetNestedType("Motion"), name)];
        void Sample(string name, float phase) => Call(doll, "Sample", Enum.Parse(doll.GetType().GetNestedType("Motion"), name), phase);
        [SetUp] public void Setup()
        {
            model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "circus-golem-look.fbx")); model.SetActive(false);
            doll = model.AddComponent(T("EscapePursuerDoll"));
            T("EditorTools.EscapePursuerRevision7Builder").GetMethod("Configure").Invoke(null, new object[] { doll });
            bones = (Transform[])Get(doll, "bones");
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(model);

        [Test] public void AllTwentyFiveClipsResolveOnLatestModelAndKeepEveryBoneAtUnitScale()
        {
            var clips = ((AnimationClip[])Get(doll, "clips")).Distinct().ToArray();
            Assert.That(clips.Length, Is.EqualTo(25));
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.That(skins.Length, Is.EqualTo(9));
            Assert.That(skins.SelectMany(s => s.bones).Distinct().Count(), Is.EqualTo(207));
            foreach (var clip in clips)
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    Assert.That(binding.path.Length == 0 || model.transform.Find(binding.path), Is.True, clip.name + "/" + binding.path);
                for (int i = 0; i <= 4; i++)
                {
                    clip.SampleAnimation(model, clip.length * i / 4);
                    foreach (var bone in bones)
                    {
                        Assert.That(Vector3.Distance(bone.localScale, Vector3.one), Is.LessThan(.002f), clip.name + "/" + bone.name);
                        Assert.That(float.IsFinite(bone.position.sqrMagnitude), Is.True);
                    }
                }
            }
        }

        [TestCase(4, 0, 1)][TestCase(12, 1, 1)][TestCase(36, -1, 1)][TestCase(36, 1, -1)]
        public void RuntimeReachKeepsTheHandAndShellRigidWhileStoredCanvasUnfolds(float distance, float x, float z)
        {
            Sample("GrabStretch", .3f);
            model.transform.SetPositionAndRotation(new Vector3(31, 12, -18), Quaternion.Euler(0, 73, 0));
            var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s => s.name == "Hand.R");
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh, true); var before = mesh.vertices;
                Vector3 point = Bone("Forearm.R").position + model.transform.TransformDirection(new Vector3(x, .2f, z).normalized) * distance;
                Call(doll, "StretchTo", point);
                Assert.That(Vector3.Distance(Bone("GripSocket.R").position, point), Is.LessThan(.003f));
                foreach (var bone in bones) Assert.That(Vector3.Distance(bone.localScale, Vector3.one), Is.LessThan(.002f), bone.name);
                skin.BakeMesh(mesh, true); var after = mesh.vertices;
                for (int i = 1; i < before.Length; i += 37)
                    Assert.That(Vector3.Distance(after[0], after[i]), Is.EqualTo(Vector3.Distance(before[0], before[i])).Within(.003f));
                Vector3 axis = (Bone("Hand.R").position - Bone("Forearm.R").position).normalized;
                float length = Vector3.Distance(Bone("Hand.R").position, Bone("Forearm.R").position);
                Assert.That(Vector3.Dot(Bone("Fold144.R").position - Bone("Forearm.R").position, axis), Is.EqualTo(length).Within(.003f));
                Assert.That(Vector3.Distance(Bone("Fold144.R").position, Bone("Fold00.R").position), Is.GreaterThan(distance - 1.5f));
                var shell = Bone("Sleeve05.R");
                Assert.That(Vector3.Distance(shell.position, Bone("Hand.R").position), Is.LessThan(.2f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase("Walk", 3.24f, .58f)][TestCase("Run", 7.48f, .30f)][TestCase("CrouchWalk", 3.6f, .6f)][TestCase("CarryWalk", 3.24f, .58f)]
        public void PlantedFootStaysStillWhenTheGameAdvancesByTheAuthoredStride(string name, float distance, float stance)
        {
            Sample(name, 0); Vector3 planted = Bone("Foot.L").position;
            // The nominal toe-off falls between baked frames. Stop at the final planted
            // key, before interpolation toward the next airborne frame begins.
            float frames = Clip(name).length * Clip(name).frameRate;
            stance = Mathf.Floor(stance * frames) / frames;
            for (int i = 1; i <= 10; i++)
            {
                float phase = stance * i / 10;
                Sample(name, phase);
                Assert.That(Vector3.Distance(Bone("Foot.L").position + model.transform.forward * (distance * phase), planted), Is.LessThan(.035f));
            }
            float run = name == "Run" ? 1 : 0, duck = name == "CrouchWalk" ? 1 : 0;
            Assert.That((float)Call(doll, "CycleDistance", duck, run), Is.EqualTo(distance).Within(.001f));
        }

        [Test] public void CrouchedTrackingFitsTheExistingPassage()
        {
            var mesh = new Mesh();
            try
            {
                float highest = 0;
                for (int frame = 0; frame <= 12; frame++)
                {
                    for (int settle = 0; settle < 30; settle++)
                    {
                        Sample("CrouchWalk", frame / 12f);
                        Call(doll, "ApplyLook", (Vector3?)new Vector3(2, 1.5f, 7), 1f / 30);
                    }
                    foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        skin.BakeMesh(mesh, true);
                        foreach (var vertex in mesh.vertices) highest = Mathf.Max(highest, skin.transform.TransformPoint(vertex).y);
                    }
                }
                Assert.That(highest, Is.LessThanOrEqualTo(3.35f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test] public void LatestGripAnimationClosesAllTenFingersAndResamplingRemovesReach()
        {
            var fingers = bones.Where(t => new[] { "Thumb1", "Index1", "Middle1", "Ring1", "Little1" }.Any(n => t.name.StartsWith(n + "."))).ToArray();
            Sample("HandFlex", 0); var open = fingers.Select(t => t.localRotation).ToArray();
            Sample("HandFlex", .5f);
            Assert.That(fingers.Length, Is.EqualTo(10));
            for (int i = 0; i < fingers.Length; i++) Assert.That(Quaternion.Angle(open[i], fingers[i].localRotation), Is.GreaterThan(15));
            Sample("Walk", .2f); var expected = Bone("Hand.R").position;
            Call(doll, "StretchTo", Bone("Forearm.R").position + Vector3.forward * 36);
            Sample("Walk", .2f);
            Assert.That(Vector3.Distance(expected, Bone("Hand.R").position), Is.LessThan(.001f));
        }

        [Test] public void CaptureSelectsTearingHoldAndCarryingInsteadOfFreezingTheReach()
        {
            var root = new GameObject("New capture poses"); root.SetActive(false);
            try
            {
                var chase = root.AddComponent(T("EscapeUnderstageChase")); Set(chase, "doll", doll);
                var attack = Get(chase, "attack");
                Set(attack, "Phase", Enum.Parse(attack.GetType().GetField("Phase").FieldType, "Grab"));
                float sever = (float)chase.GetType().GetProperty("SeverTime", Flags).GetValue(chase);
                int Action() => (int)Get(chase.GetType().GetProperty("DollPose", Flags).GetValue(chase), "Action");
                int Motion(string name) => (int)Enum.Parse(doll.GetType().GetNestedType("Motion"), name);
                Set(attack, "Started", -(double)(sever * .5f)); Set(chase, "attack", attack);
                Assert.That(Action(), Is.EqualTo(Motion("CaptureTear")));
                Set(attack, "Started", -(double)(sever * 2)); Set(chase, "attack", attack);
                Assert.That(Action(), Is.EqualTo(Motion("GrabHold")));
                Set(chase, "gaitResting", false);
                Assert.That(Action(), Is.EqualTo(Motion("CarryWalk")));
                Set(attack, "Phase", Enum.Parse(attack.GetType().GetField("Phase").FieldType, "Warning"));
                Set(attack, "Aim", Vector3.forward * 36); Set(chase, "attack", attack);
                Assert.That(Action(), Is.EqualTo(Motion("ReachFullRange")));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("Ignition", "Ignition")][TestCase("DashWarning", "ChargeWarning")]
        [TestCase("Dashing", "Dash")][TestCase("Hit", "HitLeft")][TestCase("Dying", "Death")][TestCase("Dead", "Death")]
        public void BossPhasesUseTheAuthoredClips(string phase, string clip)
        {
            var root = new GameObject("New boss poses"); root.SetActive(false);
            try
            {
                var boss = root.AddComponent(T("EscapePursuerBoss")); var state = Get(boss, "state");
                Set(state, "Phase", Enum.Parse(state.GetType().GetField("Phase").FieldType, phase)); Set(boss, "state", state);
                var args = new object[] { .2d, Enum.Parse(doll.GetType().GetNestedType("Motion"), "Idle"), 0f };
                Assert.That(Call(boss, "DollAction", args), Is.True);
                Assert.That(args[1].ToString(), Is.EqualTo(clip));
                Assert.That((float)args[2], Is.InRange(0f, 1f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void MissingLegsStayLowDuringHitsAttacksAndDeathAndResetWithTheRound()
        {
            var root = new GameObject("Maimed action support"); root.SetActive(false);
            try
            {
                var boss = root.AddComponent(T("EscapePursuerBoss")); Set(doll, "boss", boss);
                var state = Get(boss, "state"); Set(state, "Mask", (byte)48); Set(boss, "state", state);
                object Motion(string name) => Enum.Parse(doll.GetType().GetNestedType("Motion"), name);
                foreach (var name in new[] { "HitLeft", "HitRight", "Dash", "CaptureTear", "GrabHold", "Throw", "ChargeWarning" })
                {
                    for (int frame = 0; frame <= 4; frame++)
                    {
                        Call(doll, "SampleAction", Motion(name), frame / 4f);
                        Assert.That(Bone("Hips").position.y, Is.LessThan(1.15f), name);
                        foreach (var bone in bones) Assert.That(Vector3.Distance(bone.localScale, Vector3.one), Is.LessThan(.002f));
                    }
                }
                for (int frame = 0; frame <= 20; frame++)
                {
                    Call(doll, "SampleAction", Motion("Death"), frame / 20f);
                    Assert.That(Bone("Hips").position.y, Is.LessThan(1.14f));
                }
                Sample("Death", 1); var grounded = bones.Select(t => t.position).ToArray();
                Call(doll, "SampleAction", Motion("Death"), 1f);
                for (int i = 0; i < bones.Length; i++) Assert.That(Vector3.Distance(bones[i].position, grounded[i]), Is.LessThan(.001f));
                Set(state, "Mask", (byte)0); Set(boss, "state", state);
                Call(doll, "SampleAction", Motion("Idle"), 0f);
                Assert.That(Bone("Hips").position.y, Is.EqualTo(1.67f).Within(.002f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void CommonPrefabUsesLatestBonesAndAllEightSeverableParts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var linked = prefab.GetComponentInChildren(doll.GetType(), true);
            Assert.That(Get(linked, "revision"), Is.EqualTo(7));
            Assert.That(((AnimationClip[])Get(linked, "clips")).Distinct().Count(), Is.EqualTo(25));
            Assert.That(((Transform[])Get(linked, "folds")).Length, Is.EqualTo(145));
            Assert.That(((Transform[])Get(linked, "sleeves")).Length, Is.EqualTo(6));
            Assert.That((Object)Get(linked, "stretchIsolation"), Is.Null);
            Assert.That(((BoxCollider[])Get(linked, "hitboxes")).All(c => c), Is.True);
            Assert.That(((BoxCollider[])Get(linked, "hitboxes")).Length, Is.EqualTo(8));
        }
    }
}
