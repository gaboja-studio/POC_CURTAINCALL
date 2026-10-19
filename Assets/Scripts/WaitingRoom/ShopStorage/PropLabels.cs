using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>기물 도형 위에 이름을 띄우는 테스트용 표시(IMGUI). 카메라 앞, 가까운 것만 그린다.</summary>
    static class PropLabels
    {
        const float MaxDistance = 7f;
        static GUIStyle style;

        /// <summary>OnGUI에서 부른다. <paramref name="world"/> 위치(m) 위에 글자를 그린다.</summary>
        public static void Draw(Vector3 world, string text)
        {
            var camera = Camera.main;
            if (camera == null || string.IsNullOrEmpty(text)) return;
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f || screen.z > MaxDistance) return;
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            var size = style.CalcSize(new GUIContent(text));
            var rect = new Rect(screen.x - size.x * .5f - 4f, Screen.height - screen.y - size.y - 2f, size.x + 8f, size.y + 2f);
            var color = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.Label(rect, text, style);
        }
    }
}
