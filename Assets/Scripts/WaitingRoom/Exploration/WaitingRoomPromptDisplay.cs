using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    [System.Serializable]
    public sealed class RoomPromptAppearance
    {
        [Tooltip("비우면 대상의 현재 행동명을 표시합니다. 사용할 수 없는 상태의 이유는 덮어쓰지 않습니다.")]
        public string availableAction;
        [Tooltip("선택 사항. 원래 대상 위치와 별개로 UI를 붙일 지점입니다.")]
        public Transform uiAnchor;
        [Tooltip("앵커(없으면 대상)의 로컬 좌표 기준 UI 오프셋입니다. 거리·시야 판정에는 사용하지 않습니다.")]
        public Vector3 localOffset;
        public Vector3 At(Transform target,Vector3 original)=>uiAnchor?uiAnchor.TransformPoint(localOffset):original+target.TransformVector(localOffset);
    }

    [DisallowMultipleComponent, AddComponentMenu("Curtain Call/UI/대기실 대상 안내")]
    public sealed class WaitingRoomPromptDisplay : MonoBehaviour
    {
        public RoomPromptAppearance hud=new();
    }
}
