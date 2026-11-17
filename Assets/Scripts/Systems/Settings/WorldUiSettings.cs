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
    }
}
