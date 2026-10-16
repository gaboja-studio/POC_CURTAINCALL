using System.Linq;
using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonHazardCourseTests
    {
        Scene scene, previous;
        bool openedHere;
        GameObject[] roots;

        [OneTimeSetUp]
        public void SetUp()
        {
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.GetSceneByPath(HumanCannonTestSceneBuilder.ScenePath);
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(HumanCannonTestSceneBuilder.ScenePath, OpenSceneMode.Additive);
                    openedHere = true;
                }
                RestoreActive();
                roots = scene.GetRootGameObjects();
            }
            catch { TearDown(); throw; }
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            RestoreActive();
            if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            openedHere = false;
        }

        void RestoreActive()
        {
            if (previous.IsValid() && previous.isLoaded && SceneManager.GetActiveScene() != previous)
                SceneManager.SetActiveScene(previous);
        }

        [TestCase(1, 1f, 0.6f)]
        [TestCase(2, 1.25f, 0.8f)]
        [TestCase(3, 1.5f, 1f)]
        public void Difficulty_UsesApprovedSpeedAndWindPresets(int level, float speed, float wind)
        {
            Assert.IsTrue(CannonDifficulty.Valid(level));
            Assert.That(CannonDifficulty.HazardSpeed(level), Is.EqualTo(speed).Within(0.00001f));
            Assert.That(CannonDifficulty.WindSpeed(level), Is.EqualTo(wind).Within(0.00001f));
        }

        [Test]
        public void SavedCourse_DefaultsToLevelOneAndTenMinuteTarget_WithEveryHazardRegistered()
        {
            var course = All<HumanCannonHazardCourse>().Single();
            Assert.That(course.Difficulty, Is.EqualTo(1));
            Assert.That(CannonDifficulty.TargetSeconds, Is.EqualTo(600f));
            var hazards = Field<HumanCannonHazard[]>(course, "hazards");
            Assert.That(hazards, Has.Length.EqualTo(16));
            Assert.That(hazards, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(hazards);
            CollectionAssert.AreEquivalent(All<HumanCannonHazard>(), hazards);
            Assert.That(hazards.Count(h => h.Kind == CannonHazardKind.SpikeMace), Is.EqualTo(4));
            Assert.That(hazards.Count(h => h.Kind == CannonHazardKind.RotatingBlades), Is.EqualTo(4));
            Assert.That(hazards.Count(h => h.Kind == CannonHazardKind.GroundSaw), Is.EqualTo(8));
            foreach (var hazard in hazards)
            {
                Assert.That(Field<Transform>(hazard, "visual"), Is.Not.Null, hazard.name);
                Assert.That(hazard.Radius, Is.GreaterThan(0), hazard.name);
                Assert.That(hazard.enabled && hazard.gameObject.activeInHierarchy, Is.True, hazard.name);
            }
        }

        [Test]
        public void EightWindZones_AreRegisteredTriggersWithHorizontalVisibleFlow()
        {
            var zones = Field<HumanCannonWindZone[]>(All<HumanCannonHazardCourse>().Single(), "windZones");
            Assert.That(zones, Has.Length.EqualTo(8));
            Assert.That(zones, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(zones);
            CollectionAssert.AreEquivalent(All<HumanCannonWindZone>(), zones);
            foreach (var zone in zones)
            {
                Assert.That(zone.Area, Is.Not.Null, zone.name);
                Assert.IsTrue(zone.Area.isTrigger, zone.name);
                Assert.That(zone.Direction.y, Is.EqualTo(0).Within(0.00001f), zone.name);
                Assert.That(zone.Direction.magnitude, Is.EqualTo(1).Within(0.00001f), zone.name);
                var flow = zone.transform.Find("VisibleFlow");
                Assert.That(flow, Is.Not.Null, zone.name);
                Assert.That(flow.GetComponentsInChildren<LineRenderer>(true), Has.Length.EqualTo(16), zone.name);
            }
        }

        [Test]
        public void EachSection_HasTwoOrdinaryPolesAndOneGatePoleNearBounceApex()
        {
            var trampoline = All<HumanCannonTrampoline>().Single();
            float apex = Field<float>(trampoline, "surfaceHeight") +
                trampoline.UpwardSpeed * trampoline.UpwardSpeed / (2f * GameSettings.Base.Character.Gravity);
            var all = All<HumanCannonPole>();
            Assert.That(all, Has.Length.EqualTo(6));
            for (int section = 0; section < 2; section++)
            {
                var poles = all.Where(p => (p.Grip.position.z < 90) == (section == 0))
                    .OrderBy(p => p.Grip.position.z).ToArray();
                Assert.That(poles, Has.Length.EqualTo(3));
                Assert.That(poles.Count(p => p.LinkedGate == null), Is.EqualTo(2));
                Assert.That(poles.Count(p => p.LinkedGate != null), Is.EqualTo(1));
                foreach (var pole in poles)
                    Assert.That(pole.Grip.position.y, Is.EqualTo(section * 6f + apex - 0.5f).Within(0.001f), pole.name);
                Assert.That(poles[0].Grip.position.x, Is.EqualTo(section == 0 ? -7f : 7f).Within(0.001f));
                Assert.That(poles[0].Grip.position.z, Is.EqualTo(section == 0 ? 31f : 107f).Within(0.001f));
                Assert.That(poles[1].Grip.position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(poles[1].Grip.position.z, Is.EqualTo(section == 0 ? 44f : 120f).Within(0.001f));
                Assert.That(poles[2].name, Is.EqualTo("CeilingPole" + (section + 1) + "_4"));
                Assert.That(poles[2].Grip.position.x, Is.EqualTo(section == 0 ? 7f : -7f).Within(0.001f));
                Assert.That(poles[2].Grip.position.z, Is.EqualTo(section == 0 ? 61f : 137f).Within(0.001f));
                Assert.That(poles[2].Grip.position.z - poles[1].Grip.position.z, Is.GreaterThan(12f),
                    "후반 봉은 연속 6m 격자 대신 별도 선택 위치에 둡니다.");
            }
        }

        [Test]
        public void AllSixPoles_HaveUpperCablesAndAnchorsWithoutFloorSupportsOrCeilingPlanes()
        {
            var extension = roots.Single(root => root.name == "ExtendedCourse044").transform;
            Assert.That(extension.Find("FirstCeiling"), Is.Null);
            Assert.That(extension.Find("SecondCeiling"), Is.Null);
            var poles = Field<HumanCannonPole[]>(All<HumanCannonPoleSystem>().Single(), "poles");
            Assert.That(poles, Has.Length.EqualTo(6));
            for (int index = 0; index < poles.Length; index++)
            {
                var pole = poles[index];
                var ceiling = extension.Find(index < 4 ? "ExistingCeiling" + index : pole.name + "Ceiling");
                Assert.That(ceiling, Is.Not.Null, pole.name);
                Assert.That(Vector3.Distance(ceiling.position, pole.Grip.position), Is.LessThan(0.001f), pole.name);
                Assert.That(pole.transform.Find("LeftSupport"), Is.Null, pole.name);
                Assert.That(pole.transform.Find("RightSupport"), Is.Null, pole.name);
                foreach (string side in new[] { "Left", "Right" })
                {
                    var cable = ceiling.Find(side + "Cable");
                    var anchor = ceiling.Find(side + "Anchor");
                    Assert.That(cable, Is.Not.Null, pole.name);
                    Assert.That(anchor, Is.Not.Null, pole.name);
                    Assert.That(cable.position.y - cable.lossyScale.y * 0.5f,
                        Is.EqualTo(pole.Grip.position.y).Within(0.001f), pole.name);
                    Assert.That(anchor.position.y, Is.GreaterThan(cable.position.y), pole.name);
                }
            }
        }

        T[] All<T>() where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
