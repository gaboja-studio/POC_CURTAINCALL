using CurtainCall.Reward;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>공연명이 주인공인 티켓. 슬롯과 난이도 화살표는 독립 Button으로 입력을 받는다.</summary>
    public sealed class ScheduleTicketCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public Button slotButton, previousButton, nextButton;
        public TextMeshProUGUI performanceName, slotLabel, hint, tierLabel, rewards, feedback;
        public Image hoverOutline, lockIcon, successIcon;
        bool hovered;
        float feedbackUntil;
        string idleFeedback;
        bool tierLocked;
        static readonly Color Ink = new Color32(62, 24, 23, 255);
        static readonly Color[] TierColors = { new Color32(25, 76, 70, 255), new Color32(128, 88, 24, 255), new Color32(126, 37, 43, 255) };

        public void Present(int slot, string name, int tier, int maximum, bool canChangeShow, bool canChangeTier, bool host, bool focused)
        {
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(524, 280);
            rect.anchoredPosition = new Vector2(0, -304 * slot);
            ((RectTransform)transform.parent).sizeDelta = new Vector2(524, 584);
            performanceName.text = name;
            performanceName.color = Ink;
            slotLabel.text = $"공연 {slot + 1}";
            hint.text = canChangeShow ? "슬롯 클릭으로 공연 변경" : host ? "변경 불가" : "호스트가 공연을 변경합니다";
            tierLabel.text = $"{tier}단계";
            tierLabel.color = TierColors[Mathf.Clamp(tier - 1, 0, 2)];
            var rules = RewardRules.Temporary(tier);
            rewards.text = $"호응 상한 {rules.Cap}   |   완주 보너스 {rules.CompletionBonus}";
            slotButton.interactable = focused && canChangeShow;
            previousButton.interactable = focused && canChangeTier && tier > 1;
            nextButton.interactable = focused && canChangeTier && tier < maximum;
            tierLocked = !canChangeTier;
            lockIcon.gameObject.SetActive(tierLocked && Time.unscaledTime >= feedbackUntil);
            idleFeedback = !host ? "호스트 전용" : !canChangeTier ? "난이도 고정" : tier >= maximum && maximum < 3 ? $"{maximum + 1}단계 미해금" : "";
            if (Time.unscaledTime >= feedbackUntil) feedback.text = idleFeedback;
            UpdateVisuals();
        }

        public void Applied(string text)
        {
            feedback.text = text;
            feedbackUntil = Time.unscaledTime + 1.5f;
            lockIcon.gameObject.SetActive(false);
            UpdateVisuals();
        }

        void Update()
        {
            if (feedbackUntil > 0 && Time.unscaledTime >= feedbackUntil)
            {
                feedbackUntil = 0;
                feedback.text = idleFeedback;
                lockIcon.gameObject.SetActive(tierLocked);
            }
            UpdateVisuals();
        }
        void UpdateVisuals()
        {
            if (hoverOutline != null) hoverOutline.enabled = (hovered && slotButton.IsInteractable()) || Time.unscaledTime < feedbackUntil;
            if (successIcon != null) successIcon.enabled = Time.unscaledTime < feedbackUntil;
        }
        public void OnPointerEnter(PointerEventData e) => TrackPointer(e);
        public void OnPointerMove(PointerEventData e) => TrackPointer(e);
        void TrackPointer(PointerEventData e)
        {
            var hit = e.pointerCurrentRaycast.gameObject;
            hovered = hit != null && hit.GetComponentInParent<Button>() == slotButton;
            UpdateVisuals();
        }
        public void OnPointerExit(PointerEventData e) { hovered = false; UpdateVisuals(); }
        void OnDisable() { hovered = false; feedbackUntil = 0; }
    }
}
