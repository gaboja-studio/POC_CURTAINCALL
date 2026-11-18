using UnityEngine;

namespace CurtainCall.Settings
{
    public enum CheerPointStyle { FireHoop, EncoreHalo, MaskApparition }

    [CreateAssetMenu(menuName = "CurtainCall/Settings/Performance equipment")]
    public sealed class PerformanceEquipmentSettings : ScriptableObject
    {
        [Header("환호 고리 — 불은 피해를 주지 않음")]
        [InspectorName("환호 포인트 외형")] public CheerPointStyle cheerStyle;
        [InspectorName("외형 프리팹 (불/금빛/가면 순서)")] public GameObject[] cheerVisuals = new GameObject[3];
        [InspectorName("고리 내부 판정 반지름"), Min(.1f)] public float hoopRadius = 1.25f;
        [InspectorName("기본 획득 환호량"), Min(0)] public int cheerAmount = 15;
        [Header("수동 공연 기물")]
        [InspectorName("신발 추가 점프 높이"), Min(.1f)] public float extraJumpHeight = 2f;
        [InspectorName("구조 장갑 최대 거리"), Min(.1f)] public float rescueRange = 4f;
        [InspectorName("구조 장갑 전방 반각"), Range(1,90)] public float rescueHalfAngle = 55f;
        [InspectorName("구조 장갑 끌어당김 시간"), Min(.2f)] public float rescuePullSeconds = .8f;
        [InspectorName("끈끈이 장갑 유지 시간"), Min(.1f)] public float stickySeconds = 3f;
        [InspectorName("벌룬 바지 유지 시간"), Min(.1f)] public float balloonSeconds = 3f;
        [InspectorName("벌룬 바지 최대 하강 속도"), Min(.1f)] public float balloonFallSpeed = 2f;
        [InspectorName("벌룬 바지 수평 조작 배율"), Min(1f)] public float balloonSteering = 1.5f;
        [Header("사용 연출 — 신발 / 구조 / 끈끈이 / 벌룬")]
        public GameObject[] useVisuals = new GameObject[4];
        [Header("구조 대상 안내 — 소지자에게만 표시")]
        public GameObject rescueTargetVisual;
        [InspectorName("구조 대상 표식 불투명도"), Range(0,1)]
        [Tooltip("아이콘·테두리·키캡 전체에 적용. 0이면 보이지 않고 1이면 원래 불투명도입니다. 플레이 중에도 즉시 반영됩니다.")]
        public float rescueTargetOpacity = .75f;
        public GameObject UseVisual(string id)
        {
            int i = id switch { "spring-shoes" => 0, "rescue-gloves" => 1, "sticky-gloves" => 2, "balloon-pants" => 3, _ => -1 };
            return i >= 0 && useVisuals != null && i < useVisuals.Length ? useVisuals[i] : null;
        }
        public GameObject Visual(CheerPointStyle style) => cheerVisuals != null && (int)style < cheerVisuals.Length && (int)style >= 0 ? cheerVisuals[(int)style] : null;
    }
}
