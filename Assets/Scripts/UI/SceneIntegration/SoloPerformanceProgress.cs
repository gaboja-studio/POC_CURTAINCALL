using System.Globalization;
using CurtainCall.UI.PerformanceHudV1;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>정본의 2~4인 API는 유지하며 동일 씬 인스턴스의 마커로 실제 1인 명부를 표시한다.</summary>
    public sealed class SoloPerformanceProgress : MonoBehaviour
    {
        [SerializeField] PerformanceProgressView source;
        [SerializeField] RectTransform playerRoot, fireRoot;
        [SerializeField] Image playerImage, playerUnderlay, fireMarker, arrivalMarker, maximumMarker, fallMarker;
        [SerializeField] TMP_Text playerNumber, remainingText;
        [SerializeField] Sprite selfSprite, teammateSprite;
        public bool HasValidData { get; private set; }

        public bool Show(float length, float distance, bool local, float? fire, float? maximum, float? fall)
        {
            if (source == null || playerRoot == null || !float.IsFinite(length) || length <= 0 || !float.IsFinite(distance)
                || (fire.HasValue && !float.IsFinite(fire.Value)) || (maximum.HasValue && !float.IsFinite(maximum.Value))
                || (fall.HasValue && !float.IsFinite(fall.Value)))
            { if (source != null) source.SetUnavailable(); HasValidData = false; return false; }
            source.SetUnavailable();
            playerRoot.gameObject.SetActive(true);
            float size = local ? 32 : 24;
            playerRoot.sizeDelta = new Vector2(size, size);
            playerRoot.anchoredPosition = new Vector2(Position(distance, length), -30);
            playerImage.sprite = local ? selfSprite : teammateSprite;
            playerImage.color = new Color32(0x4C, 0x8D, 0xDB, 255);
            playerUnderlay.sprite = playerImage.sprite;
            playerUnderlay.rectTransform.sizeDelta = new Vector2(size + 2, size + 2);
            playerNumber.text = "1"; playerNumber.enabled = true;
            remainingText.text = local ? "남은 " + Mathf.Ceil(Mathf.Max(0, length - Mathf.Clamp(distance, 0, length))).ToString("0", CultureInfo.InvariantCulture) + " m" : "—";
            fireRoot.gameObject.SetActive(fire.HasValue);
            fireMarker.enabled = fire.HasValue;
            fireMarker.color = new Color32(0xF2, 0xB4, 0x5B, 255);
            if (fire.HasValue) fireRoot.anchoredPosition = new Vector2(Position(fire.Value, length), -16);
            arrivalMarker.enabled = true;
            maximumMarker.enabled = maximum.HasValue;
            if (maximum.HasValue) maximumMarker.rectTransform.anchoredPosition = new Vector2(Position(maximum.Value, length), -12);
            fallMarker.gameObject.SetActive(fall.HasValue);
            if (fall.HasValue) fallMarker.rectTransform.anchoredPosition = new Vector2(Position(fall.Value, length), -30);
            fireRoot.SetAsLastSibling();
            if (local) playerRoot.SetAsLastSibling();
            HasValidData = true;
            return true;
        }
        public void RemoveRemainingPrefix()
        {
            if (remainingText != null && remainingText.text.StartsWith("남은 ", System.StringComparison.Ordinal))
                remainingText.text = remainingText.text.Substring(3);
        }
        public void Clear()
        {
            if (HasValidData && source != null) source.SetUnavailable();
            HasValidData = false;
        }
        void OnDisable() { Clear(); }
        static float Position(float distance, float length) => 24 + 360 * Mathf.Clamp01(distance / length);
    }
}
