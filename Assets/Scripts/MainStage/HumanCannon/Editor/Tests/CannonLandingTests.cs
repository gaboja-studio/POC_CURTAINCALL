using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class CannonLandingTests
    {
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
