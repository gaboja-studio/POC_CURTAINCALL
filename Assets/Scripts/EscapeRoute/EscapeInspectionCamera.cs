using CurtainCall.Cameras;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.EscapeRoute
{
    /// <summary>호스트 점유 해제 후 실제 소유자 플레이어와 카메라를 함께 이동시키는 씬 전용 검토 모드.</summary>
    [RequireComponent(typeof(Camera), typeof(CinemachineBrain), typeof(EscapeInspectionSession))]
    [DefaultExecutionOrder(1500)]
    public sealed class EscapeInspectionCamera : MonoBehaviour
    {
        [SerializeField] float speed = 12f;
        [SerializeField] float sensitivity = .1f;
        CinemachineBrain brain;
        CameraViewDirector director;
        EscapeInspectionSession session;
        NetworkPlayer player;
        PlayerMover mover;
        PlayerBalance balance;
        HumanCannonPilot pilot;
        CourseControlSwitcher switcher;
        bool brainEnabled, directorEnabled, pilotEnabled, manualExit;
        CursorLockMode previousLock;
        bool previousVisible;
        float yaw, pitch, eyeHeight;
        string notice;
        public bool IsInspecting { get; private set; }
        public static Vector3 PlayerFeet(Vector3 eye, float height) => eye - Vector3.up * height;

        void Awake() { brain = GetComponent<CinemachineBrain>(); session = GetComponent<EscapeInspectionSession>(); }
        void OnEnable() { InputGate.AddReleaseKeys(Key.Digit1); GetComponent<EscapeInspectionSession>().LocalChanged += ModeChanged; }
        void Update()
        {
            var keys = Keyboard.current;
            if (!Application.isFocused || keys == null) return;
            if (keys.digit1Key.wasPressedThisFrame && !session.Pending)
            {
                if (IsInspecting)
                {
                    if (CanReturn()) { manualExit = true; if (!session.Request(false)) manualExit = false; }
                    else notice = "벽·천장에서 조금 떨어진 뒤 1을 눌러 주세요.";
                }
                else if (!InputGate.IsRequested && !session.Request(true)) notice = "게임 시작 후 정상 상태의 플레이어로 사용할 수 있습니다.";
            }
            if (!IsInspecting) return;
            var mouse = Mouse.current;
            if (keys.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse != null && mouse.rightButton.wasPressedThisFrame) LockCursor();
                return;
            }
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * sensitivity;
                yaw += delta.x; pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
            }
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            float forward = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
            float side = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
            float up = (keys.spaceKey.isPressed ? 1 : 0) - (keys.leftCtrlKey.isPressed ? 1 : 0);
            var direction = transform.forward * forward + transform.right * side + Vector3.up * up;
            transform.position += Vector3.ClampMagnitude(direction, 1f) * speed
                * (keys.leftShiftKey.isPressed ? 4f : 1f) * Time.unscaledDeltaTime;
        }
        void ModeChanged(bool active)
        {
            if (active) BeginInspection(); else EndInspection(manualExit);
            manualExit = false;
        }
        void BeginInspection()
        {
            player = NetworkPlayer.Local;
            if (player == null || !player.TryGetComponent(out mover)) { session.Request(false); return; }
            player.TryGetComponent(out pilot); player.TryGetComponent(out balance);
            pilotEnabled = pilot != null && pilot.enabled;
            if (pilot != null) { pilot.ClearMotionForInspection(); pilot.enabled = false; }
            switcher = TightropeCourse.Current != null ? TightropeCourse.Current.GetComponent<CourseControlSwitcher>() : null;
            switcher?.SuspendForReview(true);
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            director = CameraViewDirector.Current;
            brainEnabled = brain.enabled; directorEnabled = director != null && director.enabled;
            brain.enabled = false;
            if (director != null) director.enabled = false;
            InputGate.SetBlocking(this, true);
            var firstPerson = director != null && director.Settings != null ? director.Settings.FirstPerson : null;
            eyeHeight = firstPerson != null && firstPerson.EyeHeight > 0f
                ? firstPerson.EyeHeight * mover.PostureHeightScale : mover.BodyHeight * .9f;
            // 3인칭 외줄 카메라에서 전환해도 캐릭터의 위치는 유지한다.
            transform.position = player.transform.position + Vector3.up * eyeHeight;
            yaw = transform.eulerAngles.y; pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
            mover.Relocate(player.transform.position, Quaternion.Euler(0, yaw, 0));
            IsInspecting = true; notice = null;
            SyncPlayer(); LockCursor();
        }
        void LateUpdate() { if (IsInspecting) SyncPlayer(); }
        void SyncPlayer()
        {
            if (mover == null || player == null) return;
            mover.ClearPendingInput(); mover.Simulated = false; mover.Body.enabled = false;
            if (balance != null) balance.IsSuspended = true;
            // 기존 소유자 NetworkTransform으로 공유하며 재시작 출발점은 바꾸지 않는다.
            player.transform.SetPositionAndRotation(PlayerFeet(transform.position, eyeHeight), Quaternion.Euler(0, yaw, 0));
        }
        bool CanReturn()
        {
            if (mover == null) return false;
            Vector3 feet = PlayerFeet(transform.position, eyeHeight);
            float radius = Mathf.Max(.01f, mover.BodyRadius - .04f), height = Mathf.Max(radius * 2, mover.BodyHeight);
            foreach (var hit in Physics.OverlapCapsule(feet + Vector3.up * (radius + .04f),
                feet + Vector3.up * (height - radius), radius, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(player.transform) && !Physics.GetIgnoreLayerCollision(player.gameObject.layer, hit.gameObject.layer)) return false;
            return true;
        }
        void EndInspection(bool keepFlightPosition)
        {
            if (!IsInspecting) return;
            IsInspecting = false;
            if (mover != null)
            {
                if (keepFlightPosition) mover.Relocate(PlayerFeet(transform.position, eyeHeight), Quaternion.Euler(0, yaw, 0));
                mover.Body.enabled = true; mover.Simulated = true; mover.ResetExternalMotion(true);
            }
            if (balance != null) balance.IsSuspended = false;
            if (pilot != null) pilot.enabled = pilotEnabled;
            switcher?.SuspendForReview(false);
            if (director != null) director.enabled = directorEnabled;
            if (keepFlightPosition && director != null && director.enabled) director.SetLookRotation(transform.rotation);
            if (brain != null) brain.enabled = brainEnabled;
            InputGate.SetBlocking(this, false); InputGate.RequireRelease();
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
            player = null; mover = null; balance = null; pilot = null; notice = null;
        }
        static void LockCursor() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void OnDisable()
        {
            if (session != null) { session.LocalChanged -= ModeChanged; if (session.LocalActive) session.Request(false); }
            EndInspection(false);
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused && IsInspecting) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        void OnGUI()
        {
            if (!string.IsNullOrEmpty(notice)) GUI.Box(new Rect(10, Screen.height - 50, 700, 40), notice);
        }
    }
}
