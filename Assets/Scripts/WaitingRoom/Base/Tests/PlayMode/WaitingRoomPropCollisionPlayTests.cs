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

        [UnityTest, Category("CommonIdleSupportRegression")]
        public IEnumerator DormFootTrunk_IdleActualMoverSlidesOffUnsupportedCornerAndKeepsRealTopSupport()
        {
            var dormitory = GameObject.Find("WaitingRoom_LayoutA/S02_Dormitory");
            Assert.IsNotNull(dormitory, "Use the current playable dormitory rather than the retained greybox.");
            var trunk = dormitory.GetComponentsInChildren<Collider>().Single(c => c.name == "FootTrunk_2" && !c.isTrigger);
            actor = new GameObject("DormIdleCorner_ActualMover");
            var moverType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.Player.PlayerMover")).First(t => t != null);
            var mover = (Behaviour)actor.AddComponent(moverType);
            moverType.GetProperty("FreeMovement").SetValue(mover, true);
            var controller = actor.GetComponent<CharacterController>();
            var relocate = moverType.GetMethod("Relocate");
            Physics.SyncTransforms();
            var bounds = trunk.bounds;

            // The real top remains standable; only the unsupported side corner should shed the capsule.
            relocate.Invoke(mover, new object[] { new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z), Quaternion.identity });
            yield return new WaitForSeconds(1.5f);
            Assert.That(actor.transform.position.y, Is.InRange(bounds.max.y, bounds.max.y + controller.skinWidth * 2f));
            Assert.IsTrue(controller.isGrounded);

            // Same live-scene location which stalled at y=.242: x=center-.6, z=center.
            Vector3 drop = new(bounds.center.x - controller.radius * 2f, bounds.max.y + 1f, bounds.center.z);
            var floorHit = Physics.RaycastAll(drop, Vector3.down, controller.height * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider != controller && !h.collider.transform.IsChildOf(actor.transform) && h.normal.y >= Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad))
                .OrderBy(h => h.distance).First();
            relocate.Invoke(mover, new object[] { drop, Quaternion.identity });
            // Do not call Move or Update manually: actual PlayerMover.Update and common gravity must resolve it.
            yield return new WaitForSeconds(3f);
            Assert.Less(actor.transform.position.x, drop.x, "The idle capsule must leave the rounded side contact outward.");
            Assert.That(actor.transform.position.y, Is.InRange(floorHit.point.y, floorHit.point.y + controller.skinWidth * 2f),
                "The furniture's side must not keep a motionless player suspended above the floor.");
            Assert.IsTrue(controller.isGrounded);
            Assert.IsFalse((bool)moverType.GetProperty("IsAirborne").GetValue(mover));
        }

        [UnityTest, Category("CommonIdleSupportRegression")]
        public IEnumerator DormFootTrunk_IdleSlideKeepsTheCommonGapToAnotherPlayer()
        {
            var dormitory = GameObject.Find("WaitingRoom_LayoutA/S02_Dormitory");
            Assert.IsNotNull(dormitory);
            var trunk = dormitory.GetComponentsInChildren<Collider>().Single(c => c.name == "FootTrunk_2" && !c.isTrigger);
            var moverType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.Player.PlayerMover")).First(t => t != null);
            actor = new GameObject("DormIdleCorner_PlayerGapMover");
            var mover = (Behaviour)actor.AddComponent(moverType);
            moverType.GetProperty("FreeMovement").SetValue(mover, true);
            var controller = actor.GetComponent<CharacterController>();
            var relocate = moverType.GetMethod("Relocate");
            Physics.SyncTransforms();
            var bounds = trunk.bounds;
            Vector3 drop = new(bounds.center.x - controller.radius * 2f, bounds.max.y + 1f, bounds.center.z);

            // A positive control proves this live furniture contact actually produces an idle slide.
            relocate.Invoke(mover, new object[] { drop, Quaternion.identity });
            yield return new WaitForSeconds(3f);
            float actualSlide = drop.x - actor.transform.position.x;
            Assert.Greater(actualSlide, .001f);
            float floorY = actor.transform.position.y;

            isolated = new GameObject("DormIdleCorner_StationaryOtherPlayer");
            var other = (Behaviour)isolated.AddComponent(moverType);
            // Keep the real mover registered for common player blocking, while its position
            // is held still like an externally driven player. Do not disable the component.
            moverType.GetProperty("Simulated").SetValue(other, false);
            var otherController = isolated.GetComponent<CharacterController>();
            var character = moverType.GetProperty("Character", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            float gap = (float)character.GetType().GetProperty("PlayerGap").GetValue(character);
            float separation = controller.radius + otherController.radius + gap;
            float clearance = actualSlide * .25f;
            relocate.Invoke(other, new object[] { new Vector3(drop.x - separation - clearance, floorY, drop.z), Quaternion.identity });
            relocate.Invoke(mover, new object[] { drop, Quaternion.identity });
            Physics.SyncTransforms();

            float until = Time.time + 3f;
            while (Time.time < until)
            {
                yield return null; // Real PlayerMover.Update; no manual Move or private method call.
                Vector3 between = actor.transform.position - isolated.transform.position;
                between.y = 0f;
                Assert.GreaterOrEqual(between.magnitude, separation - .0005f,
                    "The idle corner correction must obey the same player gap as normal walking.");
            }

            isolated.SetActive(false);
            yield return new WaitForSeconds(2f);
            Assert.That(actor.transform.position.y, Is.InRange(floorY - .02f, floorY + controller.skinWidth * 2f),
                "Removing the adjacent player must allow the unsupported corner to shed the capsule again.");
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
