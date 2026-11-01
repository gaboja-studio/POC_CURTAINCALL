using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 상점 상세 설명 스크롤(스크롤 바 없음)에 "더 읽을 내용이 있다"는 표시를 더한다(2026-11-01 사용자).
    /// - 배치(뷰포트·설명 TMP·가격 줄 위치·화살표)는 상점 프리팹에서 고친다. 이 컴포넌트는 동작만 맡는다.
    /// - 위·아래로 더 읽을 글이 있는 쪽 가장자리만 글이 흐려진다(RectMask2D 부드러운 가장자리).
    /// - 아래에 글이 남았으면 아래 화살표가 위아래로 살짝 움직이고, 끝까지 내리면 사라진다.
    /// - 넘치는 설명을 새로 고르면 한 번 살짝 내려갔다 돌아와 스크롤된다는 것을 보여 준다. 사용자가 휠·드래그하면 바로 멈춘다.
    /// - 다른 항목을 고르면 맨 위로 돌아간다.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class ShopDescriptionScroll : MonoBehaviour, IScrollHandler, IBeginDragHandler
    {
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectMask2D mask;
        [SerializeField] TMP_Text text;

        [Header("가장자리 흐림")]
        [Tooltip("더 읽을 글이 있는 쪽 가장자리를 흐리게 하는 높이(px). 0이면 흐리지 않음")]
        [SerializeField, Min(0)] int fadeHeight = 40;
        [Tooltip("흐림이 나타나고 사라지는 빠르기(초당 px)")]
        [SerializeField, Min(1f)] float fadeSpeed = 240f;

        [Header("아래 화살표")]
        [Tooltip("아래에 글이 남았을 때 보이는 표시(없으면 쓰지 않음)")]
        [SerializeField] CanvasGroup moreIndicator;
        [SerializeField, Min(0f)] float indicatorBob = 4f;
        [SerializeField, Min(0f)] float indicatorBobSpeed = 4f;
        [SerializeField, Min(.1f)] float indicatorFadeSpeed = 6f;

        [Header("처음 고를 때 살짝 내려 보이기")]
        [SerializeField] bool nudge = true;
        [Tooltip("내려가는 거리(px)")]
        [SerializeField, Min(0f)] float nudgeDistance = 22f;
        [Tooltip("항목을 고른 뒤 기다리는 시간(초)")]
        [SerializeField, Min(0f)] float nudgeDelay = .45f;
        [Tooltip("내려갔다 돌아오는 데 걸리는 시간(초)")]
        [SerializeField, Min(.05f)] float nudgeDuration = .7f;

        string shown;
        float topPadding, bottomPadding, nudgeAt = -1f, indicatorBase;
        bool indicatorPlaced;

        /// <summary>스크롤이 필요한 만큼 넘쳤는가.</summary>
        public bool Overflows => text != null && scroll != null && Range > .5f;
        /// <summary>아래에 더 읽을 글이 남았는가(화살표 표시 조건).</summary>
        public bool HasMoreBelow => Overflows && Offset < Range - 1f;
        /// <summary>위로 지나간 글이 있는가.</summary>
        public bool HasMoreAbove => Overflows && Offset > 1f;
        /// <summary>처음 고를 때 살짝 내려 보이기가 아직 남았는가(테스트·확인용).</summary>
        public bool NudgePending => nudgeAt >= 0f;

        float Range => text.rectTransform.rect.height - ((RectTransform)scroll.viewport).rect.height;
        float Offset => scroll.content.anchoredPosition.y;

        void Reset()
        {
            scroll = GetComponent<ScrollRect>();
            mask = GetComponent<RectMask2D>();
            text = GetComponentInChildren<TMP_Text>(true);
        }

        void OnEnable() { shown = null; topPadding = bottomPadding = -fadeHeight; }

        void LateUpdate()
        {
            if (text == null || scroll == null) return;
            if (text.text != shown)
            {
                shown = text.text;
                Canvas.ForceUpdateCanvases();
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1f; // 새 항목은 맨 위부터
                nudgeAt = nudge && Overflows ? Time.unscaledTime + nudgeDelay : -1f;
            }
            Nudge();
            Fade();
            Indicator();
        }

        void Nudge()
        {
            if (nudgeAt < 0f || Time.unscaledTime < nudgeAt) return;
            float t = (Time.unscaledTime - nudgeAt) / nudgeDuration;
            float distance = Mathf.Min(nudgeDistance, Range);
            if (t >= 1f) { nudgeAt = -1f; SetOffset(0f); return; }
            SetOffset(Mathf.Sin(t * Mathf.PI) * distance);
        }

        void SetOffset(float y)
        {
            var content = scroll.content;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
            scroll.velocity = Vector2.zero;
        }

        void Fade()
        {
            if (mask == null) return;
            // 끝에 닿은 쪽은 흐린 띠를 뷰포트 밖으로 밀어(음수 여백) 글이 흐려지지 않게 한다.
            float step = fadeSpeed * Time.unscaledDeltaTime;
            topPadding = Mathf.MoveTowards(topPadding, HasMoreAbove && nudgeAt < 0f ? 0f : -fadeHeight, step);
            bottomPadding = Mathf.MoveTowards(bottomPadding, HasMoreBelow ? 0f : -fadeHeight, step);
            mask.softness = new Vector2Int(0, fadeHeight);
            mask.padding = new Vector4(0f, bottomPadding, 0f, topPadding);
        }

        void Indicator()
        {
            if (moreIndicator == null) return;
            var rect = (RectTransform)moreIndicator.transform;
            if (!indicatorPlaced) { indicatorBase = rect.anchoredPosition.y; indicatorPlaced = true; }
            moreIndicator.alpha = Mathf.MoveTowards(moreIndicator.alpha, HasMoreBelow ? 1f : 0f, indicatorFadeSpeed * Time.unscaledDeltaTime);
            float bob = Mathf.Sin(Time.unscaledTime * indicatorBobSpeed) * indicatorBob;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, indicatorBase - Mathf.Abs(bob));
        }

        // 사용자가 직접 스크롤하면 내려 보이기를 멈춘다.
        public void OnScroll(PointerEventData eventData) => nudgeAt = -1f;
        public void OnBeginDrag(PointerEventData eventData) => nudgeAt = -1f;
    }
}
