using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitPlacementMovementTests
    {
        Scene scene;
        PursuitRoom room;
        PursuitPlacement item;
        Transform a, b;
        [SetUp] public void Setup()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            room = new GameObject("Room").AddComponent<PursuitRoom>();
            item = new GameObject("Mannequin").AddComponent<PursuitPlacement>();
            item.transform.SetParent(room.transform, false);
            a = new GameObject("A").transform; a.SetParent(item.transform, false); a.position = new Vector3(-20, 0, 0);
            b = new GameObject("B").transform; b.SetParent(item.transform, false); b.position = new Vector3(20, 0, 0);
            item.PatrolPoints = new[] { a, b };
            // Ensure the editor hook is initialized before recording a Transform edit.
            Assert.IsFalse(PursuitPlacementEditing.ClearDistantRoute(item));
        }
        [TearDown] public void Cleanup()
        {
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
        }
        int SceneMove(Vector3 destination, bool rotate = false)
        {
            Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.RecordObject(item.transform, "Scene move fixture");
            item.transform.position = destination;
            if (rotate) { item.transform.rotation = Quaternion.Euler(0, 90, 0); item.transform.localScale = Vector3.one * 2; }
            Undo.FlushUndoRecordObjects(); Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            return group;
        }
        [Test] public void SceneTransformKeepsWorldRouteAndUndoRedoTogether()
        {
            var beforeA = a.position; var beforeB = b.position;
            SceneMove(new Vector3(0, 0, 3), true);
            Assert.Less(Vector3.Distance(beforeA, a.position), .001f);
            Assert.Less(Vector3.Distance(beforeB, b.position), .001f);
            Assert.AreEqual(2, item.PatrolPoints.Length, "Distance is to the segment, not far apart vertices.");
            Undo.PerformUndo();
            Assert.Less(item.transform.position.magnitude, .001f);
            Assert.Less(Vector3.Distance(beforeA, a.position), .001f);
            Undo.PerformRedo();
            Assert.Less(Vector3.Distance(new Vector3(0, 0, 3), item.transform.position), .001f);
            Assert.Less(Vector3.Distance(beforeA, a.position), .001f);
        }
        [Test] public void DistantSceneMoveClearsOwnedRouteAndUndoRestoresIt()
        {
            SceneMove(new Vector3(0, 0, 11));
            Assert.AreEqual(0, item.PatrolPoints.Length);
            Assert.IsTrue(a == null && b == null);
            Undo.PerformUndo();
            Assert.AreEqual(2, item.PatrolPoints.Length);
            Assert.Less(Vector3.Distance(new Vector3(-20, 0, 0), item.PatrolPoints[0].position), .001f);
            Assert.Less(item.transform.position.magnitude, .001f);
            Undo.PerformRedo(); Assert.AreEqual(0, item.PatrolPoints.Length);
        }
        [Test] public void SharedRouteDisconnectsOnlyMovedMannequin()
        {
            a.SetParent(room.transform, true); b.SetParent(room.transform, true);
            room.PatrolPoints = new[] { a, b }; item.PatrolPoints = System.Array.Empty<Transform>(); item.UseRoomPatrol = true;
            var other = new GameObject("Other").AddComponent<PursuitPlacement>(); other.transform.SetParent(room.transform); other.UseRoomPatrol = true;
            SceneMove(new Vector3(0, 0, 11));
            Assert.IsFalse(item.UseRoomPatrol);
            Assert.AreEqual(2, other.Route(room).Length); Assert.IsTrue(a != null && b != null);
            Assert.AreEqual(new Vector3(-20, 0, 0), a.position);
            Undo.PerformUndo(); Assert.IsTrue(item.UseRoomPatrol);
        }
        [Test] public void PanelMovePreservesRouteAndHonoursThresholdAndDisable()
        {
            PursuitAuthoring.Move(item, new Vector3(0, 0, 10), Quaternion.Euler(0, 60, 0));
            Assert.AreEqual(2, item.PatrolPoints.Length);
            Assert.Less(Vector3.Distance(new Vector3(-20, 0, 0), a.position), .001f);
            item.RouteDetachDistance = 0;
            PursuitAuthoring.Move(item, new Vector3(0, 0, 100), Quaternion.identity);
            Assert.AreEqual(2, item.PatrolPoints.Length);
            item.RouteDetachDistance = 10;
            PursuitAuthoring.Move(item, new Vector3(0, 0, 101), Quaternion.identity);
            Assert.AreEqual(0, item.PatrolPoints.Length);
        }
    }
}