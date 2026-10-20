using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.LevelPreview
{
    /// <summary>
    /// 맵 배치 확인용 1인칭 카메라(2026-10-09 PM @MoHoDu). 플레이어 눈높이에 붙어 마우스로 둘러보며, 내 모델은 숨긴다.
    /// 시작할 때 플레이어 조작을 <see cref="LevelPreviewControlScheme"/>(바라보는 방향 기준 이동·점프)로 바꾼다.
    /// 마우스 커서는 잠긴다. Esc로 풀고, 화면을 클릭하면 다시 잠긴다.
    /// 맵 배치 씬(Assets/Scenes/Levels/)의 Main Camera에 두며, 네트워크 없이 씬에 놓인 Player를 직접 따라간다. 게임 카메라가 아니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelPreviewCamera : MonoBehaviour
    {
        [Tooltip("따라갈 플레이어(씬에 놓은 Player 프리팹).")]
        [SerializeField] PlayerController player;

        [Tooltip("서 있을 때 눈높이. 발(플레이어 원점)에서 잰 높이(m). 0이면 몸통 높이의 90%. 앉으면 앉기 높이 비율만큼 함께 낮아진다.")]
        [SerializeField, Min(0f)] float eyeHeight;

        [Tooltip("마우스 감도(마우스 이동 1픽셀당 도).")]
        [SerializeField, Min(0f)] float sensitivity = 0.1f;

        [Tooltip("위아래로 볼 수 있는 최대 각도(도).")]
        [SerializeField, Range(0f, 89f)] float pitchLimit = 85f;

        [Tooltip("내 모델을 숨긴다(그림자 포함). 끄면 1인칭에서 내 몸이 화면을 가린다.")]
        [SerializeField] bool hideOwnModel = true;

        /// <summary>지금 바라보는 수평 방향(정규화). 카메라가 없거나 꺼져 있으면 Vector3.zero.</summary>
        public static Vector3 ViewForward { get; private set; }

        PlayerMover mover;
        LevelPreviewControlScheme scheme;
        float yaw;
        float pitch;
        bool modelHidden;

        void Start()
        {
            if (player == null)
            {
                Debug.LogWarning($"{nameof(LevelPreviewCamera)}: 따라갈 Player가 비어 있습니다. Inspector에서 씬의 Player를 지정하세요.", this);
                enabled = false;
                return;
            }

            mover = player.GetComponent<PlayerMover>();
            yaw = player.transform.eulerAngles.y;
            scheme = ScriptableObject.CreateInstance<LevelPreviewControlScheme>();
            scheme.hideFlags = HideFlags.DontSave;
            player.SetScheme(scheme);
            LockCursor(true);
        }

        void OnDisable()
        {
            ViewForward = Vector3.zero;
            LockCursor(false);
            // 내가 끼운 규칙만 뺀다. 다른 기능이 이미 바꿔 끼웠으면 건드리지 않는다
            if (scheme != null && player != null && player.Scheme == scheme) player.SetScheme(null);
        }

        void OnDestroy()
        {
            if (scheme != null) Destroy(scheme);
        }

        void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) LockCursor(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame) LockCursor(true);

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue() * sensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            }

            ViewForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        }

        void LateUpdate()
        {
            // 모델은 PlayerModelSlot이 실행 중에 만들므로 생긴 뒤에 숨긴다.
            if (hideOwnModel && !modelHidden) HideModel();

            // 몸통 높이 기준은 앉으면 이미 낮아지므로, 고정 눈높이만 자세 비율을 곱한다(두 번 줄이지 않는다)
            float height = eyeHeight > 0f
                ? eyeHeight * (mover != null ? mover.PostureHeightScale : 1f)
                : (mover != null ? mover.BodyHeight * 0.9f : 1.6f);
            transform.SetPositionAndRotation(player.transform.position + Vector3.up * height, Quaternion.Euler(pitch, yaw, 0f));
        }

        void HideModel()
        {
            var renderers = player.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            foreach (var r in renderers) r.enabled = false;
            modelHidden = true;
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
