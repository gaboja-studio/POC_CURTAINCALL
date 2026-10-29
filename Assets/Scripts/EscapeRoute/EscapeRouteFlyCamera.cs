using CurtainCall.LevelPreview;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.EscapeRoute
{
    /// <summary>탈출로 배경 검토 전용 자유 비행. 실제 플레이어 이동·네트워크 규칙은 변경하지 않는다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(1000)]
    public sealed class EscapeRouteFlyCamera : MonoBehaviour
    {
        [Tooltip("비행 중 정지할 기존 걷기 카메라.")]
        [SerializeField] LevelPreviewCamera groundCamera;
        [Tooltip("비행 시작 위치의 플레이어. 비행 중에는 이 씬 인스턴스만 비활성화한다.")]
        [SerializeField] PlayerController previewPlayer;
        [Tooltip("플레이어 발에서 비행을 시작할 눈높이(m).")]
        [SerializeField, Min(0f)] float eyeHeight = 1.5916f;
        [Tooltip("기본 비행 속도(m/s).")]
        [SerializeField, Min(.1f)] float moveSpeed = 12f;
        [Tooltip("Shift를 누를 때의 속도 배율.")]
        [SerializeField, Min(1f)] float fastMultiplier = 4f;
        [Tooltip("마우스 1픽셀당 시점 회전각(도).")]
        [SerializeField, Min(0f)] float sensitivity = .1f;

        bool initialized;
        bool flying;
        bool groundWasEnabled;
        bool playerWasActive;
        bool hadAssignedScheme;
        PlayerControlScheme savedScheme;
        float yaw;
        float pitch;

        void Start()
        {
            initialized = true;
            BeginFlight();
        }

        void OnEnable()
        {
            if (initialized) BeginFlight();
        }

        void BeginFlight()
        {
            if (previewPlayer == null || groundCamera == null)
            {
                Debug.LogWarning("탈출로 비행 카메라에 Player와 기존 둘러보기 카메라를 연결하세요.", this);
                enabled = false;
                return;
            }
            groundWasEnabled = groundCamera.enabled;
            playerWasActive = previewPlayer.gameObject.activeSelf;
            hadAssignedScheme = previewPlayer.HasAssignedScheme;
            savedScheme = previewPlayer.Scheme;
            yaw = previewPlayer.transform.eulerAngles.y;
            pitch = 0;
            transform.SetPositionAndRotation(previewPlayer.transform.position + Vector3.up * eyeHeight,
                Quaternion.Euler(pitch, yaw, 0));

            // Stop the walking camera's LateUpdate and the unused player's gravity/input.
            // These are runtime changes only; scene references and the shared prefab stay intact.
            groundCamera.enabled = false;
            previewPlayer.gameObject.SetActive(false);
            flying = true;
            SetCursor(true);
        }

        void Update()
        {
            if (!flying || !Application.isFocused) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetCursor(false);
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) SetCursor(true);
                return;
            }
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue() * sensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
            if (keyboard == null) return;
            float forward = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            float right = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            float vertical = (keyboard.spaceKey.isPressed ? 1 : 0)
                - (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ? 1 : 0);
            var direction = transform.forward * forward + transform.right * right + Vector3.up * vertical;
            bool fast = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            transform.position += Vector3.ClampMagnitude(direction, 1)
                * (moveSpeed * (fast ? fastMultiplier : 1) * Time.unscaledDeltaTime);
        }

        void OnApplicationFocus(bool focused)
        {
            if (flying && !focused) SetCursor(false);
        }

        void OnDisable()
        {
            if (!flying) return;
            flying = false;
            SetCursor(false);
            if (previewPlayer != null)
            {
                previewPlayer.gameObject.SetActive(playerWasActive);
                previewPlayer.SetScheme(hadAssignedScheme ? savedScheme : null);
            }
            if (groundCamera != null) groundCamera.enabled = groundWasEnabled;
        }

        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
