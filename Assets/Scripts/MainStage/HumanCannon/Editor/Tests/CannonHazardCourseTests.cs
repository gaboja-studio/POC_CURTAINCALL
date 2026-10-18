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
            var poleSaws = All<HumanCannonPoleSaw>();
            var poles = All<HumanCannonPole>();
            Assert.That(poleSaws, Has.Length.EqualTo(poles.Length), "해제용 봉을 포함해 봉마다 독립 왕복 톱 하나");
            Assert.That(hazards, Has.Length.EqualTo(16 + poleSaws.Length));
            Assert.That(hazards, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(hazards);
            CollectionAssert.AreEquivalent(All<HumanCannonHazard>(), hazards);
            var originalHazards = hazards.Except(poleSaws.Select(s => s.Hazard)).ToArray();
            Assert.That(originalHazards.Count(h => h.Kind == CannonHazardKind.SpikeMace), Is.EqualTo(4));
            Assert.That(originalHazards.Count(h => h.Kind == CannonHazardKind.RotatingBlades), Is.EqualTo(4));
            Assert.That(originalHazards.Count(h => h.Kind == CannonHazardKind.GroundSaw), Is.EqualTo(8));
            CollectionAssert.AreEquivalent(poles, poleSaws.Select(s => s.Pole).ToArray());
            CollectionAssert.AllItemsAreUnique(poleSaws.Select(s => s.Hazard).ToArray());
            foreach (var saw in poleSaws)
            {
                Assert.That(saw.Pole.HoldSeconds, Is.Zero, saw.name);
                Assert.That(saw.Hazard.Kind, Is.EqualTo(CannonHazardKind.GroundSaw), saw.name);
                Assert.That(Vector3.Distance(saw.Hazard.transform.position, saw.RailCenter), Is.LessThan(0.001f), saw.name);
                Assert.That((saw.RailCenter - saw.Hazard.Travel).x, Is.EqualTo(-24).Within(0.001f), saw.name);
                Assert.That((saw.RailCenter + saw.Hazard.Travel).x, Is.EqualTo(24).Within(0.001f), saw.name);
                Assert.That(saw.Hazard.PeriodSeconds, Is.EqualTo(4).Within(0.001f), saw.name);
                Assert.That(Mathf.Abs(Vector3.Dot(saw.Hazard.Travel.normalized, saw.Pole.Forward)), Is.LessThan(0.001f),
                    "왕복은 W/S 회피 방향과 직교한다: " + saw.name);
            }
            foreach (var hazard in hazards)
            {
                Assert.That(Field<Transform>(hazard, "visual"), Is.Not.Null, hazard.name);
                Assert.That(hazard.Radius, Is.GreaterThan(0), hazard.name);
                Assert.That(hazard.enabled && hazard.gameObject.activeInHierarchy, Is.True, hazard.name);
            }
        }

        [Test]
        public void SavedCourse_HasNoRegisteredOrSceneWindZones()
        {
            var zones = Field<HumanCannonWindZone[]>(All<HumanCannonHazardCourse>().Single(), "windZones");
            Assert.That(zones, Is.Empty, "삭제한 강풍 구간이 코스에 등록되어 있으면 안 된다.");
            Assert.That(All<HumanCannonWindZone>(), Is.Empty, "비활성 오브젝트에도 강풍 구간을 남기지 않는다.");
        }

        [Test]
        public void EachSection_HasFourOrdinaryPolesAndOneGatePoleWithoutLongEmptyGaps()
        {
            var trampoline = All<HumanCannonTrampoline>().Single();
            float apex = Field<float>(trampoline, "surfaceHeight") +
                trampoline.UpwardSpeed * trampoline.UpwardSpeed / (2f * GameSettings.Base.Character.Gravity);
            var all = All<HumanCannonPole>();
            Assert.That(all, Has.Length.EqualTo(10));
            for (int section = 0; section < 2; section++)
            {
                var poles = all.Where(p => (p.Grip.position.z < 90) == (section == 0))
                    .OrderBy(p => p.Grip.position.z).ToArray();
                Assert.That(poles, Has.Length.EqualTo(5));
                Assert.That(poles.Count(p => p.LinkedGate == null), Is.EqualTo(4));
                Assert.That(poles.Count(p => p.LinkedGate != null), Is.EqualTo(1));
                foreach (var pole in poles)
                    Assert.That(pole.Grip.position.y, Is.EqualTo(section * 6f + apex - 0.5f).Within(0.001f), pole.name);
                var gatePole = poles.Single(p => p.LinkedGate != null);
                Assert.That(gatePole.Grip.position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(gatePole.Grip.position.z, Is.EqualTo(section == 0 ? 44f : 120f).Within(0.001f));
                Assert.That(poles[0].Grip.position.z, Is.LessThanOrEqualTo(section == 0 ? 22.001f : 98.001f));
                Assert.That(poles[^1].Grip.position.z, Is.GreaterThanOrEqualTo(section == 0 ? 60.999f : 136.999f));
                for (int i = 1; i < poles.Length; i++)
                    Assert.That(poles[i].Grip.position.z - poles[i - 1].Grip.position.z, Is.InRange(1f, 13.001f),
                        "같은 위치의 중복 봉이나 긴 빈 구간 없이 분산되어야 한다.");
            }
        }

        [Test]
        public void AllTenPoles_HaveTheirOwnUpperCablesAndAnchorsWithoutFloorSupports()
        {
            var extension = roots.Single(root => root.name == "ExtendedCourse044").transform;
            Assert.That(extension.Find("FirstCeiling"), Is.Null);
            Assert.That(extension.Find("SecondCeiling"), Is.Null);
            var poles = Field<HumanCannonPole[]>(All<HumanCannonPoleSystem>().Single(), "poles");
            Assert.That(poles, Has.Length.EqualTo(10));
            foreach (string field in new[] { "visualBar", "leftCable", "rightCable", "leftAnchor", "rightAnchor" })
            {
                var references = poles.Select(p => Field<Transform>(p, field)).ToArray();
                Assert.That(references, Has.All.Not.Null);
                CollectionAssert.AllItemsAreUnique(references.Select(value => value.GetInstanceID()).ToArray(),
                    "복제한 봉이 원본 외형을 움직이면 안 된다: " + field);
            }
            for (int index = 0; index < poles.Length; index++)
            {
                var pole = poles[index];
                var left = Field<Transform>(pole, "leftAnchor");
                var right = Field<Transform>(pole, "rightAnchor");
                Assert.That(Vector3.Distance((left.position + right.position) * 0.5f,
                    pole.Grip.position + Vector3.up * 8), Is.LessThan(0.001f), "천장이 중립 봉 바로 위에 있어야 한다: " + pole.name);
                Assert.That(pole.transform.Find("LeftSupport"), Is.Null, pole.name);
                Assert.That(pole.transform.Find("RightSupport"), Is.Null, pole.name);
                foreach (string side in new[] { "Left", "Right" })
                {
                    string fieldSide = side.ToLowerInvariant();
                    var cable = Field<Transform>(pole, fieldSide + "Cable");
                    var anchor = Field<Transform>(pole, fieldSide + "Anchor");
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
