using System;
using System.Collections.Generic;
using System.Text;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.Survival;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.DummySpaces
{
    /// <summary>씬에 저장된 더미 선택/결과 화면. 기존 공개 API를 호출하며 규칙을 판정하지 않는다.</summary>
    public sealed class DummySpaceCanvas : MonoBehaviour
    {
        [Serializable]
        public sealed class ActionButton
        {
            public string id;
            public Button button;
        }

        public TMP_Text status;
        public TMP_Text live;
        public TMP_Text clock;
        public Image clockFill;
        public TMP_Text steps;
        public TMP_Text selection;
        public TMP_Text condition;
        public TMP_Text resultTitle;
        public TMP_Text resultBadge;
        public TMP_Text resultBody;
        public TMP_Text changes;
        public TMP_Text connection;
        public TMP_Text history;
        public TMP_Text details;
        public GameObject detailsPanel;
        public ActionButton[] actions;
        public DummySpacePreview preview;

        DummySpaceDiagnostics diagnostics;
        DummyPerformance performance;
        DummyWaitingRoom waiting;
        DummyExploration exploration;
        DummyEscapeRoute escape;
        readonly List<(Button button, UnityEngine.Events.UnityAction handler)> listeners = new();
        int selected;
        int serverIndex;
        float nextRefresh;
        bool initialized;
        public int ExecutionCount { get; private set; }
        public string LastAction { get; private set; } = "";
        public string ResultText => resultBody != null ? resultBody.text : "";
        public int SelectedIndex => selected;
        public string SelectedParticipant => Person()?.ParticipantId;

        static readonly Color Teal = new(0.05f, 0.48f, 0.43f);
        static readonly Color Amber = new(0.58f, 0.34f, 0.05f);
        static readonly Color Red = new(0.73f, 0.20f, 0.22f);

        void Start()
        {
            diagnostics = FindAnyObjectByType<DummySpaceDiagnostics>();
            if (diagnostics == null) return;
            performance = diagnostics.GetComponent<DummyPerformance>();
            waiting = diagnostics.GetComponent<DummyWaitingRoom>();
            exploration = diagnostics.GetComponent<DummyExploration>();
            escape = diagnostics.GetComponent<DummyEscapeRoute>();
            foreach (var action in actions)
            {
                string id = action.id;
                UnityEngine.Events.UnityAction handler = () => Execute(id);
                action.button.onClick.AddListener(handler);
                listeners.Add((action.button, handler));
            }
            initialized = true;
            Refresh();
        }

        void OnDestroy()
        {
            foreach (var item in listeners)
                if (item.button != null) item.button.onClick.RemoveListener(item.handler);
            listeners.Clear();
        }

        void Update()
        {
            if (!initialized || diagnostics == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.15f;
            Refresh();
        }

        RewardSegment Person()
        {
            var list = diagnostics != null ? diagnostics.Rewards.State?.Segments : null;
            if (list == null || list.Count == 0) return null;
            selected = Mathf.Clamp(selected, 0, list.Count - 1);
            return list[selected];
        }

        List<NetworkPlayer> Players()
        {
            var list = new List<NetworkPlayer>();
            foreach (var p in NetworkPlayer.All) if (p.IsSpawned) list.Add(p);
            list.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            return list;
        }

        NetworkPlayer ServerPlayer()
        {
            var list = Players();
            if (list.Count == 0) return null;
            serverIndex = Mathf.Clamp(serverIndex, 0, list.Count - 1);
            return list[serverIndex];
        }

        public bool CanExecute(string id)
        {
            if (diagnostics == null) return false;
            var campaign = diagnostics.Campaign;
            var phase = campaign.State.Phase;
            var rewards = diagnostics.Rewards;
            switch (id)
            {
                case "details": return true;
                case "advance": return NetworkSessionManager.EnsureExists().ConnectionState == SessionConnectionState.Offline
                    && (!campaign.State.IsRunning || campaign.IsOfflineRun)
                    && phase != CampaignPhase.Night && phase != CampaignPhase.Escape && phase != CampaignPhase.Ending;
                case "ready": return campaign.HasAuthority && phase == CampaignPhase.Night;
                case "start": return performance != null && !campaign.State.IsRunning
                    && NetworkSessionManager.EnsureExists().ConnectionState == SessionConnectionState.Offline;
                case "intro": return campaign.HasAuthority && phase == CampaignPhase.Intro;
                case "begin": return performance != null && rewards.HasAuthority && phase == CampaignPhase.Performance && !rewards.TransitionPending;
                case "person": return Person() != null;
                case "arrive": return performance != null && Person() != null && performance.CanAct(Person().ParticipantId) && !performance.HasArrived(Person().ParticipantId);
                case "cut": case "kill": case "cheer": return performance != null && Person() != null && performance.CanAct(Person().ParticipantId);
                case "timeout": return performance != null && rewards.HasAuthority && rewards.State != null && rewards.State.IsOpen;
                case "spend": return waiting != null && rewards.HasAuthority;
                case "leave": case "stage": return campaign.HasAuthority && phase == CampaignPhase.RestTime;
                case "room": return exploration != null;
                case "expire": return exploration != null && campaign.HasAuthority && phase == CampaignPhase.RestTime;
                case "lost": case "backpack": case "restorePreview": return waiting != null;
                case "catchPreview": return exploration != null;
                case "server": return Players().Count > 0;
                case "restore": return waiting != null && ServerPlayer() != null && ServerPlayer().IsServer;
                case "catch": return ServerPlayer() != null && ServerPlayer().IsServer
                    && SurvivalRules.ServerPlace == (escape != null ? SurvivalPlace.EscapeRoute : SurvivalPlace.Exploration);
                case "escape": return escape != null && campaign.HasAuthority && phase == CampaignPhase.RestTime;
                case "row": return escape != null;
                case "rowArrive": case "rowRetire": return escape != null && phase == CampaignPhase.Escape;
                case "success": case "retired": return escape != null && campaign.HasAuthority && phase == CampaignPhase.Escape;
                default: return false;
            }
        }

        public void Execute(string id)
        {
            if (id == "details") { detailsPanel.SetActive(!detailsPanel.activeSelf); return; }
            if (!CanExecute(id)) return;
            if (id == "person") { selected = (selected + 1) % diagnostics.Rewards.State.Segments.Count; Refresh(); return; }
            if (id == "row") { selected = (selected + 1) % escape.InputRows; Refresh(); return; }
            if (id == "server") { serverIndex = (serverIndex + 1) % Players().Count; Refresh(); return; }
            if (id == "lost") { waiting.PreviewLostCount = (waiting.PreviewLostCount + 1) % 4; Refresh(); return; }
            if (id == "backpack") { waiting.PreviewBackpack = !waiting.PreviewBackpack; Refresh(); return; }

            var before = Snapshot();
            var previousResult = diagnostics.Rewards.Latest;
            var personBefore = Person();
            bool applied = false;
            bool query = false;
            bool dummy = false;
            string title = "";
            string summary = "";
            string api = "";
            switch (id)
            {
                case "advance": title = "흐름 한 단계 진행"; api = "028 공개 진행 API / 공연 종료는 031 경유"; applied = diagnostics.AdvanceOffline(); break;
                case "ready": title = "밤 준비 입력"; api = "028 CampaignRun.SetReady"; applied = diagnostics.Invoke("밤 준비 → SetReady", () => diagnostics.Campaign.SetReady(!diagnostics.Campaign.IsLocalReady)); break;
                case "start": title = "오프라인 런 시작"; api = "028 StartOfflineRun → EndIntro"; applied = performance.StartOfflineRun(); break;
                case "intro": title = "도입 종료"; api = "028 EndIntro"; applied = diagnostics.Invoke(api, diagnostics.Campaign.EndIntro); break;
                case "begin": title = "공연 명부 고정"; api = "031 BeginPerformance / BeginOfflinePerformance"; applied = performance.BeginPerformance(); break;
                case "arrive": title = $"자리 {selected + 1} 도착"; api = "더미 도착 입력 → 031 FinishPerformance → 028"; applied = performance.Arrive(SelectedParticipant); break;
                case "cut": title = $"자리 {selected + 1} 절단"; api = "030 신체 규칙 → 031 ObserveParticipant"; applied = performance.Cut(SelectedParticipant); break;
                case "kill": title = $"자리 {selected + 1} 사망"; api = "030/031 생존 관찰 → 결과 판정"; applied = performance.Kill(SelectedParticipant); break;
                case "cheer": title = $"자리 {selected + 1} 환호"; api = "031 AddCheer (정산 전 입력)"; applied = performance.Cheer(SelectedParticipant); break;
                case "timeout": title = "공연 제한 시간 종료"; api = "031 FinishPerformance(false) → 028"; applied = performance.TimeExpired(); break;
                case "spend": title = $"공동 자금 {waiting.SpendAmount} 지출"; api = "031 TrySpend / 물품·유료 치료 미연결"; applied = waiting.Spend(); summary = applied ? "지출만 반영했습니다. 상품 지급·치료 완료가 아닙니다." : "잔액 또는 권한을 확인하세요. 지출은 반영되지 않았습니다."; break;
                case "leave": title = "대기실 출구 입력"; api = "028 NotifyLeftWaitingRoom / 구역은 더미 입력"; applied = exploration != null ? exploration.Leave() : waiting.Leave(); break;
                case "room": title = "대기실 구역 입력"; api = "더미 구역 → A/B 안전 조건 조회"; exploration.EnterWaitingRoom(); applied = exploration.Zone == "대기실"; dummy = true; break;
                case "stage": title = "무대 입구 + 전원 집결 가정"; api = "더미 구역 → 028 NotifyAllAtStageEntrance"; applied = exploration.EnterStage(); break;
                case "expire": title = "탐색 타이머 만료 요청"; api = "028 DebugSetRemaining(0) → 다음 Tick"; applied = exploration.Expire(); summary = "요청 후 다음 Tick에서 현행 전환을 확인하세요. 추격 정리 대기는 미연결입니다."; break;
                case "restorePreview": title = "신체 복구·소지 칸 조회"; api = "030 PlayerCondition.NextRestore → SurvivalRules.ItemSlots"; query = true; applied = true; summary = $"손실 {waiting.PreviewLostCount} → 복구 {waiting.PreviewRestorePart}\n배낭 {(waiting.PreviewBackpack ? "있음" : "없음")} → 소지 {waiting.PreviewSlots}칸\n실제 신체·인벤토리·자금은 바뀌지 않습니다."; diagnostics.Note("규칙 미리보기", title, summary); break;
                case "catchPreview": title = "탐색 잡힘 규칙 조회"; api = "030 SurvivalRules.ResolveCatch / 실제 추격 아님"; query = true; applied = true; summary = $"조회 손실 {GameSettings.Exploration.DummyLostCount} → {exploration.Preview()}\n실제 신체 변경·끌려가기·무대 이동이 아닙니다."; break;
                case "restore": title = "서버 신체 1부위 복구"; api = "030 NetworkPlayer.ServerRestorePart / 지출·일정 분리"; applied = diagnostics.RestorePlayer(ServerPlayer()); break;
                case "catch": title = "서버 잡힘 적용"; api = "030 NetworkPlayer.ServerCatch / 현재 장소 일치 확인"; applied = diagnostics.CatchPlayer(ServerPlayer(), escape != null ? SurvivalPlace.EscapeRoute : SurvivalPlace.Exploration); break;
                case "escape": title = "탈출 개시 · 전원 집결 가정"; api = "028 StartEscape / 자동 집결 감지 아님"; applied = escape.StartEscape(); break;
                case "rowArrive": case "rowRetire": title = $"행 {selected + 1} {(id == "rowArrive" ? "도착" : "리타이어")}"; api = "더미 행 입력 / 자동 집계·종료 없음"; applied = escape.SetRow(selected, id == "rowArrive"); dummy = true; break;
                case "success": case "retired": title = id == "success" ? "수동 결론 · 도착 있음" : "수동 결론 · 전원 리타이어"; api = "028 EndEscape(bool) → 엔딩 / 세대 교체"; applied = escape.Finish(id == "success"); break;
            }
            ExecutionCount++;
            LastAction = id;
            resultTitle.text = title;
            resultBadge.text = query ? "조회 · 미리보기" : !applied ? "거절 · 변화 없음" : dummy ? "더미 입력 반영" : "공통 API 반영";
            resultBadge.color = query || dummy ? Amber : applied ? Teal : Red;
            if (string.IsNullOrEmpty(summary)) summary = applied ? "입력이 반영되었습니다. 아래 변경값과 현재 상태를 확인하세요." : "조건 불충족 또는 중복 입력입니다. 현재 단계·권한·대상을 확인하세요.";
            if (performance != null && diagnostics.Rewards.Latest != null && diagnostics.Rewards.Latest != previousResult
                && (id == "arrive" || id == "kill" || id == "cut" || id == "timeout"))
            {
                var r = diagnostics.Rewards.Latest;
                summary = $"공연 {(r.Succeeded ? "성공" : "실패")} · {r.EndReason}\n실패 사유 {r.Failure} · 이번 수입 {r.Income}";
                resultBadge.text = r.Succeeded ? "공연 성공 · 결과 확정" : "공연 실패 · 결과 확정";
                resultBadge.color = r.Succeeded ? Teal : Red;
            }
            if (applied && performance != null && personBefore != null && diagnostics.Rewards.Latest == previousResult)
            {
                var after = Person();
                if (id == "cut" || id == "kill")
                    summary = $"자리 {selected + 1}: 손실 {personBefore.LostCount} → {after.LostCount} · {(after.Dead ? "사망" : "생존")}\n공연이 끝나기 전까지 시간과 다른 참가자의 상황은 계속 진행됩니다.";
                if (id == "arrive") summary = $"자리 {selected + 1} 도착 입력. 다른 생존 참가자의 도착을 기다립니다.";
                if (id == "cheer") summary = $"자리 {selected + 1} 호응 {personBefore.Amount} → {after.Amount}\n공연 종료 때 031이 정산합니다.";
            }
            if (applied && ((id == "arrive" && diagnostics.Rewards.Latest == previousResult) || dummy || id == "spend" || id == "restorePreview" || id == "catchPreview" || id == "ready"))
                diagnostics.Story($"{title}{(query ? " · 조회만, 실제 상태 변화 없음" : dummy ? " · 더미 입력" : "")}");
            resultBody.text = summary;
            changes.text = Difference(before, Snapshot());
            connection.text = api;
            Refresh();
        }

        Dictionary<string, string> Snapshot()
        {
            var c = diagnostics.Campaign.State;
            var values = new Dictionary<string, string>
            {
                ["시간대"] = PhaseName(c.Phase), ["세대 / 일차"] = $"{c.Generation} / {c.Day}",
                ["공동 자금"] = diagnostics.Rewards.Balance.ToString(), ["타이머"] = $"{c.Timer} / {c.TimeRemaining:F1}초",
                ["밤 준비"] = $"{c.ReadyCount} / {c.RequiredCount}"
            };
            if (performance != null && Person() != null)
            {
                var p = Person();
                values["선택 참가자"] = p.ParticipantId;
                values["참가 상태"] = $"손실 {p.LostCount} / {(p.Dead ? "사망" : performance.HasArrived(p.ParticipantId) ? "도착" : "진행")} / 호응 {p.Amount}";
            }
            if (exploration != null) values["더미 구역"] = $"{exploration.Zone} / {(exploration.IsSafe ? "안전" : "안전 밖")}";
            if (escape != null) values["선택 행"] = escape.RowState(selected);
            var player = ServerPlayer();
            if (player != null) values["실제 서버 대상"] = $"{player.OwnerClientId}: {player.State} / {player.LostParts}";
            return values;
        }

        static string Difference(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            var text = new StringBuilder();
            foreach (var pair in after)
                if (!before.TryGetValue(pair.Key, out string old) || old != pair.Value)
                    text.AppendLine($"{pair.Key}    {old ?? "없음"}  →  {pair.Value}");
            return text.Length > 0 ? text.ToString().TrimEnd() : "관찰한 공통 변수 변화 없음\n조회 결과·세부 입력은 위 결과와 연결 기록을 확인하세요.";
        }

        public static string PhaseName(CampaignPhase phase) => phase switch
        {
            CampaignPhase.Intro => "도입", CampaignPhase.Performance => "공연", CampaignPhase.RestTime => "쉬는 시간",
            CampaignPhase.Night => "밤 정비", CampaignPhase.Escape => "탈출", CampaignPhase.Ending => "엔딩", _ => "런 시작 전"
        };

        public void Refresh()
        {
            if (diagnostics == null) return;
            var c = diagnostics.Campaign.State;
            status.text = $"세대 {c.Generation}   /   {c.Day}일차   ·   {PhaseName(c.Phase)}          공동 자금  {diagnostics.Rewards.Balance}";
            RefreshLive(c);
            steps.text = "";
            foreach (var phase in new[] { CampaignPhase.Intro, CampaignPhase.Performance, CampaignPhase.RestTime, CampaignPhase.Night, CampaignPhase.Escape, CampaignPhase.Ending })
                steps.text += (steps.text.Length == 0 ? "" : "   /   ") + (phase == c.Phase ? $"<color=#52D6BF><b>[ {PhaseName(phase)} ]</b></color>" : PhaseName(phase));
            var sb = new StringBuilder();
            if (performance != null)
            {
                var list = diagnostics.Rewards.State?.Segments;
                if (list == null || list.Count == 0) sb.Append("아직 명부가 없습니다.\n런 시작 후 공연 명부를 고정하세요.");
                else foreach (var p in list)
                    sb.AppendLine($"{(p.ParticipantId == SelectedParticipant ? "▶" : "·")} 자리 {p.Slot + 1}   {(p.Dead ? "사망" : !p.Connected ? "이탈" : performance.HasArrived(p.ParticipantId) ? "도착" : "진행")}   손실 {p.LostCount}   호응 {p.Amount}");
            }
            if (waiting != null) sb.Append($"복구 조회 손실  {waiting.PreviewLostCount}\n배낭 입력  {(waiting.PreviewBackpack ? "있음" : "없음")}   /   조회 {waiting.PreviewSlots}칸\n더미 지출 요청액  {waiting.SpendAmount}");
            if (exploration != null) sb.Append($"현재 더미 구역  {exploration.Zone}\n안전 조건  {(exploration.IsSafe ? "안전" : "안전 밖")}   /   방식 {GameSettings.Base.Run.RestTimerMode}\n잡힘 조회 손실  {GameSettings.Exploration.DummyLostCount}");
            if (escape != null)
            {
                selected = Mathf.Clamp(selected, 0, escape.InputRows - 1);
                for (int i = 0; i < escape.InputRows; i++) sb.AppendLine($"{(selected == i ? "▶" : "·")} 행 {i + 1}   {escape.RowState(i)}");
            }
            selection.text = sb.ToString().TrimEnd();
            var player = ServerPlayer();
            condition.text = $"{(c.Phase == CampaignPhase.Night ? $"밤 준비 {c.ReadyCount}/{c.RequiredCount} · 준비 버튼 사용" : "비활성 버튼은 현재 단계에서 사용할 수 없습니다.")}\n실제 서버 대상: {(player == null ? "없음 · 규칙 조회만 가능" : $"{player.OwnerClientId} / {player.State} / {player.LostParts}")}";
            if (exploration != null) condition.text += "\n추격 정리 대기: 미연결 · 현행 만료는 즉시 공연";
            foreach (var item in actions) item.button.interactable = CanExecute(item.id);
            var entries = diagnostics.StoryText.Split('\n');
            Array.Reverse(entries);
            history.text = string.Join("\n", entries);
            details.text = $"권한 {diagnostics.Campaign.HasAuthority} / 오프라인 {diagnostics.Campaign.IsOfflineRun} / 031 전환 대기 {diagnostics.Rewards.TransitionPending}\n{diagnostics.Variables()}\n\n{Boundary()}\n\n최근 API 기록\n{diagnostics.LastNote}\n\n독립 씬은 Play 종료 후 직접 여세요. 씬 전환·세션/전달/장소 설정은 032, 이동은 045 소유입니다.";
            details.text += $"\n\n원시 API 연결 기록\n{diagnostics.TraceText}";
            var latest = diagnostics.Rewards.Latest;
            if (latest != null)
            {
                details.text += $"\n\n031 최근 확정 결과: {(latest.Succeeded ? "성공" : "실패")} / {latest.EndReason} / {latest.Failure} / 수입 {latest.Income}";
                foreach (var p in latest.Participants)
                    details.text += $"\n자리 {p.Slot + 1}: {p.Judgement}, 손실 {p.StartingLostCount} → {p.EndingLostCount}, 정산 {p.Settlement}";
            }
            if (preview != null) preview.Refresh(selected, performance, waiting, exploration, escape);
        }

        static string ClockText(float remaining)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0, remaining));
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        void RefreshLive(CampaignSnapshot state)
        {
            string heading;
            string timer = "--:--";
            float ratio = 0;
            if (state.Phase == CampaignPhase.RestTime)
            {
                float limit = state.Rest == RestKind.Guide ? GameSettings.Base.Run.GuideRestDuration : GameSettings.Base.Run.RestDuration;
                timer = ClockText(state.TimeRemaining);
                ratio = limit > 0 ? state.TimeRemaining / limit : 0;
                heading = state.Timer == RestTimerState.WaitingForExit
                    ? "탐색 제한 시간 · 첫 출구 입력 대기 (아직 감소하지 않음)"
                    : state.Timer == RestTimerState.Running ? $"탐색 제한 시간 · 진행 중{(state.AlarmRaised ? " · 소집 경보" : "")}"
                    : "탐색 제한 시간 · 정지";
            }
            else if (state.Phase == CampaignPhase.Performance && performance != null)
            {
                heading = $"더미 공연 시계 · {performance.ClockStatus}\n줄타기 제한 시간 참조 · 오프라인 검증용";
                if (performance.PerformanceTimeLimit > 0)
                {
                    timer = ClockText(performance.PerformanceTimeRemaining);
                    ratio = performance.PerformanceTimeRemaining / performance.PerformanceTimeLimit;
                }
            }
            else heading = state.Phase switch
            {
                CampaignPhase.Night => $"제한 시간 없음 · 밤 준비 {state.ReadyCount}/{state.RequiredCount}",
                CampaignPhase.Performance => "공연 진행 · 이 공간에는 더미 공연 시계 없음",
                CampaignPhase.Escape => "자동 제한 시간 없음 · 도착/리타이어와 수동 결론 입력",
                CampaignPhase.Intro => "도입 진행 · 도입 종료 입력 대기",
                CampaignPhase.Ending => "탈출 성공 · 엔딩",
                _ => "런 시작 대기 · 왼쪽 흐름 준비에서 시작하세요"
            };
            live.text = $"{heading}\n{diagnostics.CurrentStory}";
            clock.text = timer;
            clockFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(ratio), 1, 1);
            clock.color = ratio > 0 && ratio <= 0.2f ? Red : Teal;
            clockFill.color = clock.color;
        }

        string Boundary()
        {
            if (waiting != null) return "지출 ≠ 상품 지급/유료 치료. 복구와 지출은 별도 입력.\n의료 방문 1·3일차 밤은 설명만 제공하며 가격/일정은 미연결.";
            if (exploration != null) return "탐색 만료 후 추격 정리 대기: 미연결. 현행 028은 만료 시 즉시 공연 전환.\n구역은 수동 입력, 이동·충돌·전원 감지 없음. 잡힘 조회 ≠ 실제 신체 변경.";
            if (escape != null) return "행 입력 ≠ 실제 참가자 상태. 도착/전원 리타이어 자동 집계 없음.\nEndEscape(bool)는 행과 독립된 수동 결론. 잡힘 규칙: " + SurvivalRules.ResolveCatch(SurvivalPlace.EscapeRoute, 0);
            return "Dummy-0/1은 보상 명부이며 실제 NetworkPlayer 신체가 아닙니다.\n결과/정산은 031, 시간대 전환은 028 소유입니다.";
        }
    }
}
