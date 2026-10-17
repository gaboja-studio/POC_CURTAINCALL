using System;
using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 밤 정비 동안 대기실 문(로비 ↔ ⑥ 무대 입구 통로)을 잠근다. 기준: night-maintenance.md — 문이 잠기고 무대 입구 통로로 갈 수 없으며 탐색 제한 시간이 없다.
    /// 잠김 여부는 모두에게 공유되는 런 상태(<see cref="CampaignRun.State"/>)로만 정하므로 네 화면이 같다. 아침 쉬는 시간이 되면 다시 열린다.
    /// 타이머는 밤에 공용 상태가 None이라 따로 멈추지 않고, 화면 위쪽에 밤 정비 안내만 띄운다.
    /// </summary>
    public sealed class NightDoorLock : MonoBehaviour
    {
        [Tooltip("잠겼을 때 켜는 오브젝트(문을 막는 벽·잠김 표지). 4단계 씬 구성 메뉴가 만든다.")]
        [SerializeField] GameObject lockedDoor;

        [Tooltip("밤 정비 동안 화면 위쪽에 띄우는 안내.")]
        [SerializeField] string notice = "밤 정비 — 대기실 문이 잠겼습니다 · 탐색 시간 없음";

        GUIStyle style;

        /// <summary>지금 문이 잠겨 있는가.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>잠김이 바뀌었다(true = 잠김).</summary>
        public event Action<bool> LockChanged;

        void OnEnable() => Apply(ShouldLock(), true);

        void Update()
        {
            bool locked = ShouldLock();
            if (locked != IsLocked) Apply(locked, false);
        }

        static bool ShouldLock()
        {
            var run = CampaignRun.Instance;
            return run != null && run.State.Phase == CampaignPhase.Night;
        }

        void Apply(bool locked, bool initial)
        {
            IsLocked = locked;
            if (lockedDoor != null) lockedDoor.SetActive(locked);
            if (initial) return;
            Debug.Log(locked ? "[NightSchedule] 밤 정비 — 대기실 문 잠김" : "[NightSchedule] 대기실 문 열림");
            LockChanged?.Invoke(locked);
        }

        void OnGUI()
        {
            if (!IsLocked) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            const float width = 420, height = 32;
            GUI.Box(new Rect((Screen.width - width) / 2f, 10, width, height), notice, style);
        }
    }
}
