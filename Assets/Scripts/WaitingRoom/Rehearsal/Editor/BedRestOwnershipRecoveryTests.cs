using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using BodyPart = CurtainCall.Player.BodyPart;

namespace CurtainCall.WaitingRoom.EditorTools
{
    [Category("BedRestOwnershipRecovery")]
    [Category("CommonProgressionRegression")]
    public sealed class BedRestOwnershipRecoveryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new();
        readonly List<NetworkPlayer> players = new();
        CampaignRun previousRun, run;
        NetworkPlayer previousLocal, owner;
        BedRestView rest;
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static void Local(NetworkPlayer player) => typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
        GameObject Make(string name) { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(go); return go; }
        NetworkPlayer Player()
        {
            var go = Make("Bed test owner");
            var mover = go.AddComponent<PlayerMover>(); Call(mover, "Awake");
            var player = go.AddComponent<NetworkPlayer>(); Set(player, "mover", mover);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(player, true);
            players.Add(player); return player;
        }

        [SetUp]
        public void SetUp()
        {
            previousRun = CampaignRun.Instance; previousLocal = NetworkPlayer.Local;
            run = Make("Bed recovery run").AddComponent<CampaignRun>();
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
            Set(run, "state", new CampaignSnapshot { Phase = CampaignPhase.Night, Day = 2, Generation = 1 });
            Set(run, "localReady", true);
            owner = Player(); Local(owner); owner.SetRestingBed(0);
            rest = Make("Bed recovery view").AddComponent<BedRestView>();
            rest.Begin(new Bounds(Vector3.zero, new Vector3(2, .5f, 1)), Quaternion.identity);
            Assert.IsTrue(rest.IsResting);
        }

