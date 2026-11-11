using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    [Category("ScheduleBoardInteractionRecovery")]
    public sealed class ScheduleBoardInteractionRecoveryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new();
        NetworkPlayer previousLocal, player;
        ScheduleBoard previousSchedule, schedule;
        CampaignRun previousRun, run;
        ScheduleBoardView view;
        bool previousHold;
        System.Func<bool> previousNightGate, previousSecondGate;
        static List<NetworkPlayer> All => (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", Static).GetValue(null);
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        GameObject Make(string name) { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(go); return go; }

        [SetUp]
        public void SetUp()
        {
            previousLocal = NetworkPlayer.Local; previousSchedule = ScheduleBoard.Instance; previousRun = CampaignRun.Instance;
            previousNightGate = RunState.NightEndGate; previousSecondGate = RunState.SecondPerformanceGate;
            previousHold = ScheduleScreenHold.IsHeld; ScheduleScreenHold.Set(false);
            run = Make("Schedule recovery run").AddComponent<CampaignRun>();
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
            Set(run, "state", new CampaignSnapshot { Phase = CampaignPhase.RestTime, Rest = RestKind.Morning, Day = 1, Generation = 1 });
            schedule = Make("Schedule recovery model").AddComponent<ScheduleBoard>();
            Set(schedule, "book", new ScheduleBook(new[] { "Tightrope", "HumanCannon" }, new[] { "Tightrope", "HumanCannon" }));
            typeof(ScheduleBoard).GetProperty("Instance").SetValue(null, schedule);
            var actor = Make("Schedule recovery owner");
            var mover = actor.AddComponent<PlayerMover>(); Call(mover, "Awake");
            actor.AddComponent<PlayerInputReader>();
            player = actor.AddComponent<NetworkPlayer>(); Set(player, "mover", mover);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            All.Add(player); typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
            view = Make("Schedule recovery view").AddComponent<ScheduleBoardView>(); Call(view, "Awake");
            view.SetOpen(true);
            Assert.IsTrue(view.IsOpen);
            Assert.IsTrue(ScheduleScreenHold.IsHeld);
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null)
            {
                All.Remove(player);
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false);
            }
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, previousLocal);
            typeof(ScheduleBoard).GetProperty("Instance").SetValue(null, previousSchedule);
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun);
            RunState.NightEndGate = previousNightGate; RunState.SecondPerformanceGate = previousSecondGate;
            ScheduleScreenHold.Set(previousHold);
        }

        void AssertClosed()
        {
            Assert.IsFalse(view.IsOpen, "A dead or deleted owner cannot retain the Dawn schedule-window barrier.");
            Assert.IsFalse(ScheduleScreenHold.IsHeld);
            Assert.IsFalse(((UIInputBlocker)Get(view, "blocker")).IsBlocking);
        }

        [TestCase(PlayerState.Fallen), TestCase(PlayerState.Retired)]
        public void DeathClosesPhysicalBoardAndReleasesInputWithoutChangingFirstDayPolicy(PlayerState state)
        {
            var book = (ScheduleBook)Get(schedule, "book");
            string first = book.Get(1, 0), second = book.Get(1, 1);
            bool firstEditable = schedule.CanEdit(1, 0), secondEditable = schedule.CanEdit(1, 1);
            ((NetworkVariable<PlayerState>)Get(player, "state")).Value = state;
            Call(view, "Update");
            AssertClosed();
            view.Interact(); view.SetOpen(true);
            AssertClosed();
            Assert.AreEqual(first, book.Get(1, 0)); Assert.AreEqual(second, book.Get(1, 1));
            Assert.AreEqual(firstEditable, schedule.CanEdit(1, 0)); Assert.AreEqual(secondEditable, schedule.CanEdit(1, 1));
        }

        [Test]
        public void DespawnedOwnerCannotLeaveScheduleWindowOrInputHeldOpen()
        {
            All.Remove(player);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, null);
            Object.DestroyImmediate(player.gameObject);
            Call(view, "Update");
            AssertClosed();
        }
    }
}
