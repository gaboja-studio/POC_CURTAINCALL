using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 대기실 세팅(세팅 5묶음 중 "대기실"): 상점·배송구·소품실·서비스 전문가·밤 정비 등 대기실 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/WaitingRoomSettings.asset. <see cref="GameSettings.WaitingRoom"/>로 읽는다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Waiting Room Settings", fileName = "WaitingRoomSettings")]
    public sealed class WaitingRoomSettings : ScriptableObject
    {
        [Header("더미 검증 전용 — 정식 상품 가격 아님")]
        [SerializeField, Min(1)] int dummySpendAmount = 10;

        [Header("기물 구매 가능 시간")]
        [Tooltip("켜면 아침·낮·첫날 가이드 쉬는 시간에도 기물을 구매할 수 있습니다. 밤 정비에는 항상 구매할 수 있습니다.")]
        [SerializeField, InspectorName("쉬는 시간에도 기물 구매")] bool allowPurchasesDuringRest = true;
        public bool AllowPurchasesDuringRest => allowPurchasesDuringRest;

        [Header("쉬는 시간 · 알림과 추격 팀 임무")]
        [SerializeField, InspectorName("쉬는 시간 문구 설정")] BreakPresentationSettings restPresentation = new(false);
        [Header("정비 시간 · 알림과 추격 팀 임무")]
        [SerializeField, InspectorName("정비 시간 문구 설정")] BreakPresentationSettings maintenancePresentation = new(true);
        public BreakPresentationSettings RestPresentation => restPresentation ??= new(false);
        public BreakPresentationSettings MaintenancePresentation => maintenancePresentation ??= new(true);
        public BreakPresentationSettings Presentation(Campaign.CampaignPhase phase) => phase == Campaign.CampaignPhase.Night ? MaintenancePresentation : RestPresentation;

        [Serializable]
        public sealed class BreakNoticeSettings
        {
            [SerializeField, InspectorName("제목")] string title;
            [SerializeField, TextArea(2, 4), InspectorName("본문")] string body;
            public string Title => title ?? "";
            public string Body => body ?? "";
            public BreakNoticeSettings(string title, string body) { this.title = title; this.body = body; }
        }

        [Serializable]
        public sealed class BreakPresentationSettings
        {
            [SerializeField, InspectorName("1분 전 알림")] BreakNoticeSettings minute;
            [SerializeField, InspectorName("30초 전 알림")] BreakNoticeSettings thirtySeconds;
            [SerializeField, InspectorName("10초 전 알림")] BreakNoticeSettings tenSeconds;
            [SerializeField, InspectorName("추격 단계 알림 제목")] string pursuitAlertTitle = "추격 경보";
            [Tooltip("단계 순서대로 입력합니다. 빈 칸은 기존 추격 문구 표/단계 설정을 사용합니다.")]
            [SerializeField, InspectorName("추격 단계별 알림 본문")] string[] stageAnnouncements = new string[4];
            [SerializeField, InspectorName("추격 팀 임무 제목")] string questTitle;
            [Tooltip("{stage}, {escape}, {sleep}는 해당 이동/수면 조건이 가능할 때만 표시됩니다. 자유롭게 문장을 바꾸거나 줄을 추가할 수 있습니다.")]
            [SerializeField, TextArea(3, 7), InspectorName("추격 팀 임무 본문")] string questBody = "{stage}\n{escape}\n{sleep}";
            [SerializeField, TextArea(1, 3), InspectorName("공연장 이동 임무")] string stageObjective = "- 모든 플레이어가 공연장 입구로 가서 공연 진행";
            [SerializeField, TextArea(1, 3), InspectorName("탈출로 이동 임무")] string escapeObjective = "- 모든 플레이어가 탈출로로 진입해 탈출 시도";
            [SerializeField, TextArea(1, 3), InspectorName("수면 임무")] string sleepObjective;
            public BreakPresentationSettings(bool maintenance)
            {
                string period = maintenance ? "정비 시간" : "쉬는 시간";
                minute = new(period + " 1분 남음", period + "이 1분 남았습니다. 제한 시간이 끝나면 추격이 시작됩니다.");
                thirtySeconds = new(period + " 30초 남음", period + "이 30초 남았습니다. " + (maintenance ? "동료들과 숙소 침대에서 잠들거나 출구로 이동하세요." : "공연장 입구나 탈출로로 이동하세요."));
                tenSeconds = new(period + " 10초 남음", period + "이 {seconds}초 남았습니다. 곧 추격이 시작됩니다.");
                questTitle = maintenance ? "정비 종료 · 추격자를 피하세요" : "추격자를 피해 달아나세요.";
                sleepObjective = maintenance ? "- 살아 있는 모든 플레이어가 숙소 침대에서 수면" : "";
            }
            public BreakNoticeSettings Notice(int seconds) => seconds > 30 ? minute : seconds > 10 ? thirtySeconds : tenSeconds;
            public string PursuitAlertTitle => pursuitAlertTitle ?? "";
            public string StageAnnouncement(int index) => stageAnnouncements != null && index >= 0 && index < stageAnnouncements.Length ? stageAnnouncements[index] : "";
            public string QuestTitle => questTitle ?? "";
            public string QuestBody(bool stage, bool escape, bool sleep)
            {
                string body = (questBody ?? "").Replace("{stage}", stage ? stageObjective : "").Replace("{escape}", escape ? escapeObjective : "").Replace("{sleep}", sleep ? sleepObjective : "");
                return string.Join("\n", Array.FindAll(body.Replace("\r", "").Split('\n'), line => !string.IsNullOrWhiteSpace(line)));
            }
        }

        [Header("신체 복구")]
        [Tooltip("팔다리 1개당 기본 가격(토큰). 런(세대)이 새로 시작되면 이 값으로 돌아간다.")]
        [SerializeField, Min(1), InspectorName("최초 부위당 가격")] int bodyRestoreBasePrice = 50;
        [Tooltip("팀 전체가 한 번 이용할 때마다 오르는 다음 1개당 가격(토큰). 여러 개를 고쳐도 한 번만 오른다.")]
        [SerializeField, Min(0), InspectorName("이용 1회당 단가 상승")] int bodyRestorePriceStep = 25;
        [Tooltip("한 번에 복구할 수 있는 최대 팔다리 수.")]
        [SerializeField, Range(1, 4)] int bodyRestoreMaxParts = 3;

        [Header("추격 시스템 (M2)")]
        [SerializeField] PursuitSettings pursuit = new();
        public PursuitSettings Pursuit => pursuit ??= new();

        public int DummySpendAmount => Mathf.Max(1, dummySpendAmount);
        public int BodyRestoreBasePrice => Mathf.Max(1, bodyRestoreBasePrice);
        public int BodyRestorePriceStep => Mathf.Max(0, bodyRestorePriceStep);
        public int BodyRestoreMaxParts => Mathf.Clamp(bodyRestoreMaxParts, 1, 4);
    }
}
