using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitNavigationTests
    {
        static readonly Vector3 Origin = new(10000, 0, 10000);
        NavMeshData data;
        NavMeshDataInstance mesh;
        Vector3 start, reachable;
        NavMeshPath path;

        [SetUp] public void Setup()
        {
            var settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>();
            void Box(Vector3 center, Vector3 size) => sources.Add(new NavMeshBuildSource {
                shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(Origin + center, Quaternion.identity, Vector3.one),
                size = size, area = 0
            });
            Box(new Vector3(0, -.25f, 0), new Vector3(14, .5f, 14));
            Box(new Vector3(0, 1, 0), new Vector3(2, 2, 5));
            // A disconnected island makes a destination that is on NavMesh but unreachable from the patrol floor.
            Box(new Vector3(15, -.25f, 0), new Vector3(4, .5f, 4));
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Origin + Vector3.right * 6, new Vector3(30, 8, 18)), Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(data);
            mesh = NavMesh.AddNavMeshData(data);
            Assert.IsTrue(NavMesh.SamplePosition(Origin + Vector3.left * 5, out var hit, 1, NavMesh.AllAreas));
            start = hit.position;
            path = new NavMeshPath();
        }
        [TearDown] public void Cleanup()
        {
            mesh.Remove();
            if (data != null) Object.DestroyImmediate(data);
        }
        bool Navigate(Vector3 goal) => PursuitNavigation.TryPlan(start, goal, NavMesh.AllAreas, path, out reachable);

        [Test] public void PatrolPathGoesAroundObstacle()
        {
            var goal = Origin + Vector3.right * 5;
            Assert.IsTrue(Navigate(goal));
            Assert.Greater(path.corners.Length, 2, "The central wall must produce a detour, not a straight path.");
        }

        [Test] public void UnreachableDestinationUsesReachablePathEnd()
        {
            var goal = Origin + Vector3.right * 15;
            Assert.IsTrue(Navigate(goal));
            Assert.Greater(Vector3.Distance(goal, reachable), 5);
            var verification = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(start, reachable, NavMesh.AllAreas, verification));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, verification.status);
        }

        [Test] public void TargetInsideWallUsesNearbyWalkablePosition()
        {
            Assert.IsTrue(Navigate(Origin));
            Assert.Greater(Vector3.Distance(Origin, reachable), .5f);
            Assert.Less(Vector3.Distance(Origin, reachable), 4f);
        }
    }
}
