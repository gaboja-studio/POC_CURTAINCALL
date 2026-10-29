using System;
using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 밤 정비 동안 대기실 문(로비 ↔ ⑥ 무대 입구 통로)을 잠근다. 기준: night-maintenance.md — 문이 잠기고 무대 입구 통로로 갈 수 없으며 탐색 제한 시간이 없다.
    /// 문은 공장식 롤링 셔터다. 밤이 되면 위에서 내려와 닫히고(셔터 판의 충돌체가 통로를 막음), 아침 쉬는 시간이 되면 말려 올라가 열린다.
    /// 움직이는 동안 경고등이 깜박인다. 잠김 여부는 모두에게 공유되는 런 상태(<see cref="CampaignRun.State"/>)로만 정하므로 네 화면이 같다.
    /// 타이머는 밤에 공용 상태가 None이라 따로 멈추지 않고, 화면 위쪽에 밤 정비 안내만 띄운다.
    /// </summary>
    public sealed class NightDoorLock : MonoBehaviour
    {
        [Tooltip("셔터 판 묶음. 위쪽 끝이 기준점이고, 세로 크기를 줄여 말려 올라간 모습을 낸다. 4단계 씬 구성 메뉴가 만든다.")]
        [SerializeField] Transform shutter;

        [Tooltip("잠겼을 때만 켜는 오브젝트(잠김 표지).")]
        [SerializeField] GameObject lockedDoor;

        [Tooltip("셔터가 움직이는 동안 깜박이는 경고등.")]
        [SerializeField] Renderer warningLamp;

        [Tooltip("다 닫히거나 열리는 데 걸리는 시간(초).")]
        [SerializeField, Min(.1f)] float moveSeconds = 2.5f;

        [Tooltip("다 열렸을 때 남는 셔터 세로 비율(말림 통 아래로 살짝 보이는 정도).")]
        [SerializeField, Range(.01f, 1f)] float openScale = .06f;

        [Tooltip("경고등 깜박임 간격(초).")]
        [SerializeField, Min(.05f)] float blinkSeconds = .25f;

        [Tooltip("밤 정비 동안 화면 위쪽에 띄우는 안내.")]
        [SerializeField] string notice = "밤 정비 — 대기실 문이 잠겼습니다 · 탐색 시간 없음";
        [SerializeField] bool showLegacyNotice = true;

        GUIStyle style;
        float closed; // 0 = 다 열림, 1 = 다 닫힘

        /// <summary>지금 문이 잠겨 있는가(셔터가 내려오는 중 포함).</summary>
        public bool IsLocked { get; private set; }

        /// <summary>셔터가 다 내려와 통로가 막혔는가.</summary>
        public bool IsFullyClosed => closed >= 1f;

        /// <summary>잠김이 바뀌었다(true = 잠김).</summary>
        public event Action<bool> LockChanged;

        void OnEnable()
        {
            // 씬이 열릴 때는 움직이지 않고 지금 상태로 바로 둔다(밤에 대기실로 돌아오면 이미 닫혀 있다).
            Apply(ShouldLock(), true);
            closed = IsLocked ? 1f : 0f;
            Pose();
        }

        void Update()
        {
            bool locked = ShouldLock();
            if (locked != IsLocked) Apply(locked, false);

            float target = IsLocked ? 1f : 0f;
            if (!Mathf.Approximately(closed, target))
            {
                closed = Mathf.MoveTowards(closed, target, Time.deltaTime / moveSeconds);
                Pose();
            }
            if (warningLamp != null)
            {
                bool moving = !Mathf.Approximately(closed, target);
                warningLamp.enabled = !moving || Mathf.Repeat(Time.time, blinkSeconds * 2f) < blinkSeconds;
            }
        }

        static bool ShouldLock()
        {
            var run = CampaignRun.Instance;
            return run != null && (run.State.Phase == CampaignPhase.Night || run.State.Phase == CampaignPhase.Dawn
                || run.State.Phase == CampaignPhase.DemoEnding);
        }

        void Apply(bool locked, bool initial)
        {
            IsLocked = locked;
            if (lockedDoor != null) lockedDoor.SetActive(locked);
            if (initial) return;
            Debug.Log(locked ? "[NightSchedule] 밤 정비 — 대기실 문(셔터) 닫힘" : "[NightSchedule] 대기실 문(셔터) 열림");
            LockChanged?.Invoke(locked);
        }

        void Pose()
        {
            if (shutter == null) return;
            // 끝으로 갈수록 천천히 닿게 한다.
            float eased = Mathf.SmoothStep(0f, 1f, closed);
            var scale = shutter.localScale;
            scale.y = Mathf.Lerp(openScale, 1f, eased);
            shutter.localScale = scale;
        }

        void OnGUI()
        {
            if (!showLegacyNotice || !IsLocked || CampaignRun.Instance?.State.Phase == CampaignPhase.DemoEnding) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            const float width = 420, height = 32;
            GUI.Box(new Rect((Screen.width - width) / 2f, 10, width, height), notice, style);
        }
    }
}
