using System.Linq;
using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
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
        public void GateCrossing_KeepsBothCooperationGatesWithoutRemovedFanBladesOrPoleSaws()
        {
            // Task-044 2026-10-22: 지상 톱/관문 벽은 남기고 공중 장애물은 삭제한다.
            Assert.That(All<HumanCannonHazard>().Where(h => h.Kind == CannonHazardKind.RotatingBlades), Is.Empty);
            Assert.That(All<HumanCannonPoleSaw>(), Is.Empty);
            var linked = All<HumanCannonPole>().Where(p => p.LinkedGate != null).ToArray();
            Assert.That(linked, Has.Length.EqualTo(2));
            CollectionAssert.AreEquivalent(All<HumanCannonSawGate>(), linked.Select(p => p.LinkedGate));
            foreach (var pole in linked)
            {
                Assert.That(pole.HoldSeconds, Is.EqualTo(40f), pole.name);
                Assert.That(pole.LinkedGate.transform.position.z - pole.Grip.position.z, Is.EqualTo(3f).Within(.001f), pole.name);
            }
        }

        [Test]
        public void MovingBlades_AreFlatAndDiagonalExceptForPoleSaws()
        {
            foreach (var hazard in All<HumanCannonHazard>().Where(h => h.Kind != CannonHazardKind.SpikeMace))
            {
                Assert.That(Mathf.Abs(Vector3.Dot(hazard.BladeNormal, Vector3.up)), Is.GreaterThan(.999f), hazard.name);
                Assert.That(Mathf.Abs(hazard.Travel.x), Is.EqualTo(24f).Within(.001f), hazard.name);
                Assert.That(Mathf.Abs(hazard.Travel.y), Is.LessThan(.001f), hazard.name);
                float angle = Mathf.Atan2(Mathf.Abs(hazard.Travel.z), Mathf.Abs(hazard.Travel.x)) * Mathf.Rad2Deg;
                // 직접 그린 레일(1구간 첫 톱벽 앞)은 공용 30° 대각선 대신 작성한 각도를 쓴다.
                if (!hazard.UsesAuthoredPath)
                    Assert.That(angle, Is.EqualTo(hazard.IsPoleSaw ? 0f : 30f).Within(.001f), hazard.name);
                var visual = (Transform)typeof(HumanCannonHazard).GetField("visual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hazard);
                Assert.That(Mathf.Abs(Vector3.Dot(visual.forward, hazard.BladeNormal)), Is.GreaterThan(.999f), "외형/판정 일치: " + hazard.name);
            }
        }

        [Test]
        public void GroundSaws_HaveDistinctRailsAndSpacedRoutesAtFourSecondCycles()
        {
            var ground = All<HumanCannonHazard>().Where(h => h.Kind == CannonHazardKind.GroundSaw
                && h.GetComponent<HumanCannonPoleSaw>() == null).ToArray();
            Assert.That(ground, Has.Length.EqualTo(12));
            foreach (var saw in ground)
            {
                Assert.That(saw.PeriodSeconds, Is.EqualTo(4f), saw.name);
                Assert.That(Mathf.Abs(saw.Travel.x), Is.EqualTo(24f).Within(.001f), saw.name);
                Assert.That(Vector3.Distance(saw.Position(1.3, 1), saw.Position(5.3, 1)), Is.LessThan(.001f));
                var rail = saw.GetComponent<LineRenderer>();
                Assert.That(rail, Is.Not.Null, saw.name);
                Assert.That(rail.sharedMaterial, Is.Not.Null, saw.name);
                Assert.That(AssetDatabase.GetAssetPath(rail.sharedMaterial), Is.EqualTo(
                    "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/ground-saw-rail.mat"), saw.name);
                var color = rail.sharedMaterial.color;
                Assert.That(color.g, Is.GreaterThan(color.r), "지상 경로는 빨강보다 청록 성분이 강해야 한다: " + saw.name);
                Assert.That(color.b, Is.GreaterThan(color.r), saw.name);
            }
            for (int section = 0; section < 2; section++)
            {
                var saws = ground.Where(s => (s.transform.position.z < 90f) == (section == 0))
                    .OrderBy(s => s.transform.position.z).ToArray();
                Assert.That(saws, Has.Length.EqualTo(6), "구간마다 지상 톱 여섯 개");
                foreach (var saw in saws)
                {
                    // 2026-10-22 부상별 회피 보완: 중심 .3m, 톱 상단 .42m < 최저 생존 점프 .6m.
                    Assert.That(saw.transform.position.y, Is.EqualTo(section * 6f + .3f).Within(.001f), saw.name);
                    float top = saw.transform.position.y - section * 6f + (float)typeof(HumanCannonHazard)
                        .GetField("bladeHalfThickness", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(saw);
                    Assert.That(top, Is.EqualTo(.42f).Within(.001f), saw.name);
                    Assert.That(top, Is.LessThan(GameSettings.HumanCannon.JumpHeight * GameSettings.Base.BodyDamage.JumpHeightForLostLegs(2)), saw.name);
                }
                for (int i = 1; i < saws.Length; i++)
                    Assert.That(saws[i].transform.position.z - saws[i - 1].transform.position.z,
                        Is.GreaterThanOrEqualTo(5.5f - .001f), "지상 톱 사이 이동·점프 여유");
                var transport = All<HumanCannonTransport>().Single(t => t.name ==
                    (section == 0 ? "DepartureTransport" : "ShelterDeparture"));
                float front = transport.Position(1f).z
                    + Mathf.Abs((transport.Rotation * Vector3.right).z) * transport.HalfSize.x
                    + Mathf.Abs((transport.Rotation * Vector3.forward).z) * transport.HalfSize.y;
                Assert.That(saws[0].transform.position.z - front, Is.GreaterThanOrEqualTo(2f - .001f),
                    "운송 도착 발판 앞에서 첫 경로까지 2m 확보");
                Assert.That(saws[0].transform.position.z - saws[0].Radius - front - .25f,
                    Is.GreaterThan(0f), "첫 톱의 판정이 도착 발판 위 플레이어에게 닿지 않아야 한다");
                Assert.That(saws[^1].transform.position.z, Is.EqualTo(61f + section * 76f).Within(.001f));
            }
            Assert.That(All<HumanCannonPoleSaw>(), Is.Empty, "지상 톱 경로에 삭제한 공중 봉 톱을 다시 등록하지 않는다.");
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
                float diagonalZ = -24f * Mathf.Tan(30f * Mathf.Deg2Rad);
                Assert.That(Vector3.Distance(visual.position, new Vector3(24, 1, diagonalZ)), Is.LessThan(.001f));
                Assert.That(Mathf.Abs(Vector3.Dot(visual.forward, Vector3.up)), Is.GreaterThan(.999f));
                Assert.IsTrue(hazard.Touches(new Vector3(24, 0, diagonalZ), 1.5f, .25f, 2, 1));
                Assert.IsFalse(hazard.Touches(Vector3.zero, 1.5f, .25f, 2, 1), "과거 원점에 보이지 않는 판정을 남기지 않는다.");
                Assert.IsFalse(hazard.Touches(new Vector3(24, 1.5f, diagonalZ), 1.5f, .25f, 2, 1), "수평 톱보다 위로 지나가면 회피");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Set(object target, string field, object value) => target.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        T[] All<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    }
}
