using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.SpaceLink
{
    /// <summary>
    /// 장소 전환 확인용 임시 화면(<see cref="SpaceLinkHub"/>가 붙인다, 씬이 바뀌어도 유지).
    /// 전환 방식·지금 장소·런 상태·공동 자금·내 잃은 팔다리를 보여 주고, 더미 공간에 없는 신호(런 시작·도입 끝·백스테이지로 나가기)를 버튼으로 낸다.
    /// 나머지 신호(공연 종료·무대 입구 집결·탈출 등)는 각 더미 공간 화면에서 낸다. 실제 공간 연결 뒤 지운다.
    /// </summary>
    public sealed class SpaceLinkTestUI : MonoBehaviour
    {
        const float Width = 360f;

        SpaceLinkDirector director;
        CampaignRun campaign;
        bool collapsed;

        void Start()
        {
            director = GetComponent<SpaceLinkDirector>();
            campaign = CampaignRun.EnsureExists();
        }

        void OnGUI()
        {
            if (director == null || campaign == null) return;

            var s = campaign.State;
            var settings = GameSettings.Base.SpaceTransition;
            GUILayout.BeginArea(new Rect((Screen.width - Width) * 0.5f, 10f, Width, collapsed ? 34f : 330f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"장소 전환 [{SpaceRoute.Describe(settings.Mode)}] · {SpaceRoute.Describe(director.Current)}{(director.IsTransitioning ? " (전환 중)" : "")}");
            if (GUILayout.Button(collapsed ? "펼치기" : "접기", GUILayout.Width(52f))) collapsed = !collapsed;
            GUILayout.EndHorizontal();
            if (collapsed)
            {
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"판정: {(campaign.IsOfflineRun ? "오프라인" : director.HasAuthority ? "호스트" : "받는 쪽")} · 세대 {s.Generation} · {s.Day}일차 · {CampaignRun.Describe(s)}");
            GUILayout.Label($"공동 자금: {(PerformanceRewards.Instance != null ? PerformanceRewards.Instance.Balance.ToString() : "-")} · 내 잃은 팔다리: {LocalLostLimbs()}");

            DrawButtons(s);

            GUILayout.Space(4);
            foreach (var line in director.Log)
                GUILayout.Label(line);
            GUILayout.EndArea();
        }

        void DrawButtons(in CampaignSnapshot s)
        {
            if (s.Phase == CampaignPhase.None && GUILayout.Button("오프라인 런 시작(접속 안 함)"))
                campaign.StartOfflineRun();
            if (s.Phase == CampaignPhase.Intro && director.HasAuthority && GUILayout.Button("도입 끝 → 공연"))
                campaign.EndIntro();
            if (s.Phase == CampaignPhase.RestTime && director.Current == SpaceId.WaitingRoom)
            {
                GUI.enabled = director.HasAuthority;
                if (GUILayout.Button(director.HasAuthority ? "백스테이지로 나가기" : "백스테이지로 나가기 (호스트만)"))
                    director.LeaveWaitingRoom();
                GUI.enabled = true;
            }
            if (campaign.IsOfflineRun && s.Phase == CampaignPhase.Ending && GUILayout.Button("오프라인 런 끝"))
                campaign.StopOfflineRun();
        }

        static string LocalLostLimbs()
        {
            var player = NetworkPlayer.Local;
            return player != null ? $"{PlayerCondition.CountLimbs(player.LostParts)}/4" : "캐릭터 없음";
        }
    }
}
