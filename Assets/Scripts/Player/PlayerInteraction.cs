using System;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 동작 부품: 상호작용·해제 요청 창구. 조작 규칙이 요청하면 신호를 보낸다.
    /// 무엇과 상호작용할지(외줄에서는 목마 연결·해제)는 이 신호를 받는 기능(목마 006)이 정한다.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        /// <summary>상호작용 요청(외줄: 목마 연결 시도, F 짧게).</summary>
        public event Action InteractRequested;

        /// <summary>해제 요청(외줄: 목마 해제, F 길게).</summary>
        public event Action ReleaseRequested;

        /// <summary>마지막 요청 이름과 시각. 디버그 표시용.</summary>
        public string LastRequest { get; private set; } = "-";
        public float LastRequestTime { get; private set; } = -10f;

        /// <summary>상호작용을 요청한다.</summary>
        public void RequestInteract()
        {
            Record("상호작용");
            InteractRequested?.Invoke();
        }

        /// <summary>해제를 요청한다.</summary>
        public void RequestRelease()
        {
            Record("해제");
            ReleaseRequested?.Invoke();
        }

        void Record(string request)
        {
            LastRequest = request;
            LastRequestTime = Time.time;
        }
    }
}
