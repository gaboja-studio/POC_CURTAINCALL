using CurtainCall.UI.SceneIntegration;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    public sealed partial class WaitingRoomCannonRehearsal
    {
        WaitingRoomRightPanel quest;
        RehearsalVotePanel votePanel;
        RehearsalScreenFader fader;
        UIInputBlocker promptBlock, placementBlock, endingBlock;
        GameObject walls;
        bool prompt;
        bool pendingUnlockPrompt, returnToSchedule, discoveryPrompt;
        // 해금 직후가 아니라 미이수 상태로 맞은 쉬는 시간 시작 안내다(문구만 다르다).
        bool reminderPrompt;
        int unlockPromptGeneration;
        public static bool IsUnlockPromptVisible => instance != null && instance.prompt && instance.discoveryPrompt;

        void OnTrickDiscovered(string trickId, ulong actor)
        {
            if (!Host || trickId != CampaignSnapshot.HumanCannonId || HasMastered) return;
            pendingUnlockPrompt = true; reminderPrompt = false;
            unlockPromptGeneration = CampaignRun.Instance.State.Generation;
        }

        /// <summary>추격전 중에는 리허설로 이동할 수 없다. 스케줄표에서 인간대포를 고른 경우 이유만 알린다.</summary>
        static bool RefuseDuringPursuit()
        {
            if (!WaitingRoomRehearsal.IsPursuitRunning) return false;
            WaitingRoomGuidePanel.Show(T("title", "인간대포 리허설"),
                T("pursuit", "추격전 중에는 리허설을 진행할 수 없습니다. 다음 쉬는 시간에 다시 안내합니다."), 5f, true);
            return true;
        }

        void OfferUnlockedRehearsal()
        {
            if (!pendingUnlockPrompt || !Host) return;
            if (CampaignRun.Instance.State.Generation != unlockPromptGeneration || !CampaignRun.Instance.State.CannonUnlocked || HasMastered) { pendingUnlockPrompt = false; return; }
            if (!CanStart) return;
            pendingUnlockPrompt = false;
            returnToSchedule = false; discoveryPrompt = true; prompt = true;
            FindFirstObjectByType<ScheduleBoardView>()?.SetOpen(false);
        }
        int endingRound = -1, returnedRound = -1, notifiedRound = -1;
        double returnAt;
        PlayerMover endingMover;
        bool restoreControl, restoreSimulation;
        int returnPoseRound = -1;
        NetworkPlayer returnPoseOwner;
        Pose returnPose;

        void CaptureReturnPose(NetworkPlayer player)
        {
            if (ExpertService.IsTreating(player.OwnerClientId) || Pursuit.PursuitDirector.IsCaptured(player)) return;
            if (returnPoseRound == shared.round && returnPoseOwner == player) return;
            returnPose = new Pose(player.transform.position, player.transform.rotation);
            returnPoseOwner = player;
            returnPoseRound = shared.round;
        }

        void AwakeUi()
        {
            quest = new WaitingRoomRightPanel(transform, "CannonRehearsalQuestCanvas");
            votePanel = gameObject.AddComponent<RehearsalVotePanel>();
            fader = gameObject.AddComponent<RehearsalScreenFader>();
            promptBlock = gameObject.AddComponent<UIInputBlocker>(); promptBlock.BlockWhileActive = false;
            placementBlock = gameObject.AddComponent<UIInputBlocker>(); placementBlock.BlockWhileActive = false;
            endingBlock = gameObject.AddComponent<UIInputBlocker>(); endingBlock.BlockWhileActive = false;
        }

        /// <summary>Confirm is blocked before the book/campaign is changed. No returns to the unconfirmed planner.</summary>
        public static bool RequireBeforeConfirm(ScheduleBook book)
        {
            var r = instance;
            if (r == null || HasMastered || book == null) return false;
            bool required = book.IsFirstDaySecondEditable && book.Get(1, 1) == CampaignSnapshot.HumanCannonId;
            if (book.Night > 0)
                required |= ScheduleBook.DaysToApply(book.Night).Any(day => book.Setlist(day).Contains(CampaignSnapshot.HumanCannonId));
            if (!required) return false;
            if (RefuseDuringPursuit()) return true;
            FindFirstObjectByType<ScheduleBoardView>()?.SetOpen(false);
            r.prompt = true; r.reminderPrompt = false;
            return true;
        }

        /// <summary>확정 단계 없이 인간대포를 처음 선택하면 기존 리허설 안내를 연다.</summary>
        public static bool RequireBeforeSelection(string trickId)
        {
            var r = instance;
            if (trickId != CampaignSnapshot.HumanCannonId || r == null || HasMastered) return false;
            if (RefuseDuringPursuit()) return true;
            r.reminderPrompt = false;
            r.returnToSchedule = true; r.discoveryPrompt = false; r.pendingUnlockPrompt = false;
            FindFirstObjectByType<ScheduleBoardView>()?.SetOpen(false);
            r.prompt = true;
            return true;
        }

        public static void AnswerSchedulePrompt(bool yes)
        {
            var r = instance;
            if (r == null || !r.prompt) return;
            r.prompt = false; r.promptBlock.SetOpen(false); r.votePanel.Hide();
            if (yes)
            {
                if (!StartPractice()) WaitingRoomGuidePanel.Show(T("title", "인간대포 리허설"), ExpertService.IsMovingBuyer
                    ? "팀원의 신체 복구와 퇴실이 끝난 뒤 다시 시도하세요."
                    : T("unavailable", "연습실을 준비하지 못했습니다. 잠시 뒤 스케줄표에서 다시 시도하세요."), 5f,true);
            }
            else if (r.returnToSchedule) FindFirstObjectByType<ScheduleBoardView>()?.SetOpen(true);
        }

        void TickUi()
        {
            // Another owner's station request can start practice while the host's prompt is open.
            if (shared.active) prompt = false;
            // 추격전이 시작되면 리허설 이동 안내를 닫는다. 미이수면 다음 쉬는 시간 시작에 다시 띄운다.
            if (prompt && WaitingRoomRehearsal.IsPursuitRunning) { prompt = false; votePanel.Hide(); }
            promptBlock.SetOpen(prompt && !shared.active);
            if (prompt && !shared.active)
            {
                string promptTitle = !discoveryPrompt ? T("required-title", "인간대포 리허설")
                    : reminderPrompt ? T("reminder-title", "인간대포 리허설 미이수") : T("unlocked-title", "새 공연 해금 · 인간대포 리허설");
                string promptBody = !discoveryPrompt ? T("required", "인간대포를 처음 편성하기 전에 리허설을 이수해야 합니다. 지금 리허설을 진행할까요?")
                    : reminderPrompt ? T("reminder-body", "아직 인간대포 리허설을 이수하지 않았습니다. 지금 리허설로 이동할까요?")
                    : T("unlocked-body", "인간대포 공연이 해금되었습니다. 지금 리허설로 이동할까요?");
                votePanel.Show(promptTitle, promptBody,
                    new HudVoteChoice(T("yes", "예"), true, () => AnswerSchedulePrompt(true)),
                    new HudVoteChoice(T("no", "아니오"), true, () => AnswerSchedulePrompt(false)));
                return;
            }
            placementBlock.SetOpen(shared.active && !shared.ending && local != null && !shared.placed.Contains(local.OwnerClientId));
            if (!shared.active || shared.ending || local == null) { votePanel.Hide(); return; }
            bool yes = shared.votes.Contains(local.OwnerClientId);
            bool skip = shared.skipVotes.Contains(local.OwnerClientId);
            string body = WaitingRoomText.Format("cannon-rehearsal.vote-body", "과반수 동의 시 넘기기는 이수 처리, 포기는 미이수로 종료합니다.\n넘기기 {skip}/{required} · 포기 {abandon}/{required}",
                ("skip", shared.skipVotes.Count), ("abandon", shared.votes.Count), ("required", shared.participants.Count / 2 + 1));
            votePanel.Show(T("vote-title", "리허설 넘기기 / 포기"), body,
                new HudVoteChoice(skip ? T("cancel-skip", "넘기기 취소") : T("skip", "리허설 넘기기"), true, () => VoteToSkip(!skip)),
                new HudVoteChoice(yes ? T("cancel-abandon", "포기 취소") : T("abandon", "리허설 포기"), true, () => VoteToAbandon(!yes)));
            if (course != null) course.DeviceUi.PracticeTooltipExclusion = votePanel.VisibleSurface;
        }

        void UpdateQuest()
        {
            ulong id = local.OwnerClientId;
            bool arrived = shared.arrived.Contains(id);
            if (arrived && walls == null) walls = course.CreateFinishWalls();
            if (walls != null) walls.SetActive(arrived);
            if (arrived) { quest.Hide(); return; }
            if (!shared.middle.Contains(id))
                quest.Show(T("first-title", "공연 준비"), T("first", "F를 눌러 인간대포에 탑승한 뒤 건너편으로 건너가세요."));
            else if (ReachedLowerArea())
                quest.Show(T("safe-title", "안전 지역으로 이동하기"), T("safe-body", "도착 지점에서는 추락 패널티 없이 안전하게 착지할 수 있습니다. 트램펄린을 이용해 안전 지역으로 이동하세요."));
            else
                quest.Show(T("quest-title", "인간대포와 트램펄린"),
                    T("second", "인간대포로 발사하고, 트램펄린을 이용해 안전하게 착지합니다. 다음 지점까지 이동하세요.\n*1명의 공연 준비자가 트램펄린을 이용해야 합니다."));
        }

        readonly System.Collections.Generic.HashSet<string> actionGuides = new();
        readonly System.Collections.Generic.Queue<(string title, string body)> pendingGuides = new();
        int guideRound = -1;
        bool ReachedLowerArea()
        {
            Vector3 feet = local.transform.position;
            var bounds = course.LowerFloor.bounds;
            return shared.middle.Contains(local.OwnerClientId) && feet.y <= bounds.max.y + 1f
                && feet.x >= bounds.min.x && feet.x <= bounds.max.x && feet.z >= bounds.min.z && feet.z <= bounds.max.z;
        }

        void QueueGuide(string key, string title, string body)
        {
            if (actionGuides.Add(key)) pendingGuides.Enqueue((T("guide."+key+".title",title), T("guide."+key+".body",body)));
        }

        void TickActionGuides()
        {
            if (guideRound != shared.round) { guideRound = shared.round; actionGuides.Clear(); pendingGuides.Clear(); }
            ulong id = local.OwnerClientId;
            bool carrier = course.Trampoline.Carrier == id;
            if (course.Cannon.FindBoardableStation(local) >= 0 || course.Cannon.IsSeated(id))
                QueueGuide("cannon", "인간대포 발사하기", "인간대포 앞에서 {interact}를 눌러 탑승합니다. 다시 {interact}를 누르면 대포가 발사됩니다. 발사 후에는 트램펄린에 착지해야 합니다.");
            if (shared.middle.Contains(id) && Vector3.Distance(local.transform.position, course.Trampoline.transform.position) < 4f)
                QueueGuide("trampoline", "트램펄린 이용하기", "트램펄린 앞에서 {interact}를 눌러 트램펄린을 잡습니다. 트램펄린 이용자는 대포로 발사되는 다른 공연 준비자를 안전하게 받아줘야 합니다.");
            if (carrier)
                QueueGuide("carry", "트램펄린 이용하기", "트램펄린을 쥔 상태에서 자유롭게 이동이 가능하며, 다시 {interact}를 길게 누르면 트램펄린을 놓습니다.");
            if (course.Route.IsRiding(id))
            {
                if (carrier) QueueGuide("catch", "받아주기", "트램펄린을 이용해 다른 공연 준비자를 받아주며 다음 장소까지 안전하게 이동합니다.");
                // 원문의 최신 dev 원본 우선 규칙: 현재 공연은 과승 낙하 대신 출발을 거부한다.
                QueueGuide("transport", "트램 이용하기", "트램은 자동으로 이동합니다. 한 명만 이용할 수 있으며, 두 명 이상 탑승하면 출발하지 않습니다.");
            }
            if (shared.bounced.Contains(id)) QueueGuide("bounce", "안전한 착지", "트램펄린을 이용해 안전하게 착지할 수 있습니다. 지면에 착지하지 않게 주의하세요.");
            if (ReachedLowerArea()) QueueGuide("safe", "안전지역", "모든 공연 준비자가 도착 지점에 도착해야 합니다.");
            if (pendingGuides.Count == 0 || WaitingRoomTutorialPanel.IsVisible) return;
            var next = pendingGuides.Peek();
            if (RehearsalGuide.Show(next.title, next.body, 10f,next.body.Contains("{interact}")?"{interact}":null)) pendingGuides.Dequeue();
        }

        void BeginEnding(bool success, bool skipped = false)
        {
            if (shared.ending) return;
            // Release every participant, not only players who died before the vote completed.
            foreach (ulong id in shared.participants) ReleasePlayerDevices(id);
            shared.ending = true; shared.successful = success; shared.skipped = skipped; shared.mastered |= success; shared.endAt = Clock + 3d;
            RecordAction("ENDING", reason: skipped ? "MAJORITY_SKIP" : success ? "ALL_ARRIVED" : "MAJORITY_ABANDON");
            Publish();
        }

        void TickEnding()
        {
            if (!shared.ending) { ReleaseEndingLock(); return; }
            // A vote can finish before the ordinary checkpoint revival timer expires.
            RecoverEndingPlayers();
            if (endingRound != shared.round)
            {
                // ESC releases the pointer through the common menu. This return keeps the scene, so close it explicitly.
                CurtainCall.UI.SceneIntegration.CommonMenuUI.Current?.CloseAll();
                endingBlock.SetOpen(true);
                // The ending snapshot and the stack unlink can arrive in either order.
                if (!CaptureEndingMover()) return;
                Cleanup(); placementBlock.SetOpen(false); quest.Hide(); votePanel.Hide();
                endingRound = shared.round; returnAt = Clock + 1d;
                endingBlock.SetOpen(true);
                fader.Play(() => returnedRound == shared.round && !shared.active);
            }
            if (returnedRound != shared.round && Clock >= returnAt && FindRoom())
            {
                var p = NetworkPlayer.Local;
                if (p != null && p.IsSpawned && p.TryGetComponent(out PlayerMover mover))
                {
                    CaptureEndingMover();
                    bool normal = p.State == PlayerState.Normal;
                    bool bodyEnabled = mover.Body != null && mover.Body.enabled;
                    mover.SetStartPose(returnPose.position, returnPose.rotation, true);
                    if (!normal && mover.Body != null) mover.Body.enabled = bodyEnabled;
                    if (!normal) mover.SetControlEnabled(false);
                    if (normal) CurtainCall.Cameras.CameraViewDirector.Current?.SetLookRotation(returnPose.rotation);
                    // 종료 상태를 받을 때까지 복귀 위치에서 서로 밀리지 않게 고정한다.
                    mover.Simulated = false;
                    returnedRound = shared.round;
                }
            }
            if (Host && shared.active && Clock >= shared.endAt)
            {
                shared.active = false;
                PropCarry.Instance?.RestoreRehearsalSelection();
                Publish();
            }
            if (!shared.active && returnedRound == shared.round)
            {
                ReleaseEndingLock();
                if (notifiedRound == shared.round) return;
                notifiedRound = shared.round;
                WaitingRoomGuidePanel.Show(T("end-title", "리허설 종료"), shared.skipped
                    ? T("skipped", "리허설을 넘겼습니다. 이수로 기록되어 인간대포 공연을 선택할 수 있습니다. 연습실 기물의 F로 다시 연습할 수 있습니다.")
                    : shared.successful
                    ? T("complete", "인간대포 리허설을 이수했습니다. 이제 인간대포 공연을 선택할 수 있습니다. 연습실 기물의 F로 다시 연습할 수 있습니다.")
                    : T("abandoned", "리허설을 포기했습니다. 이번 시도는 이수로 기록되지 않고, 다시 시작하면 처음부터 진행합니다."), 10f);
                if(shared.skipped)
                    RehearsalGuide.Show(T("retry-title", "다시 연습하기"), T("retry-body", "연습실 기물 앞에서 {interact}로 다시 연습할 수 있습니다."),6,"{interact}");
                if (shared.successful)
                    quest.Show(T("completed-title", "리허설 종료"), T("complete", "인간대포 리허설을 완료했습니다. 이제 인간대포 공연을 진행할 수 있습니다."));
            }
        }

        bool CaptureEndingMover()
        {
            var player = NetworkPlayer.Local;
            if (ExpertService.IsLocalInTreatment) return false;
            if (endingMover != null) return true;
            if (player == null || !player.TryGetComponent(out PlayerMover mover)) return false;
            var stack = CurtainCall.Tightrope.PiggybackSystem.Current;
            if (stack != null && (stack.GetBelow(player) != null || stack.GetAbove(player) != null)) return false;
            CaptureReturnPose(player);
            // Seating/flight/transport temporarily own Simulated. Return their saved value
            // before taking the ending lock, otherwise Detach unlocks early and we restore false.
            if (player.TryGetComponent(out HumanCannonPilot pilot)) pilot.ReleaseMovement();
            stack?.GetComponent<CurtainCall.Tightrope.PiggybackFollow>()?.ReleaseForDevice(player);
            if (player.TryGetComponent(out HumanoidPlayerPresentation presentation)) presentation.CancelItemAction();
            endingMover = mover;
            restoreControl = player.State == PlayerState.Fallen || mover.ControlEnabled;
            restoreSimulation = mover.Simulated;
            mover.SetControlEnabled(false); mover.Simulated = false;
            return true;
        }

        void ReleaseEndingLock()
        {
            if (endingBlock != null) endingBlock.SetOpen(false);
            if (placementBlock != null && !shared.active) placementBlock.SetOpen(false);
            if (endingMover == null) return;
            bool normal = endingMover.TryGetComponent(out NetworkPlayer player) && player.State == PlayerState.Normal;
            endingMover.SetControlEnabled(normal && restoreControl); endingMover.Simulated = restoreSimulation; endingMover = null;
        }
    }
}
