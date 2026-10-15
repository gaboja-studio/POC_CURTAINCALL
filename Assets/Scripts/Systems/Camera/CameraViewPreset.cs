using UnityEngine;

namespace CurtainCall.Cameras
{
    public enum CameraViewKind { ThirdPerson, FirstPerson }

    /// <summary>공간에서 요청할 카메라 시점. 플레이 중 값을 바꾸면 바로 반영된다.</summary>
    [CreateAssetMenu(menuName = "CurtainCall/Camera/View Preset", fileName = "CameraViewPreset")]
    public sealed class CameraViewPreset : ScriptableObject
    {
        public CameraViewKind Kind;
        [Header("3인칭 (따라가기: 월드 기준, 바라보기: 캐릭터 로컬 기준)")]
        [Min(0f)] public float Distance = 4.5f;
        public float Height = 3.75f;
        public float LookHeight = 1.2f;
        public float LookAhead = 1.75f;
        [Header("공통")]
        [Range(1f, 179f)] public float FieldOfView = 60f;
        [Min(0f)] public float TransitionDuration;
        [Header("1인칭")]
        [Tooltip("0이면 캐릭터 몸 높이 × 0.9, 몸 높이가 없으면 1.6m.")]
        [Min(0f)] public float EyeHeight;
        [Min(0f)] public float MouseSensitivity = 0.1f;
        [Range(0f, 89f)] public float PitchLimit = 85f;
        public bool HideLocalModel = true;
    }
}
