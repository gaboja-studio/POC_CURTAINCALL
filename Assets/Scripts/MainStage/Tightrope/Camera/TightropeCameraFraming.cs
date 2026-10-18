using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Saws;
using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>
    /// 외줄 전용 구도·가림 예외 공급자. 실제 코스가 있을 때만 공용 카메라에 요청한다.
    /// 코스 뒤 월드 방향과 캐릭터 로컬 look offset 의미를 보존한다. 리그의 기존 GUID·타입은 유지한다.
    /// </summary>
    [RequireComponent(typeof(CameraViewDirector))]
    public sealed class TightropeCameraFraming : CinemachineExtension
    {
        CameraViewDirector director;
        CameraOcclusionFade fade;
        CameraViewPreset preset;
        CinemachineCamera virtualCamera;
        NetworkPlayer framedPlayer;
        TightropeFramingMath.Frame currentFrame;
        bool hasFrame;
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
            preset = ScriptableObject.CreateInstance<CameraViewPreset>();
            preset.name = "Tightrope Runtime View";
            preset.hideFlags = HideFlags.DontSave;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (director == null) director = GetComponent<CameraViewDirector>();
            director.PreparingView += Refresh;
        }

        void OnDisable()
        {
            if (director != null)
            {
                director.PreparingView -= Refresh;
                director.ReleaseView(this);
            }
            if (fade != null) fade.ReleaseSource(this);
            ResetFrame();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (preset != null) Destroy(preset);
        }

        void Refresh()
        {
            var course = TightropeCourse.Current;
            if (course == null || !course.IsGameplayActive)
            {
                director.ReleaseView(this);
                if (fade != null) fade.ReleaseSource(this);
                ResetFrame();
                return;
            }
            var rules = GameSettings.Tightrope.Camera;
            float fieldOfView = director.Settings != null && director.Settings.DefaultThirdPerson != null
                ? director.Settings.DefaultThirdPerson.FieldOfView : 60f;
            var baseline = new TightropeFramingMath.Frame(rules.Height, rules.Distance, rules.LookHeight, rules.LookAhead);
            var focus = FocusTarget();
            var subject = focus != null ? focus.GetComponentInParent<NetworkPlayer>() : null;
            if (framedPlayer != subject) ResetFrame(); // 관전 대상·층이 바뀔 때 이전 그룹의 이동량을 넘기지 않는다.
            framedPlayer = subject;
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
                    Vector3 anchor = hasSharedGroup ? sharedCenter : focus.position;
                    Quaternion rotation = hasSharedGroup ? Quaternion.LookRotation(course.Forward, Vector3.up) : focus.rotation;
                    if (hasSharedGroup) targetOffset = anchor - focus.position;
                    if (CollectSubjects(course, anchor, framedPlayer, rules, out var subjects))
                    {
                        float aspect = Camera.main != null ? Camera.main.aspect
                            : Screen.width / (float)Mathf.Max(1, Screen.height);
                        targetFrame = TightropeFramingMath.Fit(subjects, anchor, rotation, course.Forward,
                            baseline, fieldOfView, aspect, rules.MaximumDistance, rules.FramingPadding, rules.ContextLookAhead);
                    }
                }
                currentFrame = TightropeFramingMath.Smooth(currentFrame, targetFrame, rules.FramingSmoothTime, Time.unscaledDeltaTime);
                float blend = rules.FramingSmoothTime <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / rules.FramingSmoothTime);
                sharedOffset = Vector3.Lerp(sharedOffset, targetOffset, blend);
            }
            preset.Kind = CameraViewKind.ThirdPerson;
            preset.Height = currentFrame.Height;
            preset.Distance = currentFrame.Distance;
            preset.LookHeight = currentFrame.LookHeight;
            preset.LookAhead = currentFrame.LookAhead;
            preset.FieldOfView = fieldOfView;
            director.RequestView(this, preset, 0, course.Forward);
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

        bool CollectSubjects(TightropeCourse course, Vector3 anchor, NetworkPlayer subject,
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

            var saws = TightropeSaws.Current;
            var run = TightropeRun.Current;
            if (saws == null || !saws.isActiveAndEnabled || run == null || run.State != TightropeRunState.Running) return hasContext;
            for (int i = 0; i < saws.HorizontalCount; i++)
                if (saws.TryGetHorizontalPosition(i, out var position) && IsUpcoming(position))
                {
                    bounds.Encapsulate(new Bounds(position, Vector3.one * saws.Settings.horizontalVisualDiameter));
                    hasContext = true;
                }
            if (saws.TryGetVerticalPosition(out var vertical) && IsUpcoming(vertical))
            {
                bounds.Encapsulate(new Bounds(vertical, Vector3.one * saws.Settings.verticalVisualDiameter));
                hasContext = true;
            }
            return hasContext;

            bool IsUpcoming(Vector3 position)
            {
                Vector3 offset = position - anchor;
                float ahead = Vector3.Dot(offset, course.Forward);
                return ahead >= 0f && ahead <= rules.ObstacleLookAhead
                    && Mathf.Abs(Vector3.Dot(offset, course.Right)) <= rules.NearbyRadius;
            }
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
            if (stage != CinemachineCore.Stage.Finalize || !Application.IsPlaying(gameObject)
                || !isActiveAndEnabled || director == null || director.CurrentView != preset
                || !IsActivePlayer(framedPlayer) || (!hasSharedGroup && sharedOffset.sqrMagnitude < 0.0001f)) return;
            var course = TightropeCourse.Current;
            var focus = FocusTarget();
            if (course == null || !course.IsGameplayActive || focus == null) return;
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
