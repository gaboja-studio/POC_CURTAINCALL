using UnityEngine;

namespace CurtainCall.Dev
{
    /// <summary>
    /// 개발자 모드가 켜져 있으면 화면 위쪽 가운데에 "DEV MODE"를 띄운다(오른쪽 위 HUD·왼쪽 조작 패널을 가리지 않게). 씬에 배치하지 않아도 Play 시작 때 스스로 생긴다.
    /// </summary>
    public sealed class DevModeBadge : MonoBehaviour
    {
        const string Label = "DEV MODE · 인원 규칙 예외";

        GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (!DevMode.Enabled) return;
            var go = new GameObject(nameof(DevModeBadge));
            DontDestroyOnLoad(go);
            go.AddComponent<DevModeBadge>();
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
            GUI.Box(new Rect(Screen.width * 0.5f - 120, 40, 240, 32), Label, _style);
        }
    }
}
