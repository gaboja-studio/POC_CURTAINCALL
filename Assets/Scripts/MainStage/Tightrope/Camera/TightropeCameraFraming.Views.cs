using CurtainCall.Player;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.Cameras
{
    public sealed partial class TightropeCameraFraming
    {
        bool ownsInput;
        CameraViewPreset platformView;

        public static TightropeCameraFraming Current { get; private set; }
        /// <summary>플랫폼·줄 위 모두 자유 3인칭이다(Task-089). 플랫폼은 공용 값, 줄 위는 구도 맞춤 값을 쓴다.</summary>
        public CameraViewKind CurrentMode => CameraViewKind.FreeThirdPerson;
        public bool IsLocalViewActive => Application.IsPlaying(gameObject) && isActiveAndEnabled && CanUseLocalView
            && director != null && director.CurrentView == (IsLocalOnRope ? preset : platformView)
            && director.CurrentView != null;

        // 실제 표면 전환을 따르므로 줄 위 점프·목마 중에도 마우스 공전 3인칭을 유지한다.
        bool IsLocalOnRope => TightropeCourse.Current != null
            && ((TightropeTrapeze.Current != null && TightropeTrapeze.Current.IsCrossing(CurtainCall.Network.PlayerSync.NetworkPlayer.Local))
                || (TightropeCourse.Current.TryGetComponent(out CourseControlSwitcher controls) && controls.IsLocalOnRope));

        bool CanUseLocalView => director != null && !CameraSpectator.IsSpectating
            && IsActivePlayer(director.LocalPlayer) && TightropeCourse.Current != null
            && TightropeCourse.Current.IsTraversalActive;

        void EnsurePresets()
        {
            if (preset != null) return;
            var settings = director.Settings;
            // 인간대포의 일반 3인칭과 같은 렌즈·마우스 공전 설정을 복제한다.
            // 구도 확대는 줄 위 런타임 복사본에만 적용해 공용 프리셋을 변경하지 않는다.
            var thirdSource = Resources.Load<CameraViewPreset>("Settings/Camera/FreeThirdPerson");
            thirdSource ??= settings != null ? settings.DefaultThirdPerson : null;
            preset = Copy(thirdSource, CameraViewKind.FreeThirdPerson, "Tightrope Orbit Third Person");
            // 플랫폼은 다른 공간 플랫폼과 같은 공용 자유 3인칭 값을 그대로 쓴다. 구도 맞춤을 적용하지 않는다.
            platformView = Copy(thirdSource, CameraViewKind.FreeThirdPerson, "Tightrope Platform Third Person");
            platformView.TransitionDuration = preset.TransitionDuration = 0.18f;
        }

        static CameraViewPreset Copy(CameraViewPreset source, CameraViewKind kind, string label)
        {
            var result = source != null ? Instantiate(source) : ScriptableObject.CreateInstance<CameraViewPreset>();
            result.name = label;
            result.Kind = kind;
            result.hideFlags = HideFlags.DontSave;
            return result;
        }

        void ReleaseViewScope()
        {
            if (director != null)
            {
                if (ownsInput && (director.CurrentView == platformView || director.CurrentView == preset))
                    director.SetInputBlocked(false);
                director.SetPreserveCursorInFixedView(false);
                director.ReleaseView(this);
            }
            ownsInput = false;
            if (fade != null) fade.ReleaseSource(this);
            ResetFrame();
        }

        void DestroyPresets()
        {
            foreach (var view in new[] { preset, platformView })
                if (view != null)
                {
                    if (Application.IsPlaying(gameObject)) Destroy(view);
                    else DestroyImmediate(view);
                }
            preset = platformView = null;
        }
    }
}
