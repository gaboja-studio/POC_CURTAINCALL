using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitDesignerFloorTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        PursuitDesignerWindow window;
        Scene scene;
        PursuitRoom upper, lower, neighbour;
        readonly Rect viewport = new(0, 0, 600, 600);
        object Call(string name, params object[] args) => typeof(PursuitDesignerWindow).GetMethod(name, Private).Invoke(window, args);
        object Get(string name) => typeof(PursuitDesignerWindow).GetProperty(name, Private).GetValue(window);
        void Set(string name, object value) => typeof(PursuitDesignerWindow).GetField(name, Private).SetValue(window, value);
        PursuitRoom MakeRoom(string name, float x, float floor)
        {
            var room = new GameObject(name).AddComponent<PursuitRoom>();
            room.transform.position = new Vector3(x, floor, 0);
            room.Area = room.gameObject.AddComponent<BoxCollider>(); room.Area.isTrigger = true;
            room.Area.center = Vector3.up * 3; room.Area.size = new Vector3(10, 12, 10);
            var floorAnchor = new GameObject("FloorAnchor").transform;
            floorAnchor.SetParent(room.transform, false); room.SpawnPoints = new[] { floorAnchor };
            var marker = new GameObject("Marker").AddComponent<PursuitPlacement>();
            marker.transform.SetParent(room.transform, false);
            room.UsePlacements = true; room.Placements = new[] { marker };
            return room;
        }
        [SetUp] public void Setup()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            upper = MakeRoom("Upper", 0, 0);
            lower = MakeRoom("MachineRoom", 0, -10);
            neighbour = MakeRoom("Neighbour", 12, -10);
            window = ScriptableObject.CreateInstance<PursuitDesignerWindow>();
            Set("rooms", new[] { upper, lower, neighbour });
            Physics.SyncTransforms();
            Call("EnsureView", viewport);
        }
        [TearDown] public void Teardown()
        {
            Object.DestroyImmediate(window);
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        }
        [Test] public void FloorSwitchFiltersRoomsMarkersAndHitTargetsTogether()
        {
            CollectionAssert.AreEqual(new[] { upper }, (PursuitRoom[])Call("VisibleRooms"));
            Assert.AreSame(upper, Call("RoomAt", Vector3.zero));
            Call("ChooseFloor", -10f);
            Call("EnsureView", viewport);
            CollectionAssert.AreEquivalent(new[] { lower, neighbour }, (PursuitRoom[])Call("VisibleRooms"));
            CollectionAssert.AreEquivalent(new[] { lower.Placements[0], neighbour.Placements[0] }, (PursuitPlacement[])Call("VisiblePlacements"));
            Assert.AreSame(lower, Call("RoomAt", Vector3.zero));
            Assert.IsTrue((bool)Get("HasTargetRoom"));
            Call("SelectPlacement", neighbour.Placements[0]);
            Assert.AreSame(neighbour, Get("Room"), "A visible marker must select its owner even outside the centre room.");
            Call("ChooseFloor", 0f);
            Call("EnsureView", viewport);
            Assert.AreSame(upper, Get("Room"));
            CollectionAssert.AreEqual(new[] { upper.Placements[0] }, (PursuitPlacement[])Call("VisiblePlacements"));
        }
        [Test] public void ElevatedPlacementNeverChangesFloorOrDisappearsAfterRefresh()
        {
            var marker = upper.Placements[0];
            Call("SelectPlacement", marker);
            foreach (float height in new[] { 1.915f, .015f, 1.1f }) {
                marker.transform.position = new Vector3(1, height, 1);
                Assert.AreEqual(0, PursuitAuthoring.FloorReference(upper));
                Assert.IsTrue((bool)Get("HasTargetRoom"));
                CollectionAssert.Contains((PursuitPlacement[])Call("VisiblePlacements"), marker);
                CollectionAssert.Contains((PursuitRoom[])Call("VisibleRooms"), upper);
                Call("TargetCenterRoom");
                Assert.AreSame(upper, Get("Room"));
                Call("Cleanup"); Call("EnsureView", viewport);
                Assert.AreEqual(0f, Get("ActiveFloor"));
            }
            upper.Placements = System.Array.Empty<PursuitPlacement>();
            Object.DestroyImmediate(marker.gameObject);
            Call("Cleanup"); Call("EnsureView", viewport);
            Assert.AreEqual(0f, Get("ActiveFloor"));
            Assert.IsTrue((bool)Get("HasTargetRoom"));
        }
        [Test] public void RoomSelectionChangesFloorAndCameraExcludesLowerStorey()
        {
            Call("ChooseRoom", 1);
            Assert.AreEqual(-10f, Get("ActiveFloor"), "Overviews must change before the top view's next repaint.");
            Call("EnsureView", viewport);
            Call("PreparePreview", viewport);
            var camera = (Camera)typeof(PursuitDesignerWindow).GetField("previewCamera", Private).GetValue(window);
            Assert.AreEqual(-10.35f, camera.transform.position.y - camera.farClipPlane, .001f);
            Call("ChooseRoom", 0); Call("EnsureView", viewport); Call("PreparePreview", viewport);
            camera = (Camera)typeof(PursuitDesignerWindow).GetField("previewCamera", Private).GetValue(window);
            Assert.Greater(camera.transform.position.y - camera.farClipPlane, lower.transform.position.y + 2);
        }
    }
}
