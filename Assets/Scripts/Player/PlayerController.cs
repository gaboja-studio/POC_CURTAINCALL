using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 입력과 동작 부품을 잇는다. 매 프레임 현재 조작 규칙에 입력을 넘긴다.
    /// 구조: 키 → (입력 에셋) → 공통 역할 입력 → (조작 규칙) → 동작 부품.
    /// 콘텐츠는 <see cref="SetScheme"/>로 자기 조작 규칙을 끼운다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    [DefaultExecutionOrder(-50)] // 입력(-100) 다음, 동작 부품(0) 전
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("시작할 때 쓸 조작 규칙. 비워 두면 외줄 규칙을 쓴다.")]
        [SerializeField] PlayerControlScheme startScheme;

        PlayerInputReader input;
        PlayerControlContext context;

        /// <summary>현재 조작 규칙.</summary>
        public PlayerControlScheme Scheme { get; private set; }

        /// <summary>조작 규칙이 지금 지정해 둔 방향(-1/0/+1). 외줄에서는 옆줄 이동 방향.</summary>
        public int HeldDirection => context != null ? context.HeldDirection : 0;

        /// <summary>조작 규칙을 바꾼다. null이면 입력을 아무 동작에도 연결하지 않는다.</summary>
        public void SetScheme(PlayerControlScheme scheme)
        {
            if (Scheme == scheme) return;
            if (Scheme != null) Scheme.Exit(context);
            Scheme = scheme;
            if (Scheme != null) Scheme.Enter(context);
        }

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            context = new PlayerControlContext(gameObject);

            var scheme = startScheme;
            if (scheme == null)
            {
                scheme = ScriptableObject.CreateInstance<TightropeControlScheme>();
                scheme.name = "TightropeControlScheme (기본)";
            }
            SetScheme(scheme);
        }

        void Update()
        {
            if (Scheme != null)
                Scheme.Tick(context, input.Current);
        }
    }
}
