using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.TeammateDanger
{
    /// <summary>
    /// 위험한 동료 원형 아이콘 하나(2026-11-26 M3 결정). 글자·프로필 없이 원 안 아이콘으로 위험 종류를, 원 테두리로 남은 시간을 보인다.
    /// 화면 밖 동료는 가장자리에 놓고 방향 화살촉을 켜며, 화면 안 동료는 머리 위에 테두리 없이 놓는다.
    /// 배치·그림은 프리팹(TeammateDangerHud)에서 고치고, 값은 <see cref="TeammateDangerIndicators"/>가 넣는다.
    /// </summary>
    public sealed class TeammateDangerBadge : MonoBehaviour
    {
        [SerializeField] RectTransform root;
        [SerializeField] Image disc;
        [Tooltip("남은 시간 원 테두리. Filled · Radial 360 · 위에서 시계 방향.")]
        [SerializeField] Image ring;
        [SerializeField] Image icon;
        [Tooltip("방향 화살촉을 돌리는 축(원 중심). 자식 화살촉은 위쪽을 가리키게 둔다.")]
        [SerializeField] RectTransform pointerPivot;
        [SerializeField] CanvasGroup group;

        public RectTransform Root => root != null ? root : (RectTransform)transform;

        public void Show(Vector2 anchoredPosition, float size, Sprite sprite, Color color, float remaining, float? pointerDegrees, float alpha)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            var rect = Root;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(size, size);
            if (icon != null) { icon.sprite = sprite; icon.color = color; icon.enabled = sprite != null; }
            if (ring != null)
            {
                ring.enabled = remaining >= 0f;
                ring.color = color;
                ring.fillAmount = Mathf.Clamp01(remaining);
            }
            if (pointerPivot != null)
            {
                pointerPivot.gameObject.SetActive(pointerDegrees.HasValue);
                if (pointerDegrees.HasValue) pointerPivot.localRotation = Quaternion.Euler(0f, 0f, pointerDegrees.Value);
                foreach (var image in pointerPivot.GetComponentsInChildren<Image>(true)) image.color = color;
            }
            if (group != null) group.alpha = alpha;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
