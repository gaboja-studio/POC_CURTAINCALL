using CurtainCall.UI.ButtonsV1;
using UnityEngine;
using UnityEngine.Events;

namespace CurtainCall.CommonButtons
{
    /// <summary>확인 씬 전용 조작과 클릭 계수. 게임 콜백은 연결하지 않는다.</summary>
    public sealed class CommonButtonsPreview : MonoBehaviour
    {
        [SerializeField] CurtainCallButton[] buttons;
        [SerializeField] Font controlFont;
        UnityAction[] listeners;
        int[] counts;
        bool disabled;

        public void Configure(CurtainCallButton[] targets, Font font)
        {
            buttons = targets;
            controlFont = font;
        }

        void OnEnable()
        {
            counts = new int[buttons.Length];
            listeners = new UnityAction[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                listeners[i] = () => counts[index]++;
                buttons[i].onClick.AddListener(listeners[i]);
            }
        }

        void OnDisable()
        {
            if (listeners == null) return;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null) buttons[i].onClick.RemoveListener(listeners[i]);
        }

        void OnGUI()
        {
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1));
            var style = new GUIStyle(GUI.skin.label) { font = controlFont, fontSize = 16 };
            var buttonStyle = new GUIStyle(GUI.skin.button) { font = controlFont, fontSize = 16 };
            GUILayout.BeginArea(new Rect(16, 890, 1000, 180));
            GUILayout.Label("공용 버튼 확인 · 마우스 / 좌우 방향키 / Enter 또는 Space", style);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(disabled ? "모두 활성화" : "모두 비활성화", buttonStyle, GUILayout.Width(160)))
            {
                disabled = !disabled;
                foreach (var button in buttons) button.interactable = !disabled;
            }
            if (GUILayout.Button("클릭 횟수 초기화", buttonStyle, GUILayout.Width(160)))
                System.Array.Clear(counts, 0, counts.Length);
            GUILayout.EndHorizontal();
            for (int i = 0; i < buttons.Length; i++)
                GUILayout.Label(buttons[i].name + " : " + counts[i], style);
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }
    }
}
