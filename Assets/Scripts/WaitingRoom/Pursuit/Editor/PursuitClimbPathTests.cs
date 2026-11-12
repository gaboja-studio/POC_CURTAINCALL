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
        Collider box, boiler;
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
            Physics.SyncTransforms();
        }
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

        [TestCase(false)] [TestCase(true)]
        public void BodyNeverSinksIntoTheSideWhileHanging(bool round)
        {
            var (from, to, surface) = Case(round);
            var path = PursuitClimbPath.Build(from, to, null);
            for (float t = 0; t <= PursuitClimbPath.KneeOver + .001f; t += .02f) {
                var feet = path.Pose(t);
                var hits = Physics.OverlapCapsule(feet + Vector3.up * (PursuitClimbPath.BodyBottom + PursuitClimbPath.BodyRadius),
                    feet + Vector3.up * (PursuitClimbPath.BodyTop - PursuitClimbPath.BodyRadius), PursuitClimbPath.BodyRadius - .01f);
                Assert.IsFalse(hits.Contains(surface), $"{surface.name} t={t:F2} 몸 관통");
            }
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
    }
}
