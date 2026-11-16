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
    public sealed class PursuerGaitBlendTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject model;
        Component doll;
        Transform[] feet;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute." + name)).First(t => t != null);
        object Call(string name, params object[] args) => doll.GetType().GetMethod(name, Flags).Invoke(doll, args);
        float Field(string name) => (float)doll.GetType().GetField(name, Flags).GetValue(doll);
        void Sample(string name, float phase) => Call("Sample", Enum.Parse(doll.GetType().GetNestedType("Motion"), name), phase);

        [SetUp]
        public void Setup()
        {
            model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/Characters/Enemies/CircusGolemRevision7/circus-golem-look.fbx"));
            model.SetActive(false);
            doll = model.AddComponent(T("EscapePursuerDoll"));
            T("EditorTools.EscapePursuerRevision7Builder").GetMethod("Configure").Invoke(null, new object[] { doll });
            var bones = (Transform[])doll.GetType().GetField("bones", Flags).GetValue(doll);
            feet = new[] { bones.Single(b => b.name == "Foot.L"), bones.Single(b => b.name == "Foot.R") };
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(model);

        [TestCase(.25f, "Walk")]
        [TestCase(.75f, "Run")]
        public void IntermediatePaceKeepsBothFeetOnOneAuthoredContactSchedule(float run, string motion)
        {
            // At .4 Walk still plants its left foot while Run is airborne. The old
            // full-body interpolation produced neither clip's contact or flight.
            Sample(motion, .4f);
            var positions = feet.Select(f => f.position).ToArray();
            var rotations = feet.Select(f => f.rotation).ToArray();
            Call("SampleLocomotion", .4f, 0f, run);
            for (int i = 0; i < feet.Length; i++)
            {
                Assert.That(Vector3.Distance(feet[i].position, positions[i]), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(feet[i].rotation, rotations[i]), Is.LessThan(.01f));
            }
        }

        [TestCase(.25f, 0, .05f)]
        [TestCase(.25f, 1, .55f)]
        [TestCase(.75f, 0, .05f)]
        [TestCase(.75f, 1, .55f)]
        public void IntermediatePaceUsesTheSameDistanceAsItsPlantedFoot(float run, int foot, float firstPhase)
        {
            var origin = new Vector3(31, 12, -18);
            model.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, 73, 0));
            float distance = (float)Call("CycleDistance", 0f, run);
            Call("SampleLocomotion", firstPhase, 0f, run);
            Vector3 planted = feet[foot].position;
            for (int i = 1; i <= 10; i++)
            {
                float phase = firstPhase + i * .02f;
                model.transform.position = origin + model.transform.forward * (distance * (phase - firstPhase));
                Call("SampleLocomotion", phase, 0f, run);
                Assert.That(Vector3.Distance(feet[foot].position, planted), Is.LessThan(.035f), "phase " + phase);
            }
        }

        [Test]
        public void SnapshotPoseDoesNotDependOnPreviouslySampledPace()
        {
            Call("SampleLocomotion", .2f, 0f, 0f);
            Call("SampleLocomotion", .4f, 0f, .51f);
            var expected = feet.Select(f => f.position).ToArray();
            Call("SampleLocomotion", .2f, 0f, 1f);
            Call("SampleLocomotion", .4f, 0f, .51f);
            for (int i = 0; i < feet.Length; i++)
                Assert.That(Vector3.Distance(feet[i].position, expected[i]), Is.LessThan(.001f));
        }

        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        public void DisplayTransitionSpreadsAnArbitraryPhaseSwitchAcrossFrames(float from, float to)
        {
            const float phase = .325f, dt = 1f / 60;
            Call("SampleDisplayedLocomotion", phase, 0f, from, 0f, 0);
            var previous = feet.Select(f => f.position).ToArray();
            for (int frame = 1; frame <= 13; frame++)
            {
                Call("SampleDisplayedLocomotion", phase, 0f, to, frame * dt, frame);
                for (int side = 0; side < feet.Length; side++)
                {
                    Assert.That(Vector3.Distance(feet[side].position, previous[side]), Is.LessThan(.35f), "frame " + frame);
                    previous[side] = feet[side].position;
                }
            }
            Call("SampleLocomotion", phase, 0f, to);
            for (int side = 0; side < feet.Length; side++)
                Assert.That(Vector3.Distance(feet[side].position, previous[side]), Is.LessThan(.001f));
        }

        [Test]
        public void SameFrameResamplingDoesNotAdvanceTheDisplayTransition()
        {
            Call("SampleDisplayedLocomotion", .05f, 0f, 0f, 0f, 0);
            Call("SampleDisplayedLocomotion", .05f, 0f, 1f, .01f, 1);
            var previous = feet.Select(f => f.position).ToArray();
            Call("SampleDisplayedLocomotion", .05f, 0f, 1f, .12f, 1);
            for (int side = 0; side < feet.Length; side++)
                Assert.That(Vector3.Distance(feet[side].position, previous[side]), Is.LessThan(.001f));
        }

        [TestCase(0, .05f)]
        [TestCase(1, .55f)]
        public void FootfallTransitionCompensatesForTheMovingRoot(int support, float start)
        {
            const float speed = 6.6f, dt = 1f / 60;
            var origin = new Vector3(31, 12, -18);
            model.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, 73, 0));
            Call("SampleDisplayedLocomotion", start, 0f, 0f, 0f, 0);
            Vector3 planted = feet[support].position;
            float distance = Field("runCycleDistance");
            for (int frame = 1; frame <= 13; frame++)
            {
                float travelled = speed * dt * (frame - 1);
                model.transform.position = origin + model.transform.forward * travelled;
                Call("SampleDisplayedLocomotion", start + travelled / distance, 0f, 1f, frame * dt, frame);
                Assert.That(Vector3.Distance(feet[support].position, planted), Is.LessThan(.06f), "frame " + frame);
            }
            var bones = (Transform[])doll.GetType().GetField("bones", Flags).GetValue(doll);
            foreach (var bone in bones)
                Assert.That(Vector3.Distance(bone.localScale, Vector3.one), Is.LessThan(.002f), bone.name);
        }

        [Test]
        public void DirectClipSamplingRemainsPureDuringADisplayTransition()
        {
            Sample("Run", .325f);
            var expected = feet.Select(f => f.position).ToArray();
            Call("SampleDisplayedLocomotion", .325f, 0f, 0f, 0f, 0);
            Call("SampleDisplayedLocomotion", .325f, 0f, 1f, .01f, 1);
            Sample("Run", .325f);
            for (int side = 0; side < feet.Length; side++)
                Assert.That(Vector3.Distance(feet[side].position, expected[side]), Is.LessThan(.001f));
        }

        [Test]
        public void LegacyRigKeepsItsExistingInterpolatedCycleDistance()
        {
            doll.GetType().GetField("revision", Flags).SetValue(doll, 5);
            float expected = Mathf.Lerp(Field("walkCycleDistance"), Field("runCycleDistance"), .3f);
            Assert.That((float)Call("CycleDistance", .7f, .3f), Is.EqualTo(expected).Within(.0001f));
        }
    }
}
