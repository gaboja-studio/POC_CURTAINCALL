using CurtainCall.Settings;
using CurtainCall.Tightrope;
using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>
    /// 외줄 카메라 구도: 외줄 세팅(카메라)의 높이·거리·바라보는 높이·앞쪽 거리를 매 프레임 Cinemachine에 넣는다(플레이 중 수정 바로 반영).
    /// 카메라는 코스 뒤쪽 월드 고정 방향(005 결정)이고, 바라보는 점을 캐릭터 앞으로 옮겨 앞 장애물이 화면 가운데로 오게 한다.
    /// 근거: Integrations/Active/Integration-tightrope-prototype/camera-review-20261004.md (2026-10-04 PM).
    /// 카메라 리그 프리팹(PlayerFollowCamera)에 둔다.
    /// </summary>
    [RequireComponent(typeof(CinemachineFollow))]
    [RequireComponent(typeof(CinemachineRotationComposer))]
    public sealed class TightropeCameraFraming : MonoBehaviour
    {
        CinemachineFollow follow;
        CinemachineRotationComposer composer;

        void Awake()
        {
            follow = GetComponent<CinemachineFollow>();
            composer = GetComponent<CinemachineRotationComposer>();
        }

        void LateUpdate()
        {
            var rules = GameSettings.Tightrope.Camera;
            var course = TightropeCourse.Current;
            Vector3 forward = course != null ? course.Forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            // 따라가기 묶기는 월드 기준이므로 코스 뒤쪽 방향으로 넣는다. 바라보는 점은 캐릭터 기준(앞 = 코스 앞)
            follow.FollowOffset = Vector3.up * rules.Height - forward * rules.Distance;
            composer.TargetOffset = new Vector3(0f, rules.LookHeight, rules.LookAhead);
        }
    }
}
