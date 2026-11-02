using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitAuthoringTests
    {
        Scene scene;
        PursuitRoom room;
        GameObject prefab;
        [SetUp] public void Setup()
        {
            // Test Runner already owns an untitled scene and restores the user's scene setup afterwards.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            room = new GameObject("Room").AddComponent<PursuitRoom>();
            room.Area = room.gameObject.AddComponent<BoxCollider>(); room.Area.isTrigger = true;
            room.Area.center = new Vector3(0, 2, 0); room.Area.size = new Vector3(10, 4, 10);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Floor";
            floor.transform.position = new Vector3(0, -.1f, 0); floor.transform.localScale = new Vector3(10, .2f, 10);
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube); roof.transform.position = new Vector3(0, 4, 0); roof.transform.localScale = new Vector3(10, .2f, 10);
            prefab = new GameObject("FixtureMannequin"); prefab.AddComponent<PursuitActor>();
        }
        [TearDown] public void Teardown()
        {
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        }
        [Test] public void GroundChoosesFloorNotRoofAndRejectsVoid()
        {
            Assert.IsTrue(PursuitAuthoring.Ground(room, new Vector3(1, 100, 2), out var p));
            Assert.AreEqual(1, p.x); Assert.AreEqual(2, p.z); Assert.AreEqual(.015f, p.y, .002f);
            Assert.IsFalse(PursuitAuthoring.Ground(room, new Vector3(10000, 0, 10000), out _));
        }
        [Test] public void LobbyVolumeBelowFloorUsesPatrolHeightAndRejectsLowerStorey()
        {
            room.Area.center = new Vector3(0, 3, 0); room.Area.size = new Vector3(10, 12, 10);
            var anchor = new GameObject("LobbyPatrol").transform; anchor.position = Vector3.up * .02f;
            room.PatrolPoints = new[] { anchor };
            var raisedSpawn = new GameObject("SpawnOnFurniture").transform; raisedSpawn.position = Vector3.up * .666125f;
            room.SpawnPoints = new[] { raisedSpawn };
            Assert.AreEqual(.02f, PursuitAuthoring.FloorReference(room), .001f, "A single raised spawn must not override the room's floor route.");
            Assert.IsTrue(PursuitAuthoring.Ground(room, new Vector3(1, 0, 2), out var point));
            Assert.AreEqual(.015f, point.y, .002f);
            var lowerFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lowerFloor.transform.position = new Vector3(30, -5.1f, 0);
            lowerFloor.transform.localScale = new Vector3(10, .2f, 10);
            Assert.IsFalse(PursuitAuthoring.Ground(room, new Vector3(30, 0, 0), out _), "A hole on this storey must not place onto the lower floor.");
            var item = PursuitAuthoring.Add(room, prefab, new Vector3(1, 0, 2));
            Assert.IsNotNull(item);
            Assert.AreEqual(.015f, item.transform.position.y, .002f);
        }
        [Test] public void MovingRotatingAndReorderingDoNotMoveIndependentPatrolRoute()
        {
            var item = PursuitAuthoring.Add(room, prefab, Vector3.zero);
            var a = PursuitAuthoring.AddPoint(item, new Vector3(1, .015f, 1));
            var b = PursuitAuthoring.AddPoint(item, new Vector3(2, .015f, 2));
            var before = new[] { a.position, b.position };
            PursuitAuthoring.Move(item, new Vector3(-2, .015f, -2), Quaternion.Euler(0, 135, 0));
            Assert.Less(Vector3.Distance(before[0], a.position), .0001f); Assert.Less(Vector3.Distance(before[1], b.position), .0001f);
            PursuitAuthoring.Reorder(item, 0, 1);
            Assert.AreSame(b, item.PatrolPoints[0]); Assert.AreSame(a, item.PatrolPoints[1]);
            Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
            PursuitAuthoring.RemovePoint(item, 0); Assert.AreSame(a, item.PatrolPoints.Single());
            Assert.IsTrue(b == null);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.AreEqual(2, item.PatrolPoints.Length); Assert.IsNotNull(item.PatrolPoints[0]);
        }
        [Test] public void MigrationPreservesLayoutAndClonesRoutesOnlyOnce()
        {
            var director = new GameObject("Director").AddComponent<PursuitDirector>();
            director.Rooms = new[] { room }; director.Mannequins = new[] { prefab };
            var spawn = new GameObject("Spawn").transform; spawn.position = new Vector3(2, .015f, 1); spawn.rotation = Quaternion.Euler(0, 40, 0);
            var point = new GameObject("OldPatrol").transform; point.position = new Vector3(3, .015f, 2);
            room.Count = 2; room.SpawnPoints = new[] { spawn }; room.PatrolPoints = new[] { point };
            Assert.AreEqual(2, PursuitAuthoring.Migrate(director));
            Assert.AreEqual(0, PursuitAuthoring.Migrate(director));
            Assert.AreEqual(2, room.Placements.Length);
            foreach (var item in room.Placements) {
                Assert.AreSame(prefab, item.Prefab); Assert.AreEqual(spawn.position, item.transform.position);
                Assert.Less(Quaternion.Angle(spawn.rotation, item.transform.rotation), .001f);
                Assert.AreEqual(point.position, item.PatrolPoints[0].position); Assert.AreNotSame(point, item.PatrolPoints[0]);
            }
            Assert.AreNotSame(room.Placements[0].PatrolPoints[0], room.Placements[1].PatrolPoints[0]);
            PursuitAuthoring.Remove(room, room.Placements[0]); Assert.AreEqual(1, room.Placements.Length);
            Assert.IsTrue(point != null, "Legacy route stays intact.");
        }
    }
}
