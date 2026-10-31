using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 공연을 여는 배치 기물 세트(기물 기획 "트램펄린과 대포", 2026-10-30 사용자 지시: 무대 장치 보관실, F로 획득).
    /// 세트의 맵 기물(<see cref="PerformancePropPickup"/>) 중 하나에서 F를 누르면 호스트에 해금을 요청하고(<see cref="CampaignRun.DiscoverTrick"/>), 공연이 열린다.
    /// 해금은 런 상태로 모두에게 공유되며(늦게 들어온 사람 포함) 런 초기화 때 닫힌다. 열리면 맵의 기물은 사라지고 연습실의 같은 기물이 모든 화면에 보인다.
    /// </summary>
    public sealed class PerformancePropDiscovery : MonoBehaviour
    {
        [SerializeField] string trickId = CampaignSnapshot.HumanCannonId;
        [SerializeField] string trickName = "인간 대포";
        [Tooltip("탐색 중 F로 획득할 맵 위 기물(세트). 해금되면 숨긴다.")]
        [SerializeField] GameObject[] mapProps;
        [Tooltip("연습실로 옮겨진 기물. 해금 전에는 숨긴다.")]
        [SerializeField] GameObject[] practiceProps;
        [SerializeField, Min(1f)] float noticeSeconds = 8f;

        float requestedAt = -10f;
        bool? shownUnlocked;

        public string TrickId => trickId;
        public string TrickName => trickName;
        public bool Unlocked => CampaignRun.Instance != null && CampaignRun.Instance.IsTrickUnlocked(trickId);
        /// <summary>런 진행 중이고 아직 열리지 않아 획득할 수 있는지.</summary>
        public bool CanAcquire => !Unlocked && CampaignRun.Instance != null && CampaignRun.Instance.State.IsRunning;

        public void Configure(string trick, string name, GameObject[] onMap, GameObject[] inPractice)
        {
            trickId = trick; trickName = name; mapProps = onMap; practiceProps = inPractice; shownUnlocked = null;
            if (mapProps == null) return;
            foreach (var prop in mapProps)
                if (prop != null) (prop.GetComponent<PerformancePropPickup>() ?? prop.AddComponent<PerformancePropPickup>()).Bind(this);
        }

        /// <summary>세트 중 하나에서 F를 눌렀다. 호스트 응답 전 중복 요청은 1초 동안 막는다.</summary>
        public void Acquire()
        {
            if (!CanAcquire || Time.unscaledTime - requestedAt < 1f) return;
            requestedAt = Time.unscaledTime;
            CampaignRun.Instance.DiscoverTrick(trickId);
        }

        void Update()
        {
            bool unlocked = Unlocked;
            if (shownUnlocked == unlocked) return;
            if (shownUnlocked == false && unlocked) ShowGuide();
            shownUnlocked = unlocked;
            SetActive(mapProps, !unlocked);
            SetActive(practiceProps, unlocked);
        }

        static void SetActive(GameObject[] list, bool on)
        {
            if (list == null) return;
            foreach (var go in list) if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        // 획득 안내(모든 화면). 기존 패널 이미지로 만든 안내 패널을 쓴다.
        void ShowGuide() => WaitingRoomGuidePanel.Show(WaitingRoomText.Format("narration.performance-unlocked-title", "{trick} 획득!", ("trick", trickName)),
            WaitingRoomText.Format("narration.performance-unlocked-body", "'{trick}' 공연이 해금되었습니다. 이제 스케줄표에서 {trick} 공연을 편성할 수 있습니다.\n획득한 {trick}·트램펄린은 연습실로 옮겨졌습니다. 연습실에서 {trick} 공연을 연습할 수 있습니다.", ("trick", trickName)), noticeSeconds);
    }
}
