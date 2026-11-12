using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>
    /// 2026-11-12 사용자 보고(좁은 배관 위 못 오름, 계단 난간에 비비며 달림, 막힌 벽 안으로 들어감)에서 찾은 원인별 회귀 검사.
    /// 실제 대기실 140개 물체 플레이 조사: 내려오기가 물체 몸통을 사선으로 지남, 좁은 윗면 착지점 없음, 가는 선 검사로 몸이 겹친 채 오름,
    /// 막힌 접근 지점을 고수함.
    /// </summary>
    public sealed class PursuitTraversalSafetyTests
    {
        static readonly Vector3 Origin = new(-28000, 0, 28000);
        NavMeshData data;
        NavMeshDataInstance mesh;
        GameObject root;
        readonly Dictionary<string, Collider> colliders = new();

        [SetUp] public void Setup()
        {
            root = new GameObject("traversal-safety-fixture");
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .5f; settings.agentHeight = 2; settings.agentClimb = .75f;
            var sources = new List<NavMeshBuildSource>();
            void Box(string name, Vector3 center, Vector3 size, bool nav = true, Quaternion? rotation = null)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
                var r = rotation ?? Quaternion.identity;
                box.transform.SetParent(root.transform); box.transform.SetPositionAndRotation(Origin + center, r); box.transform.localScale = size;
                colliders[name] = box.GetComponent<Collider>();
                if (nav) sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(Origin + center, r, Vector3.one), size = size });
            }
            Box("바닥", new Vector3(0, -.25f, 0), new Vector3(60, .5f, 60));
            Box("캐비닛 1.9m", new Vector3(0, .95f, 0), new Vector3(1.2f, 1.9f, 1.2f));
            Box("난간 윗면(폭 8cm)", new Vector3(8, .55f, 0), new Vector3(3, 1.1f, .08f), false);
            Box("오를 상자 1.2m", new Vector3(-8, .6f, 0), new Vector3(1.2f, 1.2f, 1.2f));
            Box("머리 옆 가로대", new Vector3(-7.83f, 1.75f, -1), new Vector3(.1f, .1f, 1), false);
            // 높은 단: 앞쪽은 벽, 뒤쪽은 경사로로 올라간다(다른 루트).
            Box("높은 단 2m", new Vector3(0, 1, 15), new Vector3(6, 2, 4));
            Box("경사로", new Vector3(0, .95f, 20), new Vector3(2, .2f, 6.4f), true, Quaternion.Euler(18.5f, 0, 0));
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Origin + Vector3.up * 2, new Vector3(62, 10, 62)), Vector3.zero, Quaternion.identity);
            mesh = NavMesh.AddNavMeshData(data);
            Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup()
        {
            mesh.Remove();
            if (data != null) Object.DestroyImmediate(data);
            if (root != null) Object.DestroyImmediate(root);
            colliders.Clear();
        }

        static bool BodyHits(Vector3 feet, Collider c) => Physics.OverlapCapsule(feet + Vector3.up * .45f, feet + Vector3.up * 1.3f, .17f).Contains(c);

        [Test] public void DropWalksToTheEdgeThenFallsOutsideInsteadOfThroughTheBody()
        {
            var cabinet = colliders["캐비닛 1.9m"];
            var from = Origin + new Vector3(0, 1.9f, 0); var to = Origin + new Vector3(0, 0, -1.1f);
            bool oldHits = false;
            for (float t = 0; t <= 1; t += .01f) oldHits |= BodyHits(PursuitActor.ClimbPose(from, to, false, t), cabinet);
            Assert.IsTrue(oldHits, "이전 직선 내려오기는 캐비닛 몸통을 지났다(재현)");
            var path = PursuitClimbPath.BuildDrop(from, to, null, .6f);
            Assert.AreEqual(.6f, path.DropEdge, .06f, "가운데에서 가장자리까지 0.6m 걷는다");
            Assert.Greater(path.DropWalk, 0); Assert.AreEqual(.6f + .6f / PursuitClimbPath.DropWalkSpeed, path.Seconds, .03f);
            for (float t = 0; t <= 1; t += .005f) Assert.IsFalse(BodyHits(path.Pose(t), cabinet), $"t={t:F3} 몸통 관통");
            Assert.Less(Vector3.Distance(path.Pose(1), to), .001f);
            Assert.AreEqual(0, PursuitClimbPath.DropPhase(path.DropWalk * .5f, path.DropWalk), "걷는 동안 뛰어내리기 클립은 시작 전");
        }

        [Test] public void NarrowRailTopIsClimbable()
        {
            var goal = Origin + new Vector3(8, 1.1f, 0); var feet = Origin + new Vector3(8, 0, -.9f);
            Assert.AreEqual(PursuitDirectClimb.Plan.Climb, PursuitDirectClimb.Find(feet, goal, null, float.PositiveInfinity, NavMesh.AllAreas, out var landing),
                "폭 8cm 윗면: 가장자리 안쪽 0.15~0.45m 착지점이 없어도 대상이 선 자리로 오른다");
            Assert.AreEqual(goal.y, landing.y, .05f);
        }

        [Test] public void ClimbColumnUsesBodyWidthNotAThinLine()
        {
            var feet = Origin + new Vector3(-8, 0, -1); var goal = Origin + new Vector3(-8, 1.2f, 0);
            var bar = colliders["머리 옆 가로대"];
            var column = new Vector3(feet.x, goal.y + 1, feet.z);
            Assert.IsTrue(PursuitDirectClimb.Open(feet + Vector3.up * .45f, column, null), "가는 선은 0.17m 옆 가로대를 못 본다(재현)");
            Assert.IsFalse(PursuitDirectClimb.BodyOpen(feet + Vector3.up * PursuitDirectClimb.BodyLift, column, null), "몸 두께로는 막힘");
            Assert.AreNotEqual(PursuitDirectClimb.Plan.Climb, PursuitDirectClimb.Find(feet, goal, null, float.PositiveInfinity, NavMesh.AllAreas, out _), "가로대 아래에서는 바로 오르지 않는다");
            bar.enabled = false;
            Assert.AreEqual(PursuitDirectClimb.Plan.Climb, PursuitDirectClimb.Find(feet, goal, null, float.PositiveInfinity, NavMesh.AllAreas, out _));
        }

        [Test] public void DropFromATopFindsFloorBelowWithClearSpace()
        {
            var top = Origin + new Vector3(0, 1.9f, 0);
            Assert.IsTrue(PursuitDirectClimb.FindDrop(top, null, NavMesh.AllAreas, null, out var landing));
            Assert.Less(landing.y, top.y - .3f, "항상 아래로");
            Assert.IsTrue(PursuitDirectClimb.DropClear(top, landing, null));
            Assert.Less(new Vector2(landing.x - top.x, landing.z - top.z).magnitude, 3.5f);
        }

        [Test] public void BlockedApproachFindsAnotherRouteToTheTarget()
        {
            // 높은 단 앞·옆 바닥 접근이 모두 막힘 → 경로가 이어지는 단 위(경사로로 도는 루트)를 고른다.
            var from = Origin + new Vector3(0, 0, 11.5f);
            Assert.IsTrue(NavMesh.SamplePosition(from, out var start, 1, NavMesh.AllAreas));
            var goal = Origin + new Vector3(0, 2, 13.4f);
            bool Blocked(Vector3 p) => p.y < Origin.y + 1 && p.z < Origin.z + 17.5f;
            Assert.IsTrue(PursuitActor.FindDetour(start.position, goal, NavMesh.AllAreas, Blocked, null, new NavMeshPath(), out var detour), "다른 접근 지점");
            Assert.AreEqual(goal.y, detour.y, .3f, "단 위(대상과 같은 높이)로 우회");
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(start.position, detour, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
            Assert.Greater(PursuitNavigation.Length(path.corners), 8, "경사로를 돌아가는 루트");
            Assert.IsFalse(PursuitActor.FindDetour(start.position, goal, NavMesh.AllAreas, _ => true, null, new NavMeshPath(), out _), "모두 막히면 우회 없음");
        }
    }
}
