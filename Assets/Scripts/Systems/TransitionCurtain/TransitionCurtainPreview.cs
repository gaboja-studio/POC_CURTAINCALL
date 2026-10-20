using CurtainCall.UI;
using UnityEngine;

namespace CurtainCall.TransitionCurtain
{
    public sealed class TransitionCurtainPreview : MonoBehaviour
    {
        [SerializeField] private TransitionCurtainView view;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private UnityEngine.UI.Button[] buttons;
        private UnityEngine.Events.UnityAction[] actions;
        private float originalTimeScale;
        private bool ownsTimeScale;
        private bool bright;

        public void Configure(TransitionCurtainView target, Camera camera, UnityEngine.UI.Button[] controls)
        {
            view = target;
            previewCamera = camera;
            buttons = controls;
        }
        private void OnEnable()
        {
            if (!Application.isPlaying || buttons == null) return;
            actions = new UnityEngine.Events.UnityAction[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                int action = i;
                actions[i] = () => Run(action);
                buttons[i].onClick.AddListener(actions[i]);
            }
        }
        private void OnDisable()
        {
            if (actions != null && buttons != null)
                for (int i = 0; i < actions.Length; i++)
                    if (buttons[i] != null) buttons[i].onClick.RemoveListener(actions[i]);
            actions = null;
            RestoreTime();
        }
        private void Run(int action)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || view == null) return;
            switch (action)
            {
                case 0: view.Close(); break;
                case 1: view.Open(); break;
                case 2: view.OpenImmediate(); break;
                case 3:
                    if (view.State == TransitionCurtainState.Closing || view.State == TransitionCurtainState.Covered) view.Open();
                    else view.Close();
                    break;
                case 4:
                    if (!ownsTimeScale) { originalTimeScale = Time.timeScale; ownsTimeScale = true; }
                    Time.timeScale = 0;
                    break;
                case 5: RestoreTime(); break;
                case 6:
                    bright = !bright;
                    previewCamera.backgroundColor = bright ? new Color32(224, 222, 210, 255) : new Color32(27, 35, 42, 255);
                    break;
                case 7: view.SetLoadingText("장소 전환 중 장소 전환 중 장소 전환 중 장소 전환 중"); break;
                case 8: view.SetLoadingText("장소 전환 중"); break;
            }
        }
        private void RestoreTime()
        {
            if (!ownsTimeScale) return;
            Time.timeScale = originalTimeScale;
            ownsTimeScale = false;
        }
    }
}
