using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>상호작용 대상 분류. 우선순위 표는 이 분류의 순서로 정한다(2026-11-16 상호작용 고도화 기획).</summary>
    public enum InteractionCategory
    {
        [InspectorName("해금 가능 (조건 충족 문·잠금·벽·도구)")] Unlockable = 0,
        [InspectorName("처음 확인하는 단서")] FirstClue = 1,
        [InspectorName("잡을 수 있는 기물 오브제 (휴대 장치·운반물)")] GrabObject = 2,
        [InspectorName("습득 가능한 기물·토큰")] PickupItem = 3,
        [InspectorName("서비스 (상점·스케줄표·침대·TV·이미 본 단서 등)")] Service = 4,
        [InspectorName("플레이어 간 상호작용 (목마)")] Piggyback = 5,
        [InspectorName("해금 불가능 (안내만)")] Locked = 6,
        [InspectorName("습득 불가능한 기물 (안내만)")] PickupBlocked = 7,
        [InspectorName("공연 장치 (대포·봉·그네·구조·버튼)")] PerformanceDevice = 8,
    }

    /// <summary>화면 기준점(크로스헤어) 모양.</summary>
    public enum CrosshairShape
    {
        [InspectorName("점")] Dot,
        [InspectorName("십자")] Cross,
        [InspectorName("원")] Ring,
    }

    /// <summary>
    /// 상호작용 기준점·우선순위·아웃라인 설정(2026-11-16 상호작용 고도화). 기준점은 같은 순위 안의 순서만 정한다.
    /// 에셋: Assets/Resources/Settings/Interaction/InteractionFocusSettings.asset. 플레이 중 수정이 바로 반영된다.
    /// 기획서: Confluence "상호작용 고도화"(pages/43286568) · 구현 세부 규칙.
    /// 플레이어와 대상 사이 거리(공통 상호작용 거리)는 BaseGameSettings › Interaction에서 그대로 조정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Interaction Focus Settings", fileName = "InteractionFocusSettings")]
    public sealed class InteractionFocusSettings : ScriptableObject
    {
        public const string ResourcePath = "Settings/Interaction/InteractionFocusSettings";

        [Serializable]
        public sealed class CrosshairSettings
        {
            [InspectorName("표시"), Tooltip("일반 조작 중 화면 정중앙에 기준점을 표시한다. 꺼도 우선순위 판정은 그대로 화면 중앙 기준이다.")]
            public bool visible = true;
            [InspectorName("모양"), Tooltip("점 · 십자 · 원.")]
            public CrosshairShape shape = CrosshairShape.Dot;
            [InspectorName("크기(px)"), Tooltip("1080p 기준 지름(px). 화면 높이에 비례해 커지고 작아진다.")]
            [Range(2f, 64f)] public float size = 6f;
            [InspectorName("선 두께(px)"), Tooltip("십자·원 모양의 선 두께(1080p 기준 px).")]
            [Range(1f, 8f)] public float thickness = 2f;
            [InspectorName("기본 색")]
            public Color idleColor = new(1f, 1f, 1f, .65f);
            [InspectorName("대상 선택 색"), Tooltip("F가 작동할 대상이 기준점에 잡혀 있을 때 색.")]
            public Color targetColor = new(1f, .86f, .45f, 1f);
            [InspectorName("외곽 그림자"), Tooltip("밝은 배경에서도 보이도록 기준점 둘레에 얇은 어두운 테두리를 그린다.")]
            public bool shadow = true;
        }

        [Serializable]
        public sealed class OutlineSettings
        {
            [InspectorName("거리 안 대상도 표시"), Tooltip("켜면 플레이어 상호작용 거리 안의 다른 상호작용 가능 대상도 아래 두께·색으로 얇게 표시한다. 끄면 F가 작동할 선택 대상만 외곽선을 그린다.")]
            public bool showInRange = true;
            [InspectorName("거리 안 대상 두께(px)"), Tooltip("선택 대상이 아닌 거리 안 대상의 외곽선 두께. 0이면 표시하지 않는다.")]
            [Range(0f, 6f)] public float inRangePixels = 1.5f;
            [InspectorName("거리 안 대상 색"), Tooltip("선택 대상이 아닌 거리 안 대상의 외곽선 색. A(알파)가 투명도이며 0이면 보이지 않는다. HDR 강도로 밝기를 올릴 수 있다.")]
            [ColorUsage(true, true)] public Color inRangeColor = new(1.4f, 1.3f, 1.1f, 1f);
            [InspectorName("선택 대상 두께(px)"), Tooltip("F가 실제로 작동할 대상의 외곽선 두께.")]
            [Range(0f, 6f)] public float selectedPixels = 3f;
            [InspectorName("선택 대상 색"), Tooltip("F가 실제로 작동할 대상의 외곽선 색. A(알파)가 투명도다."), ColorUsage(true, true)]
            public Color selectedColor = new(3f, 2.8f, 2.2f, 1f);
        }

        [Serializable]
        public sealed class PiggybackStateSettings
        {
            [InspectorName("대기실 상태 변화 시 목마 해제"), Tooltip("대기실에서 침대에 눕기·사망(포획 포함) 시 그 단원과 위 단원을 목마에서 안전하게 내린다. 아래 단원은 목마를 유지한다(공연·탈출로는 기존 처리).")]
            public bool releaseOnWaitingRoomStateChange = true;
            [InspectorName("대기실 앉은 채 목마 허용"), Tooltip("대기실에서 앉은 단원도 목마 탑승·해제를 할 수 있다. 탑승하면 일어설 공간이 있어야 한다.")]
            public bool allowCrouchedInWaitingRoom = true;
        }

        [InspectorName("기준점"), SerializeField] CrosshairSettings crosshair = new();
        [InspectorName("아웃라인"), SerializeField] OutlineSettings outline = new();

        [InspectorName("대기실 우선순위"), Tooltip("상호작용 거리 안 대상 중 위에 있을수록 먼저 선택한다. 목록에 없는 분류는 맨 뒤. 같은 순위끼리는 화면 기준점에 가까운 대상 → 플레이어에 가까운 대상 순이다(기준점은 범위를 거르지 않는다).")]
        [SerializeField] InteractionCategory[] waitingRoomOrder =
        {
            InteractionCategory.Unlockable, InteractionCategory.PerformanceDevice, InteractionCategory.FirstClue,
            InteractionCategory.GrabObject, InteractionCategory.PickupItem, InteractionCategory.Service,
            InteractionCategory.Piggyback, InteractionCategory.Locked, InteractionCategory.PickupBlocked,
        };

        [InspectorName("공연·탈출로 우선순위"), Tooltip("공연과 탈출로에서 쓰는 순서. 위에 있을수록 먼저 선택한다.")]
        [SerializeField] InteractionCategory[] performanceOrder =
        {
            InteractionCategory.PerformanceDevice, InteractionCategory.Piggyback, InteractionCategory.PickupItem,
            InteractionCategory.GrabObject, InteractionCategory.Unlockable, InteractionCategory.FirstClue,
            InteractionCategory.Service, InteractionCategory.Locked, InteractionCategory.PickupBlocked,
        };

        [InspectorName("목마 상태 규칙"), SerializeField] PiggybackStateSettings piggyback = new();

        public CrosshairSettings Crosshair => crosshair;
        public OutlineSettings Outline => outline;
        public PiggybackStateSettings Piggyback => piggyback;

        /// <summary>작을수록 먼저. 목록에 없는 분류는 목록 길이(맨 뒤).</summary>
        public int Rank(InteractionCategory category, bool waitingRoom)
        {
            var order = waitingRoom ? waitingRoomOrder : performanceOrder;
            if (order == null) return (int)category;
            int index = Array.IndexOf(order, category);
            return index >= 0 ? index : order.Length + (int)category;
        }

        static InteractionFocusSettings current, fallback;

        /// <summary>지금 쓰는 설정. Resources에서 읽고 없으면 코드 기본값.</summary>
        public static InteractionFocusSettings Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<InteractionFocusSettings>(ResourcePath);
                if (current != null) return current;
                if (fallback == null)
                {
                    Debug.LogWarning($"[Settings] Resources/{ResourcePath} 세팅을 찾지 못해 코드 기본값을 씁니다.");
                    fallback = CreateInstance<InteractionFocusSettings>();
                    fallback.hideFlags = HideFlags.DontSave;
                }
                return fallback;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { current = null; }
    }
}