        [TearDown]
        public void TearDown()
        {
            if (rest != null) Call(rest, "End");
            foreach (var player in players) if (player != null) typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false);
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear(); players.Clear();
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun); Local(previousLocal);
        }

        [TestCase(PlayerState.Fallen), TestCase(PlayerState.Retired)]
        public void DeathEndsBedSessionAndClearsItsOwnersPresentation(PlayerState state)
        {
            ((NetworkVariable<PlayerState>)Get(owner, "state")).Value = state;
            Call(rest, "Update");
            Assert.IsFalse(rest.IsResting);
            Assert.IsNull(Get(rest, "blocker"));
            Assert.AreEqual(-1, owner.RestingBed);
            Assert.AreEqual(state, owner.State);
        }

        [Test]
        public void ReplacingLocalOwnerClosesOldBedWithoutChangingNewOwnersBed()
        {
            var next = Player(); next.SetRestingBed(7); Local(next);
            Call(rest, "Update");
            Assert.IsFalse(rest.IsResting); Assert.IsNull(Get(rest, "blocker"));
            Assert.AreEqual(-1, owner.RestingBed);
            Assert.AreEqual(7, next.RestingBed);
        }

        [Test]
        public void DeletingLocalOwnerReleasesBedCameraAndInputLock()
        {
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(owner, false);
            Local(null); Object.DestroyImmediate(owner.gameObject);
            Call(rest, "Update");
            Assert.IsFalse(rest.IsResting); Assert.IsNull(Get(rest, "blocker"));
        }

        [Test]
        public void OfflineDevelopmentBedDoesNotRequireANetworkAvatar()
        {
            Call(rest, "End"); Local(null); Set(run, "localReady", true);
            rest.Begin(new Bounds(Vector3.zero, Vector3.one), Quaternion.identity);
            Call(rest, "Update");
            Assert.IsTrue(rest.IsResting);
        }

        NetworkBedPose PoseAtBed(out PlayerModelSlot slot, out GameObject modelPrefab)
        {
            var bed = new GameObject("Bed pose test");
            objects.Add(bed);
            var nightBed = bed.AddComponent<NightBed>();
            owner.SetRestingBed(nightBed.BedIndex);
            Assert.That(owner.RestingBed, Is.GreaterThanOrEqualTo(0));
            slot = owner.gameObject.AddComponent<PlayerModelSlot>();
            Set(slot, "fitToHeight", false);
            modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Characters/Players/Models/Rabbit/Rabbit.prefab");
            slot.SetModel(modelPrefab);
            var pose = owner.gameObject.AddComponent<NetworkBedPose>();
            Call(pose, "Awake");
            Call(pose, "LateUpdate");
            Assert.IsTrue(pose.IsLying);
            Assert.IsFalse(slot.CurrentModel.GetComponent<HumanoidModel>().Animator.enabled);
            return pose;
        }

        [Test]
        public void EndingBedPoseDoesNotReactivateDetachedRagdollAnimator()
        {
            var pose = PoseAtBed(out var slot, out _);
            var detached = slot.DetachModel();
            objects.Add(detached);
            var model = detached.GetComponent<HumanoidModel>();
            model.StopMotion();
            ((NetworkVariable<PlayerState>)Get(owner, "state")).Value = PlayerState.Fallen;
            owner.SetRestingBed(-1);
            Call(pose, "LateUpdate");
            Assert.IsFalse(pose.IsLying);
            Assert.IsFalse(model.Animator.enabled);
        }

        [Test]
        public void AReplacementAvatarKeepsItsBedPoseAndCanWake()
        {
            var pose = PoseAtBed(out var slot, out var modelPrefab);
            var detached = slot.DetachModel();
            objects.Add(detached);
            slot.SetModel(modelPrefab);
            var animator = slot.CurrentModel.GetComponent<HumanoidModel>().Animator;
            Call(pose, "LateUpdate");
            Assert.IsTrue(pose.IsLying);
            Assert.IsFalse(animator.enabled);
            Assert.IsFalse(detached.GetComponent<HumanoidModel>().Animator.enabled);
            owner.SetRestingBed(-1);
            Call(pose, "LateUpdate");
            Assert.IsTrue(animator.enabled);
        }

        void WithRehearsal(bool cannon, System.Action<Component> action)
        {
            var type = cannon ? typeof(WaitingRoomCannonRehearsal) : typeof(WaitingRoomRehearsal);
            var singleton = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = singleton.GetValue(null);
            var component = Make("Bed rehearsal handoff").AddComponent(type);
            singleton.SetValue(null, component);
            try
            {
                if (cannon)
                {
                    Call(component, "AwakeNetwork");
                    var shared = Get(component, "shared"); shared.GetType().GetField("active").SetValue(shared, true);
                }
                else { Set(component, "active", true); Set(component, "run", run); Set(component, "local", owner); }
                action(component);
            }
            finally { singleton.SetValue(null, previous); }
        }

        [Test, Category("RehearsalEntryHandoff")]
        public void RehearsalMovementClearsBedCameraLockAndPoseWithoutHealing(
            [Values(false, true)] bool cannon, [Values(0, 1, 2, 3)] int lost, [Values(false, true)] bool updateFirst)
        {
            using var gate = new CurtainCall.Tightrope.EditorTools.Tests.InputGateTestScope();
            rest.gameObject.hideFlags = HideFlags.None; // Runtime discovery intentionally excludes DontSave objects.
            var parts = PlayerCondition.PartsForLostCount(lost);
            ((NetworkVariable<BodyPart>)Get(owner, "lostParts")).Value = parts;
            var condition = owner.gameObject.AddComponent<PlayerCondition>(); condition.SetLostParts(parts);
            var pose = PoseAtBed(out var slot, out _);
            var blocker = (UIInputBlocker)Get(rest, "blocker");
            WithRehearsal(cannon, component =>
            {
                if (updateFirst) Call(rest, "Update");
                else if (cannon)
                    typeof(BedRestView).GetMethod("EndForRehearsal", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                else
                {
                    // Supply the existing guide owner and suppress only its display in EditMode.
                    var panel = typeof(WaitingRoomTutorialPanel).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
                    var previousPanel = panel.GetValue(null); bool previousHold = ScheduleScreenHold.IsHeld;
                    panel.SetValue(null, Make("Bed handoff guide").AddComponent<WaitingRoomTutorialPanel>());
                    typeof(ScheduleScreenHold).GetProperty("IsHeld").SetValue(null, true);
                    try { Call(component, "ActivateLocal"); }
                    finally { panel.SetValue(null, previousPanel); typeof(ScheduleScreenHold).GetProperty("IsHeld").SetValue(null, previousHold); }
                }
                var mover = owner.GetComponent<PlayerMover>();
                if (cannon) mover.SetStartPose(new Vector3(9100, 9000, 9000), Quaternion.identity);
                else Call(component, "Relocate", mover, new Vector3(9100, 9000, 9000), Quaternion.identity);
                Call(pose, "LateUpdate");
                Assert.IsFalse(rest.IsResting); Assert.IsNull(Get(rest, "blocker"));
                Assert.IsTrue(blocker == null || !blocker.IsBlocking);
                Assert.IsFalse(run.IsLocalReady); Assert.AreEqual(-1, owner.RestingBed);
                Assert.IsFalse(pose.IsLying); Assert.IsTrue(slot.CurrentModel.GetComponent<HumanoidModel>().Animator.enabled);
                Assert.That(Vector3.Distance(slot.CurrentModel.transform.position, owner.transform.position), Is.LessThan(3));
                Assert.AreEqual(parts, owner.LostParts); Assert.AreEqual(parts, condition.LostParts);
                Assert.AreEqual(PlayerState.Normal, owner.State);
                // A late Begin call must not recreate a camera after the handoff.
                rest.Begin(new Bounds(Vector3.zero, Vector3.one), Quaternion.identity);
                Assert.IsFalse(rest.IsResting);
            });
            Set(run, "localReady", true);
            rest.Begin(new Bounds(Vector3.zero, Vector3.one), Quaternion.identity);
            Assert.IsTrue(rest.IsResting, "Normal bed use must recover after rehearsal ends.");
        }

        [TestCase(false), TestCase(true), Category("RehearsalEntryHandoff")]
        public void RemoteBedPoseWaitsForRehearsalEvenIfBedClearArrivesLater(bool cannon)
        {
            var pose = PoseAtBed(out var slot, out _);
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(owner, false);
            WithRehearsal(cannon, component =>
            {
                owner.transform.position = new Vector3(9100, 9000, 9000);
                Call(pose, "LateUpdate");
                Assert.IsFalse(pose.IsLying);
                Assert.IsTrue(slot.CurrentModel.GetComponent<HumanoidModel>().Animator.enabled);
                Assert.That(owner.RestingBed, Is.GreaterThanOrEqualTo(0), "Simulate an owner-written bed value arriving late.");
                ((NetworkVariable<int>)Get(owner, "restingBed")).Value = -1;
                Call(pose, "LateUpdate"); Assert.IsFalse(pose.IsLying);
            });
            Call(pose, "LateUpdate"); Assert.IsFalse(pose.IsLying);
        }
    }
}
