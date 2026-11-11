using UnityEngine;

namespace CurtainCall.Settings
{
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Trampoline", fileName = "TrampolineSettings")]
    public sealed class TrampolineSettings : ScriptableObject
    {
        [InspectorName("공통 트램펄린 외형"), Tooltip("공연·연습·대기실의 트램펄린 외형 원본. 가운데 별무늬가 있는 CC_Trampoline을 사용한다. 변경 후 Tools/CurtainCall/트램펄린/현재 씬 공통 외형 적용으로 저장된 씬에 반영한다. 반동·배치·발사 궤적은 바꾸지 않는다.")]
        [SerializeField] GameObject visualPrefab;
        [InspectorName("반동 상승 속도"), Tooltip("트램펄린에서 튀어 오를 때 위쪽 속도(m/s). 점프 높이(m)가 아니다. 모든 씬의 트램펄린과 스프링 신발 반동의 기준값이며, 신발은 아이템 효과 배율도 곱한다. 기본 12m/s.")]
        [SerializeField, Min(.1f)] float upwardSpeed = 12f;
        [InspectorName("운반 이동 속도"), Tooltip("트램펄린을 들고 이동하는 기본 속도(m/s). 부상 배율은 별도로 적용하며 반동 높이에는 영향을 주지 않는다. 기본 4.5m/s.")]
        [SerializeField, Min(0f)] float carryMoveSpeed = 4.5f;
        [InspectorName("운반 충돌 모양"), Tooltip("모든 씬에서 트램펄린 몸체가 벽과 장애물에 부딪히는 공통 메시. 원형 외관에 맞는 볼록 메시를 지정한다. 반동 높이·범위에는 영향을 주지 않는다.")]
        [SerializeField] Mesh carryCollisionMesh;
        [InspectorName("외형을 따른 반동·잡기 판정"), Tooltip("모든 트램펄린에 공통 적용. 켜면 인간대포 공연과 같이 원형 몸체·손잡이 모양으로 반동 접촉을 검사하고 손잡이를 잡기 기준으로 사용한다. 끄면 장치 전체 사각 범위와 중앙 윗면을 사용한다. 기본 켬. 씬별로 다르게 지정할 수 없다.")]
        [SerializeField] bool usePropOutline = true;
        [InspectorName("손잡이 잡기 기준 높이"), Tooltip("손잡이 위치에서 위로 올려 잡기 거리를 재는 높이(m, 장치 로컬 기준). 외형 판정을 사용할 때 적용한다. 기본 0.6m. 공통 잡기 거리 자체나 운반 자세는 바꾸지 않는다.")]
        [SerializeField, Min(0f)] float grabPointHeight = .6f;
        [InspectorName("반동에 운반자 필요")]
        [SerializeField, Tooltip("켜면 다른 단원이 손잡이를 잡고 있어야 튀어 오른다. 끄면 바닥에 내려놓고 혼자서도 사용할 수 있다. 본 공연과 연습실에 공통 적용. 기본 끔. 켜면 연습 마지막 운반자의 혼자 반동하는 단계도 제한된다.")]
        bool requireCarrierForBounce;
        public float UpwardSpeed => Mathf.Max(.1f, upwardSpeed);
        public GameObject VisualPrefab => visualPrefab;
        public float CarryMoveSpeed => Mathf.Max(0, carryMoveSpeed);
        public Mesh CarryCollisionMesh => carryCollisionMesh;
        public bool UsePropOutline => usePropOutline;
        public float GrabPointHeight => Mathf.Max(0f, grabPointHeight);
        public bool RequireCarrierForBounce => requireCarrierForBounce;
        public float AirControlSpeed => GameSettings.Base.AirControl.Speed;
        public float GrabDistance => GameSettings.Base.Interaction.Distance;
    }
}
