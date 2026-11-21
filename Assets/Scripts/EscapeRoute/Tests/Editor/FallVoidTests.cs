using System;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class FallVoidTests
    {
        [Test]
        public void ArrivalWaitsForSolidFloorInDestinationScene()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute.EscapeDevelopmentSession")).First(t => t != null);
            var method = type.GetMethod("HasArrivalFloor", BindingFlags.Static | BindingFlags.NonPublic);
            var dest = SceneManager.GetActiveScene();
            var other = EditorSceneManager.NewPreviewScene();
            var feet = new Vector3(9300f, 3100f, 7000f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bool Supported() => (bool)method.Invoke(null, new object[] { dest, feet });
            try
            {
                floor.transform.position = feet - Vector3.up * .55f;
                Assert.IsFalse((bool)method.Invoke(null, new object[] { other, feet }),
                    "An overlapping floor in the departing scene cannot release gravity.");
                var box = floor.GetComponent<BoxCollider>();
                box.isTrigger = true;
                Assert.IsFalse(Supported());
                box.isTrigger = false;
                box.enabled = false;
                Assert.IsFalse(Supported(), "A late-loading collider keeps the owner waiting.");
                box.enabled = true;
                Assert.IsTrue(Supported());

                floor.transform.position = feet - Vector3.up * 3f;
                Assert.IsFalse(Supported(), "The bottom of a shaft several metres beneath the spawn cannot release placement.");
                floor.transform.position = feet - Vector3.up * .55f;
                floor.AddComponent(AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("CurtainCall.Damage.DismemberedLimb")).First(t => t != null));
                Assert.IsFalse(Supported(), "Dislodged limbs cannot act as a spawn floor.");
            }
            finally
            {
                Object.DestroyImmediate(floor);
                EditorSceneManager.ClosePreviewScene(other);
            }
        }


        [Test]
        public void DeepFoundationKillsOnlyAfterLandingButTheLowerVoidKillsDuringFreeFall()
        {
            var frame = new GameObject("Isolated escape shaft");
            frame.transform.position = new Vector3(9300, 3100, 7000);
            frame.transform.rotation = Quaternion.Euler(0, 180, 0);
            var actor = new GameObject("Falling player");
            var floor = new GameObject("Concrete ledge");
            floor.transform.SetParent(frame.transform, false);
            try
            {
                const BindingFlags privateMembers = BindingFlags.Instance | BindingFlags.NonPublic;
                Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(name)).First(t => t != null);
                var voidType = Find("CurtainCall.EscapeRoute.EscapeFallVoid");
                var region = frame.AddComponent(voidType);
                voidType.GetField("coordinateSpace", privateMembers).SetValue(region, frame.transform);
                voidType.GetField("strandedLandingHeight", privateMembers).SetValue(region, -24.6f);
                var check = voidType.GetMethod("IsFatalPosition", privateMembers);
                var playerType = Find("CurtainCall.Network.PlayerSync.NetworkPlayer");
                var player = actor.AddComponent(playerType);
                var moverType = Find("CurtainCall.Player.PlayerMover");
                var mover = actor.GetComponent(moverType);
                playerType.GetField("mover", privateMembers).SetValue(player, mover);
                var jump = moverType.GetProperty("CurrentJump");
                void SetJump(string kind) => jump.SetValue(mover, Enum.Parse(jump.PropertyType, kind));
                bool Fatal() => (bool)check.Invoke(region, new object[] { player });
                var body = actor.GetComponent<CharacterController>();
                body.radius = .3f; body.height = 1.5f; body.center = Vector3.up * .75f;
                var tile = floor.AddComponent<BoxCollider>();
                tile.size = new Vector3(3, .5f, 3);
                floor.transform.localPosition = new Vector3(46, -26.25f, -74);
                actor.transform.position = frame.transform.TransformPoint(new Vector3(46, -25.98f, -74));
                Physics.SyncTransforms();

                SetJump("Fall");
                Assert.That(Fatal(), Is.False, "Crossing the higher limit in flight is permitted.");
                SetJump("None");
                Assert.That(Fatal(), Is.True, "Standing on the deep ledge cannot strand a player.");
                var other = EditorSceneManager.NewPreviewScene();
                try
                {
                    var secondaryRoot = new GameObject("Region in another scene");
                    SceneManager.MoveGameObjectToScene(secondaryRoot, other);
                    var isolatedRegion = secondaryRoot.AddComponent(voidType);
                    voidType.GetField("coordinateSpace", privateMembers).SetValue(isolatedRegion, frame.transform);
                    voidType.GetField("strandedLandingHeight", privateMembers).SetValue(isolatedRegion, -24.6f);
                    Assert.That(check.Invoke(isolatedRegion, new object[] { player }), Is.False,
                        "The same physical slab belongs to another scene from this region's perspective.");
                }
                finally { EditorSceneManager.ClosePreviewScene(other); }
                tile.enabled = false;
                Physics.SyncTransforms();
                Assert.That(Fatal(), Is.False, "There is no landing without a physical support.");
                SetJump("Fall");
                actor.transform.position = frame.transform.TransformPoint(new Vector3(46, -30.2f, -74));
                Assert.That(Fatal(), Is.True, "Below the hard limit no support is needed.");
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(frame);
            }
        }

        [Test]
        public void DeepLedgeContactRequiresAStationarySampleEvenWhenAirborneStateLags()
        {
            var root = new GameObject("Deep ledge sample");
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute.EscapeFallVoid")).First(t => t != null);
                var region = root.AddComponent(type);
                var check = type.GetMethod("IsSettled", BindingFlags.Instance | BindingFlags.NonPublic);
                bool Settled(Vector3 feet, double clock) => (bool)check.Invoke(region, new object[] { 41UL, feet, clock });
                Assert.IsFalse(Settled(Vector3.zero, 1d));
                Assert.IsFalse(Settled(Vector3.down * .07f, 1.4d));
                Assert.IsFalse(Settled(Vector3.down * .07f, 1.6d));
                Assert.IsTrue(Settled(Vector3.down * .07f, 1.8d));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(67, -18.5f, -17, false)] // 그네 탑승 높이
        [TestCase(66, -22, 14, false)] // 기존 진입 대포 준비대
        [TestCase(46, -29.9f, -74, false)]
        [TestCase(46, -30, -74, true)]
        [TestCase(67, -500, -17, true)] // 빠른 낙하도 아래쪽 한계로 놓치지 않음
        [TestCase(20, -31, -82, true)]
        [TestCase(146, -31, 18, true)]
        [TestCase(19.9f, -31, -74, false)]
        [TestCase(146.1f, -31, -74, false)]
        [TestCase(46, -31, -82.1f, false)]
        [TestCase(46, -31, 18.1f, false)] // 인접 시설 침범 방지
        [TestCase(float.NaN, -31, -74, false)]
        public void FallRegionExcludesTraversalAndOtherSections(float x, float y, float z, bool lethal)
        {
            var root = new GameObject("Offline fall bounds") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute.EscapeFallVoid")).First(t => t != null);
                var component = root.AddComponent(type);
                Assert.That(type.GetMethod("Contains").Invoke(component, new object[] { new Vector3(x, y, z) }), Is.EqualTo(lethal));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
