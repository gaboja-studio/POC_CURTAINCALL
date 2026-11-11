using UnityEngine;

namespace CurtainCall.Dev
{
    /// <summary>
    /// 개발자 모드가 켜져 있으면 화면 위쪽 중앙 왼편에 "DEV MODE"(시작 지점)와 치트 창 버튼을 띄운다(오른쪽 위 HUD·왼쪽 조작 패널을 가리지 않게). 씬에 배치하지 않아도 Play 시작 때 스스로 생긴다.
    /// </summary>
    public sealed class DevModeBadge : MonoBehaviour
    {
        GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (!DevMode.Enabled) return;
            var go = new GameObject(nameof(DevModeBadge));
            DontDestroyOnLoad(go);
            go.AddComponent<DevModeBadge>();
            DevStartLauncher.EnsureExists();
            DevCheatPanel.EnsureExists();
        }

        void OnGUI()
        {
            if (!DevMode.Enabled) return;
            _style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.35f, 0.3f) },
            };
            // Reserve the center for the waiting-room sign and broadcasts.
            float width = Mathf.Min(300, Screen.width * .24f);
            float x = Mathf.Max(4, Screen.width * .27f - width * .5f);
            if (GUI.Button(new Rect(x, 4, width, 24), $"시작 지점 [{DevStartLauncher.ShortcutLabel}]", _style))
                DevStartLauncher.EnsureExists()?.Toggle();
            if (GUI.Button(new Rect(x, 30, width, 24), $"치트 [{DevCheatPanel.ShortcutLabel}]", _style))
                DevCheatPanel.EnsureExists()?.Toggle();
        }
    }
}
