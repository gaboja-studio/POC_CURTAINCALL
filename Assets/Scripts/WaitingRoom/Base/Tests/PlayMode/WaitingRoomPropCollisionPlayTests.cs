using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Tests
{
    public sealed class WaitingRoomPropCollisionPlayTests
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        Transform room;
        GameObject actor, isolated, floor;

        [UnitySetUp]
        public IEnumerator Open()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            room = SceneManager.GetSceneByPath(ScenePath).GetRootGameObjects().Single(g => g.name == "WaitingRoomGreybox").transform;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlasticBarrel_RepeatedLandingsStayOutsideAndCanWalkAway()
        {
            var source = room.Find("Mood/Tunnel/WallDressing/SM_Plasticbarrel").GetComponent<MeshCollider>();
            isolated = new GameObject("BUG024_IsolatedBarrel");
            isolated.transform.SetPositionAndRotation(new Vector3(1000, 100, 1000), source.transform.rotation);
            isolated.transform.localScale = source.transform.lossyScale;
            var barrel = isolated.AddComponent<MeshCollider>();
            barrel.sharedMesh = source.sharedMesh;
            barrel.convex = source.convex;
            floor = new GameObject("BUG024_TestFloor");
            floor.transform.position = new Vector3(1000, 99.5f, 1000);
            floor.AddComponent<BoxCollider>().size = new Vector3(6, 1, 6);
            actor = new GameObject("BUG024_ActualPlayerCapsule");
            var moverType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.Player.PlayerMover")).First(t => t != null);
            var mover = (Behaviour)actor.AddComponent(moverType);
            mover.enabled = false; // Actual game capsule dimensions; displacement is synthetic test input.
            var controller = actor.GetComponent<CharacterController>();
            Physics.SyncTransforms();
            var bounds = barrel.bounds;
            Assert.IsTrue(barrel.Raycast(new Ray(bounds.center + Vector3.up * 3, Vector3.down), out var top, 6));
            for (int attempt = 0; attempt < 3; attempt++)
            {
                controller.enabled = false;
                actor.transform.position = new Vector3(bounds.center.x + (attempt - 1) * .05f, top.point.y + 1.5f, bounds.center.z);
                controller.enabled = true;
                for (int frame = 0; frame < 80; frame++) { controller.Move(Vector3.down * .04f); yield return null; }
                Assert.That(actor.transform.position.y, Is.InRange(top.point.y - .12f, top.point.y + .12f), "Must land on top rather than enter the barrel.");
                Vector3 landed = actor.transform.position;
                for (int frame = 0; frame < 40; frame++) { controller.Move(Vector3.back * .05f + Vector3.down * .02f); yield return null; }
                Assert.Greater(Vector3.Dot(actor.transform.position - landed, Vector3.back), 1f, "Must walk away without getting trapped by the side wall.");
            }
        }

        [TestCase("Mood/Tunnel/WallDressing/SM_Plasticbarrel")]
        [TestCase("Mood/Tunnel/WallDressing/SM_PAD_CardboardBox03_c1_LOD0")]
        [TestCase("Mood/Rooms/ExpertRoom/SM_WW2_WoodenChair_c1_LOD0")]
        public void ReachableHollowProps_HaveClosedCollisionVolume(string path)
        {
            var collider = room.Find(path).GetComponent<MeshCollider>();
            Assert.IsTrue(collider.convex);
            var bounds = collider.bounds;
            Assert.IsTrue(collider.Raycast(new Ray(bounds.center + Vector3.up * (bounds.extents.y + 1), Vector3.down), out var hit, bounds.size.y + 2));
            Assert.Greater(hit.point.y, bounds.min.y + bounds.size.y * .4f, "A ray from above must hit the closed upper surface, not the bottom inside.");
        }

        [Test]
        public void CorrectedFurniture_HorizontalCollidersFitVisibleLocalBounds()
        {
            string[] names = { "PackedTrunk", "WaitingChair", "ReadyPropsCrate", "CircusStorageCrate_0", "CircusStorageCrate_1", "StorageTrunk_4", "FootTrunk_1", "FootTrunk_3", "StoredCostume_0", "StoredCostume_1", "RetiredChair", "StoredBarrel_1", "EastCrates_2" };
            var boxes = room.GetComponentsInChildren<BoxCollider>().Where(c => !c.isTrigger && names.Contains(c.name)).ToArray();
            Assert.AreEqual(13, boxes.Length);
            foreach (var box in boxes)
            {
                var points = box.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).SelectMany(r => Enumerable.Range(0, 8).Select(i => box.transform.InverseTransformPoint(r.transform.TransformPoint(r.localBounds.center + Vector3.Scale(r.localBounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)))))).ToArray();
                var visual = new Bounds(points[0], Vector3.zero);
                foreach (var point in points) visual.Encapsulate(point);
                Assert.AreEqual(visual.size.x, box.size.x, .003f, box.name + " width");
                Assert.AreEqual(visual.size.z, box.size.z, .003f, box.name + " depth");
                Assert.AreEqual(visual.center.x, box.center.x, .003f, box.name + " center x");
                Assert.AreEqual(visual.center.z, box.center.z, .003f, box.name + " center z");
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (actor != null) Object.Destroy(actor);
            if (isolated != null) Object.Destroy(isolated);
            if (floor != null) Object.Destroy(floor);
            yield return null;
        }
    }
}
