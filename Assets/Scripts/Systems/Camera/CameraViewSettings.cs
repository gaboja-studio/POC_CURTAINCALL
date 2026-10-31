using System;
using UnityEngine;

namespace CurtainCall.Cameras
{
    [Serializable]
    public sealed class CameraOcclusionRules
    {
        public Vector2 TargetHeights = new(0.8f, 1.35f);
        [Min(0.001f)] public float Radius = 0.4f;
        [Range(0f, 1f)] public float Alpha = 0.35f;
        [Min(0f)] public float FadeSpeed = 6f;
    }

    [CreateAssetMenu(menuName = "CurtainCall/Camera/View Settings", fileName = "CameraViewSettings")]
    public sealed class CameraViewSettings : ScriptableObject
    {
        [Tooltip("시점 요청이 없을 때(일반 플랫폼) 쓰는 3인칭. 플랫폼은 모든 공간에서 자유 3인칭(FreeThirdPerson)으로 통일한다(Task-089). 관전 중에는 같은 거리값으로 고정 추적한다.")]
        public CameraViewPreset DefaultThirdPerson;
        public CameraViewPreset FirstPerson;
        public CameraOcclusionRules Occlusion = new();
    }
}
