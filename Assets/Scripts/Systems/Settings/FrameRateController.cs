using CurtainCall.StartLobby;
using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>시작할 때 이 PC의 FPS 설정을 적용한다. 이후 변경은 ClientSettings가 직접 적용한다.</summary>
    public sealed class FrameRateController : MonoBehaviour
    {
        static FrameRateController instance;
        int previousTarget;
        int previousVSync;

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
            // 공통 GameSettings의 개발용 FPS 값으로 로컬 선택을 덮어쓰지 않는다.
            ClientSettings.ApplyFrameRate();
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
