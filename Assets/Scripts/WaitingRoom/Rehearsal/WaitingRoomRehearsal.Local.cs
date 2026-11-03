using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 이 화면의 캐릭터 처리: 출발대 배치·추락 복귀·우측 퀘스트·좌측 설명·완료 뒤 무대 입구 길안내(기획서 4·15장).
    /// 퀘스트 문구는 <c>WaitingRoomTexts.txt</c>의 <c>rehearsal.*</c> 키로 고친다.
    /// </summary>
    public sealed partial class WaitingRoomRehearsal
    {
        /// <summary>우측 퀘스트 패널 위치(1920×1080 UI 단위). 밤 할 일 패널과 같은 자리를 쓴다(리허설과 밤은 겹치지 않는다).</summary>
        const float QuestTop = 300f, QuestWidth = 400f;
        const float IntroSeconds = 10f;

        WaitingRoomRightPanel quest;
        RehearsalPathGuide guide;
        NetworkPlayer local;
        int placedRound = -1;
        float introductionUntil, actionGuideAfter;
        bool pendingStageIntro, laneHintShown;
        readonly System.Collections.Generic.HashSet<ulong> mountHints = new();
        Pose? checkpoint, arrivalPose;
        float checkpointDistance = float.NegativeInfinity;
        string lastQuest = "";
        StageEntranceGather gather;
        CameraCursorExclusion skipCursorRegion;
        Rect SkipButtonRect => new Rect(Mathf.Max(12f, Screen.width - 292f), Screen.height - 106f, 280f, 38f);

        void AwakeLocal()
        {
            quest = new WaitingRoomRightPanel(transform, "RehearsalQuestCanvas", QuestTop, QuestWidth, "first-day.panel.") { GuideStyle = true };
            guide = gameObject.AddComponent<RehearsalPathGuide>();
            skipCursorRegion = gameObject.AddComponent<CameraCursorExclusion>();
            skipCursorRegion.enabled = false;
        }

        // 실제 공연과 동일한 HUD 프리팹·균형 표시 바인딩을 사용한다. 입력과 균형 판정을 새로 만들지 않는다.
        void BuildPerformanceUi()
        {
            var hudPrefab = Resources.Load<GameObject>("Prefabs/UIs/PerformanceHud/PerformanceHud");
            var cheerPrefab = Resources.Load<GameObject>("Prefabs/UIs/CheerGauge/C1CheerGauge");
            if (hudPrefab == null || cheerPrefab == null)
            { Debug.LogError("[Rehearsal] 기존 공연 HUD 프리팹이 없습니다."); return; }
            var hudRoot = Instantiate(hudPrefab, courseRoot.transform, false);
            hudRoot.name = "RehearsalPerformanceHud";
            var cheerRoot = Instantiate(cheerPrefab, hudRoot.transform, false);
            var stock = cheerRoot.GetComponent<CurtainCall.CheerGauge.C1CheerGaugeBinding>();
            if (stock != null) stock.enabled = false;
            var binding = hudRoot.AddComponent<CurtainCall.UI.SceneIntegration.PerformanceSceneBinding>();
            binding.Configure(hudRoot.GetComponent<CurtainCall.UI.PerformanceHudV1.PerformanceHUDView>(),
                cheerRoot.GetComponent<CurtainCall.CheerGauge.C1CheerGaugeView>(), null);
        }

        void DestroyLocal()
        {
        }

        void ActivateLocal()
        {
            placedRound = -1; lastQuest = "";
            introductionUntil = Time.unscaledTime + IntroSeconds;
            gather = null;
            WaitingRoomTutorialPanel.Show(
                WaitingRoomText.Get("rehearsal.intro-title", "최초 리허설"),
                WaitingRoomText.Get("rehearsal.intro", "공연 전에 연습실에서 외줄 조작과 협동을 익힙니다. 추락해도 대가 없이 그 구간 출발대에서 다시 시작합니다. 리허설 동안 탐색 시계는 흐르지 않습니다."),
                IntroSeconds);
        }

        void DeactivateLocal()
        {
            skipCursorRegion.enabled = false;
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
            if (!active || previous != PlayerState.Fallen || next != PlayerState.Normal) return;
            if (completed) { placedRound = -1; return; }
            if (course == null) return;
            if (checkpoint.HasValue) Relocate(local.GetComponent<PlayerMover>(), checkpoint.Value.position, checkpoint.Value.rotation);
            else PlaceLocalAtStart();
            laneHintShown = false; mountHints.Clear(); actionGuideAfter = Time.unscaledTime + 4f;
            WaitingRoomTutorialPanel.Show(WaitingRoomText.Get("rehearsal.fall-title", "추락"),
                WaitingRoomText.Get("rehearsal.fall", "대가는 없습니다. 이 구간 출발대에서 다시 시작합니다."), 4f);
        }

        /// <summary>소구간·회차가 바뀌었다(코스가 새로 만들어졌거나 사라짐).</summary>
        void OnCourseChanged()
        {
            placedRound = -1; lastQuest = "";
            checkpoint = null; arrivalPose = null; checkpointDistance = float.NegativeInfinity;
            laneHintShown = false; mountHints.Clear(); pendingStageIntro = false; actionGuideAfter = 0f;
            var s = Stage;
            if (course != null && s != null)
            {
                if (Time.unscaledTime < introductionUntil) pendingStageIntro = true;
                else WaitingRoomTutorialPanel.Show(s.DisplayTitle, s.DisplayIntro, IntroSeconds);
            }
            else if (active && completed)
                WaitingRoomTutorialPanel.Show(WaitingRoomText.Get("rehearsal.complete-title", "리허설 완료"),
                    WaitingRoomText.Get("rehearsal.complete", "바닥의 점선을 따라 무대 입구 통로로 가세요. 모든 공연 준비자가 모이면 첫 공연이 시작됩니다."), IntroSeconds);
        }

        void OnGUI()
        {
            if (CanVote)
            {
                bool selected = votes.Contains(local.OwnerClientId);
                var rect = new Rect(SkipButtonRect.x, SkipButtonRect.y - 70f, 280f, 34f);
                bool value = GUI.Toggle(rect, selected, WaitingRoomText.Format("rehearsal.vote", "다음 구간으로 ({votes}/{required})", ("votes", votes.Count), ("required", participantCount / 2 + 1)));
                if (value != selected) RequestVote(value);
            }
            if (!CanClearCurrentStage) return;
            if (GUI.Button(SkipButtonRect, WaitingRoomText.Get("rehearsal.clear-button", "현재 구간 즉시 클리어 · 다음")))
                TryClearCurrentStage();
            GUI.Label(new Rect(SkipButtonRect.x, SkipButtonRect.y - 22f, 280f, 22f),
                WaitingRoomText.Get("rehearsal.clear-button-hint", "호스트 전용 · Esc로 커서 해제"));
        }

        void LocalTick()
        {
            skipCursorRegion.enabled = CanClearCurrentStage || CanVote;
            skipCursorRegion.SetScreenRect(new Rect(SkipButtonRect.x, SkipButtonRect.y - 70f, SkipButtonRect.width, SkipButtonRect.height + 70f));
            Bind(NetworkPlayer.Local);
            if (local == null || !local.IsSpawned || !local.TryGetComponent(out PlayerMover mover)) { quest.Hide(); return; }
            PlaceLocal(mover);
            if (completed) { TickCompleted(); return; }
            guide.SetTarget(null);
            UpdateActionGuides();
            TrackPersonalProgress(mover);
            UpdateQuest(mover);
        }

        void UpdateActionGuides()
        {
            if (Stage == null || local.State != PlayerState.Normal || arrived.Contains(local.OwnerClientId)
                || Time.unscaledTime < actionGuideAfter) return;
            if (pendingStageIntro)
            {
                if (Time.unscaledTime < introductionUntil) return;
                pendingStageIntro = false;
                WaitingRoomTutorialPanel.Show(Stage.DisplayTitle, Stage.DisplayIntro, IntroSeconds);
                return;
            }
            if (!laneHintShown && Stage.Kind == RehearsalStageKind.LaneChange
                && course.GetDistance(local.transform.position) >= Stage.CheckpointDistance
                && course.Layout.GetLaneSlotAt(course.GetSide(local.transform.position)) == 0)
            {
                laneHintShown = true;
                WaitingRoomTutorialPanel.Show(WaitingRoomText.Get("rehearsal.lane-guide-title", "1단계 · 외줄타기 체크포인트 도달"),
                    WaitingRoomText.Get("rehearsal.lane-guide", "줄이 끊긴 지점에서는 Q/E + Space키로 옆줄로 건너갈 수 있습니다."), IntroSeconds);
            }
            var target = PiggybackSystem.Current != null ? PiggybackSystem.Current.FindMountTarget(local) : null;
            if (target != null && mountHints.Add(target.OwnerClientId))
                WaitingRoomTutorialPanel.Show(WaitingRoomText.Get("rehearsal.mount-title", "목마 상호작용"),
                    WaitingRoomText.Get("rehearsal.mount", "동료 가까이에서 F키를 눌러 목마에 올라탈 수 있습니다."), IntroSeconds);
        }

        void TrackPersonalProgress(PlayerMover mover)
        {
            if (course == null || Stage == null || local.State != PlayerState.Normal) return;
            if (arrived.Contains(local.OwnerClientId))
            {
                if (!arrivalPose.HasValue)
                    arrivalPose = new Pose(course.GetLanePosition(course.Layout.GetLaneSlotAt(course.GetSide(local.transform.position)), course.FinishDistance), Quaternion.LookRotation(course.Forward));
                if (!IsFinished(local) || Mathf.Abs(course.GetSide(local.transform.position)) > (course.LaneCount - 1) * course.Shape.LaneSpacing * .5f + course.Shape.SideMargin) Relocate(mover, arrivalPose.Value.position, arrivalPose.Value.rotation);
                return;
            }
            if (!local.TryGetComponent<CharacterController>(out var controller) || !controller.isGrounded) return;
            float distance = course.GetDistance(local.transform.position);
            if (!course.IsOnRope(local.transform.position, 0f, out int lane)) return;
            void Capture(int targetLane, float at)
            {
                if (lane != targetLane || distance < at || at <= checkpointDistance || !course.IsLaneUsable(lane, at)) return;
                // 트리거가 해제돼도 남는 줄만 복귀 지점으로 기록한다.
                foreach (var gap in Stage.Gaps) if (gap.Lane == lane && at >= gap.From && at <= gap.To) return;
                checkpointDistance = at;
                checkpoint = new Pose(course.GetLanePosition(lane, at), Quaternion.LookRotation(course.Forward));
            }
            if (Stage.Kind == RehearsalStageKind.LaneChange) Capture(lane, course.Layout.GetLaneEnd(lane) - 3f);
            foreach (var spot in Stage.Swings) Capture(spot.Lane, spot.From - course.Shape.RopeWalkWidth);
            foreach (var trigger in Stage.Triggers) Capture(trigger.Lane, trigger.Distance);
            foreach (var gap in Stage.Gaps) Capture(gap.Lane, gap.From - 3f);
        }

        // ── 배치 ──

        void PlaceLocal(PlayerMover mover)
        {
            if (placedRound == round) return;
            if (local.State != PlayerState.Normal) return;
            if (course != null) PlaceLocalAtStart();
            else if (!completed) return;
            else
            {
                var deck = ringRoom != null ? ringRoom.Find(RingDeckPath)?.GetComponent<Collider>() : null;
                if (deck == null) return;
                var bounds = deck.bounds;
                float margin = mover.BodyRadius;
                var p = new Vector3(
                    Mathf.Clamp(local.transform.position.x, bounds.min.x + margin, bounds.max.x - margin),
                    bounds.max.y, bounds.center.z);
                Relocate(mover, p, Quaternion.LookRotation(Vector3.forward));
            }
            placedRound = round;
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
            if (string.IsNullOrEmpty(body)) { quest.Hide(); lastQuest = ""; return; }
            if (body == lastQuest && quest.Visible) return;
            if (quest.Show(s.DisplayTitle, "[tutorial] " + body)) lastQuest = body;
        }

        string QuestFor(RehearsalStage s, PlayerMover mover)
        {
            if (local.State == PlayerState.Fallen) return WaitingRoomText.Get("rehearsal.quest.fallen", "추락했습니다. 잠시 뒤 출발대에서 다시 시작합니다.");
            var p = local.transform.position;
            float distance = course.GetDistance(p);
            if (arrived.Contains(local.OwnerClientId))
                return WaitingRoomText.Format("rehearsal.quest.arrived-vote", "도착했습니다. 동료의 연습을 기다리거나 다음 구간 진행에 동의하세요. ({arrived}/{total}, 동의 {votes}/{required})", ("arrived", arrived.Count), ("total", participantCount), ("votes", votes.Count), ("required", participantCount / 2 + 1));
            int myLane = course.Layout.GetLaneSlotAt(course.GetSide(p));

            // 트리거 그네를 잡고 있다.
            int held = HeldTriggerIndex(local);
            if (held >= 0 && held < s.Triggers.Length)
            {
                if (OwnGapAhead(s, myLane, distance, out int ownGap) && IsGapConnected(ownGap))
                    return WaitingRoomText.Get("rehearsal.quest.trigger-release", "내 줄이 이어졌습니다. F나 Space로 트리거 그네를 놓고 건너세요.");
                return WaitingRoomText.Format("rehearsal.quest.trigger-hold", "잡고 있는 동안 {lane}번 줄이 이어집니다. 동료가 건너갈 때까지 잡고 있다가 F나 Space로 놓으세요.",
                    ("lane", s.Gaps[s.Triggers[held].Gap].Lane + 1));
            }

            // 인간 그네를 잡고 있거나 날아가는 중.
            var pole = TightropeSwingPole.Current;
            if (pole != null && (pole.IsHeld(local) || pole.IsInFlight(local)))
                return NextSwingChains(s, myLane, distance)
                    ? WaitingRoomText.Get("rehearsal.quest.swing-chain", "W, S로 반동을 키워 Space로 놓으세요. 날아가는 동안 다음 그네 가까이에서 F를 눌러 잡으세요.")
                    : WaitingRoomText.Get("rehearsal.quest.swing-release", "Space를 누르면 줄을 놓습니다. W, S로 그네를 조작해 건너편 줄로 넘어가세요.");

            if (s.Kind == RehearsalStageKind.LaneChange)
            {
                if (myLane == 0 && distance >= s.CheckpointDistance)
                    return WaitingRoomText.Get("rehearsal.quest.lane-jump", "Q, E + Space를 이용해 건너편 줄로 넘어가세요.");
                if (myLane >= 1) return "";
                return WaitingRoomText.Get("rehearsal.quest.balance", "W로 나아가고 A, D로 균형을 잡으며 외줄을 건너세요.");
            }

            // 앞의 끊긴 구간이 이어졌으면 건너기, 트리거 그네 근처면 잡기, 끊겨 있으면 기다리기, 그네 앞이면 탑승.
            bool gapAhead = OwnGapAhead(s, myLane, distance, out int gapIndex) && s.Gaps[gapIndex].From - distance <= 6f;
            if (gapAhead && IsGapConnected(gapIndex))
                return WaitingRoomText.Get("rehearsal.quest.trigger-cross", "줄이 이어졌습니다. 끊긴 구간을 건너세요.");
            for (int i = 0; i < s.Triggers.Length; i++)
            {
                var trigger = s.Triggers[i];
                if (trigger.Lane != myLane || distance > trigger.Distance + 1.6f || trigger.Distance - distance > 6f) continue;
                if (IsTriggerHeld(i)) break;
                return WaitingRoomText.Format("rehearsal.quest.trigger-grab", "트리거 그네를 F로 잡으면 {lane}번 줄이 이어집니다. 동료가 건널 때까지 잡고 있으세요.",
                    ("lane", s.Gaps[trigger.Gap].Lane + 1));
            }
            if (gapAhead)
                return WaitingRoomText.Get("rehearsal.quest.trigger-wait", "앞의 줄이 끊겨 있습니다. 짝 줄의 동료가 트리거 그네를 잡으면 이어집니다.");
            foreach (var spot in s.Swings)
                if (spot.Lane == myLane && distance < spot.From && spot.From - distance <= 5f)
                    return WaitingRoomText.Get("rehearsal.quest.swing-approach", "인간 그네에 탑승하세요. (줄 끝에서 F)");
            if (!AnythingAhead(s, myLane, distance)) return "";
            return WaitingRoomText.Get("rehearsal.quest.balance", "W로 나아가고 A, D로 균형을 잡으며 외줄을 건너세요.");
        }

        /// <summary>내 줄에서 아직 지나지 않은 가장 가까운 끊긴 구간.</summary>
        static bool OwnGapAhead(RehearsalStage s, int lane, float distance, out int index)
        {
            index = -1;
            float best = float.MaxValue;
            for (int i = 0; i < s.Gaps.Length; i++)
            {
                var gap = s.Gaps[i];
                if (gap.Lane == lane && distance < gap.To && gap.From < best) { best = gap.From; index = i; }
            }
            return index >= 0;
        }

        static bool NextSwingChains(RehearsalStage s, int lane, float distance)
        {
            for (int i = 0; i < s.Swings.Length; i++)
            {
                var spot = s.Swings[i];
                if (spot.Lane == lane && distance < spot.To) return spot.ChainNext;
            }
            return false;
        }

        static bool AnythingAhead(RehearsalStage s, int lane, float distance)
        {
            foreach (var spot in s.Swings) if (spot.Lane == lane && distance < spot.To) return true;
            foreach (var gap in s.Gaps) if (gap.Lane == lane && distance < gap.To) return true;
            foreach (var trigger in s.Triggers) if (trigger.Lane == lane && distance < trigger.Distance) return true;
            return false;
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
            if (quest.Show(WaitingRoomText.Get("rehearsal.gather-title", "리허설 완료 · 무대로"), "[tutorial] " + body)) lastQuest = body;
        }
    }
}
