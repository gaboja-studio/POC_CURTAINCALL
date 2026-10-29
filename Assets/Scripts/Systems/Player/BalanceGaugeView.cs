using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.Player
{
    /// <summary>
    /// 균형 게이지 UI(빨강 | 노랑 | 초록 | 노랑 | 빨강 + 바늘)에 균형 값을 반영한다.
    /// 모양(스프라이트·색·크기·위치)은 프리팹에서 정하고, 이 스크립트는 바늘 위치·초록 폭·빨강 체류 막대만 움직인다.
    /// 프리팹: Assets/Resources/Prefabs/UIs/Player/BalanceGauge.prefab · 규칙: Harness/Project/Decisions/tightrope-balance.md
    /// </summary>
    public class BalanceGaugeView : MonoBehaviour
    {
        [Tooltip("표시할 플레이어. 비워 두면 씬에서 처음 찾은 플레이어를 쓴다(Auto Find Target이 켜져 있을 때).")]
        [SerializeField] PlayerBalance target;

        [Tooltip("대상이 비었을 때 씬에서 처음 찾은 플레이어를 쓸지. 온라인에서는 내 캐릭터를 넣는 쪽이 끈다.")]
        [SerializeField] bool autoFindTarget = true;

        [Tooltip("초록 구간. 부모(게이지 막대) 기준 가로 앵커로 폭을 맞춘다.")]
        [SerializeField] RectTransform greenZone;

        [Tooltip("노란 경고 구간. 기존 프리팹에서 비어 있으면 초록 뒤에 자동으로 생성한다.")]
        [SerializeField] RectTransform warningZone;

        [Tooltip("바늘. 부모(게이지 막대) 기준 가로 앵커로 위치를 맞춘다.")]
        [SerializeField] RectTransform needle;

        [Tooltip("누적 추락 위험 표시 묶음. 착지·노랑·빨강 위험이 남아 있으면 보인다(선택).")]
        [SerializeField] GameObject redTimerRoot;

        [Tooltip("빨강 체류 막대. 부모 기준 가로 앵커 0~비율로 채운다(선택).")]
        [SerializeField] RectTransform redTimerFill;

        [Tooltip("균형 숫자 표시(선택).")]
        [SerializeField] TMP_Text valueLabel;

        Canvas canvas;
        GameObject generatedWarning;

        /// <summary>표시할 플레이어. 온라인에서는 내 캐릭터를 넣는다.</summary>
        public PlayerBalance Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>대상이 비었을 때 씬에서 처음 찾은 플레이어를 쓸지.</summary>
        public bool AutoFindTarget
        {
            get => autoFindTarget;
            set => autoFindTarget = value;
        }

        void Update()
        {
            if (target == null && autoFindTarget)
                target = FindAnyObjectByType<PlayerBalance>();

            // 균형이 꺼져 있으면(줄 밖) 게이지를 숨긴다
            if (canvas == null) canvas = GetComponent<Canvas>();
            bool show = target != null && target.IsActive;
            if (canvas != null) canvas.enabled = show;
            if (!show) return;

            EnsureWarningZone();
            if (warningZone != null)
            {
                float half = 0.5f * target.WarningLimit / PlayerBalance.MaxValue;
                SetHorizontalAnchors(warningZone, 0.5f - half, 0.5f + half);
                warningZone.gameObject.SetActive(target.WarningLimit > target.GreenLimit);
            }

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

            float redRatio = target.RedTimeRatio;
            if (redTimerRoot != null)
                redTimerRoot.SetActive(redRatio > 0f);
            if (redTimerFill != null)
            {
                redTimerFill.anchorMin = new Vector2(0f, redTimerFill.anchorMin.y);
                redTimerFill.anchorMax = new Vector2(redRatio, redTimerFill.anchorMax.y);
                redTimerFill.offsetMin = new Vector2(0f, redTimerFill.offsetMin.y);
                redTimerFill.offsetMax = new Vector2(0f, redTimerFill.offsetMax.y);
            }

            if (valueLabel != null)
            {
                valueLabel.text = target.HasFallen
                    ? "추락!"
                    : target.Zone == BalanceZone.Red
                        ? $"균형 {target.Value:+0;-0;0}  빨강 {target.RedTime:0.0} / {target.FallTime:0.0}s"
                        : target.Zone == BalanceZone.Yellow
                            ? $"균형 {target.Value:+0;-0;0}  경고 {target.RedTime:0.0} / {target.FallTime:0.0}s · 천천히 누적"
                            : $"균형 {target.Value:+0;-0;0}";
            }
        }

        void EnsureWarningZone()
        {
            if (warningZone != null || greenZone == null || greenZone.parent == null) return;
            // 기존 씬·프리팹에도 적용한다. 초록 아래, 바늘 아래에 두어 입력과 가독성을 방해하지 않는다.
            generatedWarning = new GameObject("WarningZone", typeof(RectTransform), typeof(Image));
            generatedWarning.hideFlags = HideFlags.DontSave;
            warningZone = (RectTransform)generatedWarning.transform;
            warningZone.SetParent(greenZone.parent, false);
            warningZone.SetSiblingIndex(greenZone.GetSiblingIndex());
            warningZone.anchorMin = Vector2.zero;
            warningZone.anchorMax = Vector2.one;
            warningZone.offsetMin = warningZone.offsetMax = Vector2.zero;
            var image = generatedWarning.GetComponent<Image>();
            image.color = new Color(1f, 0.75f, 0.08f, 1f);
            image.raycastTarget = false;
        }

        void OnDestroy()
        {
            if (generatedWarning == null) return;
            if (Application.isPlaying) Destroy(generatedWarning);
            else DestroyImmediate(generatedWarning);
        }

        static void SetHorizontalAnchors(RectTransform rect, float min, float max)
        {
            rect.anchorMin = new Vector2(min, rect.anchorMin.y);
            rect.anchorMax = new Vector2(max, rect.anchorMax.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
        }
    }
}
