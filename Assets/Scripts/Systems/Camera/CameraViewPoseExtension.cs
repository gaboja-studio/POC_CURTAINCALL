using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.Cameras
{
    [RequireComponent(typeof(CameraViewDirector))]
    public sealed class CameraViewPoseExtension : CinemachineExtension
    {
        CameraViewDirector director;

        protected override void Awake()
        {
            base.Awake();
            director = GetComponent<CameraViewDirector>();
        }

        public override void PrePipelineMutateCameraStateCallback(CinemachineVirtualCameraBase vcam, ref CameraState state, float deltaTime)
        {
            if (director == null) director = GetComponent<CameraViewDirector>();
            var followBefore = vcam.Follow;
            var lookAtBefore = vcam.LookAt;
            director.PrepareView();
            if (followBefore != vcam.Follow || lookAtBefore != vcam.LookAt)
            {
                // CinemachineCamera는 이 callback 전에 대상 캐시·ReferenceLookAt을 만들었다.
                // 여기서 관전 대상을 바꿨다면 Body/Aim도 같은 프레임의 새 대상을 보게 맞춘다.
                vcam.UpdateTargetCache();
                var lookAt = vcam.LookAt;
                state.ReferenceLookAt = lookAt == null ? CameraState.kNoPoint
                    : vcam.LookAtTargetAsVcam != null ? vcam.LookAtTargetAsVcam.State.GetFinalPosition()
                    : TargetPositionCache.GetTargetPosition(lookAt);
            }
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage == CinemachineCore.Stage.Finalize && director != null) director.ApplyPose(ref state);
        }
    }
}
