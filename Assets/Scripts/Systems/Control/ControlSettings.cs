using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 조작 설정(공용). 조작 규칙을 끼우지 않은 공간에서 쓰는 **기본 이동 규칙**을 고른다(2026-10-14 PM).
    /// 에셋: Assets/Resources/Settings/Control/ControlSettings.asset. 메뉴 Tools/CurtainCall/조작/기본 이동 규칙 선택으로 연다.
    /// 플레이 중 바꾸면 규칙을 끼우지 않은 플레이어에게 다음 프레임부터 반영된다(<see cref="PlayerController"/>가 매 프레임 읽음).
    /// 비어 있거나 에셋이 없으면 <see cref="PlatformControlScheme"/>(8방향 이동·점프)을 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Control Settings", fileName = "ControlSettings")]
    public sealed class ControlSettings : ScriptableObject
    {
        public const string ResourcePath = "Settings/Control/ControlSettings";

        [Tooltip("조작 규칙을 끼우지 않은 공간에서 쓸 기본 이동 규칙. 비우면 플랫폼 이동(8방향 이동·점프).")]
        [SerializeField] PlayerControlScheme defaultScheme;

        static ControlSettings current;
        static PlayerControlScheme fallbackScheme;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null; // 에셋을 다시 읽는다(도메인 재로드를 끈 경우)

        /// <summary>지금 쓰는 조작 설정. 처음 부를 때 Resources에서 읽는다.</summary>
        public static ControlSettings Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<ControlSettings>(ResourcePath);
                if (current == null)
                {
                    Debug.LogWarning($"[ControlSettings] Resources/{ResourcePath} 설정을 찾지 못해 플랫폼 이동을 기본 이동 규칙으로 씁니다.");
                    current = CreateInstance<ControlSettings>();
                    current.hideFlags = HideFlags.DontSave;
                }
                return current;
            }
        }

        /// <summary>지금의 기본 이동 규칙. 항상 null이 아니다.</summary>
        public static PlayerControlScheme Default => Current.ResolveDefault();

        /// <summary>인스펙터에서 고른 기본 이동 규칙(비었으면 null).</summary>
        public PlayerControlScheme DefaultScheme
        {
            get => defaultScheme;
            set => defaultScheme = value;
        }

        /// <summary>고른 규칙, 비었으면 플랫폼 이동.</summary>
        public PlayerControlScheme ResolveDefault() => defaultScheme != null ? defaultScheme : Fallback;

        static PlayerControlScheme Fallback
        {
            get
            {
                if (fallbackScheme != null) return fallbackScheme;
                fallbackScheme = CreateInstance<PlatformControlScheme>();
                fallbackScheme.name = "PlatformControlScheme (기본)";
                fallbackScheme.hideFlags = HideFlags.DontSave;
                return fallbackScheme;
            }
        }
    }
}
