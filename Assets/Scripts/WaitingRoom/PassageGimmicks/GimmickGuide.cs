using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 기믹 앞에 처음 다가갔을 때 화면 위 가운데 안내 패널에 짧은 설명을 한 번 띄운다(2026-11-13 사용자). 내 화면에서만, 같은 안내는 접속 중 한 번만.
    /// 문구는 문구 파일의 guide.&lt;guideId&gt;.title / .body가 우선한다.
    /// </summary>
    public sealed class GimmickGuide : MonoBehaviour
    {
        [SerializeField] string guideId = "power-door";
        [SerializeField] string title = "안내";
        [SerializeField, TextArea] string body = "";
        [SerializeField, Min(.5f)] float radius = 5f;
        [Tooltip("이 높이 차이(m) 안에 있을 때만 뜬다. 환풍구처럼 바닥 위에 떠 있는 곳은 작게 잡는다.")]
        [SerializeField, Min(.1f)] float height = 3f;
        [SerializeField, Min(1f)] float seconds = 8f;
        static readonly System.Collections.Generic.HashSet<string> shown = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => shown.Clear();

        /// <summary>같은 안내를 코드에서 띄운다(접속 중 한 번). 이미 띄웠으면 아무 일도 없다.</summary>
        public static void ShowOnce(string id, string fallbackTitle, string fallbackBody, float seconds = 8f)
        {
            if (string.IsNullOrEmpty(id) || !shown.Add(id)) return;
            WaitingRoomGuidePanel.Show(WaitingRoomText.Get("guide." + id + ".title", fallbackTitle), WaitingRoomText.Get("guide." + id + ".body", fallbackBody), seconds);
        }

        void Update()
        {
            if (shown.Contains(guideId) || ExplorationState.Instance == null || !(ExplorationState.Instance.LocalFeet is Vector3 feet)) return;
            if (Mathf.Abs(feet.y - transform.position.y) > height || new Vector2(feet.x - transform.position.x, feet.z - transform.position.z).sqrMagnitude > radius * radius) return;
            shown.Add(guideId);
            WaitingRoomGuidePanel.Show(WaitingRoomText.Get("guide." + guideId + ".title", title), WaitingRoomText.Get("guide." + guideId + ".body", body), seconds);
        }
    }
}
