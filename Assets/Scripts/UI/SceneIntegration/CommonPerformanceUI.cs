using CurtainCall.CheerGauge;
using CurtainCall.UI.PerformanceHudV1;
using UnityEngine;

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

        public void Configure(PerformanceHUDView view, C1CheerGaugeView gauge) { hud = view; cheer = gauge; }
        void OnEnable() { Current = this; Hide(); }
        void OnDisable() { if (Current == this) Current = null; }
        public void Hide()
        {
            if (hud != null) hud.ResetForRuntime();
            if (cheer != null) { cheer.SetUnavailable(); cheer.gameObject.SetActive(false); }
        }
    }
}
