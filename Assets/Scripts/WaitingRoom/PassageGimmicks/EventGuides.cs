using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 자리가 아니라 상황으로 띄우는 안내(2026-11-14 사용자). 접속 중 한 번씩, 내 화면에서만. 카메라 이동 없이 화면 안내만 띄운다.
    /// 문구는 문구 파일 guide.&lt;id&gt;.title / .body.
    /// - pursuer: 탐색 제한 시간이 <see cref="pursuerSeconds"/>초 아래로 내려갔을 때.
    /// - practice: 로비에서 연습실로 처음 들어갔을 때(첫 공연 전 리허설 때는 띄우지 않는다).
    /// </summary>
    public sealed class EventGuides : MonoBehaviour
    {
        [Tooltip("남은 탐색 시간이 이 값(초) 아래로 내려가면 추격자 안내를 띄운다.")]
        [SerializeField, Min(1f)] float pursuerSeconds = 30f;
        [Tooltip("연습실 구역(이 충돌체 범위 안에 들어가면 연습실 안내). 비우면 이름으로 찾는다.")]
        [SerializeField] Collider practiceArea;
        bool wasOutsidePractice;

        void Update()
        {
            var run = CampaignRun.Instance;
            if (run != null && run.State.Timer == RestTimerState.Running && run.State.TimeRemaining > 0f && run.State.TimeRemaining <= pursuerSeconds)
                GimmickGuide.ShowOnce("pursuer", "추격자", "시간이 지나면 추격자가 나타납니다. 추격자를 피해 공연장까지 가야 합니다.");

            if (WaitingRoomRehearsal.IsPreTutorialRestricted || ExplorationState.Instance == null || !(ExplorationState.Instance.LocalFeet is Vector3 feet)) return;
            if (practiceArea == null) { var area = GameObject.Find("Area_S09_PracticeRoom"); practiceArea = area != null ? area.GetComponent<Collider>() : null; if (practiceArea == null) return; }
            bool inside = practiceArea.bounds.Contains(feet + Vector3.up * .5f);
            if (inside && wasOutsidePractice) GimmickGuide.ShowOnce("practice", "연습실", "연습실에서 자유롭게 공연을 연습할 수 있습니다.");
            if (!inside) wasOutsidePractice = true;
        }
    }
}
