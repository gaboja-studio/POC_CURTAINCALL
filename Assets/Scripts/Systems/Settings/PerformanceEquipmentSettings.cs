using UnityEngine;

namespace CurtainCall.Settings
{
    public enum CheerPointStyle { FireHoop, EncoreHalo, MaskApparition }

    [CreateAssetMenu(menuName = "CurtainCall/Settings/Performance equipment")]
    public sealed class PerformanceEquipmentSettings : ScriptableObject
    {
        [Header("환호 고리 — 불은 피해를 주지 않음")]
        [InspectorName("환호 포인트 사용")]
        [Tooltip("끄면 모든 환호 포인트의 표시와 획득 보상을 중단합니다. 온라인에서는 호스트 설정을 따르며, 기존 배치와 외형은 유지합니다.")]
        public bool cheerPointsEnabled = true;
        [InspectorName("환호 포인트 외형")] public CheerPointStyle cheerStyle;
        [InspectorName("외형 프리팹 (불/금빛/가면 순서)")] public GameObject[] cheerVisuals = new GameObject[3];
        [InspectorName("고리 내부 판정 반지름"), Min(.1f)] public float hoopRadius = 1.25f;
        [InspectorName("기본 획득 환호량"), Min(0)] public int cheerAmount = 15;
        [Header("환호 고리 — 매달림과 회수")]
        [InspectorName("반복 로프 메시")] public Mesh cheerRopeMesh;
        [InspectorName("로프 매듭 프리팹")] public GameObject cheerRopeKnot;
        [InspectorName("매다는 줄 재질")] public Material cheerRopeMaterial;
        [InspectorName("줄 최소 길이"), Min(10)] public float cheerRopeLength = 80f;
        [InspectorName("줄 두께"), Min(.01f)] public float cheerRopeWidth = .055f;
        [InspectorName("획득 후 아래로 당기는 거리"), Min(0)] public float cheerDipDistance = .3f;
        [InspectorName("아래로 당기는 시간"), Min(.05f)] public float cheerDipSeconds = .28f;
        [InspectorName("위로 회수하는 가속도"), Min(1)] public float cheerRetractAcceleration = 36f;
        [InspectorName("위로 회수하는 최고 속도"), Min(1)] public float cheerRetractSpeed = 28f;
        [InspectorName("숨기기 전 최소 상승 높이"), Min(3)] public float cheerHideMinHeight = 18f;
        [Header("수동 공연 기물")]
        [InspectorName("신발 추가 점프 높이"), Min(.1f)] public float extraJumpHeight = 2f;
        [InspectorName("구조 장갑 수평 범위"), Min(.1f)]
        [Tooltip("구조 장갑 사거리: 당길 수 있는 동료까지의 수평 거리(m). 높이 차이는 아래 '구조 장갑 높이 범위'로 따로 정한다. " +
                 "범위 안에서 화면에 보이는 동료 중 화면 정중앙 조준점에 가장 가까운 한 명을 고른다.")]
        public float rescueRange = 8f;
        [InspectorName("구조 장갑 높이 범위"), Min(0f)]
        [Tooltip("구조 장갑으로 당길 수 있는 위·아래 높이 차이(m). 외줄 위·대포 비행·추락 중인 동료까지 닿도록 정한다.")]
        public float rescueHeightRange = 6f;
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
