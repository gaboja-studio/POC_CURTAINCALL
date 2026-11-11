using System.Collections;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
        GameObject resultPanel;
        TMP_Text returnLabel;
        HudRoleCard outcome;
        [SerializeField] string resultHeading = "탈출 성공";
        Button returnButton;
        bool returnRequested;
        readonly HashSet<ulong> observedArrivals = new();
        int displayedArrivals = -1, displayedRetired = -1;

        public static bool IsSuccessfulEnding(CampaignSnapshot state) => state.Phase == CampaignPhase.Ending
            && state.LastEnd == RunEndReason.EscapeSucceeded;

        public static float RevealAlpha(double elapsed, float delay, float fade)
        {
            if (!double.IsFinite(elapsed) || elapsed < 0 || !float.IsFinite(delay) || !float.IsFinite(fade)) return 0;
            return Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)(elapsed - Mathf.Max(0, delay)) / Mathf.Max(.1f, fade)));
        }

        public static bool CanReveal(PlayerState state, bool doorOpen, bool inside, bool inspecting) =>
            !inspecting && doorOpen && (state == PlayerState.Arrived || state == PlayerState.Normal && inside);

        public static bool CanConfirmResult(CampaignSnapshot state, bool authority, bool requested, double elapsed, float seconds)
            => IsSuccessfulEnding(state) && authority && !requested && double.IsFinite(elapsed)
                && elapsed >= Mathf.Clamp(seconds, 1, 15);

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
            returnRequested = false;
            observedArrivals.Clear();
            displayedArrivals = displayedRetired = -1;
            if (resultPanel != null) resultPanel.SetActive(false);
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
            foreach (var actor in NetworkPlayer.All)
                if (actor != null && actor.IsSpawned && actor.State == PlayerState.Arrived)
                    observedArrivals.Add(actor.OwnerClientId);
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
            if (Time.unscaledTimeAsDouble - endingAt >= Mathf.Clamp(endingSeconds, 1, 15)) ShowResult();
        }

        void ShowResult()
        {
            if (resultPanel == null) CreateResult();
            resultPanel.SetActive(true);
            int participants = 0;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned)
                    participants++;
            var transfer = EscapeSceneTransfer.Current;
            if (transfer != null && transfer.HasDeparture) participants = transfer.Participants.Count;
            int arrived = observedArrivals.Count, retired = Mathf.Max(0, participants - arrived);
            if (arrived != displayedArrivals || retired != displayedRetired)
            {
                outcome.SetText($"탈출 {arrived}명 · 리타이어 {retired}명", resultHeading);
                displayedArrivals = arrived; displayedRetired = retired;
            }
            var run = CampaignRun.Instance;
            bool authority = run != null && run.HasAuthority;
            returnButton.interactable = authority && !returnRequested;
            string prompt = authority ? "로비로 돌아가기" : "방장의 결과 확인을 기다리는 중";
            if (returnLabel.text != prompt) returnLabel.text = prompt;
        }

        public bool ReturnToLobby()
        {
            var run = CampaignRun.Instance;
            if (run == null || endingAt < 0 || !CanConfirmResult(run.State, run.HasAuthority, returnRequested,
                    Time.unscaledTimeAsDouble - endingAt, endingSeconds)) return false;
            // Release the pending Hub transition while keeping Ending/Ended authoritative.
            // The existing lobby lever explicitly prepares the next run; never auto-start day one.
            returnRequested = true;
            return true;
        }

        void CreateResult()
        {
            resultPanel = new GameObject("Escape Result", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            resultPanel.transform.SetParent(transform, false);
            var canvas = resultPanel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = resultPanel.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            outcome = HudUIManager.Current.ShowModalOutcome(" ", transform, resultHeading, resultPanel.transform);
            var button = new GameObject("Return To Lobby", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            button.transform.SetParent(outcome.rows.transform, false);
            var size = button.GetComponent<LayoutElement>(); size.preferredWidth = 350; size.preferredHeight = 50;
            button.GetComponent<Image>().color = new Color(.22f,.23f,.2f,1);
            returnButton = button.GetComponent<Button>(); returnButton.onClick.AddListener(() => ReturnToLobby());
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(button.transform,false);
            var rect=(RectTransform)label.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            returnLabel=label.GetComponent<TMP_Text>(); returnLabel.font=outcome.body.font; returnLabel.fontSize=24;
            returnLabel.alignment=TextAlignmentOptions.Center; returnLabel.raycastTarget=false;
            HudUIManager.Current.GetComponent<UiRuntimeFontFallback>()?.WarmUpLocalText(label.transform,"로비로 돌아가기방장의 결과 확인을 기다리는 중");
            outcome.Fit();
            if (EventSystem.current == null)
            {
                var events = new GameObject("EscapeResultEvents", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(resultPanel.transform, false);
            }
        }

        public IEnumerator HoldBeforeTransition()
        {
            BeginEnding();
            // Host keeps the shared scene visible; each client renders its own ending on the same campaign state.
            while (isActiveAndEnabled && !returnRequested
                && CampaignRun.Instance != null && IsSuccessfulEnding(CampaignRun.Instance.State)) yield return null;
        }
    }
}
