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
    public sealed class PursuerHeavyGaitTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Prefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab";
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute." + name)).First(t => t != null);
        static object Get(object o, string field) => o.GetType().GetField(field, Flags).GetValue(o);
        static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
        static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Flags).Invoke(o, args);
        GameObject root;
        Component chase, doll;
        [SetUp] public void Setup()
        {
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)); root.SetActive(false);
            chase = root.GetComponentInChildren(T("EscapeUnderstageChase"), true);
            doll = root.GetComponentInChildren(T("EscapePursuerDoll"), true);
            Call(chase, "Awake");
            Set(chase, "runWeight", 1f); // These tests measure an already established run.
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        void Advance(float metres, bool rest = false) => Call(chase, "AdvanceGait", metres, 0f, 1f, rest, .016f);

        [Test] public void LongRunCountsTwoContactsPerCycleAtNormalPressureAndHiddenTravelSpeeds()
        {
            Assert.That(Get(doll, "runCycleDistance"), Is.EqualTo(7.48f).Within(.001f));
            foreach (float speed in new[] { 6f, 7.8f, 20f })
            {
                Set(chase, "stride", 0f); Set(chase, "stepNumber", 0u);
                for (int i = 0; i < 600; i++) Advance(speed / 60);
                Assert.That(Get(chase, "stepNumber"), Is.EqualTo((uint)Mathf.Floor(speed * 10 / 3.74f)), speed.ToString());
            }
        }
        [Test] public void ContactFollowsAnimationPhaseAfterIdleAndAcrossSkippedFrames()
        {
            Set(chase, "stride", .49f); Set(chase, "stepNumber", 0u);
            Advance(.02f); Assert.That(Get(chase, "stepNumber"), Is.EqualTo(0u));
            Advance(.1f); Assert.That(Get(chase, "stepNumber"), Is.EqualTo(1u));
            Set(chase, "stride", .96f); Advance(0, true); // Idle settles to .992.
            Set(chase, "stepNumber", 0u); Advance(.1f);
            Assert.That(Get(chase, "stepNumber"), Is.EqualTo(1u), "Re-start must follow the current planted pose.");
            Set(chase, "stride", .1f); Set(chase, "stepNumber", 0u); Advance(7.48f * 1.6f);
            Assert.That(Get(chase, "stepNumber"), Is.EqualTo(3u), "A hitch must retain the contact counter without queuing a burst of audio.");
        }
        [TestCase("Leaping", false)][TestCase("Landing", false)][TestCase("LeapWarning", false)]
        [TestCase("Hit", false)][TestCase("Dying", false)][TestCase("Battle", true)][TestCase("Dashing", true)]
        public void BossOnlyCountsGroundedMovementIncludingTheFinalLeapFrame(string phase, bool footsteps)
        {
            var boss = root.GetComponentInChildren(T("EscapePursuerBoss"), true);
            if (!boss) boss = root.AddComponent(T("EscapePursuerBoss"));
            Set(boss, "chase", chase);
            var state = Get(boss, "state"); var phaseType = state.GetType().GetField("Phase").FieldType;
            // state has already changed to Landing while travelPhase is still Leaping.
            Set(state, "Phase", Enum.Parse(phaseType, "Landing")); Set(boss, "state", state);
            Call(boss, "AdvanceGroundedGait", Enum.Parse(phaseType, phase), boss.transform.position - Vector3.forward * 10, .016f);
            Assert.That((uint)Get(chase, "stepNumber") > 0, Is.EqualTo(footsteps));
        }
        [Test] public void BothFootPlantsAndAirborneRecoverySurviveUnityImport()
        {
            var motion = doll.GetType().GetNestedType("Motion");
            var run = Enum.Parse(motion, "Run");
            var bones = (Transform[])Get(doll, "bones");
            foreach (var side in new[] { (name: "Foot.L", start: 0f), (name: "Foot.R", start: .5f) })
            {
                Call(doll, "Sample", run, side.start);
                var foot = bones.Single(t => t.name == side.name); var plant = foot.position;
                for (int i = 1; i <= 10; i++)
                {
                    float delta = .28f*i/10;
                    Call(doll, "Sample", run, side.start+delta);
                    Assert.That(Vector3.Distance(foot.position + doll.transform.forward*(7.48f*delta), plant), Is.LessThan(.035f), side.name);
                }
            }
            var mesh = new Mesh();
            try
            {
                foreach (float phase in new[] { .40f, .90f })
                {
                    Call(doll, "Sample", run, phase);
                    foreach (var skin in doll.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.name.StartsWith("Foot.")))
                    {
                        skin.BakeMesh(mesh, true);
                        float sole = mesh.vertices.Min(v => doll.transform.InverseTransformPoint(skin.transform.TransformPoint(v)).y);
                        Assert.That(sole, Is.GreaterThan(.02f), skin.name + "/" + phase);
                    }
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }
        [Test] public void ImpactVoiceCannotRetuneWalkingTailAndSourcesHaveNoDoppler()
        {
            var step = (AudioSource)Get(chase, "footstep"); var impact = (AudioSource)Get(chase, "impactVoice");
            Assert.That(impact, Is.Not.Null); Assert.That(impact, Is.Not.SameAs(step));
            Assert.That(step.clip.name, Is.EqualTo("HeavyFootfallL"));
            Assert.That(((AudioClip)Get(chase, "alternateFootstep")).name, Is.EqualTo("HeavyFootfallR"));
            Assert.That(((AudioClip)Get(chase, "bodyImpact")).name, Is.EqualTo("HeavyBodyImpact"));
            Assert.That(step.dopplerLevel + impact.dopplerLevel, Is.Zero);
            Assert.That(step.pitch, Is.EqualTo(1));
        }
    }
}
