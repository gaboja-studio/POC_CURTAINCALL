using System;
using CurtainCall.Cameras.Requests;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace CurtainCall.Cameras
{
    /// <summary>공용 시점 요청 진입점. 대상 선택은 LocalPlayerCamera·CameraSpectator가 맡는다.</summary>
    [DefaultExecutionOrder(-75)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineFollow), typeof(CinemachineRotationComposer))]
    public sealed class CameraViewDirector : MonoBehaviour
    {
        struct Request
        {
            public CameraViewPreset Preset;
            public Vector3 Forward;
        }

        [SerializeField] CameraViewSettings settings;
        readonly CameraRequestResolver<Object, Request> requests = new();
        CinemachineCamera virtualCamera;
        CinemachineFollow follow;
        CinemachineRotationComposer composer;
        CameraViewPreset fallback;
        Object winner;
        CameraViewPreset selected;
        CameraViewKind selectedKind;
        Vector3 selectedForward = Vector3.forward;
        NetworkPlayer local;
        PlayerMover mover;
        float yaw;
        float pitch;
        bool firstPerson;
        bool inputBlocked;
        bool cursorOwned;
        bool cursorReleased;
        CursorLockMode previousLock;
        bool previousVisible;
        float transitionStarted;
        float transitionDuration;
        Vector3 transitionPosition;
        Quaternion transitionRotation;
        float transitionFov;
        CameraState lastOutput;
        bool hasOutput;

        public static CameraViewDirector Current { get; private set; }
        /// <summary>pipeline 판정 직전 콘텐츠가 동적 요청 값을 갱신할 기회. 입력 적분은 하지 않는다.</summary>
        public event Action PreparingView;
        public CameraViewPreset CurrentView => selected;
        public CameraViewSettings Settings => settings;
        public NetworkPlayer LocalPlayer => local;
        public bool IsFirstPerson => firstPerson && CanUseLocalView;
        public bool HideLocalModel => IsFirstPerson && selected != null && selected.HideLocalModel;
        public Vector3 ViewForward => CanUseLocalView ? Horizontal(firstPerson
            ? Quaternion.Euler(0f, yaw, 0f) * Vector3.forward
            : (Camera.main != null ? Camera.main.transform.forward : transform.forward)) : Vector3.zero;

        bool CanUseLocalView => local != null && local.isActiveAndEnabled && !IsDead(local);

        void Awake()
        {
            virtualCamera = GetComponent<CinemachineCamera>();
            follow = GetComponent<CinemachineFollow>();
            composer = GetComponent<CinemachineRotationComposer>();
            if (settings == null) settings = Resources.Load<CameraViewSettings>("Settings/Camera/CameraViewSettings");
            if (settings == null || settings.DefaultThirdPerson == null)
            {
                fallback = ScriptableObject.CreateInstance<CameraViewPreset>();
                fallback.hideFlags = HideFlags.DontSave;
            }
        }

        void OnEnable() => Current = this;

        void OnDisable()
        {
            ReleaseCursor();
            requests.Clear();
            firstPerson = false;
            hasOutput = false;
            selected = null;
            winner = null;
            if (Current == this) Current = null;
        }

        void OnDestroy()
        {
            if (fallback != null) Destroy(fallback);
        }

        /// <summary>출처당 하나. 갱신은 등록 순서를 유지하고 해제 후 재등록만 최근 요청이 된다.</summary>
        public void RequestView(Object source, CameraViewPreset preset, int priority, Vector3 worldForward = default)
        {
            if (source == null) return;
            if (preset == null) { ReleaseView(source); return; }
            requests.Set(source, new Request { Preset = preset, Forward = Horizontal(worldForward) }, priority);
        }

        public void ReleaseView(Object source) => requests.Remove(source);

        /// <summary>037 등 조작 통합에서 사용할 차단 진입점. 현재 공간 규칙에는 의존하지 않는다.</summary>
        public void SetInputBlocked(bool blocked) => inputBlocked = blocked;

        public void SetLocalPlayer(NetworkPlayer player)
        {
            if (local == player) return;
            ReleaseCursor();
            requests.Clear();
            local = player;
            mover = player != null ? player.GetComponent<PlayerMover>() : null;
            selected = null;
            winner = null;
            firstPerson = false;
            hasOutput = false;
        }

        void Update()
        {
            PrepareView();
            if (!IsFirstPerson) { ReleaseCursor(); return; }
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                cursorReleased = true;
                UnlockCursor();
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerBlocked(mouse.position.ReadValue()))
                cursorReleased = false;

            bool lockNow = Application.isFocused && !cursorReleased && !inputBlocked;
            Cursor.lockState = lockNow ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockNow;
            float limit = Mathf.Clamp(selected.PitchLimit, 0f, 89f);
            if (lockNow && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * Mathf.Max(0f, selected.MouseSensitivity);
                yaw = Mathf.Repeat(yaw + delta.x, 360f);
                pitch -= delta.y;
            }
            pitch = Mathf.Clamp(pitch, -limit, limit);
        }

        /// <summary>Fixed/Smart/Late 모두 pipeline 직전에 호출. 입력 적분은 Update에서만 한다.</summary>
        internal void PrepareView()
        {
            if (!isActiveAndEnabled) return;
            PreparingView?.Invoke();
            requests.RemoveWhere((source, value) => !SourceActive(source) || value.Preset == null);
            bool allowFirst = CanUseLocalView;
            bool found = requests.TryResolve(out var source, out var request,
                value => allowFirst || value.Preset.Kind != CameraViewKind.FirstPerson);
            var preset = found ? request.Preset : DefaultView;
            if (preset == null) return;
            bool useFirst = allowFirst && preset.Kind == CameraViewKind.FirstPerson;
            if (source != winner || preset != selected || preset.Kind != selectedKind || useFirst != firstPerson)
            {
                bool enterFirst = useFirst && !firstPerson;
                winner = source;
                selected = preset;
                selectedKind = preset.Kind;
                firstPerson = useFirst;
                transitionStarted = Time.unscaledTime;
                transitionDuration = hasOutput ? Mathf.Max(0f, preset.TransitionDuration) : 0f;
                if (hasOutput)
                {
                    var view = Camera.main;
                    transitionPosition = view != null ? view.transform.position : lastOutput.GetFinalPosition();
                    transitionRotation = view != null ? view.transform.rotation : lastOutput.GetFinalOrientation();
                    transitionFov = view != null ? view.fieldOfView : lastOutput.Lens.FieldOfView;
                }
                if (enterFirst)
                {
                    Vector3 forward = Camera.main != null ? Horizontal(Camera.main.transform.forward) : Vector3.zero;
                    if (forward == Vector3.zero) forward = Horizontal(local.transform.forward);
                    yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                    pitch = 0f;
                    AcquireCursor();
                }
                if (!useFirst) ReleaseCursor();
            }
            selectedForward = found && request.Forward != Vector3.zero ? request.Forward : Vector3.forward;
            var framing = useFirst ? DefaultView : preset;
            if (framing != null)
            {
                follow.FollowOffset = Vector3.up * framing.Height - selectedForward * Mathf.Max(0f, framing.Distance);
                composer.TargetOffset = new Vector3(0f, framing.LookHeight, framing.LookAhead);
            }
            virtualCamera.Lens.FieldOfView = Mathf.Clamp(preset.FieldOfView, 1f, 179f);
        }

        internal void ApplyPose(ref CameraState state)
        {
            if (!isActiveAndEnabled || selected == null) return;
            if (IsFirstPerson)
            {
                float height = selected.EyeHeight > 0f ? selected.EyeHeight : mover != null ? mover.BodyHeight * 0.9f : 1.6f;
                SetPose(ref state, local.transform.position + Vector3.up * height, Quaternion.Euler(pitch, yaw, 0f));
                state.ReferenceLookAt = CameraState.kNoPoint;
            }
            state.Lens.FieldOfView = Mathf.Clamp(selected.FieldOfView, 1f, 179f);
            if (transitionDuration > 0f)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - transitionStarted) / transitionDuration);
                if (t < 1f)
                {
                    SetPose(ref state, Vector3.Lerp(transitionPosition, state.GetFinalPosition(), t),
                        Quaternion.Slerp(transitionRotation, state.GetFinalOrientation(), t));
                    state.Lens.FieldOfView = Mathf.Lerp(transitionFov, state.Lens.FieldOfView, t);
                }
            }
            lastOutput = state;
            hasOutput = true;
        }

        CameraViewPreset DefaultView => settings != null && settings.DefaultThirdPerson != null ? settings.DefaultThirdPerson : fallback;

        static void SetPose(ref CameraState state, Vector3 position, Quaternion rotation)
        {
            state.RawPosition = position;
            state.RawOrientation = rotation;
            state.PositionCorrection = Vector3.zero;
            state.OrientationCorrection = Quaternion.identity;
            state.Lens.Dutch = 0f;
        }

        static bool IsDead(NetworkPlayer player) => player.State == PlayerState.Fallen || player.State == PlayerState.Retired ||
            (player.TryGetComponent(out PlayerBalance balance) && balance.HasFallen);

        static bool SourceActive(Object source) => source != null && source switch
        {
            Behaviour behaviour => behaviour.isActiveAndEnabled,
            Component component => component.gameObject.activeInHierarchy,
            GameObject gameObject => gameObject.activeInHierarchy,
            _ => true,
        };

        static Vector3 Horizontal(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }

        static bool PointerBlocked(Vector2 position) =>
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) || CameraCursorExclusion.Contains(position);

        void AcquireCursor()
        {
            if (cursorOwned) return;
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            cursorOwned = true;
            cursorReleased = false;
        }

        static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void ReleaseCursor()
        {
            if (!cursorOwned) return;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
            cursorOwned = false;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && cursorOwned) UnlockCursor();
        }
    }
}
