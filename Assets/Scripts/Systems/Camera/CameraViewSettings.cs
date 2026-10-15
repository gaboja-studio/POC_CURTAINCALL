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
        public CameraViewPreset DefaultThirdPerson;
        public CameraViewPreset FirstPerson;
        public CameraOcclusionRules Occlusion = new();
    }
}
