using TMPro;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 균형 게이지 UI(빨강 | 초록 | 빨강 + 바늘)에 균형 값을 반영한다.
    /// 모양(스프라이트·색·크기·위치)은 프리팹에서 정하고, 이 스크립트는 바늘 위치와 초록 폭만 움직인다.
    /// 프리팹: Assets/Resources/Prefabs/UIs/Player/BalanceGauge.prefab · 규칙: Harness/Project/Decisions/tightrope-balance.md
    /// </summary>
    public class BalanceGaugeView : MonoBehaviour
    {
        [Tooltip("표시할 플레이어. 비워 두면 씬에서 처음 찾은 플레이어를 쓴다.")]
        [SerializeField] PlayerBalance target;

        [Tooltip("초록 구간. 부모(게이지 막대) 기준 가로 앵커로 폭을 맞춘다.")]
        [SerializeField] RectTransform greenZone;

        [Tooltip("바늘. 부모(게이지 막대) 기준 가로 앵커로 위치를 맞춘다.")]
        [SerializeField] RectTransform needle;

        [Tooltip("균형 숫자 표시(선택).")]
        [SerializeField] TMP_Text valueLabel;

        Canvas canvas;

        void Update()
        {
            if (target == null)
                target = FindAnyObjectByType<PlayerBalance>();

            // 균형이 꺼져 있으면(줄 밖) 게이지를 숨긴다
            if (canvas == null) canvas = GetComponent<Canvas>();
            bool show = target != null && target.IsActive;
            if (canvas != null) canvas.enabled = show;
            if (!show) return;

            if (greenZone != null)
            {
                float half = 0.5f * target.GreenLimit / PlayerBalance.MaxValue;
                SetHorizontalAnchors(greenZone, 0.5f - half, 0.5f + half);
            }

            if (needle != null)
            {
                float x = 0.5f + 0.5f * target.Value / PlayerBalance.MaxValue;
                SetHorizontalAnchors(needle, x, x);
            }

            if (valueLabel != null)
                valueLabel.text = $"균형 {target.Value:+0;-0;0}  ({target.Zone})";
        }

        static void SetHorizontalAnchors(RectTransform rect, float min, float max)
        {
            rect.anchorMin = new Vector2(min, rect.anchorMin.y);
            rect.anchorMax = new Vector2(max, rect.anchorMax.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
        }
    }
}
