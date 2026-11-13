using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    [Serializable]
    public sealed class PursuitSettings
    {
        [Tooltip("켜면 쉬는 시간·정비 시간이 끝났을 때 대기실 추격자가 움직이기 시작한다.")]
        public bool Enabled;
        [Tooltip("쉬는 시간이 이 초 이하로 남으면 추격자가 깨어날 조짐(경고 연출)을 보인다.")]
        [Min(0)] public float WarningSeconds = 15;
        [Tooltip("추격이 시작된 뒤 추격자가 깨어나 움직이기 전까지의 시간(초). 이 동안은 쫓지 않는다.")]
        [Min(.1f)] public float AwakeningSeconds = 2;
        [Tooltip("잡힌 플레이어를 쓰러뜨리는 동작 시간(초). 이 시간이 지나야 절단이 시작된다.")]
        [Min(.1f)] public float KnockdownSeconds = 1.2f;
        [Tooltip("쓰러뜨린 뒤 팔다리를 하나씩 자르는 간격(초). 최대 3번 자른다.")]
        [Min(.1f)] public float CutInterval = 1.3f;
        public const float DefaultCaptureBelow = .5f, DefaultCaptureAbove = .75f;
        [Tooltip("포획 수평 거리(m). 추격자와 플레이어 발 위치의 수평 거리로 잽니다.")]
        [Min(.1f)] public float CaptureRadius = 1.15f;
        [Tooltip("포획 높이 범위(m): 추격자 발보다 이만큼 낮은 곳의 플레이어까지 잡습니다.")]
        [Min(0)] public float CaptureBelow = DefaultCaptureBelow;
        [Tooltip("포획 높이 범위(m): 추격자 발보다 이만큼 높은 곳(트램펄린 표면·낮은 소품 위)의 플레이어까지 잡습니다. 높은 반동·점프 중인 플레이어는 잡지 않습니다.")]
        [Min(0)] public float CaptureAbove = DefaultCaptureAbove;
        [Tooltip("쫓던 플레이어가 이 시간(초) 넘게 보이지 않으면 놓친 것으로 보고 수색으로 바꾼다.")]
        [Min(0)] public float LoseTargetSeconds = 3;
        [Header("앉아 숨기")]
        [Tooltip("앉은 플레이어의 몸이 추격자 눈에서 장애물에 이 비율 이상 가려지면 보이지 않습니다(0.8 = 80%). 0이면 끕니다. 벽 너머 탐지·위치 무관 추격 단계는 그 단계 규칙을 따릅니다.")]
        [Range(0, 1)] public float CrouchHiddenCoverage = .8f;
        [Header("원거리 렌더 / 포획 유혈")]
        [Tooltip("켜면 곧 마주칠 수 없을 만큼 먼 추격자는 그리지 않아 성능을 아낀다.")]
        public bool CullDistantRenderers = true;
        [Tooltip("이 시간(초) 안에 서로 닿을 수 있는 거리 안의 추격자만 그린다.")]
        [Min(5)] public float EncounterSeconds = 5;
        [Tooltip("그리기 거리 계산에 더하는 여유 거리(m). 갑자기 나타나 보이는 것을 막는다.")]
        [Min(0)] public float RenderSafetyMargin = 8;
        [Tooltip("포획 절단 중 피가 튀는 간격(초).")]
        [Min(.1f)] public float CaptureBloodInterval = .22f;
        [Tooltip("포획 절단 때 튀는 피의 양 배율. 공통 신체 손상의 피 양과 곱한다.")]
        [Range(0, 3)] public float CaptureBloodAmount = 1.8f;
        [Tooltip("잡힌 본인 화면에 묻는 피 방울의 진하기(0이면 끔).")]
        [Range(0, 1)] public float CaptureScreenBlood = .8f;
        [Header("전방 상태 신호 / 강화 외형")]
        [Tooltip("켜면 추격자 앞쪽 바닥에 시야 방향과 상태(순찰·경계·추격)를 색으로 보여 준다.")]
        public bool DirectionCue = true;
        [Tooltip("바닥 시야 표시의 길이(m).")]
        [Range(.25f, 8)] public float DirectionCueLength = 1.75f;
        [HideInInspector] public float DirectionCueAngle = 70; // Legacy data only; the current stage's FieldOfView now owns the angle.
        [Tooltip("바닥 시야 표시의 불투명도.")]
        [Range(0, 1)] public float DirectionCueOpacity = .32f;
        [Tooltip("높을수록 가장자리가 더 넓게 흐려집니다.")]
        [Range(.25f, 3)] public float DirectionCueSoftness = .65f;
        [Tooltip("순찰 중일 때 바닥 시야 표시 색.")]
        public Color PatrolCueColor = new(.55f, .57f, .56f, 1);
        [Tooltip("소리를 듣고 경계할 때 바닥 시야 표시 색.")]
        public Color AlertCueColor = new(.58f, .44f, .20f, 1);
        [Tooltip("플레이어를 쫓을 때 바닥 시야 표시 색.")]
        public Color ChaseCueColor = new(.52f, .14f, .12f, 1);
        [Tooltip("이 추격 단계(1부터)부터 추격자 외형이 강화된 모습으로 바뀐다.")]
        [Min(1)] public int AdvancedAppearanceStage = 3;
        [Tooltip("한 명을 잡을 때마다 추격자 몸에 묻는 피가 늘어나는 정도(최대 1).")]
        [Range(0, 1)] public float BloodPerCapture = .28f;
        [HideInInspector] public float OutlineDistance, OutlinePixels;
        [HideInInspector] public Color OutlineColor;
        [Tooltip("켜면 추격자가 가까워질수록 화면 가장자리가 붉게 맥박친다.")]
        public bool ScreenEffect = true;
        [Tooltip("화면 가장자리 위협 효과의 최대 진하기.")]
        [Range(0, 1)] public float EdgeIntensity = .65f;
        [Tooltip("화면 가장자리 위협 효과의 폭(화면 비율).")]
        [Range(.05f, .4f)] public float EdgeWidth = .19f;
        [Tooltip("위협 효과가 맥박처럼 흔들리는 정도. 0이면 일정하게 보인다.")]
        [Range(0, 1)] public float PulseAmount = .35f;
        [Header("오르기(규칙 기반: 씬·오브젝트 수정 없이 실행 중 NavMesh 가장자리·소품 윗면 충돌체에서 판단)")]
        [Tooltip("이 단계(1부터)부터 추격자가 오르기를 씁니다. 1이면 모든 단계, 0이면 오르지 않습니다.")]
        [Min(0)] public int ClimbFromStage = 1;
        [Tooltip("오를 수 있는 최대 높이(m). 0이면 높이 제한 없이 오릅니다. 걸어서 넘는 높이(약 0.55m)보다 높고 이 값 이하인 단차·소품 윗면에 오릅니다. 2.5m를 넘는 곳은 손을 번갈아 짚으며 벽을 타고 오릅니다.")]
        [Min(0)] public float ClimbMaxHeight;
        /// <summary>실제 적용 최대 높이. 0 이하면 제한 없음(무한대), 그 외는 최소 오르기 높이 0.6m 이상.</summary>
        public float ResolvedClimbMaxHeight => ClimbMaxHeight > 0 ? Mathf.Max(.6f, ClimbMaxHeight) : float.PositiveInfinity;
        [Tooltip("1.5m까지 오르는 데 걸리는 초. 더 높으면 높이에 비례해 늘어납니다(벽 타기 속도도 같음). 오르내리는 동안은 포획하지 않습니다.")]
        [Min(.1f)] public float ClimbSeconds = 1.1f;
        [Tooltip("1.5m를 뛰어내리는 데 걸리는 초. 2.5m까지는 높이에 비례하고, 그보다 높으면 낙하처럼 높이의 제곱근에 비례해 늘어납니다.")]
        [Min(.1f)] public float DropSeconds = .5f;
        [Tooltip("같은 가장자리에 만드는 오르기 통로의 최소 간격(m).")]
        [Min(.5f)] public float ClimbLinkSpacing = 1.5f;
        [Header("오르기 손발 접점(시각 전용)")]
        [Tooltip("켜면 추격자가 오를 때 손발이 실제 표면에 닿도록 자세를 맞춘다(보이는 모습만 바뀌고 판정에는 영향 없음).")]
        public bool ClimbContactIk = true;
        [Tooltip("접점을 찾는 지형 레이어. 플레이어·추격자·트리거·동적 Rigidbody는 제외합니다.")]
        public LayerMask ClimbIkSurfaceMask = Physics.DefaultRaycastLayers;
        [Tooltip("링크 출발/착지 높이 위아래로 접점 표면을 검사하는 거리(m).")]
        [Range(.05f, .5f)] public float ClimbIkProbeHeight = .25f;
        [Tooltip("손바닥·발바닥과 표면 사이 여유(m).")]
        [Range(0, .05f)] public float ClimbIkSurfaceOffset = .01f;
        [Header("단계별 전등 연출")]
        [Tooltip("추격 단계에 따라 대기실 전등을 깜빡이거나 어둡게 하는 연출.")]
        public PursuitLightSettings Lighting = new();
        [Header("로컬 플레이어 인식 상태 눈")]
        [Tooltip("추격자가 나를 알아챘는지 보여 주는 눈 표시.")]
        public PursuitEyeSettings Eyes = new();
        [Tooltip("추격 시작부터의 누적 초. 단계별 탐지·순찰·추격 속도를 조절합니다.")]
        public PursuitStage[] Stages = {
            new(0, 10, false, false, "추격자가 움직이기 시작했습니다."),
            new(60, 20, false, false, "추격자의 시야 반경이 크게 늘어납니다."),
            new(120, 20, true, false, "추격자가 벽 뒤까지 투시하여 볼 수 있습니다."),
            new(180, 20, true, true, "추격자가 모든 위치를 알고 가장 가까운 단원을 추격합니다.")
        };

        public int StageAt(float seconds)
        {
            int result = 0;
            float latest = float.NegativeInfinity;
            if (Stages == null) return result;
            for (int i = 0; i < Stages.Length; i++)
                if (Stages[i] != null && seconds >= Stages[i].StartsAt && Stages[i].StartsAt >= latest)
                { result = i; latest = Stages[i].StartsAt; }
            return result;
        }
        public PursuitStage Stage(float seconds) => Stages != null && Stages.Length > 0
            ? Stages[StageAt(seconds)] ?? fallback : fallback;
        static readonly PursuitStage fallback = new();
    }

    [Serializable]
    public sealed class PursuitStage
    {
        [Tooltip("이 단계가 시작되는 시각(추격 시작부터 누적 초).")]
        [Min(0)] public float StartsAt;
        [Tooltip("이 단계에서 추격자가 플레이어를 볼 수 있는 거리(m).")]
        [Min(.1f)] public float SightRadius = 10;
        [Tooltip("이 단계의 시야 각도(도). 바닥 시야 표시의 각도도 이 값을 따른다.")]
        [Range(1, 180)] public float FieldOfView = 180;
        [Tooltip("대상이 없을 때 순찰하며 걷는 속도(m/s).")]
        [Min(0)] public float PatrolSpeed = 1.5f;
        [Tooltip("온전한 플레이어 최대 달리기 속도의 배율")]
        [Min(.1f)] public float ChaseSpeedMultiplier = 1.2f;
        [Tooltip("달리기 m/s. 0이면 기존 플레이어 달리기 배율을 사용합니다.")]
        [Min(0)] public float ChaseSpeed;
        public float ResolvedChaseSpeed => ChaseSpeed > 0 ? ChaseSpeed
            : GameSettings.Base.Character.FreeMoveSpeed * GameSettings.Base.Character.SprintSpeedMultiplier * Mathf.Max(.1f, ChaseSpeedMultiplier);
        [Tooltip("켜면 벽·장애물에 가려도 시야 거리 안의 플레이어를 본다.")]
        public bool SeeThroughWalls;
        [Tooltip("켜면 위치와 상관없이 항상 가장 가까운 플레이어를 알고 쫓는다.")]
        public bool AlwaysNearest;
        [Header("청각 / 행동")]
        [Tooltip("걷는 발소리를 듣는 거리(m).")]
        [Min(0)] public float WalkHearingRadius = 6;
        [Tooltip("달리는 발소리를 듣는 거리(m).")]
        [Min(0)] public float RunHearingRadius = 14;
        [Tooltip("들은 거리 비율이 이 값 이하로 가까우면 소리 난 곳까지 가서 조사하고, 더 멀면 그 자리에서 귀만 기울인다.")]
        [Range(0, 1)] public float InvestigateFraction = .65f;
        [Tooltip("벽에 가린 소리를 들을 때 듣는 거리에 곱하는 값.")]
        [Range(0, 1)] public float OccludedHearingMultiplier = .6f;
        [Tooltip("플레이어를 처음 발견한 뒤 쫓기 시작하기 전 멈칫하는 시간(초).")]
        [Min(0)] public float AlertSeconds = .7f;
        [Tooltip("먼 소리를 듣고 그 방향을 보며 귀 기울이는 시간(초).")]
        [Min(0)] public float ListenSeconds = 1.6f;
        [Tooltip("가까운 소리가 난 곳을 조사하는 최대 시간(초).")]
        [Min(.1f)] public float InvestigateSeconds = 7;
        [Tooltip("대상을 놓친 곳이나 조사 지점에서 주변을 둘러보는 시간(초).")]
        [Min(0)] public float SearchSeconds = 3;
        [Tooltip("쫓기 시작한 뒤 이 거리(m)까지는 시야 거리를 벗어나도 계속 쫓는다.")]
        [Min(.1f)] public float TrackingRadius = 26;
        [Tooltip("대상을 놓쳤을 때 순찰로 복귀하는 이탈 거리(m). 0이면 제한 없음. 경로가 없으면 최초 배치점 기준. 직접 시야에 들어온 대상은 복귀 중에도 추격합니다. 최인접 원거리 추격에도 적용합니다.")]
        [Min(0)] public float PatrolLeashDistance;
        [Tooltip("이 단계가 시작될 때 모두에게 띄우는 안내 문구.")]
        public string Announcement;
        public PursuitStage() { }
        public PursuitStage(float at, float radius, bool walls, bool nearest, string announcement)
        { StartsAt = at; SightRadius = radius; SeeThroughWalls = walls; AlwaysNearest = nearest; Announcement = announcement; }
    }
}
