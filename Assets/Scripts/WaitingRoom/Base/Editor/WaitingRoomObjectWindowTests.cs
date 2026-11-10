using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Editor
{
    public sealed class WaitingRoomObjectWindowTests
    {
        [Test]
        [Category("BreakFlow")]
        public void SafetyZonesApplyToActualTimerAndPersistThroughSerializationAndUndo()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("SafetyZoneTest"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var zones = root.AddComponent<ExplorationTimerZones>();
                BoxCollider Room(string name, Vector3 at)
                {
                    var go = new GameObject(name); go.transform.SetParent(root.transform); go.transform.position = at;
                    var box = go.AddComponent<BoxCollider>(); box.center = Vector3.up; box.size = new Vector3(8, 3, 8); return box;
                }
                var lower = Room("Lower", Vector3.zero); var upper = Room("Upper", new Vector3(0, 6, 0));
                zones.SetSafety(new[] { lower, upper }, false);
                Assert.IsTrue(zones.IsRunningZone(Vector3.zero));
                Assert.IsTrue(zones.IsRunningZone(new Vector3(0, 6, 0)));
                UnityEditor.Undo.RecordObject(zones, "Safety zone test");
                zones.SetSafety(new[] { lower }, true);
                UnityEditor.Undo.FlushUndoRecordObjects();
                Assert.IsFalse(zones.IsRunningZone(Vector3.zero));
                Assert.IsTrue(zones.IsRunningZone(new Vector3(0, 6, 0)), "Other floors are independent");
                UnityEditor.Undo.PerformUndo(); zones.RebuildZones();
                Assert.IsTrue(zones.IsRunningZone(Vector3.zero));
                var saved = JsonUtility.ToJson(zones);
                zones.SetSafety(zones.Zones, true);
                Assert.IsFalse(zones.IsRunningZone(Vector3.zero));
                JsonUtility.FromJsonOverwrite(saved, zones); zones.RebuildZones();
                Assert.IsTrue(zones.IsRunningZone(Vector3.zero), "Serialized scene restores safety settings");
                zones.OutsideIsSafe = false;
                Assert.IsTrue(zones.IsRunningZone(Vector3.one * 100));
                zones.OutsideIsSafe = true;
                Assert.IsFalse(zones.IsRunningZone(Vector3.one * 100));
            }
            finally { UnityEditor.Undo.ClearUndo(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void CabinetRewardPointAndWallFallbackFollowTheirOwner()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject Make(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
            try {
                var cabinet = Make("Cabinet"); var target = cabinet.AddComponent<ToolTarget>();
                var external = Make("ExternalRewardPoint").transform; external.position = new Vector3(2, .2f, 1);
                var so = new UnityEditor.SerializedObject(target); so.FindProperty("rewardPoint").objectReferenceValue = external; so.ApplyModifiedPropertiesWithoutUndo();
                var old = target.RewardPoint;
                WaitingRoomObjectWindow.EnsureOwnedDropAnchors(cabinet.transform);
                var at = new Vector3(10, 0, 5);
                WaitingRoomObjectWindow.MovePlacementGroup(cabinet.transform, System.Array.Empty<Transform>(), at, Quaternion.identity);
                Assert.Less(Vector3.Distance(old + at, target.RewardPoint), .001f);
                Assert.AreEqual(old, external.position);
                var wall = Make("Wall").AddComponent<BreakableWall>();
                WaitingRoomObjectWindow.MovePlacementGroup(wall.transform, System.Array.Empty<Transform>(), at, Quaternion.identity);
                Assert.AreEqual(at, wall.RewardPoint);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        [Test]
        public void ContainerMovementKeepsDropsAndUnlockConditionsTogether()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var window = ScriptableObject.CreateInstance<WaitingRoomObjectWindow>();
            GameObject Make(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
            try {
                var chest = Make("Chest"); chest.transform.position = new Vector3(10, 0, 2);
                var owner = chest.AddComponent<ExplorationGate>(); owner.ConfigureRoute("test:box");
                var source = Make("SharedExternalDropLocation"); source.transform.position = new Vector3(12, 0, 3);
                var drops = chest.AddComponent<ExplorationDrops>();
                drops.TryConfigure(new[] { new ExplorationDrops.Entry { itemId = ItemUses.MagicianHairpin, uidOffset = 7, point = source.transform } }, out _);
                var key = Make("Key"); key.transform.position = new Vector3(11, 0, 2);
                var gate = key.AddComponent<ExplorationGate>(); gate.ConfigureRoute(StructureAKeyRoute.ManagementKey, new[] { owner.TargetId }, owner.TargetId);
                var spot = Make("FoundItem").AddComponent<FoundItemSpot>(); spot.transform.position = new Vector3(10, 0, 4); spot.UnlockAfter(owner.TargetId);
                var oldKey = key.transform.position; var oldSpot = spot.transform.position; var oldDrop = source.transform.position;
                var origin = chest.transform.position; var destination = new Vector3(20, 0, 2); var rotation = Quaternion.Euler(0, 90, 0);
                WaitingRoomObjectWindow.EnsureOwnedDropAnchors(chest.transform);
                WaitingRoomObjectWindow.MovePlacementGroup(chest.transform, window.OwnedCompanions(chest.transform), destination, rotation);
                Assert.Less(Vector3.Distance(destination + rotation * (oldDrop - origin), drops.entries[0].point.position), .001f);
                Assert.Less(Vector3.Distance(destination + rotation * (oldKey - origin), key.transform.position), .001f);
                Assert.Less(Vector3.Distance(destination + rotation * (oldSpot - origin), spot.transform.position), .001f);
                Assert.AreEqual(oldDrop, source.transform.position, "Other scene geometry referenced as a drop location must stay fixed");
                Assert.AreEqual(owner.TargetId, gate.RevealTarget);
                Assert.AreEqual(7, drops.entries[0].uidOffset);
                Assert.AreEqual(ItemUses.MagicianHairpin, drops.entries[0].itemId);
            }
            finally { Object.DestroyImmediate(window); EditorSceneManager.ClosePreviewScene(scene); }
        }
        [Test]
        public void NewToolsUseExistingFoundItemSystemAndUniqueOffsets()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var window = ScriptableObject.CreateInstance<WaitingRoomObjectWindow>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try {
                window.GetType().GetField("creationItemId", flags).SetValue(window, ItemUses.MagicianHairpin);
                var first = window.CreateCatalogObject(scene).GetComponent<FoundItemSpot>();
                window.GetType().GetField("creationItemId", flags).SetValue(window, ItemUses.UvLamp);
                var second = window.CreateCatalogObject(scene).GetComponent<FoundItemSpot>();
                Assert.AreEqual(ItemUses.MagicianHairpin, first.MakeItem(1).ItemId);
                Assert.AreEqual(ItemUses.UvLamp, second.MakeItem(1).ItemId);
                Assert.AreNotEqual(first.MakeItem(1).Uid, second.MakeItem(1).Uid);
                var clone = Object.Instantiate(first.gameObject); SceneManager.MoveGameObjectToScene(clone, scene);
                window.PrepareNewPrefab(clone);
                Assert.AreNotEqual(first.SpotId, clone.GetComponent<FoundItemSpot>().SpotId);
                Assert.AreNotEqual(first.MakeItem(1).Uid, clone.GetComponent<FoundItemSpot>().MakeItem(1).Uid);
                window.GetType().GetField("creationBox", flags).SetValue(window, true);
                var box = window.CreateCatalogObject(scene);
                Assert.AreEqual("낡은 상자", box.name);
                Assert.AreEqual(ItemUses.StakePuller, box.GetComponent<ExplorationGate>().ToolId);
                Assert.IsFalse(box.GetComponent<ExplorationDrops>().Pending);
                Assert.IsNotNull(box.GetComponent<BoxCollider>());
            }
            finally { Object.DestroyImmediate(window); EditorSceneManager.ClosePreviewScene(scene); }
        }
        [Test]
        public void SchedulePlacementUsesBoardInsteadOfControllerOrigin()
        {
            var root = new GameObject("Schedule");
            try {
                var board = new GameObject("Board").transform; board.SetParent(root.transform, false); board.localPosition = new Vector3(10, 2, -3);
                var view = root.AddComponent<ScheduleBoardView>();
                var so = new UnityEditor.SerializedObject(view); so.FindProperty("board").objectReferenceValue = board; so.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(board.position, WaitingRoomObjectWindow.PlacementPoint(root.transform));
                Assert.AreNotEqual(root.transform.position, WaitingRoomObjectWindow.PlacementPoint(root.transform));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void KeysBoxesAndCabinetsAreAllowedWhileDoorsStayProtected()
        {
            Assert.IsTrue(WaitingRoomObjectWindow.IsFurnitureGate(ExplorationGateKind.Open, true, "structure-a:management-key"));
            Assert.IsTrue(WaitingRoomObjectWindow.IsFurnitureGate(ExplorationGateKind.Open, false, "structure-a:steel-box"));
            Assert.IsTrue(WaitingRoomObjectWindow.IsFurnitureGate(ExplorationGateKind.Breakable, false, "structure-a:cargo-box-1"));
            Assert.IsFalse(WaitingRoomObjectWindow.IsFurnitureGate(ExplorationGateKind.Open, false, "structure-a:management-door"));
            Assert.IsFalse(WaitingRoomObjectWindow.IsFurnitureGate(ExplorationGateKind.Cooperative, false, "structure-a:escape-door"));
        }
        [Test]
        public void RaisedFurnitureKeepsItsStorey()
        {
            var floors = new[] { -4f, 0f, 4f };
            Assert.AreEqual(0, WaitingRoomObjectWindow.FloorForHeight(2, floors));
            Assert.AreEqual(4, WaitingRoomObjectWindow.FloorForHeight(5.5f, floors));
            Assert.AreEqual(-4, WaitingRoomObjectWindow.FloorForHeight(-3, floors));
        }
        [Test]
        public void MapParentAndGeometryCannotBecomeInteractionSelectionOrLinkedAnchor()
        {
            var room = new GameObject("Room");
            var phone = new GameObject("Phone");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var anchor = new GameObject("InteractionPoint");
            try {
                phone.transform.SetParent(room.transform, false);
                phone.AddComponent<CurtainCall.WaitingRoom.ShopPhone>();
                wall.transform.SetParent(room.transform, false);
                Assert.IsFalse(WaitingRoomObjectWindow.IsInteractionObject(room.transform));
                Assert.IsNull(WaitingRoomObjectWindow.InteractionRoot(wall.transform));
                Assert.AreEqual(phone.transform, WaitingRoomObjectWindow.InteractionRoot(phone.transform));
                Assert.IsFalse(WaitingRoomObjectWindow.IsPositionAnchor(wall.transform));
                Assert.IsFalse(WaitingRoomObjectWindow.IsPositionAnchor(room.transform));
                Assert.IsTrue(WaitingRoomObjectWindow.IsPositionAnchor(anchor.transform));
            }
            finally { Object.DestroyImmediate(room); Object.DestroyImmediate(anchor); }
        }
        [Test]
        public void MapCoordinatesKeepHeightAndNorthDirection()
        {
            var rect = new Rect(0, 0, 800, 400);
            var center = new Vector3(10, 0, 20);
            Assert.AreEqual(new Vector3(10, 3, 20), WaitingRoomObjectWindow.MapWorld(rect.center, rect, center, 50, 3));
            Assert.AreEqual(new Vector3(110, 3, 70), WaitingRoomObjectWindow.MapWorld(new Vector2(800, 0), rect, center, 50, 3));
        }
        [Test]
        public void LinkedChildIsMovedOnlyOnceAndOtherSceneIsExcluded()
        {
            var a = EditorSceneManager.NewPreviewScene();
            var b = EditorSceneManager.NewPreviewScene();
            try {
                var root = new GameObject("Object").transform;
                SceneManager.MoveGameObjectToScene(root.gameObject, a);
                var child = new GameObject("InteractionPoint").transform;
                child.SetParent(root, false);
                var anchor = new GameObject("RewardPoint").transform;
                SceneManager.MoveGameObjectToScene(anchor.gameObject, a);
                var foreign = new GameObject("OtherScene").transform;
                SceneManager.MoveGameObjectToScene(foreign.gameObject, b);
                var result = WaitingRoomObjectWindow.MoveRoots(root, new[] { child, anchor, anchor, foreign });
                CollectionAssert.AreEquivalent(new[] { root, anchor }, result);
            }
            finally { EditorSceneManager.ClosePreviewScene(b); EditorSceneManager.ClosePreviewScene(a); }
        }
    }
}
