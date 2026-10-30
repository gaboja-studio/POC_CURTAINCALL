using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 입력과 동작 부품을 잇는다. 매 프레임 현재 조작 규칙에 입력을 넘긴다.
    /// 구조: 키 → (입력 에셋) → 공통 역할 입력 → (조작 규칙) → 동작 부품.
    /// 콘텐츠는 <see cref="SetScheme"/>로 자기 조작 규칙을 끼운다. 끼운 규칙이 없으면
    /// 시작 규칙 → 씬 규칙(<see cref="SceneControlScheme"/>) → 기본 이동 규칙(<see cref="ControlSettings"/>) 순으로 쓴다.
    /// 이 클래스는 구체 규칙(외줄 등)을 모른다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    [DefaultExecutionOrder(-50)] // 입력(-100) 다음, 동작 부품(0) 전
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("이 플레이어만 쓸 시작 조작 규칙(테스트용). 비워 두면 씬 규칙 → 기본 이동 규칙(조작 설정)을 쓴다.")]
        [SerializeField] PlayerControlScheme startScheme;

        public static System.Func<PlayerControlContext, PlayerInputFrame, bool> InputInterceptor;

        PlayerInputReader input;
        PlayerControlContext context;
        PlayerControlScheme assigned; // 콘텐츠가 SetScheme으로 끼운 규칙

        /// <summary>현재 조작 규칙.</summary>
        public PlayerControlScheme Scheme { get; private set; }

        /// <summary>콘텐츠가 규칙을 직접 끼웠는지. 아니면 기본 쪽 규칙을 쓰는 중이다.</summary>
        public bool HasAssignedScheme => assigned != null;

        /// <summary>조작 규칙이 지금 지정해 둔 방향(-1/0/+1). 외줄에서는 옆줄 이동 방향.</summary>
        public int HeldDirection => context != null ? context.HeldDirection : 0;

        /// <summary>콘텐츠의 조작 규칙을 끼운다. null이면 끼운 규칙을 빼고 기본 쪽 규칙(시작 → 씬 → 기본 이동)으로 돌아간다.</summary>
        public void SetScheme(PlayerControlScheme scheme)
        {
            assigned = scheme;
            Apply(ChooseScheme(assigned, startScheme, SceneControlScheme.Current, ControlSettings.Default));
        }

        /// <summary>쓸 규칙을 고른다: 끼운 규칙 → 시작 규칙 → 씬 규칙 → 기본 이동 규칙 중 처음으로 비어 있지 않은 것.</summary>
        public static PlayerControlScheme ChooseScheme(PlayerControlScheme assigned, PlayerControlScheme start,
            PlayerControlScheme scene, PlayerControlScheme projectDefault)
        {
            if (assigned != null) return assigned;
            if (start != null) return start;
            if (scene != null) return scene;
            return projectDefault;
        }

        void Apply(PlayerControlScheme scheme)
        {
            if (context == null || Scheme == scheme) return; // Awake 전이면 Awake에서 적용
            if (Scheme != null) Scheme.Exit(context);
            Scheme = scheme;
            if (Scheme != null) Scheme.Enter(context);
        }

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            context = new PlayerControlContext(gameObject);
            SetScheme(assigned);
        }

        void Update()
        {
            if (assigned == null) SetScheme(null); // 씬 규칙·기본 이동 규칙이 바뀌면 바로 반영
            if (Scheme != null && (InputGate.IsBlocked || InputInterceptor == null || !InputInterceptor(context, input.Current)))
                Scheme.Tick(context, input.Current);
        }
    }
}
