using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 기능 스크립트가 밀어 여는 그레이박스 커튼 조각(<see cref="watch"/>)을 지켜보다가,
    /// 그 조각이 닫힌 자리에서 벗어나면 천 커튼(프로토타입 <c>CurtainController</c>)을 열고, 돌아오면 닫는다.
    /// 그레이박스 조각은 렌더러만 꺼 두고 위치·동작은 그대로 쓰므로 기능 코드(⑧ 전문가 대기실 등)를 바꾸지 않는다.
    /// </summary>
    public sealed class WaitingRoomCurtainFollow : MonoBehaviour
    {
        [Tooltip("열고 닫을 천 커튼.")]
        public CurtainController curtain;
        [Tooltip("기능 스크립트가 움직이는 그레이박스 커튼 조각.")]
        public Transform watch;
        [Tooltip("닫힌 자리에서 이만큼(m) 벗어나면 열린 것으로 본다.")]
        public float threshold = 0.1f;

        Vector3 closed;
        bool open;

        void Start()
        {
            if (watch != null) closed = watch.localPosition;
        }

        void Update()
        {
            if (curtain == null || watch == null) return;
            bool now = (watch.localPosition - closed).sqrMagnitude > threshold * threshold;
            if (now == open) return;
            open = now;
            if (open) curtain.Open();
            else curtain.Rest();
        }
    }
}
