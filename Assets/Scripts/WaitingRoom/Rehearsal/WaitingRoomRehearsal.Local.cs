using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 이 화면의 캐릭터 처리: 출발대 배치·추락 복귀·우측 퀘스트·좌측 설명·완료 뒤 무대 입구 길안내·포기 투표 입력(기획서 4·15·16장).
    /// 퀘스트 문구는 <c>WaitingRoomTexts.txt</c>의 <c>rehearsal.*</c> 키로 고친다.
    /// </summary>
    public sealed partial class WaitingRoomRehearsal
    {
        /// <summary>우측 퀘스트 패널 위치(1920×1080 UI 단위). 밤 할 일 패널과 같은 자리를 쓴다(리허설과 밤은 겹치지 않는다).</summary>
        const float QuestTop = 300f, QuestWidth = 400f;
        /// <summary>연습실 통로 철창에서 이 거리(m) 안이면 포기 입력을 받는다.</summary>
        const float GiveUpReach = 4f;
        const float IntroSeconds = 10f;

        WaitingRoomRightPanel quest;
        RehearsalPathGuide guide;
        NetworkPlayer local;
        int placedRound = -1;
        bool placedAbandoned;
        string lastQuest = "";
        bool localVoted, promptedGiveUp;
        StageEntranceGather gather;
        Transform passage;

        void AwakeLocal()
        {
            quest = new WaitingRoomRightPanel(transform, "RehearsalQuestCanvas", QuestTop, QuestWidth);
            guide = gameObject.AddComponent<RehearsalPathGuide>();
        }

        void ActivateLocal()
        {
            placedRound = -1; placedAbandoned = false; localVoted = false; promptedGiveUp = false; lastQuest = "";
            gather = null; passage = null;
            WaitingRoomTutorialPanel.Show(
                WaitingRoomText.Get("rehearsal.intro-title", "최초 리허설"),
                WaitingRoomText.Get("rehearsal.intro", "공연 전에 연습실에서 외줄 조작과 협동을 익힙니다. 추락해도 대가 없이 그 구간 출발대에서 다시 시작합니다. 리허설 동안 탐색 시계는 흐르지 않습니다."),
                IntroSeconds);
        }

        void DeactivateLocal()
        {
            Bind(null);
            quest.Hide();
            guide.SetTarget(null);
            WaitingRoomTutorialPanel.Hide();
        }

        void Bind(NetworkPlayer player)
        {
            if (local == player) return;
            if (local != null) local.StateChanged -= OnLocalStateChanged;
            local = player;
            if (local != null) local.StateChanged += OnLocalStateChanged;
        }

        /// <summary>추락 복구(호스트가 보통 상태로 되돌림) → 그 소구간 출발대로. 재시도 퀘스트는 LocalTick이 다시 띄운다.</summary>
        void OnLocalStateChanged(PlayerState previous, PlayerState next)
        {
            if (!active || previous != PlayerState.Fallen || next != PlayerState.Normal || course == null) return;
            PlaceLocalAtStart();
            WaitingRoomTutorialPanel.Show(WaitingRoomText.Get("rehearsal.fall-title", "추락"),
                WaitingRoomText.Get("rehearsal.fall", "대가는 없습니다. 이 구간 출발대에서 다시 시작합니다."), 4f);
        }

        /// <summary>소구간·회차가 바뀌었다(코스가 새로 만들어졌거나 사라짐).</summary>
        void OnCourseChanged()
        {
            placedRound = -1; lastQuest = "";
            var s = Stage;
            if (course != null && s != null)
                WaitingRoomTutorialPanel.Show(s.Title, s.Intro, IntroSeconds);
            else if (active && completed)
                WaitingRoomTutorialPanel.Show(WaitingRoomText.Get("rehearsal.complete-title", "리허설 완료"),
                    WaitingRoomText.Get("rehearsal.complete", "바닥의 점선을 따라 무대 입구 통로로 가세요. 모든 공연 준비자가 모이면 첫 공연이 시작됩니다."), IntroSeconds);
            else if (active && abandoned)
                WaitingRoomTutorialPanel.Show(WaitingRoomText.Get("rehearsal.abandoned-title", "리허설 포기"),
                    WaitingRoomText.Get("rehearsal.abandoned", "연습실 밖으로 나왔습니다. 공연에 가려면 리허설을 마쳐야 합니다. 연습실 링으로 다시 들어가면 마친 구간 다음부터 이어서 합니다."), IntroSeconds);
        }

        void LocalTick()
        {
            Bind(NetworkPlayer.Local);
            if (local == null || !local.IsSpawned || !local.TryGetComponent(out PlayerMover mover)) { quest.Hide(); return; }
            PlaceLocal(mover);
            if (completed) { TickCompleted(); return; }
            guide.SetTarget(null);
            if (abandoned) { quest.Hide(); return; }
            TickGiveUpInput();
            UpdateQuest(mover);
        }

        // ── 배치 ──

        void PlaceLocal(PlayerMover mover)
        {
            if (placedRound == round && placedAbandoned == abandoned) return;
            if (local.State != PlayerState.Normal) return;
            if (abandoned)
            {
                // 연습실 밖(로비 출발 위치)으로.
                PlayerSpawnPoints.GetPose(Mathf.Max(0, local.Slot), mover.LaneSpacing, out var position, out var rotation);
                Relocate(mover, position, rotation);
            }
            else if (course != null) PlaceLocalAtStart();
            else if (!completed) return;
            placedRound = round; placedAbandoned = abandoned;
        }

        void PlaceLocalAtStart()
        {
            if (course == null || local == null || !local.TryGetComponent(out PlayerMover mover)) return;
            var s = Stage;
            int slot = Mathf.Max(0, local.Slot);
            Pose pose;
            // 1단계는 모두 왼쪽 줄(중간에서 끝나는 줄)에서 출발해야 옆줄 건너가기를 배운다. 자리 순서대로 뒤로 줄 선다.
            if (s != null && s.Kind == RehearsalStageKind.LaneChange)
                pose = new Pose(course.GetLanePosition(0, course.Shape.SpawnDistance - slot * .9f), Quaternion.LookRotation(course.Forward, Vector3.up));
            else pose = course.GetStartPose(slot);
            Relocate(mover, pose.position, pose.rotation);
        }

        void Relocate(PlayerMover mover, Vector3 position, Quaternion rotation)
        {
            if (local.TryGetComponent(out PlayerBalance balance)) { balance.ResetBalance(); balance.SetBalanceActive(false); }
            mover.SetStartPose(position, rotation, true);
            mover.SetControlEnabled(true);
            var view = CameraViewDirector.Current;
            if (view != null && view.LocalPlayer == local) view.SetLookRotation(rotation);
        }

        // ── 퀘스트(개인별, 기획서 4장) ──

        void UpdateQuest(PlayerMover mover)
        {
            var s = Stage;
            if (s == null || course == null) { quest.Hide(); return; }
            string body = QuestFor(s, mover);
            if (giveUpVotes > 0)
                body += "\n\n" + (giveUpDeadline >= 0d
                    ? WaitingRoomText.Format("rehearsal.giveup-countdown", "포기 투표 {votes}/{total} · {seconds}초 뒤 종료", ("votes", giveUpVotes), ("total", giveUpRequired), ("seconds", Mathf.CeilToInt(Mathf.Max(0f, (float)(giveUpDeadline - Clock)))))
                    : WaitingRoomText.Format("rehearsal.giveup-votes", "포기 투표 {votes}/{total}", ("votes", giveUpVotes), ("total", giveUpRequired)));
            if (body == lastQuest && quest.Visible) return;
            if (quest.Show(s.Title, body)) lastQuest = body;
        }

        string QuestFor(RehearsalStage s, PlayerMover mover)
        {
            if (local.State == PlayerState.Fallen) return WaitingRoomText.Get("rehearsal.quest.fallen", "추락했습니다. 잠시 뒤 출발대에서 다시 시작합니다.");
            var p = local.transform.position;
            float distance = course.GetDistance(p);
            if (IsFinished(local))
                return s.Rule == RehearsalClearRule.Everyone
                    ? WaitingRoomText.Get("rehearsal.quest.wait-team", "도착! 동료가 모두 도착할 때까지 기다리세요.")
                    : WaitingRoomText.Get("rehearsal.quest.arrived", "도착! 곧 다음 단계로 넘어갑니다.");
            bool onRope = course.TryGetSupportingRope(mover, out int lane);
            switch (s.Kind)
            {
                case RehearsalStageKind.LaneChange:
                    if (onRope && lane == 0 && distance >= s.CheckpointDistance)
                        return WaitingRoomText.Get("rehearsal.quest.lane-jump", "Q, E + Space를 이용해 건너편 줄로 넘어가세요.");
                    if (onRope && lane >= 1 || distance >= course.Layout.GetLaneEnd(0))
                        return WaitingRoomText.Get("rehearsal.quest.finish", "줄을 따라 도착대까지 가세요.");
                    return WaitingRoomText.Get("rehearsal.quest.balance", "W로 나아가고 A, D로 균형을 잡으며 외줄을 건너세요.");
                case RehearsalStageKind.Swing:
                    var pole = TightropeSwingPole.Current;
                    if (pole != null && pole.IsOccupied)
                        return WaitingRoomText.Get("rehearsal.quest.swing-release", "Space를 누르면 줄을 놓습니다. W, S로 그네를 조작해 건너편 줄로 넘어가세요.");
                    if (distance >= course.SwingGapTo) return WaitingRoomText.Get("rehearsal.quest.finish", "줄을 따라 도착대까지 가세요.");
                    if (distance >= course.SwingGapFrom - 5f) return WaitingRoomText.Get("rehearsal.quest.swing-approach", "인간 그네에 탑승하세요. (줄 끝에서 F)");
                    return WaitingRoomText.Get("rehearsal.quest.balance", "W로 나아가고 A, D로 균형을 잡으며 외줄을 건너세요.");
                case RehearsalStageKind.Relay:
                    var gate = TightropeRelayGate.Current;
                    if (gate != null && gate.IsHeld(local))
                        return WaitingRoomText.Get("rehearsal.quest.relay-hold", "잡고 있는 동안 동료의 줄이 이어집니다. 동료가 건너갈 때까지 잡고 있다가 F나 Space로 놓으세요.");
                    if (distance >= course.SwingGapTo) return WaitingRoomText.Get("rehearsal.quest.relay-far", "도착대로 가거나, 건너편 트리거 그네를 F로 잡아 남은 동료의 줄을 이어 주세요.");
                    if (gate != null && gate.OutboundOpen && onRope && gate.IsLaneConnected(lane))
                        return WaitingRoomText.Get("rehearsal.quest.relay-cross", "줄이 이어졌습니다. 끊긴 구간을 지나 건너편으로 가세요.");
                    if (gate != null && gate.OutboundOpen)
                        return WaitingRoomText.Get("rehearsal.quest.relay-move", "이어진 줄(실선)로 Q/E + Space로 옮겨 건너세요.");
                    if (distance >= course.SwingGapFrom - 6f)
                        return WaitingRoomText.Get("rehearsal.quest.relay-grab", "바깥 줄 끝의 트리거 그네를 F로 잡으면 동료의 줄이 이어집니다. 한 명은 잡고, 나머지는 건너세요.");
                    return WaitingRoomText.Get("rehearsal.quest.balance", "W로 나아가고 A, D로 균형을 잡으며 외줄을 건너세요.");
            }
            return "";
        }

        // ── 완료 뒤 집결 길안내(기획서 15.3) ──

        void TickCompleted()
        {
            if (gather == null) gather = FindFirstObjectByType<StageEntranceGather>();
            if (gather == null) { guide.SetTarget(null); quest.Hide(); return; }
            bool inside = gather.IsInside(local);
            guide.SetTarget(inside ? null : gather.transform.position);
            string body = inside
                ? WaitingRoomText.Format("rehearsal.quest.gathered", "무대 입구 통로에 모였습니다 {inside}/{alive}. 동료를 기다리세요.", ("inside", gather.InsideCount), ("alive", gather.AliveCount))
                : WaitingRoomText.Format("rehearsal.quest.gather", "점선을 따라 무대 입구 통로로 가세요. 전원 {inside}/{alive}이 모이면 공연이 시작됩니다.", ("inside", gather.InsideCount), ("alive", gather.AliveCount));
            if (body == lastQuest && quest.Visible) return;
            if (quest.Show(WaitingRoomText.Get("rehearsal.gather-title", "리허설 완료 · 무대로"), body)) lastQuest = body;
        }

        // ── 포기 투표 입력(기획서 16.1: 출구로 향하면 패널, 전원 즉시·과반수 30초) ──

        void TickGiveUpInput()
        {
            if (passage == null)
            {
                foreach (var lockGate in FindObjectsByType<NightDoorLock>(FindObjectsSortMode.None))
                    if (lockGate.name == NightDoorLock.RehearsalPassageName) { passage = lockGate.transform; break; }
            }
            if (passage == null) return;
            bool near = (local.transform.position - passage.position).sqrMagnitude <= GiveUpReach * GiveUpReach;
            if (!near) { promptedGiveUp = false; return; }
            if (!promptedGiveUp)
            {
                promptedGiveUp = true;
                WaitingRoomTutorialPanel.Show(WaitingRoomText.Get("rehearsal.giveup-title", "리허설 포기"),
                    WaitingRoomText.Get("rehearsal.giveup", "통로에서 F를 길게 누르면 포기에 표를 던집니다(다시 누르면 철회). 전원이 포기하면 바로, 과반수가 포기하면 30초 뒤 연습실 밖으로 나갑니다."), 8f);
            }
            if (InputGate.IsBlocked || !local.TryGetComponent(out PlayerInputReader reader) || !reader.Current.InteractLongPressed) return;
            localVoted = !localVoted;
            Send(Operation.GiveUp);
            WaitingRoomTutorialPanel.Show(WaitingRoomText.Get("rehearsal.giveup-title", "리허설 포기"),
                localVoted ? WaitingRoomText.Get("rehearsal.giveup-voted", "포기에 표를 던졌습니다. 다시 F를 길게 누르면 철회합니다.")
                    : WaitingRoomText.Get("rehearsal.giveup-withdrawn", "포기 표를 철회했습니다."), 4f);
        }
    }
}
