using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 연습실 자유 연습 공간(Task-125, 사용자 2026-11-13). 판정·투표·복귀 없이 공연 기물을 그대로 타 본다.
    /// - 외줄·인간 그네 구간: 최초 외줄타기 리허설 시간대가 끝난 뒤부터 보인다.
    /// - 인간 대포 구간(대포 2문·그네·발판·운반 트램펄린 자리): 인간대포 해금 + 대포 리허설 이수 뒤부터 보인다.
    /// - 리허설(외줄·대포)이 링을 쓰는 동안에는 모두 숨긴다.
    /// 기물은 씬에 배치돼 있고(<c>PracticeRoomFreePlayBuilder</c>) 여기서는 보이는 조건만 다룬다. 낙하 피해는 대기실 공통 설정(안전 착지)을 따른다.
    /// 대포 시스템·운반 트램펄린은 <see cref="WaitingRoomStageDevices"/>가 이 상태를 보고 만든다.
    /// </summary>
    public sealed class PracticeRoomFreePlay : MonoBehaviour
    {
        [SerializeField] GameObject ropeCourse, cannonLane;
        [SerializeField] HumanCannonStation[] cannons = System.Array.Empty<HumanCannonStation>();
        [SerializeField] Transform trampolineSpot;
        [SerializeField] HumanCannonPole[] ropePoles = System.Array.Empty<HumanCannonPole>(), cannonPoles = System.Array.Empty<HumanCannonPole>();

        public static PracticeRoomFreePlay Current { get; private set; }
        public bool CannonLaneOpen => cannonLane != null && cannonLane.activeSelf;
        public HumanCannonStation[] Cannons => cannons;
        public Transform TrampolineSpot => trampolineSpot;

        public void Configure(GameObject ropes, GameObject cannon, HumanCannonStation[] stations, Transform trampoline,
            HumanCannonPole[] onRopes, HumanCannonPole[] onCannonLane)
        {
            ropeCourse = ropes; cannonLane = cannon; cannons = stations; trampolineSpot = trampoline;
            ropePoles = onRopes; cannonPoles = onCannonLane;
        }

        void OnEnable() { Current = this; Apply(false, false); }
        void OnDisable() { if (Current == this) Current = null; }

        void Update()
        {
            var run = CampaignRun.Instance;
            bool ropes = run != null && run.State.IsRunning && run.State.Phase != CampaignPhase.Rehearsal
                && !WaitingRoomRehearsal.IsActive && !WaitingRoomCannonRehearsal.IsActive;
            Apply(ropes, ropes && run.State.CannonUnlocked && WaitingRoomCannonRehearsal.HasMastered);
        }

        void Apply(bool ropes, bool cannon)
        {
            if (ropeCourse != null && ropeCourse.activeSelf != ropes)
            {
                // 리허설과 같은 방식: 목마 시스템이 없을 때만 이 코스가 맡는다.
                if (ropes && (PiggybackSystem.Current == null || !PiggybackSystem.Current.isActiveAndEnabled)
                    && ropeCourse.GetComponent<PiggybackSystem>() == null)
                {
                    ropeCourse.AddComponent<PiggybackSystem>().SuppressLegacyFeedback = true;
                    ropeCourse.AddComponent<PiggybackFollow>();
                }
                ropeCourse.SetActive(ropes);
            }
            if (cannonLane != null && cannonLane.activeSelf != cannon) cannonLane.SetActive(cannon);
            foreach (var pole in ropePoles) if (pole != null) pole.InteractionAvailable = ropes;
            foreach (var pole in cannonPoles) if (pole != null) pole.InteractionAvailable = cannon;
        }
    }
}
