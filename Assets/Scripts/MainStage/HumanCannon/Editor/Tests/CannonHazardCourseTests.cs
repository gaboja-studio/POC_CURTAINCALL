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
            // Task-044 exception.md(2026-10-22): 지상 장애물/전기톱 벽 외 공중 장애물 제거.
            Assert.That(poleSaws, Is.Empty, "삭제한 봉 왕복 톱은 비활성 사본도 남기지 않는다.");
            Assert.That(hazards, Has.Length.EqualTo(12));
            Assert.That(hazards, Has.All.Not.Null);
            CollectionAssert.AllItemsAreUnique(hazards);
            CollectionAssert.AreEquivalent(All<HumanCannonHazard>(), hazards);
            Assert.That(hazards.Count(h => h.Kind == CannonHazardKind.SpikeMace), Is.Zero);
            Assert.That(hazards.Count(h => h.Kind == CannonHazardKind.RotatingBlades), Is.Zero);
            Assert.That(hazards.Count(h => h.Kind == CannonHazardKind.GroundSaw), Is.EqualTo(12));
            Assert.That(poles, Has.Length.EqualTo(7));
            foreach (var pole in poles)
                Assert.That(pole.HoldSeconds, Is.EqualTo(pole.LinkedGate != null ? 40f : 0f), pole.name);
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
        public void FiveRestPoles_AreDistributedAwayFromCannonsWhileBothGatePolesStayInPlace()
        {
            var trampoline = All<HumanCannonTrampoline>().Single();
            float apex = Field<float>(trampoline, "surfaceHeight") +
                trampoline.UpwardSpeed * trampoline.UpwardSpeed / (2f * GameSettings.Base.Character.Gravity);
            var all = All<HumanCannonPole>();
            Assert.That(all, Has.Length.EqualTo(7));
            var ordinary = all.Where(p => p.LinkedGate == null).OrderBy(p => p.Grip.position.z).ToArray();
            // Task-044 2026-10-22 후속 분산 결정(History/Harness-v1/plan.md).
            var expected = new[] { new Vector3(-14, apex - .5f, 34), new Vector3(17, apex - .5f, 65),
                new Vector3(-16, 6 + apex - .5f, 107), new Vector3(12, 6 + apex - .5f, 136),
                new Vector3(-4, 6.2f + apex - .5f, 174) };
            Assert.That(ordinary, Has.Length.EqualTo(expected.Length));
            for (int i = 0; i < ordinary.Length; i++)
            {
                Assert.That(Vector3.Distance(ordinary[i].Grip.position, expected[i]), Is.LessThan(.001f), ordinary[i].name);
                Assert.That(All<HumanCannonStation>().Min(station => Vector3.Distance(station.transform.position, ordinary[i].Grip.position)),
                    Is.GreaterThan(17f), "휴식 봉을 대포 가까이에 다시 모으지 않는다: " + ordinary[i].name);
            }
            var linked = all.Where(p => p.LinkedGate != null).OrderBy(p => p.Grip.position.z).ToArray();
            Assert.That(linked, Has.Length.EqualTo(2));
            for (int section = 0; section < 2; section++)
            {
                Assert.That(linked[section].Grip.position.x, Is.EqualTo(0f).Within(.001f));
                Assert.That(linked[section].Grip.position.z, Is.EqualTo(44f + section * 76f).Within(.001f));
                Assert.That(linked[section].Grip.position.y, Is.EqualTo(section * 6f + apex - .5f).Within(.001f));
            }
        }

        [Test]
        public void AllSevenPoles_HaveTheirOwnUpperCablesAndAnchorsWithoutFloorSupports()
        {
            var extension = roots.Single(root => root.name == "ExtendedCourse044").transform;
            Assert.That(extension.Find("FirstCeiling"), Is.Null);
            Assert.That(extension.Find("SecondCeiling"), Is.Null);
            var poles = Field<HumanCannonPole[]>(All<HumanCannonPoleSystem>().Single(), "poles");
            Assert.That(poles, Has.Length.EqualTo(7));
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

        [Test]
        public void CurrentBuildUsesSixtyPercentGroundSawDensityWithoutRestoringRemovedAirHazards()
        {
            var settings = GameSettings.HumanCannon;
            Assert.That(settings.SawDensity, Is.EqualTo(.6f));
            var course = All<HumanCannonHazardCourse>().Single();
            Assert.That(course.HazardCount, Is.EqualTo(12));
            Assert.That(All<HumanCannonPoleSaw>(), Is.Empty);
            int ground = 0, authored = 0;
            for (int i = 0; i < course.HazardCount; i++)
            {
                var hazard = course.Hazard(i);
                Assert.That(hazard.IsPoleSaw, Is.False, hazard.name);
                Assert.That(hazard.Kind, Is.EqualTo(CannonHazardKind.GroundSaw), hazard.name);
                if (hazard.UsesAuthoredPath)
                {
                    // 1구간 첫 톱벽 앞 3개는 직접 그린 레일로 항상 켜져 있다.
                    Assert.That(course.IsHazardEnabled(i), Is.True, hazard.name);
                    authored++;
                }
                else if (course.IsHazardEnabled(i)) ground++;
            }
            Assert.That(authored, Is.EqualTo(3));
            Assert.That(ground, Is.EqualTo(6));
        }

        T[] All<T>() where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
