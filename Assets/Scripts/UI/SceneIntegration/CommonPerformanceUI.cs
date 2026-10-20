using CurtainCall.CheerGauge;
using CurtainCall.UI.PerformanceHudV1;
using UnityEngine;
using CurtainCall.UI.GuidesV1;
using CurtainCall.UI.TooltipsV1;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>공통 UI 씬의 표시 참조. 공간 오브젝트를 직렬화 참조하지 않는다.</summary>
    public sealed class CommonPerformanceUI : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/UI/CommonPerformanceUI.unity";
        public static CommonPerformanceUI Current { get; private set; }
        [SerializeField] PerformanceHUDView hud;
        [SerializeField] C1CheerGaugeView cheer;
        public PerformanceHUDView Hud => hud;
        public C1CheerGaugeView Cheer => cheer;
        [SerializeField] PerformanceFeedback feedback;
        public PerformanceFeedback Feedback => feedback;
        public void ConfigureFeedback(PerformanceFeedback value) { feedback = value; }
        [SerializeField] GuideView guide;
        [SerializeField] TooltipView tooltip;
        public GuideView Guide => guide;
        public TooltipView Tooltip => tooltip;
        MonoBehaviour hintOwner;
        public void ClaimHints(MonoBehaviour owner) { if (hintOwner != owner) { HideHints(); hintOwner = owner; } }
        public void ReleaseHints(MonoBehaviour owner) { if (hintOwner == owner) { HideHints(); hintOwner = null; } }
        public void ConfigureHints(GuideView guideView, TooltipView tooltipView) { guide = guideView; tooltip = tooltipView; }
        public void HideHints()
        {
            if (guide != null) guide.ResetForRuntime();
            if (tooltip != null) tooltip.ResetForRuntime();
        }

        public void Configure(PerformanceHUDView view, C1CheerGaugeView gauge) { hud = view; cheer = gauge; }
        void OnEnable() { Current = this; Hide(); }
        void OnDisable() { if (Current == this) Current = null; }
        public void Hide()
        {
            HideHints();
            if (feedback != null) { feedback.SetContext(false); feedback.Clear(); }
            if (hud != null) hud.ResetForRuntime();
            if (cheer != null) { cheer.SetUnavailable(); cheer.gameObject.SetActive(false); }
        }
    }
}
