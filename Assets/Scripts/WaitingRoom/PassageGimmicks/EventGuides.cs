using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 자리가 아니라 상황으로 띄우는 안내(2026-11-14 사용자). 접속 중 한 번씩, 내 화면에서만. 카메라 이동 없이 화면 안내만 띄운다.
    /// 문구는 문구 파일 guide.&lt;id&gt;.title / .body.
    /// - pursuer: 탐색 제한 시간이 <see cref="pursuerSeconds"/>초 아래로 내려갔을 때.
    /// - practice: 로비에서 연습실로 처음 들어갔을 때(첫 공연 전 리허설 때는 띄우지 않는다).
    /// - power-circuit: 배터리를 꽂아 작업실·소품실 문이 열린 뒤 그 안으로 들어왔을 때(2026-11-15 사용자). 회로를 이미 고장 냈으면 띄우지 않는다.
    /// </summary>
    public sealed class EventGuides : MonoBehaviour
    {
        [Tooltip("남은 탐색 시간이 이 값(초) 아래로 내려가면 추격자 안내를 띄운다.")]
        [SerializeField, Min(1f)] float pursuerSeconds = 30f;
        [Tooltip("연습실 구역(이 충돌체 범위 안에 들어가면 연습실 안내). 비우면 이름으로 찾는다.")]
        [SerializeField] Collider practiceArea;
        bool wasOutsidePractice;
        [Header("작업실·소품실 문 고정 안내")]
        [Tooltip("배터리 소켓(꽂혀 있는 동안 열림).")]
        [SerializeField] string craftSocketId = "learning:craft-socket";
        [Tooltip("회로(고장 나면 문이 열린 채 고정). 고장 난 뒤에는 안내하지 않는다.")]
        [SerializeField] string craftCircuitId = "learning:craft-circuit";
        [Tooltip("문 안쪽 구역(탐색 구역 물체 이름). 이 안에 들어오면 안내한다.")]
        [SerializeField] string[] craftAreaNames = { "Area_S10_Workshop", "Area_S04_PropRoom" };
        Collider[] craftAreas;

        void Update()
        {
            var run = CampaignRun.Instance;
            if (run != null && run.State.Timer == RestTimerState.Running && run.State.TimeRemaining > 0f && run.State.TimeRemaining <= pursuerSeconds)
                GimmickGuide.ShowOnce("pursuer", "추격자", "시간이 지나면 추격자가 나타납니다. 추격자를 피해 공연장까지 가야 합니다.");

            if (WaitingRoomRehearsal.IsPreTutorialRestricted || ExplorationState.Instance == null || !(ExplorationState.Instance.LocalFeet is Vector3 feet)) return;
            var state = ExplorationState.Instance;
            if (state.IsOpen(craftSocketId) && !state.IsOpen(craftCircuitId))
            {
                if (craftAreas == null)
                {
                    craftAreas = new Collider[craftAreaNames.Length];
                    for (int i = 0; i < craftAreaNames.Length; i++) { var area = GameObject.Find(craftAreaNames[i]); craftAreas[i] = area != null ? area.GetComponent<Collider>() : null; }
                }
                foreach (var area in craftAreas)
                    if (area != null && area.bounds.Contains(feet + Vector3.up * .5f))
                    { GimmickGuide.ShowOnce("power-circuit", "잠긴 문", "전력을 끊어 문을 고장내거나, 배터리를 계속 삽입하세요."); break; }
            }
            if (practiceArea == null) { var area = GameObject.Find("Area_S09_PracticeRoom"); practiceArea = area != null ? area.GetComponent<Collider>() : null; if (practiceArea == null) return; }
            bool inside = practiceArea.bounds.Contains(feet + Vector3.up * .5f);
            if (inside && wasOutsidePractice) GimmickGuide.ShowOnce("practice", "연습실", "연습실에서 자유롭게 공연을 연습할 수 있습니다.");
            if (!inside) wasOutsidePractice = true;
        }
    }
}
