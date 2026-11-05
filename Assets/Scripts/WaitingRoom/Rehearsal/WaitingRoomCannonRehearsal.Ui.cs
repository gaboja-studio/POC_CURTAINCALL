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
        int endingRound = -1, returnedRound = -1, notifiedRound = -1;
        double returnAt;
        PlayerMover endingMover;
        bool restoreControl, restoreSimulation;

        void AwakeUi()
        {
            quest = new WaitingRoomRightPanel(transform, "CannonRehearsalQuestCanvas", 300f, 400f, "first-day.panel.") { GuideStyle = true };
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
                if (!StartPractice()) WaitingRoomGuidePanel.Show(T("title", "인간대포 리허설"), T("unavailable", "연습실을 준비하지 못했습니다. 잠시 뒤 스케줄표에서 다시 시도하세요."), 5f);
            }
            else FindFirstObjectByType<ScheduleBoardView>()?.SetOpen(true);
        }

        void TickUi()
        {
            promptBlock.SetOpen(prompt && !shared.active);
            if (prompt && !shared.active)
            {
                votePanel.Show(T("required-title", "인간대포 리허설"),
                    T("required", "인간대포를 처음 편성하기 전에 리허설을 이수해야 합니다. 지금 리허설을 진행할까요?"),
                    new RehearsalVotePanel.Choice(T("yes", "예"), true, () => AnswerSchedulePrompt(true)),
                    new RehearsalVotePanel.Choice(T("no", "아니오"), true, () => AnswerSchedulePrompt(false)));
                return;
            }
            placementBlock.SetOpen(shared.active && !shared.ending && local != null && !shared.placed.Contains(local.OwnerClientId));
            if (!shared.active || shared.ending || local == null) { votePanel.Hide(); return; }
            bool yes = shared.votes.Contains(local.OwnerClientId);
            bool skip = shared.skipVotes.Contains(local.OwnerClientId);
            string body = WaitingRoomText.Format("cannon-rehearsal.vote-body", "과반수 동의 시 넘기기는 이수 처리, 포기는 미이수로 종료합니다.\n넘기기 {skip}/{required} · 포기 {abandon}/{required}",
                ("skip", shared.skipVotes.Count), ("abandon", shared.votes.Count), ("required", shared.participants.Count / 2 + 1));
            votePanel.Show(T("vote-title", "리허설 넘기기 / 포기"), body,
                new RehearsalVotePanel.Choice(skip ? T("cancel-skip", "넘기기 취소") : T("skip", "리허설 넘기기"), true, () => VoteToSkip(!skip)),
                new RehearsalVotePanel.Choice(yes ? T("cancel-abandon", "포기 취소") : T("abandon", "리허설 포기"), true, () => VoteToAbandon(!yes)));
            if (course != null) course.DeviceUi.PracticeTooltipExclusion = votePanel.VisibleSurface;
        }

        void UpdateQuest()
        {
            ulong id = local.OwnerClientId;
            bool arrived = shared.arrived.Contains(id);
            if (arrived && walls == null) walls = course.CreateFinishWalls();
            if (walls != null) walls.SetActive(arrived);
            if (arrived) { quest.Hide(); return; }
            string text;
            var tramp = course.Trampoline;
            bool carrier = tramp.Carrier == id;
            if (!shared.middle.Contains(id)) text = T("first", "시작대의 대포에 F로 탑승하고, 준비가 끝나면 다시 F로 발사하세요. WASD로 중간 도착대에 착지하세요.");
            else if (carrier && course.Route.IsRiding(id)) text = T("ride", "운송 발판이 아래층에 도착할 때까지 기다리세요.");
            else if (carrier && local.transform.position.y > course.LowerFloor.bounds.max.y + 1f)
                text = T("carry", "한 명이 트램펄린 손잡이를 F로 잡고, 트램펄린을 든 채 혼자 운송 발판에 올라서세요. 조건이 맞으면 자동으로 아래층으로 이동합니다.");
            else if (carrier && shared.arrived.Count >= shared.participants.Count - 1)
                text = T("self", "동료가 모두 도착했습니다. 머리 위가 빈 곳에서 F를 길게 눌러 트램펄린을 놓고, 천 위로 점프해 반동을 받으세요. WASD로 최종 도착대에 착지하세요.");
            else if (carrier) text = T("catch", "동료의 낙하 지점으로 트램펄린을 옮겨 받아 주세요. 필요하면 반복해서 튕겨 올려 최종 도착대까지 보내세요.");
            else if (course.Cannon.IsFlying(id)) text = T("air", "WASD로 동료의 트램펄린 위에 내려오세요. 반동을 받은 뒤 WASD로 최종 도착대에 착지하세요.");
            else if (RehearsalCannonCourse.Contains(course.MiddleFloor, local.transform.position))
                text = T("second", "한 명은 트램펄린과 운송 발판을 담당하세요. 다른 사람은 중간 도착대의 대포에 F로 타고 다시 F로 발사해, 아래층 동료의 트램펄린으로 내려가세요.");
            else text = T("bounce", "트램펄린의 반동을 받아 WASD로 최종 도착대에 착지하세요. 놓인 트램펄린에는 천 위로 점프해 스스로 반동을 받을 수 있습니다.");
            quest.Show(T("quest-title", "[tutorial] 인간대포와 트램펄린"), text);
        }

        void BeginEnding(bool success, bool skipped = false)
        {
            if (shared.ending) return;
            shared.ending = true; shared.successful = success; shared.skipped = skipped; shared.mastered |= success; shared.endAt = Clock + 3d;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.State == PlayerState.Fallen && conditions.TryGetValue(player.OwnerClientId, out var condition))
                    player.ServerRestoreCondition(condition);
            Publish();
        }

        void TickEnding()
        {
            if (!shared.ending) { ReleaseEndingLock(); return; }
            if (endingRound != shared.round)
            {
                // ESC releases the pointer through the common menu. This return keeps the scene, so close it explicitly.
                CurtainCall.UI.SceneIntegration.CommonMenuUI.Current?.CloseAll();
                Cleanup(); placementBlock.SetOpen(false); quest.Hide(); votePanel.Hide();
                endingRound = shared.round; returnAt = Clock + 1d;
                CaptureEndingMover();
                endingBlock.SetOpen(true);
                fader.Play(() => returnedRound == shared.round && !shared.active);
            }
            if (returnedRound != shared.round && Clock >= returnAt && FindRoom())
            {
                var p = NetworkPlayer.Local;
                if (p != null && p.IsSpawned && p.TryGetComponent(out PlayerMover mover))
                {
                    CaptureEndingMover();
                    Vector3 point = entrance + Vector3.right * (Mathf.Max(0, p.Slot) - 1.5f) * .9f;
                    var rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(origin - entrance, Vector3.up));
                    mover.SetStartPose(point, rotation, true); mover.Simulated = restoreSimulation;
                    returnedRound = shared.round;
                }
            }
            if (Host && shared.active && Clock >= shared.endAt) { shared.active = false; Publish(); }
            if (!shared.active && returnedRound == shared.round)
            {
                ReleaseEndingLock();
                if (notifiedRound == shared.round) return;
                notifiedRound = shared.round;
                WaitingRoomGuidePanel.Show(T("end-title", "리허설 종료"), shared.skipped
                    ? T("skipped", "리허설을 넘겼습니다. 이수로 기록되어 스케줄표에서 인간대포를 확정할 수 있습니다. 연습실 기물의 F로 다시 연습할 수 있습니다.")
                    : shared.successful
                    ? T("complete", "인간대포 리허설을 이수했습니다. 이제 스케줄표에서 인간대포를 확정할 수 있습니다. 연습실 기물의 F로 다시 연습할 수 있습니다.")
                    : T("abandoned", "리허설을 포기했습니다. 이번 시도는 이수로 기록되지 않고, 다시 시작하면 처음부터 진행합니다."), 7f);
            }
        }

        void CaptureEndingMover()
        {
            var player = NetworkPlayer.Local;
            if (endingMover != null || player == null || !player.TryGetComponent(out PlayerMover mover)) return;
            endingMover = mover;
            // The reliable ending message may precede the revival NetworkVariable on a remote owner.
            restoreControl = player.State == PlayerState.Fallen || mover.ControlEnabled;
            restoreSimulation = player.State == PlayerState.Fallen || mover.Simulated;
            mover.SetControlEnabled(false); mover.Simulated = false;
        }

        void ReleaseEndingLock()
        {
            if (endingBlock != null) endingBlock.SetOpen(false);
            if (placementBlock != null && !shared.active) placementBlock.SetOpen(false);
            if (endingMover == null) return;
            endingMover.SetControlEnabled(restoreControl); endingMover.Simulated = restoreSimulation; endingMover = null;
        }
    }
}
