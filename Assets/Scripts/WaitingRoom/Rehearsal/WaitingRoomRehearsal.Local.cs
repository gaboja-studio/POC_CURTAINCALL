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
        PlayerMover placementLock;
        bool placementControl;
        float nextPlacementSend;
        float introductionUntil, actionGuideAfter;
        bool pendingStageIntro, laneHintShown;
        float laneIntroAt;
        readonly System.Collections.Generic.HashSet<ulong> mountHints = new();
        // 추락 복귀는 늘 소구간 출발대다(사용자 2026-11-03: 줄 위 체크포인트는 외줄 UI가 다시 켜지지 않아 제거).
        // 도착대 투명 벽(이 화면 전용). 도착한 내 캐릭터만 도착대 밖으로 못 나가게 막는다(사용자 2026-11-04: 되돌리기 대신 벽).
        GameObject finishWalls;
        int lessonReturnedRound = -1;
        string lastQuest = "";
        string rescueGuide = "";
        StageEntranceGather gather;
        CameraCursorExclusion skipCursorRegion;
        // 오른쪽 아래는 투표 패널(1920×1080 기준 높이 230+여백 36) 자리다. IMGUI 버튼은 그 위에 둔다.
        Rect SkipButtonRect => new Rect(Mathf.Max(12f, Screen.width - 292f), Screen.height - 106f - 360f * Screen.height / 1080f, 280f, 38f);

        void AwakeLocal()
        {
            quest = new WaitingRoomRightPanel(transform, "RehearsalQuestCanvas", QuestTop, QuestWidth, "first-day.panel.") { GuideStyle = true };
            guide = gameObject.AddComponent<RehearsalPathGuide>();
            guide.VisibilityChanged += PathVisibility;
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
            // 공연 씬의 균형바 캔버스와 같은 화면 맞춤(Expand). 화면비가 16:9가 아니어도 공연과 같은 크기·위치로 보인다.
            var scaler = hudRoot.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null) scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var cheerRoot = Instantiate(cheerPrefab, hudRoot.transform, false);
            var stock = cheerRoot.GetComponent<CurtainCall.CheerGauge.C1CheerGaugeBinding>();
            if (stock != null) stock.enabled = false;
            var hudView = hudRoot.GetComponent<CurtainCall.UI.PerformanceHudV1.PerformanceHUDView>();
            // Tightrope.unity의 TightropeBalanceCanvas에 저장된 override도 그대로 반영한다.
            // 정본 프리팹의 12/4px 기본값은 실제 공연의 23/15px 위험 게이지와 다르다.
            if (hudView != null && hudView.Balance != null)
            {
                var track = hudView.Balance.transform.Find("60_DwellTrack") as RectTransform;
                var fill = hudView.Balance.transform.Find("61_DwellFill") as RectTransform;
                if (track != null) track.sizeDelta = new Vector2(track.sizeDelta.x, 23f);
                if (fill != null) fill.sizeDelta = new Vector2(fill.sizeDelta.x, 15f);
            }
            var binding = hudRoot.AddComponent<CurtainCall.UI.SceneIntegration.PerformanceSceneBinding>();
            binding.Configure(hudView, cheerRoot.GetComponent<CurtainCall.CheerGauge.C1CheerGaugeView>(), null);
            // 공연 씬의 균형 UI(TightropeBalanceCanvas)는 같은 프리팹에서 균형바만 남긴 캔버스다(사용자 2026-11-04: 공연 균형 UI 그대로).
            // 바인딩이 갱신하는 진행도·응원 게이지는 그대로 두고 화면에서만 숨긴다(타이머는 바인딩이 이미 끈다).
            HideOnScreen(cheerRoot);
            if (hudView != null && hudView.Progress != null) HideOnScreen(hudView.Progress.gameObject);
        }

        static void HideOnScreen(GameObject target)
        {
            // Unity 오브젝트는 ?? 로 없음을 판정하지 않는다(에디터의 가짜 null). 반드시 TryGetComponent로 확인한다.
            if (!target.TryGetComponent(out CanvasGroup group)) group = target.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false; group.ignoreParentGroups = true;
        }

        void PathVisibility(bool shown) => RecordLocal(shown ? "GUIDE_SHOWN" : "GUIDE_HIDDEN", guideId: "GATHER_PATH");

        void RecordLocal(string state, string reason = null, string guideId = null)
        {
            var player = NetworkPlayer.Local;
            if (player != null)
                CurtainCall.Telemetry.Pipeline.SpatialLogAdapter.RehearsalAction("TIGHTROPE", state, round, stage,
                    active, IsCompleted, IsPracticing, player.OwnerClientId, false, reason, guideId: guideId);
        }

        void DestroyLocal()
        {
            if (guide != null) guide.VisibilityChanged -= PathVisibility;
            ReleasePlacementLock();
        }

        void ShowGuide(string id, string title, string body, float seconds)
        {
            if (WaitingRoomTutorialPanel.Show(title, body, seconds)) RecordLocal("GUIDE_SHOWN", guideId: id);
        }

        void ActivateLocal()
        {
            WaitingRoomFirstDayFlow.SuspendForRehearsal();
            placedRound = -1; lastQuest = "";
            rescueGuide = "";
            introductionUntil = Time.unscaledTime + IntroSeconds;
            gather = null;
            if (practice)
                ShowGuide("PRACTICE_INTRO",
                    WaitingRoomText.Get("rehearsal.practice-title", "자유 연습"),
                    WaitingRoomText.Get("rehearsal.practice-intro", "고른 소구간부터 다시 연습합니다. 추락해도 대가가 없고, 호스트가 언제든 연습을 끝낼 수 있습니다."),
                    IntroSeconds);
            else
                ShowGuide("INTRO",
                    WaitingRoomText.Get("rehearsal.intro-title", "공연 전 리허설"),
                    WaitingRoomText.Get("rehearsal.intro", "공연 전 리허설이 시작됩니다. 안내에 따라 리허설을 진행하세요. 추락하면 마지막 체크포인트에서 재시작합니다."),
                    IntroSeconds);
        }

        void DeactivateLocal()
        {
            ReleasePlacementLock();
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
            // 소구간 골라 재연습하는 호스트 버튼은 Dev 확인용으로만 남긴다. 실제 재연습은 연습실 외줄 상호작용 + 전원 동의.
            bool offer = CanOfferPractice && CurtainCall.Dev.DevMode.Enabled;
            skipCursorRegion.enabled = offer;
            if (offer) skipCursorRegion.SetScreenRect(new Rect(SkipButtonRect.x, SkipButtonRect.y - 44f * RehearsalStage.All.Length, SkipButtonRect.width, 44f * RehearsalStage.All.Length + SkipButtonRect.height));
        }

        void DrawPracticeButtons()
        {
            if (!active && CanOfferPractice && CurtainCall.Dev.DevMode.Enabled)
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
            PlaceLocalAtStart(recovery: true);
            laneHintShown = false; mountHints.Clear(); actionGuideAfter = Time.unscaledTime + 4f;
            ShowGuide("FALL_RECOVERY", WaitingRoomText.Get("rehearsal.fall-title", "추락"),
                WaitingRoomText.Get("rehearsal.fall", "리허설 진행 중에는 패널티가 없습니다. 마지막 체크포인트에서 다시 시작합니다."), 4f);
        }

        /// <summary>
        /// 퀘스트 패널 제목(기획서 4.4, 조건별). [tutorial] 표기는 조건 달성형 퀘스트(오른쪽)에만 쓰고 설명 패널(왼쪽)에는 쓰지 않는다.
        /// 문구 표의 <c>rehearsal.quest.{key}.title</c>을 읽고, 없으면 "[tutorial]"만 쓴다.
        /// </summary>
        static string QuestTitle(string key) => WaitingRoomText.Get("rehearsal.quest." + key + ".title", "[tutorial]");
        string questKey = "";
        string Quest(string key, string fallback) { questKey = key; return WaitingRoomText.Get("rehearsal.quest." + key, fallback); }
        string QuestFormat(string key, string fallback, int lane) { questKey = key; return WaitingRoomText.Format("rehearsal.quest." + key, fallback, ("lane", lane)); }

        /// <summary>소구간·회차가 바뀌었다(코스가 새로 만들어졌거나 사라짐).</summary>
        void OnCourseChanged()
        {
            lessonReturnedRound = -1;
            ReleasePlacementLock();
            nextPlacementSend = 0f;
            placedRound = -1; lastQuest = "";
            finishWalls = null;
            laneHintShown = false; mountHints.Clear(); pendingStageIntro = false; actionGuideAfter = 0f; laneIntroAt = 0f;
            var s = Stage;
            if (course != null && s != null)
            {
                if (Time.unscaledTime < introductionUntil) pendingStageIntro = true;
                else ShowStageIntroduction();
            }
            else if (active && completed)
                ShowGuide("COMPLETE", WaitingRoomText.Get("rehearsal.complete-title", "리허설 완료"),
                    WaitingRoomText.Get("rehearsal.complete", "바닥의 길안내를 따라 무대 입구 통로로 가세요. 모든 공연 준비자가 모이면 첫 공연이 시작됩니다."), IntroSeconds);
        }

        void OnGUI()
        {
            DrawPracticeButtons();
            // 기획서의 호스트 전용 즉시 클리어. 화면 오른쪽 가운데(투표·연습 버튼과 겹치지 않게).
            if (!CanClearCurrentStage) return;
            if (GUI.Button(DevClearRect, WaitingRoomText.Get("rehearsal.clear-button", "현재 구간 즉시 클리어 · 다음")))
                TryClearCurrentStage();
            GUI.Label(new Rect(DevClearRect.x, DevClearRect.y - 22f, 280f, 22f),
                WaitingRoomText.Get("rehearsal.clear-button-hint", "호스트 전용 · Esc로 커서 해제"));
        }

        Rect DevClearRect => new Rect(SkipButtonRect.x, Screen.height * .5f - 19f, SkipButtonRect.width, 38f);

        void LocalTick()
        {
            bool devClear = CanClearCurrentStage;
            skipCursorRegion.enabled = devClear || CanVote || (practice && HasAuthority);
            // 오른쪽 세로 띠: 호스트 즉시 클리어(가운데)부터 투표·연습 종료(아래)까지.
            float top = devClear ? DevClearRect.y - 22f : SkipButtonRect.y - 114f;
            skipCursorRegion.SetScreenRect(new Rect(SkipButtonRect.x, top, SkipButtonRect.width, SkipButtonRect.yMax - top));
            Bind(NetworkPlayer.Local);
            if (local == null || !local.IsSpawned || !local.TryGetComponent(out PlayerMover mover)) { quest.Hide(); return; }
            PlaceLocal(mover);
            UpdatePlacement(mover);
            if (completed) { TickCompleted(); return; }
            guide.SetTarget(null);
            UpdateActionGuides();
            TrackPersonalProgress(mover);
            UpdateQuest(mover);
        }

        void UpdateActionGuides()
        {
            if (UpdateRescueGuide()) return;
            if (Stage == null || local.State != PlayerState.Normal || arrived.Contains(local.OwnerClientId)
                || Time.unscaledTime < actionGuideAfter) return;
            if (pendingStageIntro)
            {
                if (Time.unscaledTime < introductionUntil) return;
                pendingStageIntro = false;
                ShowStageIntroduction();
                return;
            }
            if (laneIntroAt > 0f && Time.unscaledTime >= laneIntroAt)
            {
                laneIntroAt = 0f;
                ShowGuide("LANE_INTRO", WaitingRoomText.Get("rehearsal.lane-guide-title", "끊어진 줄"),
                    WaitingRoomText.Get("rehearsal.lane-guide", "줄이 끊긴 지점에서는 Q/E + Space키로 옆줄로 건너갈 수 있습니다."), IntroSeconds);
                return;
            }
            if (!laneHintShown && Stage.Kind == RehearsalStageKind.LaneChange
                && course.GetDistance(local.transform.position) >= Stage.CheckpointDistance)
            {
                laneHintShown = true;
                ShowGuide("LANE_CHANGE", WaitingRoomText.Get("rehearsal.lane-guide-title", "옆줄로 건너기"),
                    WaitingRoomText.Get("rehearsal.lane-guide", "Q/E를 누른 채 Space로 옆줄로 건너세요. 동료가 서 있는 줄의 손잡기 범위에 착지해 보세요."), IntroSeconds);
            }
            var target = PiggybackSystem.Current != null
                ? PiggybackSystem.Current.FindMountTarget(local) : null;
            if (target != null && mountHints.Add(target.OwnerClientId))
                ShowGuide("MOUNT", WaitingRoomText.Get("rehearsal.mount-title", "목마 상호작용"),
                    WaitingRoomText.Get("rehearsal.mount", "동료 가까이에서 F키를 눌러 목마에 올라탈 수 있습니다."), IntroSeconds);
        }

        void ShowStageIntroduction()
        {
            ShowGuide("STAGE_INTRO", Stage.DisplayGuideTitle, Stage.DisplayIntro, IntroSeconds);
            if (Stage.Kind == RehearsalStageKind.LaneChange)
                laneIntroAt = Time.unscaledTime + IntroSeconds;
        }

        string RescueQuestKey()
        {
            var hangs = TightropeHangSystem.Current;
            if (hangs == null || local == null || local.State != PlayerState.Normal) return "";
            if (hangs.IsHanging(local)) return "hang-help";
            if (arrived.Contains(local.OwnerClientId)) return "";
            if (hangs.IsRescuing(local)) return "rescue-pull";
            foreach (var hang in hangs.Hangs)
                if (hang.Id != local.OwnerClientId && !hang.IsPulling) return "rescue-team";
            return "";
        }

        static string RescueText(string key) => WaitingRoomText.Get("rehearsal.quest." + key, key switch
        {
            "hang-help" => "줄에 매달려 버티는 동안 동료에게 도움을 요청하세요. 가까운 동료가 F를 길게 눌러 끌어올릴 수 있습니다. 혼자 올라올 수는 없습니다.",
            "rescue-pull" => "동료를 끌어올리는 중입니다. 구조가 끝날 때까지 가까이에서 F를 계속 누르세요.",
            _ => "줄에 매달린 동료 가까이로 이동해 F를 길게 눌러 끌어올리세요. 버티는 시간이 끝나기 전에 구조해야 합니다."
        });

        bool UpdateRescueGuide()
        {
            string key = RescueQuestKey();
            if (key == rescueGuide) return key.Length > 0;
            if (key.Length == 0)
            {
                if (WaitingRoomTutorialPanel.CurrentTitle == RescueTitle(rescueGuide)) WaitingRoomTutorialPanel.Hide();
                rescueGuide = "";
                return false;
            }
            // Native seven-second hangs must not wait behind the ten-second stage introduction.
            if (WaitingRoomTutorialPanel.Show(RescueTitle(key), RescueText(key), IntroSeconds)) rescueGuide = key;
            return true;
        }

        static string RescueTitle(string key) => QuestTitle(key).Replace("[tutorial] ", "");

        void TrackPersonalProgress(PlayerMover mover)
        {
            bool block = course != null && Stage != null && local.State == PlayerState.Normal && arrived.Contains(local.OwnerClientId);
            if (block && Stage.Kind == RehearsalStageKind.Trapeze && lessonReturnedRound != round)
            {
                // 한 번의 도약/다음 봉 잡기가 끝나면 연습을 마친다. 공연의 연속 봉 코스를 재현하지 않는다.
                Pose pose = course.GetFinishPose(Mathf.Max(0, local.Slot));
                Relocate(mover, pose.position, pose.rotation);
                lessonReturnedRound = round;
            }
            if (block && finishWalls == null) finishWalls = BuildFinishWalls();
            if (finishWalls != null && finishWalls.activeSelf != block) finishWalls.SetActive(block);
        }

        /// <summary>도착대 네 둘레의 보이지 않는 벽. 앞쪽(줄 쪽) 벽은 도착선 바로 앞이라 도착 판정 위치와 겹치지 않는다.</summary>
        GameObject BuildFinishWalls()
        {
            const float Height = 4f, Thickness = .3f;
            var layout = course.Layout;
            float min = float.MaxValue, max = float.MinValue;
            for (int lane = 0; lane < layout.LaneCount; lane++)
            {
                if (layout.GetLaneEnd(lane) < layout.FinishDistance) continue;
                min = Mathf.Min(min, layout.GetLaneSide(lane)); max = Mathf.Max(max, layout.GetLaneSide(lane));
            }
            if (min > max) return null;
            float side = (min + max) * .5f, half = (max - min) * .5f + course.Shape.SideMargin;
            float near = course.FinishDistance - .4f - Thickness * .5f, far = course.FinishDistance + course.Shape.FinishPlatformLength + Thickness * .5f;
            float mid = (near + far) * .5f, length = far - near + Thickness;
            var root = new GameObject("RehearsalFinishWalls");
            root.transform.SetParent(courseRoot.transform, true);
            var rotation = Quaternion.LookRotation(course.Forward, Vector3.up);
            void Wall(string name, float s, float d, Vector3 size)
            {
                var wall = new GameObject(name, typeof(BoxCollider));
                wall.transform.SetParent(root.transform, false);
                wall.transform.SetPositionAndRotation(course.transform.position + course.Right * s + course.Forward * d + Vector3.up * (Height * .5f - .5f), rotation);
                wall.GetComponent<BoxCollider>().size = size;
            }
            Wall("Front", side, near, new Vector3(half * 2f + Thickness * 2f, Height, Thickness));
            Wall("Back", side, far, new Vector3(half * 2f + Thickness * 2f, Height, Thickness));
            Wall("Left", side - half - Thickness * .5f, mid, new Vector3(Thickness, Height, length));
            Wall("Right", side + half + Thickness * .5f, mid, new Vector3(Thickness, Height, length));
            root.SetActive(false);
            return root;
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

        void PlaceLocalAtStart(bool recovery = false)
        {
            if (course == null || local == null || !local.TryGetComponent(out PlayerMover mover)) return;
            Pose pose = course.GetStartPose(Mathf.Max(0, local.Slot));
            if (Stage.Kind is RehearsalStageKind.LaneChange or RehearsalStageKind.SinglePole or RehearsalStageKind.ConsecutivePoles)
            {
                int slot = Mathf.Max(0, local.Slot);
                // 좁은 줄 중심을 유지해야 W만으로 진입한다. 네 명 모두 4m 출발대 안에 세로로 선다.
                pose.position = course.GetLanePosition(0, -.6f - slot * .85f);
            }
            Relocate(mover, pose.position, pose.rotation);
            if (recovery) RecordLocal("RECOVERED", "START_PLATFORM");
        }

        // 출발점 순간이동과 확인 메시지는 별도 경로다. 호스트에도 같은 위치가 도착할 때까지 기다린다.
        bool IsStartPosition(Vector3 position) => course != null
            && float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z)
            && course.GetDistance(position) < -.1f
            && course.GetDistance(position) >= -course.Shape.StartPlatformLength - 1f
            && Mathf.Abs(position.y - course.transform.position.y) <= course.Shape.RopeHeightTolerance
            && Mathf.Abs(course.GetSide(position)) <= (course.LaneCount - 1) * course.LaneSpacing * .5f + course.Shape.SideMargin;

        void UpdatePlacement(PlayerMover mover)
        {
            if (completed || course == null || placedRound != round || placed.Contains(local.OwnerClientId))
            { ReleasePlacementLock(); return; }
            if (local.State != PlayerState.Normal) return;
            if (placementLock == null)
            {
                placementLock = mover; placementControl = mover.ControlEnabled;
                mover.SetControlEnabled(false);
            }
            if (!IsStartPosition(local.transform.position)) return;
            if (HasAuthority)
            {
                AcceptPlacement(local.OwnerClientId, round, local.transform.position);
                if (placed.Contains(local.OwnerClientId)) ReleasePlacementLock();
                return;
            }
            if (messaging == null || Time.unscaledTime < nextPlacementSend) return;
            nextPlacementSend = Time.unscaledTime + .2f;
            using var writer = new Unity.Netcode.FastBufferWriter(17, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe((byte)Operation.Placed);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(local.transform.position);
            messaging.SendNamedMessage(RequestMessage, Unity.Netcode.NetworkManager.ServerClientId, writer,
                Unity.Netcode.NetworkDelivery.ReliableSequenced);
        }

        void AcceptPlacement(ulong sender, int placementRound, Vector3 position)
        {
            if (!HasAuthority || !active || completed || builtRound != round || placementRound != round
                || !IsStartPosition(position)) return;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.OwnerClientId != sender || player.State != PlayerState.Normal) continue;
                if (!placed.Contains(sender) && (!IsStartPosition(player.transform.position)
                    || Vector3.Distance(player.transform.position, position) > .75f)) return;
                if (placed.Add(sender)) { RecordAction("PLACED", sender, "START_PLATFORM"); version++; Publish(); }
                // 확인 요청 재전송에도 응답해 초기 상태 동기화와의 순서 차이를 흡수한다.
                else Publish(sender);
                return;
            }
        }

        void ReleasePlacementLock()
        {
            if (placementLock != null) placementLock.SetControlEnabled(placementControl);
            placementLock = null;
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
            if (quest.Show(QuestTitle(questKey), body)) lastQuest = body;
        }

        string QuestFor(RehearsalStage s, PlayerMover mover)
        {
            if (local.State == PlayerState.Fallen) return Quest("fallen", "추락했습니다. 잠시 뒤 출발대에서 다시 시작합니다.");
            string rescue = RescueQuestKey();
            if (rescue.Length > 0) return Quest(rescue, RescueText(rescue));
            var p = local.transform.position;
            float distance = course.GetDistance(p);
            // 도착 뒤 문구는 기획서 4.4대로. 동의 현황은 투표 토글이 보여 준다.
            if (arrived.Contains(local.OwnerClientId))
                return Quest("wait-team", "도착했습니다. 동료가 모두 도착할 때까지 기다리세요.");
            int myLane = course.Layout.GetLaneSlotAt(course.GetSide(p));

            if (s.Kind == RehearsalStageKind.Relay)
            {
                var relay = TightropeRelayGate.Current;
                if (relay != null && relay.HeldPole(local) >= 0)
                    return QuestFormat("trigger-hold", "잡고 있는 동안 {lane}번 줄이 이어집니다. 동료가 건너갈 때까지 잡고 있다가 F나 Space로 놓으세요.", 2);
                if (myLane == 0 && distance >= 5f)
                    return QuestFormat("trigger-grab", "트리거 그네를 F로 잡으면 {lane}번 줄이 이어집니다. 동료가 건널 때까지 그네를 잡고 있으세요.", 2);
                if (myLane == 1 && distance >= 6f && distance < 20f)
                    return relay != null && relay.IsLaneConnected(1)
                        ? Quest("trigger-open", "연결된 줄을 따라 건너세요.")
                        : Quest("trigger-closed", "앞의 줄이 끊겨 있습니다. 건너편 줄의 공연 대기자가 트리거 그네를 잡으면 이어집니다.");
            }
            if (s.Kind is RehearsalStageKind.SinglePole or RehearsalStageKind.ConsecutivePoles)
            {
                var poles = TightropeTrapeze.Current;
                if (poles != null && (poles.IsHeld(local) || poles.IsInFlight(local)))
                    return s.Kind == RehearsalStageKind.SinglePole
                        ? Quest("pole-single", "Space를 누르면 그네를 놓습니다. W, S로 그네를 조작해 건너편 줄로 도착하세요.")
                        : Quest("pole-next", "그네를 조작해 건너편 그네에 도착하세요. 체공 중 F를 눌러 그네를 잡을 수 있습니다.");
                if (poles != null && distance >= poles.EntryDistance - 5f && distance < course.TrapezeGapTo)
                    return Quest("pole-grab", "줄 끝에서 F를 눌러 인간 그네에 탑승하세요.");
            }
            if (s.Kind == RehearsalStageKind.Trapeze)
            {
                var trapeze = TightropeTrapeze.Current;
                if (trapeze != null)
                {
                    if (trapeze.IsHeld(local) || trapeze.IsInFlight(local))
                        return NativeDeviceQuest(s.Kind);
                    if (distance < course.TrapezeGapTo && trapeze.EntryDistance - distance <= 5f)
                    {
                        for (int i = 0; i < trapeze.PoleCount; i++)
                            if (trapeze.CanAttachToPole(i, local))
                                return Quest("trapeze-attach", "봉을 잡은 동료 가까이에서 F로 매달리세요. 반동에 맞춰 Space로 다음 봉을 향해 날아갑니다.");
                        if (myLane != trapeze.LaneIndex && distance < course.TrapezeGapFrom)
                            return QuestFormat("trapeze-lane", "Q/E + Space로 {lane}번 줄의 공중그네 진입 위치로 이동하세요.", trapeze.LaneIndex + 1);
                        var stack = PiggybackSystem.Current;
                        return stack != null && stack.CanEnterTrapeze(local)
                            ? Quest("trapeze-grab", "목마 위쪽 사람이 F로 첫 봉을 잡으세요. 목마가 풀리면 아래 동료도 F로 매달리세요.")
                            : Quest("trapeze-mount", "동료 가까이에서 F로 목마를 연결하세요. 첫 공중그네는 목마로 진입합니다.");
                    }
                }
            }
            if (s.Kind == RehearsalStageKind.Piggyback)
            {
                var stack = PiggybackSystem.Current;
                byte progress = Progress(local.OwnerClientId);
                if ((progress & JumpedFromShoulders) != 0)
                    return Quest("mount-land", "분리 점프했습니다. 도착대에 착지하세요.");
                if (stack == null || !stack.IsInStack(local))
                    return Quest("mount", "동료 가까이에서 F로 목마를 연결하세요. 혼자 걸어가면 완료되지 않습니다.");
                return stack.GetFloor(local) == 1
                    ? Quest("mount-move", "위 사람을 태우고 2m 이상 이동하세요. 도착대 가까이에서 위 사람의 도약을 도와주세요.")
                    : (progress & Carried) == 0
                        ? Quest("mount-balance", "A/D로 균형을 잡으며 아래 동료와 2m 이동하세요.")
                        : Quest("mount-jump", "도착대 가까이에서 W + Space로 목마에서 앞으로 도약해 착지하세요.");
            }
            if (s.Kind == RehearsalStageKind.LaneChange && myLane == 0 && distance >= s.CheckpointDistance)
                return Quest("lane-jump", "Q, E + Space를 이용해 건너편 줄로 넘어가세요.");
            return Quest("balance", "W/S로 이동하고 A/D로 균형을 잡으며 외줄을 건너세요.");
        }

        // ── 완료 뒤 집결 길안내(기획서 15.3) ──

        void TickCompleted()
        {
            if (gather == null) gather = FindFirstObjectByType<StageEntranceGather>();
            if (gather == null) { guide.SetTarget(null); quest.Hide(); return; }
            bool inside = gather.IsInside(local);
            guide.SetTarget(inside ? null : gather.GuideDestination);
            string body = inside
                ? WaitingRoomText.Format("rehearsal.quest.gathered", "무대 입구 통로에 모였습니다 {inside}/{alive}. 동료를 기다리세요.", ("inside", gather.InsideCount), ("alive", gather.AliveCount))
                : WaitingRoomText.Format("rehearsal.quest.gather", "점선을 따라 무대 입구 통로로 가세요. 전원 {inside}/{alive}이 모이면 공연이 시작됩니다.", ("inside", gather.InsideCount), ("alive", gather.AliveCount));
            if (body == lastQuest && quest.Visible) return;
            if (quest.Show(QuestTitle(inside ? "gathered" : "gather"), body)) lastQuest = body;
        }
    }
}
