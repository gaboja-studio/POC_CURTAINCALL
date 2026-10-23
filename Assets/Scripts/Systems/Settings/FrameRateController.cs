using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>공통 세팅의 FPS 제한을 모든 씬과 PC 빌드에 적용한다.</summary>
    public sealed class FrameRateController : MonoBehaviour
    {
        static FrameRateController instance;
        int previousTarget;
        int previousVSync;
        int appliedTarget = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var host = new GameObject("FrameRateController");
            host.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(host);
            host.AddComponent<FrameRateController>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            previousTarget = Application.targetFrameRate;
            previousVSync = QualitySettings.vSyncCount;
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            int target = GameSettings.Base.FrameRate.TargetFrameRate;
            if (appliedTarget == target && Application.targetFrameRate == target
                && QualitySettings.vSyncCount == 0) return;
            // Desktop VSync overrides targetFrameRate, including when the limit is disabled.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = target;
            appliedTarget = target;
        }

        void OnDestroy()
        {
            if (instance != this) return;
            Application.targetFrameRate = previousTarget;
            QualitySettings.vSyncCount = previousVSync;
            instance = null;
        }
    }
}
