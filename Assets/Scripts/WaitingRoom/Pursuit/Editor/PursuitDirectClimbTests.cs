using System.Collections.Generic;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>
    /// NavMesh가 없는 소품 윗면(실제 대기실의 드럼통·상자·침대처럼 좁은 윗면) 직접 오르기.
    /// 실제 대기실 베이크(반경 0.5m)는 폭 1.6m 미만 윗면에 NavMesh를 만들지 않는다.
    /// </summary>
    public sealed class PursuitDirectClimbTests
    {
        static readonly Vector3 Origin = new(-26000, 0, 26000);
        NavMeshData data;
        NavMeshDataInstance mesh;
        GameObject root;
        int mask;

        [SetUp] public void Setup()
        {
            root = new GameObject("direct-climb-fixture");
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .5f; settings.agentHeight = 2; settings.agentClimb = .75f;
            var sources = new List<NavMeshBuildSource>();
            void Box(string name, Vector3 center, Vector3 size, bool nav = true)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
                box.transform.SetParent(root.transform); box.transform.position = Origin + center; box.transform.localScale = size;
                if (nav) sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(Origin + center, Quaternion.identity, Vector3.one), size = size });
            }
            Box("바닥", new Vector3(0, -.25f, 0), new Vector3(30, .5f, 30));
            Box("드럼통(1.94m, 윗면 NavMesh 없음)", new Vector3(0, .97f, 0), new Vector3(1.2f, 1.94f, 1.2f));
            Box("너무 높은 선반(3.2m)", new Vector3(8, 1.6f, 0), new Vector3(1.2f, 3.2f, 1.2f));
            Box("천장 낮은 상자(1.2m)", new Vector3(-8, .6f, 0), new Vector3(1.2f, 1.2f, 1.2f));
            Box("낮은 천장", new Vector3(-8, 2.2f, 0), new Vector3(6, .2f, 6), false);
            Box("한쪽이 배관에 막힌 상자(1m)", new Vector3(0, .5f, -8), new Vector3(1.2f, 1, 1.2f));
            Box("상자 앞 낮은 배관", new Vector3(0, 1.6f, -9.1f), new Vector3(2, .2f, .8f), false);
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Origin + Vector3.up * 2, new Vector3(32, 10, 32)), Vector3.zero, Quaternion.identity);
            mesh = NavMesh.AddNavMeshData(data);
            mask = NavMesh.AllAreas;
            Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup()
        {
            mesh.Remove();
            if (data != null) Object.DestroyImmediate(data);
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test] public void NarrowTopHasNoNavMeshLikeTheWaitingRoom()
        {
            Assert.IsFalse(NavMesh.SamplePosition(Origin + new Vector3(0, 1.94f, 0), out var hit, .4f, mask) && hit.position.y > Origin.y + 1.5f,
                "실제 대기실처럼 좁은 윗면에는 NavMesh가 없어야 링크 방식으로 오를 수 없다.");
        }

        [Test] public void ClimbsOntoNarrowTopFromBesideTheEdge()
        {
            var goal = Origin + new Vector3(0, 1.94f, .2f);
            var feet = Origin + new Vector3(0, 0, -1.1f);
            var plan = PursuitDirectClimb.Find(feet, goal, null, 2.5f, mask, out var landing);
            Assert.AreEqual(PursuitDirectClimb.Plan.Climb, plan);
            Assert.AreEqual(Origin.y + 1.94f, landing.y, .05f, "착지점은 드럼통 윗면");
            Assert.Less(Mathf.Abs(landing.x - Origin.x), .61f); Assert.Less(Mathf.Abs(landing.z - Origin.z), .61f);
        }

        [Test] public void FarFromEdgeWalksToTheEdgeFirst()
        {
            var goal = Origin + new Vector3(0, 1.94f, 0);
            var feet = Origin + new Vector3(0, 0, -3f);
            Assert.AreEqual(PursuitDirectClimb.Plan.Approach, PursuitDirectClimb.Find(feet, goal, null, 2.5f, mask, out var stand));
            Assert.AreEqual(Origin.y, stand.y, .1f);
            Assert.Less(Vector3.Distance(new Vector3(stand.x, 0, stand.z), new Vector3(Origin.x, 0, Origin.z - .6f)), 1.2f, "가장자리 앞 바닥까지 걸어간다");
        }

        [Test] public void RejectsTooHighCoveredOrHeightRuleExceeded()
        {
            Assert.AreEqual(PursuitDirectClimb.Plan.None, PursuitDirectClimb.Find(Origin + new Vector3(8, 0, -1.1f), Origin + new Vector3(8, 3.2f, 0), null, 2.5f, mask, out _), "최대 높이 초과");
            Assert.AreEqual(PursuitDirectClimb.Plan.None, PursuitDirectClimb.Find(Origin + new Vector3(-8, 0, -1.1f), Origin + new Vector3(-8, 1.2f, 0), null, 2.5f, mask, out _), "윗면에 설 머리 공간 없음");
            Assert.AreEqual(PursuitDirectClimb.Plan.None, PursuitDirectClimb.Find(Origin + new Vector3(0, 0, -1.1f), Origin + new Vector3(0, 1.94f, 0), null, 1.5f, mask, out _), "규칙 높이 낮추면 오르지 않음");
            Assert.AreEqual(PursuitDirectClimb.Plan.None, PursuitDirectClimb.Find(Origin + new Vector3(0, 0, -1.1f), Origin + new Vector3(0, 2.6f, 0), null, 2.5f, mask, out _), "공중(점프 중) 대상은 오르지 않음");
        }

        [Test] public void BlockedSideGoesAroundToAnOpenEdge()
        {
            var goal = Origin + new Vector3(0, 1, -8);
            var feet = Origin + new Vector3(0, 0, -9.15f);
            Assert.AreEqual(PursuitDirectClimb.Plan.Approach, PursuitDirectClimb.Find(feet, goal, null, 2.5f, mask, out var stand), "배관 아래에서는 못 오르고 다른 쪽으로 돈다");
            Assert.Greater(Vector3.Distance(stand, feet), .3f);
            Assert.AreEqual(PursuitDirectClimb.Plan.Climb, PursuitDirectClimb.Find(stand, goal, null, 2.5f, mask, out var landing));
            Assert.AreEqual(Origin.y + 1, landing.y, .05f);
        }

        [Test] public void DropsBackToFloorNavMesh()
        {
            var top = Origin + new Vector3(0, 1.94f, 0);
            Assert.IsTrue(PursuitDirectClimb.FindDrop(top, Origin + new Vector3(0, 0, -2), mask, null, out var landing));
            Assert.AreEqual(Origin.y, landing.y, .1f);
            Assert.Less(new Vector2(landing.x - top.x, landing.z - top.z).magnitude, 3.5f);
        }

        [Test] public void UnlimitedHeightClimbsTheTallShelfAndSettingCanCapIt()
        {
            var feet = Origin + new Vector3(8, 0, -1.1f); var goal = Origin + new Vector3(8, 3.2f, 0);
            var rules = new PursuitSettings();
            Assert.AreEqual(PursuitDirectClimb.Plan.Climb, PursuitDirectClimb.Find(feet, goal, null, rules.ResolvedClimbMaxHeight, mask, out var landing), "기본값 0 = 높이 제한 없음");
            Assert.AreEqual(Origin.y + 3.2f, landing.y, .05f);
            rules.ClimbMaxHeight = 3;
            Assert.AreEqual(PursuitDirectClimb.Plan.None, PursuitDirectClimb.Find(feet, goal, null, rules.ResolvedClimbMaxHeight, mask, out _), "설정으로 다시 제한 가능");
            rules.ClimbMaxHeight = .2f;
            Assert.AreEqual(.6f, rules.ResolvedClimbMaxHeight, 1e-5f, "최소 오르기 높이보다 낮게는 못 둔다");
        }

        [Test] public void DefaultsClimbInEveryStageWithoutHeightLimit()
        {
            var rules = new PursuitSettings();
            Assert.AreEqual(1, rules.ClimbFromStage, "1~4단계 모두 오른다");
            Assert.AreEqual(0, rules.ClimbMaxHeight, "0 = 높이 제한 없음");
            Assert.IsTrue(float.IsPositiveInfinity(rules.ResolvedClimbMaxHeight));
            Assert.IsTrue(GameSettings.WaitingRoom.Pursuit.ClimbMaxHeight <= 0 || GameSettings.WaitingRoom.Pursuit.ClimbMaxHeight >= 1.94f, "실제 대기실 드럼통(1.94m)");
            Assert.AreEqual(rules.ClimbSeconds, PursuitActor.TraversalSeconds(rules, true, 1.2f), .001f);
            Assert.Greater(PursuitActor.TraversalSeconds(rules, true, 2.4f), rules.ClimbSeconds, "높을수록 오래 오른다");
        }
    }
}
