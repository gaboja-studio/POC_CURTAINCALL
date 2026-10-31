using CurtainCall.Campaign;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>The matching arrival room and staircase are owned exclusively by the escape scene.</summary>
    public sealed class EscapeSceneArrival : MonoBehaviour
    {
        [SerializeField] BoxCollider assemblyArea, firstPersonArea;
        [SerializeField] GameObject forwardBarrier, returnBarrier;
        [SerializeField] CameraViewPreset approachView, firstPersonView;
        public static EscapeSceneArrival Current { get; private set; }
        public Transform AssemblyAnchor => assemblyArea.transform;
        public BoxCollider FirstPersonArea => firstPersonArea;
        CameraViewDirector cameraRequest;
        bool firstPerson;
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
            if (!firstPerson && transfer.Ready && player != null && player.State == PlayerState.Normal
                && EscapeEntrance.Contains(firstPersonArea, player.transform.position)) firstPerson = true;
            var director = CameraViewDirector.Current;
            if (cameraRequest != director) { ReleaseCamera(); cameraRequest = director; }
            if (cameraRequest != null) cameraRequest.RequestView(this, firstPerson ? firstPersonView : approachView, 75);
        }
        void ReleaseCamera()
        {
            if (cameraRequest != null) cameraRequest.ReleaseView(this);
            cameraRequest = null;
        }
        void OnDestroy() { ReleaseCamera(); if (Current == this) Current = null; }
    }
}
