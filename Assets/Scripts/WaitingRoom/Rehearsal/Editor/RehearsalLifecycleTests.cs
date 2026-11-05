using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using CurtainCall.Survival;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    public sealed class RehearsalLifecycleTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new();
        readonly List<NetworkPlayer> players = new();
        CampaignRun previousRun;
        NetworkPlayer previousLocal;
        object previousCannon, previousRehearsal;
        int previousStage;
        CampaignRun run;
        WaitingRoomCannonRehearsal cannon;
        WaitingRoomRehearsal rehearsal;
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static FieldInfo Singleton<T>() => typeof(T).GetField("instance", Static);
        static List<NetworkPlayer> All => (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", Static).GetValue(null);
        void Shared(string name, object value)
        {
            var shared = Get(cannon, "shared");
            shared.GetType().GetField(name).SetValue(shared, value);
        }
        GameObject Make(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            objects.Add(go);
            return go;
        }
        [SetUp]
        public void SetUp()
        {
            previousRun = CampaignRun.Instance;
            previousLocal = NetworkPlayer.Local;
            previousCannon = Singleton<WaitingRoomCannonRehearsal>().GetValue(null);
            previousRehearsal = Singleton<WaitingRoomRehearsal>().GetValue(null);
            previousStage = (int)typeof(WaitingRoomRehearsal).GetField("savedStage", Static).GetValue(null);
            run = Make("Lifecycle campaign").AddComponent<CampaignRun>();
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
            var state = new RunState();
            state.StartSession(true); state.EndIntro(); state.EndRehearsal(); state.EndPerformance();
            Set(run, "host", state); Set(run, "state", state.Snapshot); Set(run, "offlineRun", true);
            cannon = Make("Lifecycle cannon").AddComponent<WaitingRoomCannonRehearsal>();
            Call(cannon, "AwakeNetwork");
            Singleton<WaitingRoomCannonRehearsal>().SetValue(null, cannon);
            rehearsal = Make("Lifecycle tightrope").AddComponent<WaitingRoomRehearsal>();
            Singleton<WaitingRoomRehearsal>().SetValue(null, rehearsal);
            Set(rehearsal, "run", run);
            typeof(WaitingRoomRehearsal).GetField("savedStage", Static).SetValue(null, RehearsalStage.All.Length);
        }
        [TearDown]
        public void TearDown()
        {
            foreach (var p in players)
            {
                All.Remove(p);
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(p, false);
            }
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear(); players.Clear();
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, previousLocal);
            Singleton<WaitingRoomCannonRehearsal>().SetValue(null, previousCannon);
            Singleton<WaitingRoomRehearsal>().SetValue(null, previousRehearsal);
            typeof(WaitingRoomRehearsal).GetField("savedStage", Static).SetValue(null, previousStage);
        }
        NetworkPlayer Player(ulong id)
        {
            var go = Make("Lifecycle player " + id);
            var mover = go.AddComponent<PlayerMover>(); Call(mover, "Awake");
            var player = go.AddComponent<NetworkPlayer>(); Set(player, "mover", mover);
            typeof(NetworkBehaviour).GetProperty("OwnerClientId").SetValue(player, id);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            All.Add(player); players.Add(player);
            return player;
        }

        [TestCase("seat")] [TestCase("flight")] [TestCase("transport")]
        public void CannonEndingRestoresMovementOwnedByNativeDevice(string motion)
        {
            var player = Player(901);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
            var mover = player.GetComponent<PlayerMover>();
            mover.SetControlEnabled(true); mover.Simulated = true;
            var pilot = player.gameObject.AddComponent<HumanCannonPilot>();
            Set(pilot, "mover", mover); Set(pilot, "player", player);
            Call(pilot, "TakeMovement");
            if (motion == "seat") Set(pilot, "stationIndex", 0);
            else if (motion == "flight") typeof(HumanCannonPilot).GetProperty("IsFlying").SetValue(pilot, true);
            else Set(pilot, "onTransport", true);
            Assert.That(pilot.IsDriving, Is.True);
            Assert.That(mover.Simulated, Is.False);
            Shared("active", true); Shared("ending", true); Shared("round", 3);
            Call(cannon, "CaptureEndingMover");
            // The native course's OnDisable detaches the pilot when Cleanup removes it.
            Call(pilot, "Detach");
            Assert.That(mover.Simulated, Is.False, "기물 정리 후에도 종료 복귀 전 물리가 잠겨 있어야 한다.");
            Call(cannon, "ReleaseEndingLock");
            Assert.That(mover.Simulated && mover.ControlEnabled, Is.True, "탑승/비행 때의 임시 정지가 종료 후 남으면 안 된다.");
        }

        [TestCase(false)] [TestCase(true)]
        public void PendingTightropeVoteCannotStartOverCannon(bool ending)
        {
            var player = Player(902);
            Shared("active", true); Shared("ending", ending);
            Set(rehearsal, "practiceVote", true);
            ((HashSet<ulong>)Get(rehearsal, "practiceYes")).Add(player.OwnerClientId);
            Call(rehearsal, "HostTickVotes");
            Assert.That(Get(rehearsal, "practice"), Is.False, "한 연습실에서 두 리허설이 동시에 시작되면 안 된다.");
            Assert.That(Get(rehearsal, "practiceVote"), Is.False);
        }

        [TestCase(true, true)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(false, false)]
        public void LeavingPracticePhaseCancelsPendingReturnAndAllLocalLocks(bool active, bool host)
        {
            var player = Player(903);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
            var mover = player.GetComponent<PlayerMover>();
            mover.SetControlEnabled(true); mover.Simulated = true;
            Shared("active", active); Shared("ending", true); Shared("round", 2);
            Call(cannon, "CaptureEndingMover");
            var blocks = new List<UIInputBlocker>();
            foreach (string name in new[] { "promptBlock", "placementBlock", "endingBlock" })
            {
                var block = Make(name).AddComponent<UIInputBlocker>();
                block.BlockWhileActive = false; block.SetOpen(true);
                Set(cannon, name, block); blocks.Add(block);
            }
            var fader = Make("Pending return fader").AddComponent<RehearsalScreenFader>();
            Set(fader, "canvasRoot", Make("Pending return cover")); Set(cannon, "fader", fader);
            Set(cannon, "prompt", true);
            var state = run.State; state.Phase = CampaignPhase.Performance; Set(run, "state", state);
            if (!host) Set(run, "host", null);
            Vector3 position = player.transform.position;
            Assert.That(Call(cannon, "UpdatePracticeContext"), Is.False);
            foreach (var block in blocks) Assert.That(block.IsBlocking, Is.False);
            Assert.That(mover.Simulated && mover.ControlEnabled, Is.True);
            Assert.That(fader.IsActive, Is.False);
            Assert.That(Get(cannon, "prompt"), Is.False);
            Assert.That(Get(cannon, "returnedRound"), Is.EqualTo(-1));
            Assert.That(player.transform.position, Is.EqualTo(position), "다음 공연 배치를 옛 복귀 위치로 덮어쓰면 안 된다.");
            if (host)
            {
                var shared = Get(cannon, "shared");
                Assert.That(shared.GetType().GetField("active").GetValue(shared), Is.False);
                Assert.That(shared.GetType().GetField("ending").GetValue(shared), Is.False);
            }
        }

        [Test]
        public void RemoteStartClosesAnOlderSchedulePrompt()
        {
            Call(cannon, "AwakeUi");
            Set(cannon, "prompt", true); Shared("active", true);
            Call(cannon, "TickUi");
            Assert.That(Get(cannon, "prompt"), Is.False);
            Assert.That(((UIInputBlocker)Get(cannon, "promptBlock")).IsBlocking, Is.False);
        }

        [Test]
        public void NewRoomRestartsCannonActionGuidesEvenWhenRoundNumberRepeats()
        {
            Set(run, "host", null);
            Set(cannon, "guideRound", 1);
            ((HashSet<string>)Get(cannon, "actionGuides")).Add("cannon");
            ((Queue<(string title, string body)>)Get(cannon, "pendingGuides")).Enqueue(("Old", "Room"));
            Call(cannon, "ResetConnection");
            Assert.That(Get(cannon, "guideRound"), Is.EqualTo(-1));
            Assert.That((HashSet<string>)Get(cannon, "actionGuides"), Is.Empty);
            Assert.That((Queue<(string title, string body)>)Get(cannon, "pendingGuides"), Is.Empty);
        }

        [TestCase("practiceVote")] [TestCase("practice")]
        public void WaitingRoomStartRequestsAreExclusiveBeforeCourseActivation(string pending)
        {
            var oldDirector = SpaceLinkDirector.Instance;
            var oldScene = SceneManager.GetActiveScene();
            string oldName = oldScene.name;
            try
            {
                // The EditMode runner owns an unsaved scene; restore its name without adding/saving any scene.
                oldScene.name = GameSettings.Base.SpaceTransition.WaitingRoomScene;
                var director = Make("Lifecycle space director").AddComponent<SpaceLinkDirector>();
                typeof(SpaceLinkDirector).GetProperty("Instance").SetValue(null, director);
                var state = run.State; state.CannonUnlocked = true; Set(run, "state", state);
                Assert.That(WaitingRoomCannonRehearsal.CanPractice, Is.True);
                Assert.That(WaitingRoomRehearsal.CanProposePractice, Is.True);
                Shared("active", true);
                Assert.That(WaitingRoomRehearsal.CanProposePractice, Is.False);
                Shared("active", false); Set(rehearsal, pending, true);
                Assert.That(WaitingRoomCannonRehearsal.CanPractice, Is.False);
                Assert.That(WaitingRoomRehearsal.CanProposePractice, Is.False);
            }
            finally
            {
                typeof(SpaceLinkDirector).GetProperty("Instance").SetValue(null, oldDirector);
                oldScene.name = oldName;
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void FirstDayTeamWaitDoesNotLockPlayersInsideRehearsal(bool cannonLesson)
        {
            var oldFlow = Singleton<WaitingRoomFirstDayFlow>().GetValue(null);
            var memoryOnly = typeof(WaitingRoomTutorial).GetField("memoryOnly", Static);
            var memoryEnabled = typeof(WaitingRoomTutorial).GetField("memoryEnabled", Static);
            var oldMemory = memoryOnly.GetValue(null); var oldEnabled = memoryEnabled.GetValue(null);
            try
            {
                // This owner has finished/disabled the guide; a teammate still needs it.
                memoryOnly.SetValue(null, true); memoryEnabled.SetValue(null, false);
                var flow = Make("Lifecycle first day flow").AddComponent<WaitingRoomFirstDayFlow>();
                Call(flow, "Awake");
                Set(flow, "tutorialRun", run); Set(flow, "tutorialDoneKey", run.State.Generation + "/" + run.State.Day);
                var player = Player(904); typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
                if (cannonLesson) Shared("active", true);
                else { Set(rehearsal, "active", true); Set(rehearsal, "practice", true); }
                Assert.That(run.State.Timer, Is.EqualTo(RestTimerState.WaitingForExit));
                Call(flow, "LateUpdate");
                var blockers = (HashSet<object>)typeof(InputGate).GetField("blockers", Static).GetValue(null);
                Assert.That(blockers.Contains(Get(flow, "teamLock")), Is.False,
                    "리허설이 일반 안내를 중단한 동안 안내 전원 완료 잠금이 걸리면 진행할 수 없다.");
                Assert.That(run.State.Timer, Is.EqualTo(RestTimerState.WaitingForExit), "안내 완료나 타이머를 강제로 건너뛰지 않는다.");
                Shared("active", false); Set(rehearsal, "active", false); Set(rehearsal, "practice", false);
                Call(flow, "LateUpdate");
                Assert.That(blockers.Contains(Get(flow, "teamLock")), Is.True, "리허설 뒤에는 기존 전원 안내 대기로 돌아간다.");
            }
            finally
            {
                memoryOnly.SetValue(null, oldMemory); memoryEnabled.SetValue(null, oldEnabled);
                Singleton<WaitingRoomFirstDayFlow>().SetValue(null, oldFlow);
            }
        }

        [TestCase(CampaignPhase.RestTime)] [TestCase(CampaignPhase.Night)]
        public void HostFinisherReturnsToNormalAlongWithTeammatesBeforeWaitingRoomGuides(CampaignPhase phase)
        {
            for (ulong id = 0; id < 4; id++)
            {
                var player = Player(id);
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
                if (id == 0)
                {
                    typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
                    ((NetworkVariable<PlayerState>)Get(player, "state")).Value = PlayerState.Arrived;
                }
            }
            Assert.That(NetworkPlayer.Local.State, Is.EqualTo(PlayerState.Arrived));
            var step = run.State; step.Phase = phase;
            InjuryRevival.ServerApplyStep(step);
            Assert.That(NetworkPlayer.Local.State, Is.EqualTo(PlayerState.Normal), "완주한 호스트도 가이드와 상호작용의 Normal 조건을 만족해야 한다.");
            foreach (var player in players) Assert.That(player.State, Is.EqualTo(PlayerState.Normal));
        }

        [Test]
        public void ReusedCampaignRunReportsPreviouslySeenGuidesInTheNewSession()
        {
            var oldFlow = Singleton<WaitingRoomFirstDayFlow>().GetValue(null);
            var memoryOnly = typeof(WaitingRoomTutorial).GetField("memoryOnly", Static);
            var memoryEnabled = typeof(WaitingRoomTutorial).GetField("memoryEnabled", Static);
            var oldMemory = memoryOnly.GetValue(null); var oldEnabled = memoryEnabled.GetValue(null);
            try
            {
                memoryOnly.SetValue(null, true); memoryEnabled.SetValue(null, false);
                typeof(NetworkPlayer).GetProperty("Local").SetValue(null, null);
                var flow = Make("Rehosted first day flow").AddComponent<WaitingRoomFirstDayFlow>(); Call(flow, "Awake");
                Call(flow, "LateUpdate");
                Assert.That(run.State.Timer, Is.Not.EqualTo(RestTimerState.WaitingForExit));
                // Leaving the first guide/run must clear only the sent acknowledgement, not seen guides.
                Call(flow, "LateUpdate");
                var next = new RunState();
                next.StartSession(true); next.EndIntro(); next.EndRehearsal(); next.EndPerformance();
                Set(run, "host", next); Set(run, "state", next.Snapshot);
                Assert.That(run.State.Timer, Is.EqualTo(RestTimerState.WaitingForExit));
                Call(flow, "LateUpdate");
                Assert.That(run.State.Timer, Is.Not.EqualTo(RestTimerState.WaitingForExit),
                    "같은 CampaignRun으로 새 방을 열어도 호스트 완료 신호를 다시 보내야 한다.");
            }
            finally
            {
                memoryOnly.SetValue(null, oldMemory); memoryEnabled.SetValue(null, oldEnabled);
                Singleton<WaitingRoomFirstDayFlow>().SetValue(null, oldFlow);
            }
        }
    }
}
