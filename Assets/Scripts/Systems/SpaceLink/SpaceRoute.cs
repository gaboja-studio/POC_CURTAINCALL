using CurtainCall.Campaign;
using CurtainCall.Settings;

namespace CurtainCall.SpaceLink
{
    /// <summary>런이 머무는 장소. 각 장소의 씬 이름은 공통 세팅 "장소 전환"에서 읽는다.</summary>
    public enum SpaceId
    {
        /// <summary>시작 씬(접속·런 시작, 도입·엔딩).</summary>
        Hub,
        Performance,
        WaitingRoom,
        Exploration,
        EscapeRoute,
    }

    /// <summary>
    /// 시간대 → 장소 대응(게임 흐름 기획 9장, Harness/Project/Facts/run-state.md).
    /// 쉬는 시간은 대기실에서 시작하고, 대기실에서 나가면 백스테이지(<see cref="SpaceId.Exploration"/>)로 간다.
    /// </summary>
    public static class SpaceRoute
    {
        /// <summary>시간대가 시작될 때 가야 할 장소.</summary>
        public static SpaceId ForPhase(CampaignPhase phase)
        {
            switch (phase)
            {
                case CampaignPhase.Performance: return SpaceId.Performance;
                case CampaignPhase.RestTime:
                case CampaignPhase.DemoEnding:
                case CampaignPhase.Dawn:
                case CampaignPhase.Night: return SpaceId.WaitingRoom;
                case CampaignPhase.Escape: return SpaceId.EscapeRoute;
                default: return SpaceId.Hub; // 런 전·도입·엔딩
            }
        }

        public static string SceneName(SpaceId space, BaseGameSettings.SpaceTransitionSettings settings, string trickId = null)
        {
            switch (space)
            {
                case SpaceId.Performance:
                    if (trickId == "Tightrope" && !string.IsNullOrWhiteSpace(settings.TightropeScene)) return settings.TightropeScene;
                    if (trickId == "HumanCannon" && !string.IsNullOrWhiteSpace(settings.HumanCannonScene)) return settings.HumanCannonScene;
                    return settings.PerformanceScene;
                case SpaceId.WaitingRoom: return settings.WaitingRoomScene;
                case SpaceId.Exploration: return settings.ExplorationScene;
                case SpaceId.EscapeRoute: return settings.EscapeRouteScene;
                default: return settings.HubScene;
            }
        }

        /// <summary>씬 이름에 해당하는 장소. 장소 씬이 아니면 false.</summary>
        public static bool TryFind(string sceneName, BaseGameSettings.SpaceTransitionSettings settings, out SpaceId space)
        {
            if (!string.IsNullOrWhiteSpace(sceneName)
                && (sceneName == settings.TightropeScene || sceneName == settings.HumanCannonScene))
            {
                space = SpaceId.Performance;
                return true;
            }
            for (var id = SpaceId.Hub; id <= SpaceId.EscapeRoute; id++)
            {
                if (SceneName(id, settings) != sceneName) continue;
                space = id;
                return true;
            }
            space = SpaceId.Hub;
            return false;
        }

        /// <summary>장소 이름(디버그 화면용).</summary>
        public static string Describe(SpaceId space)
        {
            switch (space)
            {
                case SpaceId.Performance: return "공연장";
                case SpaceId.WaitingRoom: return "대기실";
                case SpaceId.Exploration: return "백스테이지";
                case SpaceId.EscapeRoute: return "탈출로";
                default: return "시작 씬";
            }
        }

        public static string Describe(SpaceTransitionMode mode) => mode == SpaceTransitionMode.Additive ? "추가 로드" : "통째 교체";
    }
}
