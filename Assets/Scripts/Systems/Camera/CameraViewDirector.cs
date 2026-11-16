using System;
using CurtainCall.Cameras.Requests;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.Cameras
{
    /// <summary>공용 시점 요청 진입점. 대상 선택은 LocalPlayerCamera·CameraSpectator가 맡는다.</summary>
    [DefaultExecutionOrder(-75)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineFollow), typeof(CinemachineRotationComposer))]
    public sealed partial class CameraViewDirector : MonoBehaviour
    {
        struct Request
        {
            public CameraViewPreset Preset;
            public Vector3 Forward;
            public bool FollowHeading;
            public bool FollowHeadingMotion;
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
        Vector3 followedHeading;
        float headingTurnStart, headingTurnDelta, headingTurnApplied;
        NetworkPlayer local;
        PlayerMover mover;
        float yaw;
        float pitch;
        bool firstPerson;
        bool freeThirdPerson;
        bool spectatorOrbit;
        bool watchingCapture;
        NetworkPlayer framedTarget;
        Vector3 captureSide;
        const float CaptureFramingTransition = .35f;
        bool inputBlocked;
        bool preserveCursorInFixedView;
        bool cursorOwned;
        bool cursorReleased;
        bool cursorLocked;
        bool uiCursorRequested;
        int keyboardSyncs;
        float nextKeyboardSync;
        CursorLockMode previousLock;
        bool previousVisible;
        float transitionStarted;
        float transitionDuration;
        Vector3 transitionPosition;
        Quaternion transitionRotation;
        float transitionFov;
        CameraState lastOutput;
        bool hasOutput;
        float collisionPullIn;
        float collisionElevation;
        float collisionHold;
        Vector3 lastCollisionPivot, lastCollisionDirection;
        int collisionFrame = -1;
        const float CollisionRadius = .25f;
        const float CollisionPadding = .05f;
        const float CollisionLookAhead = .28f;
        const float CollisionPredictionTime = .09f;
        const float CollisionEnterDamping = .20f;
        const float CollisionExitDamping = .55f;
        const float CollisionHoldTime = .08f;
        const float MinimumHangCameraDistance = 1.65f;
        RaycastHit[] wallHits = new RaycastHit[32];
        Collider[] pivotOverlaps = new Collider[8];
        const float DeathDropDuration = 0.6f;
        const float DeathDropDistance = 1.5f;
        const float DeathWatchDuration = 1.5f;
        bool deathSeen;
        bool deathView;
        bool hasDeathOutput;
        float deathStarted;
        float deathDropDistance;
        CameraState deathOutput;

        internal bool IsShowingDeathFall => deathView && Time.unscaledTime - deathStarted < DeathWatchDuration;

        public static CameraViewDirector Current { get; private set; }
        /// <summary>pipeline 판정 직전 콘텐츠가 동적 요청 값을 갱신할 기회. 입력 적분은 하지 않는다.</summary>
        public event Action PreparingView;
        public CameraViewPreset CurrentView => selected;
        public CameraViewSettings Settings => settings;
        public NetworkPlayer LocalPlayer => local;
        public bool IsFirstPerson => firstPerson && CanUseLocalView;
        /// <summary>3인칭 비고정: 마우스로 시선을 돌리고 카메라는 캐릭터 뒤에서 시선을 따라 돈다.</summary>
        public bool IsFreeThirdPerson => freeThirdPerson && CanUseLocalView;
        /// <summary>마우스 시선 시점(1인칭·3인칭 비고정). 기본 이동 규칙이 <see cref="ViewForward"/> 기준으로 움직인다.</summary>
        public bool UsesMouseLook => (MouseLook && CanUseLocalView) || spectatorOrbit;
        public bool HideLocalModel => IsFirstPerson && selected != null && selected.HideLocalModel;
        public Vector3 ViewForward => CanUseLocalView ? Horizontal(MouseLook
            ? Quaternion.Euler(0f, yaw, 0f) * Vector3.forward
            : (Camera.main != null ? Camera.main.transform.forward : transform.forward)) : Vector3.zero;

        bool MouseLook => firstPerson || freeThirdPerson || spectatorOrbit;
        Transform SpectatorAnchor => CameraSpectator.EscapeCaptureAnchor != null ? CameraSpectator.EscapeCaptureAnchor
            : CameraSpectator.Target != null ? CameraSpectator.Target.transform : local != null ? local.transform : null;
        public bool IsSpectatorOrbit => spectatorOrbit;
        internal void SpectatorTargetChanged() { ResetWallCollision(); transitionDuration = 0; }
        bool CanUseLocalView => local != null && local.isActiveAndEnabled && !IsDead(local);
        internal bool CanHideNearbyModel => CanUseLocalView && !IsFirstPerson && !CameraSpectator.IsSpectating && !deathView;

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

        void OnEnable()
        {
            Current = this;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        void OnActiveSceneChanged(Scene previous, Scene next)
        {
            ResetWallCollision();
            ResetDeathView();
            hasOutput = false;
            transitionDuration = 0f;
        }

        void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ResetWallCollision();
            ResetDeathView();
            ReleaseCursor();
            requests.Clear();
            firstPerson = freeThirdPerson = spectatorOrbit = false;
            preserveCursorInFixedView = false;
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
            => RequestViewWithHeading(source, preset, priority, worldForward, false);

        internal void RequestViewWithHeading(Object source, CameraViewPreset preset, int priority, Vector3 worldForward,
            bool followHeading, bool followHeadingMotion = false)
        {
            if (source == null) return;
            if (preset == null) { ReleaseView(source); return; }
            requests.Set(source, new Request { Preset = preset, Forward = Horizontal(worldForward), FollowHeading = followHeading,
                FollowHeadingMotion = followHeadingMotion }, priority);
        }

        public void ReleaseView(Object source) => requests.Remove(source);

        /// <summary>037 등 조작 통합에서 사용할 차단 진입점. 현재 공간 규칙에는 의존하지 않는다.</summary>
        public void SetInputBlocked(bool blocked) => inputBlocked = blocked;

        /// <summary>구조 중 고정 카메라에서도 기존 커서 잠금을 유지한다.</summary>
        public void SetPreserveCursorInFixedView(bool value) => preserveCursorInFixedView = value;

        /// <summary>외부 검토 카메라에서 돌아올 때 현재 시선 방향을 이어받는다.</summary>
        public void SetLookRotation(Quaternion rotation)
        {
            PrepareView();
            Vector3 angles = rotation.eulerAngles;
            yaw = angles.y;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0, angles.x), -89f, 89f);
        }

        public void SetLocalPlayer(NetworkPlayer player)
        {
            if (local == player) return;
            ResetWallCollision();
            ResetDeathView();
            ReleaseCursor();
            requests.Clear();
            local = player;
            mover = player != null ? player.GetComponent<PlayerMover>() : null;
            selected = null;
            winner = null;
            firstPerson = freeThirdPerson = spectatorOrbit = false;
            preserveCursorInFixedView = false;
            hasOutput = false;
        }

        void Update()
        {
            SyncKeyboardIfDue();
            PrepareView();
            bool uiRequested = InputGate.IsRequested;
            bool uiClosing = uiCursorRequested && !uiRequested;
            uiCursorRequested = uiRequested;
            bool uiBlocked = InputGate.IsBlocked;
            // 창을 닫은 ESC/클릭은 수동 커서 해제로 재사용하지 않는다.
            // 여러 창이 열렸다면 마지막 요청이 사라지고 키를 뗀 뒤 조작을 복귀한다.
            if (uiRequested || uiClosing) cursorReleased = false;
            if (!UsesMouseLook)
            {
                if (!preserveCursorInFixedView || uiBlocked || uiRequested) ReleaseCursor();
                if (uiRequested) UnlockCursor();
                return;
            }
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (!uiBlocked && !uiClosing && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                cursorReleased = true;
                UnlockCursor();
            }
            else if (!uiBlocked && !uiClosing && mouse != null && mouse.leftButton.wasPressedThisFrame)
                cursorReleased = PointerBlocked(mouse.position.ReadValue());

            bool lockNow = Application.isFocused && !cursorReleased && !inputBlocked && !uiBlocked;
            if (lockNow != cursorLocked) ScheduleKeyboardSync();
            cursorLocked = lockNow;
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
            // Cinemachine은 편집 모드에도 pipeline을 호출한다. 플레이 전용 요청·커서는
            // Awake 초기화가 없는 편집/Prefab Stage 오브젝트에 적용하지 않는다.
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled) return;
            // 관전 대상·시점 요청이 바뀌기 전에 직전 Finalize 출력을 한 번만 잡는다.
            RefreshDeathView();
            PreparingView?.Invoke();
            requests.RemoveWhere((source, value) => !SourceActive(source) || value.Preset == null);
            // 1인칭·3인칭 비고정은 로컬 캐릭터 기준이라 관전·소멸 중에는 후보에서 뺀다
            bool allowLocal = CanUseLocalView;
            bool found = requests.TryResolve(out var source, out var request,
                value => IsViewAllowed(SceneManager.GetActiveScene(), value.Preset.Kind, allowLocal));
            bool useSpectator = CameraSpectator.IsSpectating && !IsShowingDeathFall && SpectatorAnchor != null;
            // Corpse-local room/course requests must not frame a living spectator target.
            if (useSpectator) { found = false; source = null; request = default; }
            var preset = found ? request.Preset : DefaultView;
            if (preset == null) return;
            bool useFirst = allowLocal && preset.Kind == CameraViewKind.FirstPerson;
            bool useFree = allowLocal && preset.Kind == CameraViewKind.FreeThirdPerson;
            if (source != winner || preset != selected || preset.Kind != selectedKind || useFirst != firstPerson || useFree != freeThirdPerson || useSpectator != spectatorOrbit)
            {
                // 마우스 시선 시점에 새로 들어올 때만 시선을 현재 화면 방향으로 맞춘다(1인칭↔비고정은 시선 이어짐)
                bool enterFirst = (useFirst || useFree || useSpectator) && !MouseLook;
                winner = source;
                followedHeading = Vector3.zero;
                headingTurnDelta = headingTurnApplied = 0f;
                selected = preset;
                selectedKind = preset.Kind;
                firstPerson = useFirst;
                freeThirdPerson = useFree;
                spectatorOrbit = useSpectator;
                transitionStarted = Time.unscaledTime;
                transitionDuration = hasOutput ? Mathf.Max(0f, preset.TransitionDuration) : 0f;
                if (useSpectator && CameraSpectator.Target == null) transitionDuration = 0;
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
                    if (useSpectator && CameraSpectator.Target == null && hasDeathOutput)
                        forward = Horizontal(deathOutput.GetFinalOrientation() * Vector3.forward);
                    if (forward == Vector3.zero) forward = Horizontal(local.transform.forward);
                    yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                    pitch = useSpectator && CameraSpectator.Target == null && hasDeathOutput
                        ? Mathf.DeltaAngle(0, deathOutput.GetFinalOrientation().eulerAngles.x) : 0f;
                    AcquireCursor();
                }
                if (!MouseLook && !preserveCursorInFixedView) ReleaseCursor();
            }
            selectedForward = found && request.Forward != Vector3.zero ? request.Forward : Vector3.forward;
            Vector3 heading = useFree && found && request.FollowHeading ? request.Forward : Vector3.zero;
            UpdateFollowedHeading(heading, request.FollowHeadingMotion);
            var framing = useFirst ? DefaultView : preset;
            if (framing != null)
            {
                float postureDrop = !useSpectator && mover != null ? framing.LookHeight * (1f - mover.PostureHeightScale) : 0f;
                follow.FollowOffset = Vector3.up * (framing.Height - postureDrop) - selectedForward * Mathf.Max(0f, framing.Distance);
                composer.TargetOffset = new Vector3(0f, framing.LookHeight - postureDrop, framing.LookAhead);
            }
            virtualCamera.Lens.FieldOfView = Mathf.Clamp(preset.FieldOfView, 1f, 179f);
        }

        void UpdateFollowedHeading(Vector3 heading, bool followHeadingMotion)
        {
            if (heading == Vector3.zero)
            {
                followedHeading = Vector3.zero;
                headingTurnDelta = headingTurnApplied = 0f;
                return;
            }
            if (followedHeading == Vector3.zero || !followHeadingMotion && Vector3.Dot(heading, followedHeading) < .9999f)
            {
                followedHeading = heading;
                headingTurnStart = Time.unscaledTime;
                headingTurnApplied = 0f;
                headingTurnDelta = Mathf.DeltaAngle(yaw, Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg);
            }
            else if (followHeadingMotion)
            {
                // Keep the player's mouse offset as the moving mount turns.
                yaw += Vector3.SignedAngle(followedHeading, heading, Vector3.up);
                followedHeading = heading;
            }
            float duration = Mathf.Max(.01f, CurtainCall.Settings.GameSettings.Tightrope.Camera.FramingSmoothTime);
            float progress = Mathf.Clamp01((Time.unscaledTime - headingTurnStart) / duration);
            float applied = headingTurnDelta * Mathf.SmoothStep(0f, 1f, progress);
            yaw = Mathf.Repeat(yaw + applied - headingTurnApplied, 360f);
            headingTurnApplied = applied;
        }

        internal void ApplyPose(ref CameraState state, float deltaTime)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || selected == null) return;
            if (spectatorOrbit && SpectatorAnchor != null)
            {
                bool capture = CameraSpectator.TryGetCaptureFocus(out var focus, out var head);
                bool sameTarget = framedTarget == CameraSpectator.Target;
                if (capture && (!watchingCapture || !sameTarget))
                {
                    // 마네킹은 골반 쪽에서 몸 위로 숙인다. 머리-발 방향의 옆에서 봐야 몸이 가리지 않는다. 지금 카메라에 가까운 쪽을 고른다.
                    captureSide = Vector3.Cross(Vector3.up, head).normalized;
                    if (captureSide == Vector3.zero) captureSide = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
                    if (hasOutput && Vector3.Dot(lastOutput.GetFinalPosition() - focus, captureSide) < 0f) captureSide = -captureSide;
                }
                // 잡힌 대상으로 바뀌는 순간 카메라가 튀지 않게 짧게 잇는다.
                if (capture != watchingCapture && hasOutput && sameTarget) BeginTransition(CaptureFramingTransition);
                watchingCapture = capture;
                framedTarget = CameraSpectator.Target;
                if (CameraSpectator.Target == null && hasDeathOutput)
                    SetPose(ref state, deathOutput.GetFinalPosition() + Vector3.down * deathDropDistance, Quaternion.Euler(pitch,yaw,0));
                else if (CameraSpectator.EscapeCaptureAnchor != null)
                {
                    Vector3 center = SpectatorAnchor.position + Vector3.up * 2.5f;
                    Vector3 position = FreeThirdPersonPose(selected, center - Vector3.up * selected.LookHeight, yaw, pitch, out _);
                    position = center + (position - center) * 1.5f;
                    SetPose(ref state, position, Quaternion.LookRotation(center - position, Vector3.up));
                }
                else if (capture) SetPose(ref state, CaptureWatchPose(selected, focus, captureSide, pitch, out var watch), watch);
                else SetPose(ref state, FreeThirdPersonPose(selected, SpectatorAnchor.position, yaw, pitch, out var rotation), rotation);
                state.ReferenceLookAt = CameraState.kNoPoint;
            }
            else if (IsFirstPerson)
            {
                // 몸통 높이 기준은 앉으면 이미 낮아지므로, 프리셋 고정 눈높이만 자세 비율을 곱한다(두 번 줄이지 않는다)
                float height = selected.EyeHeight > 0f ? selected.EyeHeight * (mover != null ? mover.PostureHeightScale : 1f)
                    : mover != null ? mover.BodyHeight * 0.9f : 1.6f;
                SetPose(ref state, local.transform.position + Vector3.up * height, Quaternion.Euler(pitch, yaw, 0f));
                state.ReferenceLookAt = CameraState.kNoPoint;
            }
            else if (IsFreeThirdPerson)
            {
                float drop = mover != null ? selected.LookHeight * (1f - mover.PostureHeightScale) : 0f;
                SetPose(ref state, FreeThirdPersonPose(selected, local.transform.position - Vector3.up * drop, yaw, pitch, out var rotation), rotation);
                state.ReferenceLookAt = CameraState.kNoPoint;
            }
            state.Lens.FieldOfView = Mathf.Clamp(selected.FieldOfView, 1f, 179f);
            if (spectatorOrbit && CameraSpectator.Target == null && hasDeathOutput) state.Lens = deathOutput.Lens;
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
            if (deathView && !spectatorOrbit)
            {
                // Follow/Aim/Noise 뒤 Finalize에서 덮어쓴다. 여러 pipeline 호출도 같은 절대 시간·고정 기준점을 쓴다.
                if (!hasDeathOutput)
                {
                    deathOutput = state;
                    hasDeathOutput = true;
                    PrepareDeathOutput();
                }
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - deathStarted) / DeathDropDuration));
                state.Lens = deathOutput.Lens;
                SetPose(ref state, deathOutput.GetFinalPosition() + Vector3.down * (deathDropDistance * t),
                    deathOutput.GetFinalOrientation());
                state.ReferenceLookAt = CameraState.kNoPoint;
            }
            else if ((spectatorOrbit || UsesWallCollision(SceneManager.GetActiveScene())) && (CanUseLocalView || (spectatorOrbit && CameraSpectator.Target != null)) && !IsFirstPerson)
                ApplyWallCollision(ref state, deltaTime);
            else
                ResetWallCollision();
            lastOutput = state;
            hasOutput = true;
        }

        /// <summary>
        /// 모든 플레이 씬에 같은 벽 충돌을 적용한다. 연출/1인칭은 호출부의 시점 조건을 따른다.
        /// </summary>
        public static bool UsesWallCollision(Scene scene) => scene.IsValid();

        /// <summary>줄 소실 등으로 우선 요청이 해제돼도 탈출로는 3인칭을 유지한다.
        /// 제외된 요청은 지우지 않으므로 다른 공간으로 돌아가면 원래 우선순위를 다시 쓸 수 있다.</summary>
        public static bool IsViewAllowed(Scene scene, CameraViewKind kind, bool allowLocal)
        {
            if (!allowLocal) return kind == CameraViewKind.ThirdPerson;
            return kind != CameraViewKind.FirstPerson || !scene.IsValid()
                || !scene.name.StartsWith("escape_route", StringComparison.Ordinal);
        }

        void ResetWallCollision()
        {
            collisionFrame = -1;
            collisionPullIn = collisionElevation = collisionHold = 0f;
            reframing = false;
            lastFramingArm = Vector3.zero;
        }

        void ApplyWallCollision(ref CameraState state, float deltaTime)
        {
            var anchor = spectatorOrbit ? SpectatorAnchor : local != null ? local.transform : null;
            if (anchor == null) return;
            // 앉으면 기준점도 몸 높이만큼 낮춘다. 낮은 통로(환풍구 등)에서 기준점이 천장 밖으로 나가 카메라가 벽 밖을 비추지 않게 한다.
            var anchorMover = spectatorOrbit ? null : anchor.GetComponent<PlayerMover>();
            float lookHeight = Mathf.Max(.1f, selected.LookHeight) * (anchorMover != null ? anchorMover.PostureHeightScale : 1f);
            if (CameraSpectator.EscapeCaptureAnchor != null) lookHeight = 2.5f;
            Vector3 focus = KeepPivotUnderCeiling(anchor.position, anchor.position + Vector3.up * lookHeight);
            var subject = anchor.GetComponent<NetworkPlayer>();
            var hangs = CurtainCall.Tightrope.TightropeHangSystem.Current;
            bool hanging = hangs != null && hangs.IsHanging(subject);
            // Cannon seats and low beams can also contain the pivot. Clear the query
            // origin for every third-person view before casting the camera arm.
            Vector3 pivot = HangingCameraPivot(focus);
            Vector3 originalPosition = state.GetFinalPosition();
            Vector3 offset = originalPosition - pivot;
            float desired = offset.magnitude;
            if (desired < .001f) { ResetWallCollision(); return; }
            if (deltaTime < 0f) ResetWallCollision();
            bool reset = collisionFrame < 0;
            float step = collisionFrame == Time.frameCount ? 0f : Mathf.Max(0f, deltaTime);
            Vector3 direction = offset / desired;
            float safe = WallDistance(pivot, direction, desired);
            Vector3 originalDirection = direction;
            if (hanging || collisionElevation != 0f)
                direction = DampCollisionElevation(pivot, direction, desired, hanging, reset, step, ref safe);

            // The wider cast starts tightening the arm before the physical near-plane limit.
            // Cap the extra pull-in so a buffer touching a nearby side wall cannot collapse the arm.
            float cushioned = SweepWallDistance(pivot, direction, safe, CollisionRadius + CollisionLookAhead, false);
            cushioned = Mathf.Clamp(cushioned, Mathf.Max(0f, safe - CollisionLookAhead), safe);
            if (!reset && step > 0f && collisionElevation == 0f)
                cushioned = Mathf.Min(cushioned, PredictCollisionDistance(pivot, direction, desired, safe, step));
            if (reset || step > 0f)
            {
                lastCollisionPivot = pivot;
                lastCollisionDirection = direction;
            }
            float distance = DampCollisionDistance(desired, safe, cushioned, reset, step);
            if (hanging) distance = Mathf.Max(distance, Mathf.Min(safe, MinimumHangCameraDistance));
            Vector3 position = pivot + direction * distance;
            if (!hanging && (!spectatorOrbit || !watchingCapture && CameraSpectator.EscapeCaptureAnchor == null))
            {
                float bodyHeight = anchorMover != null && anchorMover.Body != null ? anchorMover.BodyHeight : lookHeight * 1.25f;
                position = KeepSubjectVisible(anchor.position, bodyHeight, pivot, originalPosition, position, safe, reset, step);
            }
            else reframing = false;
            if (reset || step > 0f)
            {
                lastFramingArm = position - pivot;
                lastFramingPosition = position;
            }
            collisionFrame = Time.frameCount;
            state.PositionCorrection += position - originalPosition;
            if ((Vector3.Cross(position - focus, originalDirection)).sqrMagnitude > .0001f
                || (pivot - focus).sqrMagnitude > .0001f)
            {
                // Preserve the subject's screen framing when the arm has to slide around a beam.
                Vector3 before = focus - originalPosition;
                Vector3 after = focus - position;
                if (before.sqrMagnitude > .0001f && after.sqrMagnitude > .0001f)
                {
                    Quaternion aim = Quaternion.LookRotation(after, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(before, Vector3.up));
                    state.RawOrientation = aim * state.GetCorrectedOrientation();
                    state.OrientationCorrection = Quaternion.identity;
                }
            }
        }

        float PredictCollisionDistance(Vector3 pivot, Vector3 direction, float desired, float safe, float step)
        {
            Vector3 motion = pivot - lastCollisionPivot;
            float angle = Vector3.Angle(lastCollisionDirection, direction);
            if (motion.sqrMagnitude > 1f || angle > 50f) return safe; // cuts and teleports

            // A thin post can cut across the arm in a single frame while orbiting. Ease in
            // toward the collision limit a few frames ahead in the current direction of travel.
            float lead = CollisionPredictionTime / step;
            Vector3 advance = Vector3.ClampMagnitude(motion * lead, .55f);
            float turn = Mathf.Min(18f, angle * lead);
            if (advance.sqrMagnitude < .0004f && turn < 1f) return safe;
            Vector3 futureDirection = turn < 1f ? direction
                : Quaternion.AngleAxis(turn, Vector3.Cross(lastCollisionDirection, direction).normalized) * direction;
            return Mathf.Lerp(safe, WallDistance(pivot + advance, futureDirection, desired), .8f);
        }

        float DampCollisionDistance(float desired, float safe, float cushioned, bool reset, float step)
        {
            float requested = desired - cushioned;
            if (reset) collisionPullIn = requested;
            else if (requested >= collisionPullIn || collisionHold <= 0f)
            {
                float damping = requested > collisionPullIn ? CollisionEnterDamping : CollisionExitDamping;
                collisionPullIn += Damper.Damp(requested - collisionPullIn, damping, step);
            }

            collisionHold = Mathf.Max(0f, collisionHold - step);
            float required = desired - safe;
            if (required > collisionPullIn)
            {
                collisionPullIn = required;
                collisionHold = CollisionHoldTime;
            }
            collisionPullIn = Mathf.Min(desired, collisionPullIn);
            return desired - collisionPullIn;
        }

        Vector3 DampCollisionElevation(Vector3 pivot, Vector3 direction, float desired,
            bool hanging, bool reset, float step, ref float safe)
        {
            float preferredSafe = safe;
            Vector3 preferred = hanging ? HangingCameraDirection(pivot, direction, desired, ref preferredSafe) : direction;
            float elevation = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            float bend = Mathf.Asin(Mathf.Clamp(preferred.y, -1f, 1f)) * Mathf.Rad2Deg - elevation;
            collisionElevation = reset ? bend : collisionElevation + Damper.Damp(bend - collisionElevation, CollisionExitDamping, step);
            if (Mathf.Abs(collisionElevation) < .01f) { collisionElevation = 0f; return direction; }
            Vector3 horizontal = Horizontal(direction);
            if (horizontal == Vector3.zero) return direction;
            Vector3 damped = ArmAtElevation(horizontal, elevation + collisionElevation);
            float dampedSafe = WallDistance(pivot, damped, desired);
            // A low landing can leave the intermediate angle inside the subject's body.
            if (hanging && dampedSafe < .9f && preferredSafe > dampedSafe)
            {
                collisionElevation = bend;
                safe = preferredSafe;
                return preferred;
            }
            safe = dampedSafe;
            return damped;
        }

        static Vector3 ArmAtElevation(Vector3 horizontal, float angle)
        {
            float radians = Mathf.Clamp(angle, -89f, 89f) * Mathf.Deg2Rad;
            return horizontal * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians);
        }

        // 발에서 기준점까지 위쪽이 막혀 있으면 기준점을 천장 아래(카메라 구 반지름만큼)로 내린다.
        Vector3 KeepPivotUnderCeiling(Vector3 feet, Vector3 pivot)
        {
            Vector3 from = feet + Vector3.up * .1f;
            float reach = pivot.y - from.y + CollisionRadius + CollisionPadding;
            if (reach <= 0f) return pivot;
            int count = Physics.RaycastNonAlloc(from, Vector3.up, wallHits, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float top = pivot.y;
            for (int i = 0; i < count; i++)
                if (BlocksCamera(wallHits[i].collider)) top = Mathf.Min(top, wallHits[i].point.y - CollisionRadius - CollisionPadding);
            pivot.y = Mathf.Max(from.y, top);
            return pivot;
        }

        Vector3 HangingCameraPivot(Vector3 pivot)
        {
            // The chest anchor can sit inside a landing/cannon or within the cast radius
            // of a support beam. A cast starting there reports zero in every direction.
            // Move only the camera's query origin out of authored boxes, never the player.
            float radius = CollisionRadius + CollisionPadding;
            for (int pass = 0; pass < 4; pass++)
            {
                int count;
                while ((count = Physics.OverlapSphereNonAlloc(pivot, radius, pivotOverlaps,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) == pivotOverlaps.Length)
                    Array.Resize(ref pivotOverlaps, pivotOverlaps.Length * 2);
                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    if (pivotOverlaps[i] is not BoxCollider box || !BlocksCamera(box)) continue;
                    Vector3 point = box.transform.InverseTransformPoint(pivot) - box.center;
                    Vector3 half = box.size * .5f;
                    Vector3 closest = Vector3.Min(half, Vector3.Max(-half, point));
                    Vector3 surface = box.transform.TransformPoint(box.center + closest);
                    Vector3 away = pivot - surface;
                    float distance = away.magnitude;
                    if (distance < .00001f)
                    {
                        var scale = box.transform.lossyScale;
                        int axis = 0;
                        for (int j = 1; j < 3; j++)
                            if ((half[j] - Mathf.Abs(point[j])) * Mathf.Abs(scale[j])
                                < (half[axis] - Mathf.Abs(point[axis])) * Mathf.Abs(scale[axis])) axis = j;
                        closest[axis] = point[axis] < 0 ? -half[axis] : half[axis];
                        surface = box.transform.TransformPoint(box.center + closest);
                        away = (surface - pivot).normalized;
                    }
                    else away /= distance;
                    if (distance >= radius) continue;
                    pivot = surface + away * (radius + .001f);
                    moved = true;
                }
                if (!moved) break;
            }
            return pivot;
        }

        Vector3 HangingCameraDirection(Vector3 pivot, Vector3 direction, float desired, ref float safe)
        {
            if (safe >= MinimumHangCameraDistance) return direction;
            Vector3 horizontal = Horizontal(direction);
            if (horizontal == Vector3.zero) return direction;
            Vector3 best = direction;
            float elevation = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            // Search the same vertical orbit: beneath a landing while hanging, or above it
            // during the catch/pull transition. Wall collision still checks every candidate.
            for (float delta = 15f; delta <= 180f; delta += 15f)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float angle = elevation + sign * delta;
                    if (angle < -104f || angle > 104f) continue;
                    Vector3 candidate = ArmAtElevation(horizontal, angle);
                    float distance = WallDistance(pivot, candidate, desired);
                    if (distance <= safe) continue;
                    best = candidate;
                    safe = distance;
                    if (safe >= MinimumHangCameraDistance) return best;
                }
            }
            return best;
        }

        static bool BlocksCamera(Collider obstacle)
        {
            if (obstacle == null || obstacle is CharacterController) return false;
            if (CameraSpectator.EscapeCaptureAnchor != null && obstacle.transform.IsChildOf(CameraSpectator.EscapeCaptureAnchor)) return false;
            var subject = CameraSpectator.IsSpectating ? CameraSpectator.Target : NetworkPlayer.Local;
            var held = subject != null ? CurtainCall.HumanCannon.HumanCannonCarry.Held(subject.OwnerClientId) : null;
            if (held != null && obstacle.transform.IsChildOf(held.CarryRoot)) return false;
            // These invisible walls prevent boarding a moving lift. They are not visible
            // geometry and must not squeeze the carry camera into the passenger enclosure.
            if (CurtainCall.HumanCannon.HumanCannonRouteSystem.Current?.IsBoardingBarrier(obstacle) == true) return false;
            if (CurtainCall.Tightrope.TightropeCourse.IsRopeSurface(obstacle)) return false;
            var ropes = CurtainCall.EscapeRoute.EscapeCooperativeRopes.Current;
            if (ropes != null && ropes.IsTimedSupport(obstacle)) return false;
            return obstacle.GetComponentInParent<NetworkPlayer>() == null
                && (obstacle.attachedRigidbody == null || obstacle.attachedRigidbody.isKinematic);
        }

        float WallDistance(Vector3 pivot, Vector3 direction, float desired)
            => SweepWallDistance(pivot, direction, desired, CollisionRadius, true);

        float SweepWallDistance(Vector3 pivot, Vector3 direction, float desired, float radius, bool centerRay)
        {
            if (desired <= 0f) return 0f;
            float safe = desired;
            // 벽·고정 지형만 막는다. 트리거, 플레이어와 이동 가능한 물체는 가림 처리에 맡긴다.
            int count;
            do
            {
                count = Physics.SphereCastNonAlloc(pivot, radius, direction, wallHits, desired,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count < wallHits.Length) break;
                Array.Resize(ref wallHits, wallHits.Length * 2);
            } while (true);
            for (int i = 0; i < count; i++)
            {
                var hit = wallHits[i];
                if (!BlocksCamera(hit.collider)) continue;
                safe = Mathf.Min(safe, Mathf.Max(0f, hit.distance - CollisionPadding));
            }
            // 구가 벽에 이미 겹친 상태에서 시작하면 SphereCast가 놓칠 수 있다.
            while (centerRay)
            {
                count = Physics.RaycastNonAlloc(pivot, direction, wallHits, desired,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count < wallHits.Length) break;
                Array.Resize(ref wallHits, wallHits.Length * 2);
            }
            for (int i = 0; centerRay && i < count; i++)
            {
                var hit = wallHits[i];
                if (!BlocksCamera(hit.collider)) continue;
                safe = Mathf.Min(safe, Mathf.Max(0f, hit.distance - radius - CollisionPadding));
            }
            return CameraCollisionSurface.LimitDistance(SceneManager.GetActiveScene(), pivot, direction,
                radius + CollisionPadding, safe);
        }

        void RefreshDeathView()
        {
            bool dead = local != null && IsDead(local);
            if (!dead)
            {
                if (deathView) ResumeFromDeathView();
                ResetDeathView();
                return;
            }
            if (deathSeen) return;
            deathSeen = deathView = true;
            deathStarted = Time.unscaledTime;
            deathOutput = lastOutput;
            hasDeathOutput = hasOutput;
            if (hasDeathOutput) PrepareDeathOutput();
        }

        void PrepareDeathOutput()
        {
            Vector3 position = deathOutput.GetFinalPosition();
            bool grounded = false;
            RaycastHit ground = default;
            foreach (var hit in Physics.RaycastAll(local.transform.position + Vector3.up * .5f,
                Vector3.down, 1.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!IsDeathSurface(hit.collider) || hit.normal.y < .5f) continue;
                if (!grounded || hit.distance < ground.distance) { grounded = true; ground = hit; }
            }
            if (grounded)
            {
                // 지면 사망은 낙하 연출로 더 내려가지 않는다. 바닥 위에서 마지막 사망 위치를 비춘다.
                Vector3 target = ground.point + Vector3.up * .35f;
                Vector3 away = Horizontal(position - target);
                if (away == Vector3.zero) away = -Horizontal(local.transform.forward);
                Vector3 flatOffset = Vector3.ProjectOnPlane(position - target, Vector3.up);
                if (flatOffset.sqrMagnitude < 2.5f * 2.5f)
                {
                    position.x = target.x + away.x * 2.5f;
                    position.z = target.z + away.z * 2.5f;
                }
                position.y = Mathf.Max(position.y, ground.point.y + 1.2f);
                SetPose(ref deathOutput, position, Quaternion.LookRotation(target - position, Vector3.up));
                deathDropDistance = 0f;
                return;
            }
            deathDropDistance = DeathDropDistance;
            // 공중 사망도 카메라 하강 경로에 바닥이 있으면 그 위에서 멈춘다.
            foreach (var hit in Physics.SphereCastAll(position, .25f, Vector3.down, DeathDropDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (IsDeathSurface(hit.collider) && hit.normal.y > .5f)
                    deathDropDistance = Mathf.Min(deathDropDistance, Mathf.Max(0f, hit.distance - .05f));
        }

        bool IsDeathSurface(Collider collider) => collider != null && !(collider is CharacterController)
            && !collider.transform.IsChildOf(local.transform)
            && (collider.attachedRigidbody == null || collider.attachedRigidbody.isKinematic);

        /// <summary>짧은 낙하 관찰 뒤 생존자 관전으로만 해제한다. 생존자가 없으면 고정 출력을 유지한다.</summary>
        internal void ResumeFromDeathView()
        {
            if (!deathView) return;
            deathView = hasDeathOutput = false;
            deathOutput = default;
            // Camera.main은 Brain 갱신 전 화면일 수 있다. 방금 고정한 pipeline 출력에서 이어간다.
            transitionStarted = Time.unscaledTime;
            transitionDuration = hasOutput && selected != null ? Mathf.Max(0f, selected.TransitionDuration) : 0f;
            transitionPosition = lastOutput.GetFinalPosition();
            transitionRotation = lastOutput.GetFinalOrientation();
            transitionFov = lastOutput.Lens.FieldOfView;
        }

        void ResetDeathView()
        {
            deathSeen = deathView = hasDeathOutput = false;
            deathOutput = default;
            deathStarted = 0f;
            deathDropDistance = 0f;
        }

        CameraViewPreset DefaultView => settings != null && settings.DefaultThirdPerson != null ? settings.DefaultThirdPerson : fallback;

        /// <summary>
        /// 3인칭 비고정 카메라 위치·방향. 캐릭터 발 + 바라보기 높이를 중심으로, 고정 3인칭과 같은 거리·높이(pitch 0일 때)를
        /// 시선(yaw·pitch)만큼 돌린 자리에서 시선 방향 앞(바라보기 앞 거리)을 본다.
        /// </summary>
        public static Vector3 FreeThirdPersonPose(CameraViewPreset preset, Vector3 feet, float yaw, float pitch, out Quaternion rotation)
        {
            Vector3 pivot = feet + Vector3.up * preset.LookHeight;
            var orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = pivot + orbit * new Vector3(0f, preset.Height - preset.LookHeight, -Mathf.Max(0f, preset.Distance));
            Vector3 look = pivot + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * preset.LookAhead - position;
            rotation = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look, Vector3.up) : orbit;
            return position;
        }

        /// <summary>
        /// 잡혀 바닥에 눕혀진 관전 대상을 옆에서 비스듬히 내려다보는 자리(사용자 2026-11-11).
        /// 연출이 짧아(약 5초) 좌우는 고정하고, 마우스 상하로 20~70° 사이 높이만 조절한다.
        /// </summary>
        public static Vector3 CaptureWatchPose(CameraViewPreset preset, Vector3 focus, Vector3 side, float pitch, out Quaternion rotation)
        {
            float elevation = Mathf.Clamp(pitch + 30f, 20f, 70f) * Mathf.Deg2Rad;
            Vector3 arm = side * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation);
            Vector3 position = focus + arm * Mathf.Clamp(preset.Distance, 2.5f, 4f);
            rotation = Quaternion.LookRotation(focus - position, Vector3.up);
            return position;
        }

        void BeginTransition(float duration)
        {
            transitionStarted = Time.unscaledTime;
            transitionDuration = duration;
            transitionPosition = lastOutput.GetFinalPosition();
            transitionRotation = lastOutput.GetFinalOrientation();
            transitionFov = lastOutput.Lens.FieldOfView;
        }

        static void SetPose(ref CameraState state, Vector3 position, Quaternion rotation)
        {
            state.RawPosition = position;
            state.RawOrientation = rotation;
            state.PositionCorrection = Vector3.zero;
            state.OrientationCorrection = Quaternion.identity;
            state.Lens.Dutch = 0f;
        }

        static bool IsDead(NetworkPlayer player) => player.State == PlayerState.EscapeCaptured
            ? CurtainCall.EscapeRoute.EscapeUnderstageChase.Current != null && CurtainCall.EscapeRoute.EscapeUnderstageChase.Current.HasReleasedCaptured(player)
            : player.State == PlayerState.Fallen || player.State == PlayerState.Retired ||
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

        internal static bool PointerBlocked(Vector2 position) =>
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
            cursorLocked = false;
            ScheduleKeyboardSync();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && cursorOwned) UnlockCursor();
            if (focused) ScheduleKeyboardSync();
        }

        /// <summary>
        /// 커서 잠금이 바뀌거나 포커스를 되찾으면 키보드를 실제 키 상태로 다시 맞춘다(Task-045).
        /// 에디터에서 시점 전환 중 키를 떼면 게임 쪽 키가 눌린 채 남아 계속 미끄러지는 일이 있었다(실제 키보드에서만 재현).
        /// 실제로 누르고 있는 키는 그대로 남는다. 바로 다음 프레임과 잠시 뒤 두 번 맞춘다.
        /// </summary>
        void ScheduleKeyboardSync()
        {
            keyboardSyncs = 2;
            nextKeyboardSync = Time.unscaledTime;
        }

        void SyncKeyboardIfDue()
        {
            if (keyboardSyncs == 0 || Time.unscaledTime < nextKeyboardSync) return;
            keyboardSyncs--;
            nextKeyboardSync = Time.unscaledTime + 0.25f;
            foreach (var device in InputSystem.devices)
            {
                if (device is not Keyboard) continue;
                var sync = RequestSyncCommand.Create();
                device.ExecuteCommand(ref sync);
            }
        }
    }
}
