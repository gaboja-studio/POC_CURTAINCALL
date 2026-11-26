using CurtainCall.WorldIndicators;
using UnityEngine;

namespace CurtainCall.Settings
{
    [CreateAssetMenu(menuName = "CurtainCall/Settings/World UI", fileName = "WorldUiSettings")]
    public sealed class WorldUiSettings : ScriptableObject
    {
        [Header("월드 위험 표시 · 공통")]
        [Tooltip("공격·장애물·대포 표시를 켭니다. 피해 판정에는 영향을 주지 않습니다.")] public bool enabled = true;
        [Tooltip("월드 UI의 스프라이트와 공유 머티리얼 키트.")] public WorldIndicatorStyle style;
        public AreaIndicatorView circlePrefab, rectanglePrefab;
        public SweptVolumeIndicatorView volumePrefab;
        public PathIndicatorView pathPrefab;
        public TrajectoryIndicatorView trajectoryPrefab;
        public TargetIndicatorView targetPrefab;
        [Min(1), Tooltip("카메라에서 이 거리(m) 밖의 표시를 숨깁니다.")] public float maxDistance = 80;
        [Min(.001f), Tooltip("위험 경계선 두께(m). 실제 피격 범위는 원본 시스템에서 읽습니다.")] public float lineWidth = .045f;
        [Range(0, .5f)] public float fillOpacity = .10f;
        [Min(.001f)] public float surfaceOffset = .035f;
        [Range(.02f,.5f), Tooltip("고정 장애물 안내의 지지면 재확인 간격(초). 경로가 움직이면 즉시 갱신합니다.")] public float surfaceRefreshSeconds = .1f;
        [Min(.05f)] public float hatchSpacing = .36f;
        [Min(.01f)] public float hatchDepth = .15f;
        [Range(24, 128)] public int circleSegments = 64;
        public Color danger = new Color(.878f, .424f, .471f, .95f);
        public Color ivory = new Color(.941f, .933f, .863f, .95f);
        public Color progressColor = new Color(.949f, .706f, .357f, 1);
        [Header("진행 바 · 경로")]
        [Min(.005f)] public float progressWidth = .055f;
        [Min(.01f)] public float progressOffset = .11f;
        [Min(.02f)] public float dashLength = .40f;
        [Min(.02f)] public float dashGap = .25f;
        [Min(.03f)] public float arrowSize = .28f;
        [Header("그랩 · 대상 추적과 위치 확정")]
        [Tooltip("기본은 표적만 표시합니다. 켜면 위치 확정 뒤 선딜/발사 때만 공중 통로를 추가합니다.")] public bool showGrabPath;
        [Min(.1f), Tooltip("표적 브래킷의 월드 크기(m). 공격 반지름을 뜻하지 않습니다.")] public float grabTargetSize = .65f;
        [Header("탈출로 추격자 · 화면 위험 테두리")]
        public bool escapeThreatEnabled = true;
        [Min(.1f), Tooltip("대상 지정과 별도로 화면 테두리 효과가 시작되는 추격자와의 거리(m).")]
        public float escapeThreatDistance = 18;
        [Header("대포 · 짧은 출발 궤적")]
        [Min(.2f), Tooltip("포구부터 곡선을 따라 잰 최대 길이(m). 착지점까지의 전체 예측이 아닙니다.")] public float cannonArcLength = 8;
        [Range(.01f, .15f)] public float cannonStepSeconds = .035f;
        [Range(32, 512)] public int cannonMaxSamples = 256;
        public bool cannonForSpectator = true;
        [Header("캐릭터 그림자")]
        [Tooltip("캐릭터 모델의 Realtime 그림자 생성만 끕니다. 기존 착지 원형·그림자 받기는 유지합니다.")] public bool disablePlayerRealtimeShadows = true;
        [Tooltip("추격자 몸체 Realtime 그림자를 단일 원형 그림자로 교체합니다. 비용 비교 후 선택합니다.")] public bool pursuerBlobShadows;
        [Min(.05f)] public float waitingPursuerShadowRadius = .8f, escapePursuerShadowRadius = 1.8f;
        [Range(0, 1)] public float shadowOpacity = .38f;
        [Range(.03f, .5f)] public float shadowRefreshSeconds = .1f;

        [Header("위험한 동료 표시 (2026-11-26 M3 결정: 화면 밖 = 가장자리 원형 아이콘, 화면 안 = 벽 너머 테두리 + 머리 위 아이콘)")]
        [InspectorName("위험한 동료 표시 사용"), Tooltip("내 화면에만 표시합니다. 매달림·방금 다침·낙하 부상인 동료가 대상입니다. 공연·탈출로·추격 공통.")]
        public bool teammateDangerEnabled = true;
        [InspectorName("표시 화면 프리팹"), Tooltip("TeammateDangerHudView가 붙은 화면 UI. 원형 아이콘 한 벌을 보이는 상태로 둡니다.")]
        public GameObject teammateDangerHud;
        [InspectorName("아이콘 · 매달림")] public Sprite dangerHangIcon;
        [InspectorName("아이콘 · 방금 다침(절단)")] public Sprite dangerCutIcon;
        [InspectorName("아이콘 · 낙하 부상")] public Sprite dangerFallIcon;
        [InspectorName("색 · 매달림")] public Color dangerHangColor = new(.93f, .27f, .22f, 1f);
        [InspectorName("색 · 방금 다침")] public Color dangerCutColor = new(1f, .55f, .16f, 1f);
        [InspectorName("색 · 낙하 부상")] public Color dangerFallColor = new(1f, .78f, .25f, 1f);
        [InspectorName("부상 표시 시간(초)"), Min(.5f), Tooltip("팔다리를 잃은 뒤 이 시간 동안 표시합니다. 원 테두리·몸 테두리가 이 시간에 맞춰 줄어듭니다.")]
        public float dangerInjurySeconds = 6f;
        [InspectorName("가장자리 원 크기(px, 1080p 기준)"), Min(16f)] public float dangerEdgeSize = 40f;
        [InspectorName("머리 위 아이콘 크기(px, 1080p 기준)"), Min(12f)] public float dangerHeadSize = 30f;
        [InspectorName("가장자리 여백(px)"), Min(0f), Tooltip("화면 위·양옆 끝에서 원 중심까지의 거리.")] public float dangerEdgeMargin = 48f;
        [InspectorName("아래 가장자리 여백(px)"), Min(0f), Tooltip("화면 아래 끝에서 원 중심까지의 거리. 균형 막대·슬롯 등 아래 HUD를 피한다.")] public float dangerEdgeBottomMargin = 175f;
        [InspectorName("머리 위 높이(m)"), Min(0f), Tooltip("보이는 몸(모델) 꼭대기 위로 아이콘을 띄우는 높이.")] public float dangerHeadOffset = .35f;
        [InspectorName("몸 테두리 두께(px)"), Range(1f, 8f)] public float dangerOutlinePixels = 3f;
        [InspectorName("화면 안으로 보는 범위"), Range(0f, .2f), Tooltip("화면 끝에서 이 비율 안쪽에 있어야 '화면에 보이는 동료'로 봅니다. 밖이면 가장자리 원으로 표시합니다.")]
        public float dangerOnScreenInset = .04f;
        [InspectorName("남은 시간 적을 때 깜빡임 시작 비율"), Range(0f, 1f)] public float dangerUrgentRatio = .3f;
    }
}
