using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 외줄 묘기 세팅: 코스·진행·줄 위 동작·손잡기·목마·카메라(+ 보상 연결은 부모).
    /// 에셋: Assets/Resources/GameSettings/Tricks/TightropeSettings.asset. <see cref="GameSettings.Tightrope"/>로 읽는다.
    /// 코스 모양은 코스를 만들 때 읽는다(대기 중 수정은 바로 다시 만들고, 게임 중 수정은 다음 대기 때). 나머지는 쓸 때마다 읽는다.
    /// 톱날(012)·불(014) 칸은 해당 Task가 여기에 묶음을 추가한다. 기본값은 2026-10-03 프리팹 값과 같다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Tricks/Tightrope", fileName = "TightropeSettings")]
    public sealed class TightropeSettings : TrickSettings
    {
        [Header("코스 (만들 때 적용)")]
        [SerializeField] CourseSettings course = new();
        [Header("진행")]
        [SerializeField] RunSettings run = new();
        [Header("줄 위 동작")]
        [SerializeField] RopeMovementSettings ropeMovement = new();
        [Header("손잡기")]
        [SerializeField] HandholdSettings handhold = new();
        [Header("목마")]
        [SerializeField] PiggybackSettings piggyback = new();
        [Header("카메라")]
        [SerializeField] CameraSettings camera = new();

        /// <summary>인스펙터에서 값을 바꿨을 때(에디터). 코스가 다시 만들지 판단하는 데 쓴다.</summary>
        public event Action Changed;

        public CourseSettings Course => course;
        public RunSettings Run => run;
        public RopeMovementSettings RopeMovement => ropeMovement;
        public HandholdSettings Handhold => handhold;
        public PiggybackSettings Piggyback => piggyback;
        public CameraSettings Camera => camera;

        void OnValidate() => Changed?.Invoke();

        /// <summary>
        /// 코스 모양. 온라인에서는 호스트 값을 그대로 복사해 쓰므로(코스 값 공유) JSON으로 주고받을 수 있게 칸만 둔다.
        /// </summary>
        [Serializable]
        public sealed class CourseSettings
        {
            [Header("줄")]
            [Tooltip("줄 수(개). 기획 4.")]
            [SerializeField, Min(1)] int laneCount = 4;

            [Tooltip("줄 중심 간격(m). 옆줄 점프 거리도 이 값이 된다. 기획 3.")]
            [SerializeField, Min(0.1f)] float laneSpacing = 3f;

            [Tooltip("줄마다 끝나는 거리(m, 왼쪽 줄부터). 비우거나 0 이하·도착 거리 이상이면 도착까지 간다. 기획: 모든 줄이 도착까지.")]
            [SerializeField] float[] laneEndDistances = Array.Empty<float>();

            [Tooltip("코스 길이 = 도착 판정선(m, 외줄 시작점에서). 기획 100.")]
            [SerializeField, Min(1f)] float finishDistance = 100f;

            [Tooltip("옆줄 이동 가능 구간(m). x = 시작, y = 끝. 구간 밖에서는 옆줄 이동을 막는다. 2026-10-03 PM: 0~100 전 구간.")]
            [SerializeField] Vector2[] laneChangeZones = { new(0f, 100f) };

            [Header("모양")]
            [Tooltip("시작(대기) 플랫폼 길이(m). 기획 6.")]
            [SerializeField, Min(0.5f)] float startPlatformLength = 6f;

            [Tooltip("도착(세리머니) 플랫폼 길이(m). 기획 6.")]
            [SerializeField, Min(0f)] float finishPlatformLength = 6f;

            [Tooltip("양쪽 끝 줄 중심에서 맵 경계까지 여유(m). 기획 2(유효 폭 13m).")]
            [SerializeField, Min(0f)] float sideMargin = 2f;

            [Tooltip("플랫폼 두께(m). 윗면이 줄 높이와 같다.")]
            [SerializeField, Min(0.05f)] float platformThickness = 0.5f;

            [Tooltip("줄 굵기(m, 보이는 지름). 기획 0.2.")]
            [SerializeField, Min(0.01f)] float ropeThickness = 0.2f;

            [Tooltip("줄 위를 걸을 수 있는 폭(m, 충돌 폭). 보이는 굵기보다 넓어야 캐릭터가 서 있는다. 0.4.")]
            [SerializeField, Min(0.01f)] float ropeWalkWidth = 0.4f;

            [Tooltip("발 높이가 줄 윗면에서 이 거리(m) 안이면 줄 위로 본다. 0.3.")]
            [SerializeField, Min(0.01f)] float ropeHeightTolerance = 0.3f;

            [Tooltip("출발 위치(m, 외줄 시작점 기준). 음수면 시작 플랫폼 위. -1.")]
            [SerializeField] float spawnDistance = -1f;

            [Header("추락")]
            [Tooltip("코스 아래 안전망 깊이(m). 떨어진 캐릭터를 받는다. 0이면 만들지 않는다. 6.")]
            [SerializeField, Min(0f)] float safetyNetDepth = 6f;

            [Tooltip("캐릭터가 줄 높이보다 이만큼(m) 내려가면 추락 처리. 기획 값이 없어 테스트 값 1.")]
            [SerializeField, Min(0.1f)] float fallDepth = 1f;

            public int LaneCount => laneCount;
            public float LaneSpacing => laneSpacing;
            public float[] LaneEndDistances => laneEndDistances;
            public float FinishDistance => finishDistance;
            public Vector2[] LaneChangeZones => laneChangeZones;
            public float StartPlatformLength => startPlatformLength;
            public float FinishPlatformLength => finishPlatformLength;
            public float SideMargin => sideMargin;
            public float PlatformThickness => platformThickness;
            public float RopeThickness => ropeThickness;
            public float RopeWalkWidth => ropeWalkWidth;
            public float RopeHeightTolerance => ropeHeightTolerance;
            public float SpawnDistance => spawnDistance;
            public float SafetyNetDepth => safetyNetDepth;
            public float FallDepth => fallDepth;

            /// <summary>값을 JSON으로(코스 값 공유용).</summary>
            public string ToJson() => JsonUtility.ToJson(this);

            /// <summary>JSON에서 만든다. 실패하면 null.</summary>
            public static CourseSettings FromJson(string json)
            {
                if (string.IsNullOrEmpty(json)) return null;
                try { return JsonUtility.FromJson<CourseSettings>(json); }
                catch (ArgumentException) { return null; }
            }

            /// <summary>지금 값을 복사한다. 코스가 만들 때의 값을 붙잡아 두는 데 쓴다.</summary>
            public CourseSettings Clone() => FromJson(ToJson());
        }

        /// <summary>묘기 진행(호스트 판정).</summary>
        [Serializable]
        public sealed class RunSettings
        {
            [Tooltip("제한시간(초). 게임 시작부터 잰다. 기획 300(5분).")]
            [SerializeField, Min(1f)] float timeLimit = 300f;

            [Tooltip("실패 후 묘기 재시작까지 기다리는 시간(초). 기획 값이 없어 테스트 값 3.")]
            [SerializeField, Min(0f)] float restartDelay = 3f;

            public float TimeLimit => timeLimit;
            public float RestartDelay => restartDelay;
        }

        /// <summary>줄 위 동작(이동·점프·착지 충격).</summary>
        [Serializable]
        public sealed class RopeMovementSettings
        {
            [Tooltip("줄 위 이동 속도(m/s). 기획 0.5.")]
            [SerializeField, Min(0f)] float moveSpeed = 0.5f;

            [Tooltip("점프 높이(m, 발 기준 최고점). 0.8. 공중 시간은 이 값과 중력으로 정해진다.")]
            [SerializeField, Min(0f)] float jumpHeight = 0.8f;

            [Tooltip("착지 충격(균형 ±, 랜덤 방향). 목마 배율을 곱한다. 기획 10.")]
            [SerializeField, Min(0f)] float landingShock = 10f;

            [Tooltip("이 시간(초)보다 짧게 떠 있었으면 착지 충격을 주지 않는다(작은 턱 무시). 0.15.")]
            [SerializeField, Min(0f)] float minAirTimeForShock = 0.15f;

            [Tooltip("옆줄 점프 착지 충격(균형 ±, 랜덤 방향). 목마 배율을 곱한다. 기획 35.")]
            [SerializeField, Min(0f)] float laneLandingShock = 35f;

            [Tooltip("옆줄 점프 착지 후 자연 흔들림 배율(×). 1.75(#17).")]
            [SerializeField, Min(1f)] float laneSwayBoost = 1.75f;

            [Tooltip("옆줄 점프 착지 후 흔들림이 커지는 시간(초). 반복해도 쌓이지 않고 시간만 갱신. 기획 3.")]
            [SerializeField, Min(0f)] float laneSwayBoostDuration = 3f;

            public float MoveSpeed => moveSpeed;
            public float JumpHeight => jumpHeight;
            public float LandingShock => landingShock;
            public float MinAirTimeForShock => minAirTimeForShock;
            public float LaneLandingShock => laneLandingShock;
            public float LaneSwayBoost => laneSwayBoost;
            public float LaneSwayBoostDuration => laneSwayBoostDuration;
        }

        /// <summary>손잡기(협동 옆줄 이동): 옆줄 착지 지점이 같은 줄 동료 가까이면 착지 충격·흔들림을 줄인다.</summary>
        [Serializable]
        public sealed class HandholdSettings
        {
            [Tooltip("옆줄 착지 지점과 같은 줄 동료의 앞뒤 거리(m, 몸 중심끼리)가 이 안이면 손잡기. 기획 1.0.")]
            [SerializeField, Min(0f)] float distance = 1f;

            [Tooltip("손잡기 때 옆줄 착지 충격·흔들림 증가 감소율(0~1). 기획 0.5(50%).")]
            [SerializeField, Range(0f, 1f)] float reduction = 0.5f;

            public float Distance => distance;
            public float Reduction => reduction;
        }

        /// <summary>목마(여러 층으로 쌓기).</summary>
        [Serializable]
        public sealed class PiggybackSettings
        {
            [Tooltip("목마 전체 인원별 흔들림 배율(×, 1인~4인 순서). 자연 흔들림과 충격에 곱한다. 기획 1 / 1.3 / 1.6 / 2.")]
            [SerializeField] float[] stackMultipliers = { 1f, 1.3f, 1.6f, 2f };

            [Tooltip("위층 균형 전달: 초당 (내 위층 균형값 합 × 이 값)만큼 같은 방향으로 민다. 기획 0.2.")]
            [SerializeField, Min(0f)] float upperTransfer = 0.2f;

            [Tooltip("목마 연결 거리(m, 수평). F 짧게를 누른 사람과 상대(목마면 맨 아래 사람) 사이가 이 안이어야 올라간다. 호스트 값으로 판정. 기획 미정(임시 1.5).")]
            [SerializeField, Min(0f)] float linkDistance = 1.5f;

            [Tooltip("위층이 떨어질 때 바로 아래층이 받는 반동 충격(균형 ±, 랜덤 방향, 목마 배율 적용). 호스트 값으로 보낸다. 기획 미정(임시 10, 착지 충격과 같음).")]
            [SerializeField, Min(0f)] float fallShock = 10f;

            [Tooltip("맨 위 W + SB 앞으로 분리 점프의 수평 속도(m/s, 높이는 혼자 점프와 같음). 2층 발 높이 1.5m에서 수직 톱날(지름 1.0m·상단 1.1m)을 넘게 정한 값: 약 2.4m 앞 착지. 기획 미정(임시 2.2).")]
            [SerializeField, Min(0f)] float forwardJumpSpeed = 2.2f;

            [Tooltip("맨 위 S + SB 뒤로 분리 점프의 수평 속도(m/s). 아래 사람과 겹치지 않게 착지하는 최소에 가까운 값: 약 0.75m 뒤 착지. 기획 미정(임시 0.7).")]
            [SerializeField, Min(0f)] float backwardJumpSpeed = 0.7f;

            public float UpperTransfer => upperTransfer;
            public float LinkDistance => linkDistance;
            public float FallShock => fallShock;
            public float ForwardJumpSpeed => forwardJumpSpeed;
            public float BackwardJumpSpeed => backwardJumpSpeed;

            /// <summary>목마 전체 인원의 배율. 칸이 비어 있으면 1, 인원이 칸보다 많으면 마지막 칸.</summary>
            public float GetStackMultiplier(int stackSize) =>
                stackMultipliers == null || stackMultipliers.Length == 0
                    ? 1f
                    : stackMultipliers[Mathf.Clamp(stackSize, 1, stackMultipliers.Length) - 1];
        }

        /// <summary>
        /// 외줄 카메라 구도와 가림 처리(Integrations/Active/Integration-tightrope-prototype/camera-review-20261004.md, 2026-10-04 PM).
        /// 카메라 리그(PlayerFollowCamera)의 구도·가림 처리 컴포넌트가 매 프레임 읽는다.
        /// </summary>
        [Serializable]
        public sealed class CameraSettings
        {
            [Tooltip("카메라 높이(m, 내 캐릭터 발 기준 위). 이전 2.6. 기획 미정(임시 3.75).")]
            [SerializeField, Min(0f)] float height = 3.75f;

            [Tooltip("카메라 거리(m, 코스 뒤쪽).")]
            [SerializeField, Min(0.5f)] float distance = 4.5f;

            [Tooltip("바라보는 점 높이(m, 내 캐릭터 발 기준).")]
            [SerializeField] float lookHeight = 1.2f;

            [Tooltip("바라보는 점을 내 캐릭터 앞쪽으로 옮기는 거리(m). 앞 장애물이 화면 가운데로 온다. 기획 미정(임시 1.75).")]
            [SerializeField] float lookAhead = 1.75f;

            [Tooltip("카메라와 내 캐릭터 사이를 가리는 것(다른 플레이어·장애물)의 투명도(0 투명 ~ 1 불투명). 기획 미정(임시 0.35).")]
            [SerializeField, Range(0f, 1f)] float occluderAlpha = 0.35f;

            [Tooltip("가림 판정 굵기(m, 반지름). 카메라→내 캐릭터 선에서 이 안에 걸리면 흐리게 한다.")]
            [SerializeField, Min(0f)] float occluderRadius = 0.4f;

            [Tooltip("흐려지고 돌아오는 빠르기(초당 투명도 변화).")]
            [SerializeField, Min(0.1f)] float fadeSpeed = 6f;

            public float Height => height;
            public float Distance => distance;
            public float LookHeight => lookHeight;
            public float LookAhead => lookAhead;
            public float OccluderAlpha => occluderAlpha;
            public float OccluderRadius => occluderRadius;
            public float FadeSpeed => fadeSpeed;
        }
    }
}
