using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.Network.Session;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    // Synthetic authority/state-order tests; no live clients, transport or PlayMode simulation.
    [Category("MedicalReservationRecovery")]
    [Category("CommonProgressionRegression")]
    public sealed class ExpertRoomRecoveryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new();
        readonly List<NetworkPlayer> players = new();
        readonly List<GameObject> existingRooms = new();
        ExpertService previousService, service;
        CampaignRun previousRun;
        NetworkPlayer previousLocal, local;
        ExpertRoomView room;
        ExpertServiceBook book;
        Transform inside, outside;
        static List<NetworkPlayer> All => (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", Static).GetValue(null);
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static void SetLocal(NetworkPlayer player) => typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);

        GameObject Make(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            objects.Add(go);
            return go;
        }

        NetworkPlayer Player(ulong id)
        {
            var go = Make("Medical recovery player " + id);
            var mover = go.AddComponent<PlayerMover>(); Call(mover, "Awake");
            var player = go.AddComponent<NetworkPlayer>(); Set(player, "mover", mover);
            typeof(NetworkBehaviour).GetProperty("OwnerClientId").SetValue(player, id);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            All.Add(player); players.Add(player);
            return player;
        }

        [SetUp]
        public void SetUp()
        {
            // Enter resolves the active scene entrance. Isolate this synthetic room when the
            // developer has the real WaitingRoom open, then restore that scene after the test.
            foreach (var existing in Object.FindObjectsByType<ExpertRoomView>(FindObjectsSortMode.None))
                if (existing.gameObject.activeInHierarchy) { existingRooms.Add(existing.gameObject); existing.gameObject.SetActive(false); }
            previousService = ExpertService.Instance; previousRun = CampaignRun.Instance; previousLocal = NetworkPlayer.Local;
            typeof(ExpertService).GetProperty("Instance").SetValue(null, null);
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, null);
            service = Make("Medical recovery service").AddComponent<ExpertService>(); Call(service, "Awake");
            book = (ExpertServiceBook)Get(service, "book");
            local = Player(0); SetLocal(local);
            room = Make("Medical recovery room").AddComponent<ExpertRoomView>();
            room.gameObject.hideFlags = HideFlags.HideInHierarchy; // The real Enter path excludes DontSave objects from its scene lookup.
            inside = Make("Medical inside").transform; inside.position = new Vector3(0, 0, 1.5f);
            outside = Make("Medical outside").transform; outside.position = new Vector3(0, 0, -1.4f);
            Set(room, "inside", inside); Set(room, "outside", outside);
            // Same mesh height as the active WaitingRoom lift (not a special interaction distance).
            var left = Make("Medical left door").transform; left.position = new Vector3(-.8f, 1.8f, 0);
            var right = Make("Medical right door").transform; right.position = new Vector3(.8f, 1.8f, 0);
            left.SetParent(room.transform, true); right.SetParent(room.transform, true);
            Set(room, "curtainLeft", left); Set(room, "curtainRight", right);
            Set(room, "leftClosed", left.localPosition); Set(room, "rightClosed", right.localPosition);
            local.GetComponent<PlayerMover>().SetStartPose(outside.position, Quaternion.identity);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var player in players)
            {
                All.Remove(player);
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false);
            }
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear(); players.Clear();
            typeof(ExpertService).GetProperty("Instance").SetValue(null, previousService);
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun);
            SetLocal(previousLocal);
            foreach (var existing in existingRooms) if (existing != null) existing.SetActive(true);
            existingRooms.Clear();
        }

        void BeginLocalWork()
        {
            Assert.AreEqual(ExpertRefusal.None, book.BookMedical(0, true, true, 1, 1, 3, () => true, 100));
            Assert.AreEqual(ExpertRefusal.None, book.Enter(0));
            Call(room, "Bind", service);
            Assert.IsTrue((bool)Get(room, "localInside"));
            Assert.IsTrue(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
        }

        [Test]
        public void DeathReservationCanBeReleasedBeforeUpdateAndNextBuyerCanEnter()
        {
            var previous = Player(941);
            book.BookMedical(previous.OwnerClientId, true, true, 2, 2, 3, () => true, 150);
            ((NetworkVariable<PlayerState>)Get(previous, "state")).Value = PlayerState.Fallen;
            // The new request itself must clear the dead buyer; no service Update has run.
            var purchase = ((bool ok, string text))Call(service, "BuyMedical", 0UL, 1);
            Assert.IsFalse(purchase.ok, "This uninjured test avatar cannot purchase; the old dead reservation must still clear.");
            Assert.AreEqual(ExpertRoomPhase.Idle, book.Phase);
            Assert.AreEqual(150, book.PendingRefund);
            Call(service, "RefreshReservation");
            Assert.AreEqual(150, book.PendingRefund, "Repeated cleanup cannot add another refund.");
            Assert.AreEqual(ExpertRefusal.None, book.BookMedical(0, true, true, 1, 1, 3, () => true, 125));
            Call(room, "Bind", service);
            Physics.SyncTransforms();
            var result = ((bool ok, string text))Call(service, "Enter", 0UL);
            Assert.IsTrue(result.ok, result.text);
            Assert.AreEqual(ExpertRoomPhase.Working, book.Phase);
            Assert.IsTrue((bool)Get(room, "localInside"));
            Assert.Less(Vector3.Distance(local.transform.position, inside.position), .001f);
        }

        [Test]
        public void LateWorkingSnapshotEntersAndChangedBuyerSnapshotReleasesLocalInput()
        {
            BeginLocalWork();
            // A later synchronization can retain Working while replacing the reservation owner.
            Call(service, "HandleState", "{\"revision\":50,\"phase\":2,\"buyer\":942,\"limbs\":1,\"uses\":2}");
            Call(room, "ReconcileLocalOccupancy");
            Assert.IsFalse((bool)Get(room, "localInside"));
            Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
            Assert.Less(Vector3.Distance(local.transform.position, outside.position), .001f);
        }

        [TestCase(2, 0)] [TestCase(2, 1)]
        [TestCase(3, 0)] [TestCase(3, 1)] [TestCase(3, 2)]
        [TestCase(4, 0)] [TestCase(4, 1)] [TestCase(4, 3)]
        public void MountedBuyerWaitsForDetachAndDoesNotSnapBackAfterEntering(int count, int buyerFloor)
        {
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(local, true);
            var previousStack = PiggybackSystem.Current;
            var stack = Make("Medical mounted stack").AddComponent<PiggybackSystem>();
            var follow = stack.gameObject.AddComponent<PiggybackFollow>(); Set(follow, "system", stack);
            var session = Make("Medical authority").AddComponent<NetworkSessionManager>();
            typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
            Set(stack, "session", session);
            typeof(PiggybackSystem).GetProperty("Current").SetValue(null, stack);
            try
            {
                var below = (Dictionary<ulong, ulong>)Get(stack, "belowOf");
                var above = (Dictionary<ulong, ulong>)Get(stack, "aboveOf");
                var riders = new NetworkPlayer[count];
                for (int i = 0; i < count; i++)
                {
                    riders[i] = i == buyerFloor ? local : Player((ulong)(950 + i));
                    riders[i].gameObject.AddComponent<PlayerBalance>();
                    riders[i].transform.position = outside.position + Vector3.up * i * 1.5f;
                    if (i == 0) continue;
                    below[riders[i].OwnerClientId] = riders[i - 1].OwnerClientId;
                    above[riders[i - 1].OwnerClientId] = riders[i].OwnerClientId;
                }
                Call(follow, "UpdateLocal", local);
                Assert.AreEqual(ExpertRefusal.None, book.BookMedical(0, true, true, 1, 1, 3, () => true, 100));
                Assert.AreEqual(ExpertRefusal.None, book.Enter(0));
                Call(room, "Bind", service); // Service snapshot deliberately arrives before detach.
                Assert.IsFalse((bool)Get(room, "localInside"));
                stack.ServerReleaseForRelocation(0);
                Call(room, "ReconcileLocalOccupancy");
                Assert.IsTrue((bool)Get(room, "localInside"));
                Call(follow, "UpdateLocal", local); // Late follow must not put the buyer beside the old carrier.
                Assert.Less(Vector3.Distance(local.transform.position, inside.position), .001f);
                Assert.IsFalse(stack.IsInStack(local));
                Assert.IsTrue(local.GetComponent<PlayerMover>().Simulated);
                for (int i = buyerFloor + 1; i < count; i++) Assert.IsFalse(stack.IsInStack(riders[i]));
            }
            finally { typeof(PiggybackSystem).GetProperty("Current").SetValue(null, previousStack); }
        }

        [TestCase(true), TestCase(false)]
        public void CancelAlwaysReleasesInputWhenPlayerOrReturnPointIsMissing(bool missingPlayer)
        {
            BeginLocalWork();
            if (missingPlayer) SetLocal(null); else Set(room, "outside", null);
            book.CancelUnfulfilled();
            Call(room, "ReconcileLocalOccupancy");
            Assert.IsFalse((bool)Get(room, "localInside"));
            Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ReservedInjuredRiderCanBoardAndTheHostDetachesTheirLink(int lostParts)
        {
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(local, true);
            local.gameObject.AddComponent<PlayerCondition>().SetLostParts(PlayerCondition.PartsForLostCount(lostParts));
            var support = Player(970);
            support.transform.position = outside.position - Vector3.up * 1.5f;
            var previousStack = PiggybackSystem.Current;
            var stack = Make("Medical entry stack").AddComponent<PiggybackSystem>();
            var follow = stack.gameObject.AddComponent<PiggybackFollow>(); Set(follow, "system", stack);
            var session = Make("Medical authority").AddComponent<NetworkSessionManager>();
            typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
            Set(stack, "session", session);
            typeof(PiggybackSystem).GetProperty("Current").SetValue(null, stack);
            try
            {
                ((Dictionary<ulong, ulong>)Get(stack, "belowOf"))[0] = support.OwnerClientId;
                ((Dictionary<ulong, ulong>)Get(stack, "aboveOf"))[support.OwnerClientId] = 0;
                Call(follow, "UpdateLocal", local);
                Assert.AreEqual(ExpertRefusal.None, book.BookMedical(0, true, true, lostParts, 1, 3, () => true, 100));
                Physics.SyncTransforms();
                Assert.IsTrue((bool)Call(room, "CanBoard", local));
                Assert.IsFalse((bool)Call(room, "CanBoard", support));
                var result = ((bool entered, string reason))Call(service, "Enter", 0UL);
                Assert.IsTrue(result.entered, result.reason);
                Call(room, "Bind", service);
                Assert.AreEqual(ExpertRoomPhase.Working, book.Phase);
                Assert.IsFalse(stack.IsInStack(local));
                Assert.Less(Vector3.Distance(local.transform.position, inside.position), .001f);
            }
            finally { typeof(PiggybackSystem).GetProperty("Current").SetValue(null, previousStack); }
        }

        [Test]
        public void DeathInsideReleasesInputWithoutReenablingCorpseController()
        {
            BeginLocalWork();
            var mover = local.GetComponent<PlayerMover>();
            ((NetworkVariable<PlayerState>)Get(local, "state")).Value = PlayerState.Fallen;
            mover.Body.enabled = false; mover.SetControlEnabled(false);
            Call(room, "ReconcileLocalOccupancy");
            Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
            Assert.AreEqual(PlayerState.Fallen, local.State);
            Assert.IsFalse(mover.Body.enabled);
            Assert.IsFalse(mover.ControlEnabled);
        }

        [TestCase(false), TestCase(true)]
        public void LostCabinOwnerReleasesLockWithoutMovingReplacementOrReenteringAsOffline(bool replaceOwner)
        {
            BeginLocalWork();
            All.Remove(local);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(local, false);
            SetLocal(null);
            NetworkPlayer replacement = replaceOwner ? Player(0) : null;
            var untouched = new Vector3(100, 0, 100);
            if (replacement != null) { replacement.transform.position = untouched; SetLocal(replacement); }
            Call(room, "ReconcileLocalOccupancy");
            Call(room, "ReconcileLocalOccupancy");
            Assert.AreEqual(ExpertRoomPhase.Working, book.Phase, "The delayed old reservation has not cleared yet.");
            Assert.IsFalse((bool)Get(room, "localInside"));
            Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
            if (replacement != null) Assert.AreEqual(untouched, replacement.transform.position);
        }

        [Test]
        public void InjuredBuyerCanReachBoardingPointButStillCannotReachThroughWallOrBeyondCommonRange()
        {
            var condition = local.gameObject.AddComponent<PlayerCondition>(); condition.SetLostParts(PlayerCondition.OneArmParts);
            local.transform.position = outside.position + Vector3.left * .5f;
            Physics.SyncTransforms();
            Assert.GreaterOrEqual(room.Reach(local.transform.position, local), 0f,
                "The old door-centre-plus-one-metre target was above the injured reach sphere.");
            float reach = GameSettings.Base.Interaction.Distance * GameSettings.Base.BodyDamage.ReachMultiplier(1);
            local.transform.position = outside.position + Vector3.left * (reach + .1f);
            Physics.SyncTransforms();
            Assert.Less(room.Reach(local.transform.position, local), 0f);
            local.transform.position = outside.position + Vector3.left * .5f;
            var wall = Make("Independent wall").AddComponent<BoxCollider>();
            wall.size = new Vector3(.1f, 2f, 1f);
            wall.transform.position = outside.position + new Vector3(-.25f, .75f, 0);
            Physics.SyncTransforms();
            Assert.Less(room.Reach(local.transform.position, local), 0f, "The shared wall/height/reach policy remains active.");
        }
    }
}
