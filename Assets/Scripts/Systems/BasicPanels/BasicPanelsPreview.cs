using UnityEngine;
using CurtainCall.UI.PanelsV1;

namespace CurtainCall.BasicPanels
{
    public sealed class BasicPanelsPreview : MonoBehaviour
    {
        [SerializeField] CurtainCallMenuPanels panels;
        [SerializeField] Font previewFont;
        int settingsCount;
        int leaveCount;

        public void Configure(CurtainCallMenuPanels controller, Font font)
        {
            panels = controller;
            previewFont = font;
        }

        void OnEnable()
        {
            if (panels == null) return;
            panels.OnSettingsRequested.AddListener(CountSettings);
            panels.OnLeaveConfirmed.AddListener(CountLeave);
        }

        void OnDisable()
        {
            if (panels == null) return;
            panels.OnSettingsRequested.RemoveListener(CountSettings);
            panels.OnLeaveConfirmed.RemoveListener(CountLeave);
        }

        void CountSettings() { settingsCount++; }
        void CountLeave() { leaveCount++; }

        void OnGUI()
        {
            if (panels == null) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUISkin previousSkin = GUI.skin;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1));
            var label = new GUIStyle(GUI.skin.label) { font = previewFont, fontSize = 18 };
            var button = new GUIStyle(GUI.skin.button) { font = previewFont, fontSize = 18 };
            try
            {
                GUILayout.BeginArea(new Rect(16, 900, 1200, 164), GUI.skin.box);
                GUILayout.Label("패널 확인 전용 · 실제 설정/방 이탈/사망 기능은 연결하지 않았습니다.", label);
                GUILayout.Label($"설정 신호: {settingsCount} / 나가기 확정 신호: {leaveCount}", label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("메뉴 다시 열기", button, GUILayout.Width(200), GUILayout.Height(40))) panels.Show();
                if (GUILayout.Button("모두 닫기", button, GUILayout.Width(160), GUILayout.Height(40))) panels.Hide();
                if (GUILayout.Button("카운터 초기화", button, GUILayout.Width(200), GUILayout.Height(40)))
                    settingsCount = leaveCount = 0;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
            finally
            {
                GUI.skin = previousSkin;
                GUI.matrix = previousMatrix;
            }
        }
    }
}
