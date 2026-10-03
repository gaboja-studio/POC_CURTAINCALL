using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Player
{
    /// <summary>
    /// 입력 에셋을 읽어 매 프레임 <see cref="PlayerInputFrame"/>(공통 역할 값)으로 바꾼다.
    /// 어떤 키가 어떤 역할인지는 입력 에셋에서 정한다. 다른 기능은 키를 직접 읽지 말고 <see cref="Current"/>만 읽는다.
    /// </summary>
    [DefaultExecutionOrder(-100)] // 조작 규칙보다 먼저 갱신
    public class PlayerInputReader : MonoBehaviour
    {
        const string DefaultAssetPath = "Input/PlayerControls";
        const string CommonMap = "Common";

        [Tooltip("비워 두면 Resources/Input/PlayerControls를 쓴다.")]
        [SerializeField] InputActionAsset inputAsset;

        InputActionAsset actions; // 플레이어마다 따로 켜고 끌 수 있게 복사본을 쓴다
        InputActionMap commonMap;
        InputAction move;
        InputAction posture;
        InputAction action;
        InputAction auxLeft;
        InputAction auxRight;
        InputAction interact;
        float interactPressedAt = -1f; // 누르고 있지 않으면 음수
        bool interactLongSent;

        /// <summary>이번 프레임의 입력(공통 역할 값).</summary>
        public PlayerInputFrame Current { get; private set; }

        void Awake()
        {
            var source = inputAsset != null ? inputAsset : Resources.Load<InputActionAsset>(DefaultAssetPath);
            if (source == null)
            {
                Debug.LogError($"[PlayerInputReader] 입력 에셋을 찾지 못했습니다: Resources/{DefaultAssetPath}", this);
                enabled = false;
                return;
            }

            actions = Instantiate(source);
            commonMap = actions.FindActionMap(CommonMap, true);
            move = commonMap.FindAction("Move", true);
            posture = commonMap.FindAction("Posture", true);
            action = commonMap.FindAction("Action", true);
            auxLeft = commonMap.FindAction("DirectionLeft", true);
            auxRight = commonMap.FindAction("DirectionRight", true);
            interact = commonMap.FindAction("Interact", true);
        }

        void OnEnable() => commonMap?.Enable();

        void OnDisable()
        {
            commonMap?.Disable();
            Current = default;
            interactPressedAt = -1f;
        }

        void OnDestroy()
        {
            if (actions != null) Destroy(actions);
        }

        void Update()
        {
            ReadInteract(out bool tapped, out bool longPressed, out float holdProgress);

            Current = new PlayerInputFrame
            {
                Move = move.ReadValue<float>(),
                Posture = posture.ReadValue<float>(),
                ActionPressed = action.WasPressedThisFrame(),
                AuxDirection = (auxRight.IsPressed() ? 1 : 0) - (auxLeft.IsPressed() ? 1 : 0),
                InteractTapped = tapped,
                InteractLongPressed = longPressed,
                InteractHoldProgress = holdProgress,
            };
        }

        /// <summary>짧게 = 기준 시간 전에 뗌(뗀 프레임), 길게 = 기준 시간에 닿음(그 프레임, 뗄 때는 아무것도 안 나감).</summary>
        void ReadInteract(out bool tapped, out bool longPressed, out float holdProgress)
        {
            tapped = longPressed = false;
            holdProgress = 0f;

            if (interact.WasPressedThisFrame())
            {
                interactPressedAt = Time.time;
                interactLongSent = false;
            }

            if (interactPressedAt < 0f) return;

            float held = Time.time - interactPressedAt;
            if (interact.IsPressed())
            {
                float longPressTime = GameSettings.Base.Input.LongPressTime; // 길게 누르기 기준은 세팅(입력)
                holdProgress = interactLongSent ? 1f : Mathf.Clamp01(held / longPressTime);
                if (!interactLongSent && held >= longPressTime)
                {
                    longPressed = true;
                    interactLongSent = true;
                }
                return;
            }

            // 뗐다
            if (!interactLongSent) tapped = true;
            interactPressedAt = -1f;
        }
    }
}
