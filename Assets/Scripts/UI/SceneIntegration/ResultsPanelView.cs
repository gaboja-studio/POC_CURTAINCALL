using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>C12 read-only presentation. Project code owns settlement, phase changes and input routing.</summary>
    public sealed class ResultsPanelView : MonoBehaviour
    {
        [Serializable]
        public sealed class TextLayers
        {
            public TextMeshProUGUI title, context, subtitle, progressHeading, course;
            public TextMeshProUGUI baseLabel, baseAmount, bonusLabel, bonusAmount;
            public TextMeshProUGUI fundsLabel, contribution, fundsAmount, footer, acknowledge;
        }
        [Serializable]
        public sealed class MemberLayers
        {
            public RectTransform root;
            public TextMeshProUGUI label, distance, number;
            public Image connector, marker, fall;
        }
        [SerializeField] private CanvasGroup visuals;
        [SerializeField] private RectTransform layoutStage;
        [SerializeField] private Image ribbon, grandCrest, maximumMarker;
        [SerializeField] private Sprite failureRibbon, successRibbon, grandRibbon;
        [SerializeField] private Sprite selfMarker, teammateMarker;
        [SerializeField] private TextLayers textLayers;
        [SerializeField] private MemberLayers[] memberLayers = new MemberLayers[4];
        [SerializeField] private CurtainCall.UI.ResultsV1.CurtainCallButton acknowledge;
        [SerializeField] private ResultsRewardTransfer rewardTransfer;
        public event Action<string> AcknowledgeRequested;
        public event Action Hidden;
        public bool IsVisible { get { return visible; } }
        private ResultsPresentation model;
        private bool visible, bound, dispatchingAcknowledge;
        private long epoch;
        private Vector2 lastViewport;

        public void Configure(CanvasGroup visualGroup, RectTransform stage, Image titleRibbon, Image crest,
            Image maximum, Sprite failure, Sprite success, Sprite grand, Sprite self, Sprite teammate,
            TextLayers texts, MemberLayers[] members, CurtainCall.UI.ResultsV1.CurtainCallButton confirm, ResultsRewardTransfer transfer)
        {
            long configurationEpoch = ++epoch; StopAllCoroutines(); Unbind(); ForceHidden();
            if (this == null || epoch != configurationEpoch) return;
            visuals = visualGroup; layoutStage = stage; ribbon = titleRibbon; grandCrest = crest;
            maximumMarker = maximum; failureRibbon = failure; successRibbon = success; grandRibbon = grand;
            selfMarker = self; teammateMarker = teammate; textLayers = texts; memberLayers = members;
            acknowledge = confirm; rewardTransfer = transfer; ForceHidden();
            if (Application.isPlaying && isActiveAndEnabled) Bind();
        }
        private void Awake() { if (Application.isPlaying) ForceHidden(); }
        private void OnEnable() { if (Application.isPlaying) Bind(); UpdateStageGeometry(); }
        private void OnDisable()
        {
            bool wasVisible = visible; long disablingEpoch = ++epoch;
            Unbind(); StopAllCoroutines(); ForceHidden();
            if (Application.isPlaying && wasVisible && this != null && epoch == disablingEpoch) Hidden?.Invoke();
        }
        private void LateUpdate() { UpdateStageGeometry(); }
        private void OnRectTransformDimensionsChange() { UpdateStageGeometry(); }

        public void Open(ResultsPresentation presentation, bool takeFocus = true)
        {
            if (presentation == null) throw new ArgumentNullException("presentation");
            if (Application.isPlaying && !isActiveAndEnabled) throw new InvalidOperationException("Activate the C12 view before Open.");
            RequireConfiguration();
            ResultsPresentation copy = presentation.Copy();
            long openingEpoch = ++epoch; StopAllCoroutines(); rewardTransfer.Stop(); ClearOwnedSelection();
            if (this == null || epoch != openingEpoch) return;
            model = copy; visible = true;
            visuals.gameObject.SetActive(true); visuals.interactable = true; visuals.blocksRaycasts = true;
            Render();
            if (this == null || epoch != openingEpoch || !visible || model != copy) return;
            UpdateStageGeometry();
            if (Application.isPlaying) StartCoroutine(Fade(1, 0.16f, false)); else visuals.alpha = 1;
            // Focus is the final operation: project OnSelect callbacks may hide/reopen the view.
            if (takeFocus && Application.isPlaying && acknowledge.gameObject.activeInHierarchy && acknowledge.IsInteractable())
                acknowledge.Select();
        }
        public bool SetPresentation(ResultsPresentation presentation)
        {
            if (presentation == null) throw new ArgumentNullException("presentation");
            if (!visible || model == null || presentation.SessionId != model.SessionId) return false;
            model = presentation.Copy(); rewardTransfer.Stop(); Render();
            // Revision ordering is intentionally a project concern. Refresh never plays reward motion.
            return true;
        }
        public void Acknowledge()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !visible || model == null || dispatchingAcknowledge ||
                acknowledge == null || !acknowledge.IsActive() || !acknowledge.IsInteractable() ||
                string.IsNullOrWhiteSpace(model.AcknowledgeLabel) || !model.AcknowledgeEnabled) return;
            string sessionId = model.SessionId;
            dispatchingAcknowledge = true;
            try { AcknowledgeRequested?.Invoke(sessionId); }
            finally { dispatchingAcknowledge = false; }
            // No automatic hide, currency mutation, readiness barrier or scene transition.
        }
        public void Hide()
        {
            if (!visible) return;
            long hidingEpoch = ++epoch; visible = false; model = null;
            StopAllCoroutines(); rewardTransfer.Stop();
            visuals.interactable = false; visuals.blocksRaycasts = false;
            if (Application.isPlaying && isActiveAndEnabled) StartCoroutine(Fade(0, 0.12f, true));
            else { visuals.alpha = 0; visuals.gameObject.SetActive(false); }
            ClearOwnedSelection();
            if (Application.isPlaying && this != null && epoch == hidingEpoch) Hidden?.Invoke();
        }
        public bool PlayRewardTransfer(string expectedSessionId, int pieceCount = 5)
        {
            if (!Application.isPlaying || !visible || model == null || expectedSessionId != model.SessionId) return false;
            return rewardTransfer != null && rewardTransfer.Play(pieceCount);
        }
        public void SetRewardTransferDestination(RectTransform destination)
        { if (rewardTransfer != null) rewardTransfer.SetDestination(destination); }

        private void Render()
        {
            ribbon.sprite = model.Outcome == ResultsOutcome.Failure ? failureRibbon :
                model.Outcome == ResultsOutcome.Success ? successRibbon : grandRibbon;
            grandCrest.enabled = model.Outcome == ResultsOutcome.GrandSuccess;
            textLayers.title.text = model.Title; textLayers.context.text = model.Context;
            textLayers.subtitle.text = model.Subtitle; textLayers.progressHeading.text = model.ProgressHeading;
            textLayers.course.text = model.CourseLabel; textLayers.baseLabel.text = model.BaseLabel;
            textLayers.baseAmount.text = model.BaseAmount; textLayers.bonusLabel.text = model.BonusLabel;
            textLayers.bonusAmount.text = model.BonusAmount; textLayers.fundsLabel.text = model.FundsLabel;
            textLayers.contribution.text = model.ContributionText; textLayers.fundsAmount.text = model.FundsAmount;
            textLayers.footer.text = model.Footer; textLayers.acknowledge.text = model.AcknowledgeLabel;
            float maxX;
            maximumMarker.enabled = TryX(model.NormalizedMaximum, out maxX);
            if (maximumMarker.enabled) maximumMarker.rectTransform.anchoredPosition = new Vector2(maxX - 8, -216);
            for (int i = 0; i < memberLayers.Length; i++)
            {
                MemberLayers layer = memberLayers[i]; bool active = i < model.Members.Count;
                layer.root.gameObject.SetActive(active);
                layer.label.enabled = active; layer.distance.enabled = active; layer.number.enabled = false;
                layer.connector.enabled = false; // Connectors live in a shared back layer, outside the row root.
                if (!active) continue;
                ResultsMemberPresentation member = model.Members[i];
                float laneY = 260 + i * 40, x;
                bool hasProgress = TryX(member.NormalizedProgress, out x);
                float size = member.IsLocalPlayer ? 32 : 24;
                layer.label.text = member.Label; layer.distance.text = member.DistanceLabel;
                layer.number.text = member.Id.ToString(CultureInfo.InvariantCulture);
                layer.marker.enabled = hasProgress; layer.number.enabled = hasProgress; layer.connector.enabled = hasProgress;
                layer.marker.sprite = member.IsLocalPlayer ? selfMarker : teammateMarker; layer.marker.color = member.Color;
                if (hasProgress)
                {
                    layer.marker.rectTransform.sizeDelta = new Vector2(size, size);
                    layer.marker.rectTransform.anchoredPosition = new Vector2(x - size / 2, -laneY + size / 2);
                    layer.number.rectTransform.anchoredPosition = new Vector2(x - 16, -laneY + 14);
                    layer.connector.rectTransform.anchoredPosition = new Vector2(x - 1, -224);
                    layer.connector.rectTransform.sizeDelta = new Vector2(2, laneY - 224);
                    Color connectorColor = member.Color; connectorColor.a = 0.28f; layer.connector.color = connectorColor;
                }
                float fallX; layer.fall.enabled = TryX(member.NormalizedFall, out fallX);
                if (layer.fall.enabled)
                {
                    bool overlaps = hasProgress && Mathf.Abs(fallX - x) < size / 2 + 10;
                    float fallY = laneY + (overlaps ? 14 : 0);
                    layer.fall.rectTransform.anchoredPosition = new Vector2(fallX - 10, -fallY + 10);
                }
            }
            bool showButton = !string.IsNullOrWhiteSpace(model.AcknowledgeLabel);
            bool enableButton = model.AcknowledgeEnabled;
            long renderingEpoch = epoch;
            acknowledge.gameObject.SetActive(showButton);
            if (this == null || epoch != renderingEpoch) return;
            acknowledge.interactable = enableButton;
            if (!showButton || !enableButton) ClearOwnedSelection();
        }
        private static bool TryX(float? normalized, out float x)
        {
            x = 0;
            if (!normalized.HasValue || float.IsNaN(normalized.Value) || float.IsInfinity(normalized.Value)) return false;
            x = 256 + Mathf.Clamp01(normalized.Value) * 640; return true;
        }
        private void RequireConfiguration()
        {
            if (visuals == null || layoutStage == null || ribbon == null || grandCrest == null || maximumMarker == null ||
                failureRibbon == null || successRibbon == null || grandRibbon == null || selfMarker == null || teammateMarker == null ||
                textLayers == null || memberLayers == null || memberLayers.Length != 4 || acknowledge == null || rewardTransfer == null)
                throw new InvalidOperationException("C12 layers are incomplete. Use the ResultsPanelBuilder.");
            var t = textLayers;
            if (t.title == null || t.context == null || t.subtitle == null || t.progressHeading == null || t.course == null ||
                t.baseLabel == null || t.baseAmount == null || t.bonusLabel == null || t.bonusAmount == null || t.fundsLabel == null ||
                t.contribution == null || t.fundsAmount == null || t.footer == null || t.acknowledge == null)
                throw new InvalidOperationException("C12 TMP references are incomplete.");
            foreach (MemberLayers m in memberLayers)
                if (m == null || m.root == null || m.label == null || m.distance == null || m.number == null ||
                    m.connector == null || m.marker == null || m.fall == null)
                    throw new InvalidOperationException("C12 member layers are incomplete.");
        }
        private void Bind()
        { if (!bound && acknowledge != null) { acknowledge.onClick.AddListener(Acknowledge); bound = true; } }
        private void Unbind()
        { if (bound && acknowledge != null) acknowledge.onClick.RemoveListener(Acknowledge); bound = false; }
        private void ForceHidden()
        {
            visible = false; model = null;
            if (rewardTransfer != null) rewardTransfer.Stop();
            if (visuals != null)
            { visuals.alpha = 0; visuals.interactable = false; visuals.blocksRaycasts = false; visuals.gameObject.SetActive(false); }
            ClearOwnedSelection();
        }
        private void ClearOwnedSelection()
        {
            if (!Application.isPlaying || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(null);
        }
        private IEnumerator Fade(float target, float seconds, bool deactivate)
        {
            float from = visuals.alpha, elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds); t = t * t * (3 - 2 * t);
                visuals.alpha = Mathf.Lerp(from, target, t); yield return null;
            }
            visuals.alpha = target;
            if (deactivate) visuals.gameObject.SetActive(false);
        }
        private void UpdateStageGeometry()
        {
            if (layoutStage == null) return;
            RectTransform root = transform as RectTransform; if (root == null) return;
            Vector2 size = root.rect.size;
            if (size == lastViewport || size.x <= 0 || size.y <= 0) return;
            lastViewport = size; float scale = Mathf.Min(1, size.x / 1920, size.y / 1080);
            layoutStage.localScale = new Vector3(scale, scale, 1);
        }
    }
}
