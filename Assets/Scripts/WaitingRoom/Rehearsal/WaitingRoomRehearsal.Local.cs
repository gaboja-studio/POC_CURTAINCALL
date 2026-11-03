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
        // 추락 복귀는 늘 소구간 출발대다(사용자 2026-11-03: 줄 위 체크포인트는 외줄 UI가 다시 켜지지 않아 제거).
        Pose? arrivalPose;
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
            if (practice)
                WaitingRoomTutorialPanel.Show(
                    GuideTitle(WaitingRoomText.Get("rehearsal.practice-title", "자유 연습")),
                    WaitingRoomText.Get("rehearsal.practice-intro", "고른 소구간부터 다시 연습합니다. 추락해도 대가가 없고, 호스트가 언제든 연습을 끝낼 수 있습니다."),
                    IntroSeconds);
            else
                WaitingRoomTutorialPanel.Show(
                    GuideTitle(WaitingRoomText.Get("rehearsal.intro-title", "공연 전 리허설")),
                    WaitingRoomText.Get("rehearsal.intro", "공연 전 리허설이 시작됩니다. 안내에 따라 리허설을 진행하세요. 추락하면 마지막 체크포인트에서 재시작합니다."),
                    IntroSeconds);
        }

        void DeactivateLocal()
        {
            skipCursorRegion.enabled = false;
            // 재연습을 끝내면 줄 높이에 남지 않도록 연습실 바닥(링 위)으로 내려선다.
            if (!completed && local != null && local.IsSpawned && local.TryGetComponent(out PlayerMover mover) && InPracticeRoom(local.transform.position))
                RelocateToDeck(mover);
            Bind(null);
            quest.Hide();
            guide.SetTarget(null);
            WaitingRoomTutorialPanel.Hide();
        }

        /// <summary>리허설·재연습 밖에서 호스트에게 재연습 선택 버튼을 보이기 위한 상태 갱신(커서 영역).</summary>
        void TickPracticeOffer()
        {
            Bind(NetworkPlayer.Local);
            bool offer = CanOfferPractice;
            skipCursorRegion.enabled = offer;
            if (offer) skipCursorRegion.SetScreenRect(new Rect(SkipButtonRect.x, SkipButtonRect.y - 44f * RehearsalStage.All.Length, SkipButtonRect.width, 44f * RehearsalStage.All.Length + SkipButtonRect.height));
        }

        void DrawPracticeButtons()
        {
            if (!active && CanOfferPractice)
            {
                for (int i = 0; i < RehearsalStage.All.Length; i++)
                {
                    var rect = new Rect(SkipButtonRect.x, SkipButtonRect.y - 44f * (RehearsalStage.All.Length - i), SkipButtonRect.width, 38f);
                    if (GUI.Button(rect, WaitingRoomText.Format("rehearsal.practice-button", "{stage} 재연습", ("stage", RehearsalStage.All[i].DisplayTitle))))
                        StartPractice(i);
                }
                GUI.Label(new Rect(SkipButtonRect.x, SkipButtonRect.y - 44f * RehearsalStage.All.Length - 22f, 280f, 22f),
                    WaitingRoomText.Get("rehearsal.practice-hint", "호스트 전용 · 연습실에서 소구간을 골라 다시 연습"));
                return;
            }
            if (active && practice && HasAuthority
                && GUI.Button(new Rect(SkipButtonRect.x, SkipButtonRect.y - 114f, SkipButtonRect.width, 38f), WaitingRoomText.Get("rehearsal.practice-end", "연습 종료")))
                EndPractice();
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
            PlaceLocalAtStart();
            laneHintShown = false; mountHints.Clear(); actionGuideAfter = Time.unscaledTime + 4f;
            WaitingRoomTutorialPanel.Show(GuideTitle(WaitingRoomText.Get("rehearsal.fall-title", "추락")),
                WaitingRoomText.Get("rehearsal.fall", "리허설 진행 중에는 패널티가 없습니다. 마지막 체크포인트에서 다시 시작합니다."), 4f);
        }

        /// <summary>설명 패널 제목은 기획서 4.3대로 [tutorial] 접두어를 붙인다. 퀘스트 본문에는 붙이지 않는다.</summary>
        static string GuideTitle(string title) => "[tutorial] " + title;

        /// <summary>소구간·회차가 바뀌었다(코스가 새로 만들어졌거나 사라짐).</summary>
        void OnCourseChanged()
        {
            placedRound = -1; lastQuest = "";
            arrivalPose = null;
            laneHintShown = false; mountHints.Clear(); pendingStageIntro = false; actionGuideAfter = 0f;
            var s = Stage;
            if (course != null && s != null)
            {
                if (Time.unscaledTime < introductionUntil) pendingStageIntro = true;
                else WaitingRoomTutorialPanel.Show(GuideTitle(s.DisplayTitle), s.DisplayIntro, IntroSeconds);
            }
            else if (active && completed)
                WaitingRoomTutorialPanel.Show(GuideTitle(WaitingRoomText.Get("rehearsal.complete-title", "리허설 완료")),
                    WaitingRoomText.Get("rehearsal.complete", "바닥의 점선을 따라 무대 입구 통로로 가세요. 모든 공연 준비자가 모이면 첫 공연이 시작됩니다."), IntroSeconds);
        }

        void OnGUI()
        {
            DrawPracticeButtons();
            if (CanVote)
            {
                bool selected = votes.Contains(local.OwnerClientId);
                var rect = new Rect(SkipButtonRect.x, SkipButtonRect.y - 70f, 280f, 34f);
                bool value = GUI.Toggle(rect, selected, WaitingRoomText.Format("rehearsal.vote", "다음 구간으로 ({votes}/{required})", ("votes", votes.Count), ("required", participantCount / 2 + 1)));
                if (value != selected) RequestVote(value);
            }
            // Dev 모드 호스트 전용 즉시 클리어. 화면 오른쪽 가운데(투표·연습 버튼과 겹치지 않게).
            if (!CanClearCurrentStage || !CurtainCall.Dev.DevMode.Enabled) return;
            if (GUI.Button(DevClearRect, WaitingRoomText.Get("rehearsal.clear-button", "현재 구간 즉시 클리어 · 다음")))
                TryClearCurrentStage();
            GUI.Label(new Rect(DevClearRect.x, DevClearRect.y - 22f, 280f, 22f),
                WaitingRoomText.Get("rehearsal.clear-button-hint", "Dev · 호스트 전용 · Esc로 커서 해제"));
        }

        Rect DevClearRect => new Rect(SkipButtonRect.x, Screen.height * .5f - 19f, SkipButtonRect.width, 38f);

        void LocalTick()
        {
            bool devClear = CanClearCurrentStage && CurtainCall.Dev.DevMode.Enabled;
            skipCursorRegion.enabled = devClear || CanVote || (practice && HasAuthority);
            // 오른쪽 세로 띠: Dev 즉시 클리어(가운데)부터 투표·연습 종료(아래)까지.
            float top = devClear ? DevClearRect.y - 22f : SkipButtonRect.y - 114f;
            skipCursorRegion.SetScreenRect(new Rect(SkipButtonRect.x, top, SkipButtonRect.width, SkipButtonRect.yMax - top));
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
                WaitingRoomTutorialPanel.Show(GuideTitle(Stage.DisplayTitle), Stage.DisplayIntro, IntroSeconds);
                return;
            }
            if (!laneHintShown && Stage.Kind == RehearsalStageKind.LaneChange
                && course.GetDistance(local.transform.position) >= Stage.CheckpointDistance
                && course.Layout.GetLaneSlotAt(course.GetSide(local.transform.position)) == 0)
            {
                laneHintShown = true;
                WaitingRoomTutorialPanel.Show(GuideTitle(WaitingRoomText.Get("rehearsal.lane-guide-title", "1단계 · 외줄타기 체크포인트 도달")),
                    WaitingRoomText.Get("rehearsal.lane-guide", "줄이 끊긴 지점에서는 Q/E + Space키로 옆줄로 건너갈 수 있습니다."), IntroSeconds);
            }
            var target = PiggybackSystem.Current != null ? PiggybackSystem.Current.FindMountTarget(local) : null;
            if (target != null && mountHints.Add(target.OwnerClientId))
                WaitingRoomTutorialPanel.Show(GuideTitle(WaitingRoomText.Get("rehearsal.mount-title", "목마 상호작용")),
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
        }

        // ── 배치 ──

        void PlaceLocal(PlayerMover mover)
        {
            if (placedRound == round) return;
            if (local.State != PlayerState.Normal) return;
            if (course != null) PlaceLocalAtStart();
            else if (!completed) return;
            else if (!RelocateToDeck(mover)) return;
            placedRound = round;
        }

        /// <summary>연습실 링 바닥 위, 지금 x에 가장 가까운 자리로 내려선다.</summary>
        bool RelocateToDeck(PlayerMover mover)
        {
            var deck = ringRoom != null ? ringRoom.Find(RingDeckPath)?.GetComponent<Collider>() : null;
            if (deck == null) return false;
            // 링 가운데로 내려선다. 가장자리에 두면 난간에 막혀 연습실을 나가지 못했다(2026-11-03 출구 이동 검사).
            var bounds = deck.bounds;
            var p = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Relocate(mover, p, Quaternion.LookRotation(Vector3.forward));
            return true;
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
            if (quest.Show(s.DisplayTitle, body)) lastQuest = body;
        }

        string QuestFor(RehearsalStage s, PlayerMover mover)
        {
            if (local.State == PlayerState.Fallen) return WaitingRoomText.Get("rehearsal.quest.fallen", "추락했습니다. 잠시 뒤 출발대에서 다시 시작합니다.");
            var p = local.transform.position;
            float distance = course.GetDistance(p);
            // 도착 뒤 문구는 기획서 4.4대로. 동의 현황은 투표 토글이 보여 준다.
            if (arrived.Contains(local.OwnerClientId))
                return WaitingRoomText.Get("rehearsal.quest.wait-team", "도착했습니다. 동료가 모두 도착할 때까지 기다리세요.");
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
            if (quest.Show(WaitingRoomText.Get("rehearsal.gather-title", "리허설 완료 · 무대로"), body)) lastQuest = body;
        }
    }
}
