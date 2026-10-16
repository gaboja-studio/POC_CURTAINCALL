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
            director.PrepareView();
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage == CinemachineCore.Stage.Finalize && director != null) director.ApplyPose(ref state);
        }
    }
}
