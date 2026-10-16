using CurtainCall.Dev;
using CurtainCall.SpaceLink;
using UnityEngine;

namespace CurtainCall.DummySpaces
{
    /// <summary>
    /// 개발자 모드 전용 상세 로그(IMGUI). 조작은 모두 Canvas에 있고 여기서는 읽기만 한다.
    /// 더미 공간의 권한·변수·원시 API 기록과, 장소 전환이 켜져 있으면 전환 로그를 함께 보여 준다.
    /// </summary>
    public sealed class DummySpaceDevLog : MonoBehaviour
    {
        const float Width = 460f;

        DummySpaceCanvas canvas;
        Vector2 scroll;
        bool collapsed = true;
        string text = "";
        float nextRead;

        void Start() => canvas = GetComponent<DummySpaceCanvas>();

        void Update()
        {
            if (!DevMode.Enabled || collapsed || Time.unscaledTime < nextRead) return;
            nextRead = Time.unscaledTime + 0.25f;
            text = canvas != null ? canvas.DescribeDetails() : "";
            var director = SpaceLinkDirector.Instance;
            if (director != null) text = $"장소 전환 로그\n{string.Join("\n", director.Log)}\n\n{text}";
        }

        void OnGUI()
        {
            if (!DevMode.Enabled) return;
            float height = collapsed ? 34f : Mathf.Min(Screen.height * 0.55f, 560f);
            GUILayout.BeginArea(new Rect(Screen.width - Width - 10f, Screen.height * 0.2f, Width, height), GUI.skin.box);
            if (GUILayout.Button(collapsed ? "개발자 로그 펼치기" : "개발자 로그 접기")) collapsed = !collapsed;
            if (!collapsed)
            {
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.Label(text);
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }
    }
}
