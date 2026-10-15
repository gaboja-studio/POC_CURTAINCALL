using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>
    /// ThirdPerson: 3인칭 고정(카메라 방향은 공간이 정함). FirstPerson: 1인칭(마우스 시선).
    /// FreeThirdPerson: 3인칭 비고정 — 1인칭처럼 마우스로 시선을 돌리고 카메라만 캐릭터 뒤 3인칭 위치에서 따라 돈다(Task-045).
    /// 마우스 시선 시점(1인칭·3인칭 비고정)에서는 기본 이동이 시선 기준이다.
    /// </summary>
    public enum CameraViewKind { ThirdPerson, FirstPerson, FreeThirdPerson }

    /// <summary>공간에서 요청할 카메라 시점. 플레이 중 값을 바꾸면 바로 반영된다.</summary>
    [CreateAssetMenu(menuName = "CurtainCall/Camera/View Preset", fileName = "CameraViewPreset")]
    public sealed class CameraViewPreset : ScriptableObject
    {
        public CameraViewKind Kind;
        [Header("3인칭 (고정 따라가기: 월드 기준, 바라보기: 캐릭터 로컬 기준 / 비고정: 시선 기준으로 돈다)")]
        [Min(0f)] public float Distance = 4.5f;
        public float Height = 3.75f;
        public float LookHeight = 1.2f;
        public float LookAhead = 1.75f;
        [Header("공통")]
        [Range(1f, 179f)] public float FieldOfView = 60f;
        [Min(0f)] public float TransitionDuration;
        [Header("마우스 시선 (1인칭·3인칭 비고정)")]
        [Min(0f)] public float MouseSensitivity = 0.1f;
        [Tooltip("위아래 시선 한계(도). 3인칭 비고정은 카메라가 땅에 묻히지 않게 작게 둔다.")]
        [Range(0f, 89f)] public float PitchLimit = 85f;
        [Header("1인칭")]
        [Tooltip("0이면 캐릭터 몸 높이 × 0.9, 몸 높이가 없으면 1.6m.")]
        [Min(0f)] public float EyeHeight;
        public bool HideLocalModel = true;
    }
}
