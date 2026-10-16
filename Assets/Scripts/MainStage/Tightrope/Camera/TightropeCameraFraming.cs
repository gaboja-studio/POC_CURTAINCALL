using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>
    /// 외줄 전용 구도·가림 예외 공급자. 실제 코스가 있을 때만 공용 카메라에 요청한다.
    /// 코스 뒤 월드 방향과 캐릭터 로컬 look offset 의미를 보존한다. 리그의 기존 GUID·타입은 유지한다.
    /// </summary>
    [RequireComponent(typeof(CameraViewDirector))]
    public sealed class TightropeCameraFraming : MonoBehaviour
    {
        CameraViewDirector director;
        CameraOcclusionFade fade;
        CameraViewPreset preset;
        readonly CameraOcclusionRules occlusion = new();

        void Awake()
        {
            director = GetComponent<CameraViewDirector>();
            fade = GetComponent<CameraOcclusionFade>();
            preset = ScriptableObject.CreateInstance<CameraViewPreset>();
            preset.name = "Tightrope Runtime View";
            preset.hideFlags = HideFlags.DontSave;
        }

        void OnEnable()
        {
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
        }

        void OnDestroy()
        {
            if (preset != null) Destroy(preset);
        }

        void Refresh()
        {
            var course = TightropeCourse.Current;
            if (course == null || !course.isActiveAndEnabled)
            {
                director.ReleaseView(this);
                if (fade != null) fade.ReleaseSource(this);
                return;
            }
            var rules = GameSettings.Tightrope.Camera;
            preset.Kind = CameraViewKind.ThirdPerson;
            preset.Height = rules.Height;
            preset.Distance = rules.Distance;
            preset.LookHeight = rules.LookHeight;
            preset.LookAhead = rules.LookAhead;
            preset.FieldOfView = director.Settings != null && director.Settings.DefaultThirdPerson != null
                ? director.Settings.DefaultThirdPerson.FieldOfView : 60f;
            director.RequestView(this, preset, 0, course.Forward);
            if (fade == null) return;
            occlusion.Radius = rules.OccluderRadius;
            occlusion.Alpha = rules.OccluderAlpha;
            occlusion.FadeSpeed = rules.FadeSpeed;
            fade.RequestRules(this, occlusion);
            fade.RegisterExclusion(this, Exclude);
        }

        static bool Exclude(Transform hit)
        {
            var course = TightropeCourse.Current;
            if (course != null && hit.IsChildOf(course.transform)) return true;
            var piggyback = PiggybackSystem.Current;
            return piggyback != null && piggyback.IsSameStack(NetworkPlayer.Local, hit.GetComponentInParent<NetworkPlayer>());
        }
    }
}
