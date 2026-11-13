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
        [Header("대기실 낙하 안전")]
        [SerializeField, InspectorName("대기실 전체 착지 피해 면제")]
        [Tooltip("켜면 대기실·기본 대기실·추격 대기실에서 바닥이나 발판에 착지할 때 낙하 높이로 인한 부상·사망을 받지 않는다. 최초 튜토리얼의 바닥 추락 후 출발점 복귀, 위험물 피해, 월드 Y 사망선은 유지한다. 기본 켬.")]
        bool safeFallLandings = true;
        public bool SafeFallLandings => safeFallLandings;

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

        [Header("최초 튜토리얼 공통 규칙")]
        [SerializeField] TutorialSettings tutorial = new();
        public TutorialSettings Tutorial => tutorial ??= new();

        [SerializeField, InspectorName("탐색 열쇠 사용"), Tooltip("획득·장착·사용과 열쇠 동작을 켠다. 꺼져 있으면 비밀번호·협동 조건과 머리핀 도구는 계속 적용한다.")]
        bool enableKeyInteractions;
        public bool EnableKeyInteractions => enableKeyInteractions;

        [SerializeField, InspectorName("탐색 공연 토큰 금액"), Tooltip("상자에서 나온 공연 토큰을 주웠을 때 공동 자금에 더하는 금액(원). 2026-11-12 사용자: 50원."), Min(0)]
        int explorationTokenFunds = 50;
        public int ExplorationTokenFunds => explorationTokenFunds;
        [SerializeField, InspectorName("탐색 공연 토큰 금액(작은 것)"), Min(0), Tooltip("상자에서 나오는 작은 공연 토큰 금액(원). 2026-11-13 사용자: 20원.")]
        int explorationTokenFundsSmall = 20;
        public int ExplorationTokenFundsSmall => explorationTokenFundsSmall;
        [SerializeField, InspectorName("탐색 공연 토큰 금액(큰 것)"), Min(0), Tooltip("상자·물병에서 나오는 큰 공연 토큰 금액(원). 2026-11-13 사용자: 100원.")]
        int explorationTokenFundsLarge = 100;
        public int ExplorationTokenFundsLarge => explorationTokenFundsLarge;

        [SerializeField, InspectorName("대기실 트램펄린 운반 판정 지름"), Min(0f), Tooltip("대기실에서 트램펄린을 들고 갈 때 벽·설비에 걸리는지 보는 판정의 지름(m). 공연 공통 판정(지름 약 4.95m)은 좁은 대기실에서 옮길 수 없어 대기실에서만 줄인다(2026-11-13 사용자). 0이면 공연과 같은 판정.")]
        float trampolineCarryCheckDiameter = 1f;
        public float TrampolineCarryCheckDiameter => Mathf.Max(0f, trampolineCarryCheckDiameter);

        [Header("재연습 공통 규칙")]
        [SerializeField] RehearsalSettings rehearsal = new();
        public RehearsalSettings Rehearsal => rehearsal ??= new();

        [Serializable]
        public sealed class RehearsalSettings
        {
            [SerializeField, InspectorName("재연습 사망 후 자동 복귀")]
            [Tooltip("외줄 재연습은 해당 소구간 출발대로 복귀한다. 인간대포 재연습은 마지막으로 안전하게 서 있던 위치와 당시 신체 상태로 복귀한다. 일반 공연에는 적용하지 않는다.")]
            bool recoverAfterDeath = true;
            [SerializeField, Min(0f), InspectorName("재연습 사망 복귀 대기 시간")]
            [Tooltip("재연습 중 사망 후 체크포인트로 돌아오기까지 기다리는 시간(초). 기본 1.5초.")]
            float recoverSeconds = 1.5f;
            public bool RecoverAfterDeath => recoverAfterDeath;
            public float RecoverSeconds => Mathf.Max(0f, recoverSeconds);
        }

        [System.Serializable]
        public sealed class TutorialSettings
        {
            [SerializeField, InspectorName("연습 코스 아래 바닥에 떨어지면 재시작")]
            [Tooltip("최초 외줄 튜토리얼에서 코스 아래 실제 바닥에 착지하면 높이와 무관하게 사망 후 해당 소구간 출발점으로 복귀한다. 출발대·도착대·외줄 착지는 제외. 첫 공연과 이후 재연습에는 적용하지 않는다. 기본 켬.")]
            bool resetOnFloorFall = true;
            [SerializeField, Min(0f), InspectorName("튜토리얼 사망 복귀 대기 시간")]
            [Tooltip("최초 튜토리얼에서 사망 후 온전한 몸으로 출발점에 돌아오기까지 기다리는 시간(초). 교육 완료 후 첫 공연으로 이동 중인 사망에도 적용. 기본 1.5초.")]
            float recoverSeconds = 1.5f;
            [SerializeField, InspectorName("연습실에 사람이 있으면 튜토리얼 문 열기")]
            [Tooltip("최초 튜토리얼 진행 중·완료 후 연습실 안에 사람이 있으면 문을 연다. 모두 나가면 닫고 사망 복귀 등으로 다시 들어오면 재개방한다. 첫 공연 이후의 일반 문 규칙에는 영향을 주지 않는다. 기본 켬.")]
            bool openPracticeDoorWhileOccupied = true;
            [SerializeField, Min(1f), InspectorName("튜토리얼 쇠창살 높이")]
            [Tooltip("튜토리얼 동선에서 닫힌 쇠창살의 최소 높이(m). 기본 5.5m.")]
            float lockdownGateHeight = 5.5f;
            [SerializeField, Min(1f), InspectorName("튜토리얼 쇠창살 투명벽 높이")]
            [Tooltip("잠긴 튜토리얼 통로를 목마나 점프로 넘지 못하도록 막는 투명 충돌체 높이(m). 기본 12m.")]
            float lockdownBarrierHeight = 12f;
            [SerializeField, InspectorName("인간대포 연습 중 출구 잠금")]
            [Tooltip("인간대포 연습을 시작하면 연습실 출구를 닫고 종료 시 연다. 기본 켬.")]
            bool sealCannonPracticeExit = true;
            [SerializeField, InspectorName("인간대포 연습 중 연습실 바닥 추락 시 사망")]
            [Tooltip("연습 코스의 발판이 아닌 연습실 바닥에 떨어지면 사망한다. 기본 켬.")]
            bool cannonPracticeFloorFallIsFatal = true;
            [SerializeField, InspectorName("첫 공연 이동 전 사망자 복귀 기다리기")]
            [Tooltip("최초 튜토리얼을 끝낸 뒤 집결할 때 죽은 동료의 복귀를 기다린다. 복귀한 동료도 입구에 모여야 공연으로 이동한다. 기본 켬.")]
            bool waitForRecoveryBeforePerformance = true;
            [SerializeField, InspectorName("튜토리얼 도착 지점 투명 벽")]
            [Tooltip("켜면 최초 튜토리얼 도착대에서 되돌아가는 길을 막는다. 양 끝 진입·양방향 이동을 위해 기본 끔. 재연습에는 적용하지 않는다.")]
            bool keepFinishBarrier;
            [SerializeField, Min(0f), InspectorName("목마 연습 첫 그네 낮춤 높이")]
            [Tooltip("목마로 첫 그네를 잡는 외줄 연습에서 첫 봉을 공연 배치보다 낮추는 거리(m). 최초 튜토리얼과 같은 소구간 재연습에 적용. 기본 0.5m. 봉의 공통 크기·흔들림·잡기 거리·도약 수치는 유지한다.")]
            float firstTrapezeLowering = .5f;
            public bool ResetOnFloorFall => resetOnFloorFall;
            public float RecoverSeconds => Mathf.Max(0f, recoverSeconds);
            public bool OpenPracticeDoorWhileOccupied => openPracticeDoorWhileOccupied;
            public float LockdownGateHeight => Mathf.Max(1f, lockdownGateHeight);
            public float LockdownBarrierHeight => Mathf.Max(LockdownGateHeight, lockdownBarrierHeight);
            public bool SealCannonPracticeExit => sealCannonPracticeExit;
            public bool CannonPracticeFloorFallIsFatal => cannonPracticeFloorFallIsFatal;
            public bool WaitForRecoveryBeforePerformance => waitForRecoveryBeforePerformance;
            public bool KeepFinishBarrier => keepFinishBarrier;
            public float FirstTrapezeLowering => Mathf.Max(0f, firstTrapezeLowering);
        }

        public int DummySpendAmount => Mathf.Max(1, dummySpendAmount);
        public int BodyRestoreBasePrice => Mathf.Max(1, bodyRestoreBasePrice);
        public int BodyRestorePriceStep => Mathf.Max(0, bodyRestorePriceStep);
        public int BodyRestoreMaxParts => Mathf.Clamp(bodyRestoreMaxParts, 1, 4);
    }
}
