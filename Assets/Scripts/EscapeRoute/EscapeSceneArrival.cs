using CurtainCall.Campaign;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>
    /// The matching arrival room and staircase are owned exclusively by the escape scene.
    /// 계단 접근 중에는 근접 3인칭(approachView)을 요청하고, 계단 끝 구역(firstPersonArea)에 들어서면 요청을 해제해
    /// 다른 공간 플랫폼과 같은 공용 자유 3인칭으로 진행한다(Task-089, 이전 계단 끝 1인칭 대체).
    /// </summary>
    public sealed class EscapeSceneArrival : MonoBehaviour
    {
        [SerializeField] BoxCollider assemblyArea;
        [Tooltip("계단 끝 구역. 들어서면 접근 시점을 해제한다(이름은 이전 1인칭 전환 구역에서 유지).")]
        [SerializeField] BoxCollider firstPersonArea;
        [SerializeField] GameObject forwardBarrier, returnBarrier;
        [SerializeField] CameraViewPreset approachView;
        public static EscapeSceneArrival Current { get; private set; }
        public Transform AssemblyAnchor => assemblyArea.transform;
        public BoxCollider FirstPersonArea => firstPersonArea;
        CameraViewDirector cameraRequest;
        bool reachedStairEnd;
        void Awake() => Current = this;
        void Update()
        {
            var transfer = EscapeSceneTransfer.Current;
            bool campaign = CampaignRun.Instance != null && CampaignRun.Instance.State.Phase == CampaignPhase.Escape
                && transfer != null && transfer.HasDeparture;
            if (!campaign) { ReleaseCamera(); return; }
            if (returnBarrier != null) returnBarrier.SetActive(true);
            if (forwardBarrier != null) forwardBarrier.SetActive(!transfer.Ready);
            transfer.PlaceLocal(this);
            var player = NetworkPlayer.Local;
            if (!reachedStairEnd && transfer.Ready && player != null && player.State == PlayerState.Normal
                && EscapeEntrance.Contains(firstPersonArea, player.transform.position)) reachedStairEnd = true;
            if (reachedStairEnd) { ReleaseCamera(); return; }
            var director = CameraViewDirector.Current;
            if (cameraRequest != director) { ReleaseCamera(); cameraRequest = director; }
            if (cameraRequest != null) cameraRequest.RequestView(this, approachView, 75);
        }
        void ReleaseCamera()
        {
            if (cameraRequest != null) cameraRequest.ReleaseView(this);
            cameraRequest = null;
        }
        void OnDestroy() { ReleaseCamera(); if (Current == this) Current = null; }
    }
}
