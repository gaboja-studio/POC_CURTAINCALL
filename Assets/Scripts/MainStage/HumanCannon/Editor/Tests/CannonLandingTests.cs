using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Damage;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class CannonLandingTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test, Category("StuntRegression20261120")]
        public void FinishAcceptsCapsuleStandingOnActualPlatformEdge()
        {
            var root = new GameObject("Finish edge fixture");
            root.SetActive(false);
            try
            {
                var cannon = root.AddComponent<HumanCannonSystem>();
                typeof(HumanCannonSystem).GetMethod("ResetSeats", Private).Invoke(cannon, null);
                var platform = new GameObject("Finish platform");
                platform.transform.SetParent(root.transform);
                platform.transform.position = new Vector3(15000, 0, 15000);
                var tile = platform.AddComponent<BoxCollider>();
                tile.center = Vector3.down * .25f;
                tile.size = new Vector3(4, .5f, 4);
                var zone = platform.AddComponent<HumanCannonLandingZone>();
                var player = new GameObject("Finisher").AddComponent<NetworkPlayer>();
                player.transform.SetParent(root.transform);
                player.transform.position = platform.transform.position + new Vector3(2.08f, .02f, 0);
                typeof(NetworkPlayer).GetField("mover", Private).SetValue(player, player.GetComponent<PlayerMover>());
                var body = player.GetComponent<CharacterController>();
                body.height = 1.5f; body.center = Vector3.up * .75f; body.radius = .3f;
                var supports = typeof(HumanCannonLandingZone).GetMethod("SupportsStandingPlayer", Private);
                root.SetActive(true);
                Physics.SyncTransforms();
                bool StandingOnFinish() => (bool)supports.Invoke(zone, new object[] { player, cannon });
                Assert.IsTrue(StandingOnFinish());
                player.transform.position += Vector3.right * .5f;
                Physics.SyncTransforms();
                Assert.IsFalse(StandingOnFinish());

                var upperFloor = new GameObject("Separate floor above finish");
                upperFloor.transform.SetParent(platform.transform, false);
                var upperTile = upperFloor.AddComponent<BoxCollider>();
                upperTile.center = Vector3.up * .025f;
                upperTile.size = new Vector3(2, .05f, 2);
                player.transform.position = platform.transform.position + Vector3.up * .07f;
                Physics.SyncTransforms();
                Assert.IsFalse(StandingOnFinish(), "The actual support belongs to another floor.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false), TestCase(true), Category("StuntRegression20261120")]
        public void DetachedLimbsDoNotEndAirborneMovementOrHideARealLandingSurface(bool childCollider)
        {
            var root = new GameObject("Debris support fixture");
            try
            {
                root.transform.position = new Vector3(15000, 0, 15000);
                var debris = new GameObject("Detached limb");
                debris.transform.SetParent(root.transform, false);
                debris.AddComponent<DismemberedLimb>();
                var shape = debris;
                if (childCollider)
                {
                    shape = new GameObject("Limb collider child");
                    shape.transform.SetParent(debris.transform, false);
                }
                shape.AddComponent<BoxCollider>().size = new Vector3(.7f, .1f, .7f);
                var floor = root.AddComponent<BoxCollider>();
                floor.center = Vector3.down * .26f;
                floor.size = new Vector3(3, .4f, 3);
                var feet = root.transform.position + Vector3.up * .07f;
                Physics.SyncTransforms();
                Assert.IsTrue(WalkableSupport.TryGetSupport(null, feet, out var support));
                Assert.AreSame(floor, support.collider);
                floor.enabled = false;
                Assert.IsFalse(WalkableSupport.Contains(null, feet));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(JumpKind.InPlace), TestCase(JumpKind.Fall), Category("StuntRegression20261120")]
        public void PursuitFireWaitsForTheLastPlayerToLandBeyondTheStageLine(JumpKind airborneMotion)
        {
            var root = new GameObject("Fire stage fixture");
            root.SetActive(false);
            var players = (List<NetworkPlayer>)typeof(NetworkPlayer)
                .GetField("all", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var added = new List<NetworkPlayer>();
            try
            {
                Transform Point(string name, float z)
                {
                    var point = new GameObject(name).transform;
                    point.SetParent(root.transform, false);
                    point.localPosition = Vector3.forward * z;
                    return point;
                }
                var route = root.AddComponent<HumanCannonRouteSystem>();
                var fire = root.AddComponent<HumanCannonFireWall>();
                void Set(string name, object value) =>
                    typeof(HumanCannonFireWall).GetField(name, Private).SetValue(fire, value);
                Set("start", Point("Start", 0));
                Set("boundary", Point("Boundary", 20));
                Set("visual", Point("Visual", 0));
                Set("stageLines", new[] { Point("Stage", 5) });
                Set("stageSpeedMultipliers", new[] { 1.5f });
                root.SetActive(true);
                for (int i = 0; i < 2; i++)
                {
                    var actor = Point("Teammate", 8 + i).gameObject.AddComponent<NetworkPlayer>();
                    typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(actor, (ulong)(900 + i));
                    typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(actor, true);
                    if (!players.Contains(actor)) players.Add(actor);
                    added.Add(actor);
                }
                Assert.IsTrue(fire.Arm(0));
                var jump = (Unity.Netcode.NetworkVariable<JumpKind>)typeof(NetworkPlayer).GetField("jump", Private).GetValue(added[1]);
                var boost = typeof(HumanCannonRouteSystem).GetMethod("BoostFire", Private);
                jump.Value = airborneMotion;
                Assert.IsFalse((bool)boost.Invoke(route, new object[] { fire }));
                Assert.That(fire.StageAt(0), Is.LessThan(0));
                jump.Value = JumpKind.None;
                Assert.IsTrue((bool)boost.Invoke(route, new object[] { fire }));
            }
            finally
            {
                foreach (var player in added)
                {
                    players.Remove(player);
                    typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test, Category("CommonRulesRegression")]
        public void SafeLandingUsesActualSupportAtCapsuleEdgeAndRejectsSeparateUpperFloor()
        {
            var root = new GameObject("safe edge fixture");
            var actor = new GameObject("landing capsule");
            root.transform.position = new Vector3(15000, 0, 15000);
            actor.transform.position = root.transform.position;
            try
            {
                var tile = root.AddComponent<BoxCollider>();
                tile.center = Vector3.down * .25f;
                tile.size = new Vector3(4, .5f, 4);
                var zone = root.AddComponent<HumanCannonLandingZone>();
                typeof(HumanCannonLandingZone).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(zone, null);
                var mover = actor.AddComponent<CurtainCall.Player.PlayerMover>();
                var body = actor.GetComponent<CharacterController>();
                body.height = 1.5f; body.center = Vector3.up * .75f; body.radius = .3f;
                var method = typeof(HumanCannonLandingZone).GetMethod("ContainsLanding", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(CurtainCall.Player.PlayerMover), typeof(Vector3) }, null);
                bool Safe(Vector3 feet) => (bool)method.Invoke(null, new object[] { mover, root.transform.position + feet });
                Physics.SyncTransforms();
                Assert.IsTrue(Safe(new Vector3(2.08f, .02f, 0)), "A capsule can still stand on the real top while its centre is beyond the edge.");
                Assert.IsFalse(Safe(new Vector3(2.5f, .02f, 0)), "No safe support beyond capsule contact.");
                var upper = new GameObject("ordinary surface"); upper.transform.SetParent(root.transform, false);
                var above = upper.AddComponent<BoxCollider>(); above.center = Vector3.up * .025f; above.size = new Vector3(2, .05f, 2);
                Physics.SyncTransforms();
                Assert.IsFalse(Safe(new Vector3(0, .07f, 0)), "A separate ordinary floor above a safe zone must not inherit its exemption.");
                above.enabled = false; tile.isTrigger = true;
                Assert.IsFalse(Safe(new Vector3(0, .02f, 0)), "Triggers are not safe supports.");
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(root); }
        }

        [TestCase(1, 5, 10)]
        [TestCase(2, 5, 8)]
        [TestCase(3, 5, 5)]
        [TestCase(1, 10, 20)]
        [TestCase(2, 10, 15)]
        [TestCase(3, 10, 10)]
        [TestCase(2, 15, 23)]
        [TestCase(1, -15, -15)]
        [TestCase(2, -10, -10)]
        [TestCase(3, 0, 0)]
        [TestCase(0, 5, 5)]
        public void TierCheerMultipliersRoundPositiveReactionsAndPreserveJeers(int tier, int amount, int expected)
        {
            var settings = ScriptableObject.CreateInstance<CurtainCall.Settings.AudienceReactionSettings>();
            try { Assert.AreEqual(expected, settings.ScaleReaction(amount, tier)); }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void TierCheerMultipliersCanBeTunedAndCannotOverflow()
        {
            var settings = ScriptableObject.CreateInstance<CurtainCall.Settings.AudienceReactionSettings>();
            try
            {
                settings.difficultyOneCheerMultiplier = 0f;
                Assert.AreEqual(0, settings.ScaleReaction(10, 1));
                settings.difficultyOneCheerMultiplier = 3f;
                Assert.AreEqual(30, settings.ScaleReaction(10, 1));
                Assert.AreEqual(int.MaxValue, settings.ScaleReaction(int.MaxValue, 1));
                settings.difficultyOneCheerMultiplier = float.NaN;
                Assert.AreEqual(10, settings.ScaleReaction(10, 1));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [TestCase(-5f, true)]
        [TestCase(0f, true)]
        [TestCase(79.99f, true)]
        [TestCase(80f, true)]
        [TestCase(100f, true)]
        [TestCase(110f, true)]
        public void CompletionIncludesAllValidProgressAfterSuccess(float distance, bool expected)
        {
            var settings = CurtainCall.Settings.AudienceReactionSettings.Current;
            Assert.IsNotNull(settings);
            float previous = settings.completionProgress;
            try
            {
                settings.completionProgress = .8f;
                Assert.AreEqual(expected, CurtainCall.Settings.AudienceReactionSettings.HasCompletionProgress(distance, 100f));
                Assert.IsFalse(CurtainCall.Settings.AudienceReactionSettings.HasCompletionProgress(distance, 0f));
                Assert.IsFalse(CurtainCall.Settings.AudienceReactionSettings.HasCompletionProgress(float.NaN, 100f));
                settings.completionProgress = .9f;
                Assert.IsTrue(CurtainCall.Settings.AudienceReactionSettings.HasCompletionProgress(0f, 100f), "이전 에셋의 진행률 제한은 적용하지 않는다");
            }
            finally { settings.completionProgress = previous; }
        }

        [Test]
        public void CannonsOnOnePlatformResolveTheSameSurfaceAndRespectUpperDeck()
        {
            var root = new GameObject("Platform fixture");
            try
            {
                HumanCannonLandingZone Make(string name, float height)
                {
                    var tile = new GameObject(name); tile.transform.SetParent(root.transform);
                    tile.transform.position = new Vector3(12000, height - .25f, 12000);
                    tile.AddComponent<BoxCollider>().size = new Vector3(12, .5f, 12);
                    var zone = tile.AddComponent<HumanCannonLandingZone>();
                    typeof(HumanCannonLandingZone).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic).Invoke(zone, null);
                    return zone;
                }
                var lower = Make("Lower", 0f);
                var upper = Make("Upper", 5f);
                Physics.SyncTransforms();
                Assert.AreSame(upper, HumanCannonLandingZone.PlatformBelow(new Vector3(11998, 5f, 12000)));
                Assert.AreSame(upper, HumanCannonLandingZone.PlatformBelow(new Vector3(12002, 5f, 12000)));
                Assert.AreSame(lower, HumanCannonLandingZone.PlatformBelow(new Vector3(12000, 1f, 12000)));
                upper.GetComponent<BoxCollider>().isTrigger = true;
                Assert.AreSame(lower, HumanCannonLandingZone.PlatformBelow(new Vector3(12000, 5f, 12000)));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TopAndSkinOffset_AreSafe()
        {
            var center = new Vector3(0, 0.1f, 0);
            var size = new Vector3(4, 0.2f, 4);
            Assert.IsTrue(CannonLandingSurface.Contains(new Vector3(0, 0.2f, 0), center, size, 0.12f));
            Assert.IsTrue(CannonLandingSurface.Contains(new Vector3(0, 0.28f, 0), center, size, 0.12f));
        }

        [TestCase(2f, 2f, true)]
        [TestCase(2.01f, 0f, false)]
        [TestCase(0f, -2.01f, false)]
        public void Boundary_SeparatesCushionFromOrdinaryFloor(float x, float z, bool expected)
        {
            Assert.That(CannonLandingSurface.Contains(new Vector3(x, 0.2f, z), new Vector3(0, 0.1f, 0),
                new Vector3(4, 0.2f, 4), 0.12f), Is.EqualTo(expected));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(2f)]
        public void UnderOrAboveCushion_IsNotSafe(float height)
        {
            Assert.IsFalse(CannonLandingSurface.Contains(new Vector3(0, height, 0), new Vector3(0, 0.1f, 0),
                new Vector3(4, 0.2f, 4), 0.12f));
        }

        [Test]
        public void InvalidGeometry_IsRejected()
        {
            Assert.IsFalse(CannonLandingSurface.Contains(Vector3.zero, Vector3.zero, Vector3.zero, 0.12f));
            Assert.IsFalse(CannonLandingSurface.Contains(Vector3.zero, Vector3.zero, Vector3.one, -1f));
            Assert.IsFalse(CannonLandingSurface.Contains(new Vector3(float.NaN, 0, 0), Vector3.zero, Vector3.one, 0.12f));
            Assert.IsFalse(CannonLandingSurface.Contains(Vector3.zero, Vector3.zero, Vector3.one, float.PositiveInfinity));
        }

        [Test]
        public void ScaledRotatedZone_UsesItsActualColliderTop()
        {
            var obj = new GameObject("LandingZoneTest") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                obj.transform.SetPositionAndRotation(new Vector3(6, 0.1f, 25), Quaternion.Euler(0, 45, 0));
                obj.transform.localScale = new Vector3(4, 0.2f, 2);
                var zone = obj.AddComponent<HumanCannonLandingZone>();
                Assert.IsTrue(zone.SupportsLanding(obj.transform.TransformPoint(new Vector3(0.4f, 0.5f, 0.4f))));
                Assert.IsFalse(zone.SupportsLanding(obj.transform.TransformPoint(new Vector3(0.6f, 0.5f, 0))));
                Assert.IsFalse(zone.SupportsLanding(new Vector3(6, 0, 25)));
                obj.GetComponent<BoxCollider>().isTrigger = true;
                Assert.IsFalse(zone.SupportsLanding(new Vector3(6, 0.2f, 25)));
                obj.GetComponent<BoxCollider>().isTrigger = false;
                zone.enabled = false;
                Assert.IsFalse(zone.SupportsLanding(new Vector3(6, 0.2f, 25)));
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void SafeLanding_CompletesOnlyCurrentFlightOnce()
        {
            var flights = new CannonFlightState();
            flights.Begin(4, -1, 12);
            Assert.IsTrue(flights.TryRebound(4, -1, 12, 0));
            Assert.IsFalse(flights.TryComplete(4, -1, 12, 0));
            Assert.IsTrue(flights.TryComplete(4, -1, 12, 1));
            Assert.IsFalse(flights.Contains(4));
            Assert.IsFalse(flights.TryComplete(4, -1, 12, 1));
            flights.Begin(4, 0, 13);
            Assert.IsFalse(flights.TryComplete(4, -1, 12, 1));
            Assert.IsTrue(flights.Matches(4, 0, 13, 0));
            flights.Clear();
            Assert.IsFalse(flights.TryComplete(4, 0, 13, 0));
        }
    }
}
