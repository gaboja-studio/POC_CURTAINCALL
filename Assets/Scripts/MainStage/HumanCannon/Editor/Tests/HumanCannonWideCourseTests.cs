using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class HumanCannonWideCourseTests
    {
        Scene scene, previous;
        bool openedHere;
        GameObject[] roots;

        [OneTimeSetUp]
        public void SetUp()
        {
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.GetSceneByPath(HumanCannonTestSceneBuilder.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(HumanCannonTestSceneBuilder.ScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            roots = scene.GetRootGameObjects();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void GateCrossing_HasNoFanBladeFromGrabApproachThroughExit()
        {
            var fans = All<HumanCannonHazard>().Where(h => h.Kind == CannonHazardKind.RotatingBlades).ToArray();
            foreach (var pole in All<HumanCannonPole>().Where(p => p.LinkedGate != null))
            {
                Assert.That(All<HumanCannonPoleSaw>().Count(s => s.Pole == pole), Is.EqualTo(1), "해제 봉 왕복 톱 유지");
                float start = pole.Grip.position.z - 2f, end = pole.LinkedGate.transform.position.z + 3f;
                foreach (var fan in fans)
                    for (int tick = 0; tick <= 80; tick++)
                        for (float z = start; z <= end; z += .25f)
                        {
                            var feet = pole.HangingPosition(0); feet.z = z;
                            Assert.IsFalse(fan.Touches(feet, 1.5f, .25f, tick * .1, 1f),
                                $"{pole.name}: 관문 접근/통과 공간에 {fan.name}이 겹침 (z={z})");
                        }
            }
        }

        [Test]
        public void MovingBlades_AreFlatAndTravelOnlyAcrossTheCourse()
        {
            foreach (var hazard in All<HumanCannonHazard>().Where(h => h.Kind != CannonHazardKind.SpikeMace))
            {
                Assert.That(Mathf.Abs(Vector3.Dot(hazard.BladeNormal, Vector3.up)), Is.GreaterThan(.999f), hazard.name);
                Assert.That(Mathf.Abs(hazard.Travel.x), Is.EqualTo(24f).Within(.001f), hazard.name);
                Assert.That(new Vector2(hazard.Travel.y, hazard.Travel.z).magnitude, Is.LessThan(.001f), hazard.name);
                var visual = (Transform)typeof(HumanCannonHazard).GetField("visual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hazard);
                Assert.That(Mathf.Abs(Vector3.Dot(visual.forward, hazard.BladeNormal)), Is.GreaterThan(.999f), "외형/판정 일치: " + hazard.name);
            }
        }

        [Test]
        public void GroundSaws_MatchAirbornePoleSawsAtFourSecondCycles()
        {
            var ground = All<HumanCannonHazard>().Where(h => h.Kind == CannonHazardKind.GroundSaw
                && h.GetComponent<HumanCannonPoleSaw>() == null).ToArray();
            Assert.That(ground, Has.Length.EqualTo(8));
            foreach (var saw in ground)
            {
                Assert.That(saw.PeriodSeconds, Is.EqualTo(4f), saw.name);
                Assert.That(Mathf.Abs(saw.Travel.x), Is.EqualTo(24f).Within(.001f), saw.name);
                Assert.That(Vector3.Distance(saw.Position(1.3, 1), saw.Position(5.3, 1)), Is.LessThan(.001f));
            }
            foreach (var saw in All<HumanCannonPoleSaw>()) Assert.That(saw.Hazard.PeriodSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void WideFloor_HasNoSideWallInsideOrAtTheEdge()
        {
            var floors = All<Transform>().Where(t => t.name == "FirstCourseFloor" || t.name == "SecondCourseFloor");
            foreach (var floor in floors)
            {
                var solids = floor.GetComponentsInChildren<BoxCollider>().Where(c => c.enabled).ToArray();
                Assert.That(solids.Min(c => c.bounds.min.x), Is.EqualTo(-22f).Within(.001f));
                Assert.That(solids.Max(c => c.bounds.max.x), Is.EqualTo(22f).Within(.001f));
            }
            Assert.That(All<Transform>().Where(t => t.name == "SideFences"), Is.Empty);
            Assert.That(All<HumanCannonSawGate>().Length, Is.EqualTo(2), "봉으로 해제하는 톱 관문은 유지");
        }

        [TestCase(CannonHazardKind.GroundSaw)]
        [TestCase(CannonHazardKind.RotatingBlades)]
        public void FlatMovingBlade_VisualAndDamageFollowTheSameMovingCenter(CannonHazardKind kind)
        {
            var root = new GameObject("MovingBladeTest");
            try
            {
                var hazard = root.AddComponent<HumanCannonHazard>();
                var visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false);
                Set(hazard, "kind", kind); Set(hazard, "visual", visual);
                root.transform.position = Vector3.up;
                hazard.ConfigureHorizontalSweep(Vector3.right * 24, 8);
                hazard.Present(2, 1);
                Assert.That(Vector3.Distance(visual.position, new Vector3(24, 1, 0)), Is.LessThan(.001f));
                Assert.That(Mathf.Abs(Vector3.Dot(visual.forward, Vector3.up)), Is.GreaterThan(.999f));
                Assert.IsTrue(hazard.Touches(new Vector3(24, 0, 0), 1.5f, .25f, 2, 1));
                Assert.IsFalse(hazard.Touches(Vector3.zero, 1.5f, .25f, 2, 1), "과거 원점에 보이지 않는 판정을 남기지 않는다.");
                Assert.IsFalse(hazard.Touches(new Vector3(24, 1.5f, 0), 1.5f, .25f, 2, 1), "수평 톱보다 위로 지나가면 회피");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Set(object target, string field, object value) => target.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        T[] All<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    }
}
