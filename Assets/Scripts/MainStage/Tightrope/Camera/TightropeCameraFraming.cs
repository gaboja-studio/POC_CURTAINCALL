using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>
    /// 외줄 전용 구도·가림 예외 공급자. 실제 코스가 있을 때만 공용 카메라에 요청한다.
    /// 플랫폼은 1인칭, 줄 탑승·점프·목마는 마우스 공전 3인칭이다. 관전은 고정 구도를 사용한다.
    /// </summary>
    [RequireComponent(typeof(CameraViewDirector))]
    public sealed partial class TightropeCameraFraming : CinemachineExtension
    {
        CameraViewDirector director;
        CameraOcclusionFade fade;
        CameraViewPreset preset;
        CinemachineCamera virtualCamera;
        NetworkPlayer framedPlayer;
        TightropeFramingMath.Frame currentFrame;
        bool hasFrame;
        bool firstPersonFraming;
        bool hasSharedGroup;
        Vector3 sharedOffset, sharedCenter;
        readonly List<TightropeFramingMath.LayerSubject> layerSubjects = new();
        readonly List<ulong> sharedMembers = new();
        readonly List<NetworkPlayer> groupPlayers = new();
        int updatedFrame = -1;
        readonly CameraOcclusionRules occlusion = new();

        public bool HasSharedGroup => hasSharedGroup;
        public Vector3 SharedCenter => sharedCenter;

        protected override void Awake()
        {
            base.Awake();
            director = GetComponent<CameraViewDirector>();
            fade = GetComponent<CameraOcclusionFade>();
            virtualCamera = GetComponent<CinemachineCamera>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (director == null) director = GetComponent<CameraViewDirector>();
            director.PreparingView += Refresh;
            if (Application.IsPlaying(gameObject))
            {
                Current = this;
            }
        }

        void OnDisable()
        {
            if (director != null)
            {
                director.PreparingView -= Refresh;
            }
            ReleaseViewScope();
            if (Current == this) Current = null;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            DestroyPresets();
        }

        void Refresh()
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || director == null) return;
            var course = TightropeCourse.Current;
            if (course == null || !course.IsTraversalActive)
            {
                ReleaseViewScope();
                return;
            }
            EnsurePresets();
            bool useLocal = CanUseLocalView;
            bool useFirstPerson = useLocal && !IsLocalOnRope;
            // 공용 자유 시점은 로컬 캐릭터를 중심으로 돈다. 관전 요청은 고정형으로 남겨
            // 사망한 로컬 플레이어 대신 관전 대상을 계속 따라갈 수 있게 한다.
            preset.Kind = useLocal ? CameraViewKind.FreeThirdPerson : CameraViewKind.ThirdPerson;
            var rules = GameSettings.Tightrope.Camera;
            float fieldOfView = preset.FieldOfView;
            var baseline = new TightropeFramingMath.Frame(rules.Height, rules.Distance, rules.LookHeight, rules.LookAhead);
            var focus = useLocal ? director.LocalPlayer.transform : FocusTarget();
            var subject = focus != null ? focus.GetComponentInParent<NetworkPlayer>() : null;
            if (!IsActivePlayer(subject)) { ReleaseViewScope(); return; }
            if (framedPlayer != subject || firstPersonFraming != useFirstPerson) ResetFrame();
            framedPlayer = subject;
            firstPersonFraming = useFirstPerson;
            if (!hasFrame) { currentFrame = baseline; hasFrame = true; }
            // PreparingView는 한 프레임에 Update와 Cinemachine에서 여러 번 호출될 수 있다.
            if (updatedFrame != Time.frameCount)
            {
                updatedFrame = Time.frameCount;
                var targetFrame = baseline;
                Vector3 targetOffset = Vector3.zero;
                hasSharedGroup = false;
                sharedMembers.Clear();
                groupPlayers.Clear();
                if (focus != null && IsActivePlayer(framedPlayer))
                {
                    hasSharedGroup = CollectSharedLayer(framedPlayer, rules.NearbyRadius, out sharedCenter);
                    // 공전은 내 캐릭터 중심을 유지하고, 관전만 동료들의 중심으로 이동한다.
                    Vector3 anchor = !useLocal && hasSharedGroup ? sharedCenter : focus.position;
                    Vector3 framingForward = useLocal && director.ViewForward.sqrMagnitude > .0001f
                        ? director.ViewForward : course.Forward;
                    Quaternion rotation = useLocal || hasSharedGroup
                        ? Quaternion.LookRotation(framingForward, Vector3.up) : focus.rotation;
                    if (!useLocal && hasSharedGroup) targetOffset = anchor - focus.position;
                    bool hasContext = CollectSubjects(anchor, framedPlayer, rules, out var subjects);
                    if (hasContext)
                    {
                        float aspect = Camera.main != null ? Camera.main.aspect
                            : Screen.width / (float)Mathf.Max(1, Screen.height);
                        targetFrame = TightropeFramingMath.Fit(subjects, anchor, rotation, framingForward,
                            baseline, fieldOfView, aspect, rules.MaximumDistance, rules.FramingPadding, rules.ContextLookAhead);
                    }
                }
                currentFrame = TightropeFramingMath.Smooth(currentFrame, targetFrame,
                    rules.FramingSmoothTime, Time.unscaledDeltaTime);
                float blend = rules.FramingSmoothTime <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / rules.FramingSmoothTime);
                sharedOffset = Vector3.Lerp(sharedOffset, targetOffset, blend);
            }
            preset.Height = currentFrame.Height;
            preset.Distance = currentFrame.Distance;
            preset.LookHeight = currentFrame.LookHeight;
            preset.LookAhead = currentFrame.LookAhead;
            director.RequestView(this, useFirstPerson ? firstView : preset, 0, course.Forward);
            director.SetInputBlocked(InputGate.IsBlocked);
            ownsInput = true;
            if (fade == null) return;
            occlusion.Radius = rules.OccluderRadius;
            occlusion.Alpha = rules.OccluderAlpha;
            occlusion.FadeSpeed = rules.FadeSpeed;
            fade.RequestRules(this, occlusion);
            fade.RegisterExclusion(this, Exclude);
        }

        Transform FocusTarget()
        {
            if (CameraSpectator.IsSpectating && CameraSpectator.Target != null) return CameraSpectator.Target.transform;
            return virtualCamera != null ? virtualCamera.Target.TrackingTarget : null;
        }

        static bool IsActivePlayer(NetworkPlayer player) => player != null && player.isActiveAndEnabled
            && player.State == PlayerState.Normal && (!player.TryGetComponent(out PlayerBalance balance) || !balance.HasFallen);

        bool CollectSharedLayer(NetworkPlayer subject, float radius, out Vector3 center)
        {
            layerSubjects.Clear();
            var stack = PiggybackSystem.Current;
            bool hasStack = stack != null && stack.isActiveAndEnabled;
            foreach (var player in NetworkPlayer.All)
                if (IsActivePlayer(player)) layerSubjects.Add(new TightropeFramingMath.LayerSubject(
                    player.OwnerClientId, hasStack ? stack.GetFloor(player) : 1, player.transform.position));
            bool shared = TightropeFramingMath.SharedLayerGroup(layerSubjects, subject.OwnerClientId, radius, sharedMembers, out center);
            foreach (var player in NetworkPlayer.All)
                if (IsActivePlayer(player) && sharedMembers.Contains(player.OwnerClientId)) groupPlayers.Add(player);
            return shared;
        }

        bool CollectSubjects(Vector3 anchor, NetworkPlayer subject,
            TightropeSettings.CameraSettings rules, out Bounds bounds)
        {
            bounds = PlayerBounds(subject);
            bool hasContext = false;
            var stack = PiggybackSystem.Current;
            foreach (var other in NetworkPlayer.All)
            {
                if (!IsActivePlayer(other) || other == subject) continue;
                bool sameStack = IsGroupStackMember(stack, subject, other);
                if (!sharedMembers.Contains(other.OwnerClientId)
                    && !TightropeFramingMath.IsNearby(anchor, other.transform.position, rules.NearbyRadius, sameStack)) continue;
                bounds.Encapsulate(PlayerBounds(other));
                hasContext = true;
            }

            // 움직이는 톱을 따라 구도를 바꾸면 일정한 전진이 줌·시선 이동으로 상쇄된다.
            // 동료·목마만 함께 담고, 장애물은 코스 안에서 움직이게 둔다.
            return hasContext;
        }

        bool IsGroupStackMember(PiggybackSystem stack, NetworkPlayer subject, NetworkPlayer other)
        {
            if (stack == null || !stack.isActiveAndEnabled) return false;
            if (stack.IsSameStack(subject, other)) return true;
            foreach (var member in groupPlayers) if (stack.IsSameStack(member, other)) return true;
            return false;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            // tracking target은 공용 로컬/관전 기능이 계속 소유한다. 다른 시점 요청이 이기면 손대지 않는다.
            // 단독도 현재 프레임의 이동을 그대로 따른다. Follow의 감쇠를 한 번 더 거치면
            // 프레임 간격이 변할 때 일정한 전진이 느려졌다 따라잡는 움직임으로 보인다.
            // Aim 뒤에 적용해 구도만 확정하고, Finalize의 공용 시점 전환 보간은 유지한다.
            if (stage != CinemachineCore.Stage.Aim || !Application.IsPlaying(gameObject)
                || !isActiveAndEnabled || director == null || director.CurrentView != preset || director.UsesMouseLook
                || !IsActivePlayer(framedPlayer)) return;
            var course = TightropeCourse.Current;
            var focus = FocusTarget();
            if (course == null || !course.IsTraversalActive || focus == null) return;
            Quaternion rotation = hasSharedGroup ? Quaternion.LookRotation(course.Forward, Vector3.up) : focus.rotation;
            var pose = TightropeFramingMath.CameraPose(focus.position + sharedOffset, rotation, course.Forward,
                currentFrame, out var look);
            state.RawPosition = pose.position;
            state.RawOrientation = pose.rotation;
            state.PositionCorrection = Vector3.zero;
            state.OrientationCorrection = Quaternion.identity;
            state.ReferenceLookAt = look;
            state.Lens.Dutch = 0f;
        }

        static Bounds PlayerBounds(NetworkPlayer player)
        {
            var mover = player.GetComponent<PlayerMover>();
            float height = Mathf.Max(GameSettings.Base.Character.ModelHeight,
                mover != null && mover.Body != null ? mover.BodyHeight : GameSettings.Base.Character.CapsuleHeight);
            float radius = mover != null && mover.Body != null ? mover.BodyRadius : GameSettings.Base.Character.CapsuleRadius;
            return new Bounds(player.transform.position + Vector3.up * (height * 0.5f), new Vector3(radius * 2f, height, radius * 2f));
        }

        void ResetFrame()
        {
            hasFrame = false;
            updatedFrame = -1;
            framedPlayer = null;
            hasSharedGroup = false;
            sharedOffset = sharedCenter = Vector3.zero;
            sharedMembers.Clear();
            groupPlayers.Clear();
        }

        bool Exclude(Transform hit)
        {
            var course = TightropeCourse.Current;
            if (course != null && hit.IsChildOf(course.transform)) return true;
            var piggyback = PiggybackSystem.Current;
            var player = hit.GetComponentInParent<NetworkPlayer>();
            return player != null && (sharedMembers.Contains(player.OwnerClientId) || IsGroupStackMember(piggyback, framedPlayer, player));
        }
    }
}
