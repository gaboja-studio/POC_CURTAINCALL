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
    public sealed class EntranceGaitTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Prefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab";
        GameObject container;
        Component chase, breach, doll;
        AnimationClip entrance;

        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute." + name)).First(t => t != null);
        static object Get(object owner, string field) => owner.GetType().GetField(field, Flags).GetValue(owner);
        static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Flags).SetValue(owner, value);
        static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Flags).Invoke(owner, args);
        float Phase => (float)Get(chase, "stride");
        float Run => (float)Get(chase, "runWeight");
        float Cycle(float run) => (float)Call(doll, "CycleDistance", 0f, run);
        object Pose() => Call(chase, "DollPoseAt", null, false);
        int Motion(string name) => (int)Enum.Parse(doll.GetType().GetNestedType("Motion"), name);
        void ConfigureTravel() => T("EditorTools.EscapePursuerEntranceGaitBuilder")
            .GetMethod("Configure").Invoke(null, new object[] { chase });

        [SetUp]
        public void Setup()
        {
            // An inactive outer parent also contains activation keys from the entrance
            // clip, so sampling cannot register this clone as the live chase singleton.
            container = new GameObject("Entrance gait test (inactive)");
            container.SetActive(false);
            var clone = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), container.transform);
            chase = clone.GetComponentInChildren(T("EscapeUnderstageChase"), true);
            doll = clone.GetComponentInChildren(T("EscapePursuerDoll"), true);
            breach = (Component)Get(chase, "breach");
            entrance = (AnimationClip)Get(breach, "entrance");
            Assert.That(chase && doll && breach && entrance, Is.True);
            Call(chase, "Awake");
            Set(chase, "boss", null);
            Set(chase, "walls", null);
            Set(doll, "chase", chase);
            Set(breach, "lastSample", entrance.length);
            Set(chase, "gaitResting", false);
            Set(chase, "nextFootfall", double.PositiveInfinity);
            Assert.That(Get(doll, "revision"), Is.GreaterThanOrEqualTo(7));
            Assert.That(Get(chase, "fast"), Is.Not.Null);
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(container);

        [TestCase(false)]
        [TestCase(true)]
        public void EntranceTravelMatchesActualActorTranslationAndIsMonotonic(bool transformed)
        {
            if (transformed)
            {
                container.transform.SetPositionAndRotation(new Vector3(31, 12, -18), Quaternion.Euler(0, 37, 0));
                container.transform.localScale = new Vector3(1.25f, .8f, 1.75f);
            }
            ConfigureTravel();
            var travel = (AnimationCurve)Get(breach, "entranceTravel");
            Assert.That(travel.length, Is.GreaterThan(2));
            Assert.That(travel.Evaluate(0), Is.EqualTo(0).Within(.0001f));
            entrance.SampleAnimation(breach.gameObject, 0);
            Vector3 previous = chase.transform.position;
            float measured = 0, lastCurveValue = 0;
            int count = Mathf.CeilToInt(entrance.length * 240);
            for (int i = 1; i <= count; i++)
            {
                float time = entrance.length * i / count;
                entrance.SampleAnimation(breach.gameObject, time);
                Vector3 position = chase.transform.position;
                measured += Vector3.ProjectOnPlane(position - previous, Vector3.up).magnitude;
                float authored = travel.Evaluate(time);
                Assert.That(authored, Is.GreaterThanOrEqualTo(lastCurveValue - .00001f), "time " + time);
                Assert.That(authored, Is.EqualTo(measured).Within(.02f), "sampled Actor motion at " + time);
                previous = position;
                lastCurveValue = authored;
            }
            Assert.That(measured, Is.GreaterThan(5f), "The actual entrance must be sampled, not an unmoving child.");
        }

        [Test]
        public void FirstHallPlantEndsRootPitchWithoutRemovingTheImpactRecoil()
        {
            float impact = (float)Get(breach, "impactTime");
            entrance.SampleAnimation(breach.gameObject, impact);
            Assert.That(Vector3.Angle(chase.transform.up, breach.transform.up), Is.GreaterThan(4f));
            float firstPlant = (float)T("EscapeUnderstageBreach").GetField("FirstHallPlant", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            for (int i = 0; i <= 40; i++)
            {
                float time = Mathf.Lerp(firstPlant, entrance.length, i / 40f);
                entrance.SampleAnimation(breach.gameObject, time);
                Assert.That(Vector3.Angle(chase.transform.up, breach.transform.up), Is.LessThan(.05f), "time " + time);
            }
        }

        [TestCase(3.2f)]
        [TestCase(4.0f)]
        [TestCase(5.0f)]
        public void ClearEntranceUsesWalkAndDistancePhaseWithoutCrouch(float time)
        {
            ConfigureTravel();
            Set(breach, "lastSample", time);
            var pose = Pose();
            Assert.That(Get(pose, "Action"), Is.EqualTo(Motion("Walk")));
            Assert.That(Get(pose, "Duck"), Is.EqualTo(0f));
            Assert.That(Get(pose, "CrouchBlend"), Is.EqualTo(0f));
            float start = (float)Get(breach, "impactTime") + (float)T("EscapePursuerWalls")
                .GetField("FollowThrough", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetRawConstantValue();
            entrance.SampleAnimation(breach.gameObject, start);
            Vector3 from = chase.transform.position;
            entrance.SampleAnimation(breach.gameObject, time);
            float distance = Vector3.ProjectOnPlane(chase.transform.position - from, Vector3.up).magnitude;
            Assert.That((float)Get(pose, "Stride"), Is.EqualTo(Mathf.Repeat(distance / Cycle(0), 1)).Within(.002f));
        }

        [Test]
        public void ImpactRecoveryBlendsIntoWalkWithoutReintroducingCrouch()
        {
            float followThrough = (float)T("EscapePursuerWalls")
                .GetField("FollowThrough", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetRawConstantValue();
            Set(breach, "lastSample", (float)Get(breach, "impactTime") + followThrough + .175f);
            var pose = Pose();
            Assert.That(Get(pose, "Action"), Is.EqualTo(Motion("WallStrike")));
            Assert.That((float)Get(pose, "WalkBlend"), Is.EqualTo(.5f).Within(.001f));
            Assert.That(Get(pose, "CrouchBlend"), Is.EqualTo(0f));
        }

        [Test]
        public void LowPassageStillUsesCrouchBodyHeightAndCrouchAnimation()
        {
            Set(chase, "stride", .2f);
            Call(chase, "AdvanceGait", 0f, 1f, 1f, false, .016f);
            Call(chase, "SetBodyHeight", 1f);
            var pose = Pose();
            Assert.That(Get(pose, "Action"), Is.EqualTo(-1));
            Assert.That(Get(pose, "Duck"), Is.EqualTo(1f));
            var body = chase.GetComponent<CapsuleCollider>();
            Assert.That(body.height, Is.EqualTo(3.35f).Within(.001f));
            Assert.That(body.center.y, Is.EqualTo(body.height * .5f).Within(.001f));
            var bones = (Transform[])Get(doll, "bones");
            Call(doll, "Sample", Enum.Parse(doll.GetType().GetNestedType("Motion"), "CrouchWalk"), Phase);
            var expected = bones.Select(b => b.localPosition).ToArray();
            var expectedRotation = bones.Select(b => b.localRotation).ToArray();
            Call(doll, "SampleCurrent");
            for (int i = 0; i < bones.Length; i++)
            {
                Assert.That(Vector3.Distance(bones[i].localPosition, expected[i]), Is.LessThan(.001f), bones[i].name);
                Assert.That(Quaternion.Angle(bones[i].localRotation, expectedRotation[i]), Is.LessThan(.05f), bones[i].name);
            }
            Call(chase, "SetBodyHeight", 0f);
            Assert.That(body.height, Is.EqualTo(5f).Within(.001f));
        }

        void Begin(float phase, float run, bool resting = false)
        {
            Set(chase, "stride", phase);
            Set(chase, "runWeight", run);
            Set(chase, "gaitResting", resting);
            Set(chase, "stepNumber", 0u);
        }
        void Advance(float distance, float run) => Call(chase, "AdvanceGait", distance, 0f, run, false, .016f);

        [TestCase(.2f, .55f, 0f, 1f)]
        [TestCase(.8f, 1.05f, 0f, 1f)]
        [TestCase(.2f, .55f, 1f, 0f)]
        [TestCase(.8f, 1.05f, 1f, 0f)]
        public void MovingPaceChangesOnlyAtNextPlantAndPreservesDistance(float start, float plant, float from, float to)
        {
            Begin(start, from);
            float before = (plant - start) * Cycle(from) - .01f;
            Advance(before, to);
            Assert.That(Run, Is.EqualTo(from), "No mid-flight clip change.");
            Assert.That(Phase, Is.EqualTo(Mathf.Repeat(start + before / Cycle(from), 1)).Within(.00001f));
            Advance(.01f + .03f * Cycle(to), to);
            Assert.That(Run, Is.EqualTo(to));
            Assert.That(Phase, Is.EqualTo(Mathf.Repeat(plant + .03f, 1)).Within(.00001f));
            Assert.That(Get(chase, "stepNumber"), Is.EqualTo(1u));
        }

        [TestCase(.2f, .55f, 0f, 1f)]
        [TestCase(.8f, 1.05f, 1f, 0f)]
        public void TransitionPhaseDoesNotDependOnFramePartition(float start, float plant, float from, float to)
        {
            float before = (plant - start) * Cycle(from);
            float after = Cycle(to) * .13f;
            Begin(start, from);
            Advance(before + after, to);
            float singlePhase = Phase;
            uint singleSteps = (uint)Get(chase, "stepNumber");
            Begin(start, from);
            Advance(before * .4f, to);
            Advance(before * .6f + after * .2f, to);
            Advance(after * .8f, to);
            Assert.That(Run, Is.EqualTo(to));
            Assert.That(Phase, Is.EqualTo(singlePhase).Within(.00001f));
            Assert.That(Get(chase, "stepNumber"), Is.EqualTo(singleSteps));
        }

        [Test]
        public void PaceHysteresisRejectsMidpointJitterButAcceptsBothThresholds()
        {
            Begin(.2f, 0f);
            foreach (float request in new[] { .49f, .51f, .64f })
            {
                Advance(Cycle(0) * .5f, request);
                Assert.That(Run, Is.EqualTo(0f));
            }
            Advance(Cycle(0) * .5f, .65f);
            Assert.That(Run, Is.EqualTo(1f));
            foreach (float request in new[] { .51f, .49f, .36f })
            {
                Advance(Cycle(1) * .5f, request);
                Assert.That(Run, Is.EqualTo(1f));
            }
            Advance(Cycle(1) * .5f, .35f);
            Assert.That(Run, Is.EqualTo(0f));
        }

        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        public void StartingFromRestSelectsPaceImmediately(float from, float to)
        {
            Begin(.325f, from, true);
            Advance(.1f, to);
            Assert.That(Run, Is.EqualTo(to));
            Assert.That(Phase, Is.EqualTo(.325f + .1f / Cycle(to)).Within(.00001f));
        }

        [Test]
        public void DiscretePresentationDoesNotQuantizeCommonMovementSpeed()
        {
            float walk = (float)Call(chase, "Speed", 0f, 0f);
            float run = (float)Call(chase, "Speed", 0f, 1f);
            foreach (float display in new[] { 0f, 1f })
            {
                Begin(.2f, display);
                Assert.That((float)Call(chase, "Speed", 0f, .5f), Is.EqualTo((walk + run) * .5f).Within(.00001f));
            }
        }
    }
}
