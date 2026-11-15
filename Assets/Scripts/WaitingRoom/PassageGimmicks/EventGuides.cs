using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 자리가 아니라 상황으로 띄우는 안내(2026-11-14 사용자). 접속 중 한 번씩, 내 화면에서만. 카메라 이동 없이 화면 안내만 띄운다.
    /// 문구는 문구 파일 guide.&lt;id&gt;.title / .body.
    /// - pursuer: 탐색 제한 시간이 <see cref="pursuerSeconds"/>초 아래로 내려갔을 때.
    /// - practice: 로비에서 연습실로 처음 들어갔을 때(첫 공연 전 리허설 때는 띄우지 않는다).
    /// - power-circuit: 배터리를 꽂아 작업실·소품실 문이 열린 뒤 그 문을 지나 안으로 들어온 순간(2026-11-15 사용자). 회로를 이미 고장 냈으면 띄우지 않는다.
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
        // 문과 소켓은 처음 쓸 때 대상 ID로 찾는다. 문턱을 바깥(소켓 쪽)에서 안쪽으로 넘는 순간에만 안내한다.
        Bounds? craftDoor; Vector3 craftOutside; bool craftSearched, craftWasOutside;

        void Update()
        {
            var run = CampaignRun.Instance;
            if (run != null && run.State.Timer == RestTimerState.Running && run.State.TimeRemaining > 0f && run.State.TimeRemaining <= pursuerSeconds)
                GimmickGuide.ShowOnce("pursuer", "추격자", "시간이 지나면 추격자가 나타납니다. 추격자를 피해 공연장까지 가야 합니다.");

            if (WaitingRoomRehearsal.IsPreTutorialRestricted || ExplorationState.Instance == null || !(ExplorationState.Instance.LocalFeet is Vector3 feet)) return;
            var state = ExplorationState.Instance;
            // 방 구역에 있기만 하면 뜨던 것을, 열린 문을 직접 지나 들어온 순간으로 바꿨다(2026-11-15 사용자: 문을 열고 입장한 다음에 나와야 한다).
            if (!craftSearched)
            {
                craftSearched = true;
                ExplorationGate door = null, socket = null;
                foreach (var gate in FindObjectsByType<ExplorationGate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (gate.TargetId == craftSocketId) socket = gate;
                    else if (!string.IsNullOrEmpty(gate.OpenedBy) && System.Array.IndexOf(gate.OpenedBy.Split('|'), craftSocketId) >= 0 && gate.GetComponent<Collider>() != null) door = gate;
                }
                if (door != null && socket != null)
                {
                    // 열린 문은 막음 충돌이 꺼져 크기를 못 잰다 → 꺼져 있으면 상자 설정값으로 잰다.
                    var block = door.GetComponent<Collider>();
                    craftDoor = block.enabled || !(block is BoxCollider shape) ? block.bounds : new Bounds(shape.transform.TransformPoint(shape.center), Vector3.Scale(shape.size, shape.transform.lossyScale));
                    craftOutside = socket.transform.position;
                }
            }
            if (craftDoor is Bounds box)
            {
                // 문짝이 얇은 쪽이 지나가는 방향이다. 문 폭 안, 문턱 앞뒤 2.5m 안에서만 어느 쪽에 있는지 본다.
                int along = box.size.x < box.size.z ? 0 : 2, across = 2 - along;
                float depth = feet[along] - box.center[along];
                if (Mathf.Abs(feet[across] - box.center[across]) > box.extents[across] || Mathf.Abs(depth) > 2.5f || Mathf.Abs(feet.y - box.min.y) > 1.5f) craftWasOutside = false;
                else if (depth * (craftOutside[along] - box.center[along]) > 0f) craftWasOutside = true;
                else if (craftWasOutside)
                {
                    craftWasOutside = false;
                    if (state.IsOpen(craftSocketId) && !state.IsOpen(craftCircuitId))
                        GimmickGuide.ShowOnce("power-circuit", "잠긴 문", "잠긴 문을 열린 채로 유지하려면 전력을 끊어 문을 고장내거나, 배터리를 삽입해야 합니다.");
                }
            }
            if (practiceArea == null) { var area = GameObject.Find("Area_S09_PracticeRoom"); practiceArea = area != null ? area.GetComponent<Collider>() : null; if (practiceArea == null) return; }
            bool inside = practiceArea.bounds.Contains(feet + Vector3.up * .5f);
            if (inside && wasOutsidePractice) GimmickGuide.ShowOnce("practice", "연습실", "연습실에서 자유롭게 공연을 연습할 수 있습니다.");
            if (!inside) wasOutsidePractice = true;
        }
    }
}
