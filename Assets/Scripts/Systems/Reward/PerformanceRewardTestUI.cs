using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Reward
{
    /// <summary>Reward 테스트 씬 전용. 저장 에셋을 바꾸지 않는 수치 override와 공개 진입점 버튼을 제공한다.</summary>
    public sealed class PerformanceRewardTestUI : MonoBehaviour
    {
        [SerializeField] RewardTable rewardTable;
        readonly Dictionary<string, int> dummyParts = new();
        readonly string[] inputs = { "10", "5", "100", "100", "10" };
        CampaignRun campaign;
        PerformanceRewards rewards;
        NetworkSessionManager session;
        Vector2 scroll;
        int dummyCount = 2;
        int difficulty = 1;
        string expense = "10";
        string message = "";

        void Start()
        {
            campaign = CampaignRun.EnsureExists();
            rewards = PerformanceRewards.EnsureExists();
            session = NetworkSessionManager.EnsureExists();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(360, 10, Mathf.Max(400, Screen.width - 370), Screen.height - 20), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            if (campaign == null || rewards == null || session == null)
            { GUILayout.Label("공개 진입점 연결 대기"); GUILayout.EndScrollView(); GUILayout.EndArea(); return; }
            GUILayout.Label($"공연 결과·보상·공동 자금 테스트 / Dev Mode: {DevMode.Enabled}");
            GUILayout.Label($"{CampaignRun.Describe(campaign.State)} / 세대 {campaign.State.Generation} · {campaign.State.Day}일차");
            GUILayout.Label($"권한: {(rewards.HasAuthority ? "호스트/오프라인" : "읽기 전용")} / 잔액 {rewards.Balance}");
            GUILayout.Label($"보상 에셋: {(rewardTable != null ? rewardTable.name : "없음(임시 기본값)")} / override: {rewards.HasRulesOverride}");
            if (rewards.TransitionPending) GUILayout.Label("결과 확정 완료, 상태 송신 후 시간대 전환 재시도 중");
            DrawCampaign();
            DrawPerformance();
            DrawRules();
            DrawResults();
            GUILayout.Label(message);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawCampaign()
        {
            var current = campaign.State;
            if (!current.IsRunning && session.ConnectionState == SessionConnectionState.Offline)
            {
                if (GUILayout.Button("오프라인 런 시작")) Report(campaign.StartOfflineRun(), "오프라인 런 시작");
            }
            if (current.Phase == CampaignPhase.Night && GUILayout.Button(campaign.IsLocalReady ? "준비 취소" : "밤 준비 완료"))
                Report(campaign.SetReady(!campaign.IsLocalReady), "준비 변경");
            GUI.enabled = campaign.HasAuthority;
            if (current.Phase == CampaignPhase.Intro && GUILayout.Button("도입 끝 → 공연")) Report(campaign.EndIntro(), "도입 종료");
            if (current.Phase == CampaignPhase.RestTime)
            {
                if (GUILayout.Button("전원 무대 입구 → 다음 공연")) Report(campaign.NotifyAllAtStageEntrance(), "무대 입구 집결");
                if (GUILayout.Button("쉬는 시간 만료(마지막 아침이면 세대 교체)")) Report(campaign.DebugSetRemaining(0), "타이머 만료");
                if (GUILayout.Button("탈출 개시")) Report(campaign.StartEscape(), "탈출 개시");
            }
            if (current.Phase == CampaignPhase.Escape && GUILayout.Button("전원 리타이어 → 새 세대")) Report(campaign.EndEscape(false), "세대 교체");
            if (campaign.IsOfflineRun && GUILayout.Button("오프라인 런 끝내기")) campaign.StopOfflineRun();
            GUI.enabled = true;
        }

        void DrawPerformance()
        {
            GUILayout.Space(6);
            GUI.enabled = rewards.HasAuthority;
            GUILayout.Label("난이도(새 공연에 적용)");
            difficulty = GUILayout.Toolbar(difficulty - 1, new[] { "1", "2", "3" }) + 1;
            if (campaign.IsOfflineRun)
            {
                GUILayout.Label("더미 참가자 수(새 공연에 적용)");
                dummyCount = GUILayout.Toolbar(dummyCount - 2, new[] { "2명", "3명", "4명" }) + 2;
            }
            GUI.enabled = rewards.HasAuthority && campaign.State.Phase == CampaignPhase.Performance;
            if (GUILayout.Button("공연 명부 고정·호응 시작"))
            {
                string previousId = rewards.State?.Performance?.Id;
                bool began;
                if (campaign.IsOfflineRun)
                {
                    var seeds = new List<RewardParticipantSeed>();
                    for (int i = 0; i < dummyCount; i++) seeds.Add(new RewardParticipantSeed($"Dummy-{i}", (ulong)i, i));
                    began = rewards.BeginOfflinePerformance(seeds, difficulty, rewardTable);
                }
                else began = rewards.BeginPerformance(difficulty, rewardTable);
                if (began && previousId != rewards.State?.Performance?.Id) dummyParts.Clear();
                if (began && rewards.State != null)
                    foreach (var person in rewards.State.Segments)
                        if (!dummyParts.ContainsKey(person.ParticipantId)) dummyParts.Add(person.ParticipantId, 0);
                Report(began, "공연 시작(같은 공연 재호출은 멱등)");
            }
            var state = rewards.State;
            GUI.enabled = rewards.HasAuthority && state != null && state.IsOpen;
            if (state != null)
            {
                // 버튼 실행 중 snapshot이 바뀌어도 현재 프레임은 이 불변 명부를 읽는다.
                foreach (var person in state.Segments)
                {
                    GUILayout.Label($"자리 {person.Slot + 1}: 호응 {person.Amount}/{state.Rules.Cap}, 손실 {person.StartingLostCount}→{person.LostCount}, {(person.Dead ? "사망" : person.Connected ? "생존" : "이탈")}");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("환호 +")) Report(rewards.AddCheer(person.ParticipantId), "환호");
                    if (GUILayout.Button("야유 −")) Report(rewards.AddCheer(person.ParticipantId, false), "야유");
                    if (GUILayout.Button("1곳 절단")) ChangePerson(person.ParticipantId, person.ClientId, false, false);
                    if (GUILayout.Button("사망")) ChangePerson(person.ParticipantId, person.ClientId, true, false);
                    if (campaign.IsOfflineRun && GUILayout.Button("이탈")) ChangePerson(person.ParticipantId, person.ClientId, false, true);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("묘기 성공으로 종료·정산")) Report(rewards.FinishPerformance(true, "Completed"), "성공 종료 요청");
            if (GUILayout.Button("제한 시간 실패로 종료·정산")) Report(rewards.FinishPerformance(false, "TimeExpired"), "실패 종료 요청");
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        void ChangePerson(string id, ulong clientId, bool dead, bool disconnected)
        {
            if (!campaign.IsOfflineRun)
            {
                foreach (var player in NetworkPlayer.All)
                {
                    if (player.OwnerClientId != clientId) continue;
                    if (dead) player.ServerKill(); else player.ServerCut();
                    return;
                }
                return;
            }
            dummyParts.TryGetValue(id, out int parts);
            if (!dead && !disconnected && parts != 15)
                for (int bit = 1; bit <= 8; bit <<= 1) if ((parts & bit) == 0) { parts |= bit; break; }
            dummyParts[id] = parts;
            Report(rewards.ObserveParticipant(id, parts, dead, !disconnected), "참가자 상태 관찰");
        }

        void DrawRules()
        {
            GUILayout.Space(6);
            GUILayout.Label("검증용 수치: 환호 / 야유 감소량 / 상한 / 완주 보너스 / 절단 보너스");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < inputs.Length; i++) inputs[i] = GUILayout.TextField(inputs[i], GUILayout.MinWidth(45));
            GUILayout.EndHorizontal();
            GUI.enabled = rewards.HasAuthority;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("런타임 override 적용(에셋 저장 안 함)"))
            {
                var values = new int[5];
                bool valid = true;
                for (int i = 0; i < values.Length; i++) valid &= int.TryParse(inputs[i], out values[i]) && values[i] >= 0;
                if (valid) Report(rewards.SetRulesOverride(new RewardRules(values[0], values[1], values[2], values[3], values[4])), "수치 적용");
                else message = "수치는 0 이상 정수여야 합니다.";
            }
            if (GUILayout.Button("에셋 값으로 복귀")) Report(rewards.SetRulesOverride(null), "override 해제");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            expense = GUILayout.TextField(expense);
            if (GUILayout.Button("테스트 지출"))
                Report(long.TryParse(expense, out long value) && rewards.TrySpend(value, "테스트 지출"), "공동 자금 지출");
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        void DrawResults()
        {
            var result = rewards.Latest;
            if (result != null)
            {
                GUILayout.Label($"최근 결과: {(result.Succeeded ? "성공" : "실패")} / {result.EndReason} / {result.Failure} / 수입 {result.Income} (호응 {result.CheerReward} + 완주 {result.CompletionReward})");
                foreach (var person in result.Participants)
                    GUILayout.Label($"자리 {person.Slot + 1}: {person.Judgement}, 손실 {person.StartingLostCount}→{person.EndingLostCount}, 정산 {person.Settlement}");
            }
            var page = rewards.LedgerPage;
            if (page == null) return;
            GUILayout.Label($"장부 {page.LedgerStart + 1}~{page.LedgerStart + page.Ledger.Count} / 총 {page.LedgerCount}건, revision {page.Revision}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("이전 장부")) rewards.RequestLedgerPage(Math.Max(0, page.LedgerStart - 20));
            if (GUILayout.Button("최신 장부")) rewards.RequestLedgerPage(Math.Max(0, page.LedgerCount - 20));
            GUILayout.EndHorizontal();
            foreach (var entry in page.Ledger)
                GUILayout.Label($"#{entry.Number} 세대 {entry.Generation} {entry.Day}일차 {entry.Description}: {entry.Amount:+0;-0;0} → {entry.Balance}");
        }

        void Report(bool success, string operation) => message = $"{operation}: {(success ? "적용" : "거절/변화 없음")}";
    }
}
