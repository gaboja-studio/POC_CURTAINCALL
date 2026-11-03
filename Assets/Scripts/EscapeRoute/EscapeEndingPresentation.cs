using System.Collections;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>갈대밭 도착의 로컬 표시. 성공 집계는 EscapeCampaignContent가 계속 소유한다.</summary>
    [DefaultExecutionOrder(900)]
    public sealed class EscapeEndingPresentation : MonoBehaviour
    {
        [SerializeField] EscapeCampaignContent content;
        [SerializeField] EscapeExitSaw exitSaw;
        [SerializeField] BoxCollider arrivalArea;
        [SerializeField] CanvasGroup title;
        [SerializeField] CinemachineCamera endingCamera;
        [SerializeField, Min(0)] float revealDelay = 1f;
        [SerializeField, Min(.1f)] float fadeSeconds = 1.8f;
        [SerializeField, Min(1)] float endingSeconds = 6f;
        public static EscapeEndingPresentation Current { get; private set; }
        double shownAt = -1, endingAt = -1;
        CommonInventoryUI inventory;
        EscapeInspectionSession inspection;
        PerformanceSpectatorBinding spectator;

        public static bool IsSuccessfulEnding(CampaignSnapshot state) => state.Phase == CampaignPhase.Ending
            && state.LastEnd == RunEndReason.EscapeSucceeded;

        public static float RevealAlpha(double elapsed, float delay, float fade)
        {
            if (!double.IsFinite(elapsed) || elapsed < 0 || !float.IsFinite(delay) || !float.IsFinite(fade)) return 0;
            return Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)(elapsed - Mathf.Max(0, delay)) / Mathf.Max(.1f, fade)));
        }

        public static bool CanReveal(PlayerState state, bool doorOpen, bool inside, bool inspecting) =>
            !inspecting && doorOpen && (state == PlayerState.Arrived || state == PlayerState.Normal && inside);

        void OnEnable()
        {
            Current = this;
            inspection = FindFirstObjectByType<EscapeInspectionSession>();
            NetworkPlayer.Restarted += Restarted;
            ResetPresentation();
        }
        void OnDisable()
        {
            NetworkPlayer.Restarted -= Restarted;
            ResetPresentation();
            if (Current == this) Current = null;
        }
        void Restarted(NetworkPlayer player) { if (player == NetworkPlayer.Local) ResetPresentation(); }
        void ResetPresentation()
        {
            shownAt = endingAt = -1;
            if (title != null) { title.alpha = 0; title.interactable = title.blocksRaycasts = false; }
            if (endingCamera != null) endingCamera.enabled = false;
            InputGate.SetBlocking(this, false);
        }
        void BeginEnding()
        {
            if (endingAt >= 0) return;
            endingAt = Time.unscaledTimeAsDouble;
            if (shownAt < 0) shownAt = endingAt;
            if (endingCamera != null) endingCamera.enabled = true;
            InputGate.SetBlocking(this, true);
        }
        void LateUpdate()
        {
            if (content == null || !content.IsActive) return;
            var campaign = CampaignRun.Instance;
            if (campaign != null && IsSuccessfulEnding(campaign.State)) BeginEnding();
            var local = NetworkPlayer.Local;
            if (shownAt < 0 && local != null && local.IsSpawned && CanReveal(local.State,
                    exitSaw != null && exitSaw.DoorOpen, EscapeEntrance.Contains(arrivalArea, local.transform.position),
                    inspection != null && inspection.IsInspecting(local.OwnerClientId))) shownAt = Time.unscaledTimeAsDouble;
            if (title != null) title.alpha = shownAt < 0 ? 0 : RevealAlpha(Time.unscaledTimeAsDouble - shownAt, revealDelay, fadeSeconds);
            if (endingAt < 0) return;
            CommonPerformanceUI.Current?.HideHints();
            if (spectator == null) spectator = CommonPerformanceUI.Current != null ? CommonPerformanceUI.Current.GetComponent<PerformanceSpectatorBinding>() : null;
            spectator?.View?.HideImmediate();
            if (inventory == null) inventory = FindFirstObjectByType<CommonInventoryUI>();
            if (inventory != null && inventory.group != null) inventory.group.alpha = 0;
        }
        public IEnumerator HoldBeforeTransition()
        {
            BeginEnding();
            // Host keeps the shared scene visible; each client renders its own ending on the same campaign state.
            double until = endingAt + Mathf.Clamp(endingSeconds, 1, 15);
            while (isActiveAndEnabled && Time.unscaledTimeAsDouble < until
                && CampaignRun.Instance != null && IsSuccessfulEnding(CampaignRun.Instance.State)) yield return null;
        }
    }
}
