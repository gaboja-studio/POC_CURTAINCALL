using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    [Serializable]
    public sealed class PursuitSettings
    {
        public bool Enabled;
        [Min(0)] public float WarningSeconds = 15;
        [Min(.1f)] public float AwakeningSeconds = 2;
        [Min(.1f)] public float KnockdownSeconds = 1.2f;
        [Min(.1f)] public float CutInterval = 1.3f;
        public const float DefaultCaptureBelow = .5f, DefaultCaptureAbove = .75f;
        [Tooltip("포획 수평 거리(m). 추격자와 플레이어 발 위치의 수평 거리로 잽니다.")]
        [Min(.1f)] public float CaptureRadius = 1.15f;
        [Tooltip("포획 높이 범위(m): 추격자 발보다 이만큼 낮은 곳의 플레이어까지 잡습니다.")]
        [Min(0)] public float CaptureBelow = DefaultCaptureBelow;
        [Tooltip("포획 높이 범위(m): 추격자 발보다 이만큼 높은 곳(트램펄린 표면·낮은 소품 위)의 플레이어까지 잡습니다. 높은 반동·점프 중인 플레이어는 잡지 않습니다.")]
        [Min(0)] public float CaptureAbove = DefaultCaptureAbove;
        [Min(0)] public float LoseTargetSeconds = 3;
        [Header("앉아 숨기")]
        [Tooltip("앉은 플레이어의 몸이 추격자 눈에서 장애물에 이 비율 이상 가려지면 보이지 않습니다(0.8 = 80%). 0이면 끕니다. 벽 너머 탐지·위치 무관 추격 단계는 그 단계 규칙을 따릅니다.")]
        [Range(0, 1)] public float CrouchHiddenCoverage = .8f;
        [Header("원거리 렌더 / 포획 유혈")]
        public bool CullDistantRenderers = true;
        [Min(5)] public float EncounterSeconds = 5;
        [Min(0)] public float RenderSafetyMargin = 8;
        [Min(.1f)] public float CaptureBloodInterval = .22f;
        [Range(0, 3)] public float CaptureBloodAmount = 1.8f;
        [Range(0, 1)] public float CaptureScreenBlood = .8f;
        [Header("전방 상태 신호 / 강화 외형")]
        public bool DirectionCue = true;
        [Range(.25f, 8)] public float DirectionCueLength = 1.75f;
        [HideInInspector] public float DirectionCueAngle = 70; // Legacy data only; the current stage's FieldOfView now owns the angle.
        [Range(0, 1)] public float DirectionCueOpacity = .32f;
        [Tooltip("높을수록 가장자리가 더 넓게 흐려집니다.")]
        [Range(.25f, 3)] public float DirectionCueSoftness = .65f;
        public Color PatrolCueColor = new(.55f, .57f, .56f, 1);
        public Color AlertCueColor = new(.58f, .44f, .20f, 1);
        public Color ChaseCueColor = new(.52f, .14f, .12f, 1);
        [Min(1)] public int AdvancedAppearanceStage = 3;
        [Range(0, 1)] public float BloodPerCapture = .28f;
        [HideInInspector] public float OutlineDistance, OutlinePixels;
        [HideInInspector] public Color OutlineColor;
        public bool ScreenEffect = true;
        [Range(0, 1)] public float EdgeIntensity = .65f;
        [Range(.05f, .4f)] public float EdgeWidth = .19f;
        [Range(0, 1)] public float PulseAmount = .35f;
        [Header("오르기(규칙 기반: 씬·오브젝트 수정 없이 실행 중 NavMesh 가장자리·소품 윗면 충돌체에서 판단)")]
        [Tooltip("이 단계(1부터)부터 추격자가 오르기를 씁니다. 1이면 모든 단계, 0이면 오르지 않습니다.")]
        [Min(0)] public int ClimbFromStage = 1;
        [Tooltip("오를 수 있는 최대 높이(m). 걸어서 넘는 높이(약 0.55m)보다 높고 이 값 이하인 단차·소품 윗면에 오릅니다.")]
        [Range(.6f, 3)] public float ClimbMaxHeight = 2.5f;
        [Tooltip("1.5m까지 오르는 데 걸리는 초. 더 높으면 높이에 비례해 늘어납니다. 오르내리는 동안은 포획하지 않습니다.")]
        [Min(.1f)] public float ClimbSeconds = 1.1f;
        [Tooltip("뛰어내리는 데 걸리는 초.")]
        [Min(.1f)] public float DropSeconds = .5f;
        [Tooltip("같은 가장자리에 만드는 오르기 통로의 최소 간격(m).")]
        [Min(.5f)] public float ClimbLinkSpacing = 1.5f;
        [Header("오르기 손발 접점(시각 전용)")]
        public bool ClimbContactIk = true;
        [Tooltip("접점을 찾는 지형 레이어. 플레이어·추격자·트리거·동적 Rigidbody는 제외합니다.")]
        public LayerMask ClimbIkSurfaceMask = Physics.DefaultRaycastLayers;
        [Tooltip("링크 출발/착지 높이 위아래로 접점 표면을 검사하는 거리(m).")]
        [Range(.05f, .5f)] public float ClimbIkProbeHeight = .25f;
        [Tooltip("손바닥·발바닥과 표면 사이 여유(m).")]
        [Range(0, .05f)] public float ClimbIkSurfaceOffset = .01f;
        [Header("단계별 전등 연출")]
        public PursuitLightSettings Lighting = new();
        [Header("로컬 플레이어 인식 상태 눈")]
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
        [Min(0)] public float StartsAt;
        [Min(.1f)] public float SightRadius = 10;
        [Range(1, 180)] public float FieldOfView = 180;
        [Min(0)] public float PatrolSpeed = 1.5f;
        [Tooltip("온전한 플레이어 최대 달리기 속도의 배율")]
        [Min(.1f)] public float ChaseSpeedMultiplier = 1.2f;
        [Tooltip("달리기 m/s. 0이면 기존 플레이어 달리기 배율을 사용합니다.")]
        [Min(0)] public float ChaseSpeed;
        public float ResolvedChaseSpeed => ChaseSpeed > 0 ? ChaseSpeed
            : GameSettings.Base.Character.FreeMoveSpeed * GameSettings.Base.Character.SprintSpeedMultiplier * Mathf.Max(.1f, ChaseSpeedMultiplier);
        public bool SeeThroughWalls;
        public bool AlwaysNearest;
        [Header("청각 / 행동")]
        [Min(0)] public float WalkHearingRadius = 6;
        [Min(0)] public float RunHearingRadius = 14;
        [Range(0, 1)] public float InvestigateFraction = .65f;
        [Range(0, 1)] public float OccludedHearingMultiplier = .6f;
        [Min(0)] public float AlertSeconds = .7f;
        [Min(0)] public float ListenSeconds = 1.6f;
        [Min(.1f)] public float InvestigateSeconds = 7;
        [Min(0)] public float SearchSeconds = 3;
        [Min(.1f)] public float TrackingRadius = 26;
        [Tooltip("대상을 놓쳤을 때 순찰로 복귀하는 이탈 거리(m). 0이면 제한 없음. 경로가 없으면 최초 배치점 기준. 직접 시야에 들어온 대상은 복귀 중에도 추격합니다. 최인접 원거리 추격에도 적용합니다.")]
        [Min(0)] public float PatrolLeashDistance;
        public string Announcement;
        public PursuitStage() { }
        public PursuitStage(float at, float radius, bool walls, bool nearest, string announcement)
        { StartsAt = at; SightRadius = radius; SeeThroughWalls = walls; AlwaysNearest = nearest; Announcement = announcement; }
    }
}
