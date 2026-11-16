using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Always-visible build identity, independent of gameplay and microphone scenes.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class BuildVersionUI : MonoBehaviour
    {
        static BuildVersionUI instance;
        public static string CurrentText => Application.version + "_" +
            (Application.isEditor || Debug.isDebugBuild ? "TEST" : "LIVE");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstance() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var existing = FindFirstObjectByType<BuildVersionUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                return;
            }
            new GameObject("BuildVersionHUD", typeof(RectTransform), typeof(BuildVersionUI));
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);

            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32700;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var rect = new GameObject("VersionLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            // Common microphone is at (24, 70), 72x36. Keep the build stamp below it.
            rect.anchoredPosition = new Vector2(24, 18);
            rect.sizeDelta = new Vector2(320, 24);
            var label = rect.GetComponent<TextMeshProUGUI>();
            label.font = Resources.Load<TMP_FontAsset>("Fonts/SettingsUi/Pretendard-SemiBold SDF") ?? TMP_Settings.defaultFontAsset;
            label.fontSize = 16;
            label.color = new Color32(211, 211, 199, 205);
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = CurrentText;
            // No GraphicRaycaster: the stamp must never intercept microphone/menu input.
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
