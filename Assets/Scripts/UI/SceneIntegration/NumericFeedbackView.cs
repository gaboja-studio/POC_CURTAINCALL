using CurtainCall.Settings;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Two preallocated number lanes. No card, icon, scale bounce or gameplay-centre motion.</summary>
    public sealed class NumericFeedbackView : MonoBehaviour
    {
        [SerializeField] TMP_Text positive;
        [SerializeField] TMP_Text negative;
        [SerializeField] TMP_Text notice;
        [SerializeField] TMP_Text subject;
        [SerializeField] UnityEngine.UI.Image backdrop;
        [SerializeField] UnityEngine.UI.Image frame;
        readonly NumericReactionState plus = new(), minus = new();
        float noticeUntil;
        int subjectSlot = int.MinValue;
        bool subjectSpectating;
        public bool HasRuntimePresentation => plus.Visible(Time.unscaledTime) || minus.Visible(Time.unscaledTime) || Time.unscaledTime < noticeUntil;
        public bool IsVisible => HasRuntimePresentation;
        public string LastIssue => positive == null || negative == null ? "Number labels are missing." : string.Empty;

        public void Configure(TMP_Text up, TMP_Text down, TMP_Text information)
        { positive = up; negative = down; notice = information; ResetForRuntime(); }
        public void ConfigureSubject(TMP_Text label) { subject = label; subjectSlot = int.MinValue; }
        public void ConfigureSkin(UnityEngine.UI.Image fill, UnityEngine.UI.Image border) { backdrop = fill; frame = border; }
        public void SetSubject(bool spectating, int slot)
        {
            if (subjectSlot == slot && subjectSpectating == spectating) return;
            subjectSlot = slot; subjectSpectating = spectating;
            if (subject != null) subject.text = spectating ? "P" + (slot + 1) + " · 관전 호응" : "내 호응";
        }

        public void Show(int actual, float receivedAt)
        {
            if (actual == 0 || positive == null || negative == null) return;
            var settings = GameSettings.Base.AudienceFeedback;
            var state = actual > 0 ? plus : minus;
            state.Add(actual, receivedAt, settings.CombineSeconds, settings.PopupSeconds);
            (actual > 0 ? positive : negative).text = state.Value > 0 ? "+" + state.Value : "−" + (-state.Value);
            Refresh();
        }
        public void ShowHandhold()
        {
            if (notice == null) return;
            notice.text = "손 잡아줌";
            noticeUntil = Time.unscaledTime + GameSettings.Base.AudienceFeedback.PopupSeconds;
            Refresh();
        }
        void LateUpdate() => Refresh();
        void Refresh()
        {
            var settings = GameSettings.Base.AudienceFeedback;
            var rect = (RectTransform)transform;
            bool gauge = settings.Anchor == AudienceFeedbackSettings.PopupAnchor.CheerGauge;
            bool top = GameSettings.Base.HudLayout.BalancePosition == BaseGameSettings.BalanceHudPosition.TopCenter;
            var anchor = gauge ? new Vector2(0, 1) : new Vector2(.5f, top ? 1 : 0);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = gauge ? new Vector2(218, -96) : new Vector2(224, top ? -80 : 96);
            float now = Time.unscaledTime;
            bool both = plus.Visible(now) && minus.Visible(now);
            rect.sizeDelta = new Vector2(184, both ? 152 : 104);
            Render(positive, plus, now, -65, settings.FontSize);
            Render(negative, minus, now, plus.Visible(now) ? -113 : -65, settings.FontSize);
            float opacity = Mathf.Max(positive != null ? positive.alpha : 0, negative != null ? negative.alpha : 0);
            if (subject != null) subject.alpha = opacity;
            if (backdrop != null) backdrop.color = new Color(1, 1, 1, opacity * .96f);
            if (frame != null) frame.color = new Color(.82f, .83f, .78f, opacity);
            if (notice != null) notice.alpha = now < noticeUntil ? Mathf.Clamp01((noticeUntil - now) / .2f) : 0;
        }
        static void Render(TMP_Text label, NumericReactionState state, float now, float y, float size)
        {
            if (label == null) return;
            label.fontSize = size;
            label.rectTransform.anchoredPosition = new Vector2(0, y);
            label.alpha = state.Visible(now) ? Mathf.Clamp01((state.Expires - now) / .22f) : 0;
        }
        public void ResetForRuntime()
        {
            plus.Clear(); minus.Clear(); noticeUntil = 0;
            if (positive != null) { positive.text = ""; positive.alpha = 0; }
            if (negative != null) { negative.text = ""; negative.alpha = 0; }
            if (notice != null) { notice.text = ""; notice.alpha = 0; }
            if (subject != null) subject.alpha = 0;
            if (backdrop != null) backdrop.color = Color.clear;
            if (frame != null) frame.color = Color.clear;
        }
        void OnDisable() => ResetForRuntime();
    }
}
