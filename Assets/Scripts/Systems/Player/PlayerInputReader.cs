using CurtainCall.Cameras;
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
        InputAction turnAround;
        InputAction auxLeft;
        InputAction auxRight;
        InputAction interact;
        InputAction sprint;
        InputAction crouch;
        InputAction uvLight; // 옛 입력 에셋에는 없을 수 있다
        InputAction useDevice;
        double interactPressedAt = -1d;
        bool interactLongSent;
        bool interactTapPending, interactLongPending;
        // 반대 키 동시 입력은 마지막에 누른 방향. 그 키를 떼면 아직 누르고 있는 이전 키 방향으로 돌아간다.
        readonly OppositeKeyAxis moveKeys = new(), postureKeys = new(), auxKeys = new();

        /// <summary>이번 프레임의 입력(공통 역할 값).</summary>
        public PlayerInputFrame Current { get; private set; }
        /// <summary>상호작용 패널을 닫을 때도 사용자가 바꾼 공통 키 매핑을 따른다.</summary>
        public bool WasPressedThisFrame(string actionName) => commonMap?.FindAction(actionName, false)?.WasPressedThisFrame() == true;

        public string BindingLabel(string actionName, string fallback)
        {
            var binding = commonMap?.FindAction(actionName, false);
            if (binding != null)
                for (int i = 0; i < binding.bindings.Count; i++)
                {
                    var entry = binding.bindings[i];
                    if (!entry.isComposite && !entry.isPartOfComposite && !string.IsNullOrEmpty(entry.effectivePath)
                        && (entry.effectivePath.StartsWith("<Keyboard>/") || entry.effectivePath.StartsWith("<Mouse>/")))
                        return InputBindingPreferences.KeyLabel(entry.effectivePath, fallback);
                }
            return fallback;
        }
        public string BindingPartLabel(string actionName, string partName, string fallback)
        {
            var binding = commonMap?.FindAction(actionName, false);
            if (binding != null)
                for (int i = 0; i < binding.bindings.Count; i++)
                {
                    var entry = binding.bindings[i];
                    if (entry.isPartOfComposite && entry.name == partName && !string.IsNullOrEmpty(entry.effectivePath)
                        && entry.effectivePath.StartsWith("<Keyboard>/")) return InputBindingPreferences.KeyLabel(entry.effectivePath, fallback);
                }
            return fallback;
        }

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
            InputBindingPreferences.ApplySavedOverrides(actions);
            InputBindingPreferences.Changed += RefreshBindingPreferences;
            commonMap = actions.FindActionMap(CommonMap, true);
            move = commonMap.FindAction("Move", true);
            posture = commonMap.FindAction("Posture", true);
            action = commonMap.FindAction("Action", true);
            turnAround = commonMap.FindAction("TurnAround", false);
            auxLeft = commonMap.FindAction("DirectionLeft", true);
            auxRight = commonMap.FindAction("DirectionRight", true);
            interact = commonMap.FindAction("Interact", true);
            interact.performed += OnInteractPressed;
            interact.canceled += OnInteractReleased;
            sprint = commonMap.FindAction("Sprint", true);
            crouch = commonMap.FindAction("Crouch", true);
            uvLight = commonMap.FindAction("UvLight", false);
            useDevice = commonMap.FindAction(InputBindingPreferences.DeviceUseAction, false);
        }

        public bool ControlsReleased
        {
            get
            {
                if (commonMap == null) return true;
                foreach (var input in commonMap.actions)
                    foreach (var control in input.controls)
                        if (control.IsActuated()) return false;
                return true;
            }
        }

        public void ClearPendingInput()
        {
            CommonInteractionSelection.Forget(this);
            Current = default;
            interactPressedAt = -1d;
            interactLongSent = interactTapPending = interactLongPending = false;
            moveKeys.Reset(); postureKeys.Reset(); auxKeys.Reset();
            if (TryGetComponent(out PlayerMover mover)) mover.ClearPendingInput();
            if (TryGetComponent(out PlayerBalance balance)) balance.SetCorrectionInput(0f);
        }

        void OnEnable()
        {
            commonMap?.Enable();
            InputGate.Register(this);
        }

        void OnDisable()
        {
            InputGate.Unregister(this);
            commonMap?.Disable();
            ClearPendingInput();
        }

        // Awake~Destroy 동안 구독하여 비활성 Reader도 다음 활성화 전에 최신 키를 가진다.
        void RefreshBindingPreferences()
        {
            bool wasEnabled = commonMap != null && commonMap.enabled;
            commonMap?.Disable();
            InputBindingPreferences.ApplySavedOverrides(actions);
            ClearPendingInput();
            if (wasEnabled) commonMap.Enable();
        }

        void OnDestroy()
        {
            InputBindingPreferences.Changed -= RefreshBindingPreferences;
            if (interact != null)
            {
                interact.performed -= OnInteractPressed;
                interact.canceled -= OnInteractReleased;
            }
            if (actions != null) Destroy(actions);
        }

        void Update()
        {
            if (InputGate.IsBlocked)
            {
                ClearPendingInput();
                return;
            }
            ReadInteract(Time.realtimeSinceStartupAsDouble, out bool tapped, out bool longPressed, out float holdProgress);

            Current = new PlayerInputFrame
            {
                Move = moveKeys.Read(move),
                Posture = postureKeys.Read(posture),
                ActionPressed = action.WasPressedThisFrame(),
                TurnAroundPressed = turnAround != null && turnAround.WasPressedThisFrame(),
                AuxDirection = auxKeys.Update(auxLeft.IsPressed(), auxRight.IsPressed()),
                InteractTapped = tapped,
                InteractHeld = interact.IsPressed(),
                InteractLongPressed = longPressed,
                InteractHoldProgress = holdProgress,
                SprintHeld = sprint.IsPressed(),
                CrouchHeld = crouch.IsPressed(),
                UvLightHeld = uvLight != null && uvLight.IsPressed(),
                UseDevicePressed = ReadDeviceUse(),
            };
            CommonInteractionSelection.Selected(this);
        }

        bool ReadDeviceUse()
        {
            if (useDevice == null || !useDevice.WasPressedThisFrame()) return false;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || useDevice.bindings.Count == 0
                || useDevice.bindings[0].effectivePath != InputBindingPreferences.LeftClickPath) return true;
            if (Cursor.lockState == CursorLockMode.Locked) return true;
            return (CameraViewDirector.Current == null || !CameraViewDirector.Current.UsesMouseLook)
                && !CameraViewDirector.PointerBlocked(mouse.position.ReadValue());
        }

        void OnInteractPressed(InputAction.CallbackContext context)
        {
            if (interactPressedAt >= 0d) return;
            interactPressedAt = context.time;
            interactLongSent = false;
        }

        void OnInteractReleased(InputAction.CallbackContext context)
        {
            if (interactPressedAt >= 0d && !interactLongSent)
            {
                // A slow frame can receive the press and release together. Use the event timestamps.
                if (context.time - interactPressedAt >= GameSettings.Base.Input.LongPressTime) interactLongPending = true;
                else interactTapPending = true;
            }
            interactPressedAt = -1d;
            interactLongSent = false;
        }

        /// <summary>기준에 도달하거나 늦게 뗀 프레임에 홀드를 한 번 보낸다. 기준 전에 떼면 탭이다.</summary>
        void ReadInteract(double now, out bool tapped, out bool longPressed, out float holdProgress)
        {
            longPressed = interactLongPending;
            tapped = interactTapPending && !longPressed;
            interactTapPending = interactLongPending = false;
            holdProgress = 0f;
            if (interactPressedAt < 0d || !interact.IsPressed()) return;

            double held = now - interactPressedAt;
            float threshold = GameSettings.Base.Input.LongPressTime;
            holdProgress = interactLongSent ? 1f : Mathf.Clamp01((float)(held / threshold));
            if (interactLongSent || held < threshold) return;
            tapped = false;
            longPressed = interactLongSent = true;
        }
    }
}
