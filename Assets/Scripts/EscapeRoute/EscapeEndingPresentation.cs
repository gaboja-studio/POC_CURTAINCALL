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
        TMP_Text resultLabel, returnLabel;
        TMP_FontAsset resultFont;
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
                resultLabel.text = $"탈출 성공\n\n탈출 {arrived}명 · 리타이어 {retired}명";
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
            resultFont = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/Pretendard-Medium/Pretendard-Medium 1"));
            resultFont.hideFlags = HideFlags.DontSave;
            resultFont.TryAddCharacters("탈출 성공 리타이어 명 · 0123456789로비돌아가기방장의결과확인을기다리는중");
            resultPanel = new GameObject("Escape Result", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            resultPanel.transform.SetParent(transform, false);
            var canvas = resultPanel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = resultPanel.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(resultPanel.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.3f, .27f); rect.anchorMax = new Vector2(.7f, .68f);
            rect.offsetMin = rect.offsetMax = Vector2.zero; panel.GetComponent<Image>().color = new Color(.025f,.025f,.025f,.94f);
            TMP_Text Text(string name, Transform parent, Vector2 min, Vector2 max, float size)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
                var r = go.GetComponent<RectTransform>(); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
                var label = go.GetComponent<TMP_Text>(); label.font = resultFont;
                label.fontSize = size; label.alignment = TextAlignmentOptions.Center; label.color = Color.white; label.raycastTarget = false;
                return label;
            }
            resultLabel = Text("Result", panel.transform, new Vector2(.05f,.37f), new Vector2(.95f,.94f), 36);
            var button = new GameObject("Return To Lobby", typeof(RectTransform), typeof(Image), typeof(Button)); button.transform.SetParent(panel.transform, false);
            var br = button.GetComponent<RectTransform>(); br.anchorMin = new Vector2(.1f,.1f); br.anchorMax = new Vector2(.9f,.29f); br.offsetMin = br.offsetMax = Vector2.zero;
            button.GetComponent<Image>().color = new Color(.22f,.23f,.2f,1);
            returnButton = button.GetComponent<Button>(); returnButton.onClick.AddListener(() => ReturnToLobby());
            returnLabel = Text("Label", button.transform, Vector2.zero, Vector2.one, 27);
            if (EventSystem.current == null)
            {
                var events = new GameObject("EscapeResultEvents", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(resultPanel.transform, false);
            }
        }

        void OnDestroy()
        {
            if (resultFont == null) return;
            if (Application.isPlaying) Destroy(resultFont);
            else DestroyImmediate(resultFont);
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
