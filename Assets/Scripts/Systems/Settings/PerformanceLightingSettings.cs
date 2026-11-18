using UnityEngine;
namespace CurtainCall.Settings
{
    [CreateAssetMenu(menuName="CurtainCall/Settings/Performance Lighting",fileName="PerformanceLightingSettings")]
    public sealed class PerformanceLightingSettings : ScriptableObject
    {
        [Header("공연장 전체 · 플레이 중 변경 즉시 반영")]
        [Tooltip("감광과 천장 추적등을 켭니다. 끄면 기존 조명으로 복원합니다.")] public bool enabled=true;
        [Range(0,1),Tooltip("기존 배경 작업등 밝기 배율. 0은 꺼짐, 1은 원래 밝기입니다.")] public float backgroundBrightness=.16f;
        [Range(0,1),Tooltip("공연 씬 방향광 밝기 배율. 높이면 객석·심부까지 밝아집니다.")] public float directionalBrightness=.045f;
        [Range(0,2),Tooltip("코스 고정 조명 밝기 배율. 로프·착지점이 어두우면 올립니다.")] public float courseBrightness=.04f;
        [Range(0,1),Tooltip("공연장 구조물 표면의 명도 배율. 객석과 전구는 제외합니다. 낮추면 철골의 시각적 밀도가 줄어듭니다.")] public float backgroundSurface=.4f;
        [Range(0,1),Tooltip("철골·벽 등 배경 재질의 자체 발광 배율. 조명을 낮춰도 배경이 밝으면 이 값을 줄입니다.")] public float backgroundEmission=.07f;
        [Range(0,1),Tooltip("객석 재질의 자체 발광 배율. 좌석 층의 윤곽을 어둠 속에 남깁니다.")] public float audienceEmission=.45f;
        [Min(0),Tooltip("공연 진행축으로부터 감광을 시작하는 거리(m). 높이 차이도 포함합니다.")] public float dimStartDistance=14;
        [Min(1),Tooltip("최대 감광에 도달하는 거리(m). 시작 거리보다 크게 설정합니다.")] public float dimEndDistance=110;
        [Range(0,1),Tooltip("먼 배경등에 남길 밝기 비율. 0이면 심부 작업등이 꺼집니다.")] public float distantBrightness=.12f;
        [Tooltip("상부 그림자의 환경광 색상.")] public Color ambientSky=new(.0025f,.0033f,.0055f);
        [Tooltip("수평 방향 그림자의 환경광 색상.")] public Color ambientEquator=new(.0018f,.002f,.0029f);
        [Tooltip("하부 그림자의 환경광 색상.")] public Color ambientGround=new(.0025f,.0013f,.0012f);
        [Tooltip("먼 배경과 카메라 바탕의 안개 색상.")] public Color fogColor=new(.009f,.010f,.018f);
        [Range(0,.02f),Tooltip("실제 게임 크기 기준 안개 밀도. 높으면 먼 구조가 빨리 사라집니다.")] public float fogDensity=.0037f;
        [Header("객석 · 공연장 존재감")]
        [Range(0,3),Tooltip("객석 전용 조명 배율. 배경 감광과 독립적으로 좌석을 드러냅니다.")] public float audienceBrightness=.75f;
        [Tooltip("객석 조명 색상. 채도를 낮추면 관객 윤곽이 잘 보입니다.")] public Color audienceColor=new(.65f,.20f,.16f);
        [Range(0,3),Tooltip("객석 난간의 작은 전구 밝기. 층별 곡선을 표시합니다.")] public float audienceBulbBrightness=.65f;
        [Range(0,3),Tooltip("철창·기계·천막의 국소 조명 밝기 배율.")] public float accentBrightness=.65f;
        [Header("공연 구역 간접 보조등 · 객석과 분리")]
        [Tooltip("공연 경로 양옆의 부드러운 보조등을 켭니다. 코스·기물·플레이어만 비추고 배경은 제외합니다.")] public bool indirectFill=true;
        [Range(0,3),Tooltip("공연 구역 보조등 밝기 배율. 로프·대포·발판이 어두우면 올립니다. 추적 스폿 밝기와 별개입니다.")] public float indirectBrightness=.55f;
        [Range(8,45),Tooltip("보조등 한 개가 도달하는 거리(m). 먼 배경은 별도 렌더링 레이어로 제외합니다.")] public float indirectRange=30;
        [Tooltip("공연 구역 보조등 색상. 채도가 낮은 따뜻한 백색을 권장합니다.")] public Color indirectColor=new(1,.91f,.81f);
        [Range(45,135),Tooltip("보조등이 퍼지는 바깥 각도(도). 이 등에는 가시 빛기둥이 없어 스폿 연출과 경쟁하지 않습니다.")] public float indirectAngle=110;
        [Range(.5f,1),Tooltip("결승에도 유지할 보조등 비율. 공연 가독성을 위해 절반 아래로 낮추지 않습니다.")] public float indirectFinaleBrightness=.85f;
        [Header("트랙 외곽 추적 스포트라이트")]
        [Tooltip("플레이어 추적등 사용. 꺼도 코스와 객석 조명은 유지됩니다.")] public bool followSpots=true;
        [Min(0),Tooltip("플레이어에 도달할 스폿 밝기 기준. 등기구와의 거리 제곱으로 광량을 보정합니다.")] public float performerIlluminance=5;
        [Min(0),Tooltip("외줄타기 추적 스폿의 밝기 배율. 공통 광량에 곱하며 인간 대포에는 영향을 주지 않습니다.")] public float tightropeSpotBrightness=1.6f;
        [Min(0),Tooltip("인간 대포 추적 스폿의 밝기 배율. 1이면 기존 광량을 유지합니다.")] public float humanCannonSpotBrightness=1;
        [Min(.3f),Tooltip("플레이어 위치의 빛 원뿔 반경(m). 기본 0.75m로 캐릭터 너비보다 조금 넓게 비춥니다.")] public float spotRadius=.75f;
        [Range(.1f,.98f),Tooltip("바깥 각도 대비 밝은 중심 비율. 1에 가까우면 가장자리 번짐이 줄어듭니다.")] public float innerConeRatio=.82f;
        [Tooltip("주 추적등 색상. 캐릭터 색을 읽기 위한 옅은 백색입니다.")] public Color spotColor=new(.92f,.94f,1);
        [Tooltip("일부 등기구에 섞을 보조 색상.")] public Color secondarySpotColor=new(.76f,.80f,1);
        [Range(0,1),Tooltip("보조 색상 혼합 비율. 0은 주 색상만 사용합니다.")] public float colorTint=.18f;
        [Min(.1f),Tooltip("플레이어 추적 회전 반응 속도(1/초). 높으면 즉시 따라갑니다.")] public float trackingSpeed=12;
        [Min(.05f),Tooltip("등기구 점등·소등 전환 시간(초). 갑작스러운 점멸을 막습니다.")] public float handoffSeconds=.4f;
        [Min(0),Tooltip("등기구를 교체하기 위해 필요한 거리 이득(m). 높이면 잦은 교대가 줄어듭니다.")] public float fixtureHysteresis=8;
        [Min(0),Tooltip("플레이어 원점에서 위로 겨냥할 높이(m). 보통 가슴을 향합니다.")] public float focusHeight=.9f;
        [Tooltip("추적 스폿 그림자 사용. 켜면 천장과 플레이어 사이의 구조물이 빛을 가릴 수 있으며 다인원 성능 비용이 증가합니다.")] public bool spotShadows=false;
        [Range(0,1),Tooltip("추적 그림자의 진하기.")] public float shadowStrength=.8f;
        [Header("원뿔 빛 · 번짐 억제")]
        [Range(0,.2f),Tooltip("광원에서 플레이어로 퍼지는 원뿔 내부의 빛 농도. 실제 조명 밝기와 별개이며 낮으면 화면을 덜 덮습니다.")] public float beamOpacity=.065f;
        [Range(.1f,1),Tooltip("플레이어 위치에서 실제 스폿 대비 보이는 원뿔 반경 비율. 발원지 반경은 항상 0이며 거리에 비례해 넓어집니다.")] public float beamWidth=1;
        [Range(0,1),Tooltip("기존 장식용 빛기둥 농도 배율. 추적 빛기둥과 경쟁하지 않게 낮게 둡니다.")] public float decorativeBeamBrightness=.12f;
        [Header("결승 연출")]
        [Range(0,1),Tooltip("결승 감광 시작 진행률. 0.86은 코스86%입니다.")] public float finaleStart=.86f;
        [Range(0,1),Tooltip("결승 감광 최대 진행률.")] public float finaleFull=.96f;
        [Min(.1f),Tooltip("결승 밝기 전환 시간(초).")]
        public float finaleFadeSeconds=1.5f;
        [Range(0,1),Tooltip("결승 배경등에 남길 밝기 비율. 객석은 덜 어둡게 유지됩니다.")] public float finaleBackground=.42f;
        [Range(1,2),Tooltip("결승 플레이어 스폿 밝기 배율.")] public float finaleSpotBoost=1.12f;
    }
}
