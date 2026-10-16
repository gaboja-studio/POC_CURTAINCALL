using CurtainCall.Dev;
using UnityEngine;

namespace CurtainCall.SpaceLink
{
    /// <summary>
    /// 개발자 모드 전용 장소 전환 로그(IMGUI, <see cref="SpaceLinkHub"/>가 붙인다, 씬이 바뀌어도 유지).
    /// 조작은 Canvas에 있다. 더미 공간 안에서는 더미 공간 개발자 로그가 이 로그를 함께 보여 주므로 시작 로비에서만 그린다.
    /// </summary>
    public sealed class SpaceLinkDevLog : MonoBehaviour
    {
        const float Width = 460f;

        SpaceLinkDirector director;
        bool collapsed = true;

        void Start() => director = GetComponent<SpaceLinkDirector>();

        void OnGUI()
        {
            if (!DevMode.Enabled || director == null || director.Current != SpaceId.Hub) return;
            float height = collapsed ? 34f : 260f;
            GUILayout.BeginArea(new Rect(Screen.width - Width - 10f, Screen.height * 0.2f, Width, height), GUI.skin.box);
            if (GUILayout.Button(collapsed ? "장소 전환 로그 펼치기" : "장소 전환 로그 접기")) collapsed = !collapsed;
            if (!collapsed)
                foreach (var line in director.Log)
                    GUILayout.Label(line);
            GUILayout.EndArea();
        }
    }
}
