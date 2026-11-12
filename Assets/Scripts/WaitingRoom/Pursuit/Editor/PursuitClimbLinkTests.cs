using System.Collections.Generic;
using System.Linq;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>규칙 기반 오르기 통로: 씬 수정 없이 NavMesh 가장자리 단차만으로 통로를 만든다.</summary>
    public sealed class PursuitClimbLinkTests
    {
        static readonly Vector3 Origin = new(-20000, 0, 20000);
        NavMeshData data;
        NavMeshDataInstance mesh;
        GameObject roof;

        [SetUp] public void Setup()
        {
            // 실제 추격 베이크(PursuitSceneBuilder)와 같은 에이전트 크기.
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .24f; settings.agentHeight = 1.7f; settings.agentClimb = .55f; settings.agentSlope = 55;
            var sources = new List<NavMeshBuildSource>();
            void Box(Vector3 center, Vector3 size) => sources.Add(new NavMeshBuildSource {
                shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(Origin + center, Quaternion.identity, Vector3.one),
                size = size, area = 0 });
            Box(new Vector3(0, -.25f, 0), new Vector3(24, .5f, 24));
            Box(new Vector3(-6, .6f, 0), new Vector3(3, 1.2f, 3));   // 오를 수 있는 상자(1.2m)
            Box(new Vector3(6, 1.25f, 0), new Vector3(3, 2.5f, 3));  // 너무 높은 상자(2.5m)
            Box(new Vector3(0, .5f, 7), new Vector3(3, 1f, 3));      // 위가 막힌 상자(1m)
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Origin + Vector3.up * 2, new Vector3(26, 8, 26)), Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(data);
            mesh = NavMesh.AddNavMeshData(data);
            // 물리 충돌체만 있는 낮은 천장: 상자 위에는 NavMesh가 있지만 몸이 넘어갈 공간이 없다.
            roof = new GameObject("climb-test-roof");
            roof.transform.position = Origin + new Vector3(0, 1.9f, 7);
            roof.AddComponent<BoxCollider>().size = new Vector3(6, .2f, 6);
            Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup()
        {
            PursuitClimbLinks.Clear();
            mesh.Remove();
            if (data != null) Object.DestroyImmediate(data);
            if (roof != null) Object.DestroyImmediate(roof);
        }

        static List<(Vector3 bottom, Vector3 top)> Local(float maxHeight) => PursuitClimbLinks.Candidates(maxHeight, 0)
            .Where(c => (c.top - Origin).sqrMagnitude < 30 * 30).ToList();

        [Test] public void CandidatesFollowHeightRuleAndClearance()
        {
            var all = Local(1.5f);
            var onCrate = all.Where(c => Mathf.Abs(c.top.y - Origin.y - 1.2f) < .15f && c.top.x < Origin.x).ToList();
            Assert.IsNotEmpty(onCrate, "A 1.2m crate within the 1.5m rule gets climb candidates.");
            foreach (var (bottom, top) in onCrate) {
                Assert.AreEqual(0, bottom.y - Origin.y, .15f);
                Assert.Less(new Vector2(top.x - bottom.x, top.z - bottom.z).magnitude, 1.6f, "The foot of the climb is right beside the edge.");
            }
            Assert.IsFalse(all.Any(c => c.top.y - Origin.y > 2), "A 2.5m crate is above the rule.");
            Assert.IsFalse(all.Any(c => c.top.z - Origin.z > 5 && c.top.y - Origin.y > .8f), "A covered top has no room to climb over.");
            Assert.IsFalse(Local(1f).Any(c => c.top.y - Origin.y > 1.1f), "Lowering the rule removes the 1.2m crate.");
        }

        [Test] public void LinksConnectOnlyThroughClimbAreaAndCanBeTurnedOff()
        {
            Assert.IsTrue(NavMesh.SamplePosition(Origin + new Vector3(-10, 0, 0), out var floor, 1, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(Origin + new Vector3(-6, 1.2f, 0), out var top, .5f, NavMesh.AllAreas));
            var path = new NavMeshPath();
            NavMesh.CalculatePath(floor.position, top.position, NavMesh.AllAreas, path);
            Assert.AreNotEqual(NavMeshPathStatus.PathComplete, path.status, "Without links the crate top is an island.");

            var rules = new PursuitSettings();
            Assert.Greater(PursuitClimbLinks.Build(0, rules), 0);
            NavMesh.CalculatePath(floor.position, top.position, NavMesh.AllAreas, path);
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "Climb links reach the crate top.");
            NavMesh.CalculatePath(floor.position, top.position, NavMesh.AllAreas & ~PursuitClimbLinks.AreaMask, path);
            Assert.AreNotEqual(NavMeshPathStatus.PathComplete, path.status, "Stages before ClimbFromStage exclude the climb area.");

            rules.ClimbFromStage = 0;
            Assert.AreEqual(0, PursuitClimbLinks.Build(0, rules), "ClimbFromStage 0 turns climbing off.");
            NavMesh.CalculatePath(floor.position, top.position, NavMesh.AllAreas, path);
            Assert.AreNotEqual(NavMeshPathStatus.PathComplete, path.status);
        }

        [Test] public void ClimbPoseRisesBesideTheEdgeThenStepsOver()
        {
            var from = new Vector3(0, 0, 0); var to = new Vector3(0, 1.2f, .8f);
            Assert.AreEqual(from, PursuitActor.ClimbPose(from, to, true, 0));
            Assert.That(Vector3.Distance(to, PursuitActor.ClimbPose(from, to, true, 1)), Is.LessThan(.001f));
            var rising = PursuitActor.ClimbPose(from, to, true, .3f);
            Assert.AreEqual(0, rising.z, .001f, "The actor climbs straight up the side before stepping over.");
            Assert.Greater(rising.y, 0);
            var drop = PursuitActor.ClimbPose(to, from, false, .5f);
            Assert.Greater(drop.y, Mathf.Lerp(to.y, from.y, .25f), "Dropping down keeps a small hop.");
        }
    }
}
