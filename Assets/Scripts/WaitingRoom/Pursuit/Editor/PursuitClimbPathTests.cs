using System;
using System.Linq;
using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>
    /// 오르기 몸 경로가 클립 키포즈에 맞춰 손을 실제 가장자리에 두는지(상자·눕힌 원기둥 보일러), 몸이 둥근 옆면을 뚫지 않는지,
    /// 가장자리가 없으면 기존 경로로 돌아가는지 검사한다(2026-11-12 지하실 보일러 허공 잡기 보고).
    /// </summary>
    public sealed class PursuitClimbPathTests
    {
        static readonly string[] Models = { "Male_01", "Male_02", "Male_03", "Male_04", "Female_01", "Female_02", "Female_03", "Female_04", "Female_05", "Female_06" };
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        readonly Vector3 origin = new(-24000, 0, 24000);
        GameObject fixture, go;
        Collider box, boiler, wall;
        const float WallHeight = 6;
        PursuitSettings rules;

        [SetUp] public void Setup()
        {
            fixture = new GameObject("Pursuit climb path fixture");
            rules = new PursuitSettings();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(fixture.transform);
            floor.transform.position = origin + new Vector3(0, -.25f, 0); floor.transform.localScale = new Vector3(20, .5f, 20);
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube); b.transform.SetParent(fixture.transform); b.name = "상자 2.42m";
            b.transform.position = origin + new Vector3(-5, 1.21f, 1.2f); b.transform.localScale = new Vector3(2, 2.42f, 2.4f);
            box = b.GetComponent<Collider>();
            // 실제 대기실 B1 보일러: 지름 2.3m 눕힌 원기둥(CapsuleCollider, 축 X), 꼭대기 2.65m.
            var shell = new GameObject("눕힌 원기둥"); shell.transform.SetParent(fixture.transform);
            shell.transform.position = origin + new Vector3(5, 1.5f, 1.4f);
            var capsule = shell.AddComponent<CapsuleCollider>(); capsule.direction = 0; capsule.radius = 1.15f; capsule.height = 6;
            boiler = capsule;
            // 실제 대기실 팔레트 랙(약 6m)처럼 높은 벽: 2.5m 초과분은 벽 타기.
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube); w.transform.SetParent(fixture.transform); w.name = "높은 벽 6m";
            w.transform.position = origin + new Vector3(0, WallHeight / 2, 6); w.transform.localScale = new Vector3(3, WallHeight, 2);
            wall = w.GetComponent<Collider>();
            Physics.SyncTransforms();
        }

        (Vector3 from, Vector3 to) Tall => (origin + new Vector3(0, 0, 4.4f), origin + new Vector3(0, WallHeight, 5.5f));
        [TearDown] public void Cleanup() { if (fixture != null) Object.DestroyImmediate(fixture); }

        (Vector3 from, Vector3 to, Collider surface) Case(bool round) => round
            ? (origin + new Vector3(5, 0, 0), origin + new Vector3(5, 2.41f, .7f), boiler)
            : (origin + new Vector3(-5, 0, -.6f), origin + new Vector3(-5, 2.42f, .5f), box);

        PursuitActor Actor(string model)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PursuitClimbAnimationImporter.Root + "/Prefabs/Pursuit_" + model + ".prefab");
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene()); go.transform.SetParent(fixture.transform);
            return go.GetComponent<PursuitActor>();
        }

        [Test] public void FindsTheRealLipAndFallsBackWithoutOne()
        {
            var (from, to, _) = Case(false);
            var path = PursuitClimbPath.Build(from, to, null);
            Assert.IsTrue(path.Shaped); Assert.AreEqual(.6f, path.LipDistance, .06f, "상자 벽까지 0.6m");
            Assert.Less(Vector3.Distance(path.Pose(0), from), .001f); Assert.Less(Vector3.Distance(path.Pose(1), to), .001f);
            var round = Case(true);
            var arc = PursuitClimbPath.Build(round.from, round.to, null);
            Assert.IsTrue(arc.Shaped); Assert.That(arc.LipDistance, Is.InRange(.4f, .6f), "곡면에서 경사 50° 이내가 시작되는 곳");
            var air = PursuitClimbPath.Build(origin + new Vector3(0, 0, -8), origin + new Vector3(0, 1.5f, -7), null);
            Assert.IsFalse(air.Shaped, "잡을 윗면이 없으면 기존 경로");
            for (float t = 0; t <= 1; t += .1f)
                Assert.Less(Vector3.Distance(air.Pose(t), PursuitActor.ClimbPose(origin + new Vector3(0, 0, -8), origin + new Vector3(0, 1.5f, -7), true, t)), .001f);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void BodyNeverSinksIntoTheSideWhileHanging(int kind)
        {
            var (from, to, surface) = kind == 2 ? (Tall.from, Tall.to, wall) : Case(kind == 1);
            float height = to.y - from.y;
            var path = PursuitClimbPath.Build(from, to, null);
            Assert.IsTrue(path.Shaped);
            for (float t = 0; t <= 1; t += .005f) {
                var clock = PursuitClimbPath.ClockAt(t, height);
                if (!clock.OnWall && clock.Clip > PursuitClimbPath.KneeOver) break;
                var feet = path.Pose(t);
                var hits = Physics.OverlapCapsule(feet + Vector3.up * (PursuitClimbPath.BodyBottom + PursuitClimbPath.BodyRadius),
                    feet + Vector3.up * (PursuitClimbPath.BodyTop - PursuitClimbPath.BodyRadius), PursuitClimbPath.BodyRadius - .01f);
                Assert.IsFalse(hits.Contains(surface), $"{surface.name} t={t:F3} 몸 관통");
            }
        }

        [Test] public void UpTo2_5mKeepsTheVerifiedSingleMotion()
        {
            Assert.AreEqual(0, PursuitClimbPath.WallFraction(PursuitClimbPath.WallClimbFrom));
            foreach (float h in new[] { .8f, 1.5f, 2.42f, 2.5f })
                for (float t = 0; t <= 1; t += .05f) {
                    var clock = PursuitClimbPath.ClockAt(t, h);
                    Assert.AreEqual(t, clock.Clip, 1e-5f, $"{h}m t={t}"); Assert.IsFalse(clock.OnWall);
                }
            Assert.AreEqual(rules.DropSeconds * 2.5f / 1.5f, PursuitActor.TraversalSeconds(rules, false, 2.5f), 1e-4f, "2.5m까지 내려오기 시간 비례 유지");
            float tall = PursuitActor.TraversalSeconds(rules, false, 6);
            Assert.That(tall, Is.GreaterThan(rules.DropSeconds * 2.5f / 1.5f).And.LessThan(rules.DropSeconds * 6 / 1.5f), "높은 곳은 낙하처럼 완만히 늘어남");
        }

        [Test] public void TallWallClimbsAtSteadySpeedAndEndsOnTheLip()
        {
            var (from, to) = Tall;
            float height = to.y - from.y, duration = PursuitActor.TraversalSeconds(rules, true, height);
            var path = PursuitClimbPath.Build(from, to, null);
            Assert.IsTrue(path.Shaped); Assert.AreEqual(.6f, path.LipDistance, .06f);
            Assert.Less(Vector3.Distance(path.Pose(0), from), .001f); Assert.Less(Vector3.Distance(path.Pose(1), to), .001f);
            float speed = PursuitActor.ClimbReferenceHeight / rules.ClimbSeconds, previousY = from.y, wallStart = -1, wallEnd = -1;
            int cycleCount = 0; float lastCycle = -1;
            for (int i = 0; i <= 2000; i++) {
                float t = i / 2000f;
                var clock = PursuitClimbPath.ClockAt(t, height);
                var p = path.Pose(t);
                Assert.GreaterOrEqual(p.y, previousY - .001f, $"t={t:F3} 오르다 내려감");
                if (clock.OnWall) {
                    if (wallStart < 0) wallStart = p.y;
                    wallEnd = p.y;
                    float rise = (p.y - previousY) / (duration / 2000f);
                    if (clock.Wall > .03f && clock.Wall < .97f) Assert.AreEqual(speed, rise, speed * .1f, $"t={t:F3} 벽 타기 속도");
                    Assert.That(clock.Clip, Is.InRange(PursuitClimbPath.ContactClip - 1e-4f, PursuitClimbPath.PullClip + 1e-4f));
                    if (clock.Cycle != lastCycle) { cycleCount++; lastCycle = clock.Cycle; }
                }
                previousY = p.y;
            }
            Assert.AreEqual(from.y + PursuitClimbPath.WallClimbFrom - PursuitClimbPath.ContactHandHeight, wallStart, .05f, "벽 타기는 2.5m 오르기의 접촉 높이에서 시작");
            Assert.AreEqual(to.y - PursuitClimbPath.ContactHandHeight, wallEnd, .05f, "벽 타기 끝에 손이 가장자리 높이");
            Assert.AreEqual(PursuitClimbPath.WallCycles(height), cycleCount);
            Assert.AreEqual(1, PursuitClimbPath.ClipTime(1, height), 1e-5f);
            Assert.AreEqual(rules.ClimbSeconds * height / PursuitActor.ClimbReferenceHeight, duration, 1e-4f, "벽 타기도 같은 속도");
        }

        [TestCaseSource(nameof(Models))]
        public void HandsStayOnTheEdgeUntilKneeOverOnBoxAndLyingCylinder(string model)
        {
            var actor = Actor(model);
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            foreach (bool round in new[] { false, true }) {
                var (from, to, surface) = Case(round);
                go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(Vector3.ProjectOnPlane(to - from, Vector3.up)));
                var path = PursuitClimbPath.Build(from, to, go.transform);
                var ik = new PursuitClimbIk(go.transform, skin);
                ik.Begin(from, to, true, rules);
                var hands = ((Array)typeof(PursuitClimbIk).GetField("limbs", F).GetValue(ik)).Cast<object>()
                    .Where(l => l != null && (bool)l.GetType().GetField("hand").GetValue(l)).ToArray();
                Assert.Greater(hands.Length, 0);
                float sum = 0, worst = 0; int count = 0;
                for (int f = 0; f <= 60; f++) {
                    float t = f / 60f;
                    ik.Restore();
                    go.transform.position = path.Pose(t);
                    actor.Clips[7].SampleAnimation(actor.Motion.gameObject, t * actor.Clips[7].length);
                    ik.Apply(t, rules);
                    Assert.Less(ik.MaxContactError, .01f);
                    if (t < .14f || t > .58f) continue;
                    foreach (var hand in hands) {
                        var end = (Transform)hand.GetType().GetField("end").GetValue(hand);
                        var point = end.TransformPoint((Vector3)hand.GetType().GetField("contact").GetValue(hand));
                        float gap = Vector3.Distance(surface.ClosestPoint(point), point);
                        sum += gap; worst = Mathf.Max(worst, gap); count++;
                    }
                }
                ik.Clear();
                string label = $"{model} {(round ? "눕힌 원기둥" : "상자")}";
                Assert.Less(sum / count, .05f, label + " 짚는 동안 손이 표면에서 떨어짐(허공 잡기)");
                Assert.Less(worst, .3f, label + " 최대 손-표면 거리");
            }
        }

        [TestCaseSource(nameof(Models))]
        public void HandsPlantOnTheWallWhileScalingAndOnTheLipAfter(string model)
        {
            var actor = Actor(model);
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            var (from, to) = Tall;
            float height = to.y - from.y;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(Vector3.ProjectOnPlane(to - from, Vector3.up)));
            var path = PursuitClimbPath.Build(from, to, go.transform);
            var ik = new PursuitClimbIk(go.transform, skin);
            ik.Begin(from, to, true, rules);
            var hands = ((Array)typeof(PursuitClimbIk).GetField("limbs", F).GetValue(ik)).Cast<object>()
                .Where(l => l != null && (bool)l.GetType().GetField("hand").GetValue(l)).ToArray();
            float wallSum = 0, lipSum = 0; int wallCount = 0, lipCount = 0, planted = 0;
            for (int f = 0; f <= 600; f++) {
                float t = f / 600f;
                var clock = PursuitClimbPath.ClockAt(t, height);
                ik.Restore();
                go.transform.position = path.Pose(t);
                actor.Clips[7].SampleAnimation(actor.Motion.gameObject, clock.Clip * actor.Clips[7].length);
                ik.Apply(t, rules);
                Assert.Less(ik.MaxContactError, .01f);
                bool onWall = clock.OnWall && clock.Phase > .1f && clock.Phase < .36f;
                bool onLip = !clock.OnWall && clock.Clip > .14f && clock.Clip < .58f && t > .5f;
                if (!onWall && !onLip) continue;
                if (onWall) planted += ik.PlantedHands;
                foreach (var hand in hands) {
                    var end = (Transform)hand.GetType().GetField("end").GetValue(hand);
                    var point = end.TransformPoint((Vector3)hand.GetType().GetField("contact").GetValue(hand));
                    float gap = Vector3.Distance(wall.ClosestPoint(point), point);
                    if (onWall) { wallSum += gap; wallCount++; } else { lipSum += gap; lipCount++; }
                }
            }
            ik.Clear();
            Assert.Greater(wallCount, 0); Assert.Greater(lipCount, 0);
            Assert.Greater(planted, wallCount / 2, $"{model} 벽 타기 중 손이 벽에 고정되지 않음");
            Assert.Less(wallSum / wallCount, .05f, $"{model} 벽 타기 중 손이 벽에서 떨어짐");
            Assert.Less(lipSum / lipCount, .05f, $"{model} 벽 타기 뒤 손이 가장자리에서 떨어짐");
        }
    }
}
