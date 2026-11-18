using System.Reflection;
using CurtainCall.UI.BackstageHudV1;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 소지 칸 키 글자(2026-11-18 사용자).
    /// - 평소에는 칸 번호(1·2)를 흰 글자로 보이고, 그 칸의 공연 기물을 지금 쓸 수 있을 때만 같은 자리에서 사용키(Q·E, 설정에서 바꾼 키)로
    ///   부드럽게 바뀌며 노란 글자가 된다. 쓸 수 없게 되면 다시 칸 번호로 돌아간다.
    /// - 키 이름이 길면 글자 크기를 줄여 칸 안에 넣고, 최소 크기로도 넘치면 "..."으로 줄인다(글꼴에 "…" 글자가 없어 마침표 세 개를 쓴다).
    /// 슬롯 템플릿(외부 키트)의 키 TMP에 실행 중 붙는다. 색·전환 시간·글자 크기는 인스펙터에서 고칠 수 있다.
    /// </summary>
    public sealed class InventorySlotKeyLabel : MonoBehaviour
    {
        static readonly FieldInfo KeyTextField = typeof(InventorySlotView).GetField("keyText", BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField] Color slotColor = Color.white;
        [SerializeField] Color useColor = new(1f, .8f, .2f, 1f);
        [SerializeField, Min(0f)] float fadeOutSeconds = .1f, fadeInSeconds = .14f;
        [Tooltip("바뀔 때 글자가 움직이는 거리(UI 단위). 나갈 때 위로, 들어올 때 아래에서 올라온다")]
        [SerializeField] float slide = 6f;
        [Tooltip("긴 키 이름일 때 줄일 수 있는 가장 작은 글자 크기(원래 크기 대비 비율)")]
        [SerializeField, Range(.2f, 1f)] float minFontScale = .45f;

        TMP_Text label;
        float baseSize;
        Vector2 basePosition;
        string shownText;
        bool shownUse, initialized;
        Sequence tween;

        /// <summary>칸의 키 글자 담당(없으면 키 TMP에 붙인다). 키 TMP를 못 찾으면 null.</summary>
        public static InventorySlotKeyLabel For(InventorySlotView slot)
        {
            if (slot == null) return null;
            var text = KeyTextField?.GetValue(slot) as TMP_Text;
            if (text == null) return null;
            var label = text.GetComponent<InventorySlotKeyLabel>();
            if (label == null) { label = text.gameObject.AddComponent<InventorySlotKeyLabel>(); label.Bind(text); }
            return label;
        }

        void Bind(TMP_Text text)
        {
            label = text;
            baseSize = text.fontSize;
            basePosition = text.rectTransform.anchoredPosition;
        }

        /// <summary>
        /// 칸 번호 또는 사용키를 보인다. 칸 데이터가 갱신될 때마다(외부 키트가 글자를 칸 번호로 다시 쓴 뒤) 부른다.
        /// 보이는 내용이 바뀔 때만 전환 연출을 하고, 그 밖에는 지금 상태를 그대로 다시 적용한다.
        /// </summary>
        public void Show(string slotText, string useKey, bool useReady)
        {
            if (label == null) return;
            bool use = useReady && !string.IsNullOrWhiteSpace(useKey);
            string text = use ? useKey : slotText ?? "";
            if (!initialized || !isActiveAndEnabled)
            {
                initialized = true;
                Apply(text, use);
                return;
            }
            if (text == shownText && use == shownUse)
            {
                // 외부 키트가 칸 번호로 덮어썼을 수 있다. 진행 중 연출이 아니면 지금 상태를 다시 쓴다.
                if (tween == null || !tween.IsActive()) Apply(text, use);
                return;
            }
            shownText = text; shownUse = use;
            tween?.Kill();
            var rect = label.rectTransform;
            label.alpha = Mathf.Max(label.alpha, 0f);
            tween = DOTween.Sequence().SetTarget(this).SetUpdate(true)
                .Append(DOTween.To(() => label.alpha, a => label.alpha = a, 0f, fadeOutSeconds).SetEase(Ease.InQuad))
                .Join(rect.DOAnchorPos(basePosition + Vector2.up * slide, fadeOutSeconds).SetEase(Ease.InQuad))
                .AppendCallback(() => { Write(text, use); label.alpha = 0f; rect.anchoredPosition = basePosition - Vector2.up * slide; })
                .Append(DOTween.To(() => label.alpha, a => label.alpha = a, 1f, fadeInSeconds).SetEase(Ease.OutQuad))
                .Join(rect.DOAnchorPos(basePosition, fadeInSeconds).SetEase(Ease.OutQuad))
                .OnKill(() => { if (label != null) { label.alpha = 1f; rect.anchoredPosition = basePosition; } });
        }

        void Apply(string text, bool use)
        {
            shownText = text; shownUse = use;
            tween?.Kill(); tween = null;
            Write(text, use);
            label.alpha = 1f;
            label.rectTransform.anchoredPosition = basePosition;
        }

        // 칸 안에 맞춘다: 글자 크기를 줄이고, 최소 크기로도 넘치면 뒤를 "..."으로 줄인다.
        void Write(string text, bool use)
        {
            label.color = use ? useColor : slotColor;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = false;
            label.overflowMode = TextOverflowModes.Overflow;
            float width = label.rectTransform.rect.width;
            float size = baseSize;
            if (width > 1f && !string.IsNullOrEmpty(text))
            {
                float needed = label.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x * (size / Mathf.Max(.01f, label.fontSize));
                if (needed > width) size = Mathf.Max(baseSize * minFontScale, baseSize * width / needed);
                label.fontSize = size;
                if (label.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x > width)
                {
                    string cut = text;
                    while (cut.Length > 1 && label.GetPreferredValues(cut + "...", float.PositiveInfinity, float.PositiveInfinity).x > width) cut = cut.Substring(0, cut.Length - 1);
                    text = cut.TrimEnd() + "...";
                }
            }
            else label.fontSize = baseSize;
            label.text = text;
        }

        void OnDisable()
        {
            tween?.Kill(); tween = null;
            if (label != null) { label.alpha = 1f; label.rectTransform.anchoredPosition = basePosition; }
        }
    }
}
