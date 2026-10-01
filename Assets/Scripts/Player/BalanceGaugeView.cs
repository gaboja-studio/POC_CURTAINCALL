using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 화면 하단 균형 게이지(초록·노랑·빨강 + 바늘). 테스트용 표시이며 정식 UI가 생기면 대체한다.
    /// 화면 예시: Docs/References/ref_001.png
    /// </summary>
    public class BalanceGaugeView : MonoBehaviour
    {
        [Tooltip("표시할 플레이어. 비워 두면 씬에서 처음 찾은 플레이어를 쓴다.")]
        [SerializeField] PlayerBalance target;

        [Tooltip("화면 너비 대비 게이지 너비.")]
        [SerializeField, Range(0.1f, 1f)] float widthRatio = 0.4f;

        [SerializeField, Min(4f)] float height = 28f;

        [Tooltip("화면 아래에서 띄우는 거리(px).")]
        [SerializeField, Min(0f)] float bottomMargin = 40f;

        [SerializeField] Color greenColor = new Color(0.25f, 0.68f, 0.35f);
        [SerializeField] Color yellowColor = new Color(0.9f, 0.65f, 0.2f);
        [SerializeField] Color redColor = new Color(0.82f, 0.25f, 0.2f);
        [SerializeField] Color needleColor = Color.white;

        GUIStyle captionStyle;

        void Update()
        {
            if (target == null)
                target = FindAnyObjectByType<PlayerBalance>();
        }

        void OnGUI()
        {
            if (target == null) return;

            float width = Screen.width * widthRatio;
            var bar = new Rect((Screen.width - width) * 0.5f, Screen.height - bottomMargin - height, width, height);
            float half = width * 0.5f;
            float center = bar.center.x;

            // 빨강 → 노랑 → 초록 순서로 겹쳐 그린다
            Fill(bar, redColor);
            Fill(Band(center, half * target.YellowLimit, bar), yellowColor);
            Fill(Band(center, half * target.GreenLimit, bar), greenColor);

            float needleX = center + half * target.Value;
            Fill(new Rect(needleX - 2f, bar.y - 6f, 4f, bar.height + 12f), needleColor);

            captionStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            var caption = new Rect(bar.x, bar.yMax + 2f, bar.width, 20f);
            GUI.Label(caption, $"◀ A        균형 {target.Value:+0.00;-0.00; 0.00}  ({target.Zone})        D ▶", captionStyle);
        }

        static Rect Band(float center, float halfWidth, Rect bar) =>
            new Rect(center - halfWidth, bar.y, halfWidth * 2f, bar.height);

        static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
