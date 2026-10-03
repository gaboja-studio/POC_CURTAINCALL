using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 게임 기본 세팅: 세션·캐릭터·입력·균형 공통·연출·동기화. 묘기와 상관없이 쓰는 값.
    /// 에셋: Assets/Resources/GameSettings/BaseGameSettings.asset. <see cref="GameSettings.Base"/>로 읽는다.
    /// 기본값은 2026-10-03 프리팹 값(옮기기 전 값)과 같다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Base Game Settings", fileName = "BaseGameSettings")]
    public sealed class BaseGameSettings : ScriptableObject
    {
        [Header("세션")]
        [SerializeField] SessionSettings session = new();
        [Header("캐릭터")]
        [SerializeField] CharacterSettings character = new();
        [Header("입력")]
        [SerializeField] InputSettings input = new();
        [Header("균형 공통")]
        [SerializeField] BalanceSettings balance = new();
        [Header("연출")]
        [SerializeField] PresentationSettings presentation = new();
        [Header("동기화")]
        [SerializeField] SyncSettings sync = new();

        public SessionSettings Session => session;
        public CharacterSettings Character => character;
        public InputSettings Input => input;
        public BalanceSettings Balance => balance;
        public PresentationSettings Presentation => presentation;
        public SyncSettings Sync => sync;

        void OnValidate() => balance.Validate();

        /// <summary>세션(방) 인원.</summary>
        [Serializable]
        public sealed class SessionSettings
        {
            [Tooltip("방 정원(명). 이 인원이 차면 더 들어오지 못한다(자동 시작은 하지 않음). 방을 만들 때 적용된다.")]
            [SerializeField, Min(1)] int maxPlayers = 4;

            [Tooltip("호스트가 게임을 시작할 수 있는 최소 인원(명, 호스트 포함). 정원보다 크면 정원을 쓴다. 2026-10-02 테스트용 1.")]
            [SerializeField, Min(1)] int minPlayers = 1;

            public int MaxPlayers => maxPlayers;
            public int MinPlayers => Mathf.Min(minPlayers, maxPlayers);
        }

        /// <summary>캐릭터 몸과 일반 이동(플랫폼).</summary>
        [Serializable]
        public sealed class CharacterSettings
        {
            [Tooltip("플랫폼 위 이동 속도(m/s). 줄 위 속도는 외줄 세팅. 2026-10-03 PM 테스트 값 3.")]
            [SerializeField, Min(0f)] float freeMoveSpeed = 3f;

            [Tooltip("플랫폼 위에서 이동 방향으로 몸을 돌리는 속도(도/초).")]
            [SerializeField, Min(0f)] float turnSpeed = 720f;

            [Tooltip("중력 크기(m/s²). 기획 기준 9.81. 점프 높이·공중 시간도 이 값으로 계산한다.")]
            [SerializeField, Min(0.1f)] float gravity = 9.81f;

            [Tooltip("몸통(충돌 캡슐) 높이(m). 2026-10-03 기준 1.5. ⚠ 모델 맞춤 키(1.73)와 다르다 — 모델 작업(017)에서 확정.")]
            [SerializeField, Min(0.1f)] float capsuleHeight = 1.5f;

            [Tooltip("몸통(충돌 캡슐) 지름(m). 2026-10-03 기준 0.6. 높이보다 클 수 없다.")]
            [SerializeField, Min(0.05f)] float capsuleDiameter = 0.6f;

            [Tooltip("모델을 맞출 캐릭터 키(m). 기획 1.73. ⚠ 캡슐 높이(1.5)와 다르다 — 모델 작업(017)에서 확정. 모델을 끼울 때 적용된다.")]
            [SerializeField, Min(0.1f)] float modelHeight = 1.73f;

            [Tooltip("몸통으로 래그돌 같은 물체를 밀 때 주는 속도(m/s). 0이면 밀지 않는다.")]
            [SerializeField, Min(0f)] float pushSpeed = 2f;

            [Tooltip("다른 플레이어와 닿을 때 남기는 틈(m).")]
            [SerializeField, Min(0f)] float playerGap = 0.05f;

            public float FreeMoveSpeed => freeMoveSpeed;
            public float TurnSpeed => turnSpeed;
            public float Gravity => gravity;
            public float CapsuleHeight => capsuleHeight;
            public float CapsuleRadius => Mathf.Min(capsuleDiameter, capsuleHeight) * 0.5f;
            public float ModelHeight => modelHeight;
            public float PushSpeed => pushSpeed;
            public float PlayerGap => playerGap;
        }

        /// <summary>입력 판정.</summary>
        [Serializable]
        public sealed class InputSettings
        {
            [Tooltip("상호작용 키를 이 시간(초) 이상 누르면 '길게'(목마 해제 등). 기획 값이 없어 튜닝값 0.5.")]
            [SerializeField, Min(0.05f)] float longPressTime = 0.5f;

            public float LongPressTime => longPressTime;
        }

        /// <summary>균형 게이지 공통 규칙. 규칙: Harness/Project/Decisions/tightrope-balance.md</summary>
        [Serializable]
        public sealed class BalanceSettings
        {
            [Tooltip("중앙에서 이 값(±, 게이지 ±100 기준)까지 초록, 넘으면 빨강. 기획 40.")]
            [SerializeField, Range(0f, 100f)] float greenLimit = 40f;

            [Tooltip("빨강에 이 시간(초) 이상 머물면 추락. 기획 확정 2초.")]
            [SerializeField, Min(0.05f)] float fallTime = 2f;

            [Tooltip("균형 보정(A/D)을 끝까지 눌렀을 때 바늘이 움직이는 양(초당). 기획 60.")]
            [SerializeField, Min(0f)] float correctionSpeed = 60f;

            [Tooltip("걷는 중 자연 흔들림(초당). 기획 15.")]
            [SerializeField, Min(0f)] float movingSway = 15f;

            [Tooltip("서 있을 때 자연 흔들림(초당). 기획 5.")]
            [SerializeField, Min(0f)] float idleSway = 5f;

            [Tooltip("흔들림 방향이 바뀌는 간격(초). x~y 사이에서 랜덤. 기획 1~2초.")]
            [SerializeField] Vector2 swayDirectionInterval = new(1f, 2f);

            [Tooltip("기울기 가속: 초당 (현재 균형값 × 이 값)만큼 바깥으로 민다. 많이 기울수록 빨리 넘어간다. 기획 0.5.")]
            [SerializeField, Min(0f)] float tiltAcceleration = 0.5f;

            public float GreenLimit => greenLimit;
            public float FallTime => fallTime;
            public float CorrectionSpeed => correctionSpeed;
            public float MovingSway => movingSway;
            public float IdleSway => idleSway;
            public Vector2 SwayDirectionInterval => swayDirectionInterval;
            public float TiltAcceleration => tiltAcceleration;

            internal void Validate()
            {
                if (swayDirectionInterval.x < 0.05f) swayDirectionInterval.x = 0.05f;
                if (swayDirectionInterval.y < swayDirectionInterval.x) swayDirectionInterval.y = swayDirectionInterval.x;
            }
        }

        /// <summary>겉모습 연출(판정과 무관).</summary>
        [Serializable]
        public sealed class PresentationSettings
        {
            [Tooltip("균형이 끝(±100)일 때 몸 기울기(도).")]
            [SerializeField, Range(0f, 90f)] float maxTiltAngle = 25f;

            [Tooltip("몸 기울기가 목표를 따라가는 빠르기. 클수록 바로, 작을수록 부드럽게.")]
            [SerializeField, Min(0.1f)] float tiltFollowSharpness = 10f;

            [Tooltip("추락해 래그돌로 무너질 때 기울어 있던 쪽으로 밀어 주는 속도(m/s). 0이면 그냥 무너진다.")]
            [SerializeField, Min(0f)] float ragdollTiltPush = 1.5f;

            public float MaxTiltAngle => maxTiltAngle;
            public float TiltFollowSharpness => tiltFollowSharpness;
            public float RagdollTiltPush => ragdollTiltPush;
        }

        /// <summary>온라인 동기화.</summary>
        [Serializable]
        public sealed class SyncSettings
        {
            [Tooltip("균형 값이 이만큼(게이지 ±100 기준) 바뀌어야 다른 화면에 다시 보낸다. 작을수록 기울기가 촘촘히 맞고 전송이 늘어난다.")]
            [SerializeField, Min(0f)] float balanceSendThreshold = 0.5f;

            public float BalanceSendThreshold => balanceSendThreshold;
        }
    }
}
